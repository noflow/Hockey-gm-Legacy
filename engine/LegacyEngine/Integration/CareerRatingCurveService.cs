using LegacyEngine.Rosters;

namespace LegacyEngine.Integration;

/// <summary>Creates stable individual peak windows and a realistic remaining-upside estimate.</summary>
public sealed class CareerRatingCurveService
{
    public NewGmScenarioSnapshot EnsureCurves(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        // Curves are frequently refreshed by presentation code; reuse established ratings when available.
        var rated = scenario.TrueRatings.Count > 0 ? scenario : new HockeyIntelligenceRatingService().EnsureRatings(scenario);
        var existing = rated.CareerRatingCurves.ToDictionary(curve => curve.PersonId, StringComparer.Ordinal);
        var curves = rated.TrueRatings
            .Select(truth => BuildCurve(rated, truth, existing.GetValueOrDefault(truth.PersonId)))
            .OrderBy(curve => curve.PlayerName, StringComparer.Ordinal)
            .ToArray();
        var updated = rated with { CareerRatingCurves = curves };
        updated.Validate();
        return updated;
    }

    public IReadOnlyList<string> BuildDossierLines(NewGmScenarioSnapshot scenario, string personId)
    {
        var curve = scenario.CareerRatingCurves.FirstOrDefault(item => item.PersonId == personId)
            ?? EnsureCurves(scenario).CareerRatingCurves.First(item => item.PersonId == personId);
        return new[]
        {
            $"Career stage: {Display(curve.GrowthStage)}",
            $"Peak window: ages {curve.PeakWindow.ExpectedStartAge}-{curve.PeakWindow.ExpectedEndAge}",
            $"Original potential estimate: {curve.Trajectory.OriginalPotentialEstimate}",
            $"Current potential estimate: {curve.Trajectory.CurrentPotentialEstimate}",
            $"Career-high overall: {curve.Trajectory.CareerHighOverall}",
            $"Trajectory: {curve.Trajectory.Trend}",
            $"Peak status: {curve.PeakProfile.Summary}",
            $"Decline outlook: {curve.DeclineProfile.Summary}",
            $"Development target: {curve.DevelopmentTarget.PrimaryFocus}",
            $"Best path: {curve.DevelopmentTarget.BestLeaguePlacement}",
            $"Usage guidance: {curve.DevelopmentTarget.RecommendedUsage}"
        };
    }

    private static PlayerCareerRatingCurve BuildCurve(NewGmScenarioSnapshot scenario, PlayerTrueRatings truth, PlayerCareerRatingCurve? existing)
    {
        var age = Age(scenario, truth.PersonId);
        var seed = StableHash(truth.PersonId);
        var goalie = truth.Position == RosterPosition.Goalie;
        var expectedStart = (goalie ? 27 : 25) + seed % 4 - 1;
        var duration = (goalie ? 7 : 5) + (seed / 11 % 4);
        var existingOriginal = existing?.Trajectory.OriginalPotentialEstimate ?? truth.Potential;
        var originalPotential = Math.Max(existingOriginal, truth.Overall);
        var declineStart = expectedStart + duration;
        var currentPotential = CurrentPotential(truth, age, originalPotential, expectedStart, declineStart);
        var historyHigh = scenario.PlayerRatingHistory.ForPerson(truth.PersonId).Select(item => item.Overall.Midpoint).Append(truth.Overall).Max();
        var actualPeakAge = historyHigh > truth.Overall && age > expectedStart ? age - 1 : age >= expectedStart && truth.Overall >= currentPotential ? age : existing?.PeakWindow.ActualPeakAge;
        var stage = StageFor(age, truth.Overall, currentPotential, expectedStart, declineStart);
        var atCeiling = truth.Overall >= currentPotential;
        var trend = TrendFor(stage, truth.Overall, currentPotential, existing?.Trajectory.CurrentPotentialEstimate);
        var peak = new PlayerPeakProfile(
            Math.Max(currentPotential, truth.Overall),
            historyHigh,
            atCeiling,
            atCeiling ? "At estimated ceiling; focus shifts to maintenance and refinement." : "Still has remaining development runway.");
        var decline = new PlayerDeclineProfile(
            declineStart,
            goalie ? 2 + seed % 3 : 3 + seed % 4,
            25 + seed % 55,
            age >= declineStart ? "Age-related decline is now part of the development outlook." : "No immediate decline concern; monitor workload and durability.");
        var trajectory = new PlayerRatingTrajectory(
            originalPotential,
            currentPotential,
            peak.ExpectedPeakOverall,
            historyHigh,
            trend,
            currentPotential > originalPotential ? "Current projection has exceeded the original estimate." : currentPotential < originalPotential - 4 ? "Projection has narrowed as career evidence accumulates." : "Projection remains within the established development path.");
        var curve = new PlayerCareerRatingCurve(
            truth.PersonId,
            truth.PlayerName,
            truth.Position,
            stage,
            new PlayerPeakWindow(expectedStart, declineStart - 1, actualPeakAge),
            peak,
            decline,
            DevelopmentTargetFor(truth.Position, stage),
            trajectory,
            scenario.CurrentDate);
        curve.Validate();
        return curve;
    }

