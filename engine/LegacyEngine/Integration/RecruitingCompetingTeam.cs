namespace LegacyEngine.Integration;

public sealed record RecruitingCompetingTeam(
    string RecruitPersonId,
    string TeamName,
    int InterestStrength,
    bool HasOffer,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Weaknesses,
    string WhyRecruitMayChooseThem)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(RecruitPersonId))
        {
            throw new ArgumentException("Recruit person id is required.", nameof(RecruitPersonId));
        }

        if (string.IsNullOrWhiteSpace(TeamName))
        {
            throw new ArgumentException("Competing team name is required.", nameof(TeamName));
        }

        if (InterestStrength is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(InterestStrength), "Competing team interest must be between 0 and 100.");
        }

        if (Strengths.Count == 0 || Weaknesses.Count == 0 || string.IsNullOrWhiteSpace(WhyRecruitMayChooseThem))
        {
            throw new ArgumentException("Competing team profile requires strengths, weaknesses, and explanation.", nameof(WhyRecruitMayChooseThem));
        }
    }
}
