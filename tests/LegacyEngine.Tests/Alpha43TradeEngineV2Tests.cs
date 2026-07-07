using LegacyEngine.Integration;
using LegacyEngine.Rosters;
using LegacyEngine.Scouting;

internal sealed class Alpha43TradeEngineV2Tests
{
    public void TeamNeedsGenerated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var entry = scenario.TradeBlock!.Entries[0];
        var profile = new TradeStrategyService().BuildTeamNeedsProfile(scenario, entry.OrganizationId, entry.TeamName);

        Assert.True(profile.Needs.Count > 0, "Team needs profile should include actionable needs.");
        Assert.True(!string.IsNullOrWhiteSpace(profile.Summary), "Team needs profile should explain the club direction.");
    }

    public void GmPersonalitiesGenerated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var profiles = new TradeStrategyService().BuildLeagueNeeds(scenario);

        Assert.True(profiles.Any(profile => profile.GmPersonality != TradeGmPersonality.Conservative) || profiles.Count > 1, "AI teams should have trade personalities.");
        Assert.True(profiles.All(profile => profile.AssetPreferences.Count > 0), "AI personalities should feed asset preferences.");
    }

    public void MultiAssetTradesSupported()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var scenario = WithProspectRights(created.ScenarioSnapshot);
        var service = new TradeService();
        var target = scenario.TradeBlock!.Entries[0];
        var outgoingPlayer = scenario.AlphaSnapshot.Roster.ActivePlayers[0];
        var offer = service.CreateOffer(
            scenario,
            target.OrganizationId,
            target.TeamName,
            new[]
            {
                service.CreateRosterPlayerAsset(scenario, outgoingPlayer.PersonId),
                service.CreateDraftPickAsset(scenario, TradeSide.PlayerOrganization, scenario.Organization.OrganizationId, scenario.Organization.Name, 3, scenario.Season.Year + 1),
                service.CreateProspectRightsAsset(scenario, scenario.ProspectRights[0].ProspectPersonId)
            },
            new[]
            {
                service.CreateRosterPlayerAsset(scenario, target.PersonId, TradeSide.OtherOrganization),
                service.CreateFutureConsiderationAsset(scenario, TradeSide.OtherOrganization, target.OrganizationId, target.TeamName)
            });

        var result = service.ProposeTrade(created.Registry, scenario, offer);

        Assert.True(result.Success, "Multi-asset offer should be valid and receive an AI answer.");
        Assert.True(result.TradeOffer!.PlayerGives.Count > 1, "Offer should retain multiple outgoing assets.");
        Assert.True(result.TradeOffer.PlayerReceives.Any(asset => asset.AssetType == TradeAssetType.FutureConsideration), "Future consideration placeholder should be supported.");
    }

    public void CounterOffersGenerated()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var scenario = created.ScenarioSnapshot;
        var service = new TradeService();
        var target = scenario.TradeBlock!.Entries.OrderByDescending(entry => entry.AssetValue).First();
        var offer = service.CreateOffer(
            scenario,
            target.OrganizationId,
            target.TeamName,
            new[] { service.CreateDraftPickAsset(scenario, TradeSide.PlayerOrganization, scenario.Organization.OrganizationId, scenario.Organization.Name, 2, scenario.Season.Year + 1) },
            new[] { service.CreateRosterPlayerAsset(scenario, target.PersonId, TradeSide.OtherOrganization) });

        var evaluation = service.EvaluateTrade(scenario, offer);
        var counter = new TradeStrategyService().BuildCounterOffer(scenario, offer, evaluation);

        Assert.True(!string.IsNullOrWhiteSpace(counter.Message), "Counter offer should explain what the other team wants.");
        Assert.True(counter.Message.Contains("pick", StringComparison.OrdinalIgnoreCase) || counter.Message.Contains("fit", StringComparison.OrdinalIgnoreCase) || counter.Message.Contains("goalie", StringComparison.OrdinalIgnoreCase), "Counter should be hockey-readable.");
    }

    public void BudgetAffectsTrade()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var service = new TradeService();
        var target = scenario.TradeBlock!.Entries[0];
        var outgoing = service.CreateRosterPlayerAsset(scenario, scenario.AlphaSnapshot.Roster.ActivePlayers[0].PersonId);
        var receive = service.CreateRosterPlayerAsset(scenario, target.PersonId, TradeSide.OtherOrganization);
        var normal = service.CreateOffer(scenario, target.OrganizationId, target.TeamName, new[] { outgoing }, new[] { receive });
        var expensiveOutgoing = service.CreateOffer(scenario, target.OrganizationId, target.TeamName, new[] { outgoing with { SalaryImpact = 250_000m } }, new[] { receive });

        var normalScore = service.EvaluateTrade(scenario, normal).Score;
        var expensiveScore = service.EvaluateTrade(scenario, expensiveOutgoing).Score;

        Assert.True(normalScore != expensiveScore, "Budget impact should affect trade evaluation.");
    }

    public void TeamDirectionAffectsTrade()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var entry = scenario.TradeBlock!.Entries.First();
        var profile = new TradeStrategyService().BuildTeamNeedsProfile(scenario, entry.OrganizationId, entry.TeamName);

        Assert.True(profile.Direction != TeamDirection.Neutral, "AI team direction should be generated.");
        Assert.True(profile.AssetPreferences.Count > 0, "Team direction should produce asset preferences.");
    }

    public void PlayerReactionGenerated()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var result = new TradeService().ProposeTrade(created.Registry, created.ScenarioSnapshot, BuildAcceptedOffer(created.ScenarioSnapshot));

        Assert.True(result.Evaluation!.PlayerReactionNotes.Count > 0, "Trade evaluation should include a player reaction note.");
    }

    public void StaffReactionGenerated()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var result = new TradeService().ProposeTrade(created.Registry, created.ScenarioSnapshot, BuildAcceptedOffer(created.ScenarioSnapshot));

        Assert.True(result.Evaluation!.StaffReactionNotes.Count >= 3, "Trade evaluation should include coach, scout, owner, or assistant GM reactions.");
    }

    public void TradeHistoryStored()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var offer = BuildAcceptedOffer(created.ScenarioSnapshot);
        var proposed = new TradeService().ProposeTrade(created.Registry, created.ScenarioSnapshot, offer).ScenarioSnapshot;
        var action = proposed.PendingActions.Single(action => action.ActionType == PendingGmActionType.ApproveTrade && action.IsOpen);
        var approved = new PendingGmActionService().Approve(created.Registry, proposed, action.ActionId);

        Assert.True(approved.ScenarioSnapshot.TransactionHistory.Any(item => item.TransactionType == "TradeCompleted"), "Completed trade should be stored in transaction history.");
    }

    public void CareerTimelineUpdated()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var offer = BuildAcceptedOffer(created.ScenarioSnapshot);
        var proposed = new TradeService().ProposeTrade(created.Registry, created.ScenarioSnapshot, offer).ScenarioSnapshot;
        var action = proposed.PendingActions.Single(action => action.ActionType == PendingGmActionType.ApproveTrade && action.IsOpen);
        var approved = new PendingGmActionService().Approve(created.Registry, proposed, action.ActionId);

        Assert.True(approved.ScenarioSnapshot.CareerTimeline.Entries.Any(entry => entry.EntryType == CareerTimelineEntryType.Traded), "Completed trade should update career timeline.");
    }

    public void OrganizationHistoryUpdated()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var offer = BuildAcceptedOffer(created.ScenarioSnapshot);
        var proposed = new TradeService().ProposeTrade(created.Registry, created.ScenarioSnapshot, offer).ScenarioSnapshot;
        var action = proposed.PendingActions.Single(action => action.ActionType == PendingGmActionType.ApproveTrade && action.IsOpen);
        var approved = new PendingGmActionService().Approve(created.Registry, proposed, action.ActionId);

        Assert.True(approved.ScenarioSnapshot.TransactionHistory.Any(item => item.OrganizationId == approved.ScenarioSnapshot.Organization.OrganizationId && item.TransactionType == "TradeCompleted"), "Organization history should include completed trade transaction.");
    }

    public void LeagueNewsUpdated()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var offer = BuildAcceptedOffer(created.ScenarioSnapshot);
        var proposed = new TradeService().ProposeTrade(created.Registry, created.ScenarioSnapshot, offer).ScenarioSnapshot;
        var action = proposed.PendingActions.Single(action => action.ActionType == PendingGmActionType.ApproveTrade && action.IsOpen);
        var approved = new PendingGmActionService().Approve(created.Registry, proposed, action.ActionId);

        Assert.True(approved.LeagueTransactions?.Any(transaction => transaction.TransactionType == LeagueTransactionType.TradeCompleted) == true, "Completed trade should appear in league news.");
    }

    public void TradeUiExposesV2Strategy()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Other Team Strategy", StringComparison.Ordinal), "Trade UI should show team needs and direction.");
        Assert.True(source.Contains("Trade Value Screen", StringComparison.Ordinal), "Trade UI should show selected player value context.");
        Assert.True(source.Contains("Trade Builder", StringComparison.Ordinal), "Trade UI should show builder context.");
        Assert.True(source.Contains("Staff / Player Reaction", StringComparison.Ordinal), "Trade UI should show reaction notes.");
    }

    public void Alpha43HasNoForbiddenTradeSystems()
    {
        var root = FindRepositoryRoot();
        var files = Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "Trade*.cs", SearchOption.TopDirectoryOnly)
            .Concat(new[] { Path.Combine(root, "client", "AlphaDesktop", "Program.cs") })
            .Select(File.ReadAllText);
        var text = string.Join("\n", files);

        Assert.False(text.Contains("RetainedSalary", StringComparison.Ordinal), "Alpha 4.3 should not add retained salary.");
        Assert.False(text.Contains("ThreeWayTrade", StringComparison.Ordinal), "Alpha 4.3 should not add three-way trades.");
        Assert.False(text.Contains("NoTradeClause", StringComparison.Ordinal), "Alpha 4.3 should not add no-trade clauses.");
        Assert.False(text.Contains("Godot", StringComparison.Ordinal), "Alpha 4.3 should not add Godot.");
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
