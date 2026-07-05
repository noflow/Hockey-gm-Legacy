using LegacyEngine.Events;
using LegacyEngine.Recruiting;
using LegacyEngine.Scouting;

namespace LegacyEngine.Integration;

public sealed class NewGmScenarioActions
{
    public GmActionResult MoveDraftBoardPlayer(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        string prospectPersonId,
        int direction)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        if (direction == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(direction), "Draft board direction must move up or down.");
        }

        var entry = scenario.AlphaSnapshot.DraftBoard.Entries.SingleOrDefault(item => item.ProspectPersonId == prospectPersonId)
            ?? throw new ArgumentException("Prospect is not on the draft board.", nameof(prospectPersonId));
        var newRank = Math.Clamp(entry.Rank + (direction < 0 ? -1 : 1), 1, scenario.AlphaSnapshot.DraftBoard.Entries.Count);
        var draftBoard = scenario.AlphaSnapshot.DraftBoard.UpdateRank(prospectPersonId, newRank);
        var name = FindPersonName(scenario, prospectPersonId);
        QueueActionEvent(
            registry,
            scenario,
            LegacyEventType.Generic,
            "Draft board updated",
            $"{name} was moved to rank {newRank} on the draft board.",
            prospectPersonId);

        return BuildResult(
            scenario,
            scenario.AlphaSnapshot with { DraftBoard = draftBoard },
            $"Draft board updated: {name} moved to rank {newRank}.",
            "Draft board updated",
            $"{name} now sits at rank {newRank}. Advance days to let staff react.");
    }

    public GmActionResult AssignScoutFocus(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        ScoutSpecialty focus)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var assignment = new ScoutingAssignment(
            AssignmentId: $"scouting-focus-{scenario.ScoutingAssignments.Count + 1:000}",
            ScoutId: scenario.AlphaSnapshot.Scout.ScoutId,
            AssignmentType: ScoutingAssignmentType.League,
            TargetId: scenario.AlphaSnapshot.OrganizationId,
            TargetName: $"{scenario.Organization.Name} draft preparation",
            FocusAreas: new[] { focus },
            AssignedOn: scenario.CurrentDate,
            DueOn: scenario.DraftDate);
        assignment.Validate();

        QueueActionEvent(
            registry,
            scenario,
            LegacyEventType.ScoutAssigned,
            "Scout focus assigned",
            $"{scenario.AlphaSnapshot.Scout.Name} was assigned to focus on {focus} before the draft.",
            scenario.AlphaSnapshot.ScoutPerson.PersonId);

        var updatedScenario = scenario with
        {
            ScoutingAssignments = scenario.ScoutingAssignments.Append(assignment).ToArray()
        };

        return BuildResult(
            updatedScenario,
            updatedScenario.AlphaSnapshot,
            $"Scout focus assigned: {focus}.",
            "Scout focus assigned",
            $"{scenario.AlphaSnapshot.Scout.Name} will focus on {focus} through draft day.");
    }

    public GmActionResult MakeRecruitingOffer(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        string recruitPersonId)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var recruit = scenario.AlphaSnapshot.Recruits.SingleOrDefault(item => item.RecruitPersonId == recruitPersonId)
            ?? throw new ArgumentException("Recruit was not found.", nameof(recruitPersonId));
        var updatedRecruit = registry.RecruitingEngine.SubmitOffer(recruit, scenario.AlphaSnapshot.OrganizationId, scenario.CurrentDate);
        var recruits = scenario.AlphaSnapshot.Recruits
            .Select(item => item.RecruitPersonId == recruitPersonId ? updatedRecruit : item)
            .ToArray();
        var name = FindPersonName(scenario, recruitPersonId);

        return BuildResult(
            scenario,
            scenario.AlphaSnapshot with { Recruits = recruits },
            $"Recruiting offer made to {name}.",
            "Recruiting offer submitted",
            $"{name} received a recruiting offer. Advance days to see how the recruit responds.",
            LegacyEventType.RecruitingOfferSubmitted,
            recruitPersonId);
    }

    private static GmActionResult BuildResult(
        NewGmScenarioSnapshot scenario,
        AlphaWorldSnapshot alphaSnapshot,
        string summary,
        string inboxTitle,
        string inboxSummary,
        LegacyEventType eventType = LegacyEventType.Generic,
        string? primaryPersonId = null)
    {
        var updatedScenario = scenario with { AlphaSnapshot = alphaSnapshot };
        var inbox = new[]
        {
            new AlphaInboxItem(
                InboxItemId: $"inbox:gm-action:{Guid.NewGuid():N}",
                Date: new DateTimeOffset(scenario.CurrentDate.Year, scenario.CurrentDate.Month, scenario.CurrentDate.Day, 13, 0, 0, TimeSpan.Zero),
                EventType: eventType,
                Severity: LegacyEventSeverity.Notice,
                Title: inboxTitle,
                Summary: inboxSummary,
                PrimaryPersonId: primaryPersonId ?? alphaSnapshot.GeneralManager.PersonId)
        };

        var result = new GmActionResult(updatedScenario, alphaSnapshot, inbox, summary);
        result.Validate();
        return result;
    }

    private static void QueueActionEvent(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        LegacyEventType eventType,
        string title,
        string description,
        string? primaryPersonId)
    {
        var date = scenario.CurrentDate;
        var legacyEvent = registry.EventEngine.CreateEvent(
            new DateTimeOffset(date.Year, date.Month, date.Day, 12, 30, 0, TimeSpan.Zero),
            eventType,
            LegacyEventSeverity.Notice,
            LegacyEventVisibility.Organization,
            title,
            description,
            new LegacyEventContext(primaryPersonId, OrganizationId: scenario.AlphaSnapshot.OrganizationId),
            new Dictionary<string, object?>
            {
                ["scenario"] = "alpha_1_1_gm_action",
                ["gm_person_id"] = scenario.AlphaSnapshot.GeneralManager.PersonId
            });

        registry.EventEngine.QueueEvent(legacyEvent);
    }

    private static string FindPersonName(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.SingleOrDefault(person => person.PersonId == personId)?.Identity.DisplayName ?? personId;
}
