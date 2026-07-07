using LegacyEngine.Integration;

internal sealed class Alpha51FunctionalUiTradeWindowTests
{
    public void PlayerDossierAndStaffProfileUsePopupFramework()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("private void ShowPopup(", StringComparison.Ordinal), "AlphaDesktop should have a reusable popup helper.");
        Assert.True(source.Contains("ShowPopup($\"Player Dossier -", StringComparison.Ordinal), "Player dossier should open through the popup helper.");
        Assert.True(source.Contains("ShowPopup($\"Staff Profile -", StringComparison.Ordinal), "Staff profile should open through the popup helper.");
        Assert.True(source.Contains("ShowConfirmationPopup(", StringComparison.Ordinal), "Confirmation popup helper should be present.");
        Assert.True(source.Contains("ShowContractOfferPlaceholder(", StringComparison.Ordinal), "Contract Offer popup placeholder should be present.");
        Assert.True(source.Contains("Title = \"Scouting Assignment\"", StringComparison.Ordinal), "Scouting assignment popup should have a clear title.");
    }

    public void LeagueWorkspaceExposesFunctionalTeamAreas()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("new WorkspaceScreen(\"Teams\", CreateSelectablePeopleContent(\"Teams\"))", StringComparison.Ordinal), "Teams should be selectable.");
        Assert.True(source.Contains("new WorkspaceScreen(\"Transactions\", CreateTextScreen(\"Transactions\"))", StringComparison.Ordinal), "League transactions should be exposed.");
        Assert.True(source.Contains("new WorkspaceScreen(\"League Free Agents\", CreateSelectablePeopleContent(\"League Free Agents\"))", StringComparison.Ordinal), "League free agents should be exposed.");
        Assert.True(source.Contains("new WorkspaceScreen(\"League Trade Block\", CreateSelectablePeopleContent(\"League Trade Block\"))", StringComparison.Ordinal), "League trade block should be exposed.");
        Assert.True(source.Contains("new WorkspaceScreen(\"League Standings\", CreateTextScreen(\"League Standings\"))", StringComparison.Ordinal), "League standings shortcut should be exposed.");
    }

    public void OrganizationTeamPopupShowsSelectableRoster()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("ShowOrganizationPopup(", StringComparison.Ordinal), "Organization/team popup should exist.");
        Assert.True(source.Contains("BuildOtherTeamPlayerDetail(", StringComparison.Ordinal), "Other-team player detail panel should exist.");
        Assert.True(source.Contains("State.OtherTeamTradeRoster(team.OrganizationId)", StringComparison.Ordinal), "Other-team roster should be visible.");
        Assert.True(source.Contains("\"Add to Trade Proposal\"", StringComparison.Ordinal), "Other-team player action should add to trade proposal.");
        Assert.True(source.Contains("\"Add to Watchlist\"", StringComparison.Ordinal), "Watchlist placeholder should be present.");
    }

    public void TradeBuilderPopupShowsAssetsAndImpacts()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("ShowTradeBuilderPopup(", StringComparison.Ordinal), "Trade builder popup should exist.");
        Assert.True(source.Contains("BuildTradeBuilderPopupContent(", StringComparison.Ordinal), "Trade builder content builder should exist.");
        Assert.True(source.Contains("CreateDetailPanel(\"Your assets\"", StringComparison.Ordinal), "Trade builder should show player assets.");
        Assert.True(source.Contains("CreateDetailPanel(\"Trade proposal\"", StringComparison.Ordinal), "Trade builder should show proposal column.");
        Assert.True(source.Contains("CreateDetailPanel(\"Other team assets\"", StringComparison.Ordinal), "Trade builder should show other team assets.");
        Assert.True(source.Contains("\"Roster impact\"", StringComparison.Ordinal), "Trade builder should show roster impact.");
        Assert.True(source.Contains("\"Budget impact\"", StringComparison.Ordinal), "Trade builder should show budget impact.");
        Assert.True(source.Contains("\"AI evaluation\"", StringComparison.Ordinal), "Trade builder should show AI evaluation.");
        Assert.True(source.Contains("State.ProposeTradeFor(entry.PersonId)", StringComparison.Ordinal), "Trade builder should propose through existing state/trade service path.");
    }

    public void NoPopupTopLevelTabDuplication()
    {
        var source = AlphaDesktopSource();

        Assert.False(source.Contains("AddWorkspaceTab(tabs, \"Player Dossier\"", StringComparison.Ordinal), "Player dossier popup should not be a top-level tab.");
        Assert.False(source.Contains("AddWorkspaceTab(tabs, \"Trade Builder\"", StringComparison.Ordinal), "Trade builder popup should not be a top-level tab.");
        Assert.False(source.Contains("AddWorkspaceTab(tabs, \"Staff Profile\"", StringComparison.Ordinal), "Staff profile popup should not be a top-level tab.");
    }

    public void AcceptedTradeStillCreatesPendingGmAction()
    {
        var scenario = new MultiLeagueCareerService()
            .CreateScenario(new MultiLeagueCareerService().SelectLeagueAndTeam(LeagueExperience.Junior, "org-prairie-falcons"));
        var service = new TradeService();
        var blockEntry = scenario.ScenarioSnapshot.TradeBlock!.Entries.OrderBy(entry => entry.AssetValue).First();
        var outgoingPlayers = scenario.ScenarioSnapshot.AlphaSnapshot.Roster.ActivePlayers.Take(5)
            .Select(player => service.CreateRosterPlayerAsset(scenario.ScenarioSnapshot, player.PersonId))
            .ToArray();
        var incoming = service.CreateRosterPlayerAsset(scenario.ScenarioSnapshot, blockEntry.PersonId, TradeSide.OtherOrganization);
        var offer = service.CreateOffer(
            scenario.ScenarioSnapshot,
            blockEntry.OrganizationId,
            blockEntry.TeamName,
            outgoingPlayers,
            new[] { incoming });

        var result = service.ProposeTrade(scenario.Registry, scenario.ScenarioSnapshot, offer);

        Assert.True(result.Success, result.Message);
        Assert.Equal(TradeOfferStatus.Accepted, result.TradeOffer!.Status);
        Assert.True(result.ScenarioSnapshot.PendingActions.Any(action => action.ActionType == PendingGmActionType.ApproveTrade && action.IsOpen), "Accepted trade should create a pending GM approval.");
        Assert.True(result.ScenarioSnapshot.AlphaSnapshot.Roster.FindPlayer(blockEntry.PersonId) is null, "Accepted trade should not add the incoming player to roster before approval.");
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
