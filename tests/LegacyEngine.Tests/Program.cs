using LegacyEngine.RuleEngine;

var tests = new RuleEngineTests();
var ownerTests = new OwnerEngineTests();
var scoutingTests = new ScoutingEngineTests();
var personTests = new PersonEngineTests();
var relationshipTests = new RelationshipEngineTests();
var eventTests = new EventEngineTests();
var worldTests = new WorldEngineTests();
var recruitingTests = new RecruitingEngineTests();
var contractTests = new ContractEngineTests();
var draftTests = new DraftEngineTests();
var rosterTests = new RosterEngineTests();
var developmentTests = new DevelopmentEngineTests();
var injuryTests = new InjuryEngineTests();
var alphaIntegrationTests = new AlphaIntegrationTests();
var humanIntelligenceTests = new HumanIntelligenceEngineTests();
var runner = new TestRunner();

runner.Run("junior_v1 rulebook loads", tests.JuniorRulebookLoads);
runner.Run("invalid rulebook is rejected", tests.InvalidRulebookIsRejected);
runner.Run("roster validator returns valid and invalid rule codes", tests.RosterValidator);
runner.Run("eligibility validator returns valid and invalid rule codes", tests.EligibilityValidator);
runner.Run("contract validator returns valid and invalid rule codes", tests.ContractValidator);
runner.Run("draft validator returns valid and invalid rule codes", tests.DraftValidator);
runner.Run("playoff validator returns valid and invalid rule codes", tests.PlayoffValidator);
runner.Run("budget validator returns valid and invalid rule codes", tests.BudgetValidator);
runner.Run("owner archetypes expose expected profiles", ownerTests.OwnerArchetypeProfiles);
runner.Run("owner model validates budgets, goals, scores, and autonomy", ownerTests.OwnerModelValidation);
runner.Run("owner can be assigned to an organization", ownerTests.OwnerAssignment);
runner.Run("owner evaluation can extend the GM", ownerTests.OwnerEvaluationExtendsGm);
runner.Run("owner evaluation can warn, final warn, or fire the GM", ownerTests.OwnerEvaluationConsequences);
runner.Run("owner evaluation applies trust confidence and patience changes", ownerTests.OwnerEvaluationAppliesRelationshipChanges);
runner.Run("scout model validates specialties and skill scores", scoutingTests.ScoutModelValidation);
runner.Run("scouting assignments validate targets focus and dates", scoutingTests.ScoutingAssignmentValidation);
runner.Run("scouting reports are imperfect and language based", scoutingTests.ScoutingReportsAreImperfect);
runner.Run("confidence levels respond to scout fit and GM scouting bonus", scoutingTests.ConfidenceLevelsUseScoutFitAndGmBonus);
runner.Run("player dossier stores required sections reports and GM notes", scoutingTests.PlayerDossierStructure);
runner.Run("person model creates and validates identity", personTests.CreatingAPerson);
runner.Run("person age calculation handles birthdays", personTests.CalculatingAge);
runner.Run("person can add a role", personTests.AddingARole);
runner.Run("person can end a role", personTests.EndingARole);
runner.Run("person can hold multiple roles", personTests.HoldingMultipleRoles);
runner.Run("person reputation changes are clamped and recorded", personTests.ReputationChanges);
runner.Run("person status changes are recorded", personTests.StatusChanges);
runner.Run("person career timeline entries are stored chronologically", personTests.CareerTimelineEntries);
runner.Run("relationship engine creates directional relationships", relationshipTests.CreateRelationship);
runner.Run("relationship reverse direction remains independent", relationshipTests.DirectionalityAndReverseIndependence);
runner.Run("relationship trust can increase and decrease", relationshipTests.TrustChanges);
runner.Run("relationship dimensions can change", relationshipTests.AllDimensionsChange);
runner.Run("relationship values clamp between zero and one hundred", relationshipTests.ValueClamping);
runner.Run("relationship changes update last interaction and history", relationshipTests.LastInteractionAndHistory);
runner.Run("relationship stores multiple history entries", relationshipTests.MultipleHistoryEntries);
runner.Run("relationship decay moves trust toward neutral", relationshipTests.DecayMovesTrustTowardNeutral);
runner.Run("relationship decay reduces friendship and rivalry slowly", relationshipTests.DecayReducesFriendshipAndRivalry);
runner.Run("inactive relationship does not change by default", relationshipTests.InactiveRelationshipDoesNotChangeByDefault);
runner.Run("event engine creates events with unique IDs", eventTests.CreateEventsWithUniqueIds);
runner.Run("event stores date type severity visibility status context and metadata", eventTests.EventStoresRequiredFields);
runner.Run("event queue stores queued events", eventTests.QueueEvents);
runner.Run("event engine processes queued events in date order", eventTests.ProcessQueuedEventsInDateOrder);
runner.Run("event engine marks processed events and archives history", eventTests.ProcessAndArchiveEvents);
runner.Run("event history can query by person organization type and date range", eventTests.QueryEventHistory);
runner.Run("event engine does not mutate external domain state", eventTests.EventEngineDoesNotMutateExternalDomainState);
runner.Run("world engine creates a world with unique id", worldTests.WorldCreation);
runner.Run("world engine stores current date", worldTests.CurrentDateStored);
runner.Run("world engine advances one day", worldTests.AdvanceOneDay);
runner.Run("world engine advances multiple days", worldTests.AdvanceMultipleDays);
runner.Run("world engine updates season year", worldTests.SeasonYearUpdates);
runner.Run("world engine phase can be set", worldTests.PhaseCanBeSet);
runner.Run("world engine returns daily simulation result", worldTests.DailySimulationResultReturned);
runner.Run("world engine processes queued events", worldTests.EventQueueProcessingIsCalled);
runner.Run("world engine does not mutate external domain state", worldTests.WorldEngineDoesNotMutateExternalDomainState);
runner.Run("recruit profile can be created", recruitingTests.RecruitProfileCreation);
runner.Run("recruit starts as available", recruitingTests.RecruitStartsAsAvailable);
runner.Run("recruit interest can increase and decrease", recruitingTests.InterestCanIncreaseAndDecrease);
runner.Run("recruiting offer changes status to offered", recruitingTests.OfferChangesStatusToOffered);
runner.Run("recruiting visit can be recorded", recruitingTests.VisitCanBeRecorded);
runner.Run("recruiting promise can be added", recruitingTests.PromiseCanBeAdded);
runner.Run("recruiting decision can commit", recruitingTests.DecisionCanCommit);
runner.Run("recruiting decision can reject", recruitingTests.DecisionCanReject);
runner.Run("higher interest improves recruiting decision score", recruitingTests.HigherInterestImprovesDecisionScore);
runner.Run("promises influence recruiting decision", recruitingTests.PromisesInfluenceDecision);
runner.Run("recruiting creates events for offer commit and reject", recruitingTests.EventsCreatedForOfferCommitAndReject);
runner.Run("recruiting does not modify roster state", recruitingTests.NoRosterModificationOccurs);
runner.Run("contract offer can be created", contractTests.ContractOfferCreation);
runner.Run("contract can be signed", contractTests.ContractSigning);
runner.Run("contract can be rejected", contractTests.ContractRejection);
runner.Run("contract can be terminated", contractTests.ContractTermination);
runner.Run("contract status changes are tracked", contractTests.StatusChanges);
runner.Run("contract start and end dates are tracked", contractTests.StartAndEndDateTracking);
runner.Run("contract money and stipend are tracked", contractTests.MoneyAndStipendTracking);
runner.Run("contract allowed clause is accepted", contractTests.AllowedClauseAccepted);
runner.Run("contract disallowed clause is rejected by rule engine", contractTests.DisallowedClauseRejectedWhenRuleEngineSaysNo);
runner.Run("junior education package clause works", contractTests.JuniorEducationPackageClauseWorks);
runner.Run("junior housing support clause works", contractTests.JuniorHousingSupportClauseWorks);
runner.Run("contract events are created", contractTests.EventsCreatedForOfferSignRejectTerminate);
runner.Run("contracts do not modify roster state", contractTests.NoRosterModificationOccurs);
runner.Run("draft can be created", draftTests.DraftCreation);
runner.Run("draft order generated from reverse standings", draftTests.DraftOrderGeneratedFromReverseStandings);
runner.Run("draft creates correct number of picks", draftTests.CorrectNumberOfPicksCreated);
runner.Run("draft valid selection succeeds", draftTests.ValidSelectionSucceeds);
runner.Run("draft prevents duplicate prospect selection", draftTests.SameProspectCannotBeSelectedTwice);
runner.Run("draft invalid round fails", draftTests.InvalidRoundFails);
runner.Run("draft selection after completion fails", draftTests.SelectionAfterCompletionFails);
runner.Run("draft can be marked completed", draftTests.DraftCanBeMarkedCompleted);
runner.Run("draft board can add prospect", draftTests.DraftBoardCanAddProspect);
runner.Run("draft board can update rank", draftTests.DraftBoardCanUpdateRank);
runner.Run("draft board can remove prospect", draftTests.DraftBoardCanRemoveProspect);
runner.Run("draft board entry can reference scouting report", draftTests.DraftBoardEntryCanReferenceScoutingReport);
runner.Run("draft events are created", draftTests.EventsCreatedForStartSelectionCompletion);
runner.Run("draft respects rule engine validation", draftTests.RuleEngineValidationIsRespected);
runner.Run("draft does not modify roster state", draftTests.NoRosterModificationOccurs);
runner.Run("roster can be created", rosterTests.RosterCreation);
runner.Run("roster can add player", rosterTests.AddPlayer);
runner.Run("roster can remove player", rosterTests.RemovePlayer);
runner.Run("roster rejects duplicate active player", rosterTests.DuplicateActivePlayerRejected);
runner.Run("roster can move player to reserve", rosterTests.MovePlayerToReserve);
runner.Run("roster can move player to injured reserve", rosterTests.MovePlayerToInjuredReserve);
runner.Run("roster can release player", rosterTests.ReleasePlayer);
runner.Run("roster stores released date", rosterTests.ReleasedDateStored);
runner.Run("roster validates max roster size", rosterTests.ValidateMaxRosterSize);
runner.Run("roster validates active roster size", rosterTests.ValidateActiveRosterSize);
runner.Run("roster validates goalie requirement", rosterTests.ValidateGoalieRequirement);
runner.Run("roster validates overage slots", rosterTests.ValidateOverageSlots);
runner.Run("roster validates import slots", rosterTests.ValidateImportSlots);
runner.Run("roster creates events for add remove IR and release", rosterTests.EventsCreatedForAddRemoveIrRelease);
runner.Run("development profile can be created", developmentTests.DevelopmentProfileCreation);
runner.Run("development monthly update changes attributes", developmentTests.MonthlyUpdateChangesAttributes);
runner.Run("high work ethic improves development", developmentTests.HighWorkEthicImprovesDevelopment);
runner.Run("low coachability slows development", developmentTests.LowCoachabilitySlowsDevelopment);
runner.Run("confidence affects development", developmentTests.ConfidenceAffectsDevelopment);
runner.Run("injury penalty reduces development", developmentTests.InjuryPenaltyReducesDevelopment);
runner.Run("facility bonus improves development", developmentTests.FacilityBonusImprovesDevelopment);
runner.Run("coaching bonus improves development", developmentTests.CoachingBonusImprovesDevelopment);
runner.Run("current ability cannot exceed potential", developmentTests.CurrentAbilityCannotExceedPotential);
runner.Run("veteran and declining stages can regress", developmentTests.VeteranDecliningStageCanRegress);
runner.Run("development result includes summary", developmentTests.DevelopmentResultIncludesSummary);
runner.Run("development update creates event", developmentTests.EventsCreatedForDevelopmentUpdate);
runner.Run("development breakout event can be created", developmentTests.BreakoutEventCanBeCreated);
runner.Run("development regression event can be created", developmentTests.RegressionEventCanBeCreated);
runner.Run("development does not expose true ratings in dossier-facing output", developmentTests.TrueRatingsAreNotExposedInDossierFacingOutput);
runner.Run("development module has no UI or Godot dependency", developmentTests.NoUiOrGodotDependencyExists);
runner.Run("injury can be created", injuryTests.InjuryCreation);
runner.Run("injury severity is stored", injuryTests.SeverityStored);
runner.Run("injury body part is stored", injuryTests.BodyPartStored);
runner.Run("injury expected return date is calculated and stored", injuryTests.ExpectedReturnDateCalculatedAndStored);
runner.Run("injury recovery update changes status", injuryTests.RecoveryUpdateChangesStatus);
runner.Run("injury can be cleared", injuryTests.InjuryCanBeCleared);
runner.Run("injury actual return date is stored", injuryTests.ActualReturnDateIsStored);
runner.Run("injury games missed can increase", injuryTests.GamesMissedCanIncrease);
runner.Run("injury re-aggravation changes status", injuryTests.ReAggravationChangesStatus);
runner.Run("injury recurrence risk is tracked", injuryTests.RecurrenceRiskIsTracked);
runner.Run("injury long-term impact is tracked", injuryTests.LongTermImpactIsTracked);
runner.Run("career-threatening injury can be marked", injuryTests.CareerThreateningInjuryCanBeMarked);
runner.Run("injury result includes summary", injuryTests.InjuryResultIncludesSummary);
runner.Run("injury events are created", injuryTests.EventsCreatedForInjuryRecoveryReAggravationCareerThreatening);
runner.Run("injury does not move roster automatically", injuryTests.NoRosterMovementOccursAutomatically);
runner.Run("injury module has no UI or Godot dependency", injuryTests.NoUiOrGodotDependencyExists);
runner.Run("alpha world can be bootstrapped", alphaIntegrationTests.BootstrapAlphaWorld);
runner.Run("alpha world has people", alphaIntegrationTests.AlphaWorldHasPeople);
runner.Run("alpha roster exists", alphaIntegrationTests.AlphaRosterExists);
runner.Run("alpha recruits exist", alphaIntegrationTests.AlphaRecruitsExist);
runner.Run("alpha scout exists", alphaIntegrationTests.AlphaScoutExists);
runner.Run("alpha owner exists", alphaIntegrationTests.AlphaOwnerExists);
runner.Run("alpha draft board exists", alphaIntegrationTests.AlphaDraftBoardExists);
runner.Run("alpha daily simulation advances date", alphaIntegrationTests.AdvanceOneDayAdvancesDate);
runner.Run("alpha simulation result is returned", alphaIntegrationTests.SimulationResultIsReturned);
runner.Run("alpha daily simulation processes event queue", alphaIntegrationTests.EventQueueIsProcessed);
runner.Run("alpha inbox items can be generated", alphaIntegrationTests.InboxItemsCanBeGenerated);
runner.Run("alpha registry shares event engine", alphaIntegrationTests.RegistrySharesEventEngine);
runner.Run("alpha integration layer has no UI or Godot dependency", alphaIntegrationTests.IntegrationLayerHasNoUiOrGodotDependency);
runner.Run("human intelligence decision option can be created", humanIntelligenceTests.DecisionOptionCreation);
runner.Run("human intelligence decision context can be created", humanIntelligenceTests.DecisionContextCreation);
runner.Run("human intelligence factor weighting affects score", humanIntelligenceTests.FactorWeighting);
runner.Run("human intelligence selects highest scored option", humanIntelligenceTests.HighestScoredOptionSelected);
runner.Run("human intelligence returns ranked options", humanIntelligenceTests.RankedOptionsReturned);
runner.Run("human intelligence personality affects score", humanIntelligenceTests.PersonalityAffectsScore);
runner.Run("human intelligence relationship trust affects score", humanIntelligenceTests.RelationshipTrustAffectsScore);
runner.Run("human intelligence risk tolerance improves risky option", humanIntelligenceTests.HighRiskToleranceImprovesRiskyOptionScore);
runner.Run("human intelligence loyalty improves loyal option", humanIntelligenceTests.HighLoyaltyImprovesLoyalOptionScore);
runner.Run("human intelligence returns plain-language reasons", humanIntelligenceTests.PlainLanguageReasonsReturned);
runner.Run("human intelligence deterministic tests are stable", humanIntelligenceTests.DeterministicTestsAreStable);
runner.Run("human intelligence module has no UI or Godot dependency", humanIntelligenceTests.NoUiOrGodotDependencyExists);

