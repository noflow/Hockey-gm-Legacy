using LegacyEngine.Development;
using LegacyEngine.Draft;
using LegacyEngine.Events;
using LegacyEngine.People;
using LegacyEngine.Rosters;
using LegacyEngine.Staff;

namespace LegacyEngine.Integration;

public sealed class PlayerLifeCycleService
{
    public NewGmScenarioSnapshot EnsureLifeCycle(NewGmScenarioSnapshot scenario, EngineRegistry? registry = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var existingMilestones = scenario.PlayerMilestones
            .GroupBy(milestone => milestone.MilestoneId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var existingAchievements = scenario.PlayerAchievements
            .GroupBy(achievement => achievement.AchievementId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var states = new List<PlayerCareerState>();
        var summaries = new List<PlayerCareerSummary>();
        var milestones = existingMilestones.Values.ToList();
        var achievements = existingAchievements.Values.ToList();
        var timeline = scenario.CareerTimeline;
        var news = scenario.PlayerLifeCycleNews.ToList();

        foreach (var player in CollectPlayers(scenario))
        {
            var context = BuildContext(scenario, player);
            var state = BuildCareerState(scenario, context);
            var generatedMilestones = BuildMilestones(scenario, context, state).ToArray();
            foreach (var milestone in generatedMilestones)
            {
                if (existingMilestones.ContainsKey(milestone.MilestoneId))
                {
                    continue;
                }

                milestones.Add(milestone);
                existingMilestones[milestone.MilestoneId] = milestone;
                timeline = timeline.Add(ToTimelineEntry(scenario, milestone));
                QueueMilestoneEvent(registry, scenario, milestone);
                if (milestone.IsNotable)
                {
                    var transaction = ToLeagueNews(scenario, milestone);
                    if (news.All(item => item.TransactionId != transaction.TransactionId))
                    {
                        news.Add(transaction);
                    }
                }
            }

            var generatedAchievements = BuildAchievements(scenario, context, state, generatedMilestones).ToArray();
            foreach (var achievement in generatedAchievements)
            {
                if (existingAchievements.ContainsKey(achievement.AchievementId))
                {
                    continue;
                }

                achievements.Add(achievement);
                existingAchievements[achievement.AchievementId] = achievement;
                timeline = timeline.Add(ToTimelineEntry(scenario, achievement));
            }

            var allPlayerMilestones = milestones
                .Where(milestone => milestone.PersonId == context.Person.PersonId)
                .OrderBy(milestone => milestone.Date)
                .ToArray();
            var allPlayerAchievements = achievements
                .Where(achievement => achievement.PersonId == context.Person.PersonId)
                .OrderBy(achievement => achievement.Date)
                .ToArray();
            var summary = BuildSummary(scenario, context, state, allPlayerMilestones, allPlayerAchievements);
            states.Add(state);
            summaries.Add(summary);
        }

        var updated = scenario with
        {
            PlayerCareerStates = states
                .OrderBy(item => item.PlayerName, StringComparer.Ordinal)
                .ToArray(),
            PlayerCareerSummaries = summaries
                .OrderBy(item => item.PlayerName, StringComparer.Ordinal)
                .ToArray(),
            PlayerMilestones = milestones
                .OrderByDescending(item => item.Date)
                .ThenBy(item => item.PlayerName, StringComparer.Ordinal)
                .ToArray(),
            PlayerAchievements = achievements
                .OrderByDescending(item => item.Date)
                .ThenBy(item => item.PlayerName, StringComparer.Ordinal)
                .ToArray(),
            PlayerLifeCycleNews = news
                .OrderByDescending(item => item.Date)
                .ThenBy(item => item.PersonName, StringComparer.Ordinal)
                .Take(80)
                .ToArray(),
            CareerTimeline = timeline
        };
        updated.Validate();
        return updated;
    }

    public IReadOnlyList<ActionCenterItem> BuildActionItems(NewGmScenarioSnapshot scenario)
    {
        var items = new List<ActionCenterItem>();
        foreach (var summary in scenario.PlayerCareerSummaries.Take(40))
        {
            var latestMilestone = summary.Milestones
                .OrderByDescending(milestone => milestone.Date)
                .FirstOrDefault(milestone => milestone.Date >= scenario.CurrentDate.AddDays(-14) && milestone.IsNotable);
            if (latestMilestone is not null)
            {
                items.Add(new ActionCenterItem(
                    $"action-center:lifecycle:milestone:{latestMilestone.MilestoneId}",
                    $"Career milestone: {summary.PlayerName}",
                    ActionCenterCategory.PlayerDevelopment,
                    ActionCenterPriority.Normal,
                    scenario.CurrentDate.AddDays(7),
                    summary.PersonId,
                    summary.PlayerName,
                    scenario.Organization.OrganizationId,
                    scenario.Organization.Name,
                    latestMilestone.Summary,
                    "Milestones help build player story, reputation, and long-term attachment.",
                    "Open the player dossier and review the Career section.",
                    null,
                    null,
                    null));
            }

            if (summary.CareerPhase is PlayerCareerPhase.Breakout or PlayerCareerPhase.LateBloomer or PlayerCareerPhase.CareerRevival)
            {
                items.Add(new ActionCenterItem(
                    $"action-center:lifecycle:breakout:{summary.PersonId}",
                    $"Player story developing: {summary.PlayerName}",
                    ActionCenterCategory.PlayerDevelopment,
                    ActionCenterPriority.Important,
                    scenario.CurrentDate.AddDays(14),
                    summary.PersonId,
                    summary.PlayerName,
                    scenario.Organization.OrganizationId,
                    scenario.Organization.Name,
                    summary.CareerSummaryText,
                    "Breakout and revival stories can change contracts, roster plans, trade value, and staff recommendations.",
                    "Review role, development plan, and contract timing before the story cools.",
                    null,
                    null,
                    null));
            }
            else if (summary.CareerPhase is PlayerCareerPhase.BustRisk or PlayerCareerPhase.CareerDecline)
            {
                items.Add(new ActionCenterItem(
                    $"action-center:lifecycle:decline:{summary.PersonId}",
                    $"Career warning: {summary.PlayerName}",
                    ActionCenterCategory.PlayerDevelopment,
                    ActionCenterPriority.Important,
                    scenario.CurrentDate.AddDays(14),
                    summary.PersonId,
                    summary.PlayerName,
                    scenario.Organization.OrganizationId,
                    scenario.Organization.Name,
                    summary.CareerSummaryText,
                    "Decline and bust-risk stories can affect lineup usage, development plans, and roster decisions.",
                    "Review staff, medical, and development notes before committing to a bigger role.",
                    null,
                    null,
                    null));
            }
        }

        return items
            .GroupBy(item => item.ActionCenterItemId, StringComparer.Ordinal)
            .Select(group => group.First())
            .Take(8)
            .ToArray();
    }

    public IReadOnlyList<string> BuildMonthlyHighlights(NewGmScenarioSnapshot scenario)
    {
        var since = scenario.CurrentDate.AddDays(-31);
        var lines = scenario.PlayerMilestones
            .Where(milestone => milestone.Date >= since)
            .OrderByDescending(milestone => milestone.Date)
            .Take(6)
            .Select(milestone => $"{milestone.Date:yyyy-MM-dd}: {milestone.Summary}")
            .ToList();

        var breakout = scenario.PlayerCareerSummaries
            .Where(summary => summary.CareerPhase is PlayerCareerPhase.Breakout or PlayerCareerPhase.LateBloomer)
            .OrderByDescending(summary => summary.LegacyScore)
            .FirstOrDefault();
        if (breakout is not null)
        {
            lines.Add($"Biggest breakout: {breakout.PlayerName} - {breakout.CareerSummaryText}");
        }

        var decline = scenario.PlayerCareerSummaries
            .Where(summary => summary.CareerPhase is PlayerCareerPhase.CareerDecline or PlayerCareerPhase.BustRisk)
            .OrderByDescending(summary => summary.LegacyScore)
            .FirstOrDefault();
        if (decline is not null)
        {
            lines.Add($"Biggest decline/risk: {decline.PlayerName} - {decline.CareerSummaryText}");
        }

        return lines.Count == 0 ? new[] { "No major player life-cycle stories this month." } : lines;
    }

    public PlayerCareerSummary? FindSummary(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.PlayerCareerSummaries.FirstOrDefault(summary => summary.PersonId == personId);

    private static IReadOnlyList<Person> CollectPlayers(NewGmScenarioSnapshot scenario)
    {
        var ids = scenario.AlphaSnapshot.Roster.Players.Select(player => player.PersonId)
            .Concat(scenario.AlphaSnapshot.Players.Select(player => player.PersonId))
            .Concat(scenario.ProspectRights.Select(prospect => prospect.ProspectPersonId))
            .Concat(scenario.AlphaSnapshot.DraftBoard.Entries.Select(entry => entry.ProspectPersonId))
            .Concat(scenario.AlphaSnapshot.Recruits.Select(recruit => recruit.RecruitPersonId))
            .Concat(scenario.FreeAgentMarket?.FreeAgents.Select(agent => agent.PersonId) ?? Array.Empty<string>())
            .Concat(scenario.TradeBlock?.Entries.Select(entry => entry.PersonId) ?? Array.Empty<string>())
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        return scenario.AlphaSnapshot.People
            .Concat(scenario.AlphaSnapshot.Players)
            .Where(person => ids.Contains(person.PersonId))
            .GroupBy(person => person.PersonId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    private static PlayerContext BuildContext(NewGmScenarioSnapshot scenario, Person person)
    {
        var roster = scenario.AlphaSnapshot.Roster.FindPlayer(person.PersonId);
        var prospect = scenario.ProspectRights.FirstOrDefault(item => item.ProspectPersonId == person.PersonId);
        var board = scenario.AlphaSnapshot.DraftBoard.Entries.FirstOrDefault(item => item.ProspectPersonId == person.PersonId);
        var freeAgent = scenario.FreeAgentMarket?.Find(person.PersonId);
        var tradeBlock = scenario.TradeBlock?.Find(person.PersonId);
        var pipeline = scenario.PlayerPipeline.FirstOrDefault(item => item.PersonId == person.PersonId);
        var stat = scenario.CareerStatSummaries.FirstOrDefault(item => item.PersonId == person.PersonId);
        var seasonStat = scenario.PlayerStats.FirstOrDefault(item => item.PersonId == person.PersonId);
        var goalieStat = scenario.GoalieStats.FirstOrDefault(item => item.PersonId == person.PersonId);
        var development = scenario.AlphaSnapshot.DevelopmentProfiles.FirstOrDefault(item => item.PersonId == person.PersonId);
        var review = scenario.DevelopmentReviews
            .Where(item => item.PersonId == person.PersonId)
            .OrderByDescending(item => item.ReviewDate)
            .FirstOrDefault();
        var draft = scenario.DraftPickHistory.FirstOrDefault(item => item.PlayerPersonId == person.PersonId);
        var position = roster?.Position
            ?? prospect?.Position
            ?? board?.Bio?.Position
            ?? freeAgent?.Position
            ?? tradeBlock?.Position
            ?? stat?.Position
            ?? RosterPosition.Unknown;
        var currentTeam = roster is not null
            ? scenario.Organization.Name
            : prospect?.CurrentTeam
            ?? board?.Bio?.CurrentTeam
            ?? freeAgent?.PreviousTeam
            ?? tradeBlock?.TeamName
            ?? pipeline?.CurrentTeamName
            ?? "Unassigned";
        var league = pipeline?.CurrentLevel
            ?? prospect?.CurrentLeague
            ?? board?.Bio?.League
            ?? stat?.PrimaryLeague
            ?? "Unknown";

        return new PlayerContext(person, roster, prospect, board, freeAgent, tradeBlock, pipeline, stat, seasonStat, goalieStat, development, review, draft, position, currentTeam, league);
    }

    private static PlayerCareerState BuildCareerState(NewGmScenarioSnapshot scenario, PlayerContext context)
    {
        var age = context.Person.CalculateAge(scenario.CurrentDate);
        var games = (context.CareerStats?.GamesPlayed ?? 0) + Math.Max(context.SeasonStats?.GamesPlayed ?? 0, context.GoalieStats?.GamesPlayed ?? 0);
        var goals = (context.CareerStats?.Goals ?? 0) + (context.SeasonStats?.Goals ?? 0);
        var assists = (context.CareerStats?.Assists ?? 0) + (context.SeasonStats?.Assists ?? 0);
        var points = goals + assists;
        var stage = DetermineStage(context, age, games);
        var phase = DeterminePhase(context, age, games, points);
        var legacyScore = CalculateLegacyScore(context, stage, games, goals, points);
        var reputation = BuildReputation(context, stage, legacyScore, games, points);
        var summary = $"{context.Person.Identity.DisplayName} is a {age}-year-old {context.Position} in the {stage} stage with a {phase} career phase.";

        var state = new PlayerCareerState(
            context.Person.PersonId,
            context.Person.Identity.DisplayName,
            age,
            context.Position,
            stage,
            phase,
            reputation,
            games,
            goals,
            assists,
            points,
            legacyScore,
            context.CurrentTeam,
            context.CurrentLeague,
            summary);
        state.Validate();
        return state;
    }

    private static PlayerLifeStage DetermineStage(PlayerContext context, int age, int games)
    {
        if (context.Person.Status == PersonStatus.Retired)
        {
            return PlayerLifeStage.Retired;
        }

        if (context.Person.Status == PersonStatus.Deceased)
        {
            return PlayerLifeStage.Retired;
        }

        var league = context.CurrentLeague;
        if (age <= 15)
        {
            return PlayerLifeStage.Youth;
        }

        if (league.Contains("NHL", StringComparison.OrdinalIgnoreCase) && games >= 80 && age is >= 24 and <= 31)
        {
            return PlayerLifeStage.Prime;
        }

        if (league.Contains("NHL", StringComparison.OrdinalIgnoreCase) && games >= 20)
        {
            return age >= 33 ? PlayerLifeStage.Veteran : PlayerLifeStage.NhlRegular;
        }

        if (age >= 34)
        {
            return PlayerLifeStage.Declining;
        }

        if (league.Contains("AHL", StringComparison.OrdinalIgnoreCase) || context.Pipeline?.CurrentLevel.Contains("AHL", StringComparison.OrdinalIgnoreCase) == true)
        {
            return PlayerLifeStage.DevelopingProfessional;
        }

        if (context.Prospect is not null || context.DraftPick is not null || context.BoardEntry is not null)
        {
            return PlayerLifeStage.Prospect;
        }

        return age <= 20 ? PlayerLifeStage.Junior : PlayerLifeStage.DevelopingProfessional;
    }

    private static PlayerCareerPhase DeterminePhase(PlayerContext context, int age, int games, int points)
    {
        if (context.DevelopmentReview?.ImprovedThemes.Count > 0 && age <= 24)
        {
            return PlayerCareerPhase.Breakout;
        }

        if (context.DevelopmentProfile is not null)
        {
            var confidence = SafeTrait(context.DevelopmentProfile, DevelopmentAttribute.Confidence);
            var work = SafeTrait(context.DevelopmentProfile, DevelopmentAttribute.WorkEthic);
            if (confidence >= 68 && work >= 66 && age <= 25)
            {
                return PlayerCareerPhase.Breakout;
            }

            if (confidence <= 38 && age <= 21)
            {
                return PlayerCareerPhase.BustRisk;
            }
        }

        if (age >= 32 && games >= 500)
        {
            return PlayerCareerPhase.VeteranLeadership;
        }

        if (age >= 33)
        {
            return PlayerCareerPhase.CareerDecline;
        }

        if (age is >= 24 and <= 30 && games >= 100)
        {
            return PlayerCareerPhase.Prime;
        }

        if (age >= 25 && games < 80 && points >= 40)
        {
            return PlayerCareerPhase.LateBloomer;
        }

        if (games >= 80 && points <= 15 && age <= 23)
        {
            return PlayerCareerPhase.Plateau;
        }

        return PlayerCareerPhase.Developing;
    }

    private static PlayerReputation BuildReputation(PlayerContext context, PlayerLifeStage stage, int legacyScore, int games, int points)
    {
        var score = Math.Clamp(legacyScore / 2 + games / 20 + points / 15, 0, 100);
        var category = (stage, score, games, points) switch
        {
            (PlayerLifeStage.Declining, _, >= 500, _) => PlayerReputationCategory.DecliningVeteran,
            (PlayerLifeStage.Veteran, _, >= 500, _) => PlayerReputationCategory.VeteranLeader,
            (_, >= 90, _, _) => PlayerReputationCategory.FranchisePlayer,
            (_, >= 82, _, _) => PlayerReputationCategory.Superstar,
            (_, >= 74, _, _) => PlayerReputationCategory.Elite,
            (_, >= 64, _, _) => PlayerReputationCategory.Star,
            (_, >= 52, _, _) => PlayerReputationCategory.EmergingStar,
            (_, >= 36, _, _) => PlayerReputationCategory.Reliable,
            (PlayerLifeStage.Prospect or PlayerLifeStage.Junior or PlayerLifeStage.Youth, _, _, _) => PlayerReputationCategory.Prospect,
            _ => PlayerReputationCategory.Unknown
        };
        var summary = category switch
        {
            PlayerReputationCategory.Prospect => "Known mostly through projection, scouting, and development path.",
            PlayerReputationCategory.VeteranLeader => "Respected for longevity and room influence.",
            PlayerReputationCategory.DecliningVeteran => "Still recognizable, but staff should watch workload and decline.",
            PlayerReputationCategory.FranchisePlayer => "Seen as a franchise-level driver in the league story.",
            _ => $"League perception is {category}."
        };
        var reputation = new PlayerReputation(context.Person.PersonId, category, score, summary);
        reputation.Validate();
        return reputation;
    }

    private static int CalculateLegacyScore(PlayerContext context, PlayerLifeStage stage, int games, int goals, int points)
    {
        var score = games / 10 + goals / 4 + points / 8;
        if (context.DraftPick is not null)
        {
            score += Math.Max(1, 8 - context.DraftPick.Round);
        }

        if (context.DevelopmentReview?.ImprovedThemes.Count > 0)
        {
            score += 4;
        }

        if (stage is PlayerLifeStage.Veteran or PlayerLifeStage.Prime)
        {
            score += 6;
        }

        if (context.Person.ActiveRolesOn(DateOnly.MaxValue).Any(role => role.Title.Contains("Captain", StringComparison.OrdinalIgnoreCase)))
        {
            score += 8;
        }

        return Math.Clamp(score, 0, 500);
    }

    private static IEnumerable<PlayerMilestone> BuildMilestones(NewGmScenarioSnapshot scenario, PlayerContext context, PlayerCareerState state)
    {
        if (context.DraftPick is not null)
        {
            yield return Milestone(context, PlayerMilestoneType.Drafted, DraftDateFor(scenario, context.DraftPick.Year), context.DraftPick.Year, $"{context.PlayerName} was drafted in round {context.DraftPick.Round}, pick {context.DraftPick.OverallPick}.", true);
        }
        else if (context.Prospect is not null && context.Prospect.RoundNumber > 0)
        {
            yield return Milestone(context, PlayerMilestoneType.Drafted, scenario.CurrentDate.AddDays(-20), scenario.Season.Year, $"{context.PlayerName} is held as a drafted prospect, round {context.Prospect.RoundNumber}, pick {context.Prospect.PickNumber}.", context.Prospect.RoundNumber <= 2);
        }

        if (scenario.Contracts.Any(contract => contract.PersonId == context.Person.PersonId))
        {
            var contract = scenario.Contracts.Where(contract => contract.PersonId == context.Person.PersonId).OrderBy(contract => contract.Term.StartDate).First();
            yield return Milestone(context, PlayerMilestoneType.SignedFirstContract, contract.Term.StartDate, contract.Term.StartDate.Year, $"{context.PlayerName} signed a first tracked contract.", true);
        }

        if (state.CurrentLeague.Contains("AHL", StringComparison.OrdinalIgnoreCase) || context.Pipeline?.CurrentLevel.Contains("AHL", StringComparison.OrdinalIgnoreCase) == true)
        {
            yield return Milestone(context, PlayerMilestoneType.AhlDebut, scenario.CurrentDate.AddDays(-45), scenario.Season.Year, $"{context.PlayerName} entered the AHL/pro development path.", false);
        }

        if (state.CurrentLeague.Contains("NHL", StringComparison.OrdinalIgnoreCase) && state.GamesPlayed > 0)
        {
            yield return Milestone(context, PlayerMilestoneType.NhlDebut, scenario.CurrentDate.AddDays(-Math.Min(180, state.GamesPlayed)), scenario.Season.Year, $"{context.PlayerName} made a first tracked NHL appearance.", true);
            if (state.Goals > 0)
            {
                yield return Milestone(context, PlayerMilestoneType.FirstNhlGoal, scenario.CurrentDate.AddDays(-Math.Min(150, state.GamesPlayed)), scenario.Season.Year, $"{context.PlayerName} recorded a first tracked NHL goal.", true);
            }

            if (state.Points > 0)
            {
                yield return Milestone(context, PlayerMilestoneType.FirstNhlPoint, scenario.CurrentDate.AddDays(-Math.Min(160, state.GamesPlayed)), scenario.Season.Year, $"{context.PlayerName} recorded a first tracked NHL point.", true);
            }
        }

        foreach (var threshold in new[] { 100, 250, 500, 750, 1000 })
        {
            if (state.GamesPlayed >= threshold)
            {
                yield return Milestone(context, GameMilestone(threshold), scenario.CurrentDate.AddDays(-Math.Min(365, threshold / 2)), scenario.Season.Year, $"{context.PlayerName} reached {threshold} tracked career games.", threshold >= 500);
            }
        }

        foreach (var threshold in new[] { 100, 250, 500 })
        {
            if (state.Goals >= threshold)
            {
                yield return Milestone(context, GoalMilestone(threshold), scenario.CurrentDate.AddDays(-Math.Min(365, threshold)), scenario.Season.Year, $"{context.PlayerName} reached {threshold} tracked career goals.", true);
            }
        }

        if (context.GoalieStats?.Shutouts > 0 || context.CareerStats?.Shutouts > 0)
        {
            yield return Milestone(context, PlayerMilestoneType.FirstShutout, scenario.CurrentDate.AddDays(-30), scenario.Season.Year, $"{context.PlayerName} recorded a first tracked shutout.", true);
        }

        if (context.Person.Status == PersonStatus.Retired)
        {
            yield return Milestone(context, PlayerMilestoneType.Retirement, scenario.CurrentDate, scenario.Season.Year, $"{context.PlayerName} retired.", true);
        }
    }

    private static IEnumerable<PlayerAchievement> BuildAchievements(NewGmScenarioSnapshot scenario, PlayerContext context, PlayerCareerState state, IReadOnlyList<PlayerMilestone> milestones)
    {
        if (state.CareerPhase == PlayerCareerPhase.Breakout)
        {
            yield return Achievement(context, PlayerAchievementType.BreakoutSeason, scenario.CurrentDate, $"{context.PlayerName} is flagged as a breakout story.");
        }

        if (state.CareerPhase == PlayerCareerPhase.VeteranLeadership || state.Reputation.Category == PlayerReputationCategory.VeteranLeader)
        {
            yield return Achievement(context, PlayerAchievementType.VeteranLeader, scenario.CurrentDate, $"{context.PlayerName} is recognized as a veteran leadership presence.");
        }

        var teamLeader = scenario.PlayerStats.OrderByDescending(stat => stat.Points).FirstOrDefault();
        if (teamLeader?.PersonId == context.Person.PersonId && teamLeader.Points > 0)
        {
            yield return Achievement(context, PlayerAchievementType.TopScorer, scenario.CurrentDate, $"{context.PlayerName} currently leads tracked skater scoring.");
        }

        if (milestones.Any(milestone => milestone.MilestoneType == PlayerMilestoneType.NhlDebut) && state.Age <= 22)
        {
            yield return Achievement(context, PlayerAchievementType.RookieLeader, scenario.CurrentDate, $"{context.PlayerName} is building a rookie-season story.");
        }
    }

    private static PlayerCareerSummary BuildSummary(
        NewGmScenarioSnapshot scenario,
        PlayerContext context,
        PlayerCareerState state,
        IReadOnlyList<PlayerMilestone> milestones,
        IReadOnlyList<PlayerAchievement> achievements)
    {
        var story = BuildStory(context, state, milestones, achievements).ToArray();
        var staff = InfluentialStaff(scenario, context).ToArray();
        var coach = new StaffCoachingService().BuildDossierStaffOpinions(scenario, context.Person.PersonId).FirstOrDefault()
            ?? "Coach comments are still forming.";
        var scout = scenario.CompletedScoutingReports
            .Where(report => report.PlayerId == context.Person.PersonId)
            .OrderByDescending(report => report.CreatedOn)
            .Select(report => $"{PersonName(scenario, report.ScoutId)}: {report.Recommendation}")
            .FirstOrDefault()
            ?? context.BoardEntry?.ProjectionText
            ?? "Scout comments are still forming.";
        var medical = new MedicalHealthService().BuildDossierMedicalLines(scenario, context.Person.PersonId).FirstOrDefault()
            ?? "No medical history of note.";
        var text = $"{state.PlayerName}: {state.LifeStage}, {state.CareerPhase}, {state.Reputation.Category}, legacy score {state.LegacyScore}.";
        var summary = new PlayerCareerSummary(
            state.PersonId,
            state.PlayerName,
            state.LifeStage,
            state.CareerPhase,
            state.Reputation.Category,
            state.LegacyScore,
            text,
            story,
            milestones,
            achievements,
            staff,
            coach,
            scout,
            medical);
        summary.Validate();
        return summary;
    }

    private static IEnumerable<string> BuildStory(PlayerContext context, PlayerCareerState state, IReadOnlyList<PlayerMilestone> milestones, IReadOnlyList<PlayerAchievement> achievements)
    {
        if (context.DraftPick is not null)
        {
            yield return $"Drafted {context.DraftPick.OverallPick.Ordinal()} overall in {context.DraftPick.Year}.";
        }
        else if (context.Prospect is not null)
        {
            yield return $"Rights held from round {context.Prospect.RoundNumber}, pick {context.Prospect.PickNumber}.";
        }

        if (context.Pipeline is not null)
        {
            yield return $"{context.Pipeline.CurrentLevel} path: {context.Pipeline.PipelineStatus}, {context.Pipeline.AssignmentStatus}.";
        }

        if (context.DevelopmentReview is not null)
        {
            yield return $"Development review: {string.Join(", ", context.DevelopmentReview.ImprovedThemes)}.";
        }

        foreach (var milestone in milestones.OrderBy(item => item.Date).Take(6))
        {
            yield return milestone.Summary;
        }

        foreach (var achievement in achievements.OrderBy(item => item.Date).Take(4))
        {
            yield return achievement.Summary;
        }

        yield return $"Current story: {state.LifeStage} with {state.Reputation.Category} reputation.";
    }

    private static IEnumerable<string> InfluentialStaff(NewGmScenarioSnapshot scenario, PlayerContext context)
    {
        var staff = new List<string>();
        var coach = scenario.StaffMembers.FirstOrDefault(member => member.CurrentRole is StaffRole.HeadCoach or StaffRole.AssistantCoach or StaffRole.DevelopmentCoach);
        if (coach is not null)
        {
            staff.Add($"{PersonName(scenario, coach.PersonId)} ({StaffRoles.Title(coach.CurrentRole)})");
        }

        var scoutReport = scenario.CompletedScoutingReports
            .Where(report => report.PlayerId == context.Person.PersonId)
            .OrderByDescending(report => report.CreatedOn)
            .FirstOrDefault();
        if (scoutReport is not null)
        {
            staff.Add($"{PersonName(scenario, scoutReport.ScoutId)} (Scout)");
        }

        var medical = scenario.StaffMembers.FirstOrDefault(member => member.CurrentRole is StaffRole.HeadAthleticTherapist or StaffRole.AthleticTherapist or StaffRole.AssistantTrainer or StaffRole.TeamDoctor or StaffRole.Physiotherapist);
        if (medical is not null)
        {
            staff.Add($"{PersonName(scenario, medical.PersonId)} ({StaffRoles.Title(medical.CurrentRole)})");
        }

        return staff.Count == 0 ? new[] { "No key staff influence identified yet." } : staff.Distinct(StringComparer.Ordinal).Take(4).ToArray();
    }

    private static PlayerMilestone Milestone(PlayerContext context, PlayerMilestoneType type, DateOnly date, int seasonYear, string summary, bool notable)
    {
        var milestone = new PlayerMilestone(
            $"player-milestone:{context.Person.PersonId}:{type}",
            context.Person.PersonId,
            context.PlayerName,
            type,
            date,
            seasonYear,
            summary,
            notable);
        milestone.Validate();
        return milestone;
    }

    private static PlayerAchievement Achievement(PlayerContext context, PlayerAchievementType type, DateOnly date, string summary)
    {
        var achievement = new PlayerAchievement(
            $"player-achievement:{context.Person.PersonId}:{type}",
            context.Person.PersonId,
            context.PlayerName,
            type,
            date,
            summary);
        achievement.Validate();
        return achievement;
    }

    private static CareerTimelineEntry ToTimelineEntry(NewGmScenarioSnapshot scenario, PlayerMilestone milestone) =>
        new(
            $"career:lifecycle:{milestone.MilestoneId}",
            TimelineTypeFor(milestone.MilestoneType),
            milestone.Date,
            milestone.SeasonYear,
            milestone.PersonId,
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            milestone.MilestoneType.ToString(),
            milestone.Summary,
            null,
            milestone.IsNotable ? HistoryImportance.Important : HistoryImportance.Normal);

    private static CareerTimelineEntry ToTimelineEntry(NewGmScenarioSnapshot scenario, PlayerAchievement achievement) =>
        new(
            $"career:lifecycle:{achievement.AchievementId}",
            achievement.AchievementType is PlayerAchievementType.TopScorer or PlayerAchievementType.TeamMvp or PlayerAchievementType.MostImproved ? CareerTimelineEntryType.Award : CareerTimelineEntryType.Breakout,
            achievement.Date,
            scenario.Season.Year,
            achievement.PersonId,
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            achievement.AchievementType.ToString(),
            achievement.Summary,
            null,
            HistoryImportance.Normal);

    private static LeagueTransaction ToLeagueNews(NewGmScenarioSnapshot scenario, PlayerMilestone milestone)
    {
        var transaction = new LeagueTransaction(
            $"transaction:lifecycle:{milestone.MilestoneId}",
            new DateTimeOffset(milestone.Date.Year, milestone.Date.Month, milestone.Date.Day, 12, 0, 0, TimeSpan.Zero),
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            milestone.PersonId,
            milestone.PlayerName,
            LeagueTransactionType.PlayerMilestone,
            LeagueNewsCategory.League,
            milestone.Summary);
        transaction.Validate();
        return transaction;
    }

    private static void QueueMilestoneEvent(EngineRegistry? registry, NewGmScenarioSnapshot scenario, PlayerMilestone milestone)
    {
        if (registry is null)
        {
            return;
        }

        var legacyEvent = registry.EventEngine.CreateEvent(
            new DateTimeOffset(milestone.Date.Year, milestone.Date.Month, milestone.Date.Day, 12, 15, 0, TimeSpan.Zero),
            LegacyEventType.MilestoneReached,
            milestone.IsNotable ? LegacyEventSeverity.Notice : LegacyEventSeverity.Info,
            LegacyEventVisibility.Organization,
            $"Career milestone: {milestone.PlayerName}",
            milestone.Summary,
            new LegacyEventContext(PrimaryPersonId: milestone.PersonId, OrganizationId: scenario.Organization.OrganizationId, SeasonId: scenario.Season.SeasonId),
            new Dictionary<string, object?>
            {
                ["player_lifecycle"] = true,
                ["milestone_type"] = milestone.MilestoneType.ToString(),
                ["player_name"] = milestone.PlayerName,
                ["team_name"] = scenario.Organization.Name
            });
        registry.EventEngine.QueueEvent(legacyEvent);
    }

    private static CareerTimelineEntryType TimelineTypeFor(PlayerMilestoneType type) =>
        type switch
        {
            PlayerMilestoneType.Drafted => CareerTimelineEntryType.Drafted,
            PlayerMilestoneType.SignedFirstContract => CareerTimelineEntryType.Signed,
            PlayerMilestoneType.AhlDebut or PlayerMilestoneType.NhlDebut or PlayerMilestoneType.FirstNhlGoal or PlayerMilestoneType.FirstNhlPoint or PlayerMilestoneType.FirstShutout => CareerTimelineEntryType.Debut,
            PlayerMilestoneType.JuniorChampionship or PlayerMilestoneType.Championship => CareerTimelineEntryType.Championship,
            PlayerMilestoneType.Retirement => CareerTimelineEntryType.Retired,
            _ => CareerTimelineEntryType.Breakout
        };

    private static PlayerMilestoneType GameMilestone(int threshold) =>
        threshold switch
        {
            100 => PlayerMilestoneType.Games100,
            250 => PlayerMilestoneType.Games250,
            500 => PlayerMilestoneType.Games500,
            750 => PlayerMilestoneType.Games750,
            _ => PlayerMilestoneType.Games1000
        };

    private static PlayerMilestoneType GoalMilestone(int threshold) =>
        threshold switch
        {
            100 => PlayerMilestoneType.Goals100,
            250 => PlayerMilestoneType.Goals250,
            _ => PlayerMilestoneType.Goals500
        };

    private static DateOnly DraftDateFor(NewGmScenarioSnapshot scenario, int year) =>
        year == scenario.Season.Year
            ? scenario.DraftDate
            : new DateOnly(year, Math.Clamp(scenario.DraftDate.Month, 1, 12), Math.Clamp(scenario.DraftDate.Day, 1, 28));

    private static int SafeTrait(PlayerDevelopmentProfile profile, DevelopmentAttribute attribute)
    {
        try
        {
            return profile.TraitValue(attribute);
        }
        catch (ArgumentException)
        {
            return 50;
        }
    }

    private static string PersonName(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? scenario.AlphaSnapshot.Players.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? personId;

    private sealed record PlayerContext(
        Person Person,
        RosterPlayer? RosterPlayer,
        DraftRightsRecord? Prospect,
        DraftBoardEntry? BoardEntry,
        FreeAgent? FreeAgent,
        TradeBlockEntry? TradeBlockEntry,
        PlayerPipelineRecord? Pipeline,
        CareerStatSummary? CareerStats,
        PlayerSeasonStatLine? SeasonStats,
        GoalieSeasonStatLine? GoalieStats,
        PlayerDevelopmentProfile? DevelopmentProfile,
        DevelopmentReview? DevelopmentReview,
        DraftPickHistory? DraftPick,
        RosterPosition Position,
        string CurrentTeam,
        string CurrentLeague)
    {
        public string PlayerName => Person.Identity.DisplayName;
    }
}

internal static class PlayerLifeCycleTextExtensions
{
    public static string Ordinal(this int value)
    {
        var suffix = value % 100 is 11 or 12 or 13
            ? "th"
            : (value % 10) switch
            {
                1 => "st",
                2 => "nd",
                3 => "rd",
                _ => "th"
            };
        return $"{value}{suffix}";
    }
}
