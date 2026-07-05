using LegacyEngine.People;

namespace LegacyEngine.Integration;

public sealed record GmProfileCreationSettings(
    string FirstName,
    string LastName,
    string PreferredName,
    Gender Gender,
    DateOnly? BirthDate,
    int? Age,
    string Nationality,
    string Birthplace,
    GmBackground Background,
    GmStyle Style,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Weaknesses,
    string PersonId = "person-player-gm-001")
{
    public static GmProfileCreationSettings JordanHayes(string personId = "person-player-gm-001") =>
        new(
            FirstName: "Jordan",
            LastName: "Hayes",
            PreferredName: "Jordan",
            Gender: Gender.NonBinary,
            BirthDate: new DateOnly(1987, 2, 18),
            Age: null,
            Nationality: "Canada",
            Birthplace: "Swift Current, SK",
            Background: GmBackground.Operations,
            Style: GmStyle.Balanced,
            Strengths: new[] { "communication", "development planning" },
            Weaknesses: new[] { "limited draft history" },
            PersonId: personId);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(FirstName))
        {
            throw new ArgumentException("GM first name is required.", nameof(FirstName));
        }

        if (string.IsNullOrWhiteSpace(LastName))
        {
            throw new ArgumentException("GM last name is required.", nameof(LastName));
        }

        if (string.IsNullOrWhiteSpace(PreferredName))
        {
            throw new ArgumentException("GM preferred name is required.", nameof(PreferredName));
        }

        if (BirthDate is null && Age is null)
        {
            throw new ArgumentException("GM birth date or age is required.", nameof(BirthDate));
        }

        if (Age is <= 17 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(Age), "GM age must be between 18 and 100.");
        }

        if (string.IsNullOrWhiteSpace(Nationality))
        {
            throw new ArgumentException("GM nationality is required.", nameof(Nationality));
        }

        if (string.IsNullOrWhiteSpace(Birthplace))
        {
            throw new ArgumentException("GM birthplace is required.", nameof(Birthplace));
        }

        if (Strengths.Count == 0)
        {
            throw new ArgumentException("GM creation should include at least one strength.", nameof(Strengths));
        }

        if (Weaknesses.Count == 0)
        {
            throw new ArgumentException("GM creation should include at least one weakness.", nameof(Weaknesses));
        }

        if (string.IsNullOrWhiteSpace(PersonId))
        {
            throw new ArgumentException("GM person id is required.", nameof(PersonId));
        }
    }
}
