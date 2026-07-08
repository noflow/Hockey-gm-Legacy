using LegacyEngine.Integration;

public sealed class Alpha63RelationshipExpansionTests
{
    public void RelationshipTypesExist()
    {
        var types = Enum.GetNames<ExpandedRelationshipType>();

        Assert.Contains(nameof(ExpandedRelationshipType.GmPlayer), types);
        Assert.Contains(nameof(ExpandedRelationshipType.GmStaff), types);
        Assert.Contains(nameof(ExpandedRelationshipType.GmOwner), types);
        Assert.Contains(nameof(ExpandedRelationshipType.GmAgent), types);
        Assert.Contains(nameof(ExpandedRelationshipType.PlayerCoach), types);
        Assert.Contains(nameof(ExpandedRelationshipType.PlayerStaff), types);
        Assert.Contains(nameof(ExpandedRelationshipType.PlayerPlayer), types);
        Assert.Contains(nameof(ExpandedRelationshipType.StaffStaff), types);
        Assert.Contains(nameof(ExpandedRelationshipType.StaffOwner), types);
        Assert.Contains(nameof(ExpandedRelationshipType.OrganizationAgent), types);
        Assert.Contains(nameof(ExpandedRelationshipType.OrganizationPlayer), types);
        Assert.Contains(nameof(ExpandedRelationshipType.OrganizationStaff), types);
        Assert.Contains(nameof(ExpandedRelationshipType.OrganizationOrganization), types);
    }

    public void RelationshipChangeRecordsReasonAndDate()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var player = created.ScenarioSnapshot.AlphaSnapshot.Roster.ActivePlayers.First();
        var updated = new RelationshipExpansionService().RecordChange(
            created.ScenarioSnapshot,
            created.ScenarioSnapshot.AlphaSnapshot.GeneralManager.PersonId,
            player.PersonId,
            ExpandedRelationshipType.GmPlayer,
            RelationshipChangeTrigger.OwnerMeeting,
            3,
            created.ScenarioSnapshot.CurrentDate,
            "GM checked in after a difficult practice.",
            "The player appreciated clear communication.");

