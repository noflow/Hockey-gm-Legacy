using LegacyEngine.Contracts;
using LegacyEngine.Integration;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;
using LegacyEngine.Scouting;

internal sealed class Alpha54PlayerPipelineTests
{
    public void NhlDraftCreatesJuniorYouthProspects()
    {
        var scenario = ScenarioFor(LeagueExperience.Nhl, "org-seattle-cascades").ScenarioSnapshot;
        var juniorYouthCount = scenario.AlphaSnapshot.DraftBoard.Entries.Count(entry =>
            entry.Bio?.League.Contains("U18", StringComparison.OrdinalIgnoreCase) == true
            || entry.Bio?.League.Contains("High School", StringComparison.OrdinalIgnoreCase) == true
            || entry.Bio?.League.Contains("USHL", StringComparison.OrdinalIgnoreCase) == true
            || entry.Bio?.League.Contains("J18", StringComparison.OrdinalIgnoreCase) == true
            || entry.Bio?.League.Contains("U20", StringComparison.OrdinalIgnoreCase) == true);

        Assert.True(juniorYouthCount >= scenario.AlphaSnapshot.DraftBoard.Entries.Count / 2, "NHL draft board should mostly be junior/youth/international junior prospects.");
    }

    public void EighteenYearOldChlProspectCannotBeAssignedToAhl()
    {
        var ready = ScenarioWithProspect(Age: 18, League: "OHL", Status: ProspectStatus.Signed, DevelopmentLevel: PlayerDevelopmentLevel.Junior, IsChl: true);
        var prospect = ready.ScenarioSnapshot.ProspectRights[0];

        var result = new ProspectDecisionService().ApplyDecision(
            ready.Registry,
            ready.ScenarioSnapshot,
            new ProspectDecision(prospect.ProspectPersonId, ProspectDecisionType.AssignToAffiliate, ready.ScenarioSnapshot.CurrentDate));

        Assert.False(result.Success, "18-year-old CHL prospect should not be assignable to AHL.");
        Assert.True(result.Message.Contains("still CHL/junior eligible", StringComparison.OrdinalIgnoreCase), result.Message);
    }

    public void NineteenYearOldChlProspectCannotBeAssignedToAhlUnlessExceptionEnabled()
    {
        var ready = ScenarioWithProspect(Age: 19, League: "WHL", Status: ProspectStatus.Signed, DevelopmentLevel: PlayerDevelopmentLevel.Junior, IsChl: true);
        var prospect = ready.ScenarioSnapshot.ProspectRights[0];

        var blocked = new ProspectDecisionService().ApplyDecision(
            ready.Registry,
            ready.ScenarioSnapshot,
            new ProspectDecision(prospect.ProspectPersonId, ProspectDecisionType.AssignToAffiliate, ready.ScenarioSnapshot.CurrentDate));

        var exceptionRulebook = WithAssignmentRules(ready.Registry.Rulebook!, new PlayerAssignmentRules
        {
            JuniorAgeCutoff = 19,
            AhlEligibilityAge = 20,
            ChlToAhlRestrictionEnabled = true,
            OneNineteenYearOldChlExceptionEnabled = true,
            EuropeanAndCollegeProspectsCanPlayAhlAt18 = true,
            ElcSlideAgeCutoff = 19,
            ElcSlideNhlGameThreshold = 10
        });
        var allowed = new ProspectDecisionService().ApplyDecision(
            ready.Registry with { Rulebook = exceptionRulebook },
            ready.ScenarioSnapshot with { LeagueProfile = ready.ScenarioSnapshot.LeagueProfile with { Rulebook = exceptionRulebook } },
            new ProspectDecision(prospect.ProspectPersonId, ProspectDecisionType.AssignToAffiliate, ready.ScenarioSnapshot.CurrentDate));

        Assert.False(blocked.Success, "19-year-old CHL prospect should be blocked by default.");
        Assert.True(allowed.Success, allowed.Message);
    }

