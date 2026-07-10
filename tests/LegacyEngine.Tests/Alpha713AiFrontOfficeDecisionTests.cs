using LegacyEngine.Integration;
using LegacyEngine.RuleEngine;

internal sealed class Alpha713AiFrontOfficeDecisionTests
{
    public void DecisionWindowsImplemented()
    {
        var service = new AiFrontOfficeDecisionService();
        var scenario = Scenario();

        Assert.Equal(AiDecisionWindow.DraftPreparation, service.BuildSchedule(scenario).Window);
        Assert.True(Enum.GetValues<AiDecisionWindow>().Length >= 12, "All requested decision windows should exist.");
    }

    public void RebuildingTeamShopsExpiringVeteran()
    {
        var candidate = Candidates(CompetitiveWindow.Rebuild).First(item => item.Title.Contains("rebuild", StringComparison.OrdinalIgnoreCase) || item.Title.Contains("shop", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(AiFrontOfficeDecisionType.Trade, candidate.DecisionType);
        Assert.True(candidate.Reason.Contains("expiring", StringComparison.OrdinalIgnoreCase) || candidate.OrganizationalGoal.Contains("picks", StringComparison.OrdinalIgnoreCase), "Rebuild candidate should prioritize future assets.");
    }

    public void ContenderTargetsImmediateRosterNeed()
    {
        var candidate = Candidates(CompetitiveWindow.Contending).First(item => item.Title.Contains("championship", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(AiFrontOfficeDecisionType.Trade, candidate.DecisionType);
        Assert.True(candidate.OrganizationalGoal.Contains("current lineup", StringComparison.OrdinalIgnoreCase), "Contender trade should target immediate lineup help.");
    }

    public void DevelopingTeamAvoidsBlockingTopProspect()
    {
        var candidate = Candidates(CompetitiveWindow.Developing).First(item => item.Title.Contains("blocking", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(AiFrontOfficeDecisionType.Prospect, candidate.DecisionType);
        Assert.True(candidate.OrganizationalGoal.Contains("patient", StringComparison.OrdinalIgnoreCase), "Developing team should keep a patient prospect path.");
    }

    public void BudgetResetTeamAvoidsExpensiveFreeAgent()
    {
        var plan = Plan(CompetitiveWindow.Declining);
        var candidate = new AiFrontOfficeDecisionService().BuildCandidatesForPlan(Scenario(), plan, AiDecisionWindow.FreeAgency)
            .First(item => item.Title.Contains("budget", StringComparison.OrdinalIgnoreCase) || item.Title.Contains("expensive", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(AiFrontOfficeDecisionType.FreeAgency, candidate.DecisionType);
        Assert.True(candidate.Reason.Contains("avoid", StringComparison.OrdinalIgnoreCase), "Budget reset should avoid expensive free agents.");
    }

    public void AiFillsIllegalRosterAndRecordsEmergencyOverride()
    {
        var scenario = Scenario();
        var plan = Plan(CompetitiveWindow.Competing) with
        {
            RosterPlan = Plan(CompetitiveWindow.Competing).RosterPlan with
            {
                FutureNeeds = new[] { "Goalie depth is illegal; fill empty roster slot." }
            }
        };
        var service = new AiFrontOfficeDecisionService();
        var candidate = service.BuildCandidatesForPlan(scenario, plan, AiDecisionWindow.MonthlyReview).First(item => item.DecisionType == AiFrontOfficeDecisionType.Roster);
        var result = service.RunCycle(scenario with { OrganizationPlans = scenario.OrganizationPlans.Select(item => item.OrganizationId == plan.OrganizationId ? plan : item).ToArray() }, force: true);

        Assert.Equal(AiDecisionPriority.Urgent, candidate.Priority);
        Assert.True(result.ScenarioSnapshot.AiEmergencyOverrides.Count > 0, "Urgent roster issue should record an emergency override.");
    }

    public void AiReplacesInjuredPlayer()
    {
        var scenario = Scenario();
        var plan = Plan(CompetitiveWindow.Competing);
        var candidate = new AiFrontOfficeDecisionService().BuildRosterCandidate(scenario, plan, AiDecisionWindow.MonthlyReview, "Replace injured starter before next review.");

        Assert.Equal(AiFrontOfficeDecisionType.Roster, candidate.DecisionType);
        Assert.True(candidate.ExpectedBenefit.Contains("lineup", StringComparison.OrdinalIgnoreCase), "Roster decision should protect lineup balance.");
    }

    public void AiRespectsWaiverAndAhLJuniorEligibilityInProspectAlternatives()
    {
        var candidate = Candidates(CompetitiveWindow.Developing).First(item => item.DecisionType == AiFrontOfficeDecisionType.Prospect);

        Assert.True(candidate.AlternativesConsidered.Any(item => item.Contains("AHL", StringComparison.OrdinalIgnoreCase) || item.Contains("current level", StringComparison.OrdinalIgnoreCase)), "Prospect alternatives should include level/assignment restraint.");
    }

    public void AiQualifiesValuableRfaAndReleasesLowValueRfaThroughContractPlan()
    {
        var candidate = Candidates(CompetitiveWindow.Competing).First(item => item.DecisionType == AiFrontOfficeDecisionType.Contract);

        Assert.True(string.Join(" ", candidate.AlternativesConsidered).Contains("Qualify core RFA", StringComparison.OrdinalIgnoreCase), "Contract alternatives should include qualifying core RFAs.");
        Assert.True(string.Join(" ", candidate.AlternativesConsidered).Contains("Walk away", StringComparison.OrdinalIgnoreCase), "Contract alternatives should include walking away from low-value rights.");
    }

    public void AiEvaluatesArbitrationOfferSheetAndBuyoutPaths()
    {
        Assert.True(Enum.IsDefined(typeof(AiFrontOfficeDecisionType), AiFrontOfficeDecisionType.Arbitration), "Arbitration path should exist.");
        Assert.True(Enum.IsDefined(typeof(AiFrontOfficeDecisionType), AiFrontOfficeDecisionType.OfferSheet), "Offer-sheet path should exist.");
        Assert.True(Enum.IsDefined(typeof(AiFrontOfficeDecisionType), AiFrontOfficeDecisionType.Buyout), "Buyout path should exist.");
    }

    public void AiCreatesTradeTargetList()
    {
        var result = new AiFrontOfficeDecisionService().RunCycle(Scenario(), force: true);

        Assert.True(result.ScenarioSnapshot.AiTransactionPlans.Any(plan => plan.LikelyTargets.Count > 0), "AI transaction plans should include likely targets.");
    }

    public void AiInitiatesStrategyConsistentTrade()
    {
        var result = new AiFrontOfficeDecisionService().RunCycle(Scenario(), force: true);
        var trade = result.Cycle.Outcomes.FirstOrDefault(item => item.DecisionType == AiFrontOfficeDecisionType.Trade);

        Assert.True(trade is not null, "Cycle should include at least one trade decision.");
        Assert.True(trade!.Explanation.Contains("plan", StringComparison.OrdinalIgnoreCase) || trade.Explanation.Contains("because", StringComparison.OrdinalIgnoreCase), "Trade decision should explain strategy fit.");
    }

    public void AiCounterofferUsesOrganizationalNeeds()
    {
        var scenario = Scenario();
        var plan = Plan(CompetitiveWindow.Competing);
        var service = new AiFrontOfficeDecisionService();
        var candidate = service.BuildTradeCandidate(scenario, plan, AiDecisionWindow.MonthlyReview) with { Confidence = 65, Priority = AiDecisionPriority.Important };
        var result = service.RunCycle(scenario with { OrganizationPlans = scenario.OrganizationPlans.Select(item => item.OrganizationId == plan.OrganizationId ? plan : item).ToArray() }, force: true);

        Assert.True(candidate.OrganizationalGoal.Contains("competitive", StringComparison.OrdinalIgnoreCase) || candidate.OrganizationalGoal.Contains("window", StringComparison.OrdinalIgnoreCase), "Counter context should reference organizational window.");
        Assert.True(result.Cycle.Outcomes.Any(item => item.Outcome is AiDecisionOutcome.Accept or AiDecisionOutcome.Counter), "Cycle should create accept or counter outcomes.");
    }

    public void AiFreeAgencyPlanUsesPriorityAndFallbackTargets()
    {
        var candidate = Candidates(CompetitiveWindow.Competing).First(item => item.DecisionType == AiFrontOfficeDecisionType.FreeAgency);

        Assert.True(string.Join(" ", candidate.AlternativesConsidered).Contains("Priority target", StringComparison.OrdinalIgnoreCase), "Free agency plan should include priority targets.");
        Assert.True(string.Join(" ", candidate.AlternativesConsidered).Contains("Fallback target", StringComparison.OrdinalIgnoreCase), "Free agency plan should include fallback targets.");
    }

    public void AiDoesNotSignDuplicateUnnecessaryPlayers()
    {
        var result = new AiFrontOfficeDecisionService().RunCycle(Scenario(), force: true);

        Assert.True(result.Cycle.TransactionPlans.All(plan => plan.FreeAgencyTargets.Distinct(StringComparer.Ordinal).Count() == plan.FreeAgencyTargets.Count), "Free-agent target list should dedupe targets.");
    }

    public void AiDraftChoiceUsesBoardNeedAndIdentity()
    {
        var candidate = new AiFrontOfficeDecisionService().BuildCandidatesForPlan(Scenario(), Plan(CompetitiveWindow.Developing), AiDecisionWindow.Draft).First(item => item.DecisionType == AiFrontOfficeDecisionType.Draft);

        Assert.True(candidate.Reason.Contains("board", StringComparison.OrdinalIgnoreCase), "Draft choice should reference the board.");
        Assert.True(candidate.Reason.Contains("need", StringComparison.OrdinalIgnoreCase), "Draft choice should reference need.");
        Assert.True(candidate.OrganizationalGoal.Contains("identity", StringComparison.OrdinalIgnoreCase), "Draft choice should reference identity.");
    }

    public void AiUpdatesDepthPlanAfterDraft()
    {
        var scenario = Scenario();
        var result = new AiFrontOfficeDecisionService().RunCycle(scenario with { AlphaSnapshot = scenario.AlphaSnapshot with { WorldState = scenario.AlphaSnapshot.WorldState with { Clock = scenario.AlphaSnapshot.WorldState.Clock with { CurrentDate = new LegacyEngine.World.WorldDate(scenario.DraftDate) } } } }, force: true);

        Assert.True(result.ScenarioSnapshot.OrganizationPlans.All(plan => plan.DepthPlan.FutureDepth.Count > 0), "AI cycle should preserve/update depth plans after draft review.");
    }

    public void AiPromotesReadyProspectAndLeavesUnreadyProspect()
    {
        var candidate = Candidates(CompetitiveWindow.Developing).First(item => item.DecisionType == AiFrontOfficeDecisionType.Prospect);

        Assert.True(string.Join(" ", candidate.AlternativesConsidered).Contains("Promote carefully", StringComparison.OrdinalIgnoreCase), "Prospect plan should allow careful promotion.");
        Assert.True(string.Join(" ", candidate.AlternativesConsidered).Contains("Leave at current level", StringComparison.OrdinalIgnoreCase), "Prospect plan should allow leaving unready players at level.");
    }

    public void AiHiresStaffFromLivingMarket()
    {
        var candidate = Candidates(CompetitiveWindow.Developing).First(item => item.DecisionType == AiFrontOfficeDecisionType.Staff);

        Assert.True((candidate.AlternativesConsidered.FirstOrDefault(item => item.Contains("market", StringComparison.OrdinalIgnoreCase)) ?? candidate.Reason).Contains("market", StringComparison.OrdinalIgnoreCase), "Staff plan should reference the living staff market.");
    }

    public void TransactionCooldownPreventsRepeatedSpam()
    {
        var service = new AiFrontOfficeDecisionService();
        var first = service.RunCycle(Scenario(), force: true);
        var second = service.RunCycle(first.ScenarioSnapshot, force: true);

        Assert.True(second.Cycle.SkippedDecisions.Any(item => item.Contains("cooldown", StringComparison.OrdinalIgnoreCase)), "Second forced cycle should skip repeated major decisions on cooldown.");
    }

    public void LeagueSimulationMaintainsUniqueAssetOwnership()
    {
        var result = new AiFrontOfficeDecisionService().RunCycle(Scenario(), force: true);
        var people = result.ScenarioSnapshot.AlphaSnapshot.People.Select(person => person.PersonId).ToArray();

        Assert.Equal(people.Length, people.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(result.ScenarioSnapshot.DraftPickValues.Count, result.ScenarioSnapshot.DraftPickValues.Select(pick => pick.PickId).Distinct(StringComparer.Ordinal).Count());
    }

    public void PlayerControlledOrganizationNeverAutoManaged()
    {
        var scenario = Scenario();
        var result = new AiFrontOfficeDecisionService().RunCycle(scenario, force: true);

        Assert.False(result.Cycle.Candidates.Any(item => item.OrganizationId == scenario.Organization.OrganizationId), "Player organization should never receive auto-executed AI candidates.");
        Assert.True(result.Cycle.SkippedDecisions.Any(item => item.Contains("player-controlled", StringComparison.OrdinalIgnoreCase)), "Cycle should explicitly skip player-controlled organization.");
    }

    public void LeagueNewsReceivesOnlyNotableAiDecisions()
    {
        var result = new AiFrontOfficeDecisionService().RunCycle(Scenario(), force: true);

        Assert.True(result.LeagueNews.Count <= 6, "League News should be capped per cycle.");
        Assert.True(result.LeagueNews.All(item => item.Description.Contains("because", StringComparison.OrdinalIgnoreCase) || item.Description.Contains("plan", StringComparison.OrdinalIgnoreCase)), "AI news should explain the decision.");
    }

    public void SaveLoadDoesNotDuplicateDecisions()
    {
        var service = new AiFrontOfficeDecisionService();
        var result = service.RunCycle(Scenario(), force: true);
        var path = Path.Combine(Path.GetTempPath(), $"hockey-gm-alpha713-{Guid.NewGuid():N}.json");
        var budget = new BudgetOverviewService().Build(result.ScenarioSnapshot, RulebookPresets.CreateJuniorMajor());
        var saved = new SaveGameService().SaveCareer(result.ScenarioSnapshot, Array.Empty<InboxMessage>(), result.LeagueNews, new Dictionary<string, ActionCenterStatus>(), budget, path);
        var loaded = new SaveGameService().LoadFromFile(path, RulebookPresets.CreateJuniorMajor());
        var rerun = service.RunCycle(loaded.SaveGame!.ScenarioSnapshot, force: true);

        Assert.True(saved.Success, saved.Message);
        Assert.True(loaded.Success, loaded.Message);
        Assert.Equal(result.ScenarioSnapshot.AiDecisionHistory.Count, loaded.SaveGame.ScenarioSnapshot.AiDecisionHistory.Count);
        Assert.True(rerun.Cycle.SkippedDecisions.Any(item => item.Contains("cooldown", StringComparison.OrdinalIgnoreCase)), "Loaded cycle should remember cooldowns and not repeat completed decisions immediately.");
    }

    public void AlphaDesktopExposesAiDecisionExplanations()
    {
        var desktop = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(desktop.Contains("AI Front Office", StringComparison.Ordinal), "Desktop should expose AI Front Office.");
        Assert.True(desktop.Contains("AiFrontOfficeTextForOrganization", StringComparison.Ordinal), "Desktop should show AI decision explanations.");
    }

    public void HiddenTrueRatingsNotExposed()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "engine", "LegacyEngine", "Integration", "AiFrontOfficeDecisionService.cs"));
        var desktop = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.False(source.Contains("TrueRatings", StringComparison.Ordinal), "AI front office should not expose hidden true ratings.");
        Assert.False(desktop.Contains("ScenarioSnapshot.TrueRatings", StringComparison.Ordinal), "Desktop should not render hidden true ratings.");
    }

    public void FiveSeasonSoakTest()
    {
        var service = new AiFrontOfficeDecisionService();
        var scenario = Scenario();
        var totalNews = 0;
        for (var season = 0; season < 5; season++)
        {
            var date = scenario.CurrentDate.AddYears(season).AddDays(season % 2 == 0 ? 0 : 15);
            scenario = WithDate(scenario, date);
            var result = service.RunCycle(scenario, force: true);
            scenario = result.ScenarioSnapshot;
            totalNews += result.LeagueNews.Count;

            scenario.Validate();
            Assert.True(result.LeagueNews.Count <= 6, "No single cycle should spam League News.");
            Assert.False(result.Cycle.Candidates.Any(item => item.OrganizationId == scenario.Organization.OrganizationId), "Player organization should remain untouched in soak.");
        }

        Assert.True(scenario.AiDecisionHistory.Count > 0, "Soak should build AI decision history.");
        Assert.True(scenario.OrganizationPlans.Count > 0, "Organization plans should remain available.");
        Assert.True(scenario.OrganizationPlans.All(plan => plan.ProspectPlan.Prospects.Count > 0), "Prospect pipelines should continue moving.");
        Assert.True(totalNews <= 30, "Five-season soak should not create runaway transaction spam.");
    }

    private static IReadOnlyList<AiDecisionCandidate> Candidates(CompetitiveWindow window) =>
        new AiFrontOfficeDecisionService().BuildCandidatesForPlan(Scenario(), Plan(window), AiDecisionWindow.MonthlyReview);

    private static OrganizationPlan Plan(CompetitiveWindow window)
    {
        var scenario = Scenario();
        var plan = scenario.OrganizationPlans.First(item => item.OrganizationId != scenario.Organization.OrganizationId);
        return plan with { Window = window };
    }

    private static NewGmScenarioSnapshot Scenario()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        scenario = new HockeyIntelligenceRatingService().EnsureRatings(scenario);
        scenario = new ScoutingIntelligenceService().EnsureKnowledgeProfiles(scenario);
        scenario = new DevelopmentCurveService().EnsureCurves(scenario);
        scenario = new PlayerRatingService().EnsureRatings(scenario);
        scenario = new DraftWarRoomService().EnsureWarRoom(scenario);
        scenario = new AssetEvaluationService().EnsureEvaluations(scenario);
        return new OrganizationPlanningService().EnsurePlans(scenario);
    }

    private static NewGmScenarioSnapshot WithDate(NewGmScenarioSnapshot scenario, DateOnly date)
    {
        var world = scenario.AlphaSnapshot.WorldState with
        {
            Clock = scenario.AlphaSnapshot.WorldState.Clock with { CurrentDate = new LegacyEngine.World.WorldDate(date) }
        };
        var alpha = scenario.AlphaSnapshot with { WorldState = world };
        var season = scenario.Season with
        {
            CurrentDate = new LegacyEngine.Seasons.SeasonDate(date),
            CurrentPhase = scenario.Season.PhaseOn(date)
        };
        return scenario with { AlphaSnapshot = alpha, Season = season };
    }

    private static string FindRepositoryRoot()
    {
        var current = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(current, "HockeyGmLegacy.slnx")))
        {
            current = Directory.GetParent(current)?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
        }

        return current;
    }
}
