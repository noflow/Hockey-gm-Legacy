namespace LegacyEngine.World;

public sealed record WorldState(
    WorldId WorldId,
    string WorldName,
    WorldClock Clock,
    WorldPhase CurrentPhase,
    WorldSettings Settings,
    IReadOnlyList<WorldSystemRegistration> SystemRegistrations)
{
    public WorldDate CurrentDate => Clock.CurrentDate;

    public int CurrentSeasonYear => Settings.GetSeasonYear(CurrentDate);

    public void Validate()
    {
        WorldId.Validate();
        Settings.Validate();

        if (string.IsNullOrWhiteSpace(WorldName))
        {
            throw new ArgumentException("World name is required.", nameof(WorldName));
        }

        foreach (var registration in SystemRegistrations)
        {
            registration.Validate();
        }
    }
}
