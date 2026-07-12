using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

public enum FreeAgencyTargetPriority
{
    Watch,
    Depth,
    Need,
    Priority,
    MustHave
}

public sealed record FreeAgencyTarget(
    string PersonId,
    string PlayerName,
    RosterPosition Position,
    FreeAgencyTargetPriority Priority,
    bool IsShortlisted,
    int PlayerInterest,
    int CompetingOfferCount,
    string MarketTiming,
    string FitSummary,
    string Recommendation)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(MarketTiming)
            || string.IsNullOrWhiteSpace(FitSummary)
            || string.IsNullOrWhiteSpace(Recommendation)
            || PlayerInterest is < 0 or > 100
            || CompetingOfferCount < 0)
        {
            throw new ArgumentException("Free agency target requires identity, market context, and valid interest values.");
        }
    }
}

public sealed record FreeAgencyTargetBoard(
    DateOnly CurrentDate,
    FreeAgencyWindow Window,
    IReadOnlyList<FreeAgencyTarget> Targets,
    string Summary)
{
    public void Validate()
    {
        Window.Validate();
        foreach (var target in Targets)
        {
            target.Validate();
        }

        if (string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Free agency target board requires a summary.");
        }
    }
}
