namespace LegacyEngine.Scouting;

public sealed class ScoutingReportGenerator
{
    public ScoutingReport GenerateReport(
        Scout scout,
        ScoutingAssignment assignment,
        PlayerScoutingSnapshot player,
        DateOnly createdOn,
        GmScoutingProfile? gmScoutingProfile = null)
    {
        scout.Validate();
        assignment.Validate();
        player.Validate();
        gmScoutingProfile?.Validate();

        if (assignment.ScoutId != scout.ScoutId)
        {
            throw new ArgumentException("Assignment scout id must match the reporting scout.", nameof(assignment));
        }

        var confidenceScore = CalculateConfidenceScore(scout, assignment, gmScoutingProfile);
        var confidence = ToConfidenceLevel(confidenceScore);
        var uncertainty = ToUncertainty(confidence);
        var currentEstimate = BuildRange(player.CurrentAbility, scout.ReportBias, uncertainty);
        var potentialEstimate = BuildRange(player.Potential, scout.ReportBias / 2, uncertainty + 3);
        var recommendation = ChooseRecommendation(currentEstimate, potentialEstimate, player);

        var report = new ScoutingReport(
            ReportId: $"{assignment.AssignmentId}:{player.PlayerId}:{createdOn:yyyyMMdd}",
            PlayerId: player.PlayerId,
            ScoutId: scout.ScoutId,
            AssignmentId: assignment.AssignmentId,
            CreatedOn: createdOn,
            Facts: BuildFacts(player),
            Observations: BuildObservations(player, assignment),
            Opinions: BuildOpinions(currentEstimate, potentialEstimate, player),
            Unknowns: BuildUnknowns(player),
            Confidence: confidence,
            CurrentAbilityEstimate: currentEstimate,
            PotentialEstimate: potentialEstimate,
            Recommendation: recommendation,
            Details: new Dictionary<string, object?>
            {
                ["confidence_score"] = confidenceScore,
                ["gm_personal_scouting_bonus"] = gmScoutingProfile?.CalculatePersonalScoutingBonus(assignment) ?? 0,
                ["imperfect_report"] = true
            });

        report.Validate();
        return report;
    }

    private static int CalculateConfidenceScore(Scout scout, ScoutingAssignment assignment, GmScoutingProfile? gmScoutingProfile)
    {
        var specialtyBonus = assignment.FocusAreas.Any(scout.HasSpecialty) ? 12 : -8;
        var diligenceBonus = scout.Diligence / 5;
        var gmBonus = gmScoutingProfile?.CalculatePersonalScoutingBonus(assignment) ?? 0;

        return Math.Clamp(scout.Accuracy + specialtyBonus + diligenceBonus + gmBonus - 15, 0, 100);
    }

    private static ScoutingConfidenceLevel ToConfidenceLevel(int confidenceScore) =>
        confidenceScore switch
        {
            >= 85 => ScoutingConfidenceLevel.VeryHigh,
            >= 65 => ScoutingConfidenceLevel.High,
            >= 40 => ScoutingConfidenceLevel.Medium,
            >= 15 => ScoutingConfidenceLevel.Low,
            _ => ScoutingConfidenceLevel.Unknown
        };

    private static int ToUncertainty(ScoutingConfidenceLevel confidence) =>
        confidence switch
        {
            ScoutingConfidenceLevel.VeryHigh => 4,
            ScoutingConfidenceLevel.High => 7,
            ScoutingConfidenceLevel.Medium => 11,
            ScoutingConfidenceLevel.Low => 16,
            _ => 22
        };

    private static ScoutedRatingRange BuildRange(int internalRating, int reportBias, int uncertainty)
    {
        var perceivedCenter = Math.Clamp(internalRating + reportBias, 0, 100);
        var range = new ScoutedRatingRange(
            Low: Math.Clamp(perceivedCenter - uncertainty, 0, 100),
            High: Math.Clamp(perceivedCenter + uncertainty, 0, 100));

        range.Validate();
        return range;
    }

    private static IReadOnlyList<string> BuildFacts(PlayerScoutingSnapshot player) =>
        new[]
        {
            $"{player.Name} is a {player.Age}-year-old {player.Position}.",
            $"Current team: {player.Team}."
        };

    private static IReadOnlyList<string> BuildObservations(PlayerScoutingSnapshot player, ScoutingAssignment assignment)
    {
        var focus = string.Join(", ", assignment.FocusAreas);
        return new[]
        {
            $"Viewed through assignment focus: {focus}.",
            player.WorkEthic >= 70 ? "Motor and preparation showed up positively." : "Consistency of effort still needs more viewings.",
            player.Coachability >= 70 ? "Responds well to instruction and role adjustments." : "Coachability remains an open question."
        };
    }

    private static IReadOnlyList<string> BuildOpinions(ScoutedRatingRange currentEstimate, ScoutedRatingRange potentialEstimate, PlayerScoutingSnapshot player)
    {
        var projection = potentialEstimate.High >= 80 ? "upper-tier upside" : potentialEstimate.High >= 65 ? "regular contributor upside" : "depth projection";
        var risk = player.InjuryRisk >= 70 ? "medical risk needs attention" : "medical risk does not dominate the projection";

        return new[]
        {
            $"Current ability appears to sit in the {currentEstimate.Low}-{currentEstimate.High} band.",
            $"Projection suggests {projection}.",
            $"Scout opinion: {risk}."
        };
    }

    private static IReadOnlyList<string> BuildUnknowns(PlayerScoutingSnapshot player) =>
        new[]
        {
            "Long-term development curve remains uncertain.",
            "Pressure handling requires more evidence.",
            player.InjuryRisk >= 60 ? "Future durability is not settled." : "Future injury outcomes remain unknowable."
        };

    private static ScoutingRecommendation ChooseRecommendation(
        ScoutedRatingRange currentEstimate,
        ScoutedRatingRange potentialEstimate,
        PlayerScoutingSnapshot player)
    {
        if (potentialEstimate.High >= 85 && player.Character >= 65)
        {
            return ScoutingRecommendation.PriorityTarget;
        }

        if (potentialEstimate.High >= 75)
        {
            return ScoutingRecommendation.Target;
        }

        if (currentEstimate.High >= 60 || potentialEstimate.High >= 65)
        {
            return ScoutingRecommendation.Consider;
        }

        return player.InjuryRisk >= 80 ? ScoutingRecommendation.Avoid : ScoutingRecommendation.Watch;
    }
}
