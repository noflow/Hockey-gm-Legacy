using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public sealed class AiFrontOfficeDecisionService
{
    private const int RoutineLimitPerTeam = 2;
    private const int MajorDecisionCooldownDays = 21;
    private const int MaxLeagueNewsPerCycle = 6;
    private readonly OrganizationPlanningService _planning = new();

    public AiFrontOfficeDecisionResult RunCycle(NewGmScenarioSnapshot scenario, bool force = false)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var prepared = scenario.OrganizationPlans.Count > 0 && scenario.CurrentOrganizationPlan is not null
            ? scenario
            : _planning.EnsurePlans(scenario);
        var schedule = BuildSchedule(prepared);
        if (!force && !schedule.ShouldRun)
        {
            var skipped = new[] { $"Skipped AI front office cycle: {schedule.Reason}" };
            var idleCycle = new AiFrontOfficeDecisionCycle(
                CycleId(prepared.CurrentDate),
                prepared.CurrentDate,
                schedule,
                Array.Empty<AiDecisionCandidate>(),
                Array.Empty<AiDecisionOutcomeRecord>(),
                prepared.AiTransactionPlans,
                prepared.AiDecisionCooldowns,
                prepared.AiEmergencyOverrides,
                skipped,
                "No AI front office review was scheduled today.");
            var unchanged = prepared with { LatestAiDecisionCycle = idleCycle };
            return Result(unchanged, idleCycle, Array.Empty<LeagueTransaction>(), idleCycle.Summary);
        }

        var candidates = new List<AiDecisionCandidate>();
        var transactionPlans = new List<AiTransactionPlan>();
        var skippedDecisions = new List<string>();

        foreach (var plan in prepared.OrganizationPlans.OrderBy(plan => plan.OrganizationName, StringComparer.Ordinal))
        {
            if (plan.OrganizationId == prepared.Organization.OrganizationId)
            {
                skippedDecisions.Add($"{plan.OrganizationName}: player-controlled organization is advisory-only; no automatic AI action.");
                continue;
            }

            var teamCandidates = BuildCandidatesForPlan(prepared, plan, schedule.Window);
            var allowed = ApplyFrequencyControls(prepared, teamCandidates, skippedDecisions).ToArray();
            candidates.AddRange(allowed);
            transactionPlans.Add(BuildTransactionPlan(prepared, plan, schedule.Window, allowed));
        }

        var outcomes = candidates.Select(candidate => BuildOutcome(prepared, candidate)).ToArray();
        var cooldowns = MergeCooldowns(prepared, outcomes);
        var emergencies = MergeEmergencyOverrides(prepared, candidates);
        var history = MergeHistory(prepared, outcomes);
        var news = BuildLeagueNews(prepared, outcomes);
        var cycle = new AiFrontOfficeDecisionCycle(
            CycleId(prepared.CurrentDate),
            prepared.CurrentDate,
            schedule,
            candidates,
            outcomes,
            transactionPlans,
            cooldowns,
            emergencies,
            skippedDecisions,
            $"AI front offices reviewed {candidates.Count} candidate(s), recorded {outcomes.Count(item => item.Outcome == AiDecisionOutcome.Accept)} action plan(s), and created {news.Count} notable league item(s).");
        cycle.Validate();

        var updated = prepared with
        {
            LatestAiDecisionCycle = cycle,
            AiDecisionHistory = history,
            AiTransactionPlans = transactionPlans,
            AiDecisionCooldowns = cooldowns,
            AiEmergencyOverrides = emergencies
        };
        updated.Validate();
        return Result(updated, cycle, news, cycle.Summary);
    }

    public AiDecisionSchedule BuildSchedule(NewGmScenarioSnapshot scenario)
    {
        var window = WindowFor(scenario);
        var shouldRun = window is AiDecisionWindow.Draft
            or AiDecisionWindow.TradeDeadline
            or AiDecisionWindow.ContractRightsPeriod
            or AiDecisionWindow.FreeAgency
            or AiDecisionWindow.TrainingCamp
            || scenario.CurrentDate.Day is 1 or 15;
        var reason = shouldRun
            ? $"{Readable(window)} review is active for {scenario.CurrentDate:yyyy-MM-dd}."
            : $"No scheduled front-office review for {scenario.CurrentDate:yyyy-MM-dd}; next routine reviews run on the 1st and 15th.";
        return new AiDecisionSchedule(scenario.CurrentDate, window, shouldRun, reason);
    }

    public IReadOnlyList<AiDecisionCandidate> BuildCandidatesForPlan(NewGmScenarioSnapshot scenario, OrganizationPlan plan, AiDecisionWindow window)
    {
        var candidates = new List<AiDecisionCandidate>();
        var topNeed = plan.RosterPlan.FutureNeeds.FirstOrDefault() ?? "Maintain roster balance.";
        candidates.Add(BuildRosterCandidate(scenario, plan, window, topNeed));
        candidates.Add(BuildProspectCandidate(scenario, plan, window));
        candidates.Add(BuildContractCandidate(scenario, plan, window));

        if (window is AiDecisionWindow.TradeDeadline or AiDecisionWindow.MonthlyReview or AiDecisionWindow.EarlySeason or AiDecisionWindow.EarlyOffseason)
        {
            candidates.Add(BuildTradeCandidate(scenario, plan, window));
        }

        if (window is AiDecisionWindow.FreeAgency or AiDecisionWindow.EarlyOffseason or AiDecisionWindow.MonthlyReview)
        {
            candidates.Add(BuildFreeAgencyCandidate(scenario, plan, window));
        }

        if (window is AiDecisionWindow.Draft or AiDecisionWindow.DraftPreparation)
        {
            candidates.Add(BuildDraftCandidate(scenario, plan, window));
        }

        if (window is AiDecisionWindow.Preseason or AiDecisionWindow.MonthlyReview or AiDecisionWindow.EarlyOffseason)
        {
            candidates.Add(BuildStaffCandidate(scenario, plan, window));
        }

        if (plan.Window == CompetitiveWindow.Rebuild)
        {
            candidates.Add(BuildRebuildCandidate(scenario, plan, window));
        }
        else if (plan.Window is CompetitiveWindow.Contending or CompetitiveWindow.AllIn)
        {
            candidates.Add(BuildContenderCandidate(scenario, plan, window));
        }
        else if (plan.Window == CompetitiveWindow.Developing)
        {
            candidates.Add(BuildDevelopingCandidate(scenario, plan, window));
        }
        else if (plan.ContractPlan.CapBudgetSummary.Contains("pressure", StringComparison.OrdinalIgnoreCase)
            || plan.Window == CompetitiveWindow.Declining)
        {
            candidates.Add(BuildBudgetResetCandidate(scenario, plan, window));
        }

        foreach (var candidate in candidates)
        {
            candidate.Validate();
        }

        return candidates
            .OrderByDescending(candidate => (int)candidate.Priority)
            .ThenBy(candidate => candidate.DecisionType)
            .Take(6)
            .ToArray();
    }

    public string BuildFrontOfficeText(NewGmScenarioSnapshot scenario, string organizationId, string teamName)
    {
        var prepared = scenario.LatestAiDecisionCycle is null && scenario.AiTransactionPlans.Count == 0
            ? RunCycle(scenario, force: true).ScenarioSnapshot
            : scenario;
        var plan = prepared.OrganizationPlans.FirstOrDefault(item => item.OrganizationId == organizationId)
            ?? _planning.BuildPlan(prepared, organizationId, teamName);
        var transactionPlan = prepared.AiTransactionPlans.FirstOrDefault(item => item.OrganizationId == organizationId);
        var recent = prepared.AiDecisionHistory
            .Where(item => item.OrganizationId == organizationId)
            .OrderByDescending(item => item.Date)
            .Take(5)
            .ToArray();
        var cycleCandidates = prepared.LatestAiDecisionCycle?.Candidates
            .Where(item => item.OrganizationId == organizationId)
            .Take(5)
            .ToArray() ?? Array.Empty<AiDecisionCandidate>();

        var lines = new List<string>
        {
            $"AI Front Office - {plan.OrganizationName}",
            $"Plan: {OrganizationPlanningService.Readable(plan.Window)} | {plan.RosterPlan.FutureNeeds.FirstOrDefault() ?? plan.Summary}",
            $"Current priorities: {string.Join("; ", plan.RosterPlan.FutureNeeds.Take(3))}",
            $"Assets being shopped: {string.Join("; ", transactionPlan?.AssetsBeingShopped.Take(3) ?? Array.Empty<string>())}",
            $"Likely targets: {string.Join("; ", transactionPlan?.LikelyTargets.Take(3) ?? plan.TradeTargets.Take(3))}",
            $"Contract priorities: {string.Join("; ", transactionPlan?.ContractPriorities.Take(3) ?? plan.ContractPlan.ExtensionTargets.Take(3).Select(item => item.PlayerName))}",
            $"Prospect decisions: {string.Join("; ", transactionPlan?.ProspectDecisions.Take(3) ?? plan.ProspectPlan.Prospects.Take(3).Select(item => item.Recommendation))}",
            $"Staff priorities: {string.Join("; ", transactionPlan?.StaffPriorities.Take(3) ?? Array.Empty<string>())}",
            string.Empty,
            "Recent AI decisions:"
        };
        lines.AddRange(recent.Length == 0
            ? new[] { "- No completed front-office decisions yet." }
            : recent.Select(item => $"- {item.Date:yyyy-MM-dd}: {item.DecisionType} / {item.Priority} / {item.Outcome} - {item.Explanation}"));
        lines.Add(string.Empty);
        lines.Add("Current decision queue:");
        lines.AddRange(cycleCandidates.Length == 0
            ? new[] { "- No active AI candidates for this team in the latest cycle." }
            : cycleCandidates.Select(item => $"- {item.Priority}: {item.Title} - {item.Explanation.Summary}"));
        return string.Join(Environment.NewLine, lines);
    }

    public AiDecisionCandidate BuildRosterCandidate(NewGmScenarioSnapshot scenario, OrganizationPlan plan, AiDecisionWindow window, string need) =>
        Candidate(
            scenario,
            plan,
            window,
            AiFrontOfficeDecisionType.Roster,
            need.Contains("goalie", StringComparison.OrdinalIgnoreCase) || need.Contains("illegal", StringComparison.OrdinalIgnoreCase) ? AiDecisionPriority.Urgent : AiDecisionPriority.Useful,
            need.Contains("goalie", StringComparison.OrdinalIgnoreCase) ? "Stabilize goalie depth" : "Review roster balance",
            need,
            "Keep the roster legal and aligned with the organization plan.",
            scenario.CurrentDate.AddDays(7),
            "Waiting can leave the lineup short or force a rushed transaction.",
            "May require a depth signing, waiver move, or assignment.",
            "Protects lineup balance without daily roster churn.",
            72,
            new[] { "Internal recall", "Short-term signing", "Hold roster if legal" },
            null,
            null);

    public AiDecisionCandidate BuildProspectCandidate(NewGmScenarioSnapshot scenario, OrganizationPlan plan, AiDecisionWindow window)
    {
        var prospect = plan.ProspectPlan.Prospects.FirstOrDefault();
        var title = prospect is null ? "Review prospect pipeline" : $"Set path for {prospect.PlayerName}";
        var reason = prospect is null
            ? "Pipeline has no immediate high-priority prospect decision."
            : prospect.Recommendation;
        return Candidate(
            scenario,
            plan,
            window,
            AiFrontOfficeDecisionType.Prospect,
            prospect?.IsBlocked == true ? AiDecisionPriority.Important : AiDecisionPriority.Useful,
            title,
            reason,
            "Move prospects according to readiness, eligibility, and timeline.",
            window is AiDecisionWindow.TrainingCamp ? scenario.CurrentDate.AddDays(3) : null,
            "Wrong placement can stall development or block a better fit.",
            "May delay short-term lineup help.",
            "Protects development path and future depth.",
            prospect?.IsBlocked == true ? 76 : 66,
            new[] { "Leave at current level", "Promote carefully", "Trade if permanently blocked" },
            prospect?.PersonId,
            prospect?.PlayerName);
    }

    public AiDecisionCandidate BuildContractCandidate(NewGmScenarioSnapshot scenario, OrganizationPlan plan, AiDecisionWindow window)
    {
        var target = plan.ContractPlan.ExtensionTargets.FirstOrDefault() ?? plan.ContractPlan.ExpiringContracts.FirstOrDefault();
        return Candidate(
            scenario,
            plan,
            window,
            AiFrontOfficeDecisionType.Contract,
            target is null ? AiDecisionPriority.Routine : AiDecisionPriority.Important,
            target is null ? "Monitor contract board" : $"Resolve contract path for {target.PlayerName}",
            target?.Recommendation ?? plan.ContractPlan.Summary,
            "Make contract decisions that fit value, role, and future commitments.",
            scenario.CurrentDate.AddDays(window is AiDecisionWindow.ContractRightsPeriod ? 3 : 21),
            "Waiting can lose leverage or create rights pressure.",
            "May use cap or budget room.",
            "Keeps useful players without extending everyone automatically.",
            target is null ? 55 : 74,
            new[] { "Qualify core RFA", "Walk away from low-value rights", "Delay expensive extension" },
            target?.PersonId,
            target?.PlayerName);
    }

    public AiDecisionCandidate BuildTradeCandidate(NewGmScenarioSnapshot scenario, OrganizationPlan plan, AiDecisionWindow window)
    {
        var target = plan.TradeTargets.FirstOrDefault() ?? "No urgent trade pressure.";
        var rebuild = plan.Window == CompetitiveWindow.Rebuild;
        return Candidate(
            scenario,
            plan,
            window,
            AiFrontOfficeDecisionType.Trade,
            window == AiDecisionWindow.TradeDeadline ? AiDecisionPriority.Urgent : AiDecisionPriority.Important,
            rebuild ? "Shop expiring veteran for future assets" : "Explore strategy-consistent trade market",
            target,
            rebuild ? "Prioritize picks and prospects." : "Use trades to support the competitive window.",
            window == AiDecisionWindow.TradeDeadline ? scenario.CurrentDate.AddDays(1) : scenario.CurrentDate.AddDays(14),
            "The market can move before the club addresses its need.",
            rebuild ? "May reduce current roster strength." : "May require picks or prospects.",
            rebuild ? "Improves future asset base." : "Targets a specific roster weakness.",
            window == AiDecisionWindow.TradeDeadline ? 82 : 70,
            new[] { "Stand pat", "Shop surplus asset", "Counter only if fit improves" },
            null,
            null);
    }

    public AiDecisionCandidate BuildFreeAgencyCandidate(NewGmScenarioSnapshot scenario, OrganizationPlan plan, AiDecisionWindow window)
    {
        var target = plan.FreeAgencyTargets.FirstOrDefault() ?? "Hold cap and budget flexibility.";
        var budgetReset = plan.Window is CompetitiveWindow.Declining || plan.ContractPlan.CapBudgetSummary.Contains("pressure", StringComparison.OrdinalIgnoreCase);
        return Candidate(
            scenario,
            plan,
            window,
            AiFrontOfficeDecisionType.FreeAgency,
            budgetReset ? AiDecisionPriority.Important : AiDecisionPriority.Useful,
            budgetReset ? "Avoid expensive free-agent bidding" : "Prepare free-agent target list",
            target,
            "Fill needs without blocking priority prospects.",
            scenario.CurrentDate.AddDays(10),
            "Poor discipline can create duplicate signings or block young players.",
            "May require salary/term limits.",
            "Creates priority and fallback targets before the market opens.",
            budgetReset ? 78 : 67,
            new[] { "Priority target", "Fallback target", "Late-market bargain" },
            null,
            null);
    }

    public AiDecisionCandidate BuildDraftCandidate(NewGmScenarioSnapshot scenario, OrganizationPlan plan, AiDecisionWindow window) =>
        Candidate(
            scenario,
            plan,
            window,
            AiFrontOfficeDecisionType.Draft,
            window == AiDecisionWindow.Draft ? AiDecisionPriority.Critical : AiDecisionPriority.Important,
            "Align draft board with team needs",
            $"Draft board plan should support team need: {plan.RosterPlan.FutureNeeds.FirstOrDefault() ?? "future depth"} while preserving elite-talent exceptions.",
            "Draft using board, need, identity, position scarcity, and pipeline fit.",
            scenario.DraftDate,
            "Poor board discipline can over-draft one position or ignore value drops.",
            "May pass on a need when elite talent falls.",
            "Improves pipeline and future depth plan.",
            84,
            new[] { "Best player available", "Need-based tie-breaker", "Trade pick only if value aligns" },
            null,
            null);

    public AiDecisionCandidate BuildStaffCandidate(NewGmScenarioSnapshot scenario, OrganizationPlan plan, AiDecisionWindow window) =>
        Candidate(
            scenario,
            plan,
            window,
            AiFrontOfficeDecisionType.Staff,
            AiDecisionPriority.Useful,
            "Review staff market fit",
            plan.Window is CompetitiveWindow.Rebuild or CompetitiveWindow.Developing
                ? "Development and scouting staff must support the long-term pipeline."
                : "Staff decisions should support current competitive expectations.",
            "Fill vacancies and avoid poor chemistry hires.",
            scenario.CurrentDate.AddDays(30),
            "Waiting can leave a department thin.",
            "May require budget room.",
            "Improves department fit without regenerating candidates.",
            62,
            new[] { "Promote internal candidate", "Hire market fit", "Defer if chemistry risk is high" },
            null,
            null);

    private static AiDecisionCandidate BuildRebuildCandidate(NewGmScenarioSnapshot scenario, OrganizationPlan plan, AiDecisionWindow window) =>
        Candidate(
            scenario,
            plan,
            window,
            AiFrontOfficeDecisionType.Trade,
            AiDecisionPriority.Important,
            "Begin rebuild asset review",
            "Rebuilding team should shop expiring veterans and avoid expensive aging UFAs.",
            "Accumulate picks and prospects.",
            scenario.CurrentDate.AddDays(21),
            "Veteran value can decline if the market dries up.",
            "Short-term roster quality may fall.",
            "Creates future flexibility.",
            80,
            new[] { "Trade veteran", "Hold until deadline", "Package for younger asset" },
            null,
            null);

    private static AiDecisionCandidate BuildContenderCandidate(NewGmScenarioSnapshot scenario, OrganizationPlan plan, AiDecisionWindow window) =>
        Candidate(
            scenario,
            plan,
            window,
            AiFrontOfficeDecisionType.Trade,
            AiDecisionPriority.Important,
            "Protect championship window",
            "Contender should target immediate roster need and keep useful veterans.",
            "Improve current lineup without unnecessary development projects.",
            scenario.CurrentDate.AddDays(14),
            "A rival may acquire the better fit first.",
            "May spend future assets.",
            "Supports playoff push.",
            78,
            new[] { "Acquire rental", "Use internal depth", "Preserve top prospect if price is too high" },
            null,
            null);

    private static AiDecisionCandidate BuildDevelopingCandidate(NewGmScenarioSnapshot scenario, OrganizationPlan plan, AiDecisionWindow window) =>
        Candidate(
            scenario,
            plan,
            window,
            AiFrontOfficeDecisionType.Prospect,
            AiDecisionPriority.Important,
            "Avoid blocking top prospect",
            "Developing team should promote carefully and avoid blocking important prospects.",
            "Create a patient path for young players.",
            scenario.CurrentDate.AddDays(30),
            "Wrong role can stall a prospect.",
            "May require a short-term veteran bridge instead of a long contract.",
            "Keeps prospect pipeline moving.",
            76,
            new[] { "Leave at current level", "Promote carefully", "Bridge veteran", "AHL assignment", "Increase role only when ready" },
            null,
            null);

    private static AiDecisionCandidate BuildBudgetResetCandidate(NewGmScenarioSnapshot scenario, OrganizationPlan plan, AiDecisionWindow window) =>
        Candidate(
            scenario,
            plan,
            window,
            AiFrontOfficeDecisionType.FreeAgency,
            AiDecisionPriority.Important,
            "Preserve budget flexibility",
            "Budget-reset team should avoid expensive free agents and move expensive contracts where possible.",
            "Reset commitments without creating permanent roster holes.",
            scenario.CurrentDate.AddDays(14),
            "Waiting can leave fewer cheap replacements.",
            "May miss a high-profile target.",
            "Improves future cap/budget health.",
            79,
            new[] { "Late-market bargain", "Internal replacement", "Salary-clearing trade" },
            null,
            null);

    private static AiDecisionCandidate Candidate(
        NewGmScenarioSnapshot scenario,
        OrganizationPlan plan,
        AiDecisionWindow window,
        AiFrontOfficeDecisionType type,
        AiDecisionPriority priority,
        string title,
        string reason,
        string goal,
        DateOnly? deadline,
        string risk,
        string cost,
        string benefit,
        int confidence,
        IReadOnlyList<string> alternatives,
        string? personId,
        string? personName)
    {
        var candidateId = $"ai-candidate:{plan.OrganizationId}:{scenario.CurrentDate:yyyyMMdd}:{type}:{StableHash(title + reason) % 10000}";
        var explanation = new AiDecisionExplanation(
            $"{plan.OrganizationName}: {reason}",
            $"{OrganizationPlanningService.Readable(plan.Window)} plan: {plan.Summary}",
            $"{OrganizationPlanningService.Readable(window)} window on {scenario.CurrentDate:yyyy-MM-dd}.",
            risk);
        return new AiDecisionCandidate(
            candidateId,
            plan.OrganizationId,
            plan.OrganizationName,
            window,
            type,
            priority,
            title,
            reason,
            goal,
            deadline,
            risk,
            cost,
            benefit,
            confidence,
            alternatives,
            personId,
            personName,
            explanation);
    }

    private static IReadOnlyList<AiDecisionCandidate> ApplyFrequencyControls(
        NewGmScenarioSnapshot scenario,
        IReadOnlyList<AiDecisionCandidate> candidates,
        List<string> skipped)
    {
        var selected = new List<AiDecisionCandidate>();
        var routineCount = 0;
        foreach (var candidate in candidates)
        {
            var cooldown = scenario.AiDecisionCooldowns.FirstOrDefault(item =>
                item.OrganizationId == candidate.OrganizationId
                && item.DecisionType == candidate.DecisionType
                && item.Until >= scenario.CurrentDate);
            if (cooldown is not null)
            {
                skipped.Add($"{candidate.TeamName}: skipped {candidate.Title}; cooldown until {cooldown.Until:yyyy-MM-dd}.");
                continue;
            }

            if (candidate.Priority is AiDecisionPriority.Routine or AiDecisionPriority.Useful)
            {
                if (routineCount >= RoutineLimitPerTeam)
                {
                    skipped.Add($"{candidate.TeamName}: skipped routine decision '{candidate.Title}' to avoid transaction churn.");
                    continue;
                }

                routineCount++;
            }

            selected.Add(candidate);
        }

        return selected.Take(4).ToArray();
    }

    private static AiDecisionOutcomeRecord BuildOutcome(NewGmScenarioSnapshot scenario, AiDecisionCandidate candidate)
    {
        var outcome = candidate.Priority switch
        {
            AiDecisionPriority.Critical or AiDecisionPriority.Urgent => AiDecisionOutcome.Accept,
            AiDecisionPriority.Important when candidate.Confidence >= 70 => AiDecisionOutcome.Accept,
            AiDecisionPriority.Important => AiDecisionOutcome.Counter,
            AiDecisionPriority.Useful => AiDecisionOutcome.Wait,
            _ => AiDecisionOutcome.Wait
        };
        var major = candidate.Priority is AiDecisionPriority.Important or AiDecisionPriority.Urgent or AiDecisionPriority.Critical;
        var action = outcome switch
        {
            AiDecisionOutcome.Accept => $"Proceed with {candidate.DecisionType.ToString().ToLowerInvariant()} plan.",
            AiDecisionOutcome.Counter => $"Counter or revise {candidate.DecisionType.ToString().ToLowerInvariant()} plan.",
            _ => $"Monitor {candidate.DecisionType.ToString().ToLowerInvariant()} path."
        };
        return new AiDecisionOutcomeRecord(
            $"ai-outcome:{candidate.CandidateId}",
            candidate.CandidateId,
            candidate.OrganizationId,
            candidate.TeamName,
            candidate.DecisionType,
            candidate.Priority,
            outcome,
            scenario.CurrentDate,
            action,
            $"{candidate.TeamName} chose '{action}' because {candidate.Explanation.Summary}",
            major,
            major && outcome is AiDecisionOutcome.Accept or AiDecisionOutcome.Counter);
    }

    private static IReadOnlyList<AiDecisionCooldown> MergeCooldowns(NewGmScenarioSnapshot scenario, IReadOnlyList<AiDecisionOutcomeRecord> outcomes)
    {
        var active = scenario.AiDecisionCooldowns
            .Where(item => item.Until >= scenario.CurrentDate)
            .ToList();
        foreach (var outcome in outcomes.Where(item => item.IsMajorDecision))
        {
            active.RemoveAll(item => item.OrganizationId == outcome.OrganizationId && item.DecisionType == outcome.DecisionType);
            active.Add(new AiDecisionCooldown(
                outcome.OrganizationId,
                outcome.DecisionType,
                scenario.CurrentDate.AddDays(MajorDecisionCooldownDays),
                $"{outcome.TeamName} completed a major {outcome.DecisionType} review."));
        }

        return active.ToArray();
    }

    private static IReadOnlyList<AiEmergencyOverride> MergeEmergencyOverrides(NewGmScenarioSnapshot scenario, IReadOnlyList<AiDecisionCandidate> candidates)
    {
        var active = scenario.AiEmergencyOverrides
            .Where(item => item.EndDate >= scenario.CurrentDate)
            .ToList();
        foreach (var candidate in candidates.Where(item => item.Priority is AiDecisionPriority.Urgent or AiDecisionPriority.Critical && item.DecisionType is AiFrontOfficeDecisionType.Roster or AiFrontOfficeDecisionType.Emergency))
        {
            if (active.Any(item => item.OrganizationId == candidate.OrganizationId && item.Reason == candidate.Reason))
            {
                continue;
            }

            active.Add(new AiEmergencyOverride(
                $"ai-override:{candidate.OrganizationId}:{scenario.CurrentDate:yyyyMMdd}:{candidate.DecisionType}",
                candidate.OrganizationId,
                candidate.TeamName,
                scenario.CurrentDate,
                scenario.CurrentDate.AddDays(14),
                candidate.Reason,
                candidate.OrganizationalGoal,
                true));
        }

        return active.ToArray();
    }

    private static IReadOnlyList<AiDecisionHistoryEntry> MergeHistory(NewGmScenarioSnapshot scenario, IReadOnlyList<AiDecisionOutcomeRecord> outcomes)
    {
        var existingIds = scenario.AiDecisionHistory.Select(item => item.HistoryId).ToHashSet(StringComparer.Ordinal);
        var additions = outcomes
            .Select(outcome => new AiDecisionHistoryEntry(
                $"ai-history:{outcome.OutcomeId}",
                outcome.OrganizationId,
                outcome.TeamName,
                outcome.Date,
                WindowFor(scenario),
                outcome.DecisionType,
                outcome.Priority,
                outcome.Outcome,
                outcome.ActionTaken,
                outcome.Explanation))
            .Where(item => !existingIds.Contains(item.HistoryId))
            .ToArray();
        return scenario.AiDecisionHistory.Concat(additions).TakeLast(400).ToArray();
    }

    private static IReadOnlyList<LeagueTransaction> BuildLeagueNews(NewGmScenarioSnapshot scenario, IReadOnlyList<AiDecisionOutcomeRecord> outcomes)
    {
        return outcomes
            .Where(outcome => outcome.CreatedLeagueNews)
            .OrderByDescending(outcome => (int)outcome.Priority)
            .ThenBy(outcome => outcome.TeamName, StringComparer.Ordinal)
            .Take(MaxLeagueNewsPerCycle)
            .Select(outcome => new LeagueTransaction(
                $"transaction:ai-front-office:{outcome.OrganizationId}:{outcome.Date:yyyyMMdd}:{outcome.DecisionType}:{StableHash(outcome.Explanation)}",
                new DateTimeOffset(outcome.Date.Year, outcome.Date.Month, outcome.Date.Day, 12, 0, 0, TimeSpan.Zero),
                outcome.OrganizationId,
                outcome.TeamName,
                null,
                "Front Office",
                TransactionTypeFor(outcome.DecisionType),
                CategoryFor(outcome.DecisionType),
                $"{outcome.TeamName}: {outcome.Explanation}"))
            .ToArray();
    }

    private static AiTransactionPlan BuildTransactionPlan(
        NewGmScenarioSnapshot scenario,
        OrganizationPlan plan,
        AiDecisionWindow window,
        IReadOnlyList<AiDecisionCandidate> candidates)
    {
        var assets = candidates
            .Where(item => item.DecisionType == AiFrontOfficeDecisionType.Trade && item.Title.Contains("shop", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Title)
            .DefaultIfEmpty(plan.Window == CompetitiveWindow.Rebuild ? "Expiring veteran contracts" : "No active shop list.")
            .Take(4)
            .ToArray();
        var staff = candidates
            .Where(item => item.DecisionType == AiFrontOfficeDecisionType.Staff)
            .Select(item => item.Reason)
            .DefaultIfEmpty("Monitor department fit.")
            .Take(3)
            .ToArray();
        var prospects = candidates
            .Where(item => item.DecisionType == AiFrontOfficeDecisionType.Prospect)
            .Select(item => item.Reason)
            .DefaultIfEmpty(plan.ProspectPlan.Prospects.FirstOrDefault()?.Recommendation ?? "No urgent prospect move.")
            .Take(4)
            .ToArray();
        return new AiTransactionPlan(
            plan.OrganizationId,
            plan.OrganizationName,
            window,
            assets,
            plan.TradeTargets.Take(5).ToArray(),
            plan.ContractPlan.ExtensionTargets.Select(item => $"{item.PlayerName}: {item.Recommendation}").DefaultIfEmpty(plan.ContractPlan.Summary).Take(5).ToArray(),
            prospects,
            staff,
            plan.FreeAgencyTargets.Take(5).ToArray(),
            scenario.CurrentDate,
            $"{plan.OrganizationName} transaction plan follows {OrganizationPlanningService.Readable(plan.Window).ToLowerInvariant()} window and {OrganizationPlanningService.Readable(window).ToLowerInvariant()} timing.");
    }

    private static LeagueTransactionType TransactionTypeFor(AiFrontOfficeDecisionType type) =>
        type switch
        {
            AiFrontOfficeDecisionType.Contract or AiFrontOfficeDecisionType.FreeAgency => LeagueTransactionType.ContractOffered,
            AiFrontOfficeDecisionType.Trade => LeagueTransactionType.TradeCompleted,
            AiFrontOfficeDecisionType.Draft => LeagueTransactionType.DraftPick,
            AiFrontOfficeDecisionType.Staff => LeagueTransactionType.StaffHired,
            AiFrontOfficeDecisionType.Waiver or AiFrontOfficeDecisionType.Roster or AiFrontOfficeDecisionType.Prospect => LeagueTransactionType.PlayerAssigned,
            _ => LeagueTransactionType.TeamIdentityUpdate
        };

    private static LeagueNewsCategory CategoryFor(AiFrontOfficeDecisionType type) =>
        type switch
        {
            AiFrontOfficeDecisionType.Contract or AiFrontOfficeDecisionType.FreeAgency or AiFrontOfficeDecisionType.Arbitration or AiFrontOfficeDecisionType.Buyout or AiFrontOfficeDecisionType.OfferSheet => LeagueNewsCategory.Signings,
            AiFrontOfficeDecisionType.Trade or AiFrontOfficeDecisionType.Roster or AiFrontOfficeDecisionType.Prospect or AiFrontOfficeDecisionType.Waiver => LeagueNewsCategory.RosterMoves,
            AiFrontOfficeDecisionType.Draft => LeagueNewsCategory.Draft,
            AiFrontOfficeDecisionType.Staff => LeagueNewsCategory.Staff,
            _ => LeagueNewsCategory.League
        };

    private static AiDecisionWindow WindowFor(NewGmScenarioSnapshot scenario)
    {
        if (scenario.CurrentDate == scenario.DraftDate)
        {
            return AiDecisionWindow.Draft;
        }

        if (scenario.CurrentDate < scenario.DraftDate && scenario.DaysUntilDraft <= 14)
        {
            return AiDecisionWindow.DraftPreparation;
        }

        if (scenario.CurrentDate > scenario.DraftDate && scenario.CurrentDate <= scenario.DraftDate.AddDays(10))
        {
            return AiDecisionWindow.ContractRightsPeriod;
        }

        if (scenario.CurrentDate.Month is 7 or 8)
        {
            return AiDecisionWindow.FreeAgency;
        }

        var campOpens = scenario.Season.Calendar.Milestones.FirstOrDefault(milestone => milestone.Type == LegacyEngine.Seasons.SeasonMilestoneType.TrainingCampOpens)?.Date.Value;
        var seasonBegins = scenario.Season.Calendar.Milestones.FirstOrDefault(milestone => milestone.Type == LegacyEngine.Seasons.SeasonMilestoneType.SeasonBegins)?.Date.Value;
        if (campOpens is not null && seasonBegins is not null && scenario.CurrentDate >= campOpens.Value && scenario.CurrentDate <= seasonBegins.Value)
        {
            return AiDecisionWindow.TrainingCamp;
        }

        if (scenario.Playoffs.Bracket is not null && scenario.Playoffs.Bracket.Status != PlayoffStatus.Completed)
        {
            return AiDecisionWindow.Playoffs;
        }

        if (scenario.CurrentDate.Month == 2 && scenario.CurrentDate.Day >= 15)
        {
            return AiDecisionWindow.TradeDeadline;
        }

        if (scenario.Season.CurrentPhase == LegacyEngine.Seasons.SeasonPhase.Offseason)
        {
            return scenario.CurrentDate > scenario.DraftDate ? AiDecisionWindow.EarlyOffseason : AiDecisionWindow.Preseason;
        }

        if (scenario.CurrentDate.Month <= 11)
        {
            return AiDecisionWindow.EarlySeason;
        }

        if (scenario.CurrentDate.Month >= 4)
        {
            return AiDecisionWindow.EndOfRegularSeason;
        }

        return AiDecisionWindow.MonthlyReview;
    }

    private static string Readable(object value)
    {
        var text = value.ToString() ?? string.Empty;
        return string.Concat(text.SelectMany((character, index) =>
            index > 0 && char.IsUpper(character) ? new[] { ' ', character } : new[] { character }));
    }

    private static string CycleId(DateOnly date) => $"ai-cycle:{date:yyyyMMdd}";

    private static AiFrontOfficeDecisionResult Result(
        NewGmScenarioSnapshot scenario,
        AiFrontOfficeDecisionCycle cycle,
        IReadOnlyList<LeagueTransaction> news,
        string message)
    {
        var result = new AiFrontOfficeDecisionResult(scenario, cycle, news, message);
        result.Validate();
        return result;
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 17;
            foreach (var character in value)
            {
                hash = (hash * 31) + character;
            }

            return Math.Abs(hash);
        }
    }
}
