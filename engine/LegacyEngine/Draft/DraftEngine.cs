using LegacyEngine.Events;
using LegacyEngine.RuleEngine;

namespace LegacyEngine.Draft;

public sealed class DraftEngine
{
    private readonly EventEngine _eventEngine;

    public DraftEngine(EventEngine? eventEngine = null)
    {
        _eventEngine = eventEngine ?? new EventEngine();
    }

    public EventEngine EventEngine => _eventEngine;

    public Draft CreateDraftFromRulebook(
        string draftId,
        int seasonYear,
        IReadOnlyList<OrganizationStanding> standings,
        DateTimeOffset createdAt,
        Rulebook rulebook)
    {
        ArgumentNullException.ThrowIfNull(rulebook);
        var rules = rulebook.DraftRules
            ?? throw new ArgumentException("Rulebook is missing draft rules.", nameof(rulebook));

        var validator = new DraftRuleValidator(rulebook);
        ValidateRound(validator, 1, isPlayerEligible: true);
        return CreateDraft(draftId, seasonYear, rules.Rounds, standings, createdAt, validator);
    }

    public Draft CreateDraft(
        string draftId,
        int seasonYear,
        int numberOfRounds,
        IReadOnlyList<OrganizationStanding> standings,
        DateTimeOffset createdAt,
        DraftRuleValidator? ruleValidator = null)
    {
        if (numberOfRounds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(numberOfRounds), "Draft must have at least one round.");
        }

        ValidateRound(ruleValidator, 1, isPlayerEligible: true);

        var order = DraftOrder.FromReverseStandings(standings);
        var picks = CreatePicks(draftId, numberOfRounds, order);
        var draft = new Draft(draftId, seasonYear, DraftStatus.InProgress, numberOfRounds, order, picks);
        draft.Validate();

        QueueDraftEvent(
            draft,
            LegacyEventType.DraftStarted,
            createdAt,
            "Draft started",
            "The annual draft started.",
            new Dictionary<string, object?>
            {
                ["season_year"] = seasonYear,
                ["rounds"] = numberOfRounds,
                ["pick_count"] = picks.Count
            });

        return draft;
    }

    public DraftSelectionResult SelectProspect(
        Draft draft,
        int roundNumber,
        int pickNumber,
        string prospectPersonId,
        DateTimeOffset selectedAt,
        DraftEligibility? eligibility = null,
        DraftRuleValidator? ruleValidator = null)
    {
        draft.Validate();

        if (draft.Status == DraftStatus.Completed)
        {
            throw new InvalidOperationException("Cannot select a prospect after the draft is completed.");
        }

        if (roundNumber < 1 || roundNumber > draft.NumberOfRounds)
        {
            throw new ArgumentOutOfRangeException(nameof(roundNumber), "Draft round is invalid.");
        }

        eligibility?.Validate();
        if (eligibility is not null && eligibility.ProspectPersonId != prospectPersonId)
        {
            throw new ArgumentException("Draft eligibility prospect id must match selected prospect.", nameof(eligibility));
        }

        ValidateRound(ruleValidator, roundNumber, eligibility?.IsEligible ?? true);

        if (string.IsNullOrWhiteSpace(prospectPersonId))
        {
            throw new ArgumentException("Prospect person id is required.", nameof(prospectPersonId));
        }

        if (draft.HasSelectedProspect(prospectPersonId))
        {
            throw new InvalidOperationException("Prospect has already been selected in this draft.");
        }

        var pick = draft.Picks.SingleOrDefault(item => item.RoundNumber == roundNumber && item.PickNumber == pickNumber);
        if (pick is null)
        {
            throw new ArgumentException("Draft pick was not found.", nameof(pickNumber));
        }

        var selection = new DraftSelection(prospectPersonId, selectedAt);
        var selectedPick = pick.Select(selection);
        var updatedDraft = draft with
        {
            Picks = draft.Picks
                .Select(item => item.PickId == selectedPick.PickId ? selectedPick : item)
                .OrderBy(item => item.RoundNumber)
                .ThenBy(item => item.PickNumber)
                .ToArray()
        };

        var legacyEvent = QueueDraftEvent(
            updatedDraft,
            LegacyEventType.PlayerDrafted,
            selectedAt,
            "Player drafted",
            "A player was selected in the draft.",
            new Dictionary<string, object?>
            {
                ["prospect_person_id"] = prospectPersonId,
                ["organization_id"] = selectedPick.OwningOrganizationId,
                ["round"] = roundNumber,
                ["pick"] = pickNumber
            },
            primaryPersonId: prospectPersonId,
            organizationId: selectedPick.OwningOrganizationId);

        return new DraftSelectionResult(
            updatedDraft,
            selectedPick,
            selection,
            legacyEvent,
            "Draft selection completed.");
    }

    public Draft MarkCompleted(Draft draft, DateTimeOffset completedAt)
    {
        draft.Validate();
        var completed = draft with { Status = DraftStatus.Completed };
        QueueDraftEvent(
            completed,
            LegacyEventType.DraftCompleted,
            completedAt,
            "Draft completed",
            "The annual draft was completed.",
            new Dictionary<string, object?>
            {
                ["season_year"] = completed.SeasonYear,
                ["selected_count"] = completed.Picks.Count(item => item.IsSelected)
            });

        return completed;
    }

    public DraftBoard CreateDraftBoard(string boardId, string organizationId) =>
        DraftBoard.Create(boardId, organizationId);

    private static IReadOnlyList<DraftPick> CreatePicks(string draftId, int numberOfRounds, DraftOrder order)
    {
        var picks = new List<DraftPick>();
        for (var round = 1; round <= numberOfRounds; round++)
        {
            for (var index = 0; index < order.OrganizationIds.Count; index++)
            {
                var pickNumber = ((round - 1) * order.OrganizationIds.Count) + index + 1;
                picks.Add(new DraftPick(
                    PickId: $"{draftId}:r{round}:p{pickNumber}",
                    RoundNumber: round,
                    PickNumber: pickNumber,
                    OwningOrganizationId: order.OrganizationIds[index],
                    Selection: null));
            }
        }

        return picks;
    }

    private static void ValidateRound(DraftRuleValidator? validator, int round, bool isPlayerEligible)
    {
        if (validator is null)
        {
            return;
        }

        var result = validator.Validate(new DraftValidationRequest(round, isPlayerEligible));
        if (!result.IsValid)
        {
            throw new InvalidOperationException(result.Message);
        }
    }

    private LegacyEvent QueueDraftEvent(
        Draft draft,
        LegacyEventType eventType,
        DateTimeOffset occurredAt,
        string title,
        string description,
        IReadOnlyDictionary<string, object?> metadata,
        string? primaryPersonId = null,
        string? organizationId = null)
    {
        var legacyEvent = _eventEngine.CreateEvent(
            occurredAt,
            eventType,
            LegacyEventSeverity.Notice,
            LegacyEventVisibility.League,
            title,
            description,
            new LegacyEventContext(
                PrimaryPersonId: primaryPersonId,
                OrganizationId: organizationId,
                SeasonId: draft.SeasonYear.ToString()),
            metadata);

        return _eventEngine.QueueEvent(legacyEvent);
    }
}
