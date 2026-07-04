namespace LegacyEngine.Scouting;

public sealed record Scout(
    string ScoutId,
    string Name,
    IReadOnlyCollection<ScoutSpecialty> Specialties,
    int Accuracy,
    int Diligence,
    int ReportBias)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ScoutId))
        {
            throw new ArgumentException("Scout id is required.", nameof(ScoutId));
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Scout name is required.", nameof(Name));
        }

        if (Specialties.Count == 0)
        {
            throw new ArgumentException("A scout must have at least one specialty.", nameof(Specialties));
        }

        ValidateScore(Accuracy, nameof(Accuracy));
        ValidateScore(Diligence, nameof(Diligence));

        if (ReportBias is < -20 or > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(ReportBias), "Scout report bias must be between -20 and 20.");
        }
    }

    public bool HasSpecialty(ScoutSpecialty specialty) => Specialties.Contains(specialty);

    private static void ValidateScore(int value, string name)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(name, "Scout scores must be between 0 and 100.");
        }
    }
}
