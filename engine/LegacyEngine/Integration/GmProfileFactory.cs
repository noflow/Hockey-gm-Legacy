using LegacyEngine.HumanIntelligence;
using LegacyEngine.People;

namespace LegacyEngine.Integration;

public sealed class GmProfileFactory
{
    public GmCreationResult Create(GmProfileCreationSettings settings, DateOnly scenarioDate)
    {
        settings.Validate();

        var birthDate = settings.BirthDate ?? scenarioDate.AddYears(-settings.Age!.Value);
        var personality = BuildPersonality(settings.Style, settings.Background);
        var reputation = BuildReputation(settings.Background);
        var person = new Person(
            PersonId: settings.PersonId,
            Identity: new PersonIdentity(
                settings.FirstName.Trim(),
                settings.LastName.Trim(),
                settings.Gender,
                birthDate,
                settings.Nationality.Trim(),
                settings.Birthplace.Trim()),
            Status: PersonStatus.Active,
            Roles: Array.Empty<PersonRole>(),
            Reputation: reputation,
            Personality: personality,
            CareerTimeline: Array.Empty<CareerTimelineEntry>())
            .AddCareerTimelineEntry(new CareerTimelineEntry(
                EntryId: $"gm-profile-created:{settings.PersonId}",
                Date: scenarioDate,
                EntryType: CareerTimelineEntryType.StatusChanged,
                Summary: $"{settings.FirstName.Trim()} {settings.LastName.Trim()} began a GM career profile.",
                Details: new Dictionary<string, object?>
                {
                    ["preferred_name"] = settings.PreferredName.Trim(),
                    ["background"] = settings.Background.ToString(),
                    ["style"] = settings.Style.ToString(),
                    ["strengths"] = string.Join(", ", settings.Strengths),
                    ["weaknesses"] = string.Join(", ", settings.Weaknesses)
                }));

        var intelligence = BuildHumanIntelligence(settings.PersonId, settings.Style, personality);
        var result = new GmCreationResult(
            Person: person,
            PreferredName: settings.PreferredName.Trim(),
            Background: settings.Background,
            Style: settings.Style,
            Strengths: settings.Strengths.Select(item => item.Trim()).Where(item => item.Length > 0).ToArray(),
            Weaknesses: settings.Weaknesses.Select(item => item.Trim()).Where(item => item.Length > 0).ToArray(),
            HumanIntelligenceProfile: intelligence,
            Summary: $"{settings.PreferredName.Trim()} enters the organization as a {settings.Style} GM with a {settings.Background} background.");
        result.Validate();
        return result;
    }

    private static PersonalityProfile BuildPersonality(GmStyle style, GmBackground background)
    {
        var ambition = 62;
        var loyalty = 62;
        var temperament = 58;
        var adaptability = 60;
        var professionalism = 68;

        switch (style)
        {
            case GmStyle.DevelopmentFirst:
                loyalty += 6;
                professionalism += 4;
                break;
            case GmStyle.ScoutDriven:
                adaptability += 5;
                professionalism += 3;
                break;
            case GmStyle.AggressiveBuilder:
                ambition += 12;
                temperament -= 4;
                break;
            case GmStyle.RelationshipFirst:
                loyalty += 10;
                temperament += 5;
                break;
            case GmStyle.Analytical:
                adaptability += 8;
                professionalism += 6;
                break;
        }

        if (background == GmBackground.FormerPlayer)
        {
            loyalty += 4;
            temperament += 3;
        }
        else if (background == GmBackground.Agent)
        {
            ambition += 5;
        }

        return new PersonalityProfile(
            Math.Clamp(ambition, 0, 100),
            Math.Clamp(loyalty, 0, 100),
            Math.Clamp(temperament, 0, 100),
            Math.Clamp(adaptability, 0, 100),
            Math.Clamp(professionalism, 0, 100));
    }

    private static PersonReputation BuildReputation(GmBackground background) =>
        background switch
        {
            GmBackground.FormerPlayer => new PersonReputation(58, 50, 22),
            GmBackground.Scout => new PersonReputation(46, 52, 14),
            GmBackground.Coach => new PersonReputation(52, 47, 12),
            GmBackground.Agent => new PersonReputation(44, 50, 18),
            GmBackground.Analyst => new PersonReputation(38, 42, 10),
            _ => new PersonReputation(42, 40, 10)
        };

    private static HumanIntelligenceProfile BuildHumanIntelligence(
        string personId,
        GmStyle style,
        PersonalityProfile personality)
    {
        var riskTolerance = style switch
        {
            GmStyle.AggressiveBuilder => 74,
            GmStyle.Analytical => 48,
            GmStyle.DevelopmentFirst => 44,
            _ => 55
        };
        var communication = style == GmStyle.RelationshipFirst ? 76 : 62;

        return new HumanIntelligenceProfile(
            PersonId: personId,
            Ambition: personality.Ambition,
            Loyalty: personality.Loyalty,
            RiskTolerance: riskTolerance,
            PressureHandling: Math.Clamp((personality.Temperament + personality.Professionalism) / 2, 0, 100),
            Professionalism: personality.Professionalism,
            Communication: communication);
    }
}
