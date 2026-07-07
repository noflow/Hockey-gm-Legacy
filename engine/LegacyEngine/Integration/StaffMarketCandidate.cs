using LegacyEngine.Staff;

namespace LegacyEngine.Integration;

public sealed record StaffMarketCandidate(
    string MarketCandidateId,
    StaffCandidate Candidate,
    StaffMarketStatus Status,
    string? CurrentEmployerOrganizationId,
    string CurrentEmployer,
    IReadOnlyList<StaffRole> AcceptableRoles,
    int HiringInterest,
    StaffMarketReasonAvailable ReasonAvailable,
    IReadOnlyList<string> CareerHistory,
    string AvailabilitySummary)
{
    public string CandidateId => Candidate.CandidateId;

    public string PersonId => Candidate.Person.PersonId;

    public string Name => Candidate.Person.Identity.DisplayName;

    public StaffRole DesiredRole => Candidate.StaffMember.CurrentRole;

    public StaffDepartment Department => Candidate.StaffMember.Department;

    public StaffSalary SalaryAsk => Candidate.ExpectedSalary;

    public int Reputation => Candidate.Reputation;

    public bool CanBeHired =>
        Status is StaffMarketStatus.Available or StaffMarketStatus.Interested
        && string.IsNullOrWhiteSpace(CurrentEmployerOrganizationId);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(MarketCandidateId)
            || string.IsNullOrWhiteSpace(CurrentEmployer)
            || string.IsNullOrWhiteSpace(AvailabilitySummary))
        {
            throw new ArgumentException("Staff market candidate requires identity and availability context.");
        }

        Candidate.Validate();
        if (HiringInterest is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(HiringInterest), "Hiring interest must be between zero and one hundred.");
        }

        if (AcceptableRoles.Count == 0)
        {
            throw new ArgumentException("Staff market candidate requires acceptable roles.", nameof(AcceptableRoles));
        }

        if (CareerHistory.Count == 0)
        {
            throw new ArgumentException("Staff market candidate requires career history.", nameof(CareerHistory));
        }
    }
}
