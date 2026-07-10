namespace LegacyEngine.Integration;

public enum AiDecisionWindow
{
    Preseason,
    EarlySeason,
    MonthlyReview,
    TradeDeadline,
    EndOfRegularSeason,
    Playoffs,
    EarlyOffseason,
    DraftPreparation,
    Draft,
    ContractRightsPeriod,
    FreeAgency,
    TrainingCamp
}

public enum AiDecisionPriority
{
    Routine,
    Useful,
    Important,
    Urgent,
    Critical
}

public enum AiFrontOfficeDecisionType
{
    Roster,
    Prospect,
    Contract,
    Trade,
    FreeAgency,
    Draft,
    Staff,
    Waiver,
    Arbitration,
    Buyout,
    OfferSheet,
    Emergency
}

public sealed record AiDecisionSchedule(
    DateOnly Date,
    AiDecisionWindow Window,
    bool ShouldRun,
    string Reason)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Reason))
        {
            throw new ArgumentException("AI decision schedule requires a reason.", nameof(Reason));
        }
    }
}

public sealed record AiDecisionExplanation(
    string Summary,
    string PlanReference,
    string WhyNow,
    string Risk)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Summary)
            || string.IsNullOrWhiteSpace(PlanReference)
            || string.IsNullOrWhiteSpace(WhyNow)
            || string.IsNullOrWhiteSpace(Risk))
        {
            throw new ArgumentException("AI decision explanation requires readable context.");
        }
    }
}

public sealed record AiDecisionCandidate(
    string CandidateId,
    string OrganizationId,
    string TeamName,
    AiDecisionWindow Window,
    AiFrontOfficeDecisionType DecisionType,
    AiDecisionPriority Priority,
    string Title,
    string Reason,
    string OrganizationalGoal,
    DateOnly? Deadline,
    string RiskOfWaiting,
    string ExpectedCost,
    string ExpectedBenefit,
    int Confidence,
    IReadOnlyList<string> AlternativesConsidered,
    string? RelatedPersonId,
    string? RelatedPersonName,
    AiDecisionExplanation Explanation)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(CandidateId)
            || string.IsNullOrWhiteSpace(OrganizationId)
            || string.IsNullOrWhiteSpace(TeamName)
            || string.IsNullOrWhiteSpace(Title)
            || string.IsNullOrWhiteSpace(Reason)
            || string.IsNullOrWhiteSpace(OrganizationalGoal)
            || string.IsNullOrWhiteSpace(RiskOfWaiting)
            || string.IsNullOrWhiteSpace(ExpectedCost)
            || string.IsNullOrWhiteSpace(ExpectedBenefit))
        {
            throw new ArgumentException("AI decision candidate requires readable context.");
        }

        if (Confidence is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(Confidence), "AI decision confidence must be between 0 and 100.");
        }

        if (AlternativesConsidered.Count == 0 || AlternativesConsidered.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("AI decision candidate requires alternatives.", nameof(AlternativesConsidered));
        }

        Explanation.Validate();
    }
}

public sealed record AiDecisionOutcomeRecord(
    string OutcomeId,
    string CandidateId,
    string OrganizationId,
    string TeamName,
    AiFrontOfficeDecisionType DecisionType,
    AiDecisionPriority Priority,
    AiDecisionOutcome Outcome,
    DateOnly Date,
    string ActionTaken,
    string Explanation,
    bool IsMajorDecision,
    bool CreatedLeagueNews)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OutcomeId)
            || string.IsNullOrWhiteSpace(CandidateId)
            || string.IsNullOrWhiteSpace(OrganizationId)
            || string.IsNullOrWhiteSpace(TeamName)
            || string.IsNullOrWhiteSpace(ActionTaken)
            || string.IsNullOrWhiteSpace(Explanation))
        {
            throw new ArgumentException("AI decision outcome requires readable context.");
        }
    }
}

