using LegacyEngine.Events;
using LegacyEngine.Injuries;
using LegacyEngine.Owners;
using LegacyEngine.People;
using LegacyEngine.Rosters;
using LegacyEngine.Scouting;
using LegacyEngine.Seasons;

namespace LegacyEngine.Integration;

public sealed class NewGmFirstDayInboxFactory
{
    public IReadOnlyList<AlphaInboxItem> Create(
        DateOnly scenarioDate,
        DateOnly draftDate,
        Owner owner,
        Person generalManager,
        Person headCoach,
        Scout headScout,
        Roster roster,
        IReadOnlyList<Injury> injuries,
        Season season,
        string? generalManagerPreferredName = null)
    {
        var at = new DateTimeOffset(scenarioDate.Year, scenarioDate.Month, scenarioDate.Day, 8, 0, 0, TimeSpan.Zero);
        var daysUntilDraft = draftDate.DayNumber - scenarioDate.DayNumber;
        var items = new List<AlphaInboxItem>
        {
            new(
                InboxItemId: "new-gm-inbox-owner-welcome",
                Date: at,
                EventType: LegacyEventType.OwnerGoalSet,
                Severity: LegacyEventSeverity.Notice,
                Title: $"Welcome from {owner.Name}",
                Summary: $"Welcome aboard, {generalManagerPreferredName ?? generalManager.Identity.DisplayName}. Ownership expects a disciplined draft, clear roster plan, and visible progress in player development.",
                PrimaryPersonId: generalManager.PersonId),
            new(
                InboxItemId: "new-gm-inbox-draft-board",
                Date: at.AddMinutes(20),
                EventType: LegacyEventType.ScoutAssigned,
                Severity: LegacyEventSeverity.Notice,
                Title: $"Draft board from {headScout.Name}",
                Summary: $"The scouting staff has an initial board ready. The draft is in {daysUntilDraft} days, and the top tier still needs a final review.",
                PrimaryPersonId: generalManager.PersonId),
            new(
                InboxItemId: "new-gm-inbox-roster-needs",
                Date: at.AddMinutes(40),
                EventType: LegacyEventType.Generic,
                Severity: LegacyEventSeverity.Notice,
                Title: $"Roster notes from {headCoach.Identity.DisplayName}",
                Summary: $"The current roster has {roster.Players.Count} players. Coaching wants another defense option, more center depth, and clarity on injured players before camp.",
                PrimaryPersonId: headCoach.PersonId),
            new(
                InboxItemId: "new-gm-inbox-draft-timeline",
                Date: at.AddHours(1),
                EventType: LegacyEventType.MilestoneReached,
                Severity: LegacyEventSeverity.Notice,
                Title: "League draft timeline",
                Summary: $"The league office confirms draft preparation is underway. Current season phase is {season.CurrentPhase}; draft day is {draftDate:yyyy-MM-dd}.",
                PrimaryPersonId: generalManager.PersonId)
        };

        var activeInjury = injuries.FirstOrDefault(injury => injury.IsActive);
        if (activeInjury is not null)
        {
            items.Add(new AlphaInboxItem(
                InboxItemId: "new-gm-inbox-injury-warning",
                Date: at.AddHours(1).AddMinutes(20),
                EventType: LegacyEventType.PlayerInjured,
                Severity: LegacyEventSeverity.Warning,
                Title: "Medical note before roster review",
                Summary: $"One roster player is recovering from a {activeInjury.Severity} {activeInjury.InjuryType}. Expected return is {activeInjury.ExpectedReturnDate:yyyy-MM-dd}.",
                PrimaryPersonId: activeInjury.PersonId));
        }

        return items;
    }
}
