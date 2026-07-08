using LegacyEngine.Contracts;
using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public sealed class TradeStrategyService
{
    public TeamNeedsProfile BuildTeamNeedsProfile(NewGmScenarioSnapshot scenario, string organizationId, string teamName)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var isPlayerTeam = organizationId == scenario.Organization.OrganizationId;
        var standings = scenario.Standings?.Teams.FirstOrDefault(team => team.OrganizationId == organizationId);
        var direction = DetermineDirection(scenario, organizationId, standings);
        var personality = DeterminePersonality(organizationId, teamName, direction);
        var needs = isPlayerTeam
            ? BuildPlayerTeamNeeds(scenario, direction)
            : BuildSyntheticTeamNeeds(scenario, organizationId, teamName, direction);
        var preferences = BuildAssetPreferences(direction, personality, needs);
        var summary = $"{teamName} projects as {Readable(direction)} with a {Readable(personality)} front office. Priority needs: {string.Join(", ", needs.Take(3).Select(need => Readable(need.Need)))}.";
        var profile = new TeamNeedsProfile(organizationId, teamName, direction, personality, needs, preferences, summary);
        profile.Validate();
        return profile;
    }

    public IReadOnlyList<TeamNeedsProfile> BuildLeagueNeeds(NewGmScenarioSnapshot scenario)
    {
        var teams = SeasonFrameworkService.LeagueTeams(scenario)
            .Select(team => BuildTeamNeedsProfile(scenario, team.OrganizationId, team.TeamName))
            .ToArray();
        foreach (var team in teams)
        {
            team.Validate();
        }

        return teams;
    }

    public TradeValueSummary BuildTradeValueSummary(NewGmScenarioSnapshot scenario, string personId)
    {
        var block = scenario.TradeBlock?.Find(personId);
        var rosterPlayer = scenario.AlphaSnapshot.Roster.FindPlayer(personId);
        var rights = scenario.ProspectRights.FirstOrDefault(prospect => prospect.ProspectPersonId == personId);
        var name = PersonName(scenario, personId, block?.Name ?? rights?.ProspectName);
        var age = block?.Age ?? rights?.Age ?? rosterPlayer?.Age ?? PersonAge(scenario, personId);
        var role = block?.CurrentRole
            ?? (rights is not null ? $"Prospect rights, R{rights.RoundNumber} P{rights.PickNumber}" : rosterPlayer?.Status.ToString())
            ?? "Depth option";
        var contract = ContractText(scenario, personId, out var yearsRemaining, out var salary);
        var prospectValue = rights is not null
            ? $"{rights.ScoutingConfidence?.ToString() ?? "Unknown"} confidence rights asset"
            : age <= 18 ? "Young player with future value" : "Current-roster value";
        var stat = scenario.CareerStatSummaries.FirstOrDefault(summary => summary.PersonId == personId);
        var production = stat is null ? "limited historical production data" : $"{stat.Points} career points in tracked history";
        var estimated = ValueBand(block?.AssetValue ?? EstimateValue(age, rosterPlayer?.Position ?? rights?.Position ?? block?.Position ?? RosterPosition.Unknown, stat?.Points ?? 0));
        var opinion = block is not null
            ? $"{block.TeamName} may move him because {block.ReasonAvailable.ToLowerInvariant()}"
            : "Internal value depends on roster fit, contract timing, and development path.";

        var value = new TradeValueSummary(
            personId,
            name,
            age,
            role,
            contract,
            salary,
            yearsRemaining,
            production,
            prospectValue,
            estimated,
            opinion);
        value.Validate();
        return value;
    }

    public TradeEvaluation EvaluateTrade(NewGmScenarioSnapshot scenario, TradeOffer offer)
    {
        var profile = BuildTeamNeedsProfile(scenario, offer.OtherOrganizationId, offer.OtherOrganizationName);
        var aiProfile = new OrganizationAiService().BuildProfile(
            scenario,
            new LeagueAiService().BuildOrganizationProfile(scenario, offer.OtherOrganizationId, offer.OtherOrganizationName));
        var giveValue = offer.PlayerGives.Sum(asset => asset.Value);
        var receiveValue = offer.PlayerReceives.Sum(asset => asset.Value);
        var budgetImpact = offer.PlayerReceives.Sum(asset => asset.SalaryImpact) - offer.PlayerGives.Sum(asset => asset.SalaryImpact);
        var otherTeamBudgetImpact = -budgetImpact;
        var matchedNeeds = MatchedNeeds(profile, offer.PlayerGives);
        var aiDecision = new OrganizationAiService().EvaluateTradeDecision(aiProfile, offer.PlayerGives, otherTeamBudgetImpact);
        var fitScore = matchedNeeds.Count * 6 + AssetPreferenceScore(profile, offer.PlayerGives);
        var personalityScore = PersonalityScore(profile, offer);
        var ageScore = AgeFitScore(profile, offer.PlayerGives);
        var budgetScore = BudgetScore(profile, otherTeamBudgetImpact);
        var valueGap = giveValue - receiveValue;
        var aiScore = Math.Clamp((aiDecision.Score - 50) / 5, -6, 6);
        if (valueGap < -35)
        {
            aiScore = Math.Min(aiScore, 0);
        }

        var score = giveValue - receiveValue + fitScore + personalityScore + ageScore + budgetScore + aiScore;
        var threshold = profile.GmPersonality switch
        {
            TradeGmPersonality.AggressiveTrader => -8,
            TradeGmPersonality.WinNow => -4,
            TradeGmPersonality.Conservative => 6,
            _ => 0
        };
        var decision = valueGap <= -50
            ? TradeOfferStatus.Rejected
            : score >= threshold
            ? TradeOfferStatus.Accepted
            : score >= threshold - 20
                ? TradeOfferStatus.Countered
                : TradeOfferStatus.Rejected;
        var interest = decision switch
        {
            TradeOfferStatus.Accepted when score >= threshold + 18 => TradeInterest.VeryInterested,
            TradeOfferStatus.Accepted => TradeInterest.High,
            TradeOfferStatus.Countered => TradeInterest.Medium,
            _ when score <= threshold - 45 => TradeInterest.NotInterested,
            _ => TradeInterest.Low
        };
        var counter = decision == TradeOfferStatus.Countered
            ? BuildCounterSuggestion(profile, offer)
            : string.Empty;
        var reasons = BuildReasons(profile, aiProfile, aiDecision, offer, score, fitScore, budgetScore, matchedNeeds, counter);
        var explanation = decision switch
        {
            TradeOfferStatus.Accepted => $"{offer.OtherOrganizationName} is willing to accept because the package fits their {Readable(aiProfile.Strategy.Phase).ToLowerInvariant()} plan and current needs. GM approval is still required.",
            TradeOfferStatus.Countered => $"{offer.OtherOrganizationName} is interested, but wants a cleaner fit with its {Readable(aiProfile.Personality).ToLowerInvariant()} profile before agreeing. {counter}",
            _ => $"{offer.OtherOrganizationName} rejected the offer because the package does not satisfy its {Readable(aiProfile.Strategy.Phase).ToLowerInvariant()} strategy or priority needs."
        };
        var reactions = BuildStaffReaction(scenario, offer, decision, profile);

        var evaluation = new TradeEvaluation(
            decision,
            score,
            explanation,
            reasons,
            budgetImpact,
            offer.PlayerReceives.Count(asset => asset.AssetType == TradeAssetType.Player) - offer.PlayerGives.Count(asset => asset.AssetType == TradeAssetType.Player),
            interest,
            counter,
            new[] { reactions.HeadCoach, reactions.Scout, reactions.Owner, reactions.AssistantGm },
            new[] { reactions.PlayerReaction },
            profile.Direction,
            profile.GmPersonality,
            matchedNeeds);
        evaluation.Validate();
        return evaluation;
    }

    public TradeCounterOffer BuildCounterOffer(NewGmScenarioSnapshot scenario, TradeOffer offer, TradeEvaluation evaluation)
    {
        var profile = BuildTeamNeedsProfile(scenario, offer.OtherOrganizationId, offer.OtherOrganizationName);
        var requested = RequestedCounterAssets(scenario, offer, profile);
        var message = string.IsNullOrWhiteSpace(evaluation.CounterSuggestion)
            ? BuildCounterSuggestion(profile, offer)
            : evaluation.CounterSuggestion;
        var revisedGives = offer.PlayerGives
            .Concat(requested)
            .GroupBy(asset => asset.AssetId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        var counter = new TradeCounterOffer(offer.TradeOfferId, message, requested, $"Counter reflects {offer.OtherOrganizationName}'s {Readable(profile.Direction).ToLowerInvariant()} needs.")
        {
            RevisedPlayerGives = revisedGives,
            RevisedPlayerReceives = offer.PlayerReceives.ToArray()
        };
        counter.Validate();
        return counter;
    }

    public TradeStaffReaction BuildStaffReaction(NewGmScenarioSnapshot scenario, TradeOffer offer, TradeOfferStatus decision, TeamNeedsProfile? profile = null)
    {
        profile ??= BuildTeamNeedsProfile(scenario, offer.OtherOrganizationId, offer.OtherOrganizationName);
        var incoming = offer.PlayerReceives.FirstOrDefault(asset => asset.AssetType is TradeAssetType.Player or TradeAssetType.ProspectRights);
        var outgoing = offer.PlayerGives.FirstOrDefault(asset => asset.AssetType is TradeAssetType.Player or TradeAssetType.ProspectRights);
        var budgetImpact = offer.PlayerReceives.Sum(asset => asset.SalaryImpact) - offer.PlayerGives.Sum(asset => asset.SalaryImpact);
        var coach = incoming is null
            ? "Head coach: no immediate lineup piece identified."
            : $"Head coach: {incoming.DisplayName} fits as {incoming.Summary}.";
        var scout = incoming is null
            ? "Head scout: package is mostly future assets."
            : $"Head scout: recommends confirming {incoming.DisplayName}'s development evidence before approval.";
        var owner = budgetImpact > 2_000m
            ? "Owner: concerned about added hockey operations cost."
            : "Owner: comfortable if the move supports the club plan.";
        var assistant = decision switch
        {
            TradeOfferStatus.Accepted => "Assistant GM: acceptable framework, but approval should still be deliberate.",
            TradeOfferStatus.Countered => $"Assistant GM: counter is workable if it adds {Readable(profile.AssetPreferences.First()).ToLowerInvariant()}.",
            _ => "Assistant GM: recommends walking away unless the ask drops."
        };
        var player = outgoing is null
            ? "Player reaction: roster room remains stable."
            : $"Player reaction: {outgoing.DisplayName} may be unsettled by trade talks and will need communication.";
        var impacts = outgoing is null
            ? Array.Empty<string>()
            : new[] { $"GM relationship risk with {outgoing.DisplayName} if rumors linger." };
        var reaction = new TradeStaffReaction(coach, scout, owner, assistant, player, impacts);
        reaction.Validate();
        return reaction;
    }

    private static IReadOnlyList<TeamNeed> BuildPlayerTeamNeeds(NewGmScenarioSnapshot scenario, TeamDirection direction)
    {
        var active = scenario.AlphaSnapshot.Roster.ActivePlayers.ToArray();
        var needs = new List<TeamNeed>();
        var goalies = active.Count(player => player.Position == RosterPosition.Goalie);
        var defense = active.Count(player => player.Position == RosterPosition.Defense);
        var forwards = active.Count(player => player.Position is RosterPosition.Center or RosterPosition.LeftWing or RosterPosition.RightWing);
        if (goalies < 2)
        {
            needs.Add(new TeamNeed(PositionNeed.StartingGoalie, TradePriority.Urgent, "Roster has fewer than two active goalies."));
        }

        if (defense < 6)
        {
            needs.Add(new TeamNeed(PositionNeed.DefensiveDefenseman, TradePriority.High, "Defense depth is below the target for opening roster balance."));
        }

        if (forwards < 12)
        {
            needs.Add(new TeamNeed(PositionNeed.TopSixForward, TradePriority.High, "Forward group needs more reliable minutes."));
        }

        if (scenario.ProspectRights.Count < 8)
        {
            needs.Add(new TeamNeed(PositionNeed.ProspectDepth, TradePriority.Medium, "Prospect pipeline is lighter than preferred."));
        }

        if (scenario.Contracts.Sum(contract => contract.Money.SalaryOrStipend) > 140_000m)
        {
            needs.Add(new TeamNeed(PositionNeed.BudgetRelief, TradePriority.Medium, "Contract commitments are putting pressure on the hockey operations budget."));
        }

        if (direction is TeamDirection.Rebuilder or TeamDirection.ProspectBuild)
        {
            needs.Add(new TeamNeed(PositionNeed.DraftPicks, TradePriority.High, "Team direction favors future assets."));
        }

        if (needs.Count == 0)
        {
            needs.Add(new TeamNeed(PositionNeed.VeteranLeadership, TradePriority.Low, "Roster is balanced enough to value selective leadership upgrades."));
        }

        return needs.Take(5).ToArray();
    }

    private static IReadOnlyList<TeamNeed> BuildSyntheticTeamNeeds(NewGmScenarioSnapshot scenario, string organizationId, string teamName, TeamDirection direction)
    {
        var seed = StableHash($"{organizationId}:{teamName}:{scenario.Season.Year}");
        var needs = new List<TeamNeed>();
        if (direction is TeamDirection.Rebuilder or TeamDirection.ProspectBuild)
        {
            needs.Add(new TeamNeed(PositionNeed.DraftPicks, TradePriority.High, "Front office wants more future flexibility."));
            needs.Add(new TeamNeed(PositionNeed.ProspectDepth, TradePriority.High, "Pipeline depth is a priority."));
        }
        else if (direction == TeamDirection.WinNow)
        {
            needs.Add(new TeamNeed(PositionNeed.TopSixForward, TradePriority.High, "Club is hunting for immediate scoring help."));
            needs.Add(new TeamNeed(PositionNeed.VeteranLeadership, TradePriority.Medium, "Management wants playoff-room maturity."));
        }
        else if (direction == TeamDirection.BudgetReset)
        {
            needs.Add(new TeamNeed(PositionNeed.BudgetRelief, TradePriority.Urgent, "Ownership wants financial room."));
            needs.Add(new TeamNeed(PositionNeed.DraftPicks, TradePriority.Medium, "Draft picks help reset the asset base."));
        }
        else
        {
            needs.Add(new TeamNeed(seed % 2 == 0 ? PositionNeed.DefensiveDefenseman : PositionNeed.ScoringDepth, TradePriority.Medium, "Roster balance is uneven."));
            needs.Add(new TeamNeed(PositionNeed.YoungerRoster, TradePriority.Medium, "Club wants younger players who can grow into roles."));
        }

        if (seed % 5 == 0)
        {
            needs.Add(new TeamNeed(PositionNeed.StartingGoalie, TradePriority.High, "Goaltending confidence is shaky."));
        }

        return needs.Take(5).ToArray();
    }

    private static TeamDirection DetermineDirection(NewGmScenarioSnapshot scenario, string organizationId, TeamStanding? standing)
    {
        if (standing is not null && standing.GamesPlayed > 8)
        {
            var pointsPct = standing.Points / Math.Max(1.0, standing.GamesPlayed * 2.0);
            if (pointsPct >= 0.62)
            {
                return TeamDirection.WinNow;
            }

            if (pointsPct <= 0.38)
            {
                return TeamDirection.Rebuilder;
            }
        }

        if (organizationId == scenario.Organization.OrganizationId && scenario.Contracts.Sum(contract => contract.Money.SalaryOrStipend) > 160_000m)
        {
            return TeamDirection.BudgetReset;
        }

        return (StableHash($"{organizationId}:{scenario.Season.Year}") % 5) switch
        {
            0 => TeamDirection.WinNow,
            1 => TeamDirection.Rebuilder,
            2 => TeamDirection.ProspectBuild,
            3 => TeamDirection.BudgetReset,
            _ => TeamDirection.Retool
        };
    }

    private static TradeGmPersonality DeterminePersonality(string organizationId, string teamName, TeamDirection direction)
    {
        if (direction == TeamDirection.WinNow)
        {
            return TradeGmPersonality.WinNow;
        }

        if (direction == TeamDirection.Rebuilder)
        {
            return TradeGmPersonality.Rebuilder;
        }

        if (direction == TeamDirection.BudgetReset)
        {
            return TradeGmPersonality.BudgetFocused;
        }

        return (StableHash($"{organizationId}:{teamName}") % 5) switch
        {
            0 => TradeGmPersonality.AggressiveTrader,
            1 => TradeGmPersonality.Conservative,
            2 => TradeGmPersonality.ProspectBuilder,
            3 => TradeGmPersonality.VeteranCollector,
            _ => TradeGmPersonality.DraftPickCollector
        };
    }

    private static IReadOnlyList<AssetPreference> BuildAssetPreferences(TeamDirection direction, TradeGmPersonality personality, IReadOnlyList<TeamNeed> needs)
    {
        var preferences = new List<AssetPreference>();
        if (needs.Any(need => need.Need is PositionNeed.DraftPicks))
        {
            preferences.Add(AssetPreference.DraftPick);
        }

        if (needs.Any(need => need.Need is PositionNeed.ProspectDepth or PositionNeed.YoungerRoster))
        {
            preferences.Add(AssetPreference.ProspectRights);
            preferences.Add(AssetPreference.YoungerPlayer);
        }

        if (needs.Any(need => need.Need is PositionNeed.BudgetRelief or PositionNeed.CapSpaceFuture))
        {
            preferences.Add(AssetPreference.BudgetRelief);
        }

        if (direction == TeamDirection.WinNow || personality is TradeGmPersonality.WinNow or TradeGmPersonality.VeteranCollector)
        {
            preferences.Add(AssetPreference.RosterPlayer);
            preferences.Add(AssetPreference.VeteranLeadership);
        }

        if (personality == TradeGmPersonality.DraftPickCollector)
        {
            preferences.Add(AssetPreference.DraftPick);
        }

        if (preferences.Count == 0)
        {
            preferences.Add(AssetPreference.RosterPlayer);
        }

        return preferences.Distinct().ToArray();
    }

    private static IReadOnlyList<PositionNeed> MatchedNeeds(TeamNeedsProfile profile, IReadOnlyList<TradeAsset> incomingToOtherTeam)
    {
        var matched = new List<PositionNeed>();
        foreach (var need in profile.Needs)
        {
            var fits = need.Need switch
            {
                PositionNeed.StartingGoalie => incomingToOtherTeam.Any(asset => asset.Position == RosterPosition.Goalie),
                PositionNeed.DefensiveDefenseman => incomingToOtherTeam.Any(asset => asset.Position == RosterPosition.Defense),
                PositionNeed.TopSixForward or PositionNeed.ScoringDepth => incomingToOtherTeam.Any(asset => asset.Position is RosterPosition.Center or RosterPosition.LeftWing or RosterPosition.RightWing),
                PositionNeed.ProspectDepth or PositionNeed.YoungerRoster => incomingToOtherTeam.Any(asset => asset.AssetType == TradeAssetType.ProspectRights || asset.Age <= 18),
                PositionNeed.DraftPicks => incomingToOtherTeam.Any(asset => asset.AssetType == TradeAssetType.DraftPick),
                PositionNeed.BudgetRelief or PositionNeed.CapSpaceFuture => incomingToOtherTeam.Sum(asset => asset.SalaryImpact) <= 0m,
                PositionNeed.VeteranLeadership => incomingToOtherTeam.Any(asset => asset.Age >= 19),
                _ => false
            };
            if (fits)
            {
                matched.Add(need.Need);
            }
        }

        return matched.Distinct().ToArray();
    }

    private static int AssetPreferenceScore(TeamNeedsProfile profile, IReadOnlyList<TradeAsset> incomingToOtherTeam)
    {
        var score = 0;
        foreach (var preference in profile.AssetPreferences)
        {
            score += preference switch
            {
                AssetPreference.RosterPlayer when incomingToOtherTeam.Any(asset => asset.AssetType == TradeAssetType.Player) => 5,
                AssetPreference.ProspectRights when incomingToOtherTeam.Any(asset => asset.AssetType == TradeAssetType.ProspectRights) => 7,
                AssetPreference.DraftPick when incomingToOtherTeam.Any(asset => asset.AssetType == TradeAssetType.DraftPick) => 8,
                AssetPreference.FutureConsideration when incomingToOtherTeam.Any(asset => asset.AssetType == TradeAssetType.FutureConsideration) => 2,
                AssetPreference.BudgetRelief when incomingToOtherTeam.Sum(asset => asset.SalaryImpact) <= 0m => 4,
                AssetPreference.VeteranLeadership when incomingToOtherTeam.Any(asset => asset.Age >= 19) => 4,
                AssetPreference.YoungerPlayer when incomingToOtherTeam.Any(asset => asset.Age <= 18) => 5,
                _ => 0
            };
        }

        return score;
    }

    private static int PersonalityScore(TeamNeedsProfile profile, TradeOffer offer) =>
        profile.GmPersonality switch
        {
            TradeGmPersonality.AggressiveTrader when offer.PlayerGives.Count + offer.PlayerReceives.Count > 2 => 6,
            TradeGmPersonality.Conservative when offer.PlayerGives.Count + offer.PlayerReceives.Count > 2 => -4,
            TradeGmPersonality.ProspectBuilder when offer.PlayerGives.Any(asset => asset.AssetType == TradeAssetType.ProspectRights || asset.Age <= 18) => 7,
            TradeGmPersonality.DraftPickCollector when offer.PlayerGives.Any(asset => asset.AssetType == TradeAssetType.DraftPick) => 9,
            TradeGmPersonality.BudgetFocused when offer.PlayerGives.Sum(asset => asset.SalaryImpact) <= offer.PlayerReceives.Sum(asset => asset.SalaryImpact) => 5,
            TradeGmPersonality.VeteranCollector when offer.PlayerGives.Any(asset => asset.Age >= 19) => 6,
            TradeGmPersonality.WinNow when offer.PlayerGives.Any(asset => asset.AssetType == TradeAssetType.Player) => 5,
            _ => 0
        };

    private static int AgeFitScore(TeamNeedsProfile profile, IReadOnlyList<TradeAsset> incomingToOtherTeam)
    {
        if (profile.Direction is TeamDirection.Rebuilder or TeamDirection.ProspectBuild)
        {
            return incomingToOtherTeam.Any(asset => asset.Age <= 18 || asset.AssetType is TradeAssetType.ProspectRights or TradeAssetType.DraftPick) ? 6 : -5;
        }

        if (profile.Direction == TeamDirection.WinNow)
        {
            return incomingToOtherTeam.Any(asset => asset.Age >= 18 && asset.AssetType == TradeAssetType.Player) ? 4 : -3;
        }

        return 0;
    }

    private static int BudgetScore(TeamNeedsProfile profile, decimal otherTeamBudgetImpact)
    {
        if (profile.Needs.Any(need => need.Need is PositionNeed.BudgetRelief or PositionNeed.CapSpaceFuture))
        {
            return otherTeamBudgetImpact <= 0m ? 9 : -10;
        }

        return otherTeamBudgetImpact > 4_000m ? -4 : 0;
    }

    private static IReadOnlyList<string> BuildReasons(
        TeamNeedsProfile profile,
        OrganizationAiProfile aiProfile,
        AiDecisionResult aiDecision,
        TradeOffer offer,
        int score,
        int fitScore,
        int budgetScore,
        IReadOnlyList<PositionNeed> matchedNeeds,
        string counter)
    {
        var reasons = new List<string>
        {
            $"Asset value and fit score from {offer.OtherOrganizationName}'s view: {score}.",
            $"{offer.OtherOrganizationName} direction: {Readable(profile.Direction)}; GM style: {Readable(profile.GmPersonality)}.",
            $"AI strategy: {Readable(aiProfile.Strategy.Phase)}; personality: {Readable(aiProfile.Personality)}.",
            $"Top needs: {string.Join(", ", aiProfile.CurrentNeeds.Take(3).Select(need => Readable(need.NeedType)))}.",
            $"AI decision read: {Readable(aiDecision.Outcome)} ({aiDecision.Score}/100).",
            matchedNeeds.Count == 0 ? "The package does not clearly solve a priority need." : $"Package matches: {string.Join(", ", matchedNeeds.Select(need => Readable(need)))}.",
            $"Roster/strategy fit contribution: {fitScore}.",
            budgetScore >= 0 ? "Budget impact is manageable for their plan." : "Budget impact works against their plan."
        };
        if (!string.IsNullOrWhiteSpace(counter))
        {
            reasons.Add(counter);
        }

        return reasons;
    }

    private static string BuildCounterSuggestion(TeamNeedsProfile profile, TradeOffer offer)
    {
        if (profile.AssetPreferences.Contains(AssetPreference.DraftPick))
        {
            return "They like the framework but need another draft pick included to close the value gap.";
        }

        if (profile.Needs.Any(need => need.Need == PositionNeed.DefensiveDefenseman))
        {
            return "They need a younger defenseman included before moving this player.";
        }

        if (profile.Needs.Any(need => need.Need == PositionNeed.StartingGoalie))
        {
            return "They want a goalie or stronger goaltending prospect in the package.";
        }

        return $"They are not opposed, but {offer.OtherOrganizationName} wants a better roster fit or prospect added.";
    }

    private static IReadOnlyList<TradeAsset> RequestedCounterAssets(NewGmScenarioSnapshot scenario, TradeOffer offer, TeamNeedsProfile profile)
    {
        var service = new TradeService();
        var currentIds = offer.PlayerGives.Select(asset => asset.AssetId).ToHashSet(StringComparer.Ordinal);

        if (profile.AssetPreferences.Contains(AssetPreference.DraftPick))
        {
            return new[]
            {
                service.CreateDraftPickAsset(scenario, TradeSide.PlayerOrganization, scenario.Organization.OrganizationId, scenario.Organization.Name, 3, scenario.Season.Year + 1)
            };
        }

        if (profile.Needs.Any(need => need.Need == PositionNeed.DefensiveDefenseman))
        {
            var defense = service.BuildPlayerOrganizationAssets(scenario)
                .Where(asset => asset.Position == RosterPosition.Defense && !currentIds.Contains(asset.AssetId))
                .OrderBy(asset => asset.AssetType == TradeAssetType.ProspectRights ? 0 : 1)
                .ThenByDescending(asset => asset.Value)
                .FirstOrDefault();
            if (defense is not null)
            {
                return new[] { defense };
            }
        }

        if (profile.Needs.Any(need => need.Need == PositionNeed.StartingGoalie))
        {
            var goalie = service.BuildPlayerOrganizationAssets(scenario)
                .Where(asset => asset.Position == RosterPosition.Goalie && !currentIds.Contains(asset.AssetId))
                .OrderByDescending(asset => asset.Value)
                .FirstOrDefault();
            if (goalie is not null)
            {
                return new[] { goalie };
            }
        }

        var prospect = service.BuildPlayerOrganizationAssets(scenario)
            .Where(asset => asset.AssetType == TradeAssetType.ProspectRights && !currentIds.Contains(asset.AssetId))
            .OrderByDescending(asset => asset.Value)
            .FirstOrDefault();
        if (prospect is not null)
        {
            return new[] { prospect };
        }

        var rosterPlayer = service.BuildPlayerOrganizationAssets(scenario)
            .Where(asset => asset.AssetType == TradeAssetType.Player && !currentIds.Contains(asset.AssetId))
            .OrderByDescending(asset => asset.Value)
            .FirstOrDefault();
        return rosterPlayer is null ? Array.Empty<TradeAsset>() : new[] { rosterPlayer };
    }

    private static string ContractText(NewGmScenarioSnapshot scenario, string personId, out int yearsRemaining, out decimal salary)
    {
        var contract = scenario.Contracts.Concat(scenario.AlphaSnapshot.Contracts)
            .Where(contract => contract.PersonId == personId && contract.Status == ContractStatus.Signed)
            .OrderByDescending(contract => contract.SignedOn ?? contract.OfferedOn)
            .FirstOrDefault();
        if (contract is null)
        {
            yearsRemaining = 0;
            salary = 0m;
            return "No signed contract tracked";
        }

        yearsRemaining = Math.Max(0, contract.Term.EndDate.Year - scenario.CurrentDate.Year);
        salary = contract.Money.SalaryOrStipend;
        return $"{contract.ContractType}, {salary:C0} through {contract.Term.EndDate:yyyy-MM-dd}";
    }

    private static string PersonName(NewGmScenarioSnapshot scenario, string personId, string? fallback = null) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? fallback
        ?? personId;

    private static int PersonAge(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.CalculateAge(scenario.CurrentDate)
        ?? 18;

    private static int EstimateValue(int age, RosterPosition position, int points)
    {
        var ageBonus = age <= 17 ? 12 : age >= 20 ? -4 : 4;
        var positionBonus = position == RosterPosition.Goalie ? 8 : position == RosterPosition.Defense ? 5 : 0;
        return Math.Clamp(30 + points / 5 + ageBonus + positionBonus, 20, 90);
    }

    private static string ValueBand(int value) =>
        value switch
        {
            >= 78 => "Premium league value",
            >= 62 => "Strong league value",
            >= 45 => "Moderate league value",
            _ => "Depth or speculative value"
        };

    private static string Readable(Enum value)
    {
        var text = value.ToString();
        return string.Concat(text.Select((letter, index) => index > 0 && char.IsUpper(letter) ? $" {letter}" : letter.ToString()));
    }

    private static int StableHash(string text)
    {
        unchecked
        {
            var hash = 23;
            foreach (var character in text)
            {
                hash = hash * 31 + character;
            }

            return Math.Abs(hash);
        }
    }
}