    private static int CurrentPotential(PlayerTrueRatings truth, int age, int original, int peakStart, int declineStart)
    {
        if (age >= declineStart)
        {
            return Math.Clamp(Math.Max(truth.Overall, original - Math.Max(0, age - declineStart + 1)), truth.Overall, 100);
        }

        if (age >= peakStart)
        {
            return Math.Max(truth.Overall, Math.Min(original, truth.Potential));
        }

        return Math.Max(truth.Overall, Math.Max(original, truth.Potential));
    }

    private static PlayerGrowthStage StageFor(int age, int overall, int potential, int peakStart, int declineStart) =>
        age <= 18 ? PlayerGrowthStage.RawProspect :
        age <= 20 ? PlayerGrowthStage.EarlyDevelopment :
        age < peakStart - 3 ? PlayerGrowthStage.Developing :
        age < peakStart ? overall >= potential - 5 ? PlayerGrowthStage.NearPeak : PlayerGrowthStage.ApproachingNhl :
        age < declineStart ? overall >= potential ? PlayerGrowthStage.Peak : PlayerGrowthStage.MaintainingPeak :
        age < declineStart + 3 ? PlayerGrowthStage.EarlyDecline :
        age < 37 ? PlayerGrowthStage.Declining : PlayerGrowthStage.LateCareer;

    private static string TrendFor(PlayerGrowthStage stage, int overall, int potential, int? priorPotential) =>
        stage is PlayerGrowthStage.EarlyDecline or PlayerGrowthStage.Declining or PlayerGrowthStage.LateCareer ? "Declining" :
        overall >= potential ? "At estimated ceiling" :
        priorPotential is not null && potential > priorPotential ? "Ceiling revised upward" :
        priorPotential is not null && potential < priorPotential ? "Ceiling revised downward" :
        stage is PlayerGrowthStage.RawProspect or PlayerGrowthStage.EarlyDevelopment or PlayerGrowthStage.Developing or PlayerGrowthStage.ApproachingNhl ? "Developing" : "Maintaining";

    private static PlayerDevelopmentTarget DevelopmentTargetFor(RosterPosition position, PlayerGrowthStage stage)
    {
        var focus = position switch
        {
            RosterPosition.Goalie => "Rebound control and workload management",
            RosterPosition.Defense => "Defensive reads and transition decisions",
            RosterPosition.Center => "Two-way detail and play-driving",
            _ => "Puck skills, pace, and consistent game impact"
        };
        var placement = stage switch
        {
            PlayerGrowthStage.RawProspect or PlayerGrowthStage.EarlyDevelopment => "Development league with meaningful minutes",
            PlayerGrowthStage.Developing or PlayerGrowthStage.ApproachingNhl => "Sheltered role with special-teams opportunity",
            PlayerGrowthStage.EarlyDecline or PlayerGrowthStage.Declining => "Managed workload in a role that fits current strengths",
            _ => "Best available competitive role"
        };
        var usage = position == RosterPosition.Goalie
            ? "Build starts gradually; avoid unnecessary workload spikes."
            : "Use in a role that challenges the player without burying development minutes.";
        return new PlayerDevelopmentTarget(focus, placement, usage, "Target is based on career stage and position, and should be revisited as evidence changes.");
    }

    private static int Age(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.CalculateAge(scenario.CurrentDate)
        ?? scenario.AlphaSnapshot.Roster.FindPlayer(personId)?.Age
        ?? scenario.ProspectRights.FirstOrDefault(person => person.ProspectPersonId == personId)?.Age
        ?? 20;

    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 17;
            foreach (var character in value)
            {
                hash = hash * 31 + character;
            }
            return Math.Abs(hash);
        }
    }

    private static string Display(PlayerGrowthStage stage) =>
        string.Concat(stage.ToString().SelectMany((character, index) => index > 0 && char.IsUpper(character) ? new[] { ' ', character } : new[] { character }));
}
