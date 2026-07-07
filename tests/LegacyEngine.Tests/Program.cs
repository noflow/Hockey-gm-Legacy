using LegacyEngine.RuleEngine;

var tests = new RuleEngineTests();
var ownerTests = new OwnerEngineTests();
var scoutingTests = new ScoutingEngineTests();
var personTests = new PersonEngineTests();
var relationshipTests = new RelationshipEngineTests();
var eventTests = new EventEngineTests();
var worldTests = new WorldEngineTests();
var recruitingTests = new RecruitingEngineTests();
var recruitingV2Tests = new RecruitingV2Tests();
var contractTests = new ContractEngineTests();
var draftTests = new DraftEngineTests();
var rosterTests = new RosterEngineTests();
var developmentTests = new DevelopmentEngineTests();
var injuryTests = new InjuryEngineTests();
var alphaIntegrationTests = new AlphaIntegrationTests();
var newGmScenarioTests = new NewGmScenarioTests();
var gmCreationAndActionsTests = new GmCreationAndActionsTests();
var inboxManagerTests = new InboxManagerTests();
var humanIntelligenceTests = new HumanIntelligenceEngineTests();
var communicationTests = new CommunicationEngineTests();
var staffTests = new StaffEngineTests();
var organizationTests = new OrganizationEngineTests();
var seasonTests = new SeasonEngineTests();
var alphaDraftExperienceTests = new AlphaDraftExperienceTests();
var developmentInboxPolicyTests = new DevelopmentInboxPolicyTests();
var ahlAffiliateRulebookTests = new AhlAffiliateRulebookTests();
var trainingCampTests = new TrainingCampTests();
var pendingGmActionTests = new PendingGmActionTests();
var prospectDecisionTests = new ProspectDecisionTests();
var seasonReadinessTests = new SeasonReadinessTests();
var executiveReportTests = new ExecutiveReportTests();
var scoutingOperationsTests = new ScoutingOperationsTests();
var playerDossierIntegrationTests = new PlayerDossierIntegrationTests();
var staffControlTests = new StaffControlTests();
var alphaDesktopInteractionTests = new AlphaDesktopInteractionTests();
var budgetOverviewTests = new BudgetOverviewTests();
var alpha231DedupHiringTests = new Alpha231DedupHiringTests();
var alpha24StaffBudgetTests = new Alpha24StaffBudgetTests();
var alpha25SeasonFrameworkTests = new Alpha25SeasonFrameworkTests();
var alpha26GameRecapStatsPolishTests = new Alpha26GameRecapStatsPolishTests();
var alpha27FirstMonthPlayabilityTests = new Alpha27FirstMonthPlayabilityTests();
var alpha271RosterFrontOfficeRealismTests = new Alpha271RosterFrontOfficeRealismTests();
var alpha272InboxCleanupTransactionWireTests = new Alpha272InboxCleanupTransactionWireTests();
var alpha273DraftStaffLayoutTests = new Alpha273DraftStaffLayoutTests();
var alpha28GmOfficeNavigationTests = new Alpha28GmOfficeNavigationTests();
var alpha29ActionCenterTests = new Alpha29ActionCenterTests();
var alpha30ExistingWorldHistoryTests = new Alpha30ExistingWorldHistoryTests();
var alpha31FreeAgentMarketTests = new Alpha31FreeAgentMarketTests();
var alpha32TradeEngineTests = new Alpha32TradeEngineTests();
var alpha33TradeDeadlineTests = new Alpha33TradeDeadlineTests();
var alpha34CareerHistoryTests = new Alpha34CareerHistoryTests();
var alpha35SaveLoadTests = new Alpha35SaveLoadTests();
var alpha40MultiSeasonTests = new Alpha40MultiSeasonTests();
var alpha41ContractsV2Tests = new Alpha41ContractsV2Tests();
var alpha42FreeAgencyV2Tests = new Alpha42FreeAgencyV2Tests();
var alpha43TradeEngineV2Tests = new Alpha43TradeEngineV2Tests();
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
runner.Run("recruiting v2 profile includes priorities", recruitingV2Tests.RecruitProfileIncludesPriorities);
runner.Run("recruiting v2 profile includes family priorities", recruitingV2Tests.RecruitProfileIncludesFamilyPriorities);
runner.Run("recruiting v2 recruit has competing teams", recruitingV2Tests.RecruitHasCompetingTeams);
runner.Run("recruiting v2 call recruit changes interest and trust", recruitingV2Tests.CallRecruitChangesInterestAndTrust);
runner.Run("recruiting v2 call family can affect interest", recruitingV2Tests.CallFamilyCanAffectInterest);
runner.Run("recruiting v2 visit can be accepted or declined", recruitingV2Tests.VisitCanBeAcceptedOrDeclined);
runner.Run("recruiting v2 promise is recorded", recruitingV2Tests.PromiseIsRecorded);
runner.Run("recruiting v2 promise affects interest and trust", recruitingV2Tests.PromiseAffectsInterestAndTrust);
runner.Run("recruiting v2 offer can be withdrawn", recruitingV2Tests.OfferCanBeWithdrawn);
runner.Run("recruiting v2 decision explanation is generated", recruitingV2Tests.DecisionExplanationIsGenerated);
runner.Run("recruiting v2 competing team can win recruit", recruitingV2Tests.CompetingTeamCanWinRecruit);
runner.Run("recruiting v2 human intelligence affects decision", recruitingV2Tests.HumanIntelligenceAffectsDecision);
runner.Run("recruiting v2 inbox messages are created", recruitingV2Tests.InboxMessagesAreCreated);
runner.Run("AlphaDesktop exposes recruiting v2 actions", recruitingV2Tests.AlphaDesktopExposesRecruitingActions);
runner.Run("AlphaDesktop opens dossier from recruit", recruitingV2Tests.DossierOpensFromRecruit);
runner.Run("recruiting v2 has no Godot save or game simulation dependency", recruitingV2Tests.RecruitingV2HasNoGodotSaveOrGameSimulationDependency);
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
runner.Run("alpha bootstrap creates pipeline world objects", alphaIntegrationTests.BootstrapCreatesPipelineWorldObjects);
runner.Run("alpha daily simulation advances date", alphaIntegrationTests.AdvanceOneDayAdvancesDate);
runner.Run("alpha simulation result is returned", alphaIntegrationTests.SimulationResultIsReturned);
runner.Run("alpha daily simulation advances multiple days", alphaIntegrationTests.AdvanceMultipleDaysChangesDateCorrectly);
runner.Run("alpha daily simulation processes event queue", alphaIntegrationTests.EventQueueIsProcessed);
runner.Run("alpha inbox items can be generated", alphaIntegrationTests.InboxItemsCanBeGenerated);
runner.Run("alpha relationship decay runs", alphaIntegrationTests.RelationshipDecayRuns);
runner.Run("alpha development update step runs", alphaIntegrationTests.DevelopmentUpdateStepRuns);
runner.Run("alpha injury recovery step runs", alphaIntegrationTests.InjuryRecoveryStepRuns);
runner.Run("alpha communication messages are generated", alphaIntegrationTests.CommunicationMessagesGeneratedFromImportantEvents);
runner.Run("alpha pipeline order is stable", alphaIntegrationTests.PipelineOrderIsStable);
runner.Run("alpha simulation result contains snapshot date log and inbox", alphaIntegrationTests.PipelineResultContainsSnapshotDateLogAndInbox);
runner.Run("alpha registry shares event engine", alphaIntegrationTests.RegistrySharesEventEngine);
runner.Run("alpha integration layer has no UI or Godot dependency", alphaIntegrationTests.IntegrationLayerHasNoUiOrGodotDependency);
runner.Run("new GM scenario starts two weeks before draft", newGmScenarioTests.ScenarioStartsTwoWeeksBeforeDraft);
runner.Run("new GM scenario player GM exists", newGmScenarioTests.PlayerGmExists);
runner.Run("new GM scenario organization exists", newGmScenarioTests.OrganizationExists);
runner.Run("new GM scenario owner exists", newGmScenarioTests.OwnerExists);
runner.Run("new GM scenario staff exists", newGmScenarioTests.StaffExists);
runner.Run("new GM scenario full roster exists", newGmScenarioTests.FullRosterExists);
runner.Run("new GM scenario recruit pool exists", newGmScenarioTests.RecruitPoolExists);
runner.Run("new GM scenario draft board exists", newGmScenarioTests.DraftBoardExists);
runner.Run("new GM scenario relationships exist", newGmScenarioTests.RelationshipsExist);
runner.Run("new GM scenario first-day inbox is populated", newGmScenarioTests.FirstDayInboxIsPopulated);
runner.Run("new GM scenario season phase and date are correct", newGmScenarioTests.SeasonPhaseAndDateAreCorrect);
runner.Run("new GM scenario advance day still works", newGmScenarioTests.AdvanceDayStillWorks);
runner.Run("new GM scenario playtest surfaces can read snapshot", newGmScenarioTests.PlaytestSurfacesCanReadScenarioSnapshot);
runner.Run("new GM scenario has no Godot dependency", newGmScenarioTests.ScenarioHasNoGodotDependency);
runner.Run("GM creation creates person", gmCreationAndActionsTests.GmCreationCreatesPerson);
runner.Run("created GM replaces default GM", gmCreationAndActionsTests.CreatedGmReplacesDefaultGm);
runner.Run("scenario uses created GM name", gmCreationAndActionsTests.ScenarioUsesCreatedGmName);
runner.Run("fallback GM still works", gmCreationAndActionsTests.FallbackGmStillWorks);
runner.Run("GM style maps to profile", gmCreationAndActionsTests.GmStyleMapsToHumanProfile);
runner.Run("draft board re-rank works", gmCreationAndActionsTests.DraftBoardRerankWorks);
runner.Run("scout focus assignment works", gmCreationAndActionsTests.ScoutFocusAssignmentWorks);
runner.Run("recruiting offer works", gmCreationAndActionsTests.RecruitingOfferWorks);
runner.Run("actions create events messages and inbox items", gmCreationAndActionsTests.ActionsCreateEventsMessagesAndInboxItems);
runner.Run("advance day still works after GM action", gmCreationAndActionsTests.AdvanceDayStillWorksAfterAction);
runner.Run("GM creation has no Godot dependency", gmCreationAndActionsTests.GmCreationHasNoGodotDependency);
runner.Run("inbox message categorized correctly", inboxManagerTests.MessageCategorizedCorrectly);
runner.Run("inbox read unread works", inboxManagerTests.ReadUnreadWorks);
runner.Run("inbox archive works", inboxManagerTests.ArchiveWorks);
runner.Run("inbox delete hides message", inboxManagerTests.DeleteHidesMessageFromInbox);
runner.Run("inbox pin works", inboxManagerTests.PinWorks);
runner.Run("inbox filters work", inboxManagerTests.FiltersWork);
runner.Run("inbox preserves event history", inboxManagerTests.EventHistoryIsPreserved);
runner.Run("AlphaDesktop can display inbox categories", inboxManagerTests.AlphaDesktopCanDisplayCategories);
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
runner.Run("communication message can be created", communicationTests.MessageCreation);
runner.Run("communication private message is delivered to its recipient", communicationTests.PrivateMessageDelivery);
runner.Run("communication organization-wide message reaches the organization", communicationTests.OrganizationWideMessage);
runner.Run("communication public message is visible publicly", communicationTests.PublicMessage);
runner.Run("communication rumor can be created", communicationTests.RumorCreation);
runner.Run("communication rumor confidence rises with corroboration", communicationTests.RumorConfidenceChanges);
runner.Run("communication knowledge item is stored per organization", communicationTests.KnowledgeItemStorage);
runner.Run("communication converts an event into a message", communicationTests.EventToMessageConversion);
runner.Run("communication converts important messages into inbox items", communicationTests.InboxItemConversion);
runner.Run("staff member can be created", staffTests.StaffCreation);
runner.Run("staff role can be assigned", staffTests.RoleAssignment);
runner.Run("staff role can be reassigned", staffTests.RoleReassignment);
runner.Run("staff member can be hired", staffTests.Hiring);
runner.Run("staff member can be released", staffTests.Releasing);
runner.Run("staff evaluation is generated", staffTests.EvaluationGeneration);
runner.Run("staff attributes are stored", staffTests.AttributeStorage);
runner.Run("staff department is assigned from role", staffTests.DepartmentAssignment);
runner.Run("staff contract is referenced only", staffTests.ContractReference);
runner.Run("staff behaviors create events", staffTests.EventCreation);
runner.Run("staff module has no UI or Godot dependency", staffTests.NoUiOrGodotDependencyExists);
runner.Run("organization can be created", organizationTests.OrganizationCreation);
runner.Run("organization identity fields are stored", organizationTests.IdentityFieldsStored);
runner.Run("organization owner reference is stored", organizationTests.OwnerReferenceStored);
runner.Run("organization league reference is stored", organizationTests.LeagueReferenceStored);
runner.Run("organization staff membership is added", organizationTests.StaffMembershipAdded);
runner.Run("organization staff membership is removed", organizationTests.StaffMembershipRemoved);
runner.Run("organization department is added", organizationTests.DepartmentAdded);
runner.Run("organization department is removed", organizationTests.DepartmentRemoved);
runner.Run("organization culture values are stored", organizationTests.CultureValuesStored);
runner.Run("organization reputation values are stored", organizationTests.ReputationValuesStored);
runner.Run("organization status can change", organizationTests.StatusChange);
runner.Run("organization behaviors create events", organizationTests.EventsCreated);
runner.Run("organization module has no UI or Godot dependency", organizationTests.NoUiOrGodotDependencyExists);
runner.Run("season can be created", seasonTests.SeasonCreation);
runner.Run("season phase changes as it advances", seasonTests.PhaseChanges);
runner.Run("season advances its date", seasonTests.DateAdvancement);
runner.Run("season detects milestone dates", seasonTests.MilestoneDetection);
runner.Run("leagues keep independent calendars", seasonTests.IndependentLeagueCalendars);
runner.Run("season generates lifecycle events", seasonTests.EventGeneration);
runner.Run("season dates come from the rulebook", seasonTests.RuleDrivenDates);
runner.Run("season result describes what changed", seasonTests.SeasonResultContents);
runner.Run("world engine can ask the current season phase", seasonTests.WorldEngineCanAskSeasonPhase);
runner.Run("season module has no UI or Godot dependency", seasonTests.NoUiOrGodotDependencyExists);
runner.Run("alpha draft rulebook length comes from junior rulebook", alphaDraftExperienceTests.RulebookDraftLengthComesFromJuniorRulebook);
runner.Run("alpha draft junior preset has fifteen rounds", alphaDraftExperienceTests.JuniorPresetHasFifteenRounds);
runner.Run("alpha draft NHL preset has seven rounds", alphaDraftExperienceTests.NhlPresetHasSevenRounds);
runner.Run("alpha draft AHL preset disables draft", alphaDraftExperienceTests.AhlPresetDisablesDraft);
runner.Run("alpha draft board reordering works", alphaDraftExperienceTests.DraftBoardReorderingWorks);
runner.Run("alpha draft notes and stars work", alphaDraftExperienceTests.DraftNotesAndStarsWork);
runner.Run("alpha draft AI drafting runs until player pick", alphaDraftExperienceTests.AiDraftingRunsUntilPlayerPick);
runner.Run("alpha live draft starts active draft", alphaDraftExperienceTests.StartDraftBeginsDraft);
runner.Run("alpha live draft automatically advances AI picks", alphaDraftExperienceTests.LiveDraftStartAdvancesAiPicksAutomatically);
runner.Run("alpha draft player drafting records selection", alphaDraftExperienceTests.PlayerDraftingRecordsSelection);
runner.Run("alpha live draft player selection continues to next pick", alphaDraftExperienceTests.LiveDraftPlayerSelectionContinuesToNextPlayerPick);
runner.Run("alpha draft pick adds draft rights not active roster", alphaDraftExperienceTests.PlayerDraftPickAddsDraftRightsNotActiveRoster);
runner.Run("alpha draft duplicate selection is prevented", alphaDraftExperienceTests.DuplicateDraftSelectionIsPrevented);
runner.Run("alpha draft recap is generated", alphaDraftExperienceTests.DraftRecapIsGenerated);
runner.Run("alpha draft recap inbox message is created", alphaDraftExperienceTests.DraftRecapInboxMessageCreated);
runner.Run("alpha draft only creates final review inbox", alphaDraftExperienceTests.DraftOnlyCreatesFinalReviewInbox);
runner.Run("alpha draft events are generated", alphaDraftExperienceTests.DraftEventsAreGenerated);
runner.Run("alpha draft completion creates recap event", alphaDraftExperienceTests.DraftCompletionCreatesRecapEvent);
runner.Run("alpha desktop exposes draft actions", alphaDraftExperienceTests.DesktopIntegrationExposesDraftActions);
runner.Run("alpha desktop exposes live draft modal", alphaDraftExperienceTests.DesktopIntegrationExposesLiveDraftModal);
runner.Run("development inbox no offseason spam", developmentInboxPolicyTests.NoOffseasonDevelopmentInboxSpam);
runner.Run("development inbox messages are capped", developmentInboxPolicyTests.DevelopmentInboxMessagesAreCapped);
runner.Run("development inbox names player", developmentInboxPolicyTests.PlayerNameAppearsInDevelopmentMessage);
runner.Run("routine development update stays out of inbox", developmentInboxPolicyTests.RoutineDevelopmentUpdateDoesNotCreateInboxItem);
runner.Run("meaningful development update creates inbox item", developmentInboxPolicyTests.MeaningfulDevelopmentUpdateCreatesInboxItem);
runner.Run("AHL rulebook has draft disabled", ahlAffiliateRulebookTests.AhlRulebookHasDraftDisabled);
runner.Run("AHL preset has draft disabled", ahlAffiliateRulebookTests.AhlPresetHasDraftDisabled);
runner.Run("AHL organization references NHL parent", ahlAffiliateRulebookTests.AhlOrganizationCanReferenceParentNhlOrganization);
runner.Run("NHL organization references AHL affiliate", ahlAffiliateRulebookTests.NhlOrganizationCanReferenceAhlAffiliateOrganization);
runner.Run("AHL roster player can be assigned from parent club", ahlAffiliateRulebookTests.PlayerCanBeAddedAsAssignedFromParentClub);
runner.Run("AHL draft UI is disabled by rulebook", ahlAffiliateRulebookTests.AhlDraftUiIsDisabledByRulebook);
runner.Run("Junior and NHL draft behavior remains enabled", ahlAffiliateRulebookTests.JuniorAndNhlDraftBehaviorRemainEnabled);
runner.Run("training camp can be created", trainingCampTests.TrainingCampCanBeCreated);
runner.Run("training camp invites returning roster players", trainingCampTests.ReturningRosterPlayersCanBeInvited);
runner.Run("training camp invites drafted prospects recruits and tryouts", trainingCampTests.DraftedProspectsRecruitsAndTryoutsCanBeInvited);
runner.Run("training camp invites AHL assigned parent player", trainingCampTests.AhlAssignedFromParentPlayerCanBeInvited);
runner.Run("training camp evaluation is generated", trainingCampTests.CampEvaluationIsGenerated);
runner.Run("training camp evaluation includes player name", trainingCampTests.EvaluationIncludesPlayerName);
runner.Run("training camp keep decision changes status", trainingCampTests.KeepDecisionChangesStatus);
runner.Run("training camp cut decision changes status", trainingCampTests.CutDecisionChangesStatus);
runner.Run("training camp assign to affiliate works when supported", trainingCampTests.AssignToAffiliateWorksWhenSupported);
runner.Run("training camp junior affiliate decisions unavailable", trainingCampTests.JuniorAffiliateDecisionsAreUnavailableByDefault);
runner.Run("training camp return to parent works for AHL source", trainingCampTests.ReturnToParentWorksForAhlStyleSource);
runner.Run("training camp opening roster validation uses rule engine", trainingCampTests.OpeningRosterValidationUsesRuleEngine);
runner.Run("training camp summary is generated", trainingCampTests.CampSummaryIsGenerated);
runner.Run("training camp events are created", trainingCampTests.CampEventsAreCreated);
runner.Run("training camp inbox items can be generated", trainingCampTests.InboxItemsCanBeGeneratedForCampEvents);
runner.Run("AlphaDesktop exposes training camp surface and actions", trainingCampTests.AlphaDesktopExposesTrainingCampSurfaceAndActions);
runner.Run("training camp opens automatically on calendar date", trainingCampTests.CampOpensAutomaticallyOnCalendarDate);
runner.Run("training camp closes by season start deadline", trainingCampTests.CampClosesBySeasonStartDeadline);
runner.Run("training camp junior young player can return to youth team", trainingCampTests.JuniorYoungPlayerCanBeReturnedToYouthTeam);
runner.Run("training camp NHL waiver-exempt player can be assigned to AHL", trainingCampTests.NhlWaiverExemptPlayerCanBeAssignedToAhl);
runner.Run("training camp NHL waiver-required player requires waiver action", trainingCampTests.NhlWaiverRequiredPlayerRequiresWaiverAction);
runner.Run("training camp AHL assigned player can return to parent", trainingCampTests.AhlAssignedPlayerCanBeReturnedToParent);
runner.Run("training camp over-limit roster creates pending action", trainingCampTests.RosterOverLimitCreatesWarningPendingAction);
runner.Run("training camp calendar does not auto cut or add players", trainingCampTests.CalendarCampDoesNotAutoCutOrAddRosterPlayers);
runner.Run("training camp calendar respects RuleEngine roster size", trainingCampTests.RuleEngineRosterSizeIsRespectedByCampCalendar);
runner.Run("training camp has no Godot save or game simulation dependency", trainingCampTests.TrainingCampHasNoGodotSaveOrGameSimulationDependency);
runner.Run("pending GM action advance day does not auto-sign recruit", pendingGmActionTests.AdvanceDayDoesNotAutoSignRecruit);
runner.Run("pending GM action advance day does not auto-add roster player", pendingGmActionTests.AdvanceDayDoesNotAutoAddPlayerToRoster);
runner.Run("pending GM action recruit commitment creates pending action", pendingGmActionTests.RecruitCommitmentCreatesPendingGmAction);
runner.Run("pending GM action approving sign recruit creates contract", pendingGmActionTests.ApprovingSignRecruitCreatesContract);
runner.Run("pending GM action declining sign recruit does not create contract", pendingGmActionTests.DecliningSignRecruitDoesNotCreateContract);
runner.Run("pending GM action approving add to roster adds player", pendingGmActionTests.ApprovingAddToRosterAddsPlayer);
runner.Run("pending GM action declining add to roster does not add player", pendingGmActionTests.DecliningAddToRosterDoesNotAddPlayer);
runner.Run("pending GM action creates inbox message", pendingGmActionTests.InboxMessageIsGeneratedForPendingAction);
runner.Run("AlphaDesktop exposes pending GM actions", pendingGmActionTests.AlphaDesktopExposesPendingActions);
runner.Run("pending GM actions have no Godot save or full game simulation dependency", pendingGmActionTests.PendingActionsHaveNoGodotSaveOrFullGameSimulationDependency);
runner.Run("prospect decision drafted player starts as rights held", prospectDecisionTests.DraftedPlayerStartsAsDraftRightsHeld);
runner.Run("prospect decision drafted player has non-contract paths", prospectDecisionTests.DraftedPlayerHasNonContractProspectPaths);
runner.Run("prospect decision drafted player is not rostered by default", prospectDecisionTests.DraftedPlayerIsNotActiveRosterByDefault);
runner.Run("prospect decision offer contract creates pending action", prospectDecisionTests.OfferContractCreatesPendingAction);
runner.Run("prospect decision approving signing creates contract", prospectDecisionTests.ApprovingSigningCreatesContract);
runner.Run("prospect decision invite to camp adds camp invite", prospectDecisionTests.InviteToCampAddsTrainingCampInvite);
runner.Run("prospect decision return to junior changes status", prospectDecisionTests.ReturnToJuniorChangesProspectStatus);
runner.Run("prospect decision return to youth changes status", prospectDecisionTests.ReturnToYouthTeamChangesProspectStatus);
runner.Run("prospect decision affiliate assignment respects rulebook", prospectDecisionTests.AssignToAffiliateOnlyAvailableWhenRulebookSupportsIt);
runner.Run("prospect decision AHL-style teams do not draft directly", prospectDecisionTests.AhlStyleTeamsDoNotUseAmateurDraftFlow);
runner.Run("prospect decision creates events and inbox", prospectDecisionTests.ProspectDecisionsCreateEventsAndInboxMessages);
runner.Run("AlphaDesktop exposes prospect actions", prospectDecisionTests.AlphaDesktopExposesProspectActions);
runner.Run("prospect decisions have no Godot save or game simulation dependency", prospectDecisionTests.ProspectDecisionsHaveNoGodotSaveOrGameSimulationDependency);
runner.Run("season readiness validates opening roster", seasonReadinessTests.OpeningRosterValidationPassesWhenCompliant);
runner.Run("season readiness rejects over-limit roster", seasonReadinessTests.RosterOverLimitIsRejected);
runner.Run("season readiness rejects under-limit roster", seasonReadinessTests.RosterUnderLimitIsRejected);
runner.Run("season readiness validates goalie requirement", seasonReadinessTests.GoalieRequirementIsRejected);
runner.Run("season readiness pending actions prevent season", seasonReadinessTests.PendingActionsPreventSeasonStart);
runner.Run("season readiness owner coach scout reviews generated", seasonReadinessTests.OwnerCoachAndScoutReviewsAreGenerated);
runner.Run("season readiness can become ready", seasonReadinessTests.SeasonReadinessCanBeReady);
runner.Run("season readiness begin is blocked when not ready", seasonReadinessTests.BeginSeasonIsBlockedWhenNotReady);
runner.Run("season readiness begin is enabled when ready", seasonReadinessTests.BeginSeasonIsEnabledWhenReady);
runner.Run("AlphaDesktop exposes season readiness", seasonReadinessTests.AlphaDesktopExposesSeasonReadinessSurface);
runner.Run("season readiness has no Godot save game simulation standings or playoffs", seasonReadinessTests.SeasonReadinessHasNoGodotSaveOrGameSimulationDependency);
runner.Run("executive reports readiness report generated", executiveReportTests.ReadinessReportGenerated);
runner.Run("executive reports season review generated", executiveReportTests.SeasonReviewGenerated);
runner.Run("executive reports owner review generated", executiveReportTests.OwnerReviewGenerated);
runner.Run("executive reports coach review generated", executiveReportTests.CoachReviewGenerated);
runner.Run("executive reports scout review generated", executiveReportTests.ScoutReviewGenerated);
runner.Run("executive reports medical summary generated", executiveReportTests.MedicalSummaryGenerated);
runner.Run("executive reports development summary generated", executiveReportTests.DevelopmentSummaryGenerated);
runner.Run("executive reports roster compliance included", executiveReportTests.RosterComplianceIncluded);
runner.Run("executive reports organization health calculated", executiveReportTests.OrganizationHealthCalculated);
runner.Run("executive reports previous season comparison works", executiveReportTests.PreviousSeasonComparisonWorks);
runner.Run("executive reports stored permanently", executiveReportTests.ReportsStoredPermanently);
runner.Run("executive reports archive retrieval works", executiveReportTests.ArchiveRetrievalWorks);
runner.Run("executive reports begin season stores readiness report", executiveReportTests.BeginSeasonStoresReadinessReport);
runner.Run("AlphaDesktop exposes executive reports", executiveReportTests.AlphaDesktopExposesExecutiveReports);
runner.Run("scouting operations scout can be assigned to region", scoutingOperationsTests.ScoutCanBeAssignedToRegion);
runner.Run("scouting operations scout can be assigned to player", scoutingOperationsTests.ScoutCanBeAssignedToPlayer);
runner.Run("scouting operations assignment stores priority notes and dates", scoutingOperationsTests.AssignmentStoresPriorityNotesAndDates);
runner.Run("scouting operations assignment stores duration and return date", scoutingOperationsTests.AssignmentStoresDurationAndReturnDate);
runner.Run("scouting operations assignment does not create inbox noise", scoutingOperationsTests.ScoutAssignmentDoesNotCreateInboxNoise);
runner.Run("scouting operations area assignment uses duration and return date", scoutingOperationsTests.AreaAssignmentUsesDurationAndReturnDate);
runner.Run("scouting operations assignment progresses over days", scoutingOperationsTests.AssignmentProgressesOverDays);
runner.Run("scouting operations completed assignment creates report", scoutingOperationsTests.CompletedAssignmentCreatesReport);
runner.Run("scouting operations completed assignment inbox names trip and duration", scoutingOperationsTests.CompletedAssignmentInboxNamesTripAndDuration);
runner.Run("scouting operations region fit improves confidence", scoutingOperationsTests.RegionFitImprovesConfidence);
runner.Run("scouting operations heavy workload delays report", scoutingOperationsTests.HeavyWorkloadDelaysReport);
runner.Run("scouting operations poor relationship affects communication quality", scoutingOperationsTests.PoorRelationshipAffectsCommunicationQuality);
runner.Run("scouting operations staff relationship warning can be generated", scoutingOperationsTests.StaffRelationshipWarningCanBeGenerated);
runner.Run("scouting operations staff role can be reassigned", scoutingOperationsTests.StaffRoleCanBeReassigned);
runner.Run("scouting operations staff can be released", scoutingOperationsTests.StaffCanBeReleased);
runner.Run("scouting operations placeholder staff candidate can be hired", scoutingOperationsTests.PlaceholderStaffCandidateCanBeHired);
runner.Run("AlphaDesktop exposes scout assignment UI", scoutingOperationsTests.AlphaDesktopExposesScoutAssignmentUi);
runner.Run("scouting operations have no Godot save or game simulation dependency", scoutingOperationsTests.ScoutingOperationsHaveNoGodotSaveOrGameSimulationDependency);
runner.Run("generated display names have no numeric suffixes", playerDossierIntegrationTests.GeneratedDisplayNamesHaveNoNumericSuffixes);
runner.Run("dossier can be created for roster player", playerDossierIntegrationTests.DossierCanBeCreatedForRosterPlayer);
runner.Run("dossier can be created for recruit", playerDossierIntegrationTests.DossierCanBeCreatedForRecruit);
runner.Run("dossier can be created for draft prospect", playerDossierIntegrationTests.DossierCanBeCreatedForDraftProspect);
runner.Run("dossier hides true ratings", playerDossierIntegrationTests.DossierHidesTrueRatings);
runner.Run("GM note can be added to dossier", playerDossierIntegrationTests.GmNoteCanBeAdded);
runner.Run("dossier sections populate when data exists", playerDossierIntegrationTests.ScoutingDevelopmentInjuryAndContractSectionsPopulateWhenDataExists);
runner.Run("budget overview calculates contract totals", budgetOverviewTests.BudgetOverviewCalculatesContractTotals);
runner.Run("budget overview warns when over budget", budgetOverviewTests.BudgetOverviewWarnsWhenOverBudget);
runner.Run("staff control candidate generated", staffControlTests.StaffCandidateGenerated);
runner.Run("staff control candidate has role and department fit", staffControlTests.CandidateHasRoleAndDepartmentFit);
runner.Run("staff control candidate has strengths and weaknesses", staffControlTests.CandidateHasStrengthsAndWeaknesses);
runner.Run("staff control staff can be hired", staffControlTests.StaffCanBeHired);
runner.Run("staff control staff can be released", staffControlTests.StaffCanBeReleased);
runner.Run("staff control staff role can be changed", staffControlTests.StaffRoleCanBeChanged);
runner.Run("staff control development coach focus can be set", staffControlTests.DevelopmentCoachFocusCanBeSet);
runner.Run("staff control medical staff focus can be set", staffControlTests.MedicalStaffFocusCanBeSet);
runner.Run("staff control scouting department focus can be set", staffControlTests.ScoutingDepartmentFocusCanBeSet);
runner.Run("staff control evaluation generated", staffControlTests.StaffEvaluationGenerated);
runner.Run("staff control profiles tolerate duplicate person ids", staffControlTests.StaffProfilesTolerateDuplicatePersonIds);
runner.Run("staff control chemistry warning generated", staffControlTests.ChemistryWarningGenerated);
runner.Run("staff control relationship affects chemistry", staffControlTests.RelationshipAffectsChemistry);
runner.Run("staff control events created", staffControlTests.EventsCreated);
runner.Run("staff control inbox messages created", staffControlTests.InboxMessagesCreated);
runner.Run("AlphaDesktop exposes staff controls v2", staffControlTests.AlphaDesktopExposesStaffControls);
runner.Run("staff control has no Godot save or game simulation dependency", staffControlTests.NoGodotSaveOrGameSimulation);
runner.Run("AlphaDesktop staff selected item exposes staff actions", alphaDesktopInteractionTests.StaffSelectedItemExposesStaffActions);
runner.Run("AlphaDesktop roster and prospect selected item exposes player actions", alphaDesktopInteractionTests.PlayerAndProspectSelectedItemsExposePlayerActions);
runner.Run("AlphaDesktop view dossier works from selected player", alphaDesktopInteractionTests.ViewDossierWorksFromSelectedPlayer);
runner.Run("AlphaDesktop dossier window supports editable GM notes", alphaDesktopInteractionTests.DossierWindowSupportsEditableGmNotes);
runner.Run("AlphaDesktop roster filters and readable fields are exposed", alphaDesktopInteractionTests.RosterFiltersAndReadableFieldsAreExposed);
runner.Run("AlphaDesktop budget overview is shown on dashboard and owner screen", alphaDesktopInteractionTests.BudgetOverviewIsShownOnDashboardAndOwnerScreen);
runner.Run("AlphaDesktop scouting cleanup and duration UI is exposed", alphaDesktopInteractionTests.ScoutingCleanupAndDurationUiIsExposed);
runner.Run("AlphaDesktop staff profile and focus actions are wired", alphaDesktopInteractionTests.StaffProfileAndFocusActionsAreWired);
runner.Run("AlphaDesktop recruit rows and details show position age and priorities", alphaDesktopInteractionTests.RecruitRowsAndDetailsShowPositionAgeAndPriorities);
runner.Run("AlphaDesktop dashboard summary displays counts", alphaDesktopInteractionTests.DashboardSummaryDisplaysCounts);
runner.Run("alpha 2.3.1 name generator creates five hundred names with low duplicate rate", alpha231DedupHiringTests.NameGeneratorCreatesFiveHundredNamesWithVeryLowDuplicateRate);
runner.Run("alpha 2.3.1 name generator creates forty draft classes without numeric suffixes", alpha231DedupHiringTests.NameGeneratorCreatesFortyDraftClassesWithoutNumericSuffixes);
runner.Run("alpha 2.3.1 new GM scenario has no duplicate recruit person ids", alpha231DedupHiringTests.NewGmScenarioHasNoDuplicateRecruitPersonIds);
runner.Run("alpha 2.3.1 draft board has no duplicate prospect person ids", alpha231DedupHiringTests.DraftBoardHasNoDuplicateProspectPersonIds);
runner.Run("alpha 2.3.1 recruit draft and scouting targets have no duplicate person ids", alpha231DedupHiringTests.NewGmScenarioHasNoDuplicatePersonIdsAcrossRecruitDraftAndScoutingTargets);
runner.Run("alpha 2.3.1 recruit display names contain no numeric suffixes", alpha231DedupHiringTests.RecruitDisplayNamesContainNoNumericSuffixes);
runner.Run("alpha 2.3.1 scenario generated staff and owners have clean names", alpha231DedupHiringTests.ScenarioGeneratedStaffAndOwnersHaveCleanNames);
runner.Run("alpha 2.3.1 staff candidate names come from name generator", alpha231DedupHiringTests.StaffCandidateNamesComeFromNameGenerator);
runner.Run("alpha 2.3.1 recruit UI dedupes by person id", alpha231DedupHiringTests.RecruitUiDedupesByPersonId);
runner.Run("alpha 2.3.1 draft board UI dedupes by person id", alpha231DedupHiringTests.DraftBoardUiDedupesByPersonId);
runner.Run("alpha 2.3.1 same-name players are clarified with context", alpha231DedupHiringTests.SameNameDifferentPlayersAreClarifiedWithContext);
runner.Run("alpha 2.3.1 staff candidates appear in desktop", alpha231DedupHiringTests.StaffCandidatesAppearInAlphaDesktop);
runner.Run("alpha 2.3.1 candidate can be selected", alpha231DedupHiringTests.CandidateCanBeSelected);
runner.Run("alpha 2.3.1 hire candidate creates staff member", alpha231DedupHiringTests.HireCandidateCreatesStaffMember);
runner.Run("alpha 2.3.1 hire candidate creates event and inbox", alpha231DedupHiringTests.HireCandidateCreatesEventAndInbox);
runner.Run("alpha 2.3.1 candidate cannot be hired twice", alpha231DedupHiringTests.CandidateCannotBeHiredTwice);
runner.Run("alpha 2.4 GM salary counted in budget", alpha24StaffBudgetTests.GmSalaryCountedInBudget);
runner.Run("alpha 2.4 staff salary counted in budget", alpha24StaffBudgetTests.StaffSalaryCountedInBudget);
runner.Run("alpha 2.4 hiring staff increases used budget", alpha24StaffBudgetTests.HiringStaffIncreasesUsedBudget);
runner.Run("alpha 2.4 releasing staff changes budget and creates obligation", alpha24StaffBudgetTests.ReleasingStaffChangesBudgetAndCreatesObligation);
runner.Run("alpha 2.4 candidate salary displayed", alpha24StaffBudgetTests.CandidateSalaryDisplayed);
runner.Run("alpha 2.4 over-budget warning generated", alpha24StaffBudgetTests.OverBudgetWarningGenerated);
runner.Run("alpha 2.4 league salary ranges differ", alpha24StaffBudgetTests.LeagueSalaryRangesDifferForJuniorAhlAndNhl);
runner.Run("alpha 2.4 relationship chemistry warning can appear", alpha24StaffBudgetTests.RelationshipChemistryWarningCanAppear);
runner.Run("alpha 2.4 AlphaDesktop exposes hockey operations budget", alpha24StaffBudgetTests.AlphaDesktopExposesHockeyOperationsBudget);
runner.Run("alpha 2.4 no Godot save or game simulation added", alpha24StaffBudgetTests.NoGodotSaveOrGameSimulationAdded);
runner.Run("alpha 2.5 schedule generated", alpha25SeasonFrameworkTests.ScheduleGenerated);
runner.Run("alpha 2.5 games have dates home and away", alpha25SeasonFrameworkTests.GamesHaveDatesHomeAndAway);
runner.Run("alpha 2.5 game sim creates result", alpha25SeasonFrameworkTests.GameSimCreatesResult);
runner.Run("alpha 2.5 standings update", alpha25SeasonFrameworkTests.StandingsUpdate);
runner.Run("alpha 2.5 player stats update", alpha25SeasonFrameworkTests.PlayerStatsUpdate);
runner.Run("alpha 2.5 goalie stats update", alpha25SeasonFrameworkTests.GoalieStatsUpdate);
runner.Run("alpha 2.5 daily pipeline simulates scheduled games", alpha25SeasonFrameworkTests.DailyPipelineSimulatesScheduledGames);
runner.Run("alpha 2.5 inbox game recap generated", alpha25SeasonFrameworkTests.InboxGameRecapGenerated);
runner.Run("alpha 2.5 AlphaDesktop exposes schedule standings and stats", alpha25SeasonFrameworkTests.AlphaDesktopExposesScheduleStandingsAndStats);
runner.Run("alpha 2.5 no Godot save or full 3D simulation added", alpha25SeasonFrameworkTests.SeasonFrameworkHasNoGodotSaveOrFull3DSimulation);
runner.Run("alpha 2.6 completed game creates recap", alpha26GameRecapStatsPolishTests.CompletedGameCreatesRecap);
runner.Run("alpha 2.6 recap includes score winner teams and date", alpha26GameRecapStatsPolishTests.RecapIncludesScoreWinnerTeamsAndDate);
runner.Run("alpha 2.6 three stars generated", alpha26GameRecapStatsPolishTests.ThreeStarsGenerated);
runner.Run("alpha 2.6 game recap inbox created for player team", alpha26GameRecapStatsPolishTests.GameRecapInboxCreatedForPlayerTeam);
runner.Run("alpha 2.6 league-wide games do not spam inbox", alpha26GameRecapStatsPolishTests.LeagueWideGamesDoNotSpamInbox);
runner.Run("alpha 2.6 standings sorted by points", alpha26GameRecapStatsPolishTests.StandingsSortedByPoints);
runner.Run("alpha 2.6 team leaders generated", alpha26GameRecapStatsPolishTests.TeamLeadersGenerated);
runner.Run("alpha 2.6 league leaders generated", alpha26GameRecapStatsPolishTests.LeagueLeadersGenerated);
runner.Run("alpha 2.6 recent results shown", alpha26GameRecapStatsPolishTests.RecentResultsShown);
runner.Run("alpha 2.6 upcoming games shown", alpha26GameRecapStatsPolishTests.UpcomingGamesShown);
runner.Run("alpha 2.6 dashboard shows last game next game and record", alpha26GameRecapStatsPolishTests.DashboardShowsLastGameNextGameAndRecord);
runner.Run("alpha 2.6 recap does not expose hidden ratings", alpha26GameRecapStatsPolishTests.RecapDoesNotExposeHiddenRatings);
runner.Run("alpha 2.6 no Godot save or full tactical simulation added", alpha26GameRecapStatsPolishTests.GameRecapPolishHasNoGodotSaveOrFullTacticalSimulation);
runner.Run("alpha 2.7 advance to next game stops on player game", alpha27FirstMonthPlayabilityTests.AdvanceToNextGameStopsOnPlayerGame);
runner.Run("alpha 2.7 advance to month end stops at monthly report", alpha27FirstMonthPlayabilityTests.AdvanceToMonthEndStopsAtMonthlyReport);
runner.Run("alpha 2.7 advance stops on injury", alpha27FirstMonthPlayabilityTests.AdvanceStopsOnInjury);
runner.Run("alpha 2.7 advance stops on urgent pending action", alpha27FirstMonthPlayabilityTests.AdvanceStopsOnUrgentPendingAction);
runner.Run("alpha 2.7 monthly summary generated", alpha27FirstMonthPlayabilityTests.MonthlySummaryGenerated);
runner.Run("alpha 2.7 monthly summary includes record", alpha27FirstMonthPlayabilityTests.MonthlySummaryIncludesRecord);
runner.Run("alpha 2.7 monthly summary includes required sections", alpha27FirstMonthPlayabilityTests.MonthlySummaryIncludesRequiredSections);
runner.Run("alpha 2.7 monthly summary inbox created once", alpha27FirstMonthPlayabilityTests.MonthlySummaryInboxCreatedOnce);
runner.Run("alpha 2.7 routine development does not spam inbox", alpha27FirstMonthPlayabilityTests.RoutineDevelopmentDoesNotSpamInbox);
runner.Run("alpha 2.7 league-wide games do not spam inbox", alpha27FirstMonthPlayabilityTests.LeagueWideGamesDoNotSpamInbox);
runner.Run("alpha 2.7 inbox priority sorting works", alpha27FirstMonthPlayabilityTests.InboxPrioritySortingWorks);
runner.Run("alpha 2.7 dashboard exposes advance controls and summary counts", alpha27FirstMonthPlayabilityTests.DashboardExposesAdvanceControlsAndSummaryCounts);
runner.Run("alpha 2.7 no Godot save or full tactical simulation added", alpha27FirstMonthPlayabilityTests.FirstMonthPassHasNoGodotSaveOrFullTacticalSimulation);
runner.Run("alpha 2.7.1 junior roster target is 26", alpha271RosterFrontOfficeRealismTests.JuniorRosterTargetIsTwentySix);
runner.Run("alpha 2.7.1 scenario starts with legal roster", alpha271RosterFrontOfficeRealismTests.ScenarioStartsWithLegalRoster);
runner.Run("alpha 2.7.1 draft prospects include physical and team bio", alpha271RosterFrontOfficeRealismTests.DraftProspectsIncludePhysicalAndTeamBio);
runner.Run("alpha 2.7.1 draft measurements match position ranges", alpha271RosterFrontOfficeRealismTests.DraftProspectMeasurementsMatchPositionRanges);
runner.Run("alpha 2.7.1 draft descriptions match known position", alpha271RosterFrontOfficeRealismTests.DraftProspectDescriptionsMatchKnownPosition);
runner.Run("alpha 2.7.1 scenario starts with inherited scouting reports", alpha271RosterFrontOfficeRealismTests.ScenarioStartsWithInheritedScoutingReports);
runner.Run("alpha 2.7.1 scenario roster has age mix and contract decisions", alpha271RosterFrontOfficeRealismTests.ScenarioRosterHasAgeMixAndContractDecisions);
runner.Run("alpha 2.7.1 contract terms use common pre-draft expiry day", alpha271RosterFrontOfficeRealismTests.ContractTermsUseCommonPreDraftExpiryDay);
runner.Run("alpha 2.7.1 scenario contracts expire on common pre-draft dates", alpha271RosterFrontOfficeRealismTests.ScenarioContractsExpireOnCommonPreDraftDates);
runner.Run("alpha 2.7.1 complete staff positions exist", alpha271RosterFrontOfficeRealismTests.CompleteStaffPositionsExist);
runner.Run("alpha 2.7.1 vacancies generated", alpha271RosterFrontOfficeRealismTests.VacanciesGenerated);
runner.Run("alpha 2.7.1 candidate hiring works", alpha271RosterFrontOfficeRealismTests.CandidateHiringWorks);
runner.Run("alpha 2.7.1 staff limits enforced", alpha271RosterFrontOfficeRealismTests.StaffLimitsEnforced);
runner.Run("alpha 2.7.1 dashboard warnings generated", alpha271RosterFrontOfficeRealismTests.DashboardWarningsGenerated);
runner.Run("alpha 2.7.1 AlphaDesktop exposes vacancies candidates and hiring", alpha271RosterFrontOfficeRealismTests.AlphaDesktopExposesVacanciesCandidatesAndHiring);
runner.Run("alpha 2.7.2 other-team contract signed goes to League News not inbox", alpha272InboxCleanupTransactionWireTests.OtherTeamContractSignedGoesToLeagueNewsNotInbox);
runner.Run("alpha 2.7.2 other-team roster add goes to League News not inbox", alpha272InboxCleanupTransactionWireTests.OtherTeamRosterAddGoesToLeagueNewsNotInbox);
runner.Run("alpha 2.7.2 player-team contract decision goes to inbox", alpha272InboxCleanupTransactionWireTests.PlayerTeamContractDecisionGoesToInbox);
runner.Run("alpha 2.7.2 vague system messages are filtered", alpha272InboxCleanupTransactionWireTests.VagueSystemMessagesAreFiltered);
runner.Run("alpha 2.7.2 League News feed displays transactions", alpha272InboxCleanupTransactionWireTests.LeagueNewsFeedDisplaysTransactions);
runner.Run("alpha 2.7.2 inbox remains team decision focused", alpha272InboxCleanupTransactionWireTests.InboxRemainsTeamDecisionFocused);
runner.Run("alpha 2.7.2 AlphaDesktop exposes League News tab", alpha272InboxCleanupTransactionWireTests.AlphaDesktopExposesLeagueNewsTab);
runner.Run("alpha 2.7.3 live draft middle rows include name position team and confidence", alpha273DraftStaffLayoutTests.LiveDraftMiddleRowsIncludeNamePositionTeamAndConfidence);
runner.Run("alpha 2.7.3 selecting draft prospect populates left prospect card", alpha273DraftStaffLayoutTests.SelectingDraftProspectPopulatesLeftProspectCard);
runner.Run("alpha 2.7.3 prospect card includes bio and scouting info", alpha273DraftStaffLayoutTests.ProspectCardIncludesBioAndScoutingInfo);
runner.Run("alpha 2.7.3 draft status appears on right", alpha273DraftStaffLayoutTests.DraftStatusAppearsOnRight);
runner.Run("alpha 2.7.3 drafting selected prospect removes them and adds rights", alpha273DraftStaffLayoutTests.DraftingSelectedProspectRemovesThemAndAddsRights);
runner.Run("alpha 2.7.3 draft and prospect rows expose basic bio without hidden ratings", alpha273DraftStaffLayoutTests.DraftAndProspectRowsExposeBasicBioWithoutHiddenRatings);
runner.Run("alpha 2.7.3 current staff excludes candidates and hire staff contains candidates", alpha273DraftStaffLayoutTests.CurrentStaffExcludesCandidatesAndHireStaffContainsCandidates);
runner.Run("alpha 2.7.3 hired candidate moves to current staff", alpha273DraftStaffLayoutTests.HiredCandidateMovesToCurrentStaff);
runner.Run("alpha 2.7.3 staff context buttons are correct", alpha273DraftStaffLayoutTests.StaffContextButtonsAreCorrect);
runner.Run("alpha 2.8 dashboard loads as GM Office workspace", alpha28GmOfficeNavigationTests.DashboardLoadsAsGmOfficeWorkspace);
runner.Run("alpha 2.8 Organization workspace exposes owner staff budget and health", alpha28GmOfficeNavigationTests.OrganizationWorkspaceExposesOwnerStaffBudgetAndHealth);
runner.Run("alpha 2.8 Hockey Operations workspace exposes roster prospects recruiting scouting draft and camp", alpha28GmOfficeNavigationTests.HockeyOperationsWorkspaceExposesRosterProspectsRecruitingScoutingDraftAndCamp);
runner.Run("alpha 2.8 Season workspace exposes schedule standings stats monthly summary and readiness", alpha28GmOfficeNavigationTests.SeasonWorkspaceExposesScheduleStandingsStatsMonthlySummaryAndReadiness);
runner.Run("alpha 2.8 Reports workspace exposes reports summaries and history placeholder", alpha28GmOfficeNavigationTests.ReportsWorkspaceExposesReportsSummariesAndHistoryPlaceholder);
runner.Run("alpha 2.8 main navigation is reduced to GM Office workspaces", alpha28GmOfficeNavigationTests.MainNavigationIsReducedToGmOfficeWorkspaces);
runner.Run("alpha 2.9 Action Center pulls pending GM actions", alpha29ActionCenterTests.ActionCenterPullsPendingGmActions);
runner.Run("alpha 2.9 Action Center pulls roster warnings", alpha29ActionCenterTests.ActionCenterPullsRosterWarnings);
runner.Run("alpha 2.9 Action Center pulls staff vacancies", alpha29ActionCenterTests.ActionCenterPullsStaffVacancies);
runner.Run("alpha 2.9 Action Center pulls budget warnings", alpha29ActionCenterTests.ActionCenterPullsBudgetWarnings);
runner.Run("alpha 2.9 Action Center pulls scouting completion", alpha29ActionCenterTests.ActionCenterPullsScoutingCompletion);
runner.Run("alpha 2.9 medical inbox action includes player name and position", alpha29ActionCenterTests.MedicalInboxActionIncludesPlayerNameAndPosition);
runner.Run("alpha 2.9 action item has required fields", alpha29ActionCenterTests.ActionItemHasRequiredFields);
runner.Run("alpha 2.9 daily agenda generated", alpha29ActionCenterTests.DailyAgendaGenerated);
runner.Run("alpha 2.9 assistant GM recommendations generated", alpha29ActionCenterTests.AssistantGmRecommendationsGenerated);
runner.Run("alpha 2.9 action status can change", alpha29ActionCenterTests.ActionStatusCanChange);
runner.Run("alpha 2.9 AlphaDesktop dashboard exposes Action Center counts", alpha29ActionCenterTests.AlphaDesktopDashboardExposesActionCenterCounts);
runner.Run("alpha 2.9 no Godot save or game simulation changes", alpha29ActionCenterTests.Alpha29HasNoGodotSaveOrGameSimulationChanges);
runner.Run("alpha 3.0 roster players have prior stats", alpha30ExistingWorldHistoryTests.NewGmScenarioRosterPlayersHavePriorStats);
runner.Run("alpha 3.0 older players have multi-year stats", alpha30ExistingWorldHistoryTests.OlderPlayersHaveMultiYearStats);
runner.Run("alpha 3.0 prospects have prior youth stats", alpha30ExistingWorldHistoryTests.ProspectsHavePriorYouthStats);
runner.Run("alpha 3.0 player dossier includes career history", alpha30ExistingWorldHistoryTests.PlayerDossierIncludesCareerHistory);
runner.Run("alpha 3.0 organization has prior season history", alpha30ExistingWorldHistoryTests.OrganizationHasPriorSeasonHistory);
runner.Run("alpha 3.0 history does not expose hidden ratings", alpha30ExistingWorldHistoryTests.HistoryDoesNotExposeHiddenRatings);
runner.Run("alpha 3.0 staff have career history where possible", alpha30ExistingWorldHistoryTests.StaffHaveCareerHistoryWherePossible);
runner.Run("alpha 3.0 AlphaDesktop exposes existing world history", alpha30ExistingWorldHistoryTests.AlphaDesktopExposesExistingWorldHistory);
runner.Run("alpha 3.1 free-agent market generated at scenario start", alpha31FreeAgentMarketTests.FreeAgentMarketGeneratedAtScenarioStart);
runner.Run("alpha 3.1 free agents have clean names and known bio", alpha31FreeAgentMarketTests.FreeAgentsHaveCleanNamesAndKnownBio);
runner.Run("alpha 3.1 free agents have prior stats and career history", alpha31FreeAgentMarketTests.FreeAgentsHavePriorStatsAndCareerHistory);
runner.Run("alpha 3.1 contract ask and budget impact exist", alpha31FreeAgentMarketTests.ContractAskAndBudgetImpactExist);
runner.Run("alpha 3.1 shortlist works", alpha31FreeAgentMarketTests.ShortlistWorks);
runner.Run("alpha 3.1 offer creates pending GM action and does not auto-sign", alpha31FreeAgentMarketTests.OfferCreatesPendingGmActionAndDoesNotAutoSign);
runner.Run("alpha 3.1 approving free-agent signing creates contract", alpha31FreeAgentMarketTests.ApprovingFreeAgentSigningCreatesContract);
runner.Run("alpha 3.1 declining free-agent signing does not create contract", alpha31FreeAgentMarketTests.DecliningFreeAgentSigningDoesNotCreateContract);
runner.Run("alpha 3.1 invite to camp creates workflow", alpha31FreeAgentMarketTests.InviteToCampCreatesCampWorkflow);
runner.Run("alpha 3.1 withdraw offer works", alpha31FreeAgentMarketTests.WithdrawOfferWorks);
runner.Run("alpha 3.1 low-interest free agent can reject offer", alpha31FreeAgentMarketTests.LowInterestFreeAgentCanRejectOffer);
runner.Run("alpha 3.1 other-team free-agent signing goes to league news not inbox", alpha31FreeAgentMarketTests.OtherTeamFreeAgentSigningGoesToLeagueNewsNotInbox);
runner.Run("alpha 3.1 player dossier includes free-agent sections without hidden ratings", alpha31FreeAgentMarketTests.PlayerDossierIncludesFreeAgentSectionsWithoutHiddenRatings);
runner.Run("alpha 3.1 AlphaDesktop exposes free-agent workspace and actions", alpha31FreeAgentMarketTests.AlphaDesktopExposesFreeAgentWorkspaceAndActions);
runner.Run("alpha 3.1 no Godot save or full negotiation added", alpha31FreeAgentMarketTests.Alpha31HasNoGodotSaveOrFullNegotiation);
runner.Run("alpha 3.2 trade block generated", alpha32TradeEngineTests.TradeBlockGenerated);
runner.Run("alpha 3.2 trade offer can include player asset", alpha32TradeEngineTests.TradeOfferCanIncludePlayerAsset);
runner.Run("alpha 3.2 trade offer can include prospect rights", alpha32TradeEngineTests.TradeOfferCanIncludeProspectRights);
runner.Run("alpha 3.2 trade offer can include draft pick placeholder", alpha32TradeEngineTests.TradeOfferCanIncludeDraftPickPlaceholder);
runner.Run("alpha 3.2 invalid empty offer rejected", alpha32TradeEngineTests.InvalidEmptyOfferRejected);
runner.Run("alpha 3.2 cannot trade asset not owned by team", alpha32TradeEngineTests.CannotTradeAssetNotOwnedByTeam);
runner.Run("alpha 3.2 AI accepts fair offer", alpha32TradeEngineTests.AiAcceptsFairOffer);
runner.Run("alpha 3.2 AI rejects poor offer", alpha32TradeEngineTests.AiRejectsPoorOffer);
runner.Run("alpha 3.2 accepted trade creates pending GM action", alpha32TradeEngineTests.AcceptedTradeCreatesPendingGmAction);
runner.Run("alpha 3.2 approved trade moves assets", alpha32TradeEngineTests.ApprovedTradeMovesAssets);
runner.Run("alpha 3.2 declined trade does not move assets", alpha32TradeEngineTests.DeclinedTradeDoesNotMoveAssets);
runner.Run("alpha 3.2 budget and roster impact calculated", alpha32TradeEngineTests.BudgetAndRosterImpactCalculated);
runner.Run("alpha 3.2 league news records completed trade", alpha32TradeEngineTests.LeagueNewsRecordsCompletedTrade);
runner.Run("alpha 3.2 inbox records player-team trade events", alpha32TradeEngineTests.InboxRecordsPlayerTeamTradeEvents);
runner.Run("alpha 3.2 AlphaDesktop exposes trade UI actions", alpha32TradeEngineTests.AlphaDesktopExposesTradeUiActions);
runner.Run("alpha 3.2 accepted trade does not auto-complete", alpha32TradeEngineTests.AcceptedTradeDoesNotAutoComplete);
runner.Run("alpha 3.2 no Godot save or full trade negotiation added", alpha32TradeEngineTests.Alpha32HasNoGodotSaveOrFullTradeNegotiation);
runner.Run("alpha 3.3 deadline date comes from season calendar", alpha33TradeDeadlineTests.DeadlineDateComesFromSeasonCalendar);
runner.Run("alpha 3.3 status changes at deadline windows", alpha33TradeDeadlineTests.StatusChangesAtDeadlineWindows);
runner.Run("alpha 3.3 buyer seller assessment generated", alpha33TradeDeadlineTests.BuyerSellerAssessmentGenerated);
runner.Run("alpha 3.3 trade block expands near deadline", alpha33TradeDeadlineTests.TradeBlockExpandsNearDeadline);
runner.Run("alpha 3.3 deadline rumors generated", alpha33TradeDeadlineTests.DeadlineRumorsGenerated);
runner.Run("alpha 3.3 dashboard exposes deadline card", alpha33TradeDeadlineTests.DashboardExposesDeadlineCard);
runner.Run("alpha 3.3 Action Center exposes deadline actions", alpha33TradeDeadlineTests.ActionCenterExposesDeadlineActions);
runner.Run("alpha 3.3 owner coach assistant messages limited", alpha33TradeDeadlineTests.OwnerCoachAssistantMessagesGeneratedButLimited);
runner.Run("alpha 3.3 trades allowed before deadline", alpha33TradeDeadlineTests.TradesAllowedBeforeDeadline);
runner.Run("alpha 3.3 trades blocked after deadline", alpha33TradeDeadlineTests.TradesBlockedAfterDeadline);
runner.Run("alpha 3.3 trade UI shows closed state after deadline", alpha33TradeDeadlineTests.TradeUiShowsClosedStateAfterDeadline);
runner.Run("alpha 3.3 league news posts deadline closed", alpha33TradeDeadlineTests.LeagueNewsPostsDeadlineClosed);
runner.Run("alpha 3.3 no inbox spam", alpha33TradeDeadlineTests.DeadlineDoesNotSpamInbox);
runner.Run("alpha 3.3 no Godot save realtime or full negotiation added", alpha33TradeDeadlineTests.Alpha33HasNoGodotSaveRealtimeOrFullNegotiation);
runner.Run("alpha 3.4 career timeline entry created", alpha34CareerHistoryTests.CareerTimelineEntryCreated);
runner.Run("alpha 3.4 player drafted creates draft history", alpha34CareerHistoryTests.PlayerDraftedCreatesDraftHistory);
runner.Run("alpha 3.4 drafted player appears in Where Are They Now", alpha34CareerHistoryTests.DraftedPlayerAppearsInWhereAreTheyNow);
runner.Run("alpha 3.4 draft outcome starts unknown or developing", alpha34CareerHistoryTests.DraftOutcomeStartsUnknownOrDeveloping);
runner.Run("alpha 3.4 player dossier includes timeline", alpha34CareerHistoryTests.PlayerDossierIncludesTimeline);
runner.Run("alpha 3.4 staff history is recorded", alpha34CareerHistoryTests.StaffHistoryIsRecorded);
runner.Run("alpha 3.4 GM career history is recorded", alpha34CareerHistoryTests.GmCareerHistoryIsRecorded);
runner.Run("alpha 3.4 organization season history is recorded", alpha34CareerHistoryTests.OrganizationSeasonHistoryIsRecorded);
runner.Run("alpha 3.4 trade completed creates history entry", alpha34CareerHistoryTests.TradeCompletedCreatesHistoryEntry);
runner.Run("alpha 3.4 free-agent signing creates history entry", alpha34CareerHistoryTests.FreeAgentSigningCreatesHistoryEntry);
runner.Run("alpha 3.4 injury creates history entry", alpha34CareerHistoryTests.InjuryCreatesHistoryEntry);
runner.Run("alpha 3.4 Reports History workspace exposes history views", alpha34CareerHistoryTests.ReportsHistoryWorkspaceExposesHistoryViews);
runner.Run("alpha 3.4 hidden ratings are not exposed", alpha34CareerHistoryTests.HiddenRatingsAreNotExposed);
runner.Run("alpha 3.4 no Godot save or deep database added", alpha34CareerHistoryTests.Alpha34HasNoGodotSaveOrDeepDatabase);
runner.Run("alpha 3.5 save file created", alpha35SaveLoadTests.SaveFileCreated);
runner.Run("alpha 3.5 save metadata written", alpha35SaveLoadTests.SaveMetadataWritten);
runner.Run("alpha 3.5 load restores current date", alpha35SaveLoadTests.LoadRestoresCurrentDate);
runner.Run("alpha 3.5 load restores roster", alpha35SaveLoadTests.LoadRestoresRoster);
runner.Run("alpha 3.5 load restores staff", alpha35SaveLoadTests.LoadRestoresStaff);
runner.Run("alpha 3.5 load restores inbox statuses", alpha35SaveLoadTests.LoadRestoresInboxStatuses);
runner.Run("alpha 3.5 load restores pending actions", alpha35SaveLoadTests.LoadRestoresPendingActions);
runner.Run("alpha 3.5 load restores schedule standings and stats", alpha35SaveLoadTests.LoadRestoresScheduleStandingsAndStats);
runner.Run("alpha 3.5 load restores career history", alpha35SaveLoadTests.LoadRestoresCareerHistory);
runner.Run("alpha 3.5 load restores draft history", alpha35SaveLoadTests.LoadRestoresDraftHistory);
runner.Run("alpha 3.5 load restores GM profile and team", alpha35SaveLoadTests.LoadRestoresGmProfileAndTeam);
runner.Run("alpha 3.5 version mismatch handled clearly", alpha35SaveLoadTests.VersionMismatchHandledClearly);
runner.Run("alpha 3.5 AlphaDesktop exposes save/load controls", alpha35SaveLoadTests.AlphaDesktopExposesSaveLoadControls);
runner.Run("alpha 3.5 no Godot database or cloud save added", alpha35SaveLoadTests.Alpha35HasNoGodotDatabaseOrCloudSave);
runner.Run("alpha 4.0 season completes when schedule finished", alpha40MultiSeasonTests.SeasonCompletesWhenScheduleFinished);
runner.Run("alpha 4.0 final standings archived", alpha40MultiSeasonTests.FinalStandingsArchived);
runner.Run("alpha 4.0 player stats archived", alpha40MultiSeasonTests.PlayerStatsArchived);
runner.Run("alpha 4.0 current season stats reset", alpha40MultiSeasonTests.CurrentSeasonStatsReset);
runner.Run("alpha 4.0 organization history updated", alpha40MultiSeasonTests.OrganizationHistoryUpdated);
runner.Run("alpha 4.0 end of season review generated", alpha40MultiSeasonTests.EndOfSeasonReviewGenerated);
runner.Run("alpha 4.0 offseason phase begins", alpha40MultiSeasonTests.OffseasonPhaseBegins);
runner.Run("alpha 4.0 expiring contracts identified", alpha40MultiSeasonTests.ExpiringContractsIdentified);
runner.Run("alpha 4.0 pending actions created for contract decisions", alpha40MultiSeasonTests.PendingActionsCreatedForContractDecisions);
runner.Run("alpha 4.0 player ages update naturally", alpha40MultiSeasonTests.PlayerAgesUpdateNaturally);
runner.Run("alpha 4.0 new draft class generated", alpha40MultiSeasonTests.NewDraftClassGenerated);
runner.Run("alpha 4.0 new draft class has unique clean names", alpha40MultiSeasonTests.NewDraftClassHasUniqueCleanNames);
runner.Run("alpha 4.0 previous draft class not reused", alpha40MultiSeasonTests.PreviousDraftClassNotReused);
runner.Run("alpha 4.0 save load works after rollover", alpha40MultiSeasonTests.SaveLoadWorksAfterRollover);
runner.Run("alpha 4.0 AlphaDesktop exposes offseason history state", alpha40MultiSeasonTests.AlphaDesktopExposesOffseasonHistoryState);
runner.Run("alpha 4.0 no Godot database cloud playoff awards or retirement", alpha40MultiSeasonTests.Alpha40HasNoGodotDatabaseCloudPlayoffAwardsOrRetirement);
runner.Run("alpha 4.1 contract ask created", alpha41ContractsV2Tests.ContractAskCreated);
runner.Run("alpha 4.1 offer builder calculates budget impact", alpha41ContractsV2Tests.OfferBuilderCalculatesBudgetImpact);
runner.Run("alpha 4.1 offer uses common expiry date", alpha41ContractsV2Tests.OfferUsesCommonExpiryDate);
runner.Run("alpha 4.1 likelihood estimate generated", alpha41ContractsV2Tests.LikelihoodEstimateGenerated);
runner.Run("alpha 4.1 accepted offer creates pending GM action", alpha41ContractsV2Tests.AcceptedOfferCreatesPendingGmAction);
runner.Run("alpha 4.1 rejected offer does not create contract", alpha41ContractsV2Tests.RejectedOfferDoesNotCreateContract);
runner.Run("alpha 4.1 approved pending offer creates contract", alpha41ContractsV2Tests.ApprovedPendingOfferCreatesContract);
runner.Run("alpha 4.1 declined pending offer does not create contract", alpha41ContractsV2Tests.DeclinedPendingOfferDoesNotCreateContract);
runner.Run("alpha 4.1 player role promise affects decision", alpha41ContractsV2Tests.PlayerRolePromiseAffectsDecision);
runner.Run("alpha 4.1 staff role focus promise affects decision", alpha41ContractsV2Tests.StaffRoleFocusPromiseAffectsDecision);
runner.Run("alpha 4.1 relationship affects decision", alpha41ContractsV2Tests.RelationshipAffectsDecision);
runner.Run("alpha 4.1 expiring contract screen data generated", alpha41ContractsV2Tests.ExpiringContractScreenDataGenerated);
runner.Run("alpha 4.1 contract comparison generated", alpha41ContractsV2Tests.ContractComparisonGenerated);
runner.Run("alpha 4.1 inbox messages include explanation", alpha41ContractsV2Tests.InboxMessagesIncludeExplanation);
runner.Run("alpha 4.1 league news receives other-team notable signing", alpha41ContractsV2Tests.LeagueNewsReceivesOtherTeamNotableSigning);
runner.Run("alpha 4.1 no auto signing", alpha41ContractsV2Tests.NoAutoSigning);
runner.Run("alpha 4.1 AlphaDesktop exposes contract management UI", alpha41ContractsV2Tests.AlphaDesktopExposesContractManagementUi);
runner.Run("alpha 4.1 no Godot save full agent or salary cap system", alpha41ContractsV2Tests.Alpha41HasNoGodotSaveFullAgentOrSalaryCapSystem);
runner.Run("alpha 4.2 free agency window uses season calendar", alpha42FreeAgencyV2Tests.FreeAgencyWindowUsesSeasonCalendar);
runner.Run("alpha 4.2 free agency phase changes over time", alpha42FreeAgencyV2Tests.FreeAgencyPhaseChangesOverTime);
runner.Run("alpha 4.2 motivations and competing offers generated", alpha42FreeAgencyV2Tests.MotivationsAndCompetingOffersGenerated);
runner.Run("alpha 4.2 motivations affect offer score", alpha42FreeAgencyV2Tests.MotivationsAffectOfferScore);
runner.Run("alpha 4.2 delayed response creates action center item", alpha42FreeAgencyV2Tests.DelayedResponseCreatesActionCenterItem);
runner.Run("alpha 4.2 competing offer can win player and create league news", alpha42FreeAgencyV2Tests.CompetingOfferCanWinPlayerAndCreateLeagueNews);
runner.Run("alpha 4.2 accepted offer creates pending approval and no contract", alpha42FreeAgencyV2Tests.AcceptedFreeAgentOfferCreatesPendingApprovalAndNoContract);
runner.Run("alpha 4.2 approving accepted offer creates contract", alpha42FreeAgencyV2Tests.ApprovingAcceptedFreeAgentOfferCreatesContract);
runner.Run("alpha 4.2 late market reduces ask and improves interest", alpha42FreeAgencyV2Tests.LateMarketReducesAskAndImprovesInterest);
runner.Run("alpha 4.2 staff recommendations generated", alpha42FreeAgencyV2Tests.StaffRecommendationsGenerated);
runner.Run("alpha 4.2 save load preserves market state", alpha42FreeAgencyV2Tests.SaveLoadPreservesMarketState);
runner.Run("alpha 4.2 AlphaDesktop exposes free agency v2 UI", alpha42FreeAgencyV2Tests.AlphaDesktopExposesFreeAgencyV2Ui);
runner.Run("alpha 4.2 no Godot full agent salary cap or game simulation", alpha42FreeAgencyV2Tests.Alpha42HasNoGodotFullAgentSalaryCapOrGameSimulation);
runner.Run("alpha 4.3 team needs generated", alpha43TradeEngineV2Tests.TeamNeedsGenerated);
runner.Run("alpha 4.3 GM personalities generated", alpha43TradeEngineV2Tests.GmPersonalitiesGenerated);
runner.Run("alpha 4.3 multi-asset trades supported", alpha43TradeEngineV2Tests.MultiAssetTradesSupported);
runner.Run("alpha 4.3 counter offers generated", alpha43TradeEngineV2Tests.CounterOffersGenerated);
runner.Run("alpha 4.3 budget affects trade", alpha43TradeEngineV2Tests.BudgetAffectsTrade);
runner.Run("alpha 4.3 team direction affects trade", alpha43TradeEngineV2Tests.TeamDirectionAffectsTrade);
runner.Run("alpha 4.3 player reaction generated", alpha43TradeEngineV2Tests.PlayerReactionGenerated);
runner.Run("alpha 4.3 staff reaction generated", alpha43TradeEngineV2Tests.StaffReactionGenerated);
runner.Run("alpha 4.3 trade history stored", alpha43TradeEngineV2Tests.TradeHistoryStored);
runner.Run("alpha 4.3 career timeline updated", alpha43TradeEngineV2Tests.CareerTimelineUpdated);
runner.Run("alpha 4.3 organization history updated", alpha43TradeEngineV2Tests.OrganizationHistoryUpdated);
runner.Run("alpha 4.3 league news updated", alpha43TradeEngineV2Tests.LeagueNewsUpdated);
runner.Run("alpha 4.3 Trade UI exposes v2 strategy", alpha43TradeEngineV2Tests.TradeUiExposesV2Strategy);
runner.Run("alpha 4.3 no forbidden trade systems", alpha43TradeEngineV2Tests.Alpha43HasNoForbiddenTradeSystems);

