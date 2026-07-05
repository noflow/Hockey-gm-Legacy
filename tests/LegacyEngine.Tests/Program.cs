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
runner.Run("scouting operations assignment progresses over days", scoutingOperationsTests.AssignmentProgressesOverDays);
runner.Run("scouting operations completed assignment creates report", scoutingOperationsTests.CompletedAssignmentCreatesReport);
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
runner.Run("staff control chemistry warning generated", staffControlTests.ChemistryWarningGenerated);
runner.Run("staff control relationship affects chemistry", staffControlTests.RelationshipAffectsChemistry);
runner.Run("staff control events created", staffControlTests.EventsCreated);
runner.Run("staff control inbox messages created", staffControlTests.InboxMessagesCreated);
runner.Run("AlphaDesktop exposes staff controls v2", staffControlTests.AlphaDesktopExposesStaffControls);
runner.Run("staff control has no Godot save or game simulation dependency", staffControlTests.NoGodotSaveOrGameSimulation);
runner.Run("AlphaDesktop staff selected item exposes staff actions", alphaDesktopInteractionTests.StaffSelectedItemExposesStaffActions);
runner.Run("AlphaDesktop roster and prospect selected item exposes player actions", alphaDesktopInteractionTests.PlayerAndProspectSelectedItemsExposePlayerActions);
runner.Run("AlphaDesktop view dossier works from selected player", alphaDesktopInteractionTests.ViewDossierWorksFromSelectedPlayer);
runner.Run("AlphaDesktop staff profile and focus actions are wired", alphaDesktopInteractionTests.StaffProfileAndFocusActionsAreWired);
runner.Run("AlphaDesktop recruit rows and details show position age and priorities", alphaDesktopInteractionTests.RecruitRowsAndDetailsShowPositionAgeAndPriorities);
runner.Run("AlphaDesktop dashboard summary displays counts", alphaDesktopInteractionTests.DashboardSummaryDisplaysCounts);

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
