using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public enum PlanningHorizon
{
    CurrentSeason,
    TwoYears,
    ThreeYears,
    FiveYears
}

public enum CompetitiveWindow
{
    Rebuild,
    Developing,
    Competing,
    Contending,
    AllIn,
    Declining
}

public enum DevelopmentPathStep
{
    Junior,
    Ahl,
    Nhl,
    Affiliate,
    ReturnToJunior,
    DepthRole,
    TopSix,
    TopPair,
    StarterGoalie
}

public sealed record DepthChartSlot(
    string Slot,
    string PersonId,
    string PlayerName,
    RosterPosition Position,
    string Role,
    int? Age,
    int Year,
    string Source,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Slot)
            || string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(Role)
            || string.IsNullOrWhiteSpace(Source)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Depth chart slot requires readable player context.");
        }
    }
}

public sealed record DepthPlan(
    IReadOnlyList<DepthChartSlot> CurrentDepth,
    IReadOnlyList<DepthChartSlot> FutureDepth,
    IReadOnlyList<string> Weaknesses,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Depth plan requires a summary.");
        }

        foreach (var slot in CurrentDepth.Concat(FutureDepth))
        {
            slot.Validate();
        }
    }
}

public sealed record ProspectDevelopmentPath(
    string PersonId,
    string PlayerName,
    RosterPosition Position,
    int Age,
    int ExpectedArrivalYear,
    IReadOnlyList<DevelopmentPathStep> Path,
    string ProjectedRole,
    string Recommendation,
    bool IsBlocked,
    string BlockingSummary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(ProjectedRole)
            || string.IsNullOrWhiteSpace(Recommendation)
            || string.IsNullOrWhiteSpace(BlockingSummary))
        {
            throw new ArgumentException("Prospect path requires readable context.");
        }

        if (Path.Count == 0 || ExpectedArrivalYear < 1900)
        {
            throw new ArgumentException("Prospect path requires route steps and a valid arrival year.");
        }
    }
}

public sealed record ProspectPlan(
    IReadOnlyList<ProspectDevelopmentPath> Prospects,
    IReadOnlyList<string> PipelineStrengths,
    IReadOnlyList<string> PipelineRisks,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Prospect plan requires a summary.");
        }

        foreach (var prospect in Prospects)
        {
            prospect.Validate();
        }
    }
}

public sealed record ContractPlanningItem(
    string PersonId,
    string PlayerName,
    string ContractStatus,
    int ExpiryYear,
    decimal Salary,
    string Recommendation,
    string Risk)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(ContractStatus)
            || string.IsNullOrWhiteSpace(Recommendation)
            || string.IsNullOrWhiteSpace(Risk))
        {
            throw new ArgumentException("Contract planning item requires readable context.");
        }
    }
}

public sealed record ContractPlan(
    IReadOnlyList<ContractPlanningItem> ExpiringContracts,
    IReadOnlyList<ContractPlanningItem> ExtensionTargets,
    decimal CurrentCommittedSalary,
    decimal FutureCommittedSalary,
    string CapBudgetSummary,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(CapBudgetSummary) || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Contract plan requires summary text.");
        }

        foreach (var item in ExpiringContracts.Concat(ExtensionTargets))
        {
            item.Validate();
        }
    }
}

public sealed record RosterPlan(
    IReadOnlyList<string> CurrentNeeds,
    IReadOnlyList<string> FutureNeeds,
    IReadOnlyList<string> SuccessionPlans,
    IReadOnlyList<string> PromotionCandidates,
    IReadOnlyList<string> BlockedProspects,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Roster plan requires a summary.");
        }
    }
}

public sealed record OrganizationPlan(
    string OrganizationId,
    string OrganizationName,
    PlanningHorizon Horizon,
    CompetitiveWindow Window,
    RosterPlan RosterPlan,
    ProspectPlan ProspectPlan,
    DepthPlan DepthPlan,
    ContractPlan ContractPlan,
    IReadOnlyList<string> FreeAgencyTargets,
    IReadOnlyList<string> TradeTargets,
    IReadOnlyList<string> Reports,
    DateOnly LastUpdated,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OrganizationId)
            || string.IsNullOrWhiteSpace(OrganizationName)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Organization plan requires organization identity and summary.");
        }

        RosterPlan.Validate();
        ProspectPlan.Validate();
        DepthPlan.Validate();
        ContractPlan.Validate();
    }
}
