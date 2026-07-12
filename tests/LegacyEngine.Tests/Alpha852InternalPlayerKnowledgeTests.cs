using LegacyEngine.Integration;
using LegacyEngine.RuleEngine;

internal sealed class Alpha852InternalPlayerKnowledgeTests
{
    public void OwnedPlayersReceiveExactInternalEvaluations()
    {
        var scenario = NhlScenario();
        var service = new PlayerRatingService();
        var owned = scenario.OrganizationRoster!.Players.First(player => player.CountsTowardContractLimit);
        var rating = service.BuildSnapshot(scenario, owned.PersonId);
        var knowledge = scenario.InternalPlayerKnowledge.Single(item => item.PersonId == owned.PersonId);

        Assert.True(rating.Overall.IsExact && rating.Potential.IsExact, "Owned players should show a single internal OVR/POT assessment.");
        Assert.True(knowledge.Attributes.All(attribute => attribute.Estimate is >= 0 and <= 100), "Owned players should have usable internal attribute evaluations.");
        Assert.True(knowledge.Sources.Count > 0, "Internal knowledge should name an evaluation source.");
    }

    public void ProspectRightsReceiveInternalKnowledgeWithoutQuestionMarks()
    {
        var scenario = NhlScenario();
        var prospect = scenario.OrganizationRoster!.In(OrganizationRosterGroup.UnsignedProspectRights).First();
        var knowledge = scenario.InternalPlayerKnowledge.Single(item => item.PersonId == prospect.PersonId);
        var profile = scenario.ScoutingKnowledgeProfiles.Single(item => item.PlayerId == prospect.PersonId);

        Assert.True(knowledge.Attributes.All(attribute => !string.IsNullOrWhiteSpace(attribute.Note)), "Rights-held prospects should receive an internal staff evaluation.");
        Assert.True(profile.Attributes.All(attribute => !attribute.Estimate.IsUnknown), "Owned prospect attributes should not remain unknown to the organization.");
    }

    public void OutsideUnscoutedProspectsRemainUncertain()
    {
        var scenario = NhlScenario();
        var outside = scenario.AlphaSnapshot.DraftBoard.Entries
            .First(entry => scenario.InternalPlayerKnowledge.All(knowledge => knowledge.PersonId != entry.ProspectPersonId));
        var board = new LegacyEngine.Draft.DraftBoard(
            scenario.AlphaSnapshot.DraftBoard.BoardId,
            scenario.AlphaSnapshot.DraftBoard.OrganizationId,
            scenario.AlphaSnapshot.DraftBoard.Entries.Select(entry => entry.ProspectPersonId == outside.ProspectPersonId
                ? entry with { ScoutingConfidence = null }
                : entry).ToArray());
        var reset = scenario with
        {
            AlphaSnapshot = scenario.AlphaSnapshot with { DraftBoard = board },
            ScoutedRatings = scenario.ScoutedRatings.Where(rating => rating.PersonId != outside.ProspectPersonId).ToArray(),
            ScoutingKnowledgeProfiles = scenario.ScoutingKnowledgeProfiles.Where(profile => profile.PlayerId != outside.ProspectPersonId).ToArray()
        };
        var profile = new ScoutingIntelligenceService().CreateKnowledgeProfile(reset, outside.ProspectPersonId);

        Assert.True(profile.Attributes.Any(attribute => attribute.Estimate.IsUnknown), "Outside prospects should retain unknown attributes until the club scouts them.");
    }

    public void CareerCurvesGiveGoaliesLaterPeakWindows()
    {
        var scenario = NhlScenario();
        var goalie = scenario.CareerRatingCurves.First(curve => curve.Position == LegacyEngine.Rosters.RosterPosition.Goalie);
        var skater = scenario.CareerRatingCurves.First(curve => curve.Position != LegacyEngine.Rosters.RosterPosition.Goalie);

        Assert.True(goalie.PeakWindow.ExpectedStartAge >= skater.PeakWindow.ExpectedStartAge, "Goalies should peak no earlier than a typical skater.");
        Assert.True(goalie.DevelopmentTarget.PrimaryFocus.Contains("workload", StringComparison.OrdinalIgnoreCase), "Goalie development target should include workload management.");
    }

    public void DossierShowsEvaluationAndCareerCurveWithoutTrueRatings()
    {
        var scenario = NhlScenario();
        var player = scenario.OrganizationRoster!.Players.First();
        var dossier = new PlayerDossierService().CreateDossier(scenario, player.PersonId);
        var text = string.Join(Environment.NewLine, dossier.Sections.SelectMany(section => section.Lines));

        Assert.True(text.Contains("Internal organizational evaluation", StringComparison.Ordinal), "Owned player dossier should identify the internal assessment.");
        Assert.True(text.Contains("Career stage:", StringComparison.Ordinal), "Dossier should show career stage.");
        Assert.True(text.Contains("Peak window:", StringComparison.Ordinal), "Dossier should show peak window.");
        Assert.False(text.Contains("TrueRatings", StringComparison.Ordinal), "Dossier must not expose true engine ratings.");
    }

    public void SaveLoadPreservesKnowledgeAndCareerCurves()
    {
        var scenario = NhlScenario();
        var file = Path.Combine(Path.GetTempPath(), $"hockey-gm-alpha852-{Guid.NewGuid():N}.json");
        var budget = new BudgetOverviewService().Build(scenario, RulebookPresets.CreateNhlStyle());
        var saved = new SaveGameService().SaveCareer(scenario, Array.Empty<InboxMessage>(), Array.Empty<LeagueTransaction>(), new Dictionary<string, ActionCenterStatus>(), budget, file);
        var loaded = new SaveGameService().LoadFromFile(file, RulebookPresets.CreateNhlStyle());

        Assert.True(saved.Success, saved.Message);
        Assert.True(loaded.Success, loaded.Message);
        Assert.Equal(scenario.InternalPlayerKnowledge.Count, loaded.SaveGame!.ScenarioSnapshot.InternalPlayerKnowledge.Count);
        Assert.Equal(scenario.CareerRatingCurves.Count, loaded.SaveGame.ScenarioSnapshot.CareerRatingCurves.Count);
    }

    public void DesktopUsesRatingTrendsForOwnedPlayerRows()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("RatingTrendText(player.PersonId)", StringComparison.Ordinal), "Roster rows should expose rating trend/career stage context.");
        Assert.True(source.Contains("InternalPlayerKnowledge", StringComparison.Ordinal), "Desktop should distinguish internal organizational knowledge.");
    }

    public void HasNoGodotDependency()
    {
        var integrationPath = Path.Combine(FindRepositoryRoot(), "engine", "LegacyEngine", "Integration");
        var source = string.Join(Environment.NewLine, Directory.GetFiles(integrationPath, "*Player*Knowledge*.cs").Concat(Directory.GetFiles(integrationPath, "*Career*Rating*Curve*.cs")).Select(File.ReadAllText));

        Assert.False(source.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Internal player knowledge and career curves must remain engine-only.");
    }

    private static NewGmScenarioSnapshot NhlScenario()
    {
        var careers = new MultiLeagueCareerService();
        var team = careers.TeamsFor(LeagueExperience.Nhl).First();
        return new PlayerRatingService().EnsureRatings(careers.CreateScenario(careers.SelectLeagueAndTeam(LeagueExperience.Nhl, team.OrganizationId)).ScenarioSnapshot);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HockeyGmLegacy.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root could not be located.");
    }
}
