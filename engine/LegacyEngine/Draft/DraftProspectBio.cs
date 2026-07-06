using LegacyEngine.Rosters;

namespace LegacyEngine.Draft;

public sealed record DraftProspectBio(
    RosterPosition Position,
    string ShootsCatches,
    int HeightInches,
    int WeightPounds,
    int BirthYear,
    string Hometown,
    string ProvinceState,
    string Country,
    string CurrentTeam,
    string League,
    string CharacterSummary,
    string PotentialLineupProjection)
{
    public string HeightDisplay => $"{HeightInches / 12}'{HeightInches % 12}\"";

    public string WeightDisplay => $"{WeightPounds} lbs";

    public void Validate()
    {
        if (Position == RosterPosition.Unknown)
        {
            throw new ArgumentException("Draft prospect bio must include a known position.", nameof(Position));
        }

        if (string.IsNullOrWhiteSpace(ShootsCatches)
            || string.IsNullOrWhiteSpace(Hometown)
            || string.IsNullOrWhiteSpace(ProvinceState)
            || string.IsNullOrWhiteSpace(Country)
            || string.IsNullOrWhiteSpace(CurrentTeam)
            || string.IsNullOrWhiteSpace(League)
            || string.IsNullOrWhiteSpace(CharacterSummary)
            || string.IsNullOrWhiteSpace(PotentialLineupProjection))
        {
            throw new ArgumentException("Draft prospect bio text fields are required.");
        }

        if (HeightInches is < 60 or > 84)
        {
            throw new ArgumentOutOfRangeException(nameof(HeightInches), "Prospect height must be a realistic hockey measurement.");
        }

        if (WeightPounds is < 130 or > 280)
        {
            throw new ArgumentOutOfRangeException(nameof(WeightPounds), "Prospect weight must be a realistic hockey measurement.");
        }

        if (BirthYear is < 1900 or > 2100)
        {
            throw new ArgumentOutOfRangeException(nameof(BirthYear), "Prospect birth year is invalid.");
        }
    }
}