    public void TwentyYearOldProspectCanBeAssignedToAhlIfSigned()
    {
        var ready = ScenarioWithProspect(Age: 20, League: "WHL", Status: ProspectStatus.Signed, DevelopmentLevel: PlayerDevelopmentLevel.Junior, IsChl: true);
        var prospect = ready.ScenarioSnapshot.ProspectRights[0];

        var result = new ProspectDecisionService().ApplyDecision(
            ready.Registry,
            ready.ScenarioSnapshot,
            new ProspectDecision(prospect.ProspectPersonId, ProspectDecisionType.AssignToAffiliate, ready.ScenarioSnapshot.CurrentDate));

        Assert.True(result.Success, result.Message);
        Assert.Equal(ProspectStatus.AssignedToAffiliate, result.Prospect.Status);
    }

    public void EuropeanCollegePlaceholderProspectCanBeAssignedBasedOnRulebook()
    {
        var ready = ScenarioWithProspect(Age: 18, League: "J18 Nationell", Status: ProspectStatus.Signed, DevelopmentLevel: PlayerDevelopmentLevel.Europe, IsChl: false);
        var prospect = ready.ScenarioSnapshot.ProspectRights[0];

        var result = new ProspectDecisionService().ApplyDecision(
            ready.Registry,
            ready.ScenarioSnapshot,
            new ProspectDecision(prospect.ProspectPersonId, ProspectDecisionType.AssignToAffiliate, ready.ScenarioSnapshot.CurrentDate));

        Assert.True(result.Success, result.Message);
    }

    public void SignedEighteenNineteenYearOldHasElcSlideEligibility()
    {
        var ready = ScenarioWithProspect(Age: 18, League: "OHL", Status: ProspectStatus.Signed, DevelopmentLevel: PlayerDevelopmentLevel.Junior, IsChl: true);
        var prospect = ready.ScenarioSnapshot.ProspectRights[0];

        var eligibility = new PlayerPipelineService().EvaluateAssignment(ready.ScenarioSnapshot, prospect, ready.Registry.Rulebook);

        Assert.True(eligibility.IsSlideEligible, "Signed 18/19-year-old prospect should be ELC-slide eligible.");
        Assert.True(eligibility.SlideCanBeUsed, "Signed 18/19-year-old with no NHL games should be able to slide.");
    }

    public void TenNhlGamesPreventsSlide()
    {
        var ready = ScenarioWithProspect(Age: 18, League: "OHL", Status: ProspectStatus.Signed, DevelopmentLevel: PlayerDevelopmentLevel.Junior, IsChl: true);
        var prospect = ready.ScenarioSnapshot.ProspectRights[0];
        var scenario = ready.ScenarioSnapshot with
        {
            PlayerStats = new[] { new PlayerSeasonStatLine(prospect.ProspectPersonId, prospect.ProspectName, GamesPlayed: 10) }
        };

        var eligibility = new PlayerPipelineService().EvaluateAssignment(scenario, prospect, ready.Registry.Rulebook);

        Assert.True(eligibility.IsSlideEligible, "Age should still be slide eligible.");
        Assert.False(eligibility.SlideCanBeUsed, "10 NHL games should prevent slide.");
    }

    public void FewerThanTenNhlGamesAllowsSlide()
    {
        var ready = ScenarioWithProspect(Age: 19, League: "OHL", Status: ProspectStatus.Signed, DevelopmentLevel: PlayerDevelopmentLevel.Junior, IsChl: true);
        var prospect = ready.ScenarioSnapshot.ProspectRights[0];
        var scenario = ready.ScenarioSnapshot with
        {
            PlayerStats = new[] { new PlayerSeasonStatLine(prospect.ProspectPersonId, prospect.ProspectName, GamesPlayed: 9) }
        };

        var eligibility = new PlayerPipelineService().EvaluateAssignment(scenario, prospect, ready.Registry.Rulebook);

        Assert.True(eligibility.SlideCanBeUsed, "Fewer than 10 NHL games should allow slide.");
    }

