using LegacyEngine.HumanIntelligence;
using LegacyEngine.People;

namespace LegacyEngine.Integration;

public sealed record GmCreationResult(
    Person Person,
    string PreferredName,
    GmBackground Background,
    GmStyle Style,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Weaknesses,
    HumanIntelligenceProfile HumanIntelligenceProfile,
    string Summary)
{
    public void Validate()
    {
        Person.Validate();
        HumanIntelligenceProfile.Validate();

        if (string.IsNullOrWhiteSpace(PreferredName))
        {
            throw new ArgumentException("Preferred name is required.", nameof(PreferredName));
        }

        if (Strengths.Count == 0)
        {
            throw new ArgumentException("GM creation must include strengths.", nameof(Strengths));
        }

        if (Weaknesses.Count == 0)
        {
            throw new ArgumentException("GM creation must include weaknesses.", nameof(Weaknesses));
        }

        if (string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("GM creation summary is required.", nameof(Summary));
        }
    }
}
