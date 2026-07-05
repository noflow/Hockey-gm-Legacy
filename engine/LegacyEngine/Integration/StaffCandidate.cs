using LegacyEngine.People;
using LegacyEngine.Staff;

namespace LegacyEngine.Integration;

public sealed record StaffCandidate(
    string CandidateId,
    Person Person,
    StaffMember StaffMember,
    int RoleFit,
    int DepartmentFit,
    int Reputation,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Weaknesses,
    string PersonalityFitSummary,
    string ChemistryRisk,
    string HiringRecommendation)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(CandidateId))
        {
            throw new ArgumentException("Staff candidate id is required.", nameof(CandidateId));
        }

        Person.Validate();
        StaffMember.Validate();
        ValidateScore(RoleFit, nameof(RoleFit));
        ValidateScore(DepartmentFit, nameof(DepartmentFit));
        ValidateScore(Reputation, nameof(Reputation));

        if (Strengths.Count == 0)
        {
            throw new ArgumentException("Staff candidate strengths are required.", nameof(Strengths));
        }

        if (Weaknesses.Count == 0)
        {
            throw new ArgumentException("Staff candidate weaknesses are required.", nameof(Weaknesses));
        }

        if (string.IsNullOrWhiteSpace(PersonalityFitSummary)
            || string.IsNullOrWhiteSpace(ChemistryRisk)
            || string.IsNullOrWhiteSpace(HiringRecommendation))
        {
            throw new ArgumentException("Staff candidate summaries are required.");
        }
    }

    private static void ValidateScore(int value, string name)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(name, "Staff candidate scores must be between 0 and 100.");
        }
    }
}