public sealed record AiTransactionPlan(
    string OrganizationId,
    string TeamName,
    AiDecisionWindow Window,
    IReadOnlyList<string> AssetsBeingShopped,
    IReadOnlyList<string> LikelyTargets,
    IReadOnlyList<string> ContractPriorities,
    IReadOnlyList<string> ProspectDecisions,
    IReadOnlyList<string> StaffPriorities,
    IReadOnlyList<string> FreeAgencyTargets,
    DateOnly LastUpdated,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OrganizationId)
            || string.IsNullOrWhiteSpace(TeamName)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("AI transaction plan requires organization identity and summary.");
        }
    }
}

public sealed record AiDecisionCooldown(
    string OrganizationId,
    AiFrontOfficeDecisionType DecisionType,
    DateOnly Until,
    string Reason)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OrganizationId) || string.IsNullOrWhiteSpace(Reason))
        {
            throw new ArgumentException("AI decision cooldown requires organization and reason.");
        }
    }
}

public sealed record AiEmergencyOverride(
    string OverrideId,
    string OrganizationId,
    string TeamName,
    DateOnly StartDate,
    DateOnly EndDate,
    string Reason,
    string AffectedPlan,
    bool ReturnsToPreviousStrategy)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OverrideId)
            || string.IsNullOrWhiteSpace(OrganizationId)
            || string.IsNullOrWhiteSpace(TeamName)
            || string.IsNullOrWhiteSpace(Reason)
            || string.IsNullOrWhiteSpace(AffectedPlan))
        {
            throw new ArgumentException("AI emergency override requires readable context.");
        }
    }
}

public sealed record AiDecisionHistoryEntry(
    string HistoryId,
    string OrganizationId,
    string TeamName,
    DateOnly Date,
    AiDecisionWindow Window,
    AiFrontOfficeDecisionType DecisionType,
    AiDecisionPriority Priority,
    AiDecisionOutcome Outcome,
    string Summary,
    string Explanation)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(HistoryId)
            || string.IsNullOrWhiteSpace(OrganizationId)
            || string.IsNullOrWhiteSpace(TeamName)
            || string.IsNullOrWhiteSpace(Summary)
            || string.IsNullOrWhiteSpace(Explanation))
        {
            throw new ArgumentException("AI decision history requires readable context.");
        }
    }
}

public sealed record AiFrontOfficeDecisionCycle(
    string CycleId,
    DateOnly Date,
    AiDecisionSchedule Schedule,
    IReadOnlyList<AiDecisionCandidate> Candidates,
    IReadOnlyList<AiDecisionOutcomeRecord> Outcomes,
    IReadOnlyList<AiTransactionPlan> TransactionPlans,
    IReadOnlyList<AiDecisionCooldown> Cooldowns,
    IReadOnlyList<AiEmergencyOverride> EmergencyOverrides,
    IReadOnlyList<string> SkippedDecisions,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(CycleId) || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("AI front office decision cycle requires id and summary.");
        }

        Schedule.Validate();
        foreach (var candidate in Candidates)
        {
            candidate.Validate();
        }

        foreach (var outcome in Outcomes)
        {
            outcome.Validate();
        }

        foreach (var plan in TransactionPlans)
        {
            plan.Validate();
        }

        foreach (var cooldown in Cooldowns)
        {
            cooldown.Validate();
        }

        foreach (var emergency in EmergencyOverrides)
        {
            emergency.Validate();
        }
    }
}

public sealed record AiFrontOfficeDecisionResult(
    NewGmScenarioSnapshot ScenarioSnapshot,
    AiFrontOfficeDecisionCycle Cycle,
    IReadOnlyList<LeagueTransaction> LeagueNews,
    string Message)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        Cycle.Validate();
        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("AI front office result requires a message.", nameof(Message));
        }

        foreach (var item in LeagueNews)
        {
            item.Validate();
        }
    }
}