runner.Report();
Environment.ExitCode = runner.FailedCount == 0 ? 0 : 1;

internal sealed class RuleEngineTests
{
    private readonly Rulebook _rulebook = new RulebookLoader().LoadFromFile(Path.Combine(FindRepositoryRoot(), "data", "rulebooks", "junior_v1.json"));

    public void JuniorRulebookLoads()
    {
        Assert.Equal("junior_v1", _rulebook.RulebookId);
        Assert.Equal("junior", _rulebook.LeagueType);
        Assert.Equal(26, _rulebook.RosterRules!.MinRoster);
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

        Assert.Valid(validator.Validate(new RosterValidationRequest(26, 26, 2, 3, 2)));
        Assert.Code(RuleErrorCodes.RosterTooSmall, validator.Validate(new RosterValidationRequest(17, 17, 2, 0, 0)));
        Assert.Code(RuleErrorCodes.RosterTooLarge, validator.Validate(new RosterValidationRequest(27, 26, 2, 0, 0)));
        Assert.Code(RuleErrorCodes.ActiveRosterTooLarge, validator.Validate(new RosterValidationRequest(26, 27, 2, 0, 0)));
        Assert.Code(RuleErrorCodes.NotEnoughGoalies, validator.Validate(new RosterValidationRequest(26, 26, 1, 0, 0)));
        Assert.Code(RuleErrorCodes.TooManyOveragePlayers, validator.Validate(new RosterValidationRequest(26, 26, 2, 4, 0)));
        Assert.Code(RuleErrorCodes.TooManyImportPlayers, validator.Validate(new RosterValidationRequest(26, 26, 2, 0, 3)));
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
        Assert.Code(RuleErrorCodes.InvalidDraftRound, validator.Validate(new DraftValidationRequest(16)));
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
