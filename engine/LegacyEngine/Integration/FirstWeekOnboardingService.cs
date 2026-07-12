using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

/// <summary>Keeps the opening desk manageable while leaving all underlying decisions in their workspaces.</summary>
public sealed class FirstWeekOnboardingService
{
    public FirstWeekOnboardingPlan CreatePlan(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var contract = scenario.PlayerRightsDecisions
            .Where(decision => decision.IsOpenDecision)
            .OrderBy(decision => decision.ContractExpiryDate ?? DateOnly.MaxValue)
            .FirstOrDefault();
        var bestProspect = scenario.ProspectRights.OrderBy(prospect => prospect.RoundNumber).ThenBy(prospect => prospect.PickNumber).FirstOrDefault();
        var rosterNeed = scenario.AlphaSnapshot.Roster.Players.Count(player => player.Position == RosterPosition.Defense) < 6
            ? "Add or protect right-defense depth."
            : scenario.AlphaSnapshot.Roster.Players.Count(player => player.Position == RosterPosition.Center) < 4
                ? "Protect center depth and faceoff coverage."
                : "Keep a balanced NHL roster while protecting the prospect path.";
        var window = scenario.TeamSelection.DisplayCurrentStrategy;
        var strengths = new[]
        {
            $"{scenario.AlphaSnapshot.Roster.Players.Count} active roster players are already in place.",
            "The organization has active staff, development plans, and inherited scouting information.",
            scenario.LeagueWorkforce is null ? "The club has existing league context." : "The NHL market has veteran and replacement options available."
        };
        var concerns = new[]
        {
            contract is null ? "Monitor the next contract-rights window." : $"{contract.PlayerName} is an upcoming {contract.RightsStatus} decision.",
            rosterNeed,
            scenario.RetirementConsiderations.Any(item => item.Risk >= RetirementRisk.ConsideringRetirement) ? "At least one veteran is on retirement watch." : "Maintain a clear development path for young players."
        };
        var recommended = contract?.Recommendation ?? "Review the roster recommendation before making depth additions.";
        var briefing = new AssistantGmBriefing(
            TeamIdentity: $"{scenario.Organization.Name} - {scenario.TeamSelection.RosterQuality} roster",
            CompetitiveWindow: window,
            Strengths: strengths,
            Concerns: concerns,
            KeyContractDecision: contract is null ? "No immediate rights deadline." : $"{contract.PlayerName}: {contract.RightsStatus}.",
            BestProspect: bestProspect is null ? "No top prospect is currently flagged." : $"{bestProspect.ProspectName} - {bestProspect.ProjectionText}",
            BiggestRosterNeed: rosterNeed,
            RecommendedFirstAction: recommended,
            Summary: $"{scenario.Organization.Name} enters {window.ToLowerInvariant()} mode with a functioning roster and staff. {recommended}");
        var plan = new FirstWeekOnboardingPlan(
            scenario.CurrentDate,
            briefing,
            FirstDayActionLimit: 3,
            ActionLimitsByDay: new Dictionary<int, int> { [0] = 3, [3] = 5, [5] = 7, [7] = int.MaxValue },
            Summary: "Day 1 focuses on one contract/roster decision, owner context, and an operations recommendation; other work rolls out during the first week.");
        plan.Validate();
        return plan;
    }

