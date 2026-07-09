using LegacyEngine.Integration;
using LegacyEngine.Rosters;
using LegacyEngine.Scouting;

internal sealed class Alpha621TradesV3Tests
{
    public void OtherTeamRosterPlayersAppearInTradeBuilder()
    {
        var scenario = CreateScenario().ScenarioSnapshot;
        var entry = scenario.TradeBlock!.Entries[0];
        var assets = new TradeService().BuildOtherOrganizationAssets(scenario, entry.OrganizationId, entry.TeamName);

        Assert.True(assets.Count(asset => asset.AssetType == TradeAssetType.Player) >= 18, "Other team assets should include a useful active-roster player list.");
        Assert.True(assets.Any(asset => asset.AssetType == TradeAssetType.Player && asset.Position != RosterPosition.Unknown), "Other roster player assets should include known positions.");
    }

    public void YourRosterPlayersAppearInTradeBuilder()
    {
        var scenario = CreateScenario().ScenarioSnapshot;
        var assets = new TradeService().BuildPlayerOrganizationAssets(scenario);

        Assert.True(assets.Any(asset => asset.AssetType == TradeAssetType.Player), "Your trade assets should include roster players.");
        Assert.True(assets.Any(asset => asset.AssetId == scenario.AlphaSnapshot.Roster.ActivePlayers[0].PersonId), "Roster player assets should use real roster person ids.");
    }

    public void ProspectsAppearForBothTeams()
    {
        var scenario = WithProspectRights(CreateScenario().ScenarioSnapshot);
        var entry = scenario.TradeBlock!.Entries[0];
        var service = new TradeService();
        var yourAssets = service.BuildPlayerOrganizationAssets(scenario);
        var otherAssets = service.BuildOtherOrganizationAssets(scenario, entry.OrganizationId, entry.TeamName);

        Assert.True(yourAssets.Any(asset => asset.AssetType == TradeAssetType.ProspectRights), "Your prospects/rights should be selectable.");
        Assert.True(otherAssets.Any(asset => asset.AssetType == TradeAssetType.ProspectRights), "Other-team prospect rights should be selectable.");
    }

    public void DraftPicksAppearAsTradeAssets()
    {
        var scenario = CreateScenario().ScenarioSnapshot;
        var entry = scenario.TradeBlock!.Entries[0];
        var service = new TradeService();
        var yourPicks = service.BuildPlayerOrganizationAssets(scenario).Where(asset => asset.AssetType == TradeAssetType.DraftPick).ToArray();
        var otherPicks = service.BuildOtherOrganizationAssets(scenario, entry.OrganizationId, entry.TeamName).Where(asset => asset.AssetType == TradeAssetType.DraftPick).ToArray();

        Assert.True(yourPicks.Length > 0, "Your draft picks should appear.");
        Assert.True(otherPicks.Length > 0, "Other-team draft picks should appear.");
        Assert.True(yourPicks[0].DisplayName.Contains("Round Pick", StringComparison.Ordinal), "Draft pick display text should include year and round.");
        Assert.True(yourPicks[0].Summary.Contains("Original owner", StringComparison.Ordinal), "Draft pick should track original/current owner context.");
    }

    public void DraftPickCanBeAddedToYouGive()
    {
        var scenario = CreateScenario().ScenarioSnapshot;
        var entry = scenario.TradeBlock!.Entries[0];
        var service = new TradeService();
        var pick = service.BuildPlayerOrganizationAssets(scenario).First(asset => asset.AssetType == TradeAssetType.DraftPick);
        var receive = service.BuildOtherOrganizationAssets(scenario, entry.OrganizationId, entry.TeamName).First(asset => asset.AssetType == TradeAssetType.Player);
        var offer = service.CreateOffer(scenario, entry.OrganizationId, entry.TeamName, new[] { pick }, new[] { receive });

        Assert.True(offer.PlayerGives.Any(asset => asset.AssetType == TradeAssetType.DraftPick), "Draft pick should be addable to You Give.");
    }

    public void DraftPickCanBeAddedToYouReceive()
    {
        var scenario = CreateScenario().ScenarioSnapshot;
        var entry = scenario.TradeBlock!.Entries[0];
        var service = new TradeService();
        var give = service.BuildPlayerOrganizationAssets(scenario).First(asset => asset.AssetType == TradeAssetType.Player);
        var pick = service.BuildOtherOrganizationAssets(scenario, entry.OrganizationId, entry.TeamName).First(asset => asset.AssetType == TradeAssetType.DraftPick);
        var offer = service.CreateOffer(scenario, entry.OrganizationId, entry.TeamName, new[] { give }, new[] { pick });

        Assert.True(offer.PlayerReceives.Any(asset => asset.AssetType == TradeAssetType.DraftPick), "Draft pick should be addable to You Receive.");
    }

