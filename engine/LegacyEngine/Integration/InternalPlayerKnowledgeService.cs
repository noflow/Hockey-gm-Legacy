using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

/// <summary>
/// Converts daily coaching, development, and organizational observation into a
/// high-confidence internal estimate. This deliberately does not expose the
/// engine's true ratings directly to the UI.
/// </summary>
public sealed class InternalPlayerKnowledgeService
{
    public NewGmScenarioSnapshot EnsureKnowledge(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        // Most callers already hold ratings. Avoid rebuilding the complete player universe per UI refresh.
        var rated = scenario.TrueRatings.Count > 0 ? scenario : new HockeyIntelligenceRatingService().EnsureRatings(scenario);
        var existing = rated.InternalPlayerKnowledge.ToDictionary(item => item.PersonId, StringComparer.Ordinal);
        var owned = OwnedPlayerIds(rated).ToHashSet(StringComparer.Ordinal);
        var knowledge = rated.TrueRatings
            .Where(truth => owned.Contains(truth.PersonId))
            .Select(truth => BuildKnowledge(rated, truth, existing.GetValueOrDefault(truth.PersonId)))
            .OrderBy(item => item.PlayerName, StringComparer.Ordinal)
            .ToArray();
        var updated = rated with { InternalPlayerKnowledge = knowledge };
        updated.Validate();
        return updated;
    }

    public OrganizationPlayerEvaluation BuildEvaluation(NewGmScenarioSnapshot scenario, string personId)
    {
        var prepared = EnsureKnowledge(new CareerRatingCurveService().EnsureCurves(scenario));
        var knowledge = prepared.InternalPlayerKnowledge.FirstOrDefault(item => item.PersonId == personId)
            ?? throw new ArgumentException("Player is not owned by this organization.", nameof(personId));
        var curve = prepared.CareerRatingCurves.First(item => item.PersonId == personId);
        var evaluation = new OrganizationPlayerEvaluation(
            personId,
            knowledge.PlayerName,
            knowledge.OverallEstimate,
            knowledge.PotentialEstimate,
            knowledge.KnowledgeLevel,
            curve.GrowthStage,
            curve.Trajectory.Trend,
            knowledge.Summary);
        evaluation.Validate();
        return evaluation;
    }

    private static InternalPlayerKnowledge BuildKnowledge(NewGmScenarioSnapshot scenario, PlayerTrueRatings truth, InternalPlayerKnowledge? existing)
    {
        var curve = scenario.CareerRatingCurves.FirstOrDefault(item => item.PersonId == truth.PersonId);
        var group = scenario.OrganizationRoster?.Players.FirstOrDefault(player => player.PersonId == truth.PersonId)?.Group;
        var level = group switch
        {
            OrganizationRosterGroup.NhlActiveRoster => PlayerKnowledgeLevel.Full,
            OrganizationRosterGroup.AhlAffiliateRoster or OrganizationRosterGroup.InjuredOrUnavailable => PlayerKnowledgeLevel.Detailed,
            OrganizationRosterGroup.SignedJuniorReturn => PlayerKnowledgeLevel.Detailed,
            OrganizationRosterGroup.UnsignedProspectRights => PlayerKnowledgeLevel.Working,
            _ => scenario.AlphaSnapshot.Roster.FindPlayer(truth.PersonId) is not null ? PlayerKnowledgeLevel.Detailed : PlayerKnowledgeLevel.Working
        };
        var offset = EstimateOffset(truth.PersonId, level);
        var overall = Math.Clamp(truth.Overall + offset, 0, 100);
        var currentPotential = curve?.Trajectory.CurrentPotentialEstimate ?? truth.Potential;
        var potential = Math.Clamp(Math.Max(overall, currentPotential + (offset == 0 ? 0 : Math.Sign(offset))), 0, 100);
        var confidence = level is PlayerKnowledgeLevel.Full or PlayerKnowledgeLevel.Detailed ? PlayerRatingConfidence.VeryHigh : PlayerRatingConfidence.High;
        var source = SourcesFor(group, truth.Position);
        var attributes = truth.Attributes
            .Select(attribute => new InternalAttributeEvaluation(
                attribute.Attribute,
                attribute.Category,
                Math.Clamp(attribute.Value + AttributeOffset(truth.PersonId, attribute.Attribute, level), 0, 100),
                confidence,
                scenario.CurrentDate,
                source[0],
                $"{SourceText(source[0])} evaluation is current."))
            .ToArray();
        var original = existing?.OriginalPotentialEstimate ?? currentPotential;
        var knowledge = new InternalPlayerKnowledge(
            scenario.Organization.OrganizationId,
            truth.PersonId,
            truth.PlayerName,
            truth.Position,
            level,
            overall,
            potential,
            Math.Max(original, overall),
            confidence,
            scenario.CurrentDate,
            source,
            attributes,
            $"{SourceText(source[0])} and internal staff maintain a {level.ToString().ToLowerInvariant()} evaluation of {truth.PlayerName}.");
        knowledge.Validate();
        return knowledge;
    }

