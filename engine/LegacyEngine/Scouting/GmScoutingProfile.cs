namespace LegacyEngine.Scouting;

public sealed record GmScoutingProfile(
    string GmId,
    int PersonalScouting,
    IReadOnlyCollection<ScoutSpecialty> Specialties)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(GmId))
        {
            throw new ArgumentException("GM id is required.", nameof(GmId));
        }

        if (PersonalScouting is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(PersonalScouting), "GM personal scouting must be between 0 and 100.");
        }
    }

    public int CalculatePersonalScoutingBonus(ScoutingAssignment assignment)
    {
        Validate();
        assignment.Validate();

        var skillBonus = PersonalScouting / 10;
        var focusBonus = assignment.FocusAreas.Any(Specialties.Contains) ? 5 : 0;
        var playerBonus = assignment.AssignmentType == ScoutingAssignmentType.Player ? 3 : 0;

        return skillBonus + focusBonus + playerBonus;
    }
}
