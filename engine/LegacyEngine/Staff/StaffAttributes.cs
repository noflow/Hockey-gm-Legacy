namespace LegacyEngine.Staff;

/// <summary>
/// Groups the coaching, scouting, and medical attribute scores for a staff member.
/// Every score is an integer between 0 and 100. Missing attributes read back as 0.
/// </summary>
public sealed record StaffAttributes(
    IReadOnlyDictionary<StaffCoachingAttribute, int> CoachingAttributes,
    IReadOnlyDictionary<StaffScoutingAttribute, int> ScoutingAttributes,
    IReadOnlyDictionary<StaffMedicalAttribute, int> MedicalAttributes)
{
    public static StaffAttributes Empty { get; } = new(
        new Dictionary<StaffCoachingAttribute, int>(),
        new Dictionary<StaffScoutingAttribute, int>(),
        new Dictionary<StaffMedicalAttribute, int>());

    public static StaffAttributes ForCoaching(IReadOnlyDictionary<StaffCoachingAttribute, int> coaching) =>
        Empty with { CoachingAttributes = coaching };

    public static StaffAttributes ForScouting(IReadOnlyDictionary<StaffScoutingAttribute, int> scouting) =>
        Empty with { ScoutingAttributes = scouting };

    public static StaffAttributes ForMedical(IReadOnlyDictionary<StaffMedicalAttribute, int> medical) =>
        Empty with { MedicalAttributes = medical };

    public int CoachingScore(StaffCoachingAttribute attribute) =>
        CoachingAttributes.TryGetValue(attribute, out var value) ? value : 0;

    public int ScoutingScore(StaffScoutingAttribute attribute) =>
        ScoutingAttributes.TryGetValue(attribute, out var value) ? value : 0;

    public int MedicalScore(StaffMedicalAttribute attribute) =>
        MedicalAttributes.TryGetValue(attribute, out var value) ? value : 0;

    public void Validate()
    {
        foreach (var (attribute, value) in CoachingAttributes)
        {
            ValidateScore(value, attribute.ToString());
        }

        foreach (var (attribute, value) in ScoutingAttributes)
        {
            ValidateScore(value, attribute.ToString());
        }

        foreach (var (attribute, value) in MedicalAttributes)
        {
            ValidateScore(value, attribute.ToString());
        }
    }

    private static void ValidateScore(int value, string name)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(name, "Staff attribute scores must be between 0 and 100.");
        }
    }
}