    public void InvalidAssignmentGivesClearReason()
    {
        var ready = ScenarioWithProspect(Age: 18, League: "OHL", Status: ProspectStatus.Signed, DevelopmentLevel: PlayerDevelopmentLevel.Junior, IsChl: true);
        var prospect = ready.ScenarioSnapshot.ProspectRights[0];

        var eligibility = new PlayerPipelineService().EvaluateAssignment(ready.ScenarioSnapshot, prospect, ready.Registry.Rulebook);

        Assert.True(eligibility.InvalidReasons.Any(reason => reason.Contains("Cannot assign to AHL", StringComparison.OrdinalIgnoreCase)), "Invalid assignment should explain why.");
    }

    public void NhlTeamShowsAhlAffiliateRoster()
    {
        var ready = AssignTwentyYearOldToAhl();
        var affiliate = new PlayerPipelineService().AhlAffiliateRosterForNhlTeam(ready.ScenarioSnapshot);

        Assert.True(affiliate.Any(record => record.PipelineStatus == PlayerPipelineStatus.AssignedToAhl), "NHL pipeline should expose assigned AHL affiliate players.");
    }

    public void AhlTeamShowsAssignedProspects()
    {
        var ready = ScenarioFor(LeagueExperience.Ahl, "org-evergreen-comets").ScenarioSnapshot;
        var assigned = new PlayerPipelineService().ParentClubProspectsForAhlTeam(ready);

        Assert.True(assigned.Any(record => record.PipelineStatus == PlayerPipelineStatus.AhlRoster), "AHL team should show parent-assigned prospects/depth players.");
    }

    public void DossierShowsPipelineStatus()
    {
        var ready = ScenarioWithProspect(Age: 18, League: "OHL", Status: ProspectStatus.Signed, DevelopmentLevel: PlayerDevelopmentLevel.Junior, IsChl: true).ScenarioSnapshot;
        var prospect = ready.ProspectRights[0];
        var dossier = new PlayerDossierService().CreateDossier(ready, prospect.ProspectPersonId);
        var lines = dossier.Sections.SelectMany(section => section.Lines).ToArray();

        Assert.True(lines.Any(line => line.Contains("AHL eligibility", StringComparison.OrdinalIgnoreCase)), "Dossier should show AHL eligibility.");
        Assert.True(lines.Any(line => line.Contains("Contract slide status", StringComparison.OrdinalIgnoreCase)), "Dossier should show slide status.");
        Assert.True(lines.Any(line => line.Contains("Development path", StringComparison.OrdinalIgnoreCase)), "Dossier should show development path.");
    }

    public void SaveLoadPreservesPipelineStatus()
    {
        var ready = AssignTwentyYearOldToAhl();
        var path = Path.Combine(Path.GetTempPath(), $"hockey-gm-alpha54-{Guid.NewGuid():N}.json");
        try
        {
            var saved = new SaveGameService().SaveCareer(
                ready.ScenarioSnapshot,
                Array.Empty<InboxMessage>(),
                Array.Empty<LeagueTransaction>(),
                new Dictionary<string, ActionCenterStatus>(),
                new BudgetOverviewService().Build(ready.ScenarioSnapshot, ready.Registry.Rulebook!),
                path);
            var loaded = new SaveGameService().LoadFromFile(path, ready.Registry.Rulebook);

            Assert.True(saved.Success, saved.Message);
            Assert.True(loaded.Success, loaded.Message);
            Assert.True(loaded.SaveGame!.ScenarioSnapshot.PlayerPipeline.Any(record => record.PipelineStatus == PlayerPipelineStatus.AssignedToAhl), "Assigned pipeline status should survive save/load.");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    public void AlphaDesktopExposesPipelineFilters()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("AHL eligible", StringComparison.Ordinal), "Prospect list should expose AHL eligible filter/context.");
        Assert.True(source.Contains("Junior return candidates", StringComparison.Ordinal), "Prospect list should expose junior return filter/context.");
        Assert.True(source.Contains("Unsigned rights", StringComparison.Ordinal), "Prospect list should expose unsigned rights filter/context.");
        Assert.True(source.Contains("Slide eligible", StringComparison.Ordinal), "Prospect list should expose slide eligible filter/context.");
        Assert.True(source.Contains("NHL-ready", StringComparison.Ordinal), "Prospect list should expose NHL-ready filter/context.");
    }

