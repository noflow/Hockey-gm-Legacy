namespace LegacyEngine.People;

public sealed record PersonIdentity(
    string FirstName,
    string LastName,
    Gender Gender,
    DateOnly BirthDate,
    string Nationality,
    string Birthplace)
{
    public string DisplayName => $"{FirstName} {LastName}";

    public int CalculateAge(DateOnly onDate)
    {
        if (onDate < BirthDate)
        {
            throw new ArgumentOutOfRangeException(nameof(onDate), "Age cannot be calculated before birth date.");
        }

        var age = onDate.Year - BirthDate.Year;
        if (onDate.Month < BirthDate.Month || (onDate.Month == BirthDate.Month && onDate.Day < BirthDate.Day))
        {
            age--;
        }

        return age;
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(FirstName))
        {
            throw new ArgumentException("First name is required.", nameof(FirstName));
        }

        if (string.IsNullOrWhiteSpace(LastName))
        {
            throw new ArgumentException("Last name is required.", nameof(LastName));
        }

        if (string.IsNullOrWhiteSpace(Nationality))
        {
            throw new ArgumentException("Nationality is required.", nameof(Nationality));
        }

        if (string.IsNullOrWhiteSpace(Birthplace))
        {
            throw new ArgumentException("Birthplace is required.", nameof(Birthplace));
        }
    }
}
