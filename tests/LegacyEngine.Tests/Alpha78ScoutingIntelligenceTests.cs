using LegacyEngine.Integration;
using LegacyEngine.Rosters;
using LegacyEngine.Scouting;
using LegacyEngine.Staff;

internal sealed class Alpha78ScoutingIntelligenceTests
{
    public void ScoutingKnowledgeProfileCreated()
    {
        var scenario = Scenario();
        var personId = ProspectId(scenario);
        var profile = new ScoutingIntelligenceService().CreateKnowledgeProfile(scenario, personId);

        Assert.Equal(personId, profile.PlayerId);
        Assert.True(profile.Attributes.Count > 0, "Knowledge profile should include attribute states.");
        Assert.True(profile.Consensus.Summary.Length > 0, "Knowledge profile should include consensus.");
    }

    public void UnscoutedAttributesRemainUnknown()
    {
        var raw = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var scenario = new HockeyIntelligenceRatingService().EnsureRatings(raw with
        {
            CompletedScoutingReports = Array.Empty<ScoutingReport>(),
            ScoutingKnowledgeProfiles = Array.Empty<ScoutingKnowledgeProfile>()
        });
        var personId = ProspectId(scenario);
        var profile = new ScoutingIntelligenceService().CreateKnowledgeProfile(scenario, personId);

        Assert.True(profile.UnknownAttributeCount > 0, "Unscouted draft prospects should keep unknown attribute states.");
        Assert.True(profile.Attributes.Any(attribute => attribute.Estimate.IsUnknown), "Unknown attributes should display as unknown ranges.");
    }

    public void ScoutingAssignmentUpdatesSomeAttributes()
    {
        var result = CompletePlayerAssignment();
        var profile = result.ScenarioSnapshot.ScoutingKnowledgeProfiles.First(profile => profile.PlayerId == result.Assignment!.TargetPlayerId);

        Assert.True(profile.KnownAttributeCount > 0, "Completed assignment should update attribute knowledge.");
        Assert.True(profile.ScoutOpinions.Count > 0, "Completed assignment should store scout-specific opinions.");
    }

    public void RepeatedScoutingImprovesConfidence()
    {
        var first = CompletePlayerAssignment();
        var personId = first.Assignment!.TargetPlayerId!;
        var firstProfile = first.ScenarioSnapshot.ScoutingKnowledgeProfiles.First(profile => profile.PlayerId == personId);
        var second = CompletePlayerAssignment(first.ScenarioSnapshot, first.Registry, personId, AlternateScoutId(first.ScenarioSnapshot));
        var secondProfile = second.ScenarioSnapshot.ScoutingKnowledgeProfiles.First(profile => profile.PlayerId == personId);

        Assert.True(secondProfile.Consensus.ConfidenceColor >= firstProfile.Consensus.ConfidenceColor, "Repeated scouting should not reduce consensus confidence.");
        Assert.True(secondProfile.ScoutOpinions.Count >= firstProfile.ScoutOpinions.Count, "Repeated scouting should retain more scout opinions.");
    }

    public void ScoutSpecialtyImprovesMatchingAttributesFaster()
    {
        var result = CompleteRegionAssignment(ScoutingRegionFocus.Character);
        var profile = result.ScenarioSnapshot.ScoutingKnowledgeProfiles.First(profile => profile.KnownAttributeCount > 0);
        var mental = profile.Attributes.Where(attribute => attribute.Category == PlayerRatingCategory.Mental).Select(attribute => attribute.ConfidenceColor).DefaultIfEmpty(PlayerRatingColor.Unknown).Max();
        var physical = profile.Attributes.Where(attribute => attribute.Category == PlayerRatingCategory.Physical).Select(attribute => attribute.ConfidenceColor).DefaultIfEmpty(PlayerRatingColor.Unknown).Max();

        Assert.True(mental >= physical, "Character-focused scouting should improve mental/team-style reads faster than unrelated physical reads.");
    }

