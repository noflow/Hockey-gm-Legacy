using LegacyEngine.Events;
using LegacyEngine.People;
using LegacyEngine.Rosters;
using LegacyEngine.Staff;

namespace LegacyEngine.Integration;

public sealed class StaffLifeCycleService
{
    public NewGmScenarioSnapshot EnsureLifeCycle(NewGmScenarioSnapshot scenario, EngineRegistry? registry = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var existingMilestones = scenario.StaffMilestones
            .GroupBy(milestone => milestone.MilestoneId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var states = new List<StaffCareerState>();
        var summaries = new List<StaffCareerSummary>();
        var milestones = existingMilestones.Values.ToList();
        var timeline = scenario.CareerTimeline;
        var news = scenario.StaffLifeCycleNews.ToList();

        foreach (var staff in CollectStaff(scenario))
        {
            var person = FindPerson(scenario, staff.PersonId);
            var state = BuildCareerState(scenario, staff, person);
            var generatedMilestones = BuildMilestones(scenario, staff, state).ToArray();
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

            var staffMilestones = milestones
                .Where(milestone => milestone.PersonId == staff.PersonId)
                .OrderBy(milestone => milestone.Date)
                .ToArray();
            summaries.Add(BuildSummary(scenario, staff, person, state, staffMilestones));
            states.Add(state);
        }

        var updated = scenario with
        {
            StaffCareerStates = states
                .OrderBy(item => item.StaffName, StringComparer.Ordinal)
                .ToArray(),
            StaffCareerSummaries = summaries
                .OrderByDescending(item => item.LegacyScore)
                .ThenBy(item => item.StaffName, StringComparer.Ordinal)
                .ToArray(),
            StaffMilestones = milestones
                .OrderByDescending(item => item.Date)
                .ThenBy(item => item.StaffName, StringComparer.Ordinal)
                .ToArray(),
            StaffLifeCycleNews = news
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
        foreach (var summary in scenario.StaffCareerSummaries.Take(30))
        {
            if (summary.PromotionReadiness.Contains("ready", StringComparison.OrdinalIgnoreCase)
                || summary.PromotionReadiness.Contains("candidate", StringComparison.OrdinalIgnoreCase))
            {
                items.Add(new ActionCenterItem(
                    $"action-center:staff-lifecycle:promotion:{summary.PersonId}",
                    $"Promotion review: {summary.StaffName}",
                    ActionCenterCategory.Staff,
                    ActionCenterPriority.Normal,
                    scenario.CurrentDate.AddDays(14),
                    summary.PersonId,
                    summary.StaffName,
                    scenario.Organization.OrganizationId,
                    scenario.Organization.Name,
                    summary.PromotionReadiness,
                    "Internal promotions help build coaching trees and staff continuity.",
                    "Review the staff profile before hiring outside the organization.",
                    null,
                    null,
                    null));
            }

            if (summary.LifeStage == StaffLifeStage.NearRetirement)
            {
                items.Add(new ActionCenterItem(
                    $"action-center:staff-lifecycle:retirement-watch:{summary.PersonId}",
                    $"Succession planning: {summary.StaffName}",
                    ActionCenterCategory.Staff,
                    ActionCenterPriority.Important,
                    scenario.CurrentDate.AddDays(30),
                    summary.PersonId,
                    summary.StaffName,
                    scenario.Organization.OrganizationId,
                    scenario.Organization.Name,
                    $"{summary.StaffName} is nearing the late-career stage.",
                    "Long-serving staff may need succession planning even before retirement logic exists.",
                    "Review coaching tree and internal replacement candidates.",
                    null,
                    null,
                    null));
            }

            if (!string.IsNullOrWhiteSpace(summary.ConcernSummary)
                && !summary.ConcernSummary.StartsWith("No major", StringComparison.OrdinalIgnoreCase))
            {
                items.Add(new ActionCenterItem(
                    $"action-center:staff-lifecycle:review:{summary.PersonId}",
                    $"Staff performance review: {summary.StaffName}",
                    ActionCenterCategory.Staff,
                    ActionCenterPriority.Normal,
                    scenario.CurrentDate.AddDays(21),
                    summary.PersonId,
                    summary.StaffName,
                    scenario.Organization.OrganizationId,
                    scenario.Organization.Name,
                    summary.ConcernSummary,
                    "Staff career concerns can affect development, scouting quality, and department continuity.",
                    "Open the staff profile and review focus, role, relationship, and contract timing.",
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

    public IReadOnlyList<string> BuildReportHighlights(NewGmScenarioSnapshot scenario)
    {
        var lines = new List<string>();
        var topScout = scenario.StaffCareerSummaries
            .Where(summary => summary.Department == StaffDepartment.Scouting)
            .OrderByDescending(summary => summary.LegacyScore)
            .FirstOrDefault();
        var topCoach = scenario.StaffCareerSummaries
            .Where(summary => summary.Department == StaffDepartment.Coaching)
            .OrderByDescending(summary => summary.LegacyScore)
            .FirstOrDefault();
        var promotion = scenario.StaffCareerSummaries
            .FirstOrDefault(summary => summary.PromotionReadiness.Contains("ready", StringComparison.OrdinalIgnoreCase));
        var concern = scenario.StaffCareerSummaries
            .FirstOrDefault(summary => !summary.ConcernSummary.StartsWith("No major", StringComparison.OrdinalIgnoreCase));

        lines.Add(topCoach is null ? "Coach of the year placeholder: no coaching profile available." : $"Coach of the year placeholder: {topCoach.StaffName} - {topCoach.CareerSummaryText}");
        lines.Add(topScout is null ? "Top scout: no scouting profile available." : $"Top scout: {topScout.StaffName} - {topScout.PersonalLegacy}");
        lines.Add(promotion is null ? "Promotion candidates: no urgent internal promotion recommendation." : $"Promotion candidate: {promotion.StaffName} - {promotion.PromotionReadiness}");
        lines.Add(concern is null ? "Staff concerns: no major staff career concern." : $"Staff concern: {concern.StaffName} - {concern.ConcernSummary}");
        return lines;
    }

    public StaffCareerSummary? FindSummary(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.StaffCareerSummaries.FirstOrDefault(summary => summary.PersonId == personId);

    private static IReadOnlyList<StaffMember> CollectStaff(NewGmScenarioSnapshot scenario) =>
        scenario.StaffMembers
            .Concat(scenario.StaffMarket?.Candidates.Select(candidate => candidate.Candidate.StaffMember) ?? Array.Empty<StaffMember>())
            .GroupBy(member => member.PersonId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();

    private static StaffCareerState BuildCareerState(NewGmScenarioSnapshot scenario, StaffMember staff, Person? person)
    {
        var name = StaffName(scenario, staff.PersonId);
        var age = person?.CalculateAge(scenario.CurrentDate) ?? Math.Clamp(28 + staff.Profile.YearsExperience, 24, 72);
        var lifeStage = DetermineLifeStage(staff, age);
        var reputation = BuildReputation(staff, name);
        var phase = DetermineCareerPhase(staff, lifeStage, reputation.Score);
        var legacy = LegacyScore(scenario, staff, reputation.Score);
        var summary = $"{name} is a {lifeStage} {StaffRoles.Title(staff.CurrentRole)} in a {phase} career phase with {staff.Profile.YearsExperience} year(s) of experience.";
        var state = new StaffCareerState(
            staff.PersonId,
            name,
            staff.CurrentRole,
            staff.Department,
            lifeStage,
            phase,
            reputation,
            age,
            staff.Profile.YearsExperience,
            legacy,
            CurrentOrganization(scenario, staff),
            summary);
        state.Validate();
        return state;
    }

    private static StaffCareerSummary BuildSummary(NewGmScenarioSnapshot scenario, StaffMember staff, Person? person, StaffCareerState state, IReadOnlyList<StaffMilestone> milestones)
    {
        var developed = PlayersDeveloped(scenario, staff).ToArray();
        var discovered = PlayersDiscovered(scenario, staff).ToArray();
        var tree = CoachingTree(scenario, staff).ToArray();
        var relationships = StaffRelationships(scenario, staff).ToArray();
        var orgs = Organizations(scenario, staff).ToArray();
        var roles = Roles(scenario, staff).ToArray();
        var salary = SalaryHistory(scenario, staff).ToArray();
        var legacy = PersonalLegacy(staff, developed, discovered, orgs);
        var promotion = PromotionReadiness(staff, state);
        var concern = ConcernSummary(staff, state, relationships);
        var story = CareerStory(staff, state, milestones, developed, discovered, tree, orgs).ToArray();
        var text = $"{state.StaffName} has built a {state.Reputation.Category} {staff.Department.ToString().ToLowerInvariant()} career as a {StaffRoles.Title(staff.CurrentRole)}.";

        var summary = new StaffCareerSummary(
            staff.PersonId,
            state.StaffName,
            staff.CurrentRole,
            staff.Department,
            state.LifeStage,
            state.CareerPhase,
            state.Reputation.Category,
            state.LegacyScore,
            text,
            story,
            milestones,
            orgs,
            roles,
            salary,
            developed,
            discovered,
            tree,
            relationships,
            legacy,
            promotion,
            concern);
        summary.Validate();
        return summary;
    }

    private static IEnumerable<StaffMilestone> BuildMilestones(NewGmScenarioSnapshot scenario, StaffMember staff, StaffCareerState state)
    {
        var hiredOn = HireDate(staff, scenario.CurrentDate.AddYears(-Math.Max(1, staff.Profile.YearsExperience)));
        yield return Milestone(scenario, staff, StaffMilestoneType.Hired, hiredOn, $"{state.StaffName} joined {CurrentOrganization(scenario, staff)} as {StaffRoles.Title(staff.CurrentRole)}.", false);

        if (staff.CurrentRole is StaffRole.HeadCoach or StaffRole.HeadScout or StaffRole.DirectorOfScouting or StaffRole.GeneralManager or StaffRole.AssistantGM)
        {
            yield return Milestone(scenario, staff, StaffMilestoneType.FirstHeadRole, hiredOn.AddYears(Math.Max(0, staff.Profile.YearsExperience / 3)), $"{state.StaffName} reached a lead hockey-operations role as {StaffRoles.Title(staff.CurrentRole)}.", true);
        }

        if (staff.Profile.YearsExperience >= 5)
        {
            yield return Milestone(scenario, staff, StaffMilestoneType.Years5, hiredOn.AddYears(5), $"{state.StaffName} reached five years of staff experience.", false);
        }

        if (staff.Profile.YearsExperience >= 10)
        {
            yield return Milestone(scenario, staff, StaffMilestoneType.Years10, hiredOn.AddYears(10), $"{state.StaffName} reached ten years of staff experience.", true);
        }

        if (staff.Profile.YearsExperience >= 20)
        {
            yield return Milestone(scenario, staff, StaffMilestoneType.Years20, hiredOn.AddYears(20), $"{state.StaffName} reached twenty years in hockey operations.", true);
        }

        if (staff.Department == StaffDepartment.Scouting && PlayersDiscovered(scenario, staff).Any())
        {
            yield return Milestone(scenario, staff, StaffMilestoneType.ProspectDiscovered, scenario.DraftDate.AddDays(-7), $"{state.StaffName} is tied to key prospect recommendations in the current draft class.", true);
        }

        if (staff.Department == StaffDepartment.Coaching && PlayersDeveloped(scenario, staff).Any())
        {
            yield return Milestone(scenario, staff, StaffMilestoneType.PlayerDeveloped, scenario.CurrentDate, $"{state.StaffName} is influencing active player development plans.", true);
        }

        if (CoachingTree(scenario, staff).Any())
        {
            yield return Milestone(scenario, staff, StaffMilestoneType.StaffTreeExpanded, scenario.CurrentDate, $"{state.StaffName} has an emerging staff tree inside the organization.", true);
        }
    }

    private static StaffLifeStage DetermineLifeStage(StaffMember staff, int age)
    {
        if (staff.EmploymentStatus == StaffEmploymentStatus.Released)
        {
            return StaffLifeStage.Retired;
        }

        if (age >= 64 || staff.Profile.YearsExperience >= 32)
        {
            return StaffLifeStage.NearRetirement;
        }

        if (staff.Profile.YearsExperience >= 24)
        {
            return StaffLifeStage.Veteran;
        }

        if (staff.Profile.Reputation >= 84)
        {
            return StaffLifeStage.Elite;
        }

        if (staff.Profile.Reputation >= 70 || staff.Profile.YearsExperience >= 14)
        {
            return StaffLifeStage.Respected;
        }

        if (staff.Profile.YearsExperience >= 7)
        {
            return StaffLifeStage.Established;
        }

        if (staff.CurrentRole is StaffRole.AssistantCoach or StaffRole.AssistantGM or StaffRole.RegionalScout or StaffRole.AmateurScout or StaffRole.AssistantTrainer)
        {
            return StaffLifeStage.Assistant;
        }

        return StaffLifeStage.Prospect;
    }

    private static StaffCareerPhase DetermineCareerPhase(StaffMember staff, StaffLifeStage lifeStage, int reputationScore) =>
        lifeStage switch
        {
            StaffLifeStage.NearRetirement => StaffCareerPhase.Mentor,
            StaffLifeStage.Veteran => StaffCareerPhase.Mentor,
            StaffLifeStage.Elite => StaffCareerPhase.Peak,
            StaffLifeStage.Respected => StaffCareerPhase.Established,
            StaffLifeStage.Established when reputationScore >= 65 => StaffCareerPhase.Rising,
            StaffLifeStage.Assistant => StaffCareerPhase.Learning,
            StaffLifeStage.Retired => StaffCareerPhase.Transition,
            _ => staff.Profile.Reputation < 35 ? StaffCareerPhase.Rebuilding : StaffCareerPhase.Rising
        };

    private static StaffReputation BuildReputation(StaffMember staff, string name)
    {
        var score = Math.Clamp(staff.Profile.Reputation + staff.Profile.YearsExperience / 2 + staff.PerformanceHistory.Count * 2, 0, 100);
        var category = score switch
        {
            >= 92 => StaffReputationCategory.Legendary,
            >= 80 => StaffReputationCategory.Elite,
            >= 62 => StaffReputationCategory.Respected,
            >= 42 => StaffReputationCategory.Promising,
            _ => StaffReputationCategory.Unknown
        };
        var reputation = new StaffReputation(staff.PersonId, category, score, $"{name} has a {category} staff reputation ({score}/100) built from role, experience, and performance history.");
        reputation.Validate();
        return reputation;
    }

    private static int LegacyScore(NewGmScenarioSnapshot scenario, StaffMember staff, int reputationScore)
    {
        var score = reputationScore + staff.Profile.YearsExperience * 2 + staff.PerformanceHistory.Count * 4;
        score += PlayersDeveloped(scenario, staff).Count() * 3;
        score += PlayersDiscovered(scenario, staff).Count() * 4;
        score += scenario.StaffMovementHistory.Count(move => move.PersonId == staff.PersonId) * 2;
        return Math.Clamp(score, 0, 300);
    }

    private static IEnumerable<string> CareerStory(StaffMember staff, StaffCareerState state, IReadOnlyList<StaffMilestone> milestones, IReadOnlyList<string> developed, IReadOnlyList<string> discovered, IReadOnlyList<string> tree, IReadOnlyList<string> organizations)
    {
        yield return $"{state.StaffName} is in the {state.LifeStage} stage with a {state.CareerPhase} career read.";
        yield return $"Longest organization: {organizations.FirstOrDefault() ?? state.CurrentOrganization}.";
        yield return $"Current reputation: {state.Reputation.Category}, legacy score {state.LegacyScore}.";
        if (developed.Count > 0)
        {
            yield return $"Players developed: {string.Join(", ", developed.Take(3))}.";
        }

        if (discovered.Count > 0)
        {
            yield return $"Players discovered/recommended: {string.Join(", ", discovered.Take(3))}.";
        }

        if (tree.Count > 0)
        {
            yield return $"Coaching tree: {string.Join(", ", tree.Take(3))}.";
        }

        foreach (var milestone in milestones.OrderBy(item => item.Date).Take(5))
        {
            yield return milestone.Summary;
        }
    }

    private static IEnumerable<string> PlayersDeveloped(NewGmScenarioSnapshot scenario, StaffMember staff)
    {
        if (staff.Department != StaffDepartment.Coaching)
        {
            yield break;
        }

        foreach (var review in scenario.DevelopmentReviews.OrderByDescending(item => item.ReviewDate).Take(6))
        {
            yield return PersonName(scenario, review.PersonId);
        }

        if (!scenario.DevelopmentReviews.Any())
        {
            foreach (var profile in scenario.AlphaSnapshot.DevelopmentProfiles.Take(4))
            {
                yield return PersonName(scenario, profile.PersonId);
            }
        }
    }

    private static IEnumerable<string> PlayersDiscovered(NewGmScenarioSnapshot scenario, StaffMember staff)
    {
        if (staff.Department != StaffDepartment.Scouting)
        {
            yield break;
        }

        foreach (var report in scenario.CompletedScoutingReports.Where(report => report.ScoutId == staff.PersonId).OrderByDescending(item => item.CreatedOn).Take(6))
        {
            yield return PersonName(scenario, report.PlayerId);
        }

        if (!scenario.CompletedScoutingReports.Any(report => report.ScoutId == staff.PersonId) && staff.CurrentRole is StaffRole.HeadScout or StaffRole.DirectorOfScouting)
        {
            foreach (var prospect in scenario.AlphaSnapshot.DraftBoard.Entries.OrderBy(item => item.Rank).Take(4))
            {
                yield return PersonName(scenario, prospect.ProspectPersonId);
            }
        }
    }

    private static IEnumerable<string> CoachingTree(NewGmScenarioSnapshot scenario, StaffMember staff)
    {
        if (staff.Department != StaffDepartment.Coaching && staff.CurrentRole is not StaffRole.GeneralManager and not StaffRole.AssistantGM)
        {
            yield break;
        }

        foreach (var peer in scenario.StaffMembers.Where(peer => peer.PersonId != staff.PersonId && peer.Department == StaffDepartment.Coaching).Take(4))
        {
            yield return $"{StaffName(scenario, peer.PersonId)} ({StaffRoles.Title(peer.CurrentRole)})";
        }

        foreach (var movement in scenario.StaffMovementHistory.Where(move => move.PersonId != staff.PersonId && move.Role is StaffRole.HeadCoach or StaffRole.AssistantCoach or StaffRole.DevelopmentCoach).Take(3))
        {
            yield return $"{movement.StaffName} ({StaffRoles.Title(movement.Role)})";
        }
    }

    private static IEnumerable<string> StaffRelationships(NewGmScenarioSnapshot scenario, StaffMember staff)
    {
        var links = scenario.AlphaSnapshot.Relationships
            .Where(relationship => relationship.FromPersonId == staff.PersonId || relationship.ToPersonId == staff.PersonId)
            .OrderByDescending(relationship => relationship.LastInteractionDate)
            .Take(4)
            .Select(relationship =>
            {
                var otherId = relationship.FromPersonId == staff.PersonId ? relationship.ToPersonId : relationship.FromPersonId;
                var avg = (relationship.Trust + relationship.Respect + relationship.Confidence + relationship.Loyalty) / 4;
                return $"{PersonName(scenario, otherId)}: {avg}/100 trust/respect/confidence/loyalty blend";
            });

        return links.DefaultIfEmpty("No strong relationship history recorded yet.");
    }

    private static IEnumerable<string> Organizations(NewGmScenarioSnapshot scenario, StaffMember staff)
    {
        var history = scenario.StaffCareerHistory.FirstOrDefault(item => item.PersonId == staff.PersonId);
        yield return CurrentOrganization(scenario, staff);
        if (history is not null)
        {
            foreach (var note in history.NotableHistory.Where(item => item.Contains("organization", StringComparison.OrdinalIgnoreCase)).Take(2))
            {
                yield return note;
            }
        }

        foreach (var movement in scenario.StaffMovementHistory.Where(item => item.PersonId == staff.PersonId))
        {
            if (!string.IsNullOrWhiteSpace(movement.FromTeamName))
            {
                yield return movement.FromTeamName!;
            }

            if (!string.IsNullOrWhiteSpace(movement.ToTeamName))
            {
                yield return movement.ToTeamName!;
            }
        }
    }

    private static IEnumerable<string> Roles(NewGmScenarioSnapshot scenario, StaffMember staff)
    {
        yield return StaffRoles.Title(staff.CurrentRole);
        var history = scenario.StaffCareerHistory.FirstOrDefault(item => item.PersonId == staff.PersonId);
        if (history is not null)
        {
            foreach (var role in history.PreviousRoles.Take(5))
            {
                yield return role;
            }
        }

        foreach (var role in staff.Assignments.Select(assignment => StaffRoles.Title(assignment.Role)).Take(3))
        {
            yield return role;
        }
    }

    private static IEnumerable<string> SalaryHistory(NewGmScenarioSnapshot scenario, StaffMember staff)
    {
        var contract = scenario.Contracts.FirstOrDefault(contract => contract.ContractId == staff.ContractId);
        if (contract is not null)
        {
            yield return $"{contract.Term.StartDate:yyyy-MM-dd}: {contract.Money.Currency} {contract.Money.SalaryOrStipend:C0} as {StaffRoles.Title(staff.CurrentRole)}.";
        }

        foreach (var movement in scenario.StaffMovementHistory.Where(item => item.PersonId == staff.PersonId).Take(3))
        {
            yield return $"{movement.Date:yyyy-MM-dd}: {movement.Summary}";
        }

        if (contract is null)
        {
            yield return "Salary history is not fully tracked yet.";
        }
    }

    private static string PersonalLegacy(StaffMember staff, IReadOnlyList<string> developed, IReadOnlyList<string> discovered, IReadOnlyList<string> organizations)
    {
        if (staff.Department == StaffDepartment.Scouting)
        {
            return discovered.Count == 0
                ? "Greatest prospect found: still building a discovery record."
                : $"Greatest prospect found: {discovered.First()}.";
        }

        if (staff.Department == StaffDepartment.Coaching)
        {
            return developed.Count == 0
                ? "Greatest player developed: still building a development record."
                : $"Greatest player developed: {developed.First()}.";
        }

        if (staff.Department == StaffDepartment.Medical)
        {
            return "Personal legacy: player availability and recovery confidence.";
        }

        return $"Longest organization: {organizations.FirstOrDefault() ?? "not established"}.";
    }

    private static string PromotionReadiness(StaffMember staff, StaffCareerState state) =>
        staff.CurrentRole switch
        {
            StaffRole.AssistantCoach when state.Reputation.Score >= 55 => "Ready for head coach consideration if philosophy and chemistry fit.",
            StaffRole.DevelopmentCoach when state.Reputation.Score >= 58 => "Promotion candidate: development coach could grow into assistant or head coach duties.",
            StaffRole.RegionalScout or StaffRole.AmateurScout or StaffRole.ProfessionalScout or StaffRole.Scout when state.Reputation.Score >= 55 => "Ready for head scout consideration with a larger portfolio.",
            StaffRole.AthleticTherapist or StaffRole.AssistantTrainer when state.Reputation.Score >= 60 => "Ready for senior medical/training responsibility.",
            StaffRole.HeadCoach or StaffRole.HeadScout or StaffRole.DirectorOfScouting when state.LifeStage is StaffLifeStage.Veteran or StaffLifeStage.NearRetirement => "Succession candidate should be identified from the staff tree.",
            _ => "No immediate promotion recommendation."
        };

    private static string ConcernSummary(StaffMember staff, StaffCareerState state, IReadOnlyList<string> relationships)
    {
        if (state.LifeStage == StaffLifeStage.NearRetirement)
        {
            return "Succession planning should be monitored for this late-career staff member.";
        }

        if (staff.Profile.Reputation < 40)
        {
            return "Low reputation makes this staff member a performance review candidate.";
        }

        if (relationships.Any(item => item.Contains("0/100", StringComparison.Ordinal) || item.Contains("1/100", StringComparison.Ordinal)))
        {
            return "Relationship signal suggests communication risk.";
        }

        return "No major staff career concern.";
    }

    private static StaffMilestone Milestone(NewGmScenarioSnapshot scenario, StaffMember staff, StaffMilestoneType type, DateOnly date, string summary, bool notable)
    {
        var milestone = new StaffMilestone(
            $"staff-milestone:{staff.PersonId}:{type}",
            staff.PersonId,
            StaffName(scenario, staff.PersonId),
            type,
            date,
            scenario.Season.Year,
            summary,
            notable);
        milestone.Validate();
        return milestone;
    }

    private static CareerTimelineEntry ToTimelineEntry(NewGmScenarioSnapshot scenario, StaffMilestone milestone) =>
        new(
            $"career:staff-lifecycle:{milestone.MilestoneId}",
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

    private static LeagueTransaction ToLeagueNews(NewGmScenarioSnapshot scenario, StaffMilestone milestone)
    {
        var transaction = new LeagueTransaction(
            $"transaction:staff-lifecycle:{milestone.MilestoneId}",
            new DateTimeOffset(milestone.Date.Year, milestone.Date.Month, milestone.Date.Day, 12, 30, 0, TimeSpan.Zero),
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            milestone.PersonId,
            milestone.StaffName,
            LeagueTransactionType.StaffMilestone,
            LeagueNewsCategory.Staff,
            milestone.Summary);
        transaction.Validate();
        return transaction;
    }

    private static void QueueMilestoneEvent(EngineRegistry? registry, NewGmScenarioSnapshot scenario, StaffMilestone milestone)
    {
        if (registry is null)
        {
            return;
        }

        var legacyEvent = registry.EventEngine.CreateEvent(
            new DateTimeOffset(milestone.Date.Year, milestone.Date.Month, milestone.Date.Day, 12, 45, 0, TimeSpan.Zero),
            LegacyEventType.MilestoneReached,
            milestone.IsNotable ? LegacyEventSeverity.Notice : LegacyEventSeverity.Info,
            LegacyEventVisibility.Organization,
            $"Staff milestone: {milestone.StaffName}",
            milestone.Summary,
            new LegacyEventContext(PrimaryPersonId: milestone.PersonId, OrganizationId: scenario.Organization.OrganizationId, SeasonId: scenario.Season.SeasonId),
            new Dictionary<string, object?>
            {
                ["staff_lifecycle"] = true,
                ["milestone_type"] = milestone.MilestoneType.ToString(),
                ["staff_name"] = milestone.StaffName,
                ["team_name"] = scenario.Organization.Name
            });
        registry.EventEngine.QueueEvent(legacyEvent);
    }

    private static CareerTimelineEntryType TimelineTypeFor(StaffMilestoneType type) =>
        type switch
        {
            StaffMilestoneType.Hired => CareerTimelineEntryType.StaffHired,
            StaffMilestoneType.Released or StaffMilestoneType.Retirement => CareerTimelineEntryType.StaffReleased,
            StaffMilestoneType.ChampionshipPlaceholder => CareerTimelineEntryType.Championship,
            _ => CareerTimelineEntryType.Breakout
        };

    private static DateOnly HireDate(StaffMember staff, DateOnly fallback) =>
        staff.Assignments.OrderBy(item => item.StartDate).FirstOrDefault()?.StartDate
        ?? fallback;

    private static string CurrentOrganization(NewGmScenarioSnapshot scenario, StaffMember staff) =>
        string.Equals(staff.OrganizationId, scenario.Organization.OrganizationId, StringComparison.Ordinal)
            ? scenario.Organization.Name
            : staff.OrganizationId;

    private static string StaffName(NewGmScenarioSnapshot scenario, string personId) =>
        FindPerson(scenario, personId)?.Identity.DisplayName
        ?? scenario.StaffMarket?.Candidates.FirstOrDefault(candidate => candidate.PersonId == personId)?.Name
        ?? personId;

    private static string PersonName(NewGmScenarioSnapshot scenario, string personId) =>
        FindPerson(scenario, personId)?.Identity.DisplayName
        ?? personId;

    private static Person? FindPerson(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)
        ?? scenario.AlphaSnapshot.Players.FirstOrDefault(person => person.PersonId == personId)
        ?? scenario.StaffMarket?.Candidates.Select(candidate => candidate.Candidate.Person).FirstOrDefault(person => person.PersonId == personId);
}
