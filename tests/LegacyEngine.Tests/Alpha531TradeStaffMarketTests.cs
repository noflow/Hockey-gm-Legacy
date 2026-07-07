using LegacyEngine.Integration;
using LegacyEngine.RuleEngine;
using LegacyEngine.Staff;

internal sealed class Alpha531TradeStaffMarketTests
{
    public void YourAssetCanBeSelected()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("SelectYourTradeAsset", StringComparison.Ordinal), "Trade popup should select player-organization assets.");
        Assert.True(source.Contains("_tradeYourAssetsList.SelectionChanged", StringComparison.Ordinal), "Your asset list should react to selection.");
    }

    public void OtherTeamAssetCanBeSelected()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("SelectOtherTradeAsset", StringComparison.Ordinal), "Trade popup should select other-team assets.");
        Assert.True(source.Contains("_tradeOtherAssetsList.SelectionChanged", StringComparison.Ordinal), "Other asset list should react to selection.");
    }

    public void SelectedAssetCanBeAddedToProposal()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("AddYourAssetToTradeProposal", StringComparison.Ordinal), "Your selected asset should be addable.");
        Assert.True(source.Contains("AddOtherAssetToTradeProposal", StringComparison.Ordinal), "Other selected asset should be addable.");
        Assert.True(source.Contains("Add Selected From Your Team", StringComparison.Ordinal), "Your add button should be visible.");
        Assert.True(source.Contains("Add Selected From Other Team", StringComparison.Ordinal), "Other add button should be visible.");
        Assert.True(source.Contains("Remove Selected From Offer", StringComparison.Ordinal), "Proposal should support removing selected assets.");
    }

    public void ProposalUpdatesEvaluation()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("CurrentTradeEvaluation", StringComparison.Ordinal), "Proposal should recalculate AI evaluation.");
        Assert.True(source.Contains("CurrentTradeEvaluationReasons", StringComparison.Ordinal), "Proposal should expose evaluation reasons.");
        Assert.True(source.Contains("CurrentTradeRosterImpact", StringComparison.Ordinal), "Proposal should expose roster impact.");
        Assert.True(source.Contains("CurrentTradeBudgetImpact", StringComparison.Ordinal), "Proposal should expose budget impact.");
    }

    public void InvalidEmptyProposalRejected()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var scenario = created.ScenarioSnapshot;
        var entry = scenario.TradeBlock!.Entries[0];
        var service = new TradeService();
        var offer = service.CreateOffer(scenario, entry.OrganizationId, entry.TeamName, Array.Empty<TradeAsset>(), Array.Empty<TradeAsset>());

        var result = service.ProposeTrade(created.Registry, scenario, offer);

        Assert.False(result.Success, "Empty trade offer should be rejected.");
        Assert.True(result.Message.Contains("both sides", StringComparison.OrdinalIgnoreCase), result.Message);
    }

    public void ProposeTradeDisabledUntilBothSidesHaveAssets()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("CanProposeCurrentTrade => _tradePlayerGives.Count > 0 && _tradePlayerReceives.Count > 0", StringComparison.Ordinal), "Propose Trade should require both sides.");
        Assert.True(source.Contains("State.CanProposeCurrentTrade && State.TradeDeadlineWindow.TradesAllowed", StringComparison.Ordinal), "Propose button should be disabled until valid.");
    }

    public void ViewDossierWorksFromTradePopup()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("View Dossier", StringComparison.Ordinal), "Trade popup should include View Dossier.");
        Assert.True(source.Contains("OpenDossierFor(selected?.AssetId ?? entry.PersonId)", StringComparison.Ordinal), "Trade popup should use the dossier popup path.");
    }

    public void AcceptedTradeCreatesPendingApproval()
    {
        var scenario = new MultiLeagueCareerService()
            .CreateScenario(new MultiLeagueCareerService().SelectLeagueAndTeam(LeagueExperience.Junior, "org-prairie-falcons"));
        var service = new TradeService();

        foreach (var blockEntry in scenario.ScenarioSnapshot.TradeBlock!.Entries.OrderBy(entry => entry.AssetValue))
        {
            var outgoingPlayers = scenario.ScenarioSnapshot.AlphaSnapshot.Roster.ActivePlayers.Take(5)
                .Select(player => service.CreateRosterPlayerAsset(scenario.ScenarioSnapshot, player.PersonId))
                .ToArray();
            var outgoingAssets = outgoingPlayers
                .Append(service.CreateDraftPickAsset(scenario.ScenarioSnapshot, TradeSide.PlayerOrganization, scenario.ScenarioSnapshot.Organization.OrganizationId, scenario.ScenarioSnapshot.Organization.Name, 1, scenario.ScenarioSnapshot.Season.Year + 1))
                .ToArray();
            var incoming = service.CreateRosterPlayerAsset(scenario.ScenarioSnapshot, blockEntry.PersonId, TradeSide.OtherOrganization);
            var offer = service.CreateOffer(scenario.ScenarioSnapshot, blockEntry.OrganizationId, blockEntry.TeamName, outgoingAssets, new[] { incoming });

            var result = service.ProposeTrade(scenario.Registry, scenario.ScenarioSnapshot, offer);
            if (result.TradeOffer?.Status == TradeOfferStatus.Accepted)
            {
                Assert.True(result.ScenarioSnapshot.PendingActions.Any(action => action.ActionType == PendingGmActionType.ApproveTrade && action.PersonId == result.TradeOffer.TradeOfferId && action.IsOpen), "Accepted trade should create pending GM approval.");
                Assert.True(result.ScenarioSnapshot.AlphaSnapshot.Roster.FindPlayer(blockEntry.PersonId) is null, "Accepted trade should not auto-complete roster movement.");
                return;
            }
        }

        Assert.True(false, "Expected at least one favorable trade offer to be accepted.");
    }

    public void StaffMarketExistsAtScenarioStart()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;

        Assert.True(scenario.StaffMarket is not null, "Staff market should exist at scenario start.");
    }

    public void StaffMarketHasAvailableCandidates()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;

        Assert.True(scenario.StaffMarket!.AvailableCandidates.Count > 0, "Staff market should include available or interested candidates.");
    }

    public void CandidateHasSalaryAskAndCareerHistory()
    {
        var candidate = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot.StaffMarket!.Candidates[0];

        Assert.True(candidate.SalaryAsk.AnnualAmount > 0, "Candidate should have salary ask.");
        Assert.True(candidate.CareerHistory.Count > 0, "Candidate should have career history.");
        Assert.True(!string.IsNullOrWhiteSpace(candidate.AvailabilitySummary), "Candidate should explain why they are in the market.");
    }

    public void HiringCandidateMovesThemToOrganization()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var candidate = created.ScenarioSnapshot.StaffMarket!.AvailableCandidates.First(candidate => candidate.CanBeHired);

        var result = new StaffOfficeService().HireCandidate(created.Registry, created.ScenarioSnapshot, candidate.CandidateId);

        Assert.True(result.Success, result.Message);
        Assert.True(result.ScenarioSnapshot.StaffMembers.Any(member => member.PersonId == candidate.PersonId), "Hired candidate should become current staff.");
    }

    public void CandidateMarkedHiredAfterHiring()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var candidate = created.ScenarioSnapshot.StaffMarket!.AvailableCandidates.First(candidate => candidate.CanBeHired);

        var result = new StaffOfficeService().HireCandidate(created.Registry, created.ScenarioSnapshot, candidate.CandidateId);
        var marketCandidate = result.ScenarioSnapshot.StaffMarket!.FindByPersonId(candidate.PersonId);

        Assert.True(marketCandidate is not null, "Candidate should remain tracked by market identity.");
        Assert.Equal(StaffMarketStatus.Hired, marketCandidate!.Status);
    }

    public void CandidateCannotBeHiredTwice()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var candidate = created.ScenarioSnapshot.StaffMarket!.AvailableCandidates.First(candidate => candidate.CanBeHired);
        var service = new StaffOfficeService();
        var hired = service.HireCandidate(created.Registry, created.ScenarioSnapshot, candidate.CandidateId);

        var second = service.HireCandidate(created.Registry, hired.ScenarioSnapshot, candidate.CandidateId);

        Assert.False(second.Success, "Candidate should not be hired twice.");
    }

    public void OtherTeamVacancyCanHireStaffFromMarket()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();

        var result = new StaffMarketService().SimulateOtherTeamVacancyHire(created.Registry, created.ScenarioSnapshot, "org-other-market-test", "North Valley Wolves");

        Assert.True(result.Success, result.Message);
        Assert.True(result.Candidate is not null, "Other team hire should choose a candidate.");
        Assert.Equal(StaffMarketStatus.Employed, result.Candidate!.Status);
        Assert.Equal("org-other-market-test", result.Candidate.CurrentEmployerOrganizationId);
    }

    public void ReleasedStaffReturnsToMarket()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var staff = created.ScenarioSnapshot.StaffMembers.First(member => member.CurrentRole is not StaffRole.GeneralManager);

        var result = new StaffOfficeService().ReleaseStaff(created.Registry, created.ScenarioSnapshot, staff.PersonId, "Alpha 5.3.1 market release test.");
        var marketCandidate = result.ScenarioSnapshot.StaffMarket!.FindByPersonId(staff.PersonId);

        Assert.True(result.Success, result.Message);
        Assert.True(marketCandidate is not null, "Released staff should return to market.");
        Assert.Equal(StaffMarketStatus.Available, marketCandidate!.Status);
    }

    public void LeagueNewsRecordsNotableStaffMovement()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();

        var result = new StaffMarketService().SimulateOtherTeamVacancyHire(created.Registry, created.ScenarioSnapshot, "org-other-market-test", "North Valley Wolves");

        Assert.True(result.LeagueTransactions.Any(transaction => transaction.TransactionType == LeagueTransactionType.StaffHired && transaction.Category == LeagueNewsCategory.Staff), "Other-team staff hire should create staff league news.");
    }

    public void PlayerInboxDoesNotReceiveOtherTeamRoutineStaffSpam()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();

        var result = new StaffMarketService().SimulateOtherTeamVacancyHire(created.Registry, created.ScenarioSnapshot, "org-other-market-test", "North Valley Wolves");

        Assert.Equal(0, result.InboxItems.Count);
    }

    public void SaveLoadPreservesStaffMarket()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var moved = new StaffMarketService().SimulateOtherTeamVacancyHire(created.Registry, created.ScenarioSnapshot, "org-other-market-test", "North Valley Wolves").ScenarioSnapshot;
        var path = Path.Combine(Path.GetTempPath(), $"hockey-gm-alpha531-{Guid.NewGuid():N}.json");
        try
        {
            var saved = new SaveGameService().SaveCareer(
                moved,
                Array.Empty<InboxMessage>(),
                Array.Empty<LeagueTransaction>(),
                new Dictionary<string, ActionCenterStatus>(StringComparer.Ordinal),
                new BudgetOverviewService().Build(moved, RulebookPresets.CreateJuniorMajor()),
                path,
                "Alpha 5.3.1 Staff Market Save");

            var loaded = new SaveGameService().LoadFromFile(saved.FilePath!, RulebookPresets.CreateJuniorMajor());

            Assert.True(saved.Success, saved.Message);
            Assert.True(loaded.Success, loaded.Message);
            Assert.Equal(moved.StaffMarket!.Candidates.Count, loaded.SaveGame!.ScenarioSnapshot.StaffMarket!.Candidates.Count);
            Assert.Equal(moved.StaffMovementHistory.Count, loaded.SaveGame.ScenarioSnapshot.StaffMovementHistory.Count);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    public void AlphaDesktopExposesStaffMarketFiltersActions()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("Staff Market", StringComparison.Ordinal), "Desktop should expose living staff market.");
        Assert.True(source.Contains("Hiring interest", StringComparison.Ordinal), "Candidate detail should show hiring interest.");
        Assert.True(source.Contains("Market status", StringComparison.Ordinal), "Candidate detail should show market status.");
        Assert.True(source.Contains("Approach Candidate", StringComparison.Ordinal), "Employed candidates should have an approach placeholder.");
        Assert.True(source.Contains("Hire Candidate", StringComparison.Ordinal), "Available candidates should have hire action.");
        Assert.False(source.Contains("CreateDetailButton(\"Generate Candidates\"", StringComparison.Ordinal), "Generate Candidate workflow should not remain as a visible action.");
    }

    public void NoGenerateCandidateWorkflowRemains()
    {
        var source = AlphaDesktopSource();

        Assert.False(source.Contains("Generate Candidates", StringComparison.Ordinal), "Desktop should not present Generate Candidates workflow.");
        Assert.False(source.Contains("Generate Candidate", StringComparison.Ordinal), "Desktop should not present Generate Candidate workflow.");
    }

    public void Alpha531HasNoForbiddenSystems()
    {
        var root = FindRepositoryRoot();
        var files = Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "StaffMarket*.cs", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "Trade*.cs", SearchOption.TopDirectoryOnly))
            .Select(File.ReadAllText);
        var text = string.Join("\n", files);

        Assert.False(text.Contains("Godot", StringComparison.Ordinal), "Alpha 5.3.1 should not add Godot.");
        Assert.False(text.Contains("SalaryCap", StringComparison.Ordinal), "Alpha 5.3.1 should not add salary cap.");
        Assert.False(text.Contains("AgentSystem", StringComparison.Ordinal), "Alpha 5.3.1 should not add staff agent system.");
    }

    private static string AlphaDesktopSource() =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

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
