using LegacyEngine.Integration;

var app = new AlphaConsoleApp();
app.Run();

internal sealed class AlphaConsoleApp
{
    private readonly DailySimulationCoordinator _coordinator = new();
    private readonly EngineRegistry _registry;
    private AlphaWorldSnapshot _snapshot;
    private readonly List<AlphaInboxItem> _inbox = [];

    public AlphaConsoleApp()
    {
        var alphaWorld = AlphaWorldBootstrapper.CreateAlphaWorld(new DateOnly(2026, 9, 1));
        _registry = alphaWorld.Registry;
        _snapshot = alphaWorld.Snapshot;
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
            case "advance":
                Advance(parts);
                break;
            case "roster":
                ShowRoster();
                break;
            case "recruits":
                ShowRecruits();
                break;
            case "draftboard":
                ShowDraftBoard();
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
            Console.WriteLine("Engine-only playtest harness. No UI, no Godot, no game simulation.");
        }

        Console.WriteLine($"World: {_snapshot.WorldState.WorldName}");
        Console.WriteLine($"Date:  {_snapshot.CurrentDate:yyyy-MM-dd}");
        Console.WriteLine("Type 'help' for commands.");
        Console.WriteLine();
    }

    private static void ShowHelp()
    {
        Console.WriteLine("Available commands:");
        Console.WriteLine("  help        Lists available commands.");
        Console.WriteLine("  status      Shows world date, world name, and alpha counts.");
        Console.WriteLine("  inbox       Shows current inbox items.");
        Console.WriteLine("  advance     Advances one day.");
        Console.WriteLine("  advance 7   Advances seven days.");
        Console.WriteLine("  roster      Shows alpha roster players.");
        Console.WriteLine("  recruits    Shows recruit list and statuses.");
        Console.WriteLine("  draftboard  Shows draft board entries.");
        Console.WriteLine("  clear       Clears the console.");
        Console.WriteLine("  exit        Ends the program.");
        Console.WriteLine();
    }

    private void ShowStatus()
    {
        Console.WriteLine($"World: {_snapshot.WorldState.WorldName}");
        Console.WriteLine($"Date: {_snapshot.CurrentDate:yyyy-MM-dd}");
        Console.WriteLine($"People: {_snapshot.People.Count}");
        Console.WriteLine($"Recruits: {_snapshot.Recruits.Count}");
        Console.WriteLine($"Roster players: {_snapshot.Roster.Players.Count}");
        Console.WriteLine($"Draft board entries: {_snapshot.DraftBoard.Entries.Count}");
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
            totalProcessed += latestResult.ProcessedEventCount;
            newInboxItems.AddRange(latestResult.InboxItems);
            _inbox.AddRange(latestResult.InboxItems);
        }

        Console.WriteLine($"Advanced {days} day(s).");
        Console.WriteLine($"Date: {_snapshot.CurrentDate:yyyy-MM-dd}");
        Console.WriteLine($"Processed events: {totalProcessed}");
        Console.WriteLine(latestResult?.Summary ?? "No simulation result was produced.");

        if (newInboxItems.Count == 0)
        {
            Console.WriteLine("New inbox items: 0");
        }
        else
        {
            Console.WriteLine($"New inbox items: {newInboxItems.Count}");
            foreach (var item in newInboxItems)
            {
                Console.WriteLine($"  - {item.Title}: {item.Summary}");
            }
        }

        Console.WriteLine();
    }

    private void ShowRoster()
    {
        Console.WriteLine("Roster:");
        foreach (var player in _snapshot.Roster.Players)
        {
            Console.WriteLine($"  {FindPersonName(player.PersonId)} - {player.Position} - {player.Status}");
        }

        Console.WriteLine();
    }

    private void ShowRecruits()
    {
        Console.WriteLine("Recruits:");
        foreach (var recruit in _snapshot.Recruits)
        {
            var interest = recruit.GetInterest(_snapshot.OrganizationId);
            Console.WriteLine($"  {FindPersonName(recruit.RecruitPersonId)} - {recruit.Status} - interest {interest}");
        }

        Console.WriteLine();
    }

    private void ShowDraftBoard()
    {
        Console.WriteLine("Draft Board:");
        foreach (var entry in _snapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank))
        {
            var confidence = entry.ScoutingConfidence?.ToString() ?? "Unknown";
            Console.WriteLine($"  #{entry.Rank} {FindPersonName(entry.ProspectPersonId)} - confidence {confidence}");
            Console.WriteLine($"     {entry.ProjectionText}");
        }

        Console.WriteLine();
    }

    private string FindPersonName(string personId)
    {
        var person = _snapshot.People.SingleOrDefault(person => person.PersonId == personId);
        return person is null ? personId : person.Identity.DisplayName;
    }
}