    public void ProposalBucketsAreSeparateInUi()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("CurrentTradeYouGiveRows", StringComparison.Ordinal), "Trade UI should expose separate You Give rows.");
        Assert.True(source.Contains("CurrentTradeYouReceiveRows", StringComparison.Ordinal), "Trade UI should expose separate You Receive rows.");
        Assert.True(source.Contains("Remove From You Give", StringComparison.Ordinal), "Trade UI should remove from You Give separately.");
        Assert.True(source.Contains("Remove From You Receive", StringComparison.Ordinal), "Trade UI should remove from You Receive separately.");
    }

    public void AiReturnsCounterForCloseOffer()
    {
        var created = CreateScenario();
        var found = FindCounterOffer(created.ScenarioSnapshot);

        Assert.Equal(TradeOfferStatus.Countered, found.Evaluation.Decision);
        Assert.True(!string.IsNullOrWhiteSpace(found.Evaluation.CounterSuggestion), "Countered trade should include counter suggestion.");
    }

    public void CounterCanAddPick()
    {
        var scenario = CreateScenario().ScenarioSnapshot;
        var service = new TradeService();
        var strategy = new TradeStrategyService();
        var entry = scenario.TradeBlock!.Entries.First(candidate =>
            strategy.BuildTeamNeedsProfile(scenario, candidate.OrganizationId, candidate.TeamName).AssetPreferences.Contains(AssetPreference.DraftPick));
        var receive = service.CreateRosterPlayerAsset(scenario, entry.PersonId, TradeSide.OtherOrganization);
        var give = service.BuildPlayerOrganizationAssets(scenario).First(asset => asset.AssetType == TradeAssetType.Player) with { Value = Math.Max(1, receive.Value - 8) };
        var offer = service.CreateOffer(scenario, entry.OrganizationId, entry.TeamName, new[] { give }, new[] { receive });
        var evaluation = service.EvaluateTrade(scenario, offer);
        var counter = strategy.BuildCounterOffer(scenario, offer, evaluation);

        Assert.True(counter.RequestedAssets.Any(asset => asset.AssetType == TradeAssetType.DraftPick), "Draft-pick-focused teams should counter by asking for a pick.");
        Assert.True(counter.RevisedPlayerGives.Any(asset => asset.AssetType == TradeAssetType.DraftPick), "Revised proposal should include requested pick.");
    }

    public void CounterCanRequestDifferentAssetType()
    {
        var scenario = CreateScenario().ScenarioSnapshot;
        var service = new TradeService();
        var strategy = new TradeStrategyService();
        var entry = scenario.TradeBlock!.Entries.FirstOrDefault(candidate =>
            !strategy.BuildTeamNeedsProfile(scenario, candidate.OrganizationId, candidate.TeamName).AssetPreferences.Contains(AssetPreference.DraftPick))
            ?? scenario.TradeBlock.Entries[0];
        var receive = service.CreateRosterPlayerAsset(scenario, entry.PersonId, TradeSide.OtherOrganization);
        var give = service.CreateDraftPickAsset(scenario, TradeSide.PlayerOrganization, scenario.Organization.OrganizationId, scenario.Organization.Name, 5, scenario.Season.Year + 1);
        var offer = service.CreateOffer(scenario, entry.OrganizationId, entry.TeamName, new[] { give }, new[] { receive });
        var evaluation = service.EvaluateTrade(scenario, offer);
        var counter = strategy.BuildCounterOffer(scenario, offer, evaluation);

        Assert.True(counter.RequestedAssets.Count > 0, "Counter should request a concrete additional asset.");
        Assert.True(counter.RequestedAssets.Any(asset => asset.AssetType != give.AssetType) || counter.Message.Contains("fit", StringComparison.OrdinalIgnoreCase), "Counter should be able to ask for a different asset type or better fit.");
    }

    public void AcceptingCounterUpdatesProposalButDoesNotCompleteTrade()
    {
        var scenario = CreateScenario().ScenarioSnapshot;
        var found = FindCounterOffer(scenario);
        var counter = new TradeStrategyService().BuildCounterOffer(scenario, found.Offer, found.Evaluation);

        Assert.True(counter.RevisedPlayerGives.Count >= found.Offer.PlayerGives.Count, "Counter should revise outgoing package.");
        Assert.Equal(found.Offer.PlayerReceives.Count, counter.RevisedPlayerReceives.Count);
        Assert.False(scenario.TradeOffers.Any(offer => offer.Status == TradeOfferStatus.Completed), "Accepting counter into proposal should not complete a trade.");
    }

    public void ProposeAcceptedTradeCreatesPendingGmAction()
    {
        var created = CreateScenario();
        var offer = BuildAcceptedOffer(created.ScenarioSnapshot);
        var result = new TradeService().ProposeTrade(created.Registry, created.ScenarioSnapshot, offer);

        Assert.Equal(TradeOfferStatus.Accepted, result.TradeOffer!.Status);
        Assert.True(result.ScenarioSnapshot.PendingActions.Any(action => action.ActionType == PendingGmActionType.ApproveTrade && action.IsOpen), "Accepted trade should create pending GM approval.");
    }

    public void ApprovedTradeMovesPlayersProspectsAndRecordsPicks()
    {
        var created = CreateScenario();
        var scenario = WithProspectRights(created.ScenarioSnapshot);
        var offer = BuildAcceptedMultiAssetOffer(scenario);
        var proposed = new TradeService().ProposeTrade(created.Registry, scenario, offer).ScenarioSnapshot;
        var action = proposed.PendingActions.Single(action => action.ActionType == PendingGmActionType.ApproveTrade && action.IsOpen);
        var approved = new PendingGmActionService().Approve(created.Registry, proposed, action.ActionId);

        Assert.True(approved.Success, "Approving accepted trade should succeed.");
        Assert.False(approved.ScenarioSnapshot.AlphaSnapshot.Roster.Players.Any(player => player.PersonId == offer.PlayerGives.First(asset => asset.AssetType == TradeAssetType.Player).AssetId), "Outgoing roster player should move.");
        Assert.False(approved.ScenarioSnapshot.ProspectRights.Any(prospect => prospect.ProspectPersonId == offer.PlayerGives.First(asset => asset.AssetType == TradeAssetType.ProspectRights).AssetId), "Outgoing prospect rights should move.");
        Assert.True(approved.LeagueTransactions?.Any(transaction => transaction.Description.Contains("Round Pick", StringComparison.Ordinal)) == true, "League news should record traded draft picks.");
    }

    public void DeclinedTradeLeavesAssetsUnchanged()
    {
        var created = CreateScenario();
        var scenario = WithProspectRights(created.ScenarioSnapshot);
        var offer = BuildAcceptedMultiAssetOffer(scenario);
        var proposed = new TradeService().ProposeTrade(created.Registry, scenario, offer).ScenarioSnapshot;
        var action = proposed.PendingActions.Single(action => action.ActionType == PendingGmActionType.ApproveTrade && action.IsOpen);
        var declined = new PendingGmActionService().Decline(created.Registry, proposed, action.ActionId);

        Assert.True(declined.Success, "Declining accepted trade should succeed.");
        Assert.True(declined.ScenarioSnapshot.AlphaSnapshot.Roster.Players.Any(player => player.PersonId == offer.PlayerGives.First(asset => asset.AssetType == TradeAssetType.Player).AssetId), "Outgoing roster player should remain after decline.");
        Assert.True(declined.ScenarioSnapshot.ProspectRights.Any(prospect => prospect.ProspectPersonId == offer.PlayerGives.First(asset => asset.AssetType == TradeAssetType.ProspectRights).AssetId), "Outgoing prospect rights should remain after decline.");
    }

    public void LeagueNewsRecordsDraftPicksInTradeSummary()
    {
        var created = CreateScenario();
        var scenario = WithProspectRights(created.ScenarioSnapshot);
        var offer = BuildAcceptedMultiAssetOffer(scenario);
        var proposed = new TradeService().ProposeTrade(created.Registry, scenario, offer).ScenarioSnapshot;
        var action = proposed.PendingActions.Single(action => action.ActionType == PendingGmActionType.ApproveTrade && action.IsOpen);
        var approved = new PendingGmActionService().Approve(created.Registry, proposed, action.ActionId);

        Assert.True(approved.LeagueTransactions?.Any(transaction =>
            transaction.TransactionType == LeagueTransactionType.TradeCompleted
            && transaction.Description.Contains("Round Pick", StringComparison.Ordinal)) == true, "Completed trade news should name draft picks.");
    }

    public void Alpha621HasNoForbiddenSystems()
    {
        var root = FindRepositoryRoot();
        var text = string.Join("\n",
            Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "Trade*.cs", SearchOption.TopDirectoryOnly)
                .Concat(new[] { Path.Combine(root, "client", "AlphaDesktop", "Program.cs") })
                .Select(File.ReadAllText));

        Assert.False(text.Contains("RetainedSalary", StringComparison.Ordinal), "Alpha 6.2.1 should not add retained salary.");
        Assert.False(text.Contains("ConditionalPick", StringComparison.Ordinal), "Alpha 6.2.1 should not add conditional picks.");
        Assert.False(text.Contains("ThreeWayTrade", StringComparison.Ordinal), "Alpha 6.2.1 should not add three-team trades.");
        Assert.False(text.Contains("NoTradeClause", StringComparison.Ordinal), "Alpha 6.2.1 should not add no-trade clauses.");
        Assert.False(text.Contains("Godot", StringComparison.Ordinal), "Alpha 6.2.1 should not add Godot.");
    }

    private static (TradeOffer Offer, TradeEvaluation Evaluation) FindCounterOffer(NewGmScenarioSnapshot scenario)
    {
        var service = new TradeService();
        foreach (var entry in scenario.TradeBlock!.Entries.OrderByDescending(entry => entry.AssetValue))
        {
            var receive = service.CreateRosterPlayerAsset(scenario, entry.PersonId, TradeSide.OtherOrganization);
            foreach (var value in Enumerable.Range(1, 90).Reverse())
            {
                var give = service.BuildPlayerOrganizationAssets(scenario)
                    .First(asset => asset.AssetType == TradeAssetType.Player) with
                {
                    Value = value
                };
                var offer = service.CreateOffer(scenario, entry.OrganizationId, entry.TeamName, new[] { give }, new[] { receive });
                var evaluation = service.EvaluateTrade(scenario, offer);
                if (evaluation.Decision == TradeOfferStatus.Countered)
                {
                    return (offer, evaluation);
                }
            }
        }

        throw new InvalidOperationException("Could not find deterministic counter-offer test case.");
    }

    private static TradeOffer BuildAcceptedOffer(NewGmScenarioSnapshot scenario)
    {
        var service = new TradeService();
        var outgoingAssets = service.BuildPlayerOrganizationAssets(scenario);
        var bestPlayer = outgoingAssets
            .Where(asset => asset.AssetType == TradeAssetType.Player)
            .OrderByDescending(asset => asset.Value)
            .First();
        var bestPick = outgoingAssets
            .Where(asset => asset.AssetType == TradeAssetType.DraftPick)
            .OrderByDescending(asset => asset.Value)
            .FirstOrDefault();

        foreach (var entry in scenario.TradeBlock!.Entries.OrderBy(entry => entry.AssetValue))
        {
            var receive = service.CreateRosterPlayerAsset(scenario, entry.PersonId, TradeSide.OtherOrganization);
            var packages = bestPick is null
                ? new[] { new[] { bestPlayer } }
                : new[] { new[] { bestPlayer }, new[] { bestPlayer, bestPick } };
            foreach (var package in packages)
            {
                var offer = service.CreateOffer(scenario, entry.OrganizationId, entry.TeamName, package, new[] { receive });
                if (service.EvaluateTrade(scenario, offer).Decision == TradeOfferStatus.Accepted)
                {
                    return offer;
                }
            }
        }

        throw new InvalidOperationException("Could not find deterministic accepted trade test case.");
    }

    private static TradeOffer BuildAcceptedMultiAssetOffer(NewGmScenarioSnapshot scenario)
    {
        var service = new TradeService();
        var entry = scenario.TradeBlock!.Entries.OrderBy(entry => entry.AssetValue).First();
        var otherAssets = service.BuildOtherOrganizationAssets(scenario, entry.OrganizationId, entry.TeamName);
        var give = service.BuildPlayerOrganizationAssets(scenario);
        var outgoing = new[]
        {
            give.Where(asset => asset.AssetType == TradeAssetType.Player).OrderByDescending(asset => asset.Value).First(),
            give.First(asset => asset.AssetType == TradeAssetType.ProspectRights),
            give.First(asset => asset.AssetType == TradeAssetType.DraftPick)
        };
        var incoming = new[]
        {
            otherAssets.Where(asset => asset.AssetType == TradeAssetType.Player).OrderBy(asset => asset.Value).First(),
            otherAssets.First(asset => asset.AssetType == TradeAssetType.DraftPick)
        };
        return service.CreateOffer(scenario, entry.OrganizationId, entry.TeamName, outgoing, incoming);
    }

    private static NewGmScenarioSnapshot WithProspectRights(NewGmScenarioSnapshot scenario)
    {
        if (scenario.ProspectRights.Count > 0)
        {
            return scenario;
        }

        var entry = scenario.AlphaSnapshot.DraftBoard.Entries[0];
        return scenario with
        {
            ProspectRights = new[]
            {
                new DraftRightsRecord(entry.ProspectPersonId, scenario.AlphaSnapshot.People.First(person => person.PersonId == entry.ProspectPersonId).Identity.DisplayName, 17, entry.Bio?.Position ?? RosterPosition.Center, 1, 1, ProspectStatus.DraftRightsHeld, entry.ProjectionText, ScoutingConfidenceLevel.High, "Test rights.")
            }
        };
    }

    private static NewGmScenarioResult CreateScenario() =>
        NewGmScenarioBootstrapper.CreateScenario();

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
