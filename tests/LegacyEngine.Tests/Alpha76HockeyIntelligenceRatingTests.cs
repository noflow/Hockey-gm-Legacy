using LegacyEngine.Integration;
using LegacyEngine.Scouting;

internal sealed class Alpha76HockeyIntelligenceRatingTests
{
    public void FullAttributeCatalogIsImplemented()
    {
        var scenario = Scenario();
        var attributes = scenario.TrueRatings.SelectMany(rating => rating.Attributes).ToArray();

        foreach (var category in Enum.GetValues<PlayerRatingCategory>())
        {
            Assert.True(attributes.Any(attribute => attribute.Category == category), $"{category} category should be represented.");
        }

        foreach (var attribute in RequiredAttributes())
        {
            Assert.True(attributes.Any(rating => rating.Attribute == attribute), $"{attribute} should be generated.");
        }
    }

    public void ScoutedRatingsUseRequestedConfidenceColors()
    {
        var scenario = WithDraftConfidence(null);
        var personId = scenario.AlphaSnapshot.DraftBoard.Entries.First().ProspectPersonId;
        var service = new HockeyIntelligenceRatingService();
        var unknown = scenario.ScoutedRatings.First(rating => rating.PersonId == personId);
        var redScenario = service.RecordScoutingReport(scenario, personId, 45);
        var greenScenario = service.RecordScoutingReport(redScenario, personId, 45);
        var blueScenario = service.RecordScoutingReport(greenScenario, personId, 45);
        var blackScenario = service.RecordScoutingReport(blueScenario, personId, 90);
        var red = redScenario.ScoutedRatings.First(rating => rating.PersonId == personId);
        var green = greenScenario.ScoutedRatings.First(rating => rating.PersonId == personId);
        var blue = blueScenario.ScoutedRatings.First(rating => rating.PersonId == personId);
        var black = blackScenario.ScoutedRatings.First(rating => rating.PersonId == personId);

        Assert.Equal(PlayerRatingColor.Unknown, unknown.ConfidenceColor);
        Assert.Equal(PlayerRatingColor.Red, red.ConfidenceColor);
        Assert.Equal(PlayerRatingColor.Green, green.ConfidenceColor);
        Assert.True(blue.ConfidenceColor >= PlayerRatingColor.Blue, "Repeated scouting should reach high confidence.");
        Assert.Equal(PlayerRatingColor.Black, black.ConfidenceColor);
    }

    public void SpecialtyScoutImprovesMatchingCategoryConfidence()
    {
        var scenario = WithDraftConfidence(null);
        var personId = scenario.AlphaSnapshot.DraftBoard.Entries.First(entry => entry.Bio?.Position != LegacyEngine.Rosters.RosterPosition.Goalie).ProspectPersonId;

        var scouted = new HockeyIntelligenceRatingService()
            .RecordScoutingReport(scenario, personId, scoutQuality: 55, specialty: PlayerRatingCategory.Skating)
            .ScoutedRatings
            .First(rating => rating.PersonId == personId);

        var skating = scouted.Attributes.First(attribute => attribute.Category == PlayerRatingCategory.Skating);
        var offensive = scouted.Attributes.First(attribute => attribute.Category == PlayerRatingCategory.Offensive);
        Assert.True(skating.ConfidenceColor > offensive.ConfidenceColor, "Specialty scouting should improve matching category confidence faster.");
    }

    public void RegionalFitImprovesOverallConfidenceFaster()
    {
        var scenario = WithDraftConfidence(ScoutingConfidenceLevel.Low);
        var personId = scenario.AlphaSnapshot.DraftBoard.Entries.First().ProspectPersonId;
        var service = new HockeyIntelligenceRatingService();
        var normal = service.RecordScoutingReport(scenario, personId, scoutQuality: 55).ScoutedRatings.First(rating => rating.PersonId == personId);
        var regional = service.RecordScoutingReport(scenario, personId, scoutQuality: 55, regionFit: true).ScoutedRatings.First(rating => rating.PersonId == personId);

        Assert.True(regional.ConfidenceColor > normal.ConfidenceColor, "Known-region scouting should improve confidence faster.");
    }