runner.Report();
Environment.ExitCode = runner.FailedCount == 0 ? 0 : 1;

internal sealed class RuleEngineTests
{
    private readonly Rulebook _rulebook = new RulebookLoader().LoadFromFile(Path.Combine(FindRepositoryRoot(), "data", "rulebooks", "junior_v1.json"));

    public void JuniorRulebookLoads()
    {
        Assert.Equal("junior_v1", _rulebook.RulebookId);
        Assert.Equal("junior", _rulebook.LeagueType);
        Assert.Equal(18, _rulebook.RosterRules!.MinRoster);
        Assert.Contains("junior_player_agreement", _rulebook.ContractRules!.AllowedContractTypes);
    }

    public void InvalidRulebookIsRejected()
    {
        var json = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "data", "rulebooks", "junior_v1.json"));
        var invalidJson = json.Replace("\"roster_rules\"", "\"missing_roster_rules\"", StringComparison.Ordinal);

        var exception = Assert.Throws<RulebookLoadException>(() => new RulebookLoader().LoadFromJson(invalidJson));
        Assert.Equal(RuleErrorCodes.MissingRulebookSection, exception.Result.RuleCode);
    }

    public void RosterValidator()
    {
        var validator = new RosterRuleValidator(_rulebook);

        Assert.Valid(validator.Validate(new RosterValidationRequest(22, 20, 2, 3, 2)));
        Assert.Code(RuleErrorCodes.RosterTooSmall, validator.Validate(new RosterValidationRequest(17, 17, 2, 0, 0)));
        Assert.Code(RuleErrorCodes.RosterTooLarge, validator.Validate(new RosterValidationRequest(26, 20, 2, 0, 0)));
        Assert.Code(RuleErrorCodes.ActiveRosterTooLarge, validator.Validate(new RosterValidationRequest(22, 21, 2, 0, 0)));
        Assert.Code(RuleErrorCodes.NotEnoughGoalies, validator.Validate(new RosterValidationRequest(22, 20, 1, 0, 0)));
        Assert.Code(RuleErrorCodes.TooManyOveragePlayers, validator.Validate(new RosterValidationRequest(22, 20, 2, 4, 0)));
        Assert.Code(RuleErrorCodes.TooManyImportPlayers, validator.Validate(new RosterValidationRequest(22, 20, 2, 0, 3)));
    }

    public void EligibilityValidator()
    {
        var validator = new EligibilityRuleValidator(_rulebook);

        Assert.Valid(validator.Validate(new EligibilityValidationRequest(18)));
        Assert.Code(RuleErrorCodes.PlayerTooYoung, validator.Validate(new EligibilityValidationRequest(14)));
        Assert.Code(RuleErrorCodes.PlayerTooOld, validator.Validate(new EligibilityValidationRequest(21)));
    }

    public void ContractValidator()
    {
        var validator = new ContractRuleValidator(_rulebook);

        Assert.Valid(validator.Validate(new ContractValidationRequest(
            "junior_player_agreement",
            new[] { "junior_stipend", "education_package", "housing_support" })));

        Assert.Code(RuleErrorCodes.ContractTypeNotAllowed, validator.Validate(new ContractValidationRequest("entry_level_contract")));
        Assert.Code(RuleErrorCodes.ContractClauseNotAllowed, validator.Validate(new ContractValidationRequest("junior_player_agreement", new[] { "no_trade_clause" })));

        var cappedRulebook = WithContractRules(_rulebook, new ContractRules
        {
            AllowedContractTypes = _rulebook.ContractRules!.AllowedContractTypes,
            SalaryCapEnabled = true,
            SalaryCapAmount = 100,
            JuniorStipendsEnabled = true,
            EducationPackagesEnabled = true,
            HousingSupportEnabled = true
        });

        Assert.Code(
            RuleErrorCodes.SalaryCapExceeded,
            new ContractRuleValidator(cappedRulebook).Validate(new ContractValidationRequest("junior_player_agreement", TeamPayrollAfterSigning: 101)));
    }

    public void DraftValidator()
    {
        var validator = new DraftRuleValidator(_rulebook);

        Assert.Valid(validator.Validate(new DraftValidationRequest(1)));
        Assert.Code(RuleErrorCodes.InvalidDraftRound, validator.Validate(new DraftValidationRequest(9)));
        Assert.Code(RuleErrorCodes.PlayerNotDraftEligible, validator.Validate(new DraftValidationRequest(1, IsPlayerDraftEligible: false)));

        var noDraftRulebook = WithDraftRules(_rulebook, new DraftRules { DraftEnabled = false, Rounds = 0, DraftOrder = "none" });
        Assert.Code(RuleErrorCodes.DraftDisabled, new DraftRuleValidator(noDraftRulebook).Validate(new DraftValidationRequest(1)));
    }

    public void PlayoffValidator()
    {
        var validator = new PlayoffRuleValidator(_rulebook);

        Assert.Valid(validator.Validate(new PlayoffValidationRequest(8, 16)));
        Assert.Code(RuleErrorCodes.PlayoffTeamsInvalid, validator.Validate(new PlayoffValidationRequest(10, 16)));
        Assert.Code(RuleErrorCodes.PlayoffTeamsInvalid, validator.Validate(new PlayoffValidationRequest(8, 6)));
    }

    public void BudgetValidator()
    {
        var validator = new BudgetRuleValidator(_rulebook);

        Assert.Valid(validator.Validate(new BudgetValidationRequest(75_000, 100_000)));
        Assert.Code(RuleErrorCodes.BudgetExceeded, validator.Validate(new BudgetValidationRequest(125_000, 100_000)));

        var hardCapRulebook = WithBudgetRules(_rulebook, new BudgetRules
        {
            OwnerBudgetEnabled = true,
            HardSalaryCapEnabled = true,
            HardSalaryCapAmount = 100
        });

        Assert.Code(
            RuleErrorCodes.SalaryCapExceeded,
            new BudgetRuleValidator(hardCapRulebook).Validate(new BudgetValidationRequest(50, 100, TeamPayroll: 101)));
    }

    private static Rulebook WithContractRules(Rulebook source, ContractRules contractRules) =>
        new()
        {
            RulebookId = source.RulebookId,
            LeagueType = source.LeagueType,
            Version = source.Version,
            RosterRules = source.RosterRules,
            EligibilityRules = source.EligibilityRules,
            ContractRules = contractRules,
            DraftRules = source.DraftRules,
            PlayoffRules = source.PlayoffRules,
            BudgetRules = source.BudgetRules
        };

    private static Rulebook WithDraftRules(Rulebook source, DraftRules draftRules) =>
        new()
        {
            RulebookId = source.RulebookId,
            LeagueType = source.LeagueType,
            Version = source.Version,
            RosterRules = source.RosterRules,
            EligibilityRules = source.EligibilityRules,
            ContractRules = source.ContractRules,
            DraftRules = draftRules,
            PlayoffRules = source.PlayoffRules,
            BudgetRules = source.BudgetRules
        };

    private static Rulebook WithBudgetRules(Rulebook source, BudgetRules budgetRules) =>
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
            BudgetRules = budgetRules
        };

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var rulebookPath = Path.Combine(directory.FullName, "data", "rulebooks", "junior_v1.json");
            if (File.Exists(rulebookPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find Hockey GM Legacy repository root.");
    }
}