    public IReadOnlyList<ActionCenterItem> FilterActionCenterItems(NewGmScenarioSnapshot scenario, IReadOnlyList<ActionCenterItem> items)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(items);
        var plan = scenario.OnboardingPlan ?? CreatePlan(scenario);
        var elapsedDays = Math.Max(0, scenario.CurrentDate.DayNumber - plan.StartDate.DayNumber);
        var limit = plan.ActionLimitsByDay
            .Where(stage => elapsedDays >= stage.Key)
            .OrderByDescending(stage => stage.Key)
            .Select(stage => stage.Value)
            .FirstOrDefault(plan.FirstDayActionLimit);
        var clean = items
            .Where(item => item.Status == ActionCenterStatus.Open)
            .GroupBy(item => item.ActionCenterItemId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        if (limit == int.MaxValue)
        {
            return clean;
        }

        var selected = new List<ActionCenterItem>();
        AddFirst(selected, clean, item => item.ActionCenterItemId.Contains("onboarding:contract", StringComparison.Ordinal));
        AddFirst(selected, clean, item => item.ActionCenterItemId.Contains("onboarding:owner", StringComparison.Ordinal));
        AddFirst(selected, clean, item => item.ActionCenterItemId.Contains("onboarding:roster", StringComparison.Ordinal));

        foreach (var item in clean
                     .Where(item => !selected.Any(chosen => chosen.ActionCenterItemId == item.ActionCenterItemId))
                     .Where(item => item.Category is ActionCenterCategory.Contracts or ActionCenterCategory.Roster or ActionCenterCategory.Owner or ActionCenterCategory.Staff or ActionCenterCategory.Scouting)
                     .OrderByDescending(item => item.Priority)
                     .ThenBy(item => item.DueDate ?? DateOnly.MaxValue)
                     .ThenBy(item => item.Title, StringComparer.Ordinal))
        {
            if (selected.Count >= limit)
            {
                break;
            }

            selected.Add(item);
        }

        return selected.Take(limit).ToArray();
    }

    public IReadOnlyList<AlphaInboxItem> ApplyAssistantGmBriefing(NewGmScenarioSnapshot scenario, IReadOnlyList<AlphaInboxItem> items)
    {
        var plan = scenario.OnboardingPlan ?? CreatePlan(scenario);
        return items.Select(item => item.InboxItemId == "new-gm-inbox-assistant-gm"
            ? item with { Summary = plan.AssistantGmBriefing.Summary }
            : item).ToArray();
    }

    public void AddOpeningActions(NewGmScenarioSnapshot scenario, List<ActionCenterItem> items)
    {
        var plan = scenario.OnboardingPlan ?? CreatePlan(scenario);
        if (scenario.CurrentDate.DayNumber - plan.StartDate.DayNumber > 6)
        {
            return;
        }

        var contract = scenario.PlayerRightsDecisions.Where(decision => decision.IsOpenDecision).OrderBy(decision => decision.ContractExpiryDate ?? DateOnly.MaxValue).FirstOrDefault();
        if (contract is not null)
        {
            items.Add(new ActionCenterItem(
                "action-center:onboarding:contract",
                $"Opening contract review: {contract.PlayerName}",
                ActionCenterCategory.Contracts,
                ActionCenterPriority.Important,
                contract.ExpiryRule?.Deadline ?? contract.ContractExpiryDate,
                contract.PersonId,
                contract.PlayerName,
                scenario.Organization.OrganizationId,
                scenario.Organization.Name,
                contract.Reason,
                "This is the most time-sensitive contract-rights issue inherited from the previous GM.",
                contract.Recommendation,
                null,
                null,
                null));
        }

        items.Add(new ActionCenterItem(
            "action-center:onboarding:owner",
            "Review owner mandate",
            ActionCenterCategory.Owner,
            ActionCenterPriority.Important,
            scenario.CurrentDate.AddDays(7),
            scenario.AlphaSnapshot.Owner.OwnerId,
            scenario.AlphaSnapshot.Owner.Name,
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            plan.AssistantGmBriefing.TeamIdentity,
            "The owner hired a GM to guide an existing organization, not to rebuild every department on Day 1.",
            "Review the Owner workspace and align the first week with the stated mandate.",
            null,
            null,
            null));
        items.Add(new ActionCenterItem(
            "action-center:onboarding:roster",
            "Review inherited roster plan",
            ActionCenterCategory.Roster,
            ActionCenterPriority.Normal,
            scenario.CurrentDate.AddDays(7),
            null,
            null,
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            plan.AssistantGmBriefing.BiggestRosterNeed,
            "Line combinations, assignments, development plans, and contracts already exist. Review them before changing them.",
            plan.AssistantGmBriefing.RecommendedFirstAction,
            null,
            null,
            null));
    }

    private static void AddFirst(ICollection<ActionCenterItem> selected, IEnumerable<ActionCenterItem> candidates, Func<ActionCenterItem, bool> predicate)
    {
        var item = candidates.FirstOrDefault(predicate);
        if (item is not null)
        {
            selected.Add(item);
        }
    }
}
