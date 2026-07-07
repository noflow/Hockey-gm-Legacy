using LegacyEngine.Integration;
using LegacyEngine.Rosters;
using LegacyEngine.Scouting;

internal sealed class Alpha32TradeEngineTests
{
    public void TradeBlockGenerated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;

        Assert.True(scenario.TradeBlock is not null, "Scenario should include a league trade block.");
        Assert.True(scenario.TradeBlock!.Entries.Count >= 20, "Trade block should include useful league options.");
        Assert.True(scenario.TradeBlock.Entries.All(entry => entry.Position != RosterPosition.Unknown), "Trade block rows should include known positions.");
    }

    public void TradeOfferCanIncludePlayerAsset()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var service = new TradeService();
        var player = scenario.AlphaSnapshot.Roster.ActivePlayers[0];
        var asset = service.CreateRosterPlayerAsset(scenario, player.PersonId);

        Assert.Equal(TradeAssetType.Player, asset.AssetType);
        Assert.Equal(TradeSide.PlayerOrganization, asset.Side);
        Assert.True(asset.Value > 0, "Player trade asset should have value.");
    }

    public void TradeOfferCanIncludeProspectRights()
    {
        var scenario = WithProspectRights(NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot);
        var asset = new TradeService().CreateProspectRightsAsset(scenario, scenario.ProspectRights[0].ProspectPersonId);

        Assert.Equal(TradeAssetType.ProspectRights, asset.AssetType);
        Assert.True(asset.Value > 0, "Prospect rights asset should have placeholder value.");
    }

    public void TradeOfferCanIncludeDraftPickPlaceholder()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var asset = new TradeService().CreateDraftPickAsset(scenario, TradeSide.PlayerOrganization, scenario.Organization.OrganizationId, scenario.Organization.Name, 2, scenario.Season.Year + 1);

        Assert.Equal(TradeAssetType.DraftPick, asset.AssetType);
        Assert.True(asset.DisplayName.Contains("Round 2", StringComparison.Ordinal), "Draft pick placeholder should describe round.");
    }

    public void InvalidEmptyOfferRejected()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var block = created.ScenarioSnapshot.TradeBlock!.Entries[0];
        var offer = new TradeService().CreateOffer(created.ScenarioSnapshot, block.OrganizationId, block.TeamName, Array.Empty<TradeAsset>(), Array.Empty<TradeAsset>());
        var result = new TradeService().ProposeTrade(created.Registry, created.ScenarioSnapshot, offer);

        Assert.False(result.Success, "Empty trade should fail validation.");
        Assert.Equal(TradeOfferStatus.FailedValidation, result.TradeOffer!.Status);
    }

    public void CannotTradeAssetNotOwnedByTeam()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var block = created.ScenarioSnapshot.TradeBlock!.Entries[0];
        var fake = new TradeAsset(TradeAssetType.Player, TradeSide.PlayerOrganization, created.ScenarioSnapshot.Organization.OrganizationId, created.ScenarioSnapshot.Organization.Name, "not-owned", "Not Owned", RosterPosition.Center, 18, 0, 40);
        var receive = new TradeService().CreateRosterPlayerAsset(created.ScenarioSnapshot, block.PersonId, TradeSide.OtherOrganization);
        var offer = new TradeService().CreateOffer(created.ScenarioSnapshot, block.OrganizationId, block.TeamName, new[] { fake }, new[] { receive });
        var result = new TradeService().ProposeTrade(created.Registry, created.ScenarioSnapshot, offer);

        Assert.False(result.Success, "Cannot trade a player not on the roster.");
        Assert.Equal(TradeOfferStatus.FailedValidation, result.TradeOffer!.Status);
    }

    public void AiAcceptsFairOffer()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var offer = BuildAcceptedOffer(created.ScenarioSnapshot);
        var result = new TradeService().ProposeTrade(created.Registry, created.ScenarioSnapshot, offer);

        Assert.True(result.Success, "Fair offer should be processed.");
        Assert.Equal(TradeOfferStatus.Accepted, result.TradeOffer!.Status);
    }

    public void AiRejectsPoorOffer()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var scenario = created.ScenarioSnapshot;
        var target = scenario.TradeBlock!.Entries.OrderByDescending(entry => entry.AssetValue).First();
        var service = new TradeService();
        var give = service.CreateDraftPickAsset(scenario, TradeSide.PlayerOrganization, scenario.Organization.OrganizationId, scenario.Organization.Name, 7, scenario.Season.Year + 1);
        var receive = service.CreateRosterPlayerAsset(scenario, target.PersonId, TradeSide.OtherOrganization);
        var offer = service.CreateOffer(scenario, target.OrganizationId, target.TeamName, new[] { give }, new[] { receive });
        var result = service.ProposeTrade(created.Registry, scenario, offer);

        Assert.True(result.Success, "Poor offer should still receive an AI answer.");
        Assert.Equal(TradeOfferStatus.Rejected, result.TradeOffer!.Status);
    }

    public void AcceptedTradeCreatesPendingGmAction()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var result = new TradeService().ProposeTrade(created.Registry, created.ScenarioSnapshot, BuildAcceptedOffer(created.ScenarioSnapshot));

        Assert.True(result.ScenarioSnapshot.PendingActions.Any(action => action.ActionType == PendingGmActionType.ApproveTrade && action.IsOpen), "Accepted trade should create pending GM approval.");
    }

    public void ApprovedTradeMovesAssets()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var offer = BuildAcceptedOffer(created.ScenarioSnapshot);
        var proposed = new TradeService().ProposeTrade(created.Registry, created.ScenarioSnapshot, offer).ScenarioSnapshot;
        var action = proposed.PendingActions.Single(action => action.ActionType == PendingGmActionType.ApproveTrade && action.IsOpen);
        var approved = new PendingGmActionService().Approve(created.Registry, proposed, action.ActionId);

        Assert.True(approved.Success, "Trade approval should succeed.");
        Assert.False(approved.ScenarioSnapshot.AlphaSnapshot.Roster.Players.Any(player => player.PersonId == offer.PlayerGives[0].AssetId), "Outgoing roster player should leave roster.");
        Assert.True(approved.ScenarioSnapshot.AlphaSnapshot.Roster.Players.Any(player => player.PersonId == offer.PlayerReceives[0].AssetId), "Incoming player should join roster.");
        Assert.True(approved.ScenarioSnapshot.TradeOffers.Any(item => item.TradeOfferId == offer.TradeOfferId && item.Status == TradeOfferStatus.Completed), "Trade should be marked completed.");
    }

    public void DeclinedTradeDoesNotMoveAssets()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var offer = BuildAcceptedOffer(created.ScenarioSnapshot);
        var proposed = new TradeService().ProposeTrade(created.Registry, created.ScenarioSnapshot, offer).ScenarioSnapshot;
        var action = proposed.PendingActions.Single(action => action.ActionType == PendingGmActionType.ApproveTrade && action.IsOpen);
        var declined = new PendingGmActionService().Decline(created.Registry, proposed, action.ActionId);

        Assert.True(declined.Success, "Trade decline should succeed.");
        Assert.True(declined.ScenarioSnapshot.AlphaSnapshot.Roster.Players.Any(player => player.PersonId == offer.PlayerGives[0].AssetId), "Outgoing player should remain after decline.");
        Assert.False(declined.ScenarioSnapshot.AlphaSnapshot.Roster.Players.Any(player => player.PersonId == offer.PlayerReceives[0].AssetId), "Incoming player should not join after decline.");
    }

    public void BudgetAndRosterImpactCalculated()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var result = new TradeService().ProposeTrade(created.Registry, created.ScenarioSnapshot, BuildAcceptedOffer(created.ScenarioSnapshot));

        Assert.True(result.Evaluation is not null, "Trade evaluation should be returned.");
        Assert.True(result.Evaluation!.Reasons.Count > 0, "Trade evaluation should explain its reasons.");
        Assert.Equal(0, result.Evaluation.RosterImpact);
    }

    public void LeagueNewsRecordsCompletedTrade()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var offer = BuildAcceptedOffer(created.ScenarioSnapshot);
        var proposed = new TradeService().ProposeTrade(created.Registry, created.ScenarioSnapshot, offer).ScenarioSnapshot;
        var action = proposed.PendingActions.Single(action => action.ActionType == PendingGmActionType.ApproveTrade && action.IsOpen);
        var approved = new PendingGmActionService().Approve(created.Registry, proposed, action.ActionId);

        Assert.True(approved.LeagueTransactions?.Any(transaction => transaction.TransactionType == LeagueTransactionType.TradeCompleted) == true, "Completed trade should create league news transaction.");
    }

    public void InboxRecordsPlayerTeamTradeEvents()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var result = new TradeService().ProposeTrade(created.Registry, created.ScenarioSnapshot, BuildAcceptedOffer(created.ScenarioSnapshot));

        Assert.True(result.InboxItems.Any(item => item.EventType == LegacyEngine.Events.LegacyEventType.TradeAccepted), "Accepted trade should create inbox feedback.");
        Assert.True(result.InboxItems.Any(item => item.EventType == LegacyEngine.Events.LegacyEventType.PendingGmActionCreated), "Accepted trade should create pending action inbox.");
    }

    public void AlphaDesktopExposesTradeUiActions()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("CreateSelectablePeopleContent(\"Trades\")", StringComparison.Ordinal), "AlphaDesktop should expose Trades under Hockey Operations.");
        Assert.True(source.Contains("BuildTradeRows", StringComparison.Ordinal), "Trade block rows should be built.");
        Assert.True(source.Contains("ProposeTradeFor", StringComparison.Ordinal), "Selected trade action should propose a trade.");
        Assert.True(source.Contains("WithdrawLatestTradeOffer", StringComparison.Ordinal), "Trade UI should expose withdraw action.");
    }

    public void AcceptedTradeDoesNotAutoComplete()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var offer = BuildAcceptedOffer(created.ScenarioSnapshot);
        var result = new TradeService().ProposeTrade(created.Registry, created.ScenarioSnapshot, offer);

        Assert.Equal(TradeOfferStatus.Accepted, result.TradeOffer!.Status);
        Assert.True(result.ScenarioSnapshot.AlphaSnapshot.Roster.Players.Any(player => player.PersonId == offer.PlayerGives[0].AssetId), "Proposing accepted trade should not remove outgoing player.");
        Assert.False(result.ScenarioSnapshot.AlphaSnapshot.Roster.Players.Any(player => player.PersonId == offer.PlayerReceives[0].AssetId), "Proposing accepted trade should not add incoming player.");
    }

    public void Alpha32HasNoGodotSaveOrFullTradeNegotiation()
    {
        var root = FindRepositoryRoot();
        var files = Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "Trade*.cs", SearchOption.TopDirectoryOnly)
            .Concat(new[] { Path.Combine(root, "client", "AlphaDesktop", "Program.cs") })
            .Select(File.ReadAllText);
        var text = string.Join("\n", files);

        Assert.False(text.Contains("Godot", StringComparison.Ordinal), "Alpha 3.2 should not add Godot.");
        Assert.False(text.Contains("SaveGame", StringComparison.Ordinal), "Alpha 3.2 should not add save/load.");
        Assert.False(text.Contains("TradeDeadline", StringComparison.Ordinal), "Alpha 3.2 should not add trade deadline system.");
        Assert.False(text.Contains("RetainedSalary", StringComparison.Ordinal), "Alpha 3.2 should not add retained salary.");
    }

    private static TradeOffer BuildAcceptedOffer(NewGmScenarioSnapshot scenario)
    {
        var service = new TradeService();
        var target = scenario.TradeBlock!.Entries.OrderBy(entry => entry.AssetValue).First();
        var outgoing = scenario.AlphaSnapshot.Roster.ActivePlayers
            .OrderByDescending(player => player.Position == target.Position)
            .ThenByDescending(player => scenario.CareerStatSummaries.FirstOrDefault(summary => summary.PersonId == player.PersonId)?.Points ?? 0)
            .First();
        return service.CreateOffer(
            scenario,
            target.OrganizationId,
            target.TeamName,
            new[] { service.CreateRosterPlayerAsset(scenario, outgoing.PersonId) },
            new[] { service.CreateRosterPlayerAsset(scenario, target.PersonId, TradeSide.OtherOrganization) });
    }

    private static NewGmScenarioSnapshot WithProspectRights(NewGmScenarioSnapshot scenario)
    {
        var entry = scenario.AlphaSnapshot.DraftBoard.Entries[0];
        return scenario with
        {
            ProspectRights = new[]
            {
                new DraftRightsRecord(entry.ProspectPersonId, scenario.AlphaSnapshot.People.First(person => person.PersonId == entry.ProspectPersonId).Identity.DisplayName, 17, entry.Bio?.Position ?? RosterPosition.Center, 1, 1, ProspectStatus.DraftRightsHeld, entry.ProjectionText, ScoutingConfidenceLevel.High, "Test rights.")
            }
        };
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var projectPath = Path.Combine(directory.FullName, "engine", "LegacyEngine", "LegacyEngine.csproj");
            if (File.Exists(projectPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find Hockey GM Legacy repository root.");
    }
}