    public void DevelopmentChangesHiddenTruthButVisibleEstimateWaitsForScouting()
    {
        var scenario = Scenario();
        var personId = scenario.AlphaSnapshot.DraftBoard.Entries.First().ProspectPersonId;
        var beforeTruth = scenario.TrueRatings.First(rating => rating.PersonId == personId);
        var beforeVisible = scenario.ScoutedRatings.First(rating => rating.PersonId == personId);
        var developed = new HockeyIntelligenceRatingService().ApplyDevelopmentToTrueRatings(
            scenario,
            personId,
            new Dictionary<PlayerAttributeKey, int>
            {
                [PlayerAttributeKey.Shooting] = 6,
                [PlayerAttributeKey.PuckSkill] = 5
            });
        var afterTruth = developed.TrueRatings.First(rating => rating.PersonId == personId);
        var afterVisible = developed.ScoutedRatings.First(rating => rating.PersonId == personId);

        Assert.True(afterTruth.Overall >= beforeTruth.Overall, "Development should affect hidden true ratings.");
        Assert.Equal(beforeVisible.Overall.Display, afterVisible.Overall.Display);
    }

    public void DossierShowsRatingsHistoryCurveAndNoHiddenTruth()
    {
        var scenario = new PlayerRatingService().EnsureRatings(new DevelopmentCurveService().EnsureCurves(Scenario()));
        var personId = scenario.AlphaSnapshot.DraftBoard.Entries.First().ProspectPersonId;
        var dossier = new PlayerDossierService().CreateDossier(scenario, personId);
        var text = string.Join(Environment.NewLine, dossier.Sections.SelectMany(section => section.Lines));

        Assert.True(dossier.Sections.Any(section => section.Title == "Ratings"), "Dossier should include visible ratings.");
        Assert.True(dossier.Sections.Any(section => section.Title == "Hockey Intelligence Ratings"), "Dossier should include Hockey Intelligence rating categories.");
        Assert.True(text.Contains("Rating trend:", StringComparison.Ordinal), "Dossier should include rating trend/history.");
        Assert.True(text.Contains("Development curve:", StringComparison.Ordinal), "Dossier should include development curve context.");
        Assert.False(text.Contains("PlayerTrueRatings", StringComparison.Ordinal), "Dossier should not expose hidden true rating model names.");
    }

