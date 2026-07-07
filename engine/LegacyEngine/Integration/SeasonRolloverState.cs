namespace LegacyEngine.Integration;

public sealed record SeasonRolloverState(
    bool CurrentSeasonCompleted = false,
    DateOnly? CompletedOn = null,
    SeasonYearTransition? LastTransition = null,
    IReadOnlyList<SeasonArchive>? Archives = null,
    IReadOnlyList<string>? OffseasonChecklist = null,
    IReadOnlyList<string>? ExpiringContractPersonIds = null,
    string DraftClassSummary = "")
{
    public IReadOnlyList<SeasonArchive> SeasonArchives => Archives ?? Array.Empty<SeasonArchive>();

    public IReadOnlyList<string> Checklist => OffseasonChecklist ?? Array.Empty<string>();

    public IReadOnlyList<string> ExpiringContracts => ExpiringContractPersonIds ?? Array.Empty<string>();

    public void Validate()
    {
        foreach (var archive in SeasonArchives)
        {
            archive.Validate();
        }

        foreach (var item in Checklist)
        {
            if (string.IsNullOrWhiteSpace(item))
            {
                throw new ArgumentException("Offseason checklist items cannot be blank.", nameof(OffseasonChecklist));
            }
        }

        foreach (var personId in ExpiringContracts)
        {
            if (string.IsNullOrWhiteSpace(personId))
            {
                throw new ArgumentException("Expiring contract person ids cannot be blank.", nameof(ExpiringContractPersonIds));
            }
        }

        LastTransition?.Validate();
    }
}
