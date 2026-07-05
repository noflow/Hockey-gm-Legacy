using LegacyEngine.Integration;
using LegacyEngine.Relationships;

var app = new AlphaConsoleApp();
app.Run();

internal sealed class AlphaConsoleApp
{
    private readonly DailySimulationCoordinator _coordinator = new();
    private readonly EngineRegistry _registry;
    private AlphaWorldSnapshot _snapshot;
    private NewGmScenarioSnapshot _scenarioSnapshot;
    private readonly List<AlphaInboxItem> _inbox = [];

    public AlphaConsoleApp()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        _registry = scenario.Registry;
        _scenarioSnapshot = scenario.ScenarioSnapshot;
        _snapshot = scenario.AlphaSnapshot;
        _inbox.AddRange(scenario.FirstDayInbox);
    }

    public void Run()
    {
        ShowWelcome();

        while (true)
        {
            Console.Write("> ");
            var input = Console.ReadLine();
            if (input is null)
            {
                return;
            }

            var command = input.Trim();
            if (command.Length == 0)
            {
                continue;
            }

            if (string.Equals(command, "exit", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("AlphaConsole closed.");
                return;
            }

            Execute(command);
        }
    }

    private void Execute(string command)
    {
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var verb = parts[0].ToLowerInvariant();

        switch (verb)
        {
            case "help":
                ShowHelp();
                break;
            case "status":
                ShowStatus();
                break;
            case "inbox":
                ShowInbox();
                break;
            case "owner":
                ShowOwner();
                break;
            case "staff":
                ShowStaff();
                break;
            case "advance":
                Advance(parts);
                break;
            case "roster":
                ShowRoster();
                break;
            case "recruits":
                ShowRecruits();
                break;
            case "scouting":
                ShowScouting();
                break;
            case "draftboard":
                ShowDraftBoard();
                break;
            case "relationships":
                ShowRelationships();
                break;
            case "clear":
                Console.Clear();
                ShowWelcome(compact: true);
                break;
            default:
                Console.WriteLine($"Unknown command: {command}");
                Console.WriteLine("Type 'help' for available commands.");
                break;
        }
    }

    private void ShowWelcome(bool compact = false)
    {
        if (!compact)
        {
            Console.WriteLine("Hockey GM Legacy - AlphaConsole");
            Console.WriteLine("Alpha 1.0 - New GM Scenario");
            Console.WriteLine("Engine-only playtest harness. No UI, no Godot, no game simulation.");
        }

        Console.WriteLine($"World: {_snapshot.WorldState.WorldName}");
        Console.WriteLine($"Club:  {_scenarioSnapshot.Organization.Name}");
        Console.WriteLine($"Date:  {_snapshot.CurrentDate:yyyy-MM-dd}");
        Console.WriteLine($"Draft: {_scenarioSnapshot.DraftDate:yyyy-MM-dd} ({_scenarioSnapshot.DaysUntilDraft} days away)");
        Console.WriteLine("Type 'help' for commands.");
        Console.WriteLine();
    }

    private static void ShowHelp()
    {
        Console.WriteLine("Available commands:");
        Console.WriteLine("  help        Lists available commands.");
        Console.WriteLine("  status      Shows world date, world name, and alpha counts.");
        Console.WriteLine("  inbox       Shows current inbox items.");
        Console.WriteLine("  owner       Shows owner goals, budget, trust, confidence, and patience.");
        Console.WriteLine("  staff       Shows GM, scout, and coach/staff summary.");
        Console.WriteLine("  roster      Shows alpha roster players.");
        Console.WriteLine("  recruits    Shows recruit list and statuses.");
        Console.WriteLine("  scouting    Shows scouting department and current scouting board notes.");
        Console.WriteLine("  draftboard  Shows draft board entries.");
        Console.WriteLine("  relationships Shows key relationship scores.");
        Console.WriteLine("  advance     Advances one day.");
        Console.WriteLine("  advance 7   Advances seven days.");
        Console.WriteLine("  exit        Ends the program.");
        Console.WriteLine();
    }

    private void ShowStatus()
    {
        WriteSection("Club Status");
        Console.WriteLine($"World: {_snapshot.WorldState.WorldName}");
        Console.WriteLine($"Club: {_scenarioSnapshot.Organization.Name}");
        Console.WriteLine($"Date: {_snapshot.CurrentDate:yyyy-MM-dd}");
        Console.WriteLine($"Season phase: {_scenarioSnapshot.Season.CurrentPhase}");
        Console.WriteLine($"Draft date: {_scenarioSnapshot.DraftDate:yyyy-MM-dd} ({_scenarioSnapshot.DaysUntilDraft} days away)");
        Console.WriteLine($"People: {_snapshot.People.Count}");
        Console.WriteLine($"Recruits: {_snapshot.Recruits.Count}");
        Console.WriteLine($"Roster players: {_snapshot.Roster.Players.Count}");
        Console.WriteLine($"Draft board entries: {_snapshot.DraftBoard.Entries.Count}");
        Console.WriteLine($"Relationships: {_snapshot.Relationships.Count}");
        Console.WriteLine($"Development profiles: {_snapshot.DevelopmentProfiles.Count}");
        Console.WriteLine($"Active injuries: {_snapshot.Injuries.Count(injury => injury.IsActive)}");
        Console.WriteLine($"Staff members: {_snapshot.StaffMembers.Count}");
        Console.WriteLine($"Contract references: {_snapshot.Contracts.Count}");
        Console.WriteLine($"Inbox items: {_inbox.Count}");
        Console.WriteLine();
    }

    private void ShowInbox()
    {
        if (_inbox.Count == 0)
        {
            Console.WriteLine("Inbox is empty.");
            Console.WriteLine();
            return;
        }

        foreach (var item in _inbox.OrderByDescending(item => item.Date))
        {
            Console.WriteLine($"[{item.Date:yyyy-MM-dd}] {item.Title} ({item.EventType}, {item.Severity})");
            Console.WriteLine($"  {item.Summary}");
            if (!string.IsNullOrWhiteSpace(item.PrimaryPersonId))
            {
                Console.WriteLine($"  Person: {FindPersonName(item.PrimaryPersonId)}");
            }
        }

        Console.WriteLine();
    }

    private void ShowOwner()
    {
        var owner = _snapshot.Owner;
        WriteSection("Owner");
        Console.WriteLine($"{owner.Name} - {owner.Archetype}");
        Console.WriteLine($"Autonomy: {owner.AutonomyLevel}");
        Console.WriteLine($"Trust: {owner.Trust}  Confidence: {owner.Confidence}  Patience: {owner.Patience}");
        Console.WriteLine($"Budget total: {owner.Budget.Total:C0}");
        Console.WriteLine($"  Player payroll: {owner.Budget.PlayerPayroll:C0}");
        Console.WriteLine($"  Staff: {owner.Budget.Staff:C0}");
        Console.WriteLine($"  Scouting: {owner.Budget.Scouting:C0}");
        Console.WriteLine($"  Facilities: {owner.Budget.Facilities:C0}");
        Console.WriteLine($"  Operations: {owner.Budget.Operations:C0}");
        Console.WriteLine("Goals:");
        foreach (var goal in owner.Goals.OrderByDescending(goal => goal.Priority))
        {
            Console.WriteLine($"  Priority {goal.Priority}: {goal.GoalType} - {goal.Description}");
        }

        Console.WriteLine();
    }

    private void ShowStaff()
    {
        WriteSection("Staff");
        Console.WriteLine($"GM: {_snapshot.GeneralManager.Identity.DisplayName}");
        Console.WriteLine($"  Reputation: local {_snapshot.GeneralManager.Reputation.Local}, league {_snapshot.GeneralManager.Reputation.League}, national {_snapshot.GeneralManager.Reputation.National}");
        Console.WriteLine($"Scout: {_snapshot.Scout.Name}");
        Console.WriteLine($"  Accuracy: {_snapshot.Scout.Accuracy}  Diligence: {_snapshot.Scout.Diligence}  Bias: {_snapshot.Scout.ReportBias}");
        Console.WriteLine($"  Specialties: {string.Join(", ", _snapshot.Scout.Specialties)}");

        if (_snapshot.CoachPerson is null)
        {
            Console.WriteLine("Coach: not assigned in this alpha world.");
        }
        else
        {
            Console.WriteLine($"Coach: {_snapshot.CoachPerson.Identity.DisplayName}");
            var coachRole = _snapshot.CoachPerson.Roles.FirstOrDefault(role => role.OrganizationId == _snapshot.OrganizationId);
            Console.WriteLine($"  Role: {coachRole?.Title ?? "Coach"}");
        }

        if (_snapshot.StaffMembers.Count > 0)
        {
            Console.WriteLine("Staff room:");
            foreach (var member in _snapshot.StaffMembers.OrderBy(member => member.Department).ThenBy(member => member.CurrentRole))
            {
                Console.WriteLine($"  {FindPersonName(member.PersonId)} - {member.CurrentRole} - {member.EmploymentStatus}");
                Console.WriteLine($"     Department: {member.Department}, experience {member.Profile.YearsExperience} years, contract {member.ContractId ?? "none"}");
            }
        }

        Console.WriteLine();
    }

    private void Advance(IReadOnlyList<string> parts)
    {
        var days = 1;
        if (parts.Count > 1 && (!int.TryParse(parts[1], out days) || days <= 0))
        {
            Console.WriteLine("Usage: advance or advance <positive-days>");
            Console.WriteLine();
            return;
        }

        var totalProcessed = 0;
        var newInboxItems = new List<AlphaInboxItem>();
        AlphaSimulationResult? latestResult = null;

        for (var day = 0; day < days; day++)
        {
            latestResult = _coordinator.AdvanceOneDay(_registry, _snapshot);
            _snapshot = latestResult.WorldSnapshot;
            _scenarioSnapshot = _scenarioSnapshot with
            {
                AlphaSnapshot = _snapshot,
                Season = _snapshot.Season ?? _scenarioSnapshot.Season
            };
            totalProcessed += latestResult.ProcessedEventCount;
            newInboxItems.AddRange(latestResult.InboxItems);
            _inbox.AddRange(latestResult.InboxItems);
        }

        Console.WriteLine($"Advanced {days} day(s).");
        Console.WriteLine($"Date: {_snapshot.CurrentDate:yyyy-MM-dd}");
        Console.WriteLine($"Processed events: {totalProcessed}");
        Console.WriteLine(BuildAdvanceSummary(days, totalProcessed, newInboxItems.Count, latestResult));
        if (latestResult is not null)
        {
            Console.WriteLine(days == 1 ? "Pipeline:" : "Final day pipeline:");
            foreach (var entry in latestResult.LogEntries)
            {
                Console.WriteLine($"  - {entry.Step}: {entry.Message}");
            }
        }

        if (newInboxItems.Count == 0)
        {
            Console.WriteLine("New inbox items: 0");
        }
        else
        {
            Console.WriteLine($"New inbox items: {newInboxItems.Count}");
            foreach (var item in newInboxItems.Take(10))
            {
                Console.WriteLine($"  - {item.Title}: {item.Summary}");
            }

            if (newInboxItems.Count > 10)
            {
                Console.WriteLine($"  ...and {newInboxItems.Count - 10} more. Use 'inbox' to review all items.");
            }
        }

        Console.WriteLine();
    }

    private void ShowRoster()
    {
        WriteSection("Roster");
        foreach (var player in _snapshot.Roster.Players)
        {
            var injury = _snapshot.Injuries.FirstOrDefault(injury => injury.PersonId == player.PersonId && injury.IsActive);
            var development = _snapshot.DevelopmentProfiles.FirstOrDefault(profile => profile.PersonId == player.PersonId);
            var note = injury is null ? "available" : $"{injury.Severity} {injury.InjuryType}, {injury.Status}";
            var developmentNote = development is null ? "development not tracked" : $"{development.Stage}, last updated {development.LastUpdated:yyyy-MM-dd}";
            Console.WriteLine($"  {FindPersonName(player.PersonId)} - {player.Position} - {player.Status}");
            Console.WriteLine($"     Health: {note}");
            Console.WriteLine($"     Development: {developmentNote}");
        }

        Console.WriteLine();
    }

    private void ShowRecruits()
    {
        WriteSection("Recruits");
        foreach (var recruit in _snapshot.Recruits)
        {
            var interest = recruit.GetInterest(_snapshot.OrganizationId);
            Console.WriteLine($"  {FindPersonName(recruit.RecruitPersonId)} - {recruit.Status} - interest {interest}");
            var topPriorities = recruit.Priorities
                .OrderByDescending(priority => priority.Value)
                .Take(3)
                .Select(priority => $"{priority.Key} {priority.Value}");
            Console.WriteLine($"     Priorities: {string.Join(", ", topPriorities)}");
        }

        Console.WriteLine();
    }

    private void ShowScouting()
    {
        WriteSection("Scouting");
        Console.WriteLine($"{_snapshot.Scout.Name} leads alpha scouting.");
        Console.WriteLine($"Accuracy { _snapshot.Scout.Accuracy }, diligence { _snapshot.Scout.Diligence }, specialties: {string.Join(", ", _snapshot.Scout.Specialties)}");
        Console.WriteLine("Current board notes:");
        foreach (var entry in _snapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank))
        {
            var confidence = entry.ScoutingConfidence?.ToString() ?? "Unknown";
            Console.WriteLine($"  #{entry.Rank} {FindPersonName(entry.ProspectPersonId)} - {confidence} confidence");
            Console.WriteLine($"     {entry.ProjectionText}");
        }

        Console.WriteLine();
    }

    private void ShowDraftBoard()
    {
        WriteSection("Draft Board");
        foreach (var entry in _snapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank))
        {
            var confidence = entry.ScoutingConfidence?.ToString() ?? "Unknown";
            Console.WriteLine($"  #{entry.Rank} {FindPersonName(entry.ProspectPersonId)} - confidence {confidence}");
            Console.WriteLine($"     {entry.ProjectionText}");
        }

        Console.WriteLine();
    }

    private void ShowRelationships()
    {
        WriteSection("Relationships");
        foreach (var relationship in _snapshot.Relationships.OrderBy(item => item.RelationshipType.ToString(), StringComparer.Ordinal))
        {
            Console.WriteLine($"  {DescribeRelationship(relationship)}");
            Console.WriteLine($"     Trust {relationship.Trust}, Respect {relationship.Respect}, Confidence {relationship.Confidence}, Loyalty {relationship.Loyalty}");
            Console.WriteLine($"     Influence {relationship.Influence}, Friendship {relationship.Friendship}, Rivalry {relationship.Rivalry}");
        }

        Console.WriteLine();
    }

    private string FindPersonName(string personId)
    {
        if (string.Equals(personId, _snapshot.Owner.OwnerId, StringComparison.Ordinal))
        {
            return _snapshot.Owner.Name;
        }

        var person = _snapshot.People.SingleOrDefault(person => person.PersonId == personId);
        return person is null ? personId : person.Identity.DisplayName;
    }

    private string DescribeRelationship(Relationship relationship) =>
        $"{relationship.RelationshipType}: {FindPersonName(relationship.FromPersonId)} -> {FindPersonName(relationship.ToPersonId)}";

    private string BuildAdvanceSummary(
        int days,
        int totalProcessed,
        int newInboxCount,
        AlphaSimulationResult? latestResult)
    {
        if (latestResult is null)
        {
            return "No simulation result was produced.";
        }

        if (days == 1)
        {
            return latestResult.Summary;
        }

        return $"Advanced {_snapshot.WorldState.WorldName} over {days} days; processed {totalProcessed} event(s), created {newInboxCount} inbox item(s), and ended on {_snapshot.CurrentDate:yyyy-MM-dd}.";
    }

    private static void WriteSection(string title)
    {
        Console.WriteLine(title);
        Console.WriteLine(new string('-', title.Length));
    }
}
