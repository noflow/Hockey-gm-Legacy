using LegacyEngine.Integration;

internal sealed class Alpha612FranchiseIdentityCultureTests
{
    public void FranchiseIdentityGeneratedForOrganizations()
    {
        var scenario = Scenario();

        Assert.True(scenario.FranchiseIdentities.Count >= scenario.LeagueProfile.Teams.Count, "Every league organization should receive a franchise identity.");
        Assert.True(scenario.FranchiseIdentities.All(identity => identity.CurrentIdentity != FranchisePhilosophy.Unknown), "Franchise identities should be known.");
        Assert.True(scenario.FranchiseIdentities.All(identity => identity.Culture != FranchiseCulture.Unknown), "Franchise cultures should be known.");
        Assert.True(scenario.FranchiseIdentities.All(identity => identity.FutureDirection != FranchiseDirection.Unknown), "Franchise directions should be known.");
    }

    public void CultureAndEraAreCreated()
    {
        var identity = Scenario().FranchiseIdentities.First();

        Assert.True(!string.IsNullOrWhiteSpace(identity.CurrentEra.Name), "Current era should be named.");
        Assert.True(identity.CurrentEra.StartYear > 1900, "Current era should have a start year.");
        Assert.True(identity.HistoricalEras.Count > 0, "Historical eras should be tracked.");
        Assert.True(identity.HistoricalIdentity.Count > 0, "Historical identity should be tracked.");
    }

    public void IdentityEvolutionIsSlow()
    {
        var identity = Scenario().FranchiseIdentities.First();
        var service = new FranchiseIdentityService();

        var lowEvidence = service.EvolveIdentity(identity, identity.LastUpdated.AddDays(30), FranchisePhilosophy.OffensiveHockey, FranchiseCulture.PlayerFriendly, "one good month is not enough.", 45);
        var highEvidence = service.EvolveIdentity(identity with { IdentityShifts = Array.Empty<FranchiseIdentityShift>() }, identity.LastUpdated.AddYears(3), FranchisePhilosophy.OffensiveHockey, FranchiseCulture.PlayerFriendly, "several seasons of staff, draft, and roster evidence pointed this way.", 85);

        Assert.Equal(identity.CurrentIdentity, lowEvidence.CurrentIdentity);
        Assert.Equal(FranchisePhilosophy.OffensiveHockey, highEvidence.CurrentIdentity);
        Assert.True(highEvidence.IdentityShifts.Any(shift => shift.ToIdentity == FranchisePhilosophy.OffensiveHockey), "Major identity evolution should be recorded.");
    }

    public void ReputationHistoryAndTeamDnaExist()
    {
        var identity = Scenario().FranchiseIdentities.First();

        Assert.True(identity.Reputation != FranchiseReputation.Unknown, "Reputation should be tracked.");
        Assert.True(identity.History.PlayoffAppearances >= 0, "Franchise history should track playoff appearances.");
        Assert.True(identity.TeamDna.Count > 0, "Team DNA should be visible.");
        Assert.True(identity.Strengths.Count > 0 && identity.Weaknesses.Count > 0, "Strengths and weaknesses should be visible.");
        Assert.True(identity.FutureGoals.Count > 0, "Future goals should be visible.");
    }

