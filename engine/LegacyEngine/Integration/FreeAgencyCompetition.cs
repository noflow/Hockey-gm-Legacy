namespace LegacyEngine.Integration;

public sealed record FreeAgencyCompetition(
    string CompetitionId,
    string PersonId,
    string TeamName,
    decimal EstimatedSalary,
    int EstimatedTermYears,
    string RoleOffered,
    int PlayerInterest,
    string WhyPlayerMayChooseThem,
    bool IsNotable,
    bool IsActive)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(CompetitionId)
            || string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(TeamName)
            || string.IsNullOrWhiteSpace(RoleOffered)
            || string.IsNullOrWhiteSpace(WhyPlayerMayChooseThem))
        {
            throw new ArgumentException("Competing free agent offer requires readable team, player, role, and reason fields.");
        }

        if (EstimatedSalary < 0 || EstimatedTermYears <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(EstimatedSalary), "Competing offer money and term must be valid.");
        }

        if (PlayerInterest is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(PlayerInterest), "Competing offer interest must be between 0 and 100.");
        }
    }
}
