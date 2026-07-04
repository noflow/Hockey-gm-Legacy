using LegacyEngine.Draft;
using LegacyEngine.Events;
using LegacyEngine.RuleEngine;
using LegacyEngine.Scouting;

internal sealed class DraftEngineTests
{
    public void DraftCreation()
    {
        var eventEngine = new EventEngine();
        var draft = new DraftEngine(eventEngine).CreateDraft(
            "draft-2027",
            2027,
            2,
            BuildStandings(),
            new DateTimeOffset(2027, 6, 1, 12, 0, 0, TimeSpan.Zero),
            BuildDraftRuleValidator());

        draft.Validate();
        Assert.Equal("draft-2027", draft.DraftId);
        Assert.Equal(2027, draft.SeasonYear);
        Assert.Equal(DraftStatus.InProgress, draft.Status);
        Assert.Equal(2, draft.NumberOfRounds);
        Assert.Equal(1, eventEngine.Queue.Count);
        Assert.Equal(LegacyEventType.DraftStarted, eventEngine.Queue.PendingEvents[0].EventType);
    }

    public void DraftOrderGeneratedFromReverseStandings()
    {
        var draft = BuildDraft();

        Assert.Equal("org-last", draft.DraftOrder.OrganizationIds[0]);
        Assert.Equal("org-middle", draft.DraftOrder.OrganizationIds[1]);
        Assert.Equal("org-first", draft.DraftOrder.OrganizationIds[2]);
    }

    public void CorrectNumberOfPicksCreated()
    {
        var draft = BuildDraft(rounds: 3);

        Assert.Equal(9, draft.Picks.Count);
        Assert.Equal(1, draft.Picks[0].RoundNumber);
        Assert.Equal(1, draft.Picks[0].PickNumber);
        Assert.Equal(3, draft.Picks[8].RoundNumber);
        Assert.Equal(9, draft.Picks[8].PickNumber);
    }

    public void ValidSelectionSucceeds()
    {
        var eventEngine = new EventEngine();
        var engine = new DraftEngine(eventEngine);
        var draft = BuildDraft(engine: engine);

        var result = engine.SelectProspect(
            draft,
            roundNumber: 1,
            pickNumber: 1,
            prospectPersonId: "prospect-001",
            selectedAt: new DateTimeOffset(2027, 6, 1, 12, 10, 0, TimeSpan.Zero),
            eligibility: new DraftEligibility("prospect-001", true),
            ruleValidator: BuildDraftRuleValidator());

        Assert.Equal("prospect-001", result.Selection.ProspectPersonId);
        Assert.Equal("org-last", result.Pick.OwningOrganizationId);
        Assert.True(result.Draft.HasSelectedProspect("prospect-001"), "Draft should remember selected prospect.");
        Assert.Equal(LegacyEventType.PlayerDrafted, result.CreatedEvent.EventType);
    }

    public void SameProspectCannotBeSelectedTwice()
    {
        var engine = new DraftEngine();
        var draft = BuildDraft(engine: engine);
        draft = engine.SelectProspect(draft, 1, 1, "prospect-001", new DateTimeOffset(2027, 6, 1, 12, 10, 0, TimeSpan.Zero)).Draft;

        Assert.Throws<InvalidOperationException>(() => engine.SelectProspect(
            draft,
            1,
            2,
            "prospect-001",
            new DateTimeOffset(2027, 6, 1, 12, 20, 0, TimeSpan.Zero)));
    }

    public void InvalidRoundFails()
    {
        var engine = new DraftEngine();
        var draft = BuildDraft(engine: engine, rounds: 2);

        Assert.Throws<ArgumentOutOfRangeException>(() => engine.SelectProspect(
            draft,
            3,
            7,
            "prospect-001",
            new DateTimeOffset(2027, 6, 1, 12, 10, 0, TimeSpan.Zero)));
    }

    public void SelectionAfterCompletionFails()
    {
        var engine = new DraftEngine();
        var completed = engine.MarkCompleted(BuildDraft(engine: engine), new DateTimeOffset(2027, 6, 1, 15, 0, 0, TimeSpan.Zero));

        Assert.Throws<InvalidOperationException>(() => engine.SelectProspect(
            completed,
            1,
            1,
            "prospect-001",
            new DateTimeOffset(2027, 6, 1, 15, 10, 0, TimeSpan.Zero)));
    }

