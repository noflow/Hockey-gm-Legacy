namespace LegacyEngine.Names;

public sealed record GeneratedName(
    string FirstName,
    string LastName,
    NameOrigin Origin,
    string Nationality,
    string Birthplace)
{
    public string DisplayName => $"{FirstName} {LastName}";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(FirstName))
        {
            throw new ArgumentException("Generated first name is required.", nameof(FirstName));
        }

        if (string.IsNullOrWhiteSpace(LastName))
        {
            throw new ArgumentException("Generated last name is required.", nameof(LastName));
        }

        if (string.IsNullOrWhiteSpace(Nationality))
        {
            throw new ArgumentException("Generated nationality is required.", nameof(Nationality));
        }

        if (string.IsNullOrWhiteSpace(Birthplace))
        {
            throw new ArgumentException("Generated birthplace is required.", nameof(Birthplace));
        }

        if (DisplayName.Any(char.IsDigit))
        {
            throw new ArgumentException("Generated display names must not contain numeric suffixes.", nameof(DisplayName));
        }
    }
}