    private static IEnumerable<string> OwnedPlayerIds(NewGmScenarioSnapshot scenario) =>
        (scenario.OrganizationRoster?.Players.Select(player => player.PersonId) ?? Array.Empty<string>())
            .Concat(scenario.AlphaSnapshot.Roster.Players.Select(player => player.PersonId))
            .Concat(scenario.ProspectRights.Where(prospect => prospect.Status is not ProspectStatus.Released and not ProspectStatus.Declined).Select(prospect => prospect.ProspectPersonId))
            .Distinct(StringComparer.Ordinal);

    private static IReadOnlyList<PlayerKnowledgeSource> SourcesFor(OrganizationRosterGroup? group, RosterPosition position) => group switch
    {
        OrganizationRosterGroup.NhlActiveRoster => new[] { PlayerKnowledgeSource.HeadCoach, PlayerKnowledgeSource.GameUsage, PlayerKnowledgeSource.DevelopmentReport },
        OrganizationRosterGroup.AhlAffiliateRoster => new[] { PlayerKnowledgeSource.AhlCoach, PlayerKnowledgeSource.DevelopmentCoach, PlayerKnowledgeSource.GameUsage },
        OrganizationRosterGroup.SignedJuniorReturn => new[] { PlayerKnowledgeSource.JuniorScout, PlayerKnowledgeSource.DevelopmentCoach, PlayerKnowledgeSource.DevelopmentReport },
        OrganizationRosterGroup.UnsignedProspectRights => new[] { PlayerKnowledgeSource.JuniorScout, PlayerKnowledgeSource.DevelopmentReport },
        OrganizationRosterGroup.InjuredOrUnavailable => new[] { PlayerKnowledgeSource.MedicalStaff, PlayerKnowledgeSource.HeadCoach },
        _ => position == RosterPosition.Goalie ? new[] { PlayerKnowledgeSource.HeadCoach, PlayerKnowledgeSource.TrainingStaff } : new[] { PlayerKnowledgeSource.AssistantCoach, PlayerKnowledgeSource.DevelopmentCoach }
    };

    private static int EstimateOffset(string personId, PlayerKnowledgeLevel level) =>
        level == PlayerKnowledgeLevel.Full ? 0 : StableHash(personId) % 3 - 1;

    private static int AttributeOffset(string personId, PlayerAttributeKey attribute, PlayerKnowledgeLevel level) =>
        level == PlayerKnowledgeLevel.Full ? 0 : StableHash($"{personId}:{attribute}") % 3 - 1;

    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 23;
            foreach (var character in value)
            {
                hash = hash * 37 + character;
            }
            return Math.Abs(hash);
        }
    }

    private static string SourceText(PlayerKnowledgeSource source) => source switch
    {
        PlayerKnowledgeSource.AhlCoach => "AHL coaching staff",
        PlayerKnowledgeSource.JuniorScout => "junior scouting staff",
        PlayerKnowledgeSource.DevelopmentCoach => "development staff",
        PlayerKnowledgeSource.MedicalStaff => "medical staff",
        PlayerKnowledgeSource.GameUsage => "game-usage review",
        _ => source.ToString()
    };
}
