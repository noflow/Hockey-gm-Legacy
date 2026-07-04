namespace LegacyEngine.People;

public sealed record PersonalityProfile(
    int Ambition,
    int Loyalty,
    int Temperament,
    int Adaptability,
    int Professionalism)
{
    public void Validate()
    {
        ValidateScore(Ambition, nameof(Ambition));
        ValidateScore(Loyalty, nameof(Loyalty));
        ValidateScore(Temperament, nameof(Temperament));
        ValidateScore(Adaptability, nameof(Adaptability));
        ValidateScore(Professionalism, nameof(Professionalism));
    }

    private static void ValidateScore(int value, string name)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(name, "Personality scores must be between 0 and 100.");
        }
    }
}