    public void NoSalaryCapOrWaiversAdded()
    {
        var root = FindRepositoryRoot();
        var files = Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "PlayerPipeline*.cs", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "ProspectAssignment*.cs", SearchOption.TopDirectoryOnly))
            .Select(File.ReadAllText);
        var text = string.Join("\n", files);

        Assert.False(text.Contains("SalaryCap", StringComparison.Ordinal), "Alpha 5.4 should not add salary cap systems.");
        Assert.False(text.Contains("Waiver", StringComparison.Ordinal), "Alpha 5.4 should not add waivers.");
        Assert.False(text.Contains("Godot", StringComparison.Ordinal), "Alpha 5.4 should not add Godot.");
    }

    private static NewGmScenarioResult AssignTwentyYearOldToAhl()
    {
        var ready = ScenarioWithProspect(Age: 20, League: "WHL", Status: ProspectStatus.Signed, DevelopmentLevel: PlayerDevelopmentLevel.Junior, IsChl: true);
        var prospect = ready.ScenarioSnapshot.ProspectRights[0];
        var result = new ProspectDecisionService().ApplyDecision(
            ready.Registry,
            ready.ScenarioSnapshot,
            new ProspectDecision(prospect.ProspectPersonId, ProspectDecisionType.AssignToAffiliate, ready.ScenarioSnapshot.CurrentDate));
        return ready with { ScenarioSnapshot = result.ScenarioSnapshot, AlphaSnapshot = result.ScenarioSnapshot.AlphaSnapshot };
    }

    private static NewGmScenarioResult ScenarioWithProspect(int Age, string League, ProspectStatus Status, PlayerDevelopmentLevel DevelopmentLevel, bool IsChl)
    {
        var ready = ScenarioFor(LeagueExperience.Nhl, "org-seattle-cascades");
        var scenario = ready.ScenarioSnapshot;
        var entry = scenario.AlphaSnapshot.DraftBoard.Entries.First(item => item.Bio is not null);
        var person = scenario.AlphaSnapshot.People.First(person => person.PersonId == entry.ProspectPersonId);
        var prospect = new DraftRightsRecord(
            entry.ProspectPersonId,
            person.Identity.DisplayName,
            Age,
            entry.Bio!.Position,
            1,
            1,
            Status,
            entry.ProjectionText,
            entry.ScoutingConfidence ?? ScoutingConfidenceLevel.Medium,
            "Alpha 5.4 test prospect.",
            DevelopmentLevel,
            entry.Bio.CurrentTeam,
            League,
            IsChl);
        var updated = scenario with { ProspectRights = new[] { prospect } };
        updated = new PlayerPipelineService().EnsurePipeline(updated);
        updated = new PlayerPipelineService().UpsertProspect(updated, prospect, "Alpha 5.4 test prospect entered pipeline.");
        return ready with { ScenarioSnapshot = updated, AlphaSnapshot = updated.AlphaSnapshot };
    }

    private static NewGmScenarioResult ScenarioFor(LeagueExperience experience, string organizationId)
    {
        var service = new MultiLeagueCareerService();
        return service.CreateScenario(service.SelectLeagueAndTeam(experience, organizationId));
    }

    private static Rulebook WithAssignmentRules(Rulebook source, PlayerAssignmentRules assignmentRules) =>
        new()
        {
            RulebookId = source.RulebookId,
            LeagueType = source.LeagueType,
            Version = source.Version,
            RosterRules = source.RosterRules,
            EligibilityRules = source.EligibilityRules,
            ContractRules = source.ContractRules,
            DraftRules = source.DraftRules,
            PlayoffRules = source.PlayoffRules,
            BudgetRules = source.BudgetRules,
            SeasonRules = source.SeasonRules,
            StaffRules = source.StaffRules,
            AffiliateRules = source.AffiliateRules,
            PlayerAssignmentRules = assignmentRules
        };

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "HockeyGmLegacy.slnx")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new InvalidOperationException("Repository root could not be located.");
    }
}