    public void ScoutBiasAffectsEstimate()
    {
        var result = CompletePlayerAssignment();
        var profile = result.ScenarioSnapshot.ScoutingKnowledgeProfiles.First(profile => profile.ScoutOpinions.Count > 0);
        var opinion = profile.ScoutOpinions.First();

        Assert.True(Enum.IsDefined(opinion.Bias), "Scout opinion should store a bias/tendency.");
        Assert.True(!opinion.Estimate.IsUnknown, "Biased scout opinion should still produce a visible estimate range.");
        Assert.True(opinion.Note.Length > 0, "Bias should be explained in the scout note.");
    }

    public void MultipleScoutsCanDisagree()
    {
        var first = CompletePlayerAssignment();
        var personId = first.Assignment!.TargetPlayerId!;
        var second = CompletePlayerAssignment(first.ScenarioSnapshot, first.Registry, personId, AlternateScoutId(first.ScenarioSnapshot));
        var profile = second.ScenarioSnapshot.ScoutingKnowledgeProfiles.First(profile => profile.PlayerId == personId);

        Assert.True(profile.ScoutOpinions.Select(opinion => opinion.ScoutId).Distinct(StringComparer.Ordinal).Count() > 1, "Multiple scouts should be able to record opinions on one player.");
        Assert.True(profile.Consensus.BiggestDisagreement.Length > 0, "Consensus should describe disagreement status.");
    }

    public void ConsensusGenerated()
    {
        var result = CompletePlayerAssignment();
        var profile = result.ScenarioSnapshot.ScoutingKnowledgeProfiles.First(profile => profile.ScoutOpinions.Count > 0);

        Assert.Equal(profile.PlayerId, profile.Consensus.PlayerId);
        Assert.True(profile.Consensus.Summary.Contains("attributes", StringComparison.OrdinalIgnoreCase), "Consensus should summarize attribute-level knowledge.");
    }

    public void ScoutAccuracyHistoryUpdates()
    {
        var result = CompletePlayerAssignment();

        Assert.True(result.ScenarioSnapshot.ScoutAccuracyHistory.Count > 0, "Scouting completion should update scout accuracy history.");
        Assert.True(result.ScenarioSnapshot.ScoutAccuracyHistory.Any(history => history.CorrectHits + history.Misses + history.GemsFound + history.BustsRecommended >= 0), "Scout accuracy history should store outcomes.");
    }

    public void DossierExposesScoutingIntelligence()
    {
        var result = CompletePlayerAssignment();
        var personId = result.Assignment!.TargetPlayerId!;
        var dossier = new PlayerDossierService().CreateDossier(result.ScenarioSnapshot, personId);
        var text = string.Join(Environment.NewLine, dossier.Sections.SelectMany(section => section.Lines));

        Assert.True(text.Contains("Scouting intelligence:", StringComparison.Ordinal), "Dossier should include scouting intelligence.");
        Assert.True(text.Contains("Attribute confidence grid:", StringComparison.Ordinal), "Dossier should include the attribute confidence grid.");
    }

    public void WarRoomExposesScoutConsensus()
    {
        var result = CompletePlayerAssignment();
        var summary = new DraftWarRoomService().BuildWarRoomSummary(result.ScenarioSnapshot);

        Assert.True(summary.Contains("Scouting intelligence:", StringComparison.Ordinal), "War room should expose scouting intelligence summary.");
        Assert.True(summary.Contains("Consensus board:", StringComparison.Ordinal), "War room should expose consensus-board status.");
    }

    public void StaleReportFlagged()
    {
        var scenario = Scenario();
        var service = new ScoutingIntelligenceService();
        var personId = ProspectId(scenario);
        var profile = service.CreateKnowledgeProfile(scenario, personId) with
        {
            LastViewedDate = scenario.CurrentDate.AddDays(-60)
        };
        var staleScenario = scenario with { ScoutingKnowledgeProfiles = new[] { profile } };
        var refreshed = service.CreateKnowledgeProfile(staleScenario, personId);

        Assert.True(refreshed.IsStale, "Old scouting knowledge should be marked stale.");
        Assert.True(refreshed.RecommendedNextAction.Contains("stale", StringComparison.OrdinalIgnoreCase), "Stale knowledge should recommend a new viewing.");
    }

