using LegacyEngine.Integration;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;

internal sealed class RosterMovementOrganizationTests
{
    public void DepthChartGroupsOrganizationPlayersByPosition()
    {
        var scenario = CreateNhlScenario().ScenarioSnapshot;
        var chart = new RosterMovementService().BuildDepthChart(scenario, RulebookPresets.CreateNhlStyle());

        Assert.True(chart.TotalPlayers >= 40, "An established NHL organization should show active players, affiliate depth, and rights in one depth chart.");
        Assert.True(chart.Group(RosterPosition.Center).Players.Count > 0, "Depth chart should expose centers.");
        Assert.True(chart.Group(RosterPosition.Defense).Players.Count > 0, "Depth chart should expose defensemen.");
        Assert.True(chart.Group(RosterPosition.Goalie).Players.Count > 0, "Depth chart should expose goalies.");
        Assert.True(chart.Groups.SelectMany(group => group.Players).GroupBy(player => player.PersonId).All(group => group.Count() == 1), "A person should appear once in the organization depth map.");
    }

    public void DepthChartShowsLineupAndOrganizationContext()
    {
        var scenario = CreateNhlScenario().ScenarioSnapshot;
        var chart = new RosterMovementService().BuildDepthChart(scenario, RulebookPresets.CreateNhlStyle());
        var active = chart.Groups.SelectMany(group => group.Players).First(player => player.Group == OrganizationRosterGroup.NhlActiveRoster);
        var affiliate = chart.Groups.SelectMany(group => group.Players).First(player => player.Group == OrganizationRosterGroup.AhlAffiliateRoster);

        Assert.True(!string.IsNullOrWhiteSpace(active.LineupSlot), "Active players should show lineup slot context.");
        Assert.True(active.ContractSummary.Contains("contract", StringComparison.OrdinalIgnoreCase), "Depth rows should show contract context.");
        Assert.Equal("AHL", affiliate.Level);
        Assert.True(affiliate.Team.Length > 0, "Affiliate players should show their current team.");
    }

    public void ActivePlayerCanMoveToReserveAndIsRemovedFromLineup()
    {
        var created = CreateNhlScenario();
        var scenario = created.ScenarioSnapshot;
        var player = scenario.AlphaSnapshot.Roster.ActivePlayers.First(player => scenario.CurrentLineup!.Assignments.Any(assignment => assignment.PersonId == player.PersonId && assignment.Slot != LineupSlot.HealthyScratch));

        var result = new RosterMovementService().Move(created.Registry, scenario, player.PersonId, RosterMovementType.Reserve);

        Assert.True(result.Success, result.Message);
        Assert.Equal(RosterStatus.Reserve, result.ScenarioSnapshot.AlphaSnapshot.Roster.FindPlayer(player.PersonId)!.Status);
        Assert.False(result.ScenarioSnapshot.CurrentLineup!.Assignments.Any(assignment => assignment.PersonId == player.PersonId && assignment.Slot != LineupSlot.HealthyScratch), "A reserved player should not remain in an active lineup slot.");
        Assert.True(result.ScenarioSnapshot.OrganizationRoster!.Players.Any(item => item.PersonId == player.PersonId), "Roster movement should refresh the organization allocation.");
    }

    public void ActivePlayerMovementOptionsExplainValidAndInvalidActions()
    {
        var scenario = CreateNhlScenario().ScenarioSnapshot;
        var player = scenario.AlphaSnapshot.Roster.ActivePlayers.First(player => player.Position is RosterPosition.Center or RosterPosition.LeftWing or RosterPosition.RightWing);
        var options = new RosterMovementService().BuildMovementOptions(scenario, player.PersonId, RulebookPresets.CreateNhlStyle());

        Assert.True(options.Any(option => option.MovementType == RosterMovementType.Reserve && option.IsAvailable), "Active players should be movable to reserve.");
        Assert.True(options.Any(option => option.MovementType == RosterMovementType.Release && !option.IsAvailable && option.Reason.Contains("minimum", StringComparison.OrdinalIgnoreCase)), "Release should explain when the rulebook requires the roster minimum to be maintained.");
        Assert.True(options.Any(option => option.MovementType == RosterMovementType.Activate && !option.IsAvailable && option.Reason.Contains("already", StringComparison.OrdinalIgnoreCase)), "The active state should explain why Move to Active is disabled.");
    }

    public void DesktopExposesRosterDepthAndCoreMovementActions()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Roster & Depth", StringComparison.Ordinal), "Hockey Operations should expose a roster depth view.");
        Assert.True(source.Contains("Move to Active", StringComparison.Ordinal), "Roster detail should expose an active-roster movement action.");
        Assert.True(source.Contains("Move to Reserve", StringComparison.Ordinal), "Roster detail should expose a reserve movement action.");
        Assert.True(source.Contains("Place on Injured Reserve", StringComparison.Ordinal), "Roster detail should expose an injured-reserve movement action.");
        Assert.True(source.Contains("RosterMovementService", StringComparison.Ordinal), "Desktop should route roster movement through the engine service.");
    }

    public void DesktopExposesNhlAhlMovementWorkspace()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("NHL / AHL Movement", StringComparison.Ordinal), "Hockey Operations should expose a dedicated NHL/AHL movement view.");
        Assert.True(source.Contains("Open NHL / AHL Movement", StringComparison.Ordinal), "Roster depth should link directly to NHL/AHL movement.");
        Assert.True(source.Contains("Send to AHL", StringComparison.Ordinal), "Selected NHL players should expose a send-down action.");
        Assert.True(source.Contains("Call Up from AHL", StringComparison.Ordinal), "Selected AHL players should expose a call-up action.");
        Assert.True(source.Contains("Place on Waivers", StringComparison.Ordinal), "Waiver-required send-downs should be visible as a separate action.");
    }

    private static NewGmScenarioResult CreateNhlScenario()
    {
        var careers = new MultiLeagueCareerService();
        var team = careers.TeamsFor(LeagueExperience.Nhl).First();
        return careers.CreateScenario(careers.SelectLeagueAndTeam(LeagueExperience.Nhl, team.OrganizationId));
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

        throw new DirectoryNotFoundException("Could not find Hockey GM Legacy repository root.");
    }
}
