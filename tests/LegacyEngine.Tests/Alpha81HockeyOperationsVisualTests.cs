internal sealed class Alpha81HockeyOperationsVisualTests
{
    public void HockeyOperationsUsesIntegratedThreePanelLayout()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("CreateHockeyOperationsCommandCenter", StringComparison.Ordinal), "Hockey Operations should keep a dedicated command center.");
        Assert.True(source.Contains("\"NHL Roster\"", StringComparison.Ordinal), "Left panel should expose NHL roster source.");
        Assert.True(source.Contains("\"AHL Roster\"", StringComparison.Ordinal), "Left panel should expose AHL roster source.");
        Assert.True(source.Contains("\"Junior / Returned Prospects\"", StringComparison.Ordinal), "Left panel should expose returned prospect source.");
        Assert.True(source.Contains("\"Unsigned Rights\"", StringComparison.Ordinal), "Left panel should expose unsigned rights source.");
        Assert.True(source.Contains("\"Injured Players\"", StringComparison.Ordinal), "Left panel should expose injured player source.");
        Assert.True(source.Contains("\"Waiver Wire\"", StringComparison.Ordinal), "Left panel should expose waiver wire source.");
        Assert.True(source.Contains("BuildHockeyOperationsTeamSummaryStrip", StringComparison.Ordinal), "Center panel should start with the team summary strip.");
        Assert.True(source.Contains("RenderCommandCenterPlayerCard", StringComparison.Ordinal), "Right panel should remain the selected player card.");
    }

    public void RosterRowsShowReadableHockeyContext()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("BuildHockeyOperationsPlayerCard", StringComparison.Ordinal), "Roster overview should use player cards.");
        Assert.True(source.Contains("UiPersonLink(row.Name", StringComparison.Ordinal), "Player names should be clickable.");
        Assert.True(source.Contains("State.RatingText(row.PersonId)", StringComparison.Ordinal), "Rows/cards should show visible OVR/POT.");
        Assert.True(source.Contains("State.CurrentLineupRole(row.PersonId)", StringComparison.Ordinal), "Rows/cards should show current role.");
        Assert.True(source.Contains("State.CurrentLinePair(row.PersonId)", StringComparison.Ordinal), "Rows/cards should show current line/pair.");
        Assert.True(source.Contains("State.ContractRightsStatus(row.PersonId)", StringComparison.Ordinal), "Rows/cards should show contract or rights status.");
        Assert.True(source.Contains("State.InjuryStatus(row.PersonId)", StringComparison.Ordinal), "Rows/cards should show health/status.");
        Assert.True(source.Contains("_commandCenterPositionFilter", StringComparison.Ordinal), "Hockey Operations should support a position filter.");
    }

    public void LineupBoardShowsForwardDefenseAndGoalieSlots()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("BuildHockeyOperationsLineupBoard", StringComparison.Ordinal), "Lineup should render as a board.");
        Assert.True(source.Contains("Line 1", StringComparison.Ordinal), "Lineup board should create forward line 1.");
        Assert.True(source.Contains("Line 4", StringComparison.Ordinal), "Lineup board should create forward line 4.");
        Assert.True(source.Contains("Pair 1", StringComparison.Ordinal), "Lineup board should create defense pair 1.");
        Assert.True(source.Contains("Pair 3", StringComparison.Ordinal), "Lineup board should create defense pair 3.");
        Assert.True(source.Contains("LineupSlot.Starter", StringComparison.Ordinal), "Goalie starter slot should appear.");
        Assert.True(source.Contains("LineupSlot.Backup", StringComparison.Ordinal), "Goalie backup slot should appear.");
        Assert.True(source.Contains("Chemistry", StringComparison.Ordinal), "Chemistry grades should be visible beside lines/pairs.");
    }

    public void GroupedOperationsViewsExist()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("BuildHockeyOperationsDepthChartBoard", StringComparison.Ordinal), "Depth chart view should exist.");
        Assert.True(source.Contains("BuildHockeyOperationsProspectPipeline", StringComparison.Ordinal), "Prospect pipeline should exist.");
        Assert.True(source.Contains("\"NHL Ready\"", StringComparison.Ordinal), "Prospect pipeline should group NHL-ready players.");
        Assert.True(source.Contains("\"Unsigned Rights\"", StringComparison.Ordinal), "Prospect pipeline should group unsigned rights.");
        Assert.True(source.Contains("BuildHockeyOperationsDevelopmentBoard", StringComparison.Ordinal), "Development view should exist.");
        Assert.True(source.Contains("\"Biggest Risers\"", StringComparison.Ordinal), "Development view should show risers.");
        Assert.True(source.Contains("\"Plateau / Risk Watch\"", StringComparison.Ordinal), "Development view should show risk watch.");
        Assert.True(source.Contains("BuildHockeyOperationsContractBoard", StringComparison.Ordinal), "Contract view should exist.");
        Assert.True(source.Contains("\"Expiring This Year\"", StringComparison.Ordinal), "Contract view should group expiring players.");
        Assert.True(source.Contains("\"RFA\"", StringComparison.Ordinal), "Contract view should group RFAs.");
        Assert.True(source.Contains("\"UFA\"", StringComparison.Ordinal), "Contract view should group UFAs.");
    }

    public void ScoutingTransactionsSpecialTeamsAndTacticsAreVisual()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("BuildHockeyOperationsScoutingBoard", StringComparison.Ordinal), "Scouting view should be visual.");
        Assert.True(source.Contains("\"Needs Another Look\"", StringComparison.Ordinal), "Scouting view should show low-confidence work.");
        Assert.True(source.Contains("BuildHockeyOperationsTransactionBoard", StringComparison.Ordinal), "Roster transactions view should exist.");
        Assert.True(source.Contains("Waiver / Assignment Check", StringComparison.Ordinal), "Transactions should explain waiver/assignment context.");
        Assert.True(source.Contains("BuildHockeyOperationsSpecialTeamsBoard", StringComparison.Ordinal), "Special teams board should exist.");
        Assert.True(source.Contains("PP{unit.UnitNumber}", StringComparison.Ordinal), "Power play cards should be present.");
        Assert.True(source.Contains("PK{unit.UnitNumber}", StringComparison.Ordinal), "Penalty kill cards should be present.");
        Assert.True(source.Contains("BuildHockeyOperationsTacticsBoard", StringComparison.Ordinal), "Tactics should be grouped visually.");
        Assert.True(source.Contains("\"Attack\"", StringComparison.Ordinal), "Tactics should include attack grouping.");
        Assert.True(source.Contains("\"Transition\"", StringComparison.Ordinal), "Tactics should include transition grouping.");
        Assert.True(source.Contains("\"Defense\"", StringComparison.Ordinal), "Tactics should include defense grouping.");
    }

    public void SelectedPlayerCardUsesCompactSectionsAndNoHiddenTruth()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("BuildCommandCenterSection(\"Ratings\"", StringComparison.Ordinal), "Right card should include a compact ratings section.");
        Assert.True(source.Contains("BuildCommandCenterSection(\"Development\"", StringComparison.Ordinal), "Right card should include development section.");
        Assert.True(source.Contains("BuildCommandCenterSection(\"Contract\"", StringComparison.Ordinal), "Right card should include contract section.");
        Assert.True(source.Contains("BuildCommandCenterSection(\"Usage\"", StringComparison.Ordinal), "Right card should include usage section.");
        Assert.True(source.Contains("BuildCommandCenterSection(\"Scouting\"", StringComparison.Ordinal), "Right card should include scouting section.");
        Assert.True(source.Contains("BuildCommandCenterSection(\"Medical\"", StringComparison.Ordinal), "Right card should include medical section.");
        Assert.True(source.Contains("BuildCommandCenterSection(\"Relationships\"", StringComparison.Ordinal), "Right card should include relationships section.");
        Assert.True(source.Contains("BuildCommandCenterSection(\"Career\"", StringComparison.Ordinal), "Right card should include career section.");
        Assert.False(source.Contains("TrueOverall", StringComparison.Ordinal), "Hockey Operations should not render true overall ratings.");
        Assert.False(source.Contains("TruePotential", StringComparison.Ordinal), "Hockey Operations should not render true potential ratings.");
    }

    private static string AlphaDesktopSource() =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

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