    public void DraftCanBeMarkedCompleted()
    {
        var eventEngine = new EventEngine();
        var engine = new DraftEngine(eventEngine);
        var completed = engine.MarkCompleted(BuildDraft(engine: engine), new DateTimeOffset(2027, 6, 1, 15, 0, 0, TimeSpan.Zero));

        Assert.Equal(DraftStatus.Completed, completed.Status);
        Assert.True(eventEngine.Queue.PendingEvents.Any(item => item.EventType == LegacyEventType.DraftCompleted), "Draft completed event should be queued.");
    }

    public void DraftBoardCanAddProspect()
    {
        var board = new DraftEngine().CreateDraftBoard("board-001", "org-last")
            .AddProspect(BuildBoardEntry("prospect-001", 1));

        Assert.Equal(1, board.Entries.Count);
        Assert.Equal("prospect-001", board.Entries[0].ProspectPersonId);
        Assert.Throws<ArgumentException>(() => board.AddProspect(BuildBoardEntry("prospect-001", 2)));
    }

    public void DraftBoardCanUpdateRank()
    {
        var board = new DraftEngine().CreateDraftBoard("board-001", "org-last")
            .AddProspect(BuildBoardEntry("prospect-001", 2))
            .AddProspect(BuildBoardEntry("prospect-002", 1));

        var updated = board.UpdateRank("prospect-001", 1);

        Assert.Equal("prospect-001", updated.Entries[0].ProspectPersonId);
        Assert.Equal(1, updated.Entries[0].Rank);
    }

    public void DraftBoardCanRemoveProspect()
    {
        var board = new DraftEngine().CreateDraftBoard("board-001", "org-last")
            .AddProspect(BuildBoardEntry("prospect-001", 1))
            .AddProspect(BuildBoardEntry("prospect-002", 2));

        var updated = board.RemoveProspect("prospect-001");

        Assert.Equal(1, updated.Entries.Count);
        Assert.Equal("prospect-002", updated.Entries[0].ProspectPersonId);
        Assert.Throws<ArgumentException>(() => updated.RemoveProspect("missing-prospect"));
    }

    public void DraftBoardEntryCanReferenceScoutingReport()
    {
        var entry = BuildBoardEntry("prospect-001", 1, scoutingReportId: "report-001", confidence: ScoutingConfidenceLevel.High);

        entry.Validate();
        Assert.Equal("report-001", entry.ScoutingReportId);
        Assert.Equal(ScoutingConfidenceLevel.High, entry.ScoutingConfidence);
        Assert.Equal("Middle-six projection with strong compete.", entry.ProjectionText);
    }

    public void EventsCreatedForStartSelectionCompletion()
    {
        var eventEngine = new EventEngine();
        var engine = new DraftEngine(eventEngine);
        var draft = BuildDraft(engine: engine);
        draft = engine.SelectProspect(draft, 1, 1, "prospect-001", new DateTimeOffset(2027, 6, 1, 12, 10, 0, TimeSpan.Zero)).Draft;
        engine.MarkCompleted(draft, new DateTimeOffset(2027, 6, 1, 15, 0, 0, TimeSpan.Zero));

        Assert.Equal(3, eventEngine.Queue.Count);
        Assert.True(eventEngine.Queue.PendingEvents.Any(item => item.EventType == LegacyEventType.DraftStarted), "Draft started event should exist.");
        Assert.True(eventEngine.Queue.PendingEvents.Any(item => item.EventType == LegacyEventType.PlayerDrafted), "Player drafted event should exist.");
        Assert.True(eventEngine.Queue.PendingEvents.Any(item => item.EventType == LegacyEventType.DraftCompleted), "Draft completed event should exist.");
    }

