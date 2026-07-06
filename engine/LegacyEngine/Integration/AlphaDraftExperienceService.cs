using LegacyEngine.Draft;
using LegacyEngine.Events;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;

namespace LegacyEngine.Integration;

public sealed class AlphaDraftExperienceService
{
    private static readonly IReadOnlyDictionary<string, string> DefaultOrganizationNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["org-prairie-falcons"] = "Prairie Falcons",
            ["org-swift-current-riders"] = "Swift Current Riders",
            ["org-regina-plainsmen"] = "Regina Plainsmen",
            ["org-brandon-steel"] = "Brandon Steel"
        };

    public DraftExperienceResult StartDraftDay(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        if (scenario.CurrentDate < scenario.DraftDate)
        {
            throw new InvalidOperationException("Draft day has not arrived yet.");
        }

        var rulebook = ResolveRulebook(registry);
        if (rulebook.DraftRules is not { DraftEnabled: true })
        {
            var disabledState = new DraftExperienceState(
                DraftExperienceStatus.Disabled,
                Draft: null,
                PlayerOrganizationId: scenario.AlphaSnapshot.OrganizationId,
                OrganizationNames: OrganizationNamesFor(scenario),
                Selections: Array.Empty<DraftPickSummary>(),
                Recap: null,
                CountdownPlaceholder: "Draft disabled by rulebook.");

            return BuildResult(
                scenario with { DraftExperience = disabledState },
                disabledState,
                "Draft is disabled for this league.",
                Array.Empty<AlphaInboxItem>());
        }

        var draft = registry.DraftEngine.CreateDraftFromRulebook(
            DraftIdFor(scenario),
            scenario.Season.Year,
            BuildStandings(scenario),
            AtCurrentDate(scenario, 10, 0),
            rulebook);

        var state = new DraftExperienceState(
            DraftExperienceStatus.InProgress,
            draft,
            scenario.AlphaSnapshot.OrganizationId,
            OrganizationNamesFor(scenario),
            Array.Empty<DraftPickSummary>(),
            Recap: null,
            CountdownPlaceholder: "Countdown placeholder - no live timer in Alpha 1.4.");

        var updatedScenario = scenario with { DraftExperience = state };
        return BuildResult(
            updatedScenario,
            state,
            $"Draft day started: {state.TotalRounds} rulebook-driven round(s).",
            new[]
            {
                Inbox(
                    scenario,
                    LegacyEventType.DraftStarted,
                    "Draft day is underway",
                    $"The league draft has started with {state.TotalRounds} rounds from the active rulebook.")
            });
    }

    public DraftExperienceResult RunAiPicksUntilPlayerTurn(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        var state = RequireActiveState(scenario);

        var draft = state.Draft!;
        var board = scenario.AlphaSnapshot.DraftBoard;
        var selections = state.Selections.ToList();
        var inboxItems = new List<AlphaInboxItem>();
        var picked = 0;

        while (state.CurrentPick is { } pick && pick.OwningOrganizationId != state.PlayerOrganizationId)
        {
            var prospectId = SelectAiProspect(board, draft, pick.PickNumber);
            var selection = registry.DraftEngine.SelectProspect(
                draft,
                pick.RoundNumber,
                pick.PickNumber,
                prospectId,
                AtCurrentDate(scenario, 10, pick.PickNumber),
                ruleValidator: new DraftRuleValidator(ResolveRulebook(registry)));

            draft = selection.Draft;
            selections.Add(SummaryFor(scenario, state, selection.Pick, prospectId, isPlayerSelection: false));
            if (board.Entries.Any(entry => entry.ProspectPersonId == prospectId))
            {
                board = board.RemoveProspect(prospectId);
            }

            picked++;
            state = state with { Draft = draft, Selections = selections };
        }

        if (state.CurrentPick is null)
        {
            return CompleteDraft(registry, scenario, state, board, selections);
        }

        var awaiting = state with
        {
            Status = DraftExperienceStatus.AwaitingPlayerPick,
            Draft = draft,
            Selections = selections
        };

        var updatedScenario = scenario with
        {
            AlphaSnapshot = scenario.AlphaSnapshot with { DraftBoard = board },
            DraftExperience = awaiting
        };

        if (picked > 0)
        {
            inboxItems.Add(Inbox(
                scenario,
                LegacyEventType.DraftOpened,
                "Your pick is approaching",
                $"{picked} AI selection(s) were made. {awaiting.TeamSelecting} is now on the clock."));
        }

        return BuildResult(
            updatedScenario,
            awaiting,
            picked == 0
                ? $"{awaiting.TeamSelecting} is on the clock."
                : $"AI teams made {picked} pick(s); {awaiting.TeamSelecting} is on the clock.",
            inboxItems);
    }

    public DraftExperienceResult StartLiveDraft(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario)
    {
        var started = scenario.DraftExperience is null
            ? StartDraftDay(registry, scenario)
            : BuildResult(
                scenario,
                RequireActiveState(scenario),
                "Draft day is already active.",
                Array.Empty<AlphaInboxItem>());

        return started.DraftState.Status == DraftExperienceStatus.InProgress
            ? Combine(started, RunAiPicksUntilPlayerTurn(registry, started.ScenarioSnapshot))
            : started;
    }

    public DraftExperienceResult MakePlayerSelection(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        string prospectPersonId)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        var state = RequireActiveState(scenario);

        if (!state.IsPlayerTurn)
        {
            throw new InvalidOperationException("It is not the player's turn to pick.");
        }

        if (!scenario.AlphaSnapshot.DraftBoard.Entries.Any(entry => entry.ProspectPersonId == prospectPersonId))
        {
            throw new ArgumentException("Prospect must be available on the draft board.", nameof(prospectPersonId));
        }

        var pick = state.CurrentPick!;
        var result = registry.DraftEngine.SelectProspect(
            state.Draft!,
            pick.RoundNumber,
            pick.PickNumber,
            prospectPersonId,
            AtCurrentDate(scenario, 10, pick.PickNumber),
            ruleValidator: new DraftRuleValidator(ResolveRulebook(registry)));

        var board = scenario.AlphaSnapshot.DraftBoard.RemoveProspect(prospectPersonId);
        var pickSummary = SummaryFor(scenario, state, result.Pick, prospectPersonId, isPlayerSelection: true);
        var selections = state.Selections
            .Append(pickSummary)
            .ToArray();

        var updatedState = state with
        {
            Status = DraftExperienceStatus.InProgress,
            Draft = result.Draft,
            Selections = selections
        };

        var updatedScenario = scenario with
        {
            AlphaSnapshot = scenario.AlphaSnapshot with { DraftBoard = board },
            DraftExperience = updatedState,
            DraftRights = scenario.DraftRights.Append(pickSummary).ToArray(),
            ProspectRights = AddDraftRightsRecord(scenario, pickSummary).ToArray()
        };

        var prospectName = FindPersonName(scenario, prospectPersonId);
        QueueScenarioEvent(
            registry,
            scenario,
            LegacyEventType.OwnerDraftReaction,
            "Owner draft reaction",
            $"{scenario.AlphaSnapshot.Owner.Name} noted the selection of {prospectName}.");
        QueueScenarioEvent(
            registry,
            scenario,
            LegacyEventType.ScoutRecommendationUpdated,
            "Scout draft reaction",
            $"{scenario.AlphaSnapshot.Scout.Name} updated the staff notes after the {prospectName} selection.",
            scenario.AlphaSnapshot.ScoutPerson.PersonId);

        return BuildResult(
            updatedScenario,
            updatedState,
            $"Selected {prospectName} at pick {pick.PickNumber}.",
            new[]
            {
                Inbox(
                    scenario,
                    LegacyEventType.OwnerDraftReaction,
                    "Owner reaction",
                    $"{scenario.AlphaSnapshot.Owner.Name}: Good. Make sure this fits our development mandate."),
                Inbox(
                    scenario,
                    LegacyEventType.ScoutRecommendationUpdated,
                    "Head scout reaction",
                    $"{scenario.AlphaSnapshot.Scout.Name}: We had enough confidence to make that pick."),
                Inbox(
                    scenario,
                    LegacyEventType.ProspectDecisionMade,
                    "Prospect path decision needed",
                    $"{prospectName} is now on your draft rights list. You can offer a contract, invite him to camp, return him to junior/youth while retaining rights where allowed, assign him to an affiliate where valid, or release his rights.")
            });
    }

    public DraftExperienceResult MakePlayerSelectionAndContinue(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        string prospectPersonId)
    {
        var selected = MakePlayerSelection(registry, scenario, prospectPersonId);
        return selected.DraftState.Status == DraftExperienceStatus.InProgress
            ? Combine(selected, RunAiPicksUntilPlayerTurn(registry, selected.ScenarioSnapshot))
            : selected;
    }

    public DraftExperienceResult SimulateToCompletion(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario)
    {
        var current = scenario;
        if (current.DraftExperience is null)
        {
            current = StartDraftDay(registry, current).ScenarioSnapshot;
        }

        while (current.DraftExperience?.Status is not DraftExperienceStatus.Completed and not DraftExperienceStatus.Disabled)
        {
            var ai = RunAiPicksUntilPlayerTurn(registry, current);
            current = ai.ScenarioSnapshot;
            if (current.DraftExperience?.Status == DraftExperienceStatus.Completed)
            {
                return ai;
            }

            var boardEntry = current.AlphaSnapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank).First();
            current = MakePlayerSelection(registry, current, boardEntry.ProspectPersonId).ScenarioSnapshot;
        }

        return BuildResult(
            current,
            current.DraftExperience!,
            "Draft simulation completed.",
            Array.Empty<AlphaInboxItem>());
    }

    public GmActionResult StarProspect(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        string prospectPersonId,
        bool isStarred)
    {
        var board = scenario.AlphaSnapshot.DraftBoard.SetStarred(prospectPersonId, isStarred);
        return DraftBoardAction(
            registry,
            scenario,
            board,
            isStarred ? "Prospect starred" : "Prospect unstarred",
            $"{FindPersonName(scenario, prospectPersonId)} was {(isStarred ? "starred" : "unstarred")} on the draft board.",
            prospectPersonId);
    }

    public GmActionResult UpdatePersonalNotes(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        string prospectPersonId,
        string notes)
    {
        var board = scenario.AlphaSnapshot.DraftBoard.UpdatePersonalNotes(prospectPersonId, notes);
        return DraftBoardAction(
            registry,
            scenario,
            board,
            "GM notes updated",
            $"Personal draft notes were updated for {FindPersonName(scenario, prospectPersonId)}.",
            prospectPersonId);
    }

    private DraftExperienceResult CompleteDraft(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        DraftExperienceState state,
        DraftBoard board,
        IReadOnlyList<DraftPickSummary> selections)
    {
        var completedDraft = registry.DraftEngine.MarkCompleted(state.Draft!, AtCurrentDate(scenario, 16, 0));
        var completedState = state with
        {
            Status = DraftExperienceStatus.Completed,
            Draft = completedDraft,
            Selections = selections,
            Recap = BuildRecap(scenario, completedDraft, selections)
        };

        var updatedScenario = scenario with
        {
            AlphaSnapshot = scenario.AlphaSnapshot with { DraftBoard = board },
            DraftExperience = completedState
        };

        var inbox = new[]
        {
            Inbox(
                scenario,
                LegacyEventType.DraftRecapCreated,
                "Draft recap",
                $"{completedState.Recap!.PlayersDrafted} players were drafted. Owner reaction: {completedState.Recap.OwnerReaction}")
        };

        QueueScenarioEvent(
            registry,
            scenario,
            LegacyEventType.DraftRecapCreated,
            "Draft recap created",
            $"{completedState.Recap!.PlayersDrafted} players were drafted and the recap was created.");

        return BuildResult(
            updatedScenario,
            completedState,
            $"Draft complete: {completedState.Recap!.PlayersDrafted} player(s) drafted over {completedState.Recap.RoundsCompleted} round(s).",
            inbox);
    }

    private static DraftRecap BuildRecap(
        NewGmScenarioSnapshot scenario,
        LegacyEngine.Draft.Draft completedDraft,
        IReadOnlyList<DraftPickSummary> selections)
    {
        var yourSelections = selections.Where(item => item.IsPlayerSelection).ToArray();
        var otherSelections = selections.Where(item => !item.IsPlayerSelection).Take(5).ToArray();
        var steal = yourSelections
            .OrderByDescending(item => item.PickNumber)
            .FirstOrDefault();
        var surprise = selections
            .Where(item => !item.IsPlayerSelection)
            .OrderByDescending(item => item.PickNumber)
            .FirstOrDefault();

        return new DraftRecap(
            completedDraft.NumberOfRounds,
            completedDraft.Picks.Count(item => item.IsSelected),
            yourSelections,
            otherSelections,
            steal,
            surprise,
            $"{scenario.AlphaSnapshot.Owner.Name} wants the group developed patiently.",
            $"{scenario.AlphaSnapshot.Scout.Name} believes the board discipline held up.");
    }

    private static GmActionResult DraftBoardAction(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        DraftBoard board,
        string title,
        string summary,
        string prospectPersonId)
    {
        QueueScenarioEvent(registry, scenario, LegacyEventType.DraftBoardChanged, title, summary, prospectPersonId);
        var snapshot = scenario.AlphaSnapshot with { DraftBoard = board };
        var updatedScenario = scenario with { AlphaSnapshot = snapshot };
        return new GmActionResult(
            updatedScenario,
            snapshot,
            new[] { Inbox(scenario, LegacyEventType.DraftBoardChanged, title, summary, prospectPersonId) },
            summary);
    }

    private static DraftExperienceResult BuildResult(
        NewGmScenarioSnapshot scenario,
        DraftExperienceState state,
        string summary,
        IReadOnlyList<AlphaInboxItem> inboxItems)
    {
        var result = new DraftExperienceResult(scenario, state, inboxItems, summary);
        result.Validate();
        return result;
    }

    private static DraftExperienceResult Combine(DraftExperienceResult first, DraftExperienceResult second)
    {
        var result = new DraftExperienceResult(
            second.ScenarioSnapshot,
            second.DraftState,
            first.InboxItems.Concat(second.InboxItems).ToArray(),
            $"{first.Summary} {second.Summary}");
        result.Validate();
        return result;
    }

    private static DraftExperienceState RequireActiveState(NewGmScenarioSnapshot scenario)
    {
        var state = scenario.DraftExperience
            ?? throw new InvalidOperationException("Draft day has not started.");
        if (state.Status is DraftExperienceStatus.Completed or DraftExperienceStatus.Disabled)
        {
            throw new InvalidOperationException("Draft is not active.");
        }

        if (state.Draft is null)
        {
            throw new InvalidOperationException("Draft state is missing its active draft.");
        }

        return state;
    }

    private static Rulebook ResolveRulebook(EngineRegistry registry) =>
        registry.Rulebook ?? RulebookPresets.CreateJuniorMajor();

    private static IReadOnlyList<OrganizationStanding> BuildStandings(NewGmScenarioSnapshot scenario) =>
        new[]
        {
            new OrganizationStanding(scenario.AlphaSnapshot.OrganizationId, 1),
            new OrganizationStanding("org-swift-current-riders", 4),
            new OrganizationStanding("org-regina-plainsmen", 3),
            new OrganizationStanding("org-brandon-steel", 2)
        };

    private static IReadOnlyDictionary<string, string> OrganizationNamesFor(NewGmScenarioSnapshot scenario)
    {
        var names = new Dictionary<string, string>(DefaultOrganizationNames, StringComparer.Ordinal)
        {
            [scenario.AlphaSnapshot.OrganizationId] = scenario.Organization.Name
        };
        return names;
    }

    private static string SelectAiProspect(DraftBoard board, LegacyEngine.Draft.Draft draft, int pickNumber)
    {
        var selected = draft.Picks
            .Where(item => item.Selection is not null)
            .Select(item => item.Selection!.ProspectPersonId)
            .ToHashSet(StringComparer.Ordinal);

        return board.Entries
            .OrderBy(item => item.Rank)
            .FirstOrDefault(item => !selected.Contains(item.ProspectPersonId))
            ?.ProspectPersonId
            ?? $"auto-prospect-{pickNumber:000}";
    }

    private static DraftPickSummary SummaryFor(
        NewGmScenarioSnapshot scenario,
        DraftExperienceState state,
        DraftPick pick,
        string prospectPersonId,
        bool isPlayerSelection) =>
        new(
            pick.RoundNumber,
            pick.PickNumber,
            pick.OwningOrganizationId,
            state.OrganizationNames.GetValueOrDefault(pick.OwningOrganizationId, pick.OwningOrganizationId),
            prospectPersonId,
            FindPersonName(scenario, prospectPersonId),
            isPlayerSelection);

    private static IReadOnlyList<DraftRightsRecord> AddDraftRightsRecord(
        NewGmScenarioSnapshot scenario,
        DraftPickSummary pickSummary)
    {
        if (scenario.ProspectRights.Any(record => record.ProspectPersonId == pickSummary.ProspectPersonId))
        {
            return scenario.ProspectRights;
        }

        var person = scenario.AlphaSnapshot.People.SingleOrDefault(item => item.PersonId == pickSummary.ProspectPersonId)
            ?? scenario.AlphaSnapshot.Players.SingleOrDefault(item => item.PersonId == pickSummary.ProspectPersonId);
        var boardEntry = scenario.AlphaSnapshot.DraftBoard.Entries.SingleOrDefault(item => item.ProspectPersonId == pickSummary.ProspectPersonId);
        var record = new DraftRightsRecord(
            ProspectPersonId: pickSummary.ProspectPersonId,
            ProspectName: pickSummary.ProspectName,
            Age: person?.CalculateAge(scenario.CurrentDate) ?? 0,
            Position: GuessPosition(scenario, pickSummary.ProspectPersonId),
            RoundNumber: pickSummary.RoundNumber,
            PickNumber: pickSummary.PickNumber,
            Status: ProspectStatus.DraftRightsHeld,
            ProjectionText: boardEntry?.ProjectionText ?? "Drafted prospect with incomplete projection.",
            ScoutingConfidence: boardEntry?.ScoutingConfidence,
            GmNotes: boardEntry?.PersonalNotes ?? string.Empty);
        record.Validate();

        return scenario.ProspectRights.Append(record).ToArray();
    }

    private static RosterPosition GuessPosition(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.Roster.FindPlayer(personId)?.Position
        ?? scenario.AlphaSnapshot.DraftBoard.Entries
            .FirstOrDefault(entry => entry.ProspectPersonId == personId)?.Bio?.Position
        ?? RosterPosition.Unknown;

    private static void QueueScenarioEvent(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        LegacyEventType type,
        string title,
        string description,
        string? primaryPersonId = null)
    {
        var legacyEvent = registry.EventEngine.CreateEvent(
            AtCurrentDate(scenario, 12, 0),
            type,
            LegacyEventSeverity.Notice,
            LegacyEventVisibility.Organization,
            title,
            description,
            new LegacyEventContext(primaryPersonId, OrganizationId: scenario.AlphaSnapshot.OrganizationId),
            new Dictionary<string, object?> { ["scenario"] = "alpha_1_4_draft" });
        registry.EventEngine.QueueEvent(legacyEvent);
    }

    private static AlphaInboxItem Inbox(
        NewGmScenarioSnapshot scenario,
        LegacyEventType type,
        string title,
        string summary,
        string? primaryPersonId = null) =>
        new(
            $"inbox:alpha-draft:{Guid.NewGuid():N}",
            AtCurrentDate(scenario, 12, 0),
            type,
            LegacyEventSeverity.Notice,
            title,
            summary,
            primaryPersonId);

    private static string DraftIdFor(NewGmScenarioSnapshot scenario) =>
        $"draft:{scenario.Season.LeagueId}:{scenario.Season.Year}";

    private static DateTimeOffset AtCurrentDate(NewGmScenarioSnapshot scenario, int hour, int minute) =>
        new DateTimeOffset(scenario.CurrentDate.Year, scenario.CurrentDate.Month, scenario.CurrentDate.Day, hour, 0, 0, TimeSpan.Zero)
            .AddMinutes(minute);

    private static string FindPersonName(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.SingleOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? personId;
}
