using LegacyEngine.Integration;
using LegacyEngine.RuleEngine;
using LegacyEngine.Scouting;

internal sealed class Alpha70HockeyIntelligenceRatingTests
{
    public void PlayersReceiveHiddenTrueRatings()
    {
        var scenario = Scenario();

        Assert.True(scenario.TrueRatings.Count > 0, "Players should receive hidden true ratings.");
        Assert.True(scenario.TrueRatings.All(rating => rating.Attributes.Count > 0), "True ratings should include attributes.");
    }

    public void ScoutedRatingsDoNotExposeTrueRatingsDirectly()
    {
        var scenario = Scenario();
        var personId = scenario.AlphaSnapshot.DraftBoard.Entries.First().ProspectPersonId;
        var truth = scenario.TrueRatings.First(rating => rating.PersonId == personId);
        var scouted = scenario.ScoutedRatings.First(rating => rating.PersonId == personId);

        Assert.False(scouted.Overall.Display == truth.Overall.ToString() && scouted.Potential.Display == truth.Potential.ToString() && scouted.ConfidenceColor != PlayerRatingColor.Black, "Scouted ratings should not simply expose hidden truth.");
    }

    public void UnscoutedPlayerShowsUnknownForManyAttributes()
    {
        var scenario = WithDraftConfidence(null);
        var personId = scenario.AlphaSnapshot.DraftBoard.Entries.First().ProspectPersonId;
        var scouted = scenario.ScoutedRatings.First(rating => rating.PersonId == personId);

        Assert.Equal(PlayerRatingColor.Unknown, scouted.ConfidenceColor);
        Assert.True(scouted.Attributes.Count(attribute => attribute.Range.IsUnknown) >= 10, "Unscouted players should show many unknown attributes.");
    }

    public void ScoutingRevealsRedUncertainRanges()
    {
        var scenario = WithDraftConfidence(null);
        var personId = scenario.AlphaSnapshot.DraftBoard.Entries.First().ProspectPersonId;
        var scouted = new HockeyIntelligenceRatingService().RecordScoutingReport(scenario, personId, scoutQuality: 45).ScoutedRatings.First(rating => rating.PersonId == personId);

        Assert.Equal(PlayerRatingColor.Red, scouted.ConfidenceColor);
        Assert.False(scouted.Overall.IsExact, "Red confidence should show a range.");
    }

    public void RepeatedScoutingImprovesConfidence()
    {
        var scenario = WithDraftConfidence(ScoutingConfidenceLevel.Low);
        var personId = scenario.AlphaSnapshot.DraftBoard.Entries.First().ProspectPersonId;
        var service = new HockeyIntelligenceRatingService();
        var once = service.RecordScoutingReport(scenario, personId, scoutQuality: 55);
        var twice = service.RecordScoutingReport(once, personId, scoutQuality: 55);

        Assert.True(twice.ScoutedRatings.First(rating => rating.PersonId == personId).ConfidenceColor > once.ScoutedRatings.First(rating => rating.PersonId == personId).ConfidenceColor, "Repeated scouting should improve confidence.");
    }

    public void EliteScoutImprovesConfidenceFaster()
    {
        var scenario = WithDraftConfidence(ScoutingConfidenceLevel.Low);
        var personId = scenario.AlphaSnapshot.DraftBoard.Entries.First().ProspectPersonId;
        var service = new HockeyIntelligenceRatingService();
        var normal = service.RecordScoutingReport(scenario, personId, scoutQuality: 55);
        var elite = service.RecordScoutingReport(scenario, personId, scoutQuality: 90, specialty: PlayerRatingCategory.Skating);

        Assert.True(elite.ScoutedRatings.First(rating => rating.PersonId == personId).ConfidenceColor > normal.ScoutedRatings.First(rating => rating.PersonId == personId).ConfidenceColor, "Elite scout should improve confidence faster.");
    }