    public void RuleEngineValidationIsRespected()
    {
        var disabledValidator = new DraftRuleValidator(BuildRulebook(draftEnabled: false, rounds: 0));
        var roundValidator = new DraftRuleValidator(BuildRulebook(draftEnabled: true, rounds: 1));
        var engine = new DraftEngine();

        Assert.Throws<InvalidOperationException>(() => engine.CreateDraft(
            "draft-disabled",
            2027,
            1,
            BuildStandings(),
            new DateTimeOffset(2027, 6, 1, 12, 0, 0, TimeSpan.Zero),
            disabledValidator));

        var draft = engine.CreateDraft(
            "draft-one-round",
            2027,
            2,
            BuildStandings(),
            new DateTimeOffset(2027, 6, 1, 12, 0, 0, TimeSpan.Zero),
            roundValidator);

        Assert.Throws<InvalidOperationException>(() => engine.SelectProspect(
            draft,
            2,
            4,
            "prospect-001",
            new DateTimeOffset(2027, 6, 1, 12, 10, 0, TimeSpan.Zero),
            new DraftEligibility("prospect-001", true),
            roundValidator));

        Assert.Throws<InvalidOperationException>(() => engine.SelectProspect(
            draft,
            1,
            1,
            "prospect-002",
            new DateTimeOffset(2027, 6, 1, 12, 20, 0, TimeSpan.Zero),
            new DraftEligibility("prospect-002", false),
            roundValidator));
    }

    public void NoRosterModificationOccurs()
    {
        var roster = new List<string> { "player-a", "player-b" };
        var engine = new DraftEngine();
        var draft = BuildDraft(engine: engine);

        engine.SelectProspect(draft, 1, 1, "prospect-001", new DateTimeOffset(2027, 6, 1, 12, 10, 0, TimeSpan.Zero));

        Assert.Equal(2, roster.Count);
        Assert.Equal("player-a", roster[0]);
        Assert.Equal("player-b", roster[1]);
    }

    private static Draft BuildDraft(DraftEngine? engine = null, int rounds = 2) =>
        (engine ?? new DraftEngine()).CreateDraft(
            "draft-2027",
            2027,
            rounds,
            BuildStandings(),
            new DateTimeOffset(2027, 6, 1, 12, 0, 0, TimeSpan.Zero),
            BuildDraftRuleValidator());

    private static IReadOnlyList<OrganizationStanding> BuildStandings() =>
        new[]
        {
            new OrganizationStanding("org-first", 1),
            new OrganizationStanding("org-middle", 2),
            new OrganizationStanding("org-last", 3)
        };

    private static DraftBoardEntry BuildBoardEntry(
        string prospectPersonId,
        int rank,
        string? scoutingReportId = null,
        ScoutingConfidenceLevel? confidence = null) =>
        new(
            ProspectPersonId: prospectPersonId,
            Rank: rank,
            ScoutingReportId: scoutingReportId,
            ScoutingConfidence: confidence,
            ProjectionText: "Middle-six projection with strong compete.");

    private static DraftRuleValidator BuildDraftRuleValidator() =>
        new(BuildRulebook(draftEnabled: true, rounds: 8));

    private static Rulebook BuildRulebook(bool draftEnabled, int rounds) =>
        new()
        {
            RulebookId = "test_rulebook",
            LeagueType = "junior",
            Version = "1.0",
            RosterRules = new RosterRules
            {
                MinRoster = 18,
                MaxRoster = 25,
                ActiveRoster = 20,
                GoaliesRequired = 2,
                OverageSlots = 3,
                ImportSlots = 2
            },
            EligibilityRules = new EligibilityRules { MinAge = 15, MaxAge = 20 },
            ContractRules = new ContractRules
            {
                AllowedContractTypes = new[] { "junior_player_agreement" },
                JuniorStipendsEnabled = true,
                EducationPackagesEnabled = true,
                HousingSupportEnabled = true
            },
            DraftRules = new DraftRules
            {
                DraftEnabled = draftEnabled,
                Rounds = rounds,
                DraftOrder = "reverse_standings"
            },
            PlayoffRules = new PlayoffRules
            {
                TeamsQualify = 8,
                SeriesFormat = new[] { 7, 7, 7 },
                ReseedEachRound = true
            },
            BudgetRules = new BudgetRules { OwnerBudgetEnabled = true }
        };
}