        var change = updated.RelationshipChangeHistory.First(item => item.Trigger == RelationshipChangeTrigger.OwnerMeeting);
        Assert.Equal(created.ScenarioSnapshot.CurrentDate, change.Date);
        Assert.True(change.Reason.Contains("checked in", StringComparison.OrdinalIgnoreCase), "Relationship change should store a readable reason.");
        Assert.True(change.VisibleExplanation.Contains("communication", StringComparison.OrdinalIgnoreCase), "Relationship change should store a visible explanation.");
    }

    public void SigningImprovesRelationship()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var player = scenario.AlphaSnapshot.Roster.ActivePlayers.First();
        var service = new RelationshipExpansionService();
        scenario = service.EnsureExpansion(scenario);
        var before = FindGmPlayer(scenario, player.PersonId).OverallScore;

        var afterScenario = service.RecordSigning(scenario, player.PersonId, scenario.CurrentDate);
        var after = FindGmPlayer(afterScenario, player.PersonId).OverallScore;

        Assert.True(after > before, "Signing should improve the GM-player relationship.");
    }

    public void RejectedOfferCanReduceRelationship()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var player = scenario.AlphaSnapshot.Roster.ActivePlayers.First();
        var service = new RelationshipExpansionService();
        scenario = service.EnsureExpansion(scenario);
        var before = FindGmPlayer(scenario, player.PersonId).OverallScore;

        var afterScenario = service.RecordRejectedOffer(scenario, player.PersonId, scenario.CurrentDate);
        var after = FindGmPlayer(afterScenario, player.PersonId).OverallScore;

        Assert.True(after < before, "Rejected offers should reduce the relationship score.");
    }

    public void TradeCanAffectRelationship()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var player = scenario.AlphaSnapshot.Roster.ActivePlayers.First();
        var service = new RelationshipExpansionService();
        scenario = service.EnsureExpansion(scenario);
        var before = FindGmPlayer(scenario, player.PersonId).Conflict;

        var afterScenario = service.RecordTrade(scenario, player.PersonId, scenario.CurrentDate);
        var after = FindGmPlayer(afterScenario, player.PersonId).Conflict;

        Assert.True(after > before, "Trade movement should add relationship friction.");
    }

    public void BrokenPromiseCreatesConflict()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var player = scenario.AlphaSnapshot.Roster.ActivePlayers.First();

        var updated = new RelationshipExpansionService().RecordBrokenPromise(scenario, player.PersonId, scenario.CurrentDate, "top-six role");

        Assert.True(updated.RelationshipConflicts.Any(conflict => conflict.ConflictType == RelationshipConflictType.BrokenPromise && conflict.IsMajor), "Broken promise should create a major conflict.");
    }

    public void RelationshipAffectsContractDecision()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var service = new RelationshipExpansionService();
        var freeAgent = created.ScenarioSnapshot.FreeAgentMarket!.FreeAgents.First();
        var ask = new ContractManagementService().BuildAsk(created.ScenarioSnapshot, ContractAskType.FreeAgent, freeAgent.PersonId);
        var request = new ContractOfferBuildRequest(freeAgent.PersonId, ContractAskType.FreeAgent, ask.RequestedSalary * 0.9m, ask.RequestedTermYears, ask.DesiredRole, "Development plan", false, "No staff promise", "Relationship test");
        var neutral = new ContractManagementService().BuildOffer(created.Registry, created.ScenarioSnapshot, request);
        var improvedScenario = service.RecordSigning(created.ScenarioSnapshot, freeAgent.PersonId, created.ScenarioSnapshot.CurrentDate);
        improvedScenario = service.RecordSigning(improvedScenario, freeAgent.PersonId, created.ScenarioSnapshot.CurrentDate);

        var improved = new ContractManagementService().BuildOffer(created.Registry, improvedScenario, request);

        Assert.True(improved.DecisionScore >= neutral.DecisionScore, "Positive relationship context should not hurt the contract decision score.");
        Assert.True(improved.Explanation.Reasons.Any(reason => reason.Contains("Relationship impact", StringComparison.OrdinalIgnoreCase)), "Contract explanation should include relationship impact.");
    }

    public void RelationshipAffectsStaffChemistry()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var staff = scenario.AlphaSnapshot.StaffMembers.First();

        var updated = new RelationshipExpansionService().RecordBrokenPromise(scenario, staff.PersonId, scenario.CurrentDate, "department role clarity");
        var report = new StaffOfficeService().EvaluateChemistry(updated, staff.PersonId);

        Assert.True(report.Summary.Contains("Relationship read", StringComparison.OrdinalIgnoreCase), "Staff chemistry should include expanded relationship read.");
        Assert.True(report.ConflictWarnings.Count > 0, "Relationship conflict should influence staff chemistry warnings.");
    }

    public void RelationshipAppearsInDossier()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var player = scenario.AlphaSnapshot.Roster.ActivePlayers.First();

        var dossier = new PlayerDossierService().CreateDossier(scenario, player.PersonId);
        var relationships = dossier.Sections.First(section => section.Title == "Relationships");

        Assert.True(relationships.Lines.Any(line => line.Contains("GmPlayer", StringComparison.OrdinalIgnoreCase) || line.Contains("relationship", StringComparison.OrdinalIgnoreCase)), "Dossier should show expanded relationship context.");
    }

    public void ActionCenterShowsMajorConflict()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var player = created.ScenarioSnapshot.AlphaSnapshot.Roster.ActivePlayers.First();
        var scenario = new RelationshipExpansionService().RecordBrokenPromise(created.ScenarioSnapshot, player.PersonId, created.ScenarioSnapshot.CurrentDate, "ice-time promise");
        var budget = new BudgetOverviewService().Build(scenario, created.Registry.Rulebook ?? scenario.LeagueProfile.Rulebook);
        var readiness = new SeasonReadinessService().Evaluate(created.Registry, scenario);
        var items = new ActionCenterService().BuildItems(scenario, Array.Empty<InboxMessage>(), budget, readiness, Array.Empty<StaffVacancy>());

        Assert.True(items.Any(item => item.Title.Contains("Relationship conflict", StringComparison.OrdinalIgnoreCase)), "Major conflict should appear in Action Center.");
    }

    public void SaveLoadPreservesRelationshipHistory()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var player = created.ScenarioSnapshot.AlphaSnapshot.Roster.ActivePlayers.First();
        var scenario = new RelationshipExpansionService().RecordBrokenPromise(created.ScenarioSnapshot, player.PersonId, created.ScenarioSnapshot.CurrentDate, "development promise");
        var service = new SaveGameService();
        var budget = new BudgetOverviewService().Build(scenario, created.Registry.Rulebook ?? scenario.LeagueProfile.Rulebook);
        var save = service.CreateSave(scenario, Array.Empty<InboxMessage>(), Array.Empty<LeagueTransaction>(), new Dictionary<string, ActionCenterStatus>(), budget);

        Assert.True(save.ScenarioSnapshot.RelationshipChangeHistory.Count > 0, "Save should preserve relationship change history.");
        Assert.True(save.ScenarioSnapshot.RelationshipConflicts.Count > 0, "Save should preserve relationship conflicts.");
    }

    public void NoForbiddenDependencyAdded()
    {
        var root = FindRepositoryRoot();
        var files = new[]
        {
            Path.Combine(root, "engine", "LegacyEngine", "Integration", "RelationshipExpansionModels.cs"),
            Path.Combine(root, "engine", "LegacyEngine", "Integration", "RelationshipExpansionService.cs")
        };

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            Assert.False(text.Contains("Godot", StringComparison.OrdinalIgnoreCase), $"{Path.GetFileName(file)} should not depend on Godot.");
        }
    }

    private static ExpandedRelationshipProfile FindGmPlayer(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.RelationshipProfiles.First(profile =>
            profile.RelationshipType == ExpandedRelationshipType.GmPlayer
            && profile.TargetId == personId);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var marker = Path.Combine(directory.FullName, "HockeyGmLegacy.slnx");
            if (File.Exists(marker))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find Hockey GM Legacy repository root.");
    }
}
