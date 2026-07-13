using LegacyEngine.Seasons;

namespace LegacyEngine.Integration;

public enum OpeningNightStatus
{
    NotReady,
    ReadyToBegin,
    Begun,
    Blocked
}

public sealed record OpeningNightState(
    bool PreviewGenerated = false,
    bool BriefingSent = false,
    DateOnly? BeganOn = null)
{
    public static OpeningNightState Empty { get; } = new();

    public void Validate()
    {
        if (!BriefingSent && BeganOn is not null)
        {
            throw new ArgumentException("Opening night cannot have a begin date before its briefing is sent.", nameof(BeganOn));
        }
    }
}

public sealed record OpeningNightPreview(
    OpeningNightStatus Status,
    DateOnly OpeningNightOn,
    string? OpponentName,
    int ActiveRosterCount,
    int OpeningRosterTarget,
    string RosterStatus,
    bool CapCompliant,
    string CapStatus,
    string GoaliePlan,
    string LineupSummary,
    IReadOnlyList<string> InjuryConcerns,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Concerns,
    string OwnerExpectation,
    string Summary,
    bool CanBegin)
{
    public void Validate()
    {
        if (OpeningNightOn == default)
        {
            throw new ArgumentException("Opening night date is required.", nameof(OpeningNightOn));
        }

        if (ActiveRosterCount < 0 || OpeningRosterTarget < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ActiveRosterCount), "Opening roster counts cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(RosterStatus)
            || string.IsNullOrWhiteSpace(CapStatus)
            || string.IsNullOrWhiteSpace(GoaliePlan)
            || string.IsNullOrWhiteSpace(LineupSummary)
            || string.IsNullOrWhiteSpace(OwnerExpectation)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Opening night preview summaries are required.");
        }
    }
}

public sealed record OpeningNightResult(
    bool Success,
    NewGmScenarioSnapshot ScenarioSnapshot,
    OpeningNightPreview Preview,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    string Message)
{
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(ScenarioSnapshot);
        Preview.Validate();
        if (InboxItems is null)
        {
            throw new ArgumentNullException(nameof(InboxItems));
        }

        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Opening night result message is required.", nameof(Message));
        }
    }
}