    public void WrongEstimatesConvergeTowardTrueValue()
    {
        var scenario = WithDraftConfidence(ScoutingConfidenceLevel.Low);
        var personId = scenario.AlphaSnapshot.DraftBoard.Entries.First().ProspectPersonId;
        var service = new HockeyIntelligenceRatingService();
        var truth = scenario.TrueRatings.First(rating => rating.PersonId == personId);
        var before = scenario.ScoutedRatings.First(rating => rating.PersonId == personId);
        var after = service.RecordScoutingReport(service.RecordScoutingReport(scenario, personId, 90), personId, 90).ScoutedRatings.First(rating => rating.PersonId == personId);

        Assert.True(Math.Abs(after.Overall.Midpoint - truth.Overall) <= Math.Abs(before.Overall.Midpoint - truth.Overall), "Better scouting should converge toward the true value.");
    }

    public void AttributeFamiliesAreImplemented()
    {
        var scenario = Scenario();
        var categories = scenario.TrueRatings.SelectMany(rating => rating.Attributes.Select(attribute => attribute.Category)).Distinct().ToArray();

        foreach (var category in new[] { PlayerRatingCategory.Offensive, PlayerRatingCategory.Defensive, PlayerRatingCategory.Skating, PlayerRatingCategory.Physical, PlayerRatingCategory.Skill, PlayerRatingCategory.Mental, PlayerRatingCategory.Team, PlayerRatingCategory.Goalie })
        {
            Assert.True(categories.Contains(category), $"{category} attributes should be generated.");
        }
    }

    public void JuniorRatingsLowerThanNhlRatings()
    {
        var junior = Scenario();
        var nhl = new HockeyIntelligenceRatingService().EnsureRatings(new MultiLeagueCareerService().CreateScenario(new MultiLeagueCareerService().SelectLeagueAndTeam(LeagueExperience.Nhl, "org-seattle-cascades")).ScenarioSnapshot);

        Assert.True(junior.TrueRatings.Average(rating => rating.Overall) < nhl.TrueRatings.Average(rating => rating.Overall), "Junior true ratings should average lower than NHL true ratings.");
    }

    public void EliteDraftProspectsCanReachLowEighties()
    {
        var scenario = Scenario();
        var top = scenario.TrueRatings.First(rating => rating.PersonId == scenario.AlphaSnapshot.DraftBoard.Entries.First().ProspectPersonId);

        Assert.True(top.Overall is >= 76 and <= 86, "Elite draft prospect should sit near the high-70s/low-80s junior OVR band.");
    }

    public void RarePotentialNinetyFivePlusUncommon()
    {
        var scenario = Scenario();
        var draftIds = scenario.AlphaSnapshot.DraftBoard.Entries.Select(entry => entry.ProspectPersonId).ToHashSet(StringComparer.Ordinal);
        var draftRatings = scenario.TrueRatings.Where(rating => draftIds.Contains(rating.PersonId)).ToArray();
        var rare = draftRatings.Count(rating => rating.Potential >= 95);

        Assert.True(rare >= 1, "At least one rare high-end prospect should exist in the opening class.");
        Assert.True(rare < draftRatings.Length / 5, "95+ potential should remain uncommon.");
    }

    public void DevelopmentChangesTrueAttributes()
    {
        var scenario = Scenario();
        var personId = scenario.TrueRatings.First(rating => rating.Attributes.Any(attribute => attribute.Attribute == PlayerAttributeKey.Speed)).PersonId;
        var before = scenario.TrueRatings.First(rating => rating.PersonId == personId).Attributes.First(attribute => attribute.Attribute == PlayerAttributeKey.Speed).Value;
        var after = new HockeyIntelligenceRatingService().ApplyDevelopmentToTrueRatings(scenario, personId, new Dictionary<PlayerAttributeKey, int> { [PlayerAttributeKey.Speed] = 4 });
        var updated = after.TrueRatings.First(rating => rating.PersonId == personId).Attributes.First(attribute => attribute.Attribute == PlayerAttributeKey.Speed).Value;

        Assert.Equal(Math.Clamp(before + 4, 0, 100), updated);
    }