internal sealed class TestRunner
{
    private readonly List<string> _failures = [];

    public int FailedCount => _failures.Count;

    public void Run(string name, Action test)
    {
        try
        {
            test();
            Console.WriteLine($"PASS {name}");
        }
        catch (Exception ex)
        {
            _failures.Add($"{name}: {ex.Message}");
            Console.WriteLine($"FAIL {name}");
            Console.WriteLine($"     {ex.Message}");
        }
    }

    public void Report()
    {
        Console.WriteLine();
        Console.WriteLine(_failures.Count == 0
            ? "All LegacyEngine tests passed."
            : $"{_failures.Count} LegacyEngine test(s) failed.");
    }
}

internal static class Assert
{
    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
        }
    }

    public static void Contains(string expected, IEnumerable<string> values)
    {
        if (!values.Contains(expected, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Expected collection to contain '{expected}'.");
        }
    }

    public static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void False(bool condition, string message)
    {
        if (condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void Valid(RuleValidationResult result)
    {
        if (!result.IsValid)
        {
            throw new InvalidOperationException($"Expected VALID, got '{result.RuleCode}': {result.Message}");
        }
    }

    public static void Code(string expectedCode, RuleValidationResult result)
    {
        if (result.IsValid || result.RuleCode != expectedCode)
        {
            throw new InvalidOperationException($"Expected '{expectedCode}', got '{result.RuleCode}': {result.Message}");
        }
    }

    public static TException Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException ex)
        {
            return ex;
        }

        throw new InvalidOperationException($"Expected exception '{typeof(TException).Name}'.");
    }
}
