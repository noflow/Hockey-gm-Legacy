using LegacyEngine.Contracts;
using LegacyEngine.Events;
using LegacyEngine.Recruiting;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;
using LegacyEngine.Scouting;
using LegacyEngine.Staff;

namespace LegacyEngine.Integration;

public sealed class ContractManagementService
{
    public ContractAsk BuildAsk(NewGmScenarioSnapshot scenario, ContractAskType askType, string personId)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var budget = new BudgetOverviewService().Build(scenario, RulebookPresets.CreateJuniorMajor());
        var ask = askType switch
        {
            ContractAskType.FreeAgent => BuildFreeAgentAsk(scenario, personId, budget),
            ContractAskType.Prospect => BuildProspectAsk(scenario, personId, budget),
            ContractAskType.Recruit => BuildRecruitAsk(scenario, personId, budget),
            ContractAskType.StaffMember => BuildStaffAsk(scenario, personId, budget),
            _ => BuildRosterAsk(scenario, personId, budget)
        };
        ask.Validate();
        return ask;
    }

    public ContractOfferEvaluation BuildOffer(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        ContractOfferBuildRequest request)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(request);
        scenario.Validate();
        request.Validate();

        var ask = BuildAsk(scenario, request.AskType, request.PersonId);
        var budget = new BudgetOverviewService().Build(scenario, registry.Rulebook ?? RulebookPresets.CreateJuniorMajor());
        var term = ContractExpiryCalendar.TermForYears(scenario.CurrentDate, scenario.Season.Settings, request.TermYears);
        var annual = request.AnnualSalary;
        var total = annual * request.TermYears;
        var budgetAfter = budget.RemainingBudget - annual;
        var cap = new SalaryCapService().ProjectAfterSigning(scenario, registry.Rulebook ?? scenario.LeagueProfile.Rulebook, annual, request.TermYears);
        var baseScore = ScoreOffer(scenario, ask, request, budgetAfter, cap);
        var agentReview = new AgentEngine().ReviewOffer(scenario, ask, request, baseScore);
        var score = Math.Clamp(baseScore + agentReview.ScoreModifier, 0, 100);
        var likelihood = LikelihoodFor(score);
        var decision = DecisionFor(score, ask, request);
        var explanation = Explain(ask, request, decision, score, budgetAfter, cap, agentReview);
        var currentCost = CurrentAnnualCost(scenario, request.PersonId);
        var comparison = new ContractComparison(
            CurrentAnnualCost: currentCost,
            OfferAnnualCost: annual,
            AskAnnualCost: ask.RequestedSalary,
            BudgetRemainingBefore: budget.RemainingBudget,
            BudgetRemainingAfter: budgetAfter,
            CurrentContractSummary: currentCost > 0 ? $"Current annual cost {currentCost:C0}." : "No active contract on file.",
            RoleRequested: ask.DesiredRole,
            RoleOffered: request.AskType == ContractAskType.StaffMember ? request.StaffRoleOrFocusPromise : request.RolePromise,
            TermRequestedYears: ask.RequestedTermYears,
            TermOfferedYears: request.TermYears,
            LikelyReaction: explanation.Summary);
        var evaluation = new ContractOfferEvaluation(
            EvaluationId: $"contract-eval:{Guid.NewGuid():N}",
            Ask: ask,
            OfferRequest: request,
            Term: term,
            TotalCost: total,
            AnnualCost: annual,
            BudgetRemainingBefore: budget.RemainingBudget,
            BudgetRemainingAfter: budgetAfter,
            RiskWarning: RiskWarning(ask, request, budgetAfter),
            DecisionScore: score,
            Likelihood: likelihood,
            Decision: decision,
            Explanation: explanation,
            Comparison: comparison)
        {
            CapHit = annual,
            CapRemainingBefore = cap.Before.AvailableCapSpace,
            CapRemainingAfter = cap.After.AvailableCapSpace,
            CapWarning = CapWarning(cap),
            AgentReview = agentReview
        };
        evaluation.Validate();
        return evaluation;
    }

    public ContractManagementResult SubmitOffer(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        ContractOfferBuildRequest request)
    {
        var cap = new SalaryCapService().ProjectAfterSigning(scenario, registry.Rulebook ?? scenario.LeagueProfile.Rulebook, request.AnnualSalary, request.TermYears);
        if (!cap.IsCompliant)
        {
            var blocked = BuildOffer(registry, scenario, request);
            return new ContractManagementResult(
                Success: false,
                ScenarioSnapshot: scenario,
                Evaluation: blocked,
                InboxItems: Array.Empty<AlphaInboxItem>(),
                LeagueTransactions: Array.Empty<LeagueTransaction>(),
                Message: string.Join(" ", cap.Reasons));
        }

        var evaluation = BuildOffer(registry, scenario, request);
        QueueContractEvent(registry, scenario, LegacyEventType.ContractOfferSubmitted, $"Offer sent to {evaluation.AgentName}", $"{evaluation.AgentName} is reviewing a {request.TermYears}-year offer worth {request.AnnualSalary:C0} per year for {evaluation.Ask.PersonName}.", request.PersonId, evaluation);

        var inbox = new List<AlphaInboxItem>
        {
            Inbox(scenario, LegacyEventType.ContractOfferSubmitted, $"Agent reviewing offer: {evaluation.AgentName}", evaluation.AgentOpinion, request.PersonId)
        };
        var next = scenario;

        if (evaluation.Decision == ContractOfferDecision.Accepted)
        {
            QueueContractEvent(registry, scenario, LegacyEventType.ContractOfferAccepted, $"Agent acceptance: {evaluation.AgentName}", $"{evaluation.AgentName} says {evaluation.Ask.PersonName} is ready to proceed, pending GM approval.", request.PersonId, evaluation);
            var pending = CreateAcceptedPendingAction(registry, scenario, request, evaluation);
            next = pending.ScenarioSnapshot;
            inbox.AddRange(pending.InboxItems);
            inbox.Add(Inbox(scenario, LegacyEventType.ContractOfferAccepted, $"Agent acceptance pending approval: {evaluation.Ask.PersonName}", evaluation.AgentOpinion, request.PersonId, LegacyEventSeverity.Warning));
        }
        else if (evaluation.Decision == ContractOfferDecision.Rejected)
        {
            QueueContractEvent(registry, scenario, LegacyEventType.ContractRejected, $"Agent rejection: {evaluation.AgentName}", evaluation.AgentOpinion, request.PersonId, evaluation, LegacyEventSeverity.Warning);
            inbox.Add(Inbox(scenario, LegacyEventType.ContractRejected, $"Agent rejected offer: {evaluation.Ask.PersonName}", evaluation.AgentOpinion, request.PersonId, LegacyEventSeverity.Warning));
        }
        else
        {
            QueueContractEvent(registry, scenario, LegacyEventType.ContractOfferNeedsRevision, $"Agent wants revision: {evaluation.AgentName}", evaluation.AgentOpinion, request.PersonId, evaluation, LegacyEventSeverity.Warning);
            inbox.Add(Inbox(scenario, LegacyEventType.ContractOfferNeedsRevision, $"Agent counter: {evaluation.Ask.PersonName}", $"{evaluation.AgentOpinion} {evaluation.AgentCounterSuggestion}", request.PersonId, LegacyEventSeverity.Warning));
        }

        var result = new ContractManagementResult(
            Success: true,
            ScenarioSnapshot: next,
            Evaluation: evaluation,
            InboxItems: inbox,
            LeagueTransactions: Array.Empty<LeagueTransaction>(),
            Message: evaluation.Decision == ContractOfferDecision.Accepted
                ? $"{evaluation.AgentName} accepted terms for {evaluation.Ask.PersonName}; GM approval is required before signing."
                : $"{evaluation.AgentName}: {evaluation.AgentOpinion}");
        result.Validate();
        return result;
    }

    public ContractManagementSummary BuildSummary(NewGmScenarioSnapshot scenario, Rulebook? rulebook = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var budget = new BudgetOverviewService().Build(scenario, rulebook ?? RulebookPresets.CreateJuniorMajor());
        var expiring = ActiveContracts(scenario)
            .Where(contract => contract.Term.EndDate <= scenario.CurrentDate.AddDays(60))
            .ToArray();
        var expiringPlayers = expiring
            .Where(contract => contract.ContractType == ContractType.JuniorPlayerAgreement)
            .Select(contract => BuildRosterAsk(scenario, contract.PersonId, budget))
            .ToArray();
        var expiringStaff = expiring
            .Where(contract => contract.ContractType != ContractType.JuniorPlayerAgreement)
            .Select(contract => BuildStaffAsk(scenario, contract.PersonId, budget))
            .ToArray();
        var unsignedProspects = scenario.ProspectRights
            .Where(record => record.Status is ProspectStatus.DraftRightsHeld or ProspectStatus.ContractOffered or ProspectStatus.InvitedToCamp or ProspectStatus.ReturnedToJunior or ProspectStatus.ReturnedToYouthTeam)
            .Select(record => BuildProspectAsk(scenario, record.ProspectPersonId, budget))
            .ToArray();
        var pending = scenario.PendingActions
            .Where(action => action.IsOpen && action.ActionType is PendingGmActionType.ApproveContract or PendingGmActionType.SignFreeAgent or PendingGmActionType.SignDraftPick or PendingGmActionType.SignRecruit)
            .Select(action => BuildPendingAsk(scenario, action, budget))
            .ToArray();
        var accepted = scenario.PendingActions
            .Where(action => action.IsOpen && action.ActionType == PendingGmActionType.ApproveContract)
            .Select(action => BuildPendingAsk(scenario, action, budget))
            .ToArray();

        var summary = new ContractManagementSummary(expiringPlayers, expiringStaff, unsignedProspects, pending, accepted, Array.Empty<ContractAsk>(), budget);
        summary.Validate();
        return summary;
    }

    public LeagueTransaction RecordOtherTeamNotableSigning(
        NewGmScenarioSnapshot scenario,
        string teamName,
        string personId,
        decimal annualSalary)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        if (string.IsNullOrWhiteSpace(teamName))
        {
            throw new ArgumentException("Team name is required.", nameof(teamName));
        }

        var name = ResolveName(scenario, personId);
        var transaction = new LeagueTransaction(
            $"league-contract:{Guid.NewGuid():N}",
            At(scenario.CurrentDate, 16),
            null,
            teamName,
            personId,
            name,
            LeagueTransactionType.ContractSigned,
            LeagueNewsCategory.Signings,
            $"{teamName} signed {name} for {annualSalary:C0} annually.");
        transaction.Validate();
        return transaction;
    }

    private static PendingGmActionResult CreateAcceptedPendingAction(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        ContractOfferBuildRequest request,
        ContractOfferEvaluation evaluation)
    {
        var personName = evaluation.Ask.PersonName;
        var action = new PendingGmAction(
            ActionId: $"pending-gm:{Guid.NewGuid():N}",
            ActionType: PendingGmActionType.ApproveContract,
            Status: PendingGmActionStatus.Pending,
            CreatedOn: scenario.CurrentDate,
            PersonId: request.PersonId,
            PersonName: personName,
            OrganizationId: scenario.Organization.OrganizationId,
            Title: $"Approve contract: {personName}",
            Reason: $"{evaluation.AgentName} accepted {request.TermYears} year(s) at {request.AnnualSalary:C0} for {personName}. {evaluation.AgentOpinion}",
            RecommendedAction: "Approve to create the signed contract, or decline to walk away.",
            Position: GuessPosition(scenario, request.PersonId),
            AcquisitionSource: request.AskType is ContractAskType.FreeAgent ? PlayerAcquisitionSource.FreeAgentSigning : PlayerAcquisitionSource.Unknown,
            ContractType: request.ContractType ?? DefaultContractType(request.AskType),
            OfferedSalary: request.AnnualSalary,
            OfferedTermYears: request.TermYears,
            RolePromise: request.RolePromise,
            DevelopmentPromise: request.DevelopmentPromise,
            ContractNotes: request.Notes);
        action.Validate();

        var updated = scenario with { PendingActions = scenario.PendingActions.Append(action).ToArray() };
        QueuePendingCreatedEvent(registry, scenario, action);
        return new PendingGmActionResult(
            true,
            updated,
            action,
            new[] { Inbox(scenario, LegacyEventType.PendingGmActionCreated, $"GM approval needed: {personName}", action.Reason, request.PersonId, LegacyEventSeverity.Warning) },
            $"{personName} contract is awaiting GM approval.",
            Array.Empty<LeagueTransaction>());
    }

    private static ContractAsk BuildFreeAgentAsk(NewGmScenarioSnapshot scenario, string personId, BudgetSnapshot budget)
    {
        var agent = scenario.FreeAgentMarket?.Find(personId)
            ?? throw new ArgumentException("Free agent was not found.", nameof(personId));
        var relationship = RelationshipWithGm(scenario, personId);
        return new ContractAsk(
            agent.PersonId,
            agent.Name,
            ContractAskType.FreeAgent,
            agent.ContractAsk.AnnualAmount,
            agent.ContractAsk.TermYears,
            agent.ProjectedLineupRole,
            StandardPreference(agent.Interest.PlayerOrganizationInterest, role: 75, development: 55),
            agent.Interest.CompetingInterest,
            InterestFor(agent.Interest.PlayerOrganizationInterest),
            Math.Max(0, budget.RemainingBudget - agent.ContractAsk.AnnualAmount),
            agent.FitSummary.FitScore,
            relationship,
            agent.DevelopmentTrend,
            agent.FitSummary.StaffRecommendation);
    }

    private static ContractAsk BuildProspectAsk(NewGmScenarioSnapshot scenario, string personId, BudgetSnapshot budget)
    {
        var prospect = scenario.ProspectRights.FirstOrDefault(record => record.ProspectPersonId == personId)
            ?? throw new ArgumentException("Prospect was not found.", nameof(personId));
        var confidence = prospect.ScoutingConfidence?.ToString() ?? "Low";
        var requested = scenario.LeagueProfile.Experience == LeagueExperience.Nhl
            ? NhlEntryLevelAsk(prospect)
            : 1_200m + Math.Max(0, 4 - prospect.RoundNumber) * 200m;
        var termYears = scenario.LeagueProfile.Experience == LeagueExperience.Nhl
            ? NhlEntryLevelTerm(prospect.Age)
            : 1;
        return new ContractAsk(
            prospect.ProspectPersonId,
            prospect.ProspectName,
            ContractAskType.Prospect,
            requested,
            termYears,
            RoleFor(prospect.Position),
            StandardPreference(60 + Math.Max(0, 4 - prospect.RoundNumber) * 5, role: 65, development: 85),
            $"Draft rights held; round {prospect.RoundNumber}, pick {prospect.PickNumber}.",
            ContractInterest.Medium,
            Math.Max(0, budget.RemainingBudget - requested),
            62,
            50,
            "Wants a believable development path, camp clarity, and a role that matches his age.",
            $"Scout confidence {confidence}; {prospect.ProjectionText}");
    }

    private static decimal NhlEntryLevelAsk(DraftRightsRecord prospect)
    {
        var baseSalary = prospect.RoundNumber switch
        {
            1 when prospect.PickNumber <= 10 => 950_000m,
            1 => 925_000m,
            2 => 875_000m,
            3 => 825_000m,
            4 => 775_000m,
            _ => 725_000m
        };
        return prospect.ScoutingConfidence >= ScoutingConfidenceLevel.High
            ? baseSalary + 25_000m
            : baseSalary;
    }

    private static int NhlEntryLevelTerm(int age) =>
        age switch
        {
            <= 21 => 3,
            22 or 23 => 2,
            _ => 1
        };

    private static ContractAsk BuildRecruitAsk(NewGmScenarioSnapshot scenario, string personId, BudgetSnapshot budget)
    {
        var recruit = scenario.AlphaSnapshot.Recruits.FirstOrDefault(item => item.RecruitPersonId == personId)
            ?? throw new ArgumentException("Recruit was not found.", nameof(personId));
        const decimal requested = 1_100m;
        var priorities = recruit.Priorities
            .OrderByDescending(priority => priority.Value)
            .Take(3)
            .Select(priority => priority.Key.ToString())
            .DefaultIfEmpty("Development")
            .ToArray();
        var interest = recruit.GetInterest(scenario.Organization.OrganizationId);
        return new ContractAsk(
            recruit.RecruitPersonId,
            ResolveName(scenario, recruit.RecruitPersonId),
            ContractAskType.Recruit,
            requested,
            1,
            priorities.Contains("IceTime", StringComparer.Ordinal) ? "Clear ice-time pathway" : "Development role",
            StandardPreference(interest == 0 ? 45 : interest, role: 70, development: 85),
            $"Recruiting priorities: {string.Join(", ", priorities)}.",
            ContractInterest.Medium,
            Math.Max(0, budget.RemainingBudget - requested),
            60,
            50,
            "Needs the club to connect contract path with opportunity, family comfort, and development.",
            "Staff believe the recruit needs clear communication before committing.");
    }

    private static ContractAsk BuildStaffAsk(NewGmScenarioSnapshot scenario, string personId, BudgetSnapshot budget)
    {
        var staff = scenario.StaffMembers.LastOrDefault(member => member.PersonId == personId)
            ?? scenario.StaffCandidates.FirstOrDefault(candidate => candidate.Person.PersonId == personId)?.StaffMember
            ?? throw new ArgumentException("Staff member was not found.", nameof(personId));
        var name = ResolveName(scenario, personId);
        var salary = ActiveContracts(scenario).FirstOrDefault(contract => contract.PersonId == personId)?.Money.SalaryOrStipend
            ?? new StaffBudgetService().CompensationFor(staff, scenario, RulebookPresets.CreateJuniorMajor()).Salary.AnnualAmount;
        var relationship = RelationshipWithGm(scenario, personId);
        return new ContractAsk(
            personId,
            name,
            ContractAskType.StaffMember,
            salary,
            1,
            StaffRoles.Title(staff.CurrentRole),
            StandardPreference(relationship, role: 80, development: 40),
            $"{staff.Department} fit and role clarity matter.",
            InterestFor(relationship),
            Math.Max(0, budget.RemainingBudget - salary),
            Math.Clamp((staff.Profile.Reputation + relationship) / 2, 0, 100),
            relationship,
            "Staff agreement depends on authority, focus, and front-office fit.",
            $"Staff reputation {staff.Profile.Reputation}/100.");
    }

    private static ContractAsk BuildRosterAsk(NewGmScenarioSnapshot scenario, string personId, BudgetSnapshot budget)
    {
        var name = ResolveName(scenario, personId);
        var roster = scenario.AlphaSnapshot.Roster.FindPlayer(personId);
        var contract = ActiveContracts(scenario).FirstOrDefault(item => item.PersonId == personId);
        var salary = contract?.Money.SalaryOrStipend ?? 1_300m;
        var position = roster?.Position ?? GuessPosition(scenario, personId);
        return new ContractAsk(
            personId,
            name,
            ContractAskType.RosterPlayer,
            salary + 150m,
            1,
            RoleFor(position),
            StandardPreference(58, role: 75, development: 60),
            contract is null ? "No active contract found." : $"Contract expires {contract.Term.EndDate:yyyy-MM-dd}.",
            ContractInterest.Medium,
            Math.Max(0, budget.RemainingBudget - salary),
            60,
            50,
            "Wants to understand ice-time role and development plan before renewal.",
            "Coach confidence should shape whether the renewal is worthwhile.");
    }

    private static ContractAsk BuildPendingAsk(NewGmScenarioSnapshot scenario, PendingGmAction action, BudgetSnapshot budget) =>
        new(
            action.PersonId,
            action.PersonName,
            action.ActionType switch
            {
                PendingGmActionType.SignFreeAgent => ContractAskType.FreeAgent,
                PendingGmActionType.SignDraftPick => ContractAskType.Prospect,
                PendingGmActionType.SignRecruit => ContractAskType.Recruit,
                _ => ContractAskType.RosterPlayer
            },
            action.OfferedSalary ?? scenario.FreeAgentMarket?.Find(action.PersonId)?.ContractAsk.AnnualAmount ?? 1_500m,
            action.OfferedTermYears ?? scenario.FreeAgentMarket?.Find(action.PersonId)?.ContractAsk.TermYears ?? 1,
            string.IsNullOrWhiteSpace(action.RolePromise) ? action.RecommendedAction : action.RolePromise,
            StandardPreference(65, role: 70, development: 65),
            action.Title,
            ContractInterest.High,
            Math.Max(0, budget.RemainingBudget - (action.OfferedSalary ?? 1_500m)),
            65,
            50,
            string.IsNullOrWhiteSpace(action.DevelopmentPromise) ? "Pending GM decision." : action.DevelopmentPromise,
            action.Reason);

    private static int ScoreOffer(NewGmScenarioSnapshot scenario, ContractAsk ask, ContractOfferBuildRequest request, decimal budgetAfter, SalaryCapCalculation cap)
    {
        var salaryScore = ask.RequestedSalary <= 0 ? 100 : Math.Clamp((int)Math.Round((request.AnnualSalary / ask.RequestedSalary) * 100m), 0, 125);
        var termScore = request.TermYears >= ask.RequestedTermYears ? 80 : 45;
        var roleText = request.AskType == ContractAskType.StaffMember ? request.StaffRoleOrFocusPromise : request.RolePromise;
        var roleScore = ContainsRoleSignal(roleText, ask.DesiredRole) ? 85 : 45;
        var developmentScore = string.IsNullOrWhiteSpace(request.DevelopmentPromise) ? 45 : 75;
        var relationshipScore = RelationshipWithGm(scenario, ask.PersonId);
        var budgetScore = budgetAfter < 0 ? 25 : budgetAfter < ask.RequestedSalary ? 55 : 75;
        var capScore = !cap.Before.IsEnabled ? 75 : cap.IsCompliant && cap.After.Status == SalaryCapStatus.Comfortable ? 80 : cap.IsCompliant ? 55 : 10;
        var careerFit = request.AskType switch
        {
            ContractAskType.Prospect or ContractAskType.Recruit => request.CampInvitePromise ? 75 : 55,
            ContractAskType.StaffMember => string.IsNullOrWhiteSpace(request.StaffRoleOrFocusPromise) ? 45 : 78,
            _ => 65
        };

        var score =
            salaryScore * ask.Preference.MoneyImportance +
            termScore * ask.Preference.TermImportance +
            roleScore * ask.Preference.RoleImportance +
            developmentScore * ask.Preference.DevelopmentImportance +
            relationshipScore * ask.Preference.RelationshipImportance +
            budgetScore * 40 +
            capScore * 35 +
            ask.PreferredOrganizationFit * 30 +
            careerFit * 35;
        var weight =
            ask.Preference.MoneyImportance +
            ask.Preference.TermImportance +
            ask.Preference.RoleImportance +
            ask.Preference.DevelopmentImportance +
            ask.Preference.RelationshipImportance +
            40 + 35 + 30 + 35;
        return Math.Clamp((int)Math.Round(score / Math.Max(1m, weight)), 0, 100);
    }

    private static ContractDecisionExplanation Explain(ContractAsk ask, ContractOfferBuildRequest request, ContractOfferDecision decision, int score, decimal budgetAfter, SalaryCapCalculation cap, AgentNegotiationReview agentReview)
    {
        var reasons = new List<string>
        {
            request.AnnualSalary >= ask.RequestedSalary
                ? $"Money meets the ask at {request.AnnualSalary:C0}."
                : $"Money trails the ask by {(ask.RequestedSalary - request.AnnualSalary):C0}.",
            request.TermYears >= ask.RequestedTermYears
                ? $"Term matches the requested {ask.RequestedTermYears} year(s)."
                : "Term is shorter than requested.",
            ContainsRoleSignal(request.AskType == ContractAskType.StaffMember ? request.StaffRoleOrFocusPromise : request.RolePromise, ask.DesiredRole)
                ? $"Role promise supports {ask.DesiredRole}."
                : $"Role promise does not clearly match {ask.DesiredRole}.",
            budgetAfter < 0
                ? "Budget impact is a concern and may trigger owner pushback."
                : "Budget impact is manageable.",
            CapWarning(cap)
        };
        reasons.Add($"Agent opinion: {agentReview.Opinion}");
        reasons.Add($"Agent concern: {agentReview.BiggestConcern}");
        reasons.Add($"Requested improvement: {agentReview.RequestedImprovement}");
        reasons.Add($"Agent risk: {agentReview.Risk}");
        if (!string.IsNullOrWhiteSpace(request.DevelopmentPromise))
        {
            reasons.Add($"Development/pathway promise: {request.DevelopmentPromise}");
        }

        var summary = decision switch
        {
            ContractOfferDecision.Accepted => $"{agentReview.AgentName} says {ask.PersonName} is willing to accept, but the GM must approve before anything is signed.",
            ContractOfferDecision.Rejected => $"{agentReview.AgentName} says {ask.PersonName} is likely to reject because the offer misses too many priorities.",
            ContractOfferDecision.WantsMore => $"{agentReview.AgentName} wants a better offer before {ask.PersonName} commits.",
            _ => $"{agentReview.AgentName} says {ask.PersonName} remains undecided; score {score}/100."
        };
        return new ContractDecisionExplanation(decision, summary, reasons);
    }

    private static string CapWarning(SalaryCapCalculation cap)
    {
        if (!cap.Before.IsEnabled)
        {
            return "Salary cap is not enabled for this rulebook.";
        }

        if (!cap.IsCompliant)
        {
            return string.Join(" ", cap.Reasons);
        }

        return cap.After.Status == SalaryCapStatus.NearLimit
            ? "Salary cap room would be tight after this offer."
            : "Salary cap impact is manageable.";
    }

    private static ContractOfferDecision DecisionFor(int score, ContractAsk ask, ContractOfferBuildRequest request)
    {
        if (score >= 70)
        {
            return ContractOfferDecision.Accepted;
        }

        if (score >= 52)
        {
            return request.AnnualSalary < ask.RequestedSalary || request.TermYears < ask.RequestedTermYears
                ? ContractOfferDecision.WantsMore
                : ContractOfferDecision.Undecided;
        }

        return ContractOfferDecision.Rejected;
    }

    private static ContractLikelihood LikelihoodFor(int score) =>
        score switch
        {
            >= 85 => ContractLikelihood.VeryLikely,
            >= 70 => ContractLikelihood.Likely,
            >= 50 => ContractLikelihood.Possible,
            >= 35 => ContractLikelihood.Unlikely,
            _ => ContractLikelihood.VeryUnlikely
        };

    private static string RiskWarning(ContractAsk ask, ContractOfferBuildRequest request, decimal budgetAfter)
    {
        if (budgetAfter < 0)
        {
            return $"Budget risk: this offer would put hockey operations over budget by {Math.Abs(budgetAfter):C0}.";
        }

        if (request.AnnualSalary > ask.RequestedSalary * 1.25m)
        {
            return "Value risk: offer is materially above the ask.";
        }

        if (ask.RelationshipTrustImpact < 40)
        {
            return "Relationship risk: low trust could affect acceptance and communication.";
        }

        return "No major contract risk flagged.";
    }

    private static ContractPreference StandardPreference(int interest, int role, int development) =>
        new(
            DesiredRole: "Clear role",
            MoneyImportance: Math.Clamp(45 + (100 - interest) / 4, 35, 80),
            TermImportance: 45,
            RoleImportance: role,
            DevelopmentImportance: development,
            RelationshipImportance: 55,
            Summary: "Decision weighs money, role clarity, development path, fit, and trust.");

    private static ContractType DefaultContractType(ContractAskType askType) =>
        askType switch
        {
            ContractAskType.StaffMember => ContractType.StaffContract,
            _ => ContractType.JuniorPlayerAgreement
        };

    private static IReadOnlyList<Contract> ActiveContracts(NewGmScenarioSnapshot scenario) =>
        scenario.Contracts
            .Concat(scenario.AlphaSnapshot.Contracts)
            .GroupBy(contract => contract.ContractId, StringComparer.Ordinal)
            .Select(group => group.Last())
            .Where(contract => contract.Status == ContractStatus.Signed)
            .ToArray();

    private static decimal CurrentAnnualCost(NewGmScenarioSnapshot scenario, string personId) =>
        ActiveContracts(scenario)
            .Where(contract => contract.PersonId == personId)
            .Select(contract => contract.Money.SalaryOrStipend)
            .DefaultIfEmpty(0)
            .Last();

    private static ContractInterest InterestFor(int value) =>
        value switch
        {
            >= 85 => ContractInterest.VeryHigh,
            >= 70 => ContractInterest.High,
            >= 45 => ContractInterest.Medium,
            >= 25 => ContractInterest.Low,
            _ => ContractInterest.VeryLow
        };

    private static int RelationshipWithGm(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.Relationships
            .Where(relationship => relationship.FromPersonId == scenario.AlphaSnapshot.GeneralManager.PersonId && relationship.ToPersonId == personId)
            .Select(relationship => (relationship.Trust + relationship.Respect + relationship.Confidence + relationship.Loyalty) / 4)
            .DefaultIfEmpty(50)
            .First();

    private static bool ContainsRoleSignal(string offered, string requested)
    {
        if (string.IsNullOrWhiteSpace(offered) || string.IsNullOrWhiteSpace(requested))
        {
            return false;
        }

        var separators = new[] { ' ', '-', '/', ',', '.' };
        var offeredWords = offered.Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return requested
            .Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(word => offeredWords.Contains(word, StringComparer.OrdinalIgnoreCase));
    }

    private static string RoleFor(RosterPosition position) =>
        position switch
        {
            RosterPosition.Goalie => "Goalie development path",
            RosterPosition.Defense => "Defense role",
            RosterPosition.Center => "Center role",
            RosterPosition.LeftWing or RosterPosition.RightWing => "Wing role",
            _ => "Development role"
        };

    private static RosterPosition GuessPosition(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.Roster.FindPlayer(personId)?.Position
        ?? scenario.ProspectRights.FirstOrDefault(prospect => prospect.ProspectPersonId == personId)?.Position
        ?? scenario.FreeAgentMarket?.Find(personId)?.Position
        ?? scenario.AlphaSnapshot.DraftBoard.Entries.FirstOrDefault(entry => entry.ProspectPersonId == personId)?.Bio?.Position
        ?? RosterPosition.Unknown;

    private static string ResolveName(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? scenario.AlphaSnapshot.Players.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? scenario.FreeAgentMarket?.Find(personId)?.Name
        ?? scenario.ProspectRights.FirstOrDefault(record => record.ProspectPersonId == personId)?.ProspectName
        ?? scenario.StaffCandidates.FirstOrDefault(candidate => candidate.Person.PersonId == personId)?.Person.Identity.DisplayName
        ?? personId;

    private static void QueueContractEvent(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        LegacyEventType type,
        string title,
        string description,
        string personId,
        ContractOfferEvaluation evaluation,
        LegacyEventSeverity severity = LegacyEventSeverity.Notice)
    {
        var legacyEvent = registry.EventEngine.CreateEvent(
            At(scenario.CurrentDate, 13),
            type,
            severity,
            LegacyEventVisibility.Organization,
            title,
            description,
            new LegacyEventContext(PrimaryPersonId: personId, OrganizationId: scenario.Organization.OrganizationId, SeasonId: scenario.Season.SeasonId),
            new Dictionary<string, object?>
            {
                ["person_name"] = evaluation.Ask.PersonName,
                ["team_name"] = scenario.Organization.Name,
                ["reason"] = evaluation.Explanation.Summary,
                ["annual_salary"] = evaluation.AnnualCost,
                ["term_years"] = evaluation.OfferRequest.TermYears,
                ["likelihood"] = evaluation.Likelihood.ToString(),
                ["agent_name"] = evaluation.AgentName,
                ["agent_opinion"] = evaluation.AgentOpinion,
                ["agent_style"] = evaluation.AgentNegotiationStyle.ToString()
            });
        registry.EventEngine.QueueEvent(legacyEvent);
    }

    private static void QueuePendingCreatedEvent(EngineRegistry registry, NewGmScenarioSnapshot scenario, PendingGmAction action)
    {
        var legacyEvent = registry.EventEngine.CreateEvent(
            At(scenario.CurrentDate, 13),
            LegacyEventType.PendingGmActionCreated,
            LegacyEventSeverity.Warning,
            LegacyEventVisibility.Organization,
            "Contract pending GM approval",
            action.Reason,
            new LegacyEventContext(PrimaryPersonId: action.PersonId, OrganizationId: scenario.Organization.OrganizationId, SeasonId: scenario.Season.SeasonId),
            new Dictionary<string, object?>
            {
                ["person_name"] = action.PersonName,
                ["team_name"] = scenario.Organization.Name,
                ["reason"] = action.Reason,
                ["pending_gm_action_id"] = action.ActionId
            });
        registry.EventEngine.QueueEvent(legacyEvent);
    }

    private static AlphaInboxItem Inbox(NewGmScenarioSnapshot scenario, LegacyEventType eventType, string title, string summary, string personId, LegacyEventSeverity severity = LegacyEventSeverity.Notice) =>
        new($"inbox:contracts-v2:{Guid.NewGuid():N}", At(scenario.CurrentDate, 13), eventType, severity, title, summary, personId);

    private static DateTimeOffset At(DateOnly date, int hour) =>
        new(date.Year, date.Month, date.Day, hour, 0, 0, TimeSpan.Zero);
}
