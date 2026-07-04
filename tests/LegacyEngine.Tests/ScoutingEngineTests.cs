using LegacyEngine.Scouting;

internal sealed class ScoutingEngineTests
{
    public void ScoutModelValidation()
    {
        var scout = BuildScout();

        scout.Validate();
        Assert.True(scout.HasSpecialty(ScoutSpecialty.Amateur), "Scout should expose declared specialties.");
        Assert.False(scout.HasSpecialty(ScoutSpecialty.Medical), "Scout should not claim undeclared specialties.");

        Assert.Throws<ArgumentException>(() => (scout with { Specialties = Array.Empty<ScoutSpecialty>() }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => (scout with { Accuracy = 101 }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => (scout with { ReportBias = 25 }).Validate());
    }

    public void ScoutingAssignmentValidation()
    {
        var assignment = BuildAssignment();

        assignment.Validate();
        Assert.Equal(ScoutingAssignmentType.Player, assignment.AssignmentType);
        Assert.Throws<ArgumentException>(() => (assignment with { FocusAreas = Array.Empty<ScoutSpecialty>() }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => (assignment with { DueOn = assignment.AssignedOn.AddDays(-1) }).Validate());
    }

    public void ScoutingReportsAreImperfect()
    {
        var player = BuildPlayer();
        var report = new ScoutingReportGenerator().GenerateReport(
            BuildScout(reportBias: 3),
            BuildAssignment(),
            player,
            new DateOnly(2026, 9, 15));

        Assert.Equal(player.PlayerId, report.PlayerId);
        Assert.True(report.Facts.Count > 0, "Report should include facts.");
        Assert.True(report.Observations.Count > 0, "Report should include observations.");
        Assert.True(report.Opinions.Count > 0, "Report should include opinions.");
        Assert.True(report.Unknowns.Count > 0, "Report should include unknowns.");
        Assert.True(report.CurrentAbilityEstimate.Low < player.CurrentAbility, "Report should show an ability range below the internal value.");
        Assert.True(report.CurrentAbilityEstimate.High > player.CurrentAbility, "Report should show an ability range above the internal value.");
        Assert.False(
            report.Details.ContainsKey("current_ability") || report.Details.ContainsKey("potential"),
            "Reports should not expose internal ratings in details.");
    }

    public void ConfidenceLevelsUseScoutFitAndGmBonus()
    {
        var assignment = BuildAssignment();
        var player = BuildPlayer();
        var generator = new ScoutingReportGenerator();
        var poorFitScout = BuildScout(
            accuracy: 35,
            diligence: 35,
            specialties: new[] { ScoutSpecialty.Medical });
        var strongFitScout = BuildScout(
            accuracy: 58,
            diligence: 80,
            specialties: new[] { ScoutSpecialty.Amateur, ScoutSpecialty.Forward });
        var gm = new GmScoutingProfile(
            GmId: "gm-001",
            PersonalScouting: 90,
            Specialties: new[] { ScoutSpecialty.Forward });

        var lowConfidenceReport = generator.GenerateReport(poorFitScout, assignment with { ScoutId = poorFitScout.ScoutId }, player, new DateOnly(2026, 9, 15));
        var highConfidenceReport = generator.GenerateReport(strongFitScout, assignment, player, new DateOnly(2026, 9, 15));
        var gmBonusReport = generator.GenerateReport(strongFitScout, assignment, player, new DateOnly(2026, 9, 15), gm);

        Assert.Equal(ScoutingConfidenceLevel.Low, lowConfidenceReport.Confidence);
        Assert.Equal(ScoutingConfidenceLevel.High, highConfidenceReport.Confidence);
        Assert.Equal(ScoutingConfidenceLevel.VeryHigh, gmBonusReport.Confidence);
        Assert.True(
            Width(gmBonusReport.CurrentAbilityEstimate) < Width(highConfidenceReport.CurrentAbilityEstimate),
            "GM personal scouting bonus should reduce uncertainty.");
    }

    public void PlayerDossierStructure()
    {
        var player = BuildPlayer();
        var report = new ScoutingReportGenerator().GenerateReport(
            BuildScout(),
            BuildAssignment(),
            player,
            new DateOnly(2026, 9, 15));
        var dossier = PlayerDossier
            .CreateEmpty(player.PlayerId, "Two-way forward with uncertain top-end projection.")
            .AddReport(report)
            .AddGmNotebookEntry("Watch again against faster competition.");

        Assert.Equal(player.PlayerId, dossier.PlayerId);
        Assert.Equal(1, dossier.ScoutingReports.Count);
        Assert.Equal(1, dossier.GmNotebook.Count);
        Assert.Equal(0, dossier.Analytics.Count);
        Assert.Equal(0, dossier.Medical.Count);
        Assert.Equal(0, dossier.Character.Count);
        Assert.Equal(0, dossier.Relationships.Count);
        Assert.Equal(0, dossier.Career.Count);
        Assert.Equal(0, dossier.History.Count);

        var otherPlayerReport = report with { PlayerId = "other-player" };
        Assert.Throws<ArgumentException>(() => dossier.AddReport(otherPlayerReport));
    }

    private static int Width(ScoutedRatingRange range) => range.High - range.Low;

    private static Scout BuildScout(
        int accuracy = 73,
        int diligence = 80,
        int reportBias = 0,
        IReadOnlyCollection<ScoutSpecialty>? specialties = null) =>
        new(
            ScoutId: "scout-001",
            Name: "Avery Stone",
            Specialties: specialties ?? new[] { ScoutSpecialty.Amateur, ScoutSpecialty.Forward },
            Accuracy: accuracy,
            Diligence: diligence,
            ReportBias: reportBias);

    private static ScoutingAssignment BuildAssignment() =>
        new(
            AssignmentId: "assignment-001",
            ScoutId: "scout-001",
            AssignmentType: ScoutingAssignmentType.Player,
            TargetId: "player-001",
            TargetName: "Noah Vale",
            FocusAreas: new[] { ScoutSpecialty.Amateur, ScoutSpecialty.Forward },
            AssignedOn: new DateOnly(2026, 9, 1),
            DueOn: new DateOnly(2026, 10, 1));

    private static PlayerScoutingSnapshot BuildPlayer() =>
        new(
            PlayerId: "player-001",
            Name: "Noah Vale",
            Age: 17,
            Position: "C",
            Team: "Kingston Juniors",
            CurrentAbility: 62,
            Potential: 84,
            WorkEthic: 78,
            Coachability: 74,
            InjuryRisk: 28,
            Character: 72);
}
