namespace LegacyEngine.Scouting;

public sealed record PlayerScoutingSnapshot(
    string PlayerId,
    string Name,
    int Age,
    string Position,
    string Team,
    int CurrentAbility,
    int Potential,
    int WorkEthic,
    int Coachability,
    int InjuryRisk,
    int Character)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PlayerId))
        {
            throw new ArgumentException("Player id is required.", nameof(PlayerId));
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Player name is required.", nameof(Name));
        }

        if (Age <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Age), "Player age must be positive.");
        }

        if (string.IsNullOrWhiteSpace(Position))
        {
            throw new ArgumentException("Player position is required.", nameof(Position));
        }

        if (string.IsNullOrWhiteSpace(Team))
        {
            throw new ArgumentException("Player team is required.", nameof(Team));
        }

        ValidateRating(CurrentAbility, nameof(CurrentAbility));
        ValidateRating(Potential, nameof(Potential));
        ValidateRating(WorkEthic, nameof(WorkEthic));
        ValidateRating(Coachability, nameof(Coachability));
        ValidateRating(InjuryRisk, nameof(InjuryRisk));
        ValidateRating(Character, nameof(Character));
    }

    private static void ValidateRating(int value, string name)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(name, "Internal scouting inputs must be between 0 and 100.");
        }
    }
}