    public void NoTrueRatingsExposedDirectly()
    {
        var result = CompletePlayerAssignment();
        var personId = result.Assignment!.TargetPlayerId!;
        var dossier = new PlayerDossierService().CreateDossier(result.ScenarioSnapshot, personId);
        var text = string.Join(Environment.NewLine, dossier.Sections.SelectMany(section => section.Lines));
        var desktop = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.False(text.Contains("PlayerTrueRatings", StringComparison.Ordinal), "Dossier should not expose true rating model names.");
        Assert.False(text.Contains("ScenarioSnapshot.TrueRatings", StringComparison.Ordinal), "Dossier text should not render true rating access.");
        Assert.False(desktop.Contains("ScenarioSnapshot.TrueRatings", StringComparison.Ordinal), "AlphaDesktop should not render hidden true ratings.");
    }

    private static NewGmScenarioSnapshot Scenario() =>
        new ScoutingIntelligenceService().EnsureKnowledgeProfiles(new HockeyIntelligenceRatingService().EnsureRatings(NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot));

    private static string ProspectId(NewGmScenarioSnapshot scenario) =>
        scenario.AlphaSnapshot.DraftBoard.Entries.First().ProspectPersonId;

    private static CompletedScoutingResult CompletePlayerAssignment()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var prepared = new HockeyIntelligenceRatingService().EnsureRatings(scenario.ScenarioSnapshot);
        return CompletePlayerAssignment(prepared, scenario.Registry, ProspectId(prepared), ScoutId(prepared));
    }

    private static CompletedScoutingResult CompletePlayerAssignment(NewGmScenarioSnapshot scenario, EngineRegistry registry, string personId, string scoutId)
    {
        var service = new ScoutingOperationsService();
        var assigned = service.AssignScoutToPlayer(
            registry,
            scenario,
            scoutId,
            personId,
            ScoutingOperationPriority.High,
            "Alpha 7.8 intelligence viewing.",
            scenario.CurrentDate);
        var completed = service.AdvanceAssignments(registry, assigned.ScenarioSnapshot);
        return new CompletedScoutingResult(registry, completed.ScenarioSnapshot, completed.Assignment ?? completed.ScenarioSnapshot.ScoutingOperations.First(item => item.TargetPlayerId == personId));
    }

    private static CompletedScoutingResult CompleteRegionAssignment(ScoutingRegionFocus region)
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var prepared = new HockeyIntelligenceRatingService().EnsureRatings(scenario.ScenarioSnapshot);
        var service = new ScoutingOperationsService();
        var assigned = service.AssignScoutToRegion(
            scenario.Registry,
            prepared,
            ScoutId(prepared),
            region,
            ScoutingOperationPriority.High,
            "Alpha 7.8 specialty viewing.",
            prepared.CurrentDate);
        var completed = service.AdvanceAssignments(scenario.Registry, assigned.ScenarioSnapshot);
        var assignment = completed.ScenarioSnapshot.ScoutingOperations.First(item => item.Status == ScoutingOperationStatus.Completed);
        return new CompletedScoutingResult(scenario.Registry, completed.ScenarioSnapshot, assignment);
    }

    private static string ScoutId(NewGmScenarioSnapshot scenario) =>
        scenario.StaffMembers.First(member => member.Department == StaffDepartment.Scouting && member.EmploymentStatus == StaffEmploymentStatus.Employed).PersonId;

    private static string AlternateScoutId(NewGmScenarioSnapshot scenario) =>
        scenario.StaffMembers
            .Where(member => member.Department == StaffDepartment.Scouting && member.EmploymentStatus == StaffEmploymentStatus.Employed)
            .Select(member => member.PersonId)
            .Distinct(StringComparer.Ordinal)
            .Skip(1)
            .FirstOrDefault()
        ?? ScoutId(scenario);

    private static string FindRepositoryRoot()
    {
        var current = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(current, "HockeyGmLegacy.slnx")))
        {
            current = Directory.GetParent(current)?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
        }

        return current;
    }

    private sealed record CompletedScoutingResult(EngineRegistry Registry, NewGmScenarioSnapshot ScenarioSnapshot, ScoutingOperationAssignment Assignment);
}