    public void OrganizationCommandCenterShowsFranchiseOverview()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("Franchise Overview", StringComparison.Ordinal), "Organization Command Center should expose franchise overview.");
        Assert.True(source.Contains("BuildFranchiseOverviewLines", StringComparison.Ordinal), "Organization Command Center should build franchise overview lines.");
        Assert.True(source.Contains("Franchise Direction", StringComparison.Ordinal), "Organization reports should show franchise direction.");
    }

    public void ReportsIncludeFranchiseIdentity()
    {
        var scenario = Scenario();
        var items = new FranchiseIdentityService().BuildExecutiveReportItems(scenario, scenario.Organization.OrganizationId);
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "engine", "LegacyEngine", "Integration", "ExecutiveReportService.cs"));

        Assert.True(items.ContainsKey("Current Identity"), "Franchise report items should include current identity.");
        Assert.True(items.ContainsKey("Culture"), "Franchise report items should include culture.");
        Assert.True(items.ContainsKey("Current Era"), "Franchise report items should include current era.");
        Assert.True(source.Contains("Franchise Identity", StringComparison.Ordinal), "Executive reports should include Franchise Identity sections.");
    }

    public void LeagueNewsIsLimitedAndReadable()
    {
        var news = new FranchiseIdentityService().BuildLeagueNews(Scenario(), maxItems: 3);

        Assert.True(news.Count <= 3, "Franchise identity league news should stay limited.");
        Assert.True(news.All(item => item.TransactionType == LeagueTransactionType.TeamIdentityUpdate), "Franchise identity news should use team identity updates.");
        Assert.True(news.All(item => item.Description.Contains("identity", StringComparison.OrdinalIgnoreCase)
            || item.Description.Contains("culture", StringComparison.OrdinalIgnoreCase)
            || item.Description.Contains("rebuild", StringComparison.OrdinalIgnoreCase)
            || item.Description.Contains("organization", StringComparison.OrdinalIgnoreCase)), "Franchise news should describe identity or culture.");
    }

    public void PlayerFitAppearsInDossier()
    {
        var scenario = Scenario();
        var playerId = scenario.AlphaSnapshot.Roster.Players.First().PersonId;
        var fit = new FranchiseIdentityService().EvaluatePlayerFit(scenario, playerId);
        var dossier = new PlayerDossierService().CreateDossier(scenario, playerId);

        Assert.True(fit.Score >= 0 && fit.Score <= 100, "Player fit should be scored.");
        Assert.True(dossier.Sections.Any(section => section.Title == "Organization Fit"), "Player dossier should include organization fit.");
        Assert.True(dossier.Sections.Single(section => section.Title == "Organization Fit").Lines.Any(line => line.Contains("Fit", StringComparison.OrdinalIgnoreCase)), "Dossier fit section should explain fit.");
    }

    public void StaffFitCanBeEvaluated()
    {
        var scenario = Scenario();
        var staffId = scenario.StaffMembers.First().PersonId;
        var fit = new FranchiseIdentityService().EvaluateStaffFit(scenario, staffId);

        Assert.True(fit.SubjectType == "Staff", "Staff fit should identify staff subject type.");
        Assert.True(fit.Reasons.Count > 0, "Staff fit should include reasons.");
    }

    public void SaveLoadPreservesFranchiseIdentity()
    {
        var scenario = Scenario();
        var budget = new BudgetOverviewService().Build(scenario, scenario.LeagueProfile.Rulebook);
        var service = new SaveGameService();
        var path = Path.Combine(Path.GetTempPath(), $"hockey-alpha612-{Guid.NewGuid():N}.json");

        var saved = service.SaveCareer(scenario, Array.Empty<InboxMessage>(), Array.Empty<LeagueTransaction>(), new Dictionary<string, ActionCenterStatus>(), budget, path);
        var loaded = service.LoadFromFile(path, scenario.LeagueProfile.Rulebook);

        Assert.True(saved.Success, saved.Message);
        Assert.True(loaded.Success, loaded.Message);
        Assert.Equal(scenario.FranchiseIdentities.Count, loaded.SaveGame!.ScenarioSnapshot.FranchiseIdentities.Count);
        Assert.Equal(scenario.FranchiseIdentities.First().CurrentIdentity, loaded.SaveGame.ScenarioSnapshot.FranchiseIdentities.First().CurrentIdentity);
    }

    public void NoForbiddenSystemsAdded()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "engine", "LegacyEngine", "Integration", "FranchiseIdentityService.cs"));

        Assert.False(source.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Franchise identity should not reference Godot.");
        Assert.False(source.Contains("MediaEngine", StringComparison.OrdinalIgnoreCase), "Franchise identity should not add a media engine.");
        Assert.False(source.Contains("Relocation", StringComparison.OrdinalIgnoreCase), "Franchise identity should not add relocation.");
        Assert.False(source.Contains("Expansion", StringComparison.OrdinalIgnoreCase), "Franchise identity should not add expansion.");
    }

    private static NewGmScenarioSnapshot Scenario() =>
        new FranchiseIdentityService().EnsureIdentities(NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot);

    private static string AlphaDesktopSource() =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

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

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
