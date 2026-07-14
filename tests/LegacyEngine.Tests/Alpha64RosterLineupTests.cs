using LegacyEngine.Integration;
using LegacyEngine.Contracts;
using LegacyEngine.Injuries;
using LegacyEngine.RuleEngine;

public sealed class Alpha64RosterLineupTests
{
    public void NhlRosterGenerationIncludesTopLineForwards()
    {
        var lineup = CreateNhlScenario().ScenarioSnapshot.CurrentLineup!;

        var topForwards = lineup.Assignments.Count(assignment =>
            assignment.CurrentRole is LineupRole.FranchiseForward or LineupRole.FirstLineForward or LineupRole.TopSixForward);

        Assert.True(topForwards >= 3, "NHL lineups should include top-line/top-six forwards.");
    }

    public void NhlRosterGenerationIncludesTopPairDefensemen()
    {
        var lineup = CreateNhlScenario().ScenarioSnapshot.CurrentLineup!;

        Assert.True(lineup.Assignments.Any(assignment =>
            assignment.CurrentRole is LineupRole.FranchiseDefenseman or LineupRole.TopPairDefenseman), "NHL lineups should include at least one top-pair defense role.");
    }

    public void RebuildingTeamCompositionDiffersFromContender()
    {
        var scenario = CreateNhlScenario().ScenarioSnapshot;
        var service = new LineupService();
        var contender = service.BuildDefaultLineup(scenario with
        {
            TeamSelection = scenario.TeamSelection with { RosterQuality = "Elite", CurrentStrategy = "Win now contender" }
        }, scenario.Organization.OrganizationId, scenario.Organization.Name, scenario.AlphaSnapshot.Roster.ActivePlayers);
        var rebuilding = service.BuildDefaultLineup(scenario with
        {
            TeamSelection = scenario.TeamSelection with { RosterQuality = "Rebuilding", CurrentStrategy = "Rebuild through prospects" }
        }, scenario.Organization.OrganizationId, scenario.Organization.Name, scenario.AlphaSnapshot.Roster.ActivePlayers);

        var contenderFranchiseRoles = contender.Assignments.Count(assignment => assignment.CurrentRole is LineupRole.FranchiseForward or LineupRole.FranchiseDefenseman);
        var rebuildingFranchiseRoles = rebuilding.Assignments.Count(assignment => assignment.CurrentRole is LineupRole.FranchiseForward or LineupRole.FranchiseDefenseman);

        Assert.True(contenderFranchiseRoles > rebuildingFranchiseRoles, "Contender and rebuilding compositions should not assign identical top-end roles.");
    }

    public void DefaultLineupCreatesFourForwardLines()
    {
        var lineup = CreateNhlScenario().ScenarioSnapshot.CurrentLineup!;

        Assert.Equal(4, lineup.ForwardLines.Count);
    }

    public void DefaultLineupCreatesThreeDefensePairs()
    {
        var lineup = CreateNhlScenario().ScenarioSnapshot.CurrentLineup!;

        Assert.Equal(3, lineup.DefensePairs.Count);
    }

    public void DefaultLineupCreatesStarterAndBackup()
    {
        var lineup = CreateNhlScenario().ScenarioSnapshot.CurrentLineup!;

        Assert.True(lineup.Goalies.Starter is not null, "Lineup should include a starter.");
        Assert.True(lineup.Goalies.Backup is not null, "Lineup should include a backup.");
    }