    public void AlphaDesktopUsesScoutedRatingTextOnly()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("ScenarioSnapshot.ScoutedRatings", StringComparison.Ordinal), "AlphaDesktop rating text should use scouted estimates.");
        Assert.True(source.Contains("OVR", StringComparison.Ordinal), "AlphaDesktop should show OVR labels.");
        Assert.True(source.Contains("POT", StringComparison.Ordinal), "AlphaDesktop should show POT labels.");
        Assert.False(source.Contains("ScenarioSnapshot.TrueRatings", StringComparison.Ordinal), "AlphaDesktop should not render hidden true ratings.");
        Assert.False(source.Contains("PlayerTrueRatings", StringComparison.Ordinal), "AlphaDesktop should not reference hidden true rating types.");
    }

    public void SaveLoadPreservesHockeyIntelligenceRatingsAndHistory()
    {
        var service = new SaveGameService();
        var scenario = new PlayerRatingService().EnsureRatings(new DevelopmentCurveService().EnsureCurves(Scenario()));
        var path = Path.Combine(Path.GetTempPath(), $"hockey-gm-alpha76-{Guid.NewGuid():N}.json");
        var saved = service.SaveCareer(
            scenario,
            Array.Empty<InboxMessage>(),
            Array.Empty<LeagueTransaction>(),
            new Dictionary<string, ActionCenterStatus>(),
            new BudgetOverviewService().Build(scenario, LegacyEngine.RuleEngine.RulebookPresets.CreateJuniorMajor()),
            path,
            "Alpha 7.6 Test Save");
        var loaded = service.LoadFromFile(path, LegacyEngine.RuleEngine.RulebookPresets.CreateJuniorMajor());

        Assert.True(saved.Success, saved.Message);
        Assert.True(loaded.Success, loaded.Message);
        Assert.Equal(scenario.TrueRatings.Count, loaded.SaveGame!.ScenarioSnapshot.TrueRatings.Count);
        Assert.Equal(scenario.ScoutedRatings.Count, loaded.SaveGame.ScenarioSnapshot.ScoutedRatings.Count);
        Assert.True(loaded.SaveGame.ScenarioSnapshot.PlayerRatingHistory.Snapshots.Count > 0, "Rating history should be preserved.");
    }

    private static IReadOnlyList<PlayerAttributeKey> RequiredAttributes() =>
        new[]
        {
            PlayerAttributeKey.Shooting,
            PlayerAttributeKey.Playmaking,
            PlayerAttributeKey.PuckSkill,
            PlayerAttributeKey.HockeyIQ,
            PlayerAttributeKey.Vision,
            PlayerAttributeKey.OffensiveAwareness,
            PlayerAttributeKey.Creativity,
            PlayerAttributeKey.Positioning,
            PlayerAttributeKey.StickChecking,
            PlayerAttributeKey.ShotBlocking,
            PlayerAttributeKey.DefensiveAwareness,
            PlayerAttributeKey.Backchecking,
            PlayerAttributeKey.BoardPlay,
            PlayerAttributeKey.Speed,
            PlayerAttributeKey.Acceleration,
            PlayerAttributeKey.Agility,
            PlayerAttributeKey.EdgeWork,
            PlayerAttributeKey.Balance,
            PlayerAttributeKey.Endurance,
            PlayerAttributeKey.Strength,
            PlayerAttributeKey.Hitting,
            PlayerAttributeKey.Aggression,
            PlayerAttributeKey.Grit,
            PlayerAttributeKey.Toughness,
            PlayerAttributeKey.Durability,
            PlayerAttributeKey.Passing,
            PlayerAttributeKey.HandEye,
            PlayerAttributeKey.Faceoffs,
            PlayerAttributeKey.Deception,
            PlayerAttributeKey.PuckProtection,
            PlayerAttributeKey.Stickhandling,
            PlayerAttributeKey.Leadership,
            PlayerAttributeKey.Consistency,
            PlayerAttributeKey.Coachability,
            PlayerAttributeKey.WorkEthic,
            PlayerAttributeKey.Composure,
            PlayerAttributeKey.Confidence,
            PlayerAttributeKey.TeamPlay,
            PlayerAttributeKey.Adaptability,
            PlayerAttributeKey.Professionalism,
            PlayerAttributeKey.LockerRoom,
            PlayerAttributeKey.MentorAbility,
            PlayerAttributeKey.Reflexes,
            PlayerAttributeKey.Glove,
            PlayerAttributeKey.Blocker,
            PlayerAttributeKey.ReboundManagement,
            PlayerAttributeKey.PuckTracking,
            PlayerAttributeKey.LateralMovement,
            PlayerAttributeKey.PuckHandling,
            PlayerAttributeKey.Recovery
        };

    private static NewGmScenarioSnapshot Scenario() =>
        new HockeyIntelligenceRatingService().EnsureRatings(NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot);

    private static NewGmScenarioSnapshot WithDraftConfidence(ScoutingConfidenceLevel? confidence)
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var board = scenario.AlphaSnapshot.DraftBoard with
        {
            Entries = scenario.AlphaSnapshot.DraftBoard.Entries
                .Select(entry => entry with { ScoutingConfidence = confidence })
                .ToArray()
        };
        return new HockeyIntelligenceRatingService().EnsureRatings(scenario with
        {
            AlphaSnapshot = scenario.AlphaSnapshot with { DraftBoard = board },
            TrueRatings = Array.Empty<PlayerTrueRatings>(),
            ScoutedRatings = Array.Empty<PlayerScoutedRatings>(),
            PlayerRatings = Array.Empty<PlayerRatingSnapshot>(),
            PlayerRatingHistory = PlayerRatingHistory.Empty
        });
    }

    private static string FindRepositoryRoot()
    {
        var current = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(current, "HockeyGmLegacy.slnx")))
        {
            current = Directory.GetParent(current)?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
        }

        return current;
    }
}