    public void VisibleRatingsDoNotUpdateUntilReportOrScouting()
    {
        var scenario = Scenario();
        var personId = scenario.AlphaSnapshot.Roster.Players.First().PersonId;
        var before = scenario.ScoutedRatings.First(rating => rating.PersonId == personId).Overall.Display;
        var developed = new HockeyIntelligenceRatingService().ApplyDevelopmentToTrueRatings(scenario, personId, new Dictionary<PlayerAttributeKey, int> { [PlayerAttributeKey.Speed] = 5, [PlayerAttributeKey.Shooting] = 5 });
        var after = developed.ScoutedRatings.First(rating => rating.PersonId == personId).Overall.Display;

        Assert.Equal(before, after);
    }

    public void DossierExposesRatingsCorrectly()
    {
        var scenario = Scenario();
        var personId = scenario.AlphaSnapshot.DraftBoard.Entries.First(entry => entry.Bio?.Position != LegacyEngine.Rosters.RosterPosition.Goalie).ProspectPersonId;
        var dossier = new PlayerDossierService().CreateDossier(scenario, personId);
        var text = string.Join(Environment.NewLine, dossier.Sections.SelectMany(section => section.Lines));

        Assert.True(dossier.Sections.Any(section => section.Title == "Hockey Intelligence Ratings"), "Dossier should expose Hockey Intelligence Ratings.");
        Assert.True(text.Contains("Offensive:", StringComparison.Ordinal), "Dossier should expose category ratings.");
        Assert.False(text.Contains("PlayerTrueRatings", StringComparison.Ordinal), "Dossier should not expose true rating type names.");
    }

    public void TradeDraftFreeAgentUiExposesRatings()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("RatingText", StringComparison.Ordinal), "AlphaDesktop should expose compact ratings in player rows.");
        Assert.True(source.Contains("ConfidenceColor", StringComparison.Ordinal), "AlphaDesktop should expose confidence color labels.");
    }

    public void SaveLoadPreservesTrueAndScoutedKnowledge()
    {
        var service = new SaveGameService();
        var scenario = Scenario();
        var path = Path.Combine(Path.GetTempPath(), $"hockey-gm-alpha70-{Guid.NewGuid():N}.json");
        var saved = service.SaveCareer(
            scenario,
            Array.Empty<InboxMessage>(),
            Array.Empty<LeagueTransaction>(),
            new Dictionary<string, ActionCenterStatus>(),
            new BudgetOverviewService().Build(scenario, RulebookPresets.CreateJuniorMajor()),
            path,
            "Alpha 7.0 Test Save");
        var loaded = service.LoadFromFile(path, RulebookPresets.CreateJuniorMajor());

        Assert.True(saved.Success, saved.Message);
        Assert.True(loaded.Success, loaded.Message);
        Assert.Equal(scenario.TrueRatings.Count, loaded.SaveGame!.ScenarioSnapshot.TrueRatings.Count);
        Assert.Equal(scenario.ScoutedRatings.Count, loaded.SaveGame.ScenarioSnapshot.ScoutedRatings.Count);
    }

    public void AlphaDesktopDoesNotExposeTrueRatingsDirectly()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.False(source.Contains("TrueRatings", StringComparison.Ordinal), "AlphaDesktop should not directly render true ratings.");
        Assert.False(source.Contains("PlayerTrueRatings", StringComparison.Ordinal), "AlphaDesktop should not directly expose true rating models.");
    }

    private static NewGmScenarioSnapshot Scenario() =>
        new PlayerRatingService().EnsureRatings(
            new DevelopmentCurveService().EnsureCurves(
                new HockeyIntelligenceRatingService().EnsureRatings(NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot)));

    private static NewGmScenarioSnapshot WithDraftConfidence(ScoutingConfidenceLevel? confidence)
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var board = scenario.AlphaSnapshot.DraftBoard with
        {
            Entries = scenario.AlphaSnapshot.DraftBoard.Entries
                .Select(entry => entry with { ScoutingConfidence = confidence })
                .ToArray()
        };
        var updated = scenario with
        {
            AlphaSnapshot = scenario.AlphaSnapshot with { DraftBoard = board },
            TrueRatings = Array.Empty<PlayerTrueRatings>(),
            ScoutedRatings = Array.Empty<PlayerScoutedRatings>(),
            PlayerRatings = Array.Empty<PlayerRatingSnapshot>()
        };
        return new HockeyIntelligenceRatingService().EnsureRatings(updated);
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