    public void LineupViewExposesLineSlots()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("new(\"Lineup\", CreateSelectablePeopleContent(\"Lineup\"))", StringComparison.Ordinal), "Hockey Operations should expose the Lineup view.");
        Assert.True(source.Contains("Line 1", StringComparison.Ordinal), "Lineup view should expose forward line slots.");
        Assert.True(source.Contains("Pair 1", StringComparison.Ordinal), "Lineup view should expose defense pair slots.");
        Assert.True(source.Contains("Starter", StringComparison.Ordinal), "Lineup view should expose goalie depth.");
    }

    public void LineupViewUsesSelectableSlotsAndManageActions()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("\"Lineup\" => BuildLineupRows()", StringComparison.Ordinal), "Lineup should use selectable lineup-slot rows instead of the ordinary roster list.");
        Assert.True(source.Contains("Manage slot", StringComparison.Ordinal), "Each lineup slot should expose a direct management action.");
        Assert.True(source.Contains("Assign", StringComparison.Ordinal) && source.Contains("Swap", StringComparison.Ordinal), "Lineup detail should expose assignment and swap actions.");
    }

    public void RosterRowsShowLineupRole()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Current lineup role", StringComparison.Ordinal), "Roster detail should show current lineup role.");
        Assert.True(source.Contains("Potential lineup role", StringComparison.Ordinal), "Roster detail should show potential lineup role.");
        Assert.True(source.Contains("Current line/pair", StringComparison.Ordinal), "Roster detail should show current line or pair.");
        Assert.True(source.Contains("Promised role", StringComparison.Ordinal), "Roster detail should show promised lineup role.");
        Assert.True(source.Contains("Role satisfaction", StringComparison.Ordinal), "Roster detail should show role satisfaction.");
    }

    public void DevelopmentConsidersLineupRole()
    {
        var scenario = CreateNhlScenario().ScenarioSnapshot;
        var assignment = scenario.CurrentLineup!.Assignments.First();
        var impact = new LineupService().BuildDevelopmentImpact(scenario, assignment.PersonId);

        Assert.True(impact.Summary.Contains("development", StringComparison.OrdinalIgnoreCase), "Lineup role should produce development context.");
        Assert.True(impact.Modifier is >= -10 and <= 10, "Lineup development impact should remain modest.");
    }

    public void CoachRecommendationGenerated()
    {
        var lineup = CreateNhlScenario().ScenarioSnapshot.CurrentLineup!;

        Assert.True(lineup.CoachRecommendations.Count > 0, "Lineup should produce coach recommendations or usage notes.");
    }

    public void PlayerCanBeAssignedToLineOne()
    {
        var scenario = CreateNhlScenario().ScenarioSnapshot;
        var service = new LineupService();
        var removed = service.RemovePlayerFromSlot(scenario, LineupSlot.Line2LW).ScenarioSnapshot;
        var scratch = removed.CurrentLineup!.Assignments.First(assignment => assignment.Slot == LineupSlot.HealthyScratch && assignment.Position == LegacyEngine.Rosters.RosterPosition.LeftWing);

        var result = service.AssignPlayerToSlot(removed, LineupSlot.Line1LW, scratch.PersonId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(scratch.PersonId, result.ScenarioSnapshot.CurrentLineup!.Assignments.First(assignment => assignment.Slot == LineupSlot.Line1LW).PersonId);
    }

    public void PlayersCanBeSwappedBetweenSlots()
    {
        var scenario = CreateNhlScenario().ScenarioSnapshot;
        var first = scenario.CurrentLineup!.Assignments.First(assignment => assignment.Slot == LineupSlot.Line1LW);
        var second = scenario.CurrentLineup!.Assignments.First(assignment => assignment.Slot == LineupSlot.Line2LW);

        var result = new LineupService().SwapPlayers(scenario, LineupSlot.Line1LW, LineupSlot.Line2LW);

        Assert.True(result.Success, result.Message);
        Assert.Equal(second.PersonId, result.ScenarioSnapshot.CurrentLineup!.Assignments.First(assignment => assignment.Slot == LineupSlot.Line1LW).PersonId);
        Assert.Equal(first.PersonId, result.ScenarioSnapshot.CurrentLineup!.Assignments.First(assignment => assignment.Slot == LineupSlot.Line2LW).PersonId);
    }

    public void InvalidPositionWarningGenerated()
    {
        var scenario = CreateNhlScenario().ScenarioSnapshot;
        var goalie = scenario.CurrentLineup!.Assignments.First(assignment => assignment.Slot == LineupSlot.Starter);

        var result = new LineupService().AssignPlayerToSlot(scenario, LineupSlot.Line1LW, goalie.PersonId);

        Assert.False(result.Success, "Goalie should not be placed at LW.");
        Assert.True(result.Validation.Message.Contains("cannot be assigned", StringComparison.OrdinalIgnoreCase), result.Validation.Message);
    }

    public void DuplicateLineupWarningGenerated()
    {
        var scenario = CreateNhlScenario().ScenarioSnapshot;
        var winger = scenario.CurrentLineup!.Assignments.First(assignment => assignment.Slot == LineupSlot.Line1LW);

        var result = new LineupService().AssignPlayerToSlot(scenario, LineupSlot.Line2LW, winger.PersonId);

        Assert.False(result.Success, "Duplicate active lineup placement should be rejected.");
        Assert.True(result.Validation.Message.Contains("already assigned", StringComparison.OrdinalIgnoreCase), result.Validation.Message);
    }

    public void InjuredPlayerWarningGenerated()
    {
        var scenario = CreateNhlScenario().ScenarioSnapshot;
        var player = scenario.CurrentLineup!.Assignments.First(assignment => assignment.Slot == LineupSlot.Line2LW);
        var injury = new InjuryEngine().CreateInjury(
            player.PersonId,
            scenario.CurrentDate,
            InjuryBodyPart.Knee,
            InjuryType.Strain,
            InjurySeverity.Moderate).Injury;
        var scenarioWithInjury = scenario with
        {
            AlphaSnapshot = scenario.AlphaSnapshot with
            {
                Injuries = scenario.AlphaSnapshot.Injuries.Append(injury).ToArray()
            }
        };

        var result = new LineupService().AssignPlayerToSlot(scenarioWithInjury, LineupSlot.Line1LW, player.PersonId);

        Assert.False(result.Success, "Injured players should warn instead of being silently assigned.");
        Assert.True(result.Validation.Message.Contains("active injury", StringComparison.OrdinalIgnoreCase), result.Validation.Message);
    }

    public void RolePromiseStatusesCanBeEvaluated()
    {
        var scenario = CreateNhlScenario().ScenarioSnapshot;
        var service = new LineupService();
        var player = scenario.CurrentLineup!.Assignments.First(assignment => assignment.Slot == LineupSlot.Line1LW);

        var promised = service.SetRolePromise(scenario, player.PersonId, LineupRole.FirstLineForward, "test promise");
        var kept = promised;

        Assert.Equal(LineupPromiseStatus.Kept, kept.CurrentLineup!.Usage.First(usage => usage.PersonId == player.PersonId).PromiseStatus);

        var atRisk = service.SwapPlayers(kept, LineupSlot.Line1LW, LineupSlot.Line2LW).ScenarioSnapshot;
        Assert.Equal(LineupPromiseStatus.AtRisk, atRisk.CurrentLineup!.Usage.First(usage => usage.PersonId == player.PersonId).PromiseStatus);
    }

    public void BrokenPromiseAffectsRelationshipAndMorale()
    {
        var scenario = CreateNhlScenario().ScenarioSnapshot;
        var service = new LineupService();
        var player = scenario.CurrentLineup!.Assignments.First(assignment => assignment.Slot == LineupSlot.Line1LW);
        var promised = service.SetRolePromise(scenario, player.PersonId, LineupRole.FirstLineForward, "test promise");

        var result = service.RemovePlayerFromSlot(promised, LineupSlot.Line1LW);

        Assert.True(result.Success, result.Message);
        var usage = result.ScenarioSnapshot.CurrentLineup!.Usage.First(usage => usage.PersonId == player.PersonId);
        Assert.Equal(LineupPromiseStatus.Broken, usage.PromiseStatus);
        Assert.Equal(LineupRoleSatisfaction.VeryFrustrated, usage.Satisfaction);
        Assert.True(result.ScenarioSnapshot.RelationshipChangeHistory.Any(change => change.Trigger == RelationshipChangeTrigger.BrokenPromise), "Broken role promises should record relationship fallout.");
    }

    public void DevelopmentReportReferencesLineupUsage()
    {
        var scenario = CreateNhlScenario().ScenarioSnapshot;
        var assignment = scenario.CurrentLineup!.Assignments.First(assignment => assignment.Slot != LineupSlot.HealthyScratch);

        var impact = new LineupService().BuildDevelopmentImpact(scenario, assignment.PersonId);

        Assert.True(impact.Summary.Contains("line", StringComparison.OrdinalIgnoreCase) || impact.Summary.Contains("role", StringComparison.OrdinalIgnoreCase) || impact.Summary.Contains("development", StringComparison.OrdinalIgnoreCase), impact.Summary);
    }

    public void DossierExposesRoleUsageSection()
    {
        var scenario = CreateNhlScenario().ScenarioSnapshot;
        var player = scenario.AlphaSnapshot.Roster.ActivePlayers.First();

        var dossier = new PlayerDossierService().CreateDossier(scenario, player.PersonId);

        Assert.True(dossier.Sections.Any(section => section.Title == "Role / Usage"), "Player dossier should expose Role / Usage.");
    }

    public void ContractRolePromiseWarningGenerated()
    {
        var result = CreateNhlScenario();
        var scenario = result.ScenarioSnapshot;
        var player = scenario.AlphaSnapshot.Roster.ActivePlayers.First();
        var request = new ContractOfferBuildRequest(
            player.PersonId,
            ContractAskType.RosterPlayer,
            950_000m,
            1,
            "First line forward",
            "Development plan support",
            false,
            "Roster role",
            "Testing role fit warning",
            ContractType.JuniorPlayerAgreement);

        var evaluation = new ContractManagementService().BuildOffer(result.Registry, scenario, request);

        Assert.True(evaluation.RoleFitWarning.Contains("role promise", StringComparison.OrdinalIgnoreCase), evaluation.RoleFitWarning);
    }

    public void SaveLoadPreservesLineupState()
    {
        var result = CreateNhlScenario();
        var scenario = result.ScenarioSnapshot;
        var player = scenario.CurrentLineup!.Assignments.First(assignment => assignment.Slot == LineupSlot.Line1LW);
        scenario = new LineupService().SetRolePromise(scenario, player.PersonId, LineupRole.FirstLineForward, "save test");

        var save = new SaveGameService().CreateSave(
            scenario,
            Array.Empty<InboxMessage>(),
            Array.Empty<LeagueTransaction>(),
            new Dictionary<string, ActionCenterStatus>(),
            new BudgetOverviewService().Build(scenario, RulebookPresets.CreateNhlStyle()));

        Assert.True(save.ScenarioSnapshot.CurrentLineup?.RolePromises.Any(promise => promise.PersonId == player.PersonId) == true, "Save model should preserve lineup role promises.");
        Assert.True(save.ScenarioSnapshot.CurrentLineup?.Usage.Any(usage => usage.PersonId == player.PersonId) == true, "Save model should preserve lineup usage.");
    }

    public void TradeTeamBrowserExposesLineupRole()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("TradeTargetType", StringComparison.Ordinal), "Trade/team browser should expose trade target type.");
        Assert.True(source.Contains("TradePotentialRole", StringComparison.Ordinal), "Trade/team browser should expose potential role.");
    }

    public void NoHiddenRatingsExposed()
    {
        var lineup = CreateNhlScenario().ScenarioSnapshot.CurrentLineup!;
        var rendered = string.Join(Environment.NewLine, lineup.Assignments.Select(assignment =>
            $"{assignment.PlayerName} {LineupDisplay.Role(assignment.CurrentRole)} {LineupDisplay.Role(assignment.PotentialRole)} {assignment.SlotLabel}"));

        Assert.False(rendered.Contains("CurrentAbility", StringComparison.OrdinalIgnoreCase), "Lineup output should not expose hidden current ability.");
        Assert.False(rendered.Contains("Potential =", StringComparison.OrdinalIgnoreCase), "Lineup output should not expose hidden potential ratings.");
        Assert.False(rendered.Contains("hidden rating", StringComparison.OrdinalIgnoreCase), "Lineup output should not expose hidden ratings.");
    }

    public void NoFullTacticalSimulationAdded()
    {
        var text = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "engine", "LegacyEngine", "Integration", "LineupService.cs"));

        Assert.False(text.Contains("SpecialTeams", StringComparison.OrdinalIgnoreCase), "Alpha 6.4 should not add special teams.");
        Assert.False(text.Contains("PowerPlay", StringComparison.OrdinalIgnoreCase), "Alpha 6.4 should not add power play logic.");
        Assert.False(text.Contains("PenaltyKill", StringComparison.OrdinalIgnoreCase), "Alpha 6.4 should not add penalty kill logic.");
        Assert.False(text.Contains("LineChemistry", StringComparison.OrdinalIgnoreCase), "Alpha 6.4 should not add a line chemistry engine.");
        Assert.False(text.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Alpha 6.4 should not add Godot.");
    }

    private static NewGmScenarioResult CreateNhlScenario()
    {
        var service = new MultiLeagueCareerService();
        return service.CreateScenario(service.SelectLeagueAndTeam(LeagueExperience.Nhl, "org-seattle-cascades"));
    }

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
