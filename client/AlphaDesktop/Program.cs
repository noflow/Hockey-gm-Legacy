using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LegacyEngine.Integration;

namespace AlphaDesktop;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Contains("--smoke-test", StringComparer.OrdinalIgnoreCase))
        {
            var state = AlphaDesktopState.Create();
            Console.WriteLine($"AlphaDesktop smoke test: {state.Snapshot.WorldState.WorldName} {state.Snapshot.CurrentDate:yyyy-MM-dd}");
            return;
        }

        var app = new Application();
        app.Run(new MainWindow());
    }
}

internal sealed class MainWindow : Window
{
    private readonly AlphaDesktopState _state = AlphaDesktopState.Create();
    private readonly TextBlock _dateText = new();
    private readonly TextBlock _summaryText = new();
    private readonly TextBlock _processedText = new();
    private readonly Dictionary<string, TextBox> _tabs = [];

    public MainWindow()
    {
        Title = "Hockey GM Legacy - Alpha Desktop";
        Width = 1180;
        Height = 780;
        MinWidth = 920;
        MinHeight = 620;
        Background = new SolidColorBrush(Color.FromRgb(245, 247, 250));

        Content = BuildLayout();
        RefreshAll();
    }

    private UIElement BuildLayout()
    {
        var root = new DockPanel();

        var header = BuildHeader();
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var tabs = new TabControl
        {
            Margin = new Thickness(12),
            Background = Brushes.White
        };

        AddTab(tabs, "Dashboard");
        AddTab(tabs, "Inbox");
        AddTab(tabs, "Owner");
        AddTab(tabs, "Staff");
        AddTab(tabs, "Roster");
        AddTab(tabs, "Recruits");
        AddTab(tabs, "Scouting");
        AddTab(tabs, "Draft Board");
        AddTab(tabs, "Relationships");

        root.Children.Add(tabs);
        return root;
    }

    private UIElement BuildHeader()
    {
        var header = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(20, 40, 64)),
            Padding = new Thickness(16)
        };

        var panel = new Grid();
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var textPanel = new StackPanel();
        textPanel.Children.Add(new TextBlock
        {
            Text = "Hockey GM Legacy - Alpha 0.5",
            Foreground = Brushes.White,
            FontSize = 22,
            FontWeight = FontWeights.SemiBold
        });

        _dateText.Foreground = Brushes.White;
        _dateText.FontSize = 14;
        _dateText.Margin = new Thickness(0, 4, 0, 0);
        textPanel.Children.Add(_dateText);

        _summaryText.Foreground = new SolidColorBrush(Color.FromRgb(210, 225, 240));
        _summaryText.TextWrapping = TextWrapping.Wrap;
        _summaryText.Margin = new Thickness(0, 6, 0, 0);
        textPanel.Children.Add(_summaryText);

        _processedText.Foreground = new SolidColorBrush(Color.FromRgb(210, 225, 240));
        _processedText.Margin = new Thickness(0, 4, 0, 0);
        textPanel.Children.Add(_processedText);

        Grid.SetColumn(textPanel, 0);
        panel.Children.Add(textPanel);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        buttonPanel.Children.Add(CreateButton("Advance Day", () => Advance(1)));
        buttonPanel.Children.Add(CreateButton("Advance 7 Days", () => Advance(7)));

        Grid.SetColumn(buttonPanel, 1);
        panel.Children.Add(buttonPanel);

        header.Child = panel;
        return header;
    }

    private Button CreateButton(string text, Action action)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(14, 9, 14, 9),
            Margin = new Thickness(8, 0, 0, 0),
            FontWeight = FontWeights.SemiBold
        };

        button.Click += (_, _) =>
        {
            action();
            RefreshAll();
        };

        return button;
    }

    private void AddTab(TabControl tabs, string title)
    {
        var text = new TextBox
        {
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            BorderThickness = new Thickness(0),
            Background = Brushes.White,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 14,
            Padding = new Thickness(16)
        };

        _tabs[title] = text;
        tabs.Items.Add(new TabItem
        {
            Header = title,
            Content = text
        });
    }

    private void Advance(int days) => _state.Advance(days);

    private void RefreshAll()
    {
        var snapshot = _state.Snapshot;
        _dateText.Text = $"Current date: {snapshot.CurrentDate:yyyy-MM-dd}";
        _summaryText.Text = _state.LatestSummary;
        _processedText.Text = $"Last processed events: {_state.LastProcessedEventCount} | Inbox items: {_state.Inbox.Count}";

        _tabs["Dashboard"].Text = BuildDashboard();
        _tabs["Inbox"].Text = BuildInbox();
        _tabs["Owner"].Text = BuildOwner();
        _tabs["Staff"].Text = BuildStaff();
        _tabs["Roster"].Text = BuildRoster();
        _tabs["Recruits"].Text = BuildRecruits();
        _tabs["Scouting"].Text = BuildScouting();
        _tabs["Draft Board"].Text = BuildDraftBoard();
        _tabs["Relationships"].Text = BuildRelationships();
    }

    private string BuildDashboard()
    {
        var snapshot = _state.Snapshot;
        var builder = new StringBuilder();
        builder.AppendLine("Dashboard");
        builder.AppendLine("=========");
        builder.AppendLine($"World: {snapshot.WorldState.WorldName}");
        builder.AppendLine($"Date: {snapshot.CurrentDate:yyyy-MM-dd}");
        builder.AppendLine($"Owner: {snapshot.Owner.Name}");
        builder.AppendLine($"GM: {snapshot.GeneralManager.Identity.DisplayName}");
        builder.AppendLine($"Scout: {snapshot.Scout.Name}");
        builder.AppendLine($"Coach: {snapshot.CoachPerson?.Identity.DisplayName ?? "Not assigned"}");
        builder.AppendLine();
        builder.AppendLine("Counts");
        builder.AppendLine($"People: {snapshot.People.Count}");
        builder.AppendLine($"Roster players: {snapshot.Roster.Players.Count}");
        builder.AppendLine($"Recruits: {snapshot.Recruits.Count}");
        builder.AppendLine($"Draft board entries: {snapshot.DraftBoard.Entries.Count}");
        builder.AppendLine($"Relationships: {snapshot.Relationships.Count}");
        builder.AppendLine($"Development profiles: {snapshot.DevelopmentProfiles.Count}");
        builder.AppendLine($"Active injuries: {snapshot.Injuries.Count(injury => injury.IsActive)}");
        builder.AppendLine();
        builder.AppendLine("Latest Summary");
        builder.AppendLine(_state.LatestSummary);
        return builder.ToString();
    }

    private string BuildInbox()
    {
        if (_state.Inbox.Count == 0)
        {
            return "Inbox\n=====\nNo inbox items yet. Advance a day to process alpha events.";
        }

        var builder = new StringBuilder();
        builder.AppendLine("Inbox");
        builder.AppendLine("=====");
        foreach (var item in _state.Inbox.OrderByDescending(item => item.Date))
        {
            builder.AppendLine($"[{item.Date:yyyy-MM-dd}] {item.Title} ({item.EventType}, {item.Severity})");
            builder.AppendLine($"  {item.Summary}");
            if (!string.IsNullOrWhiteSpace(item.PrimaryPersonId))
            {
                builder.AppendLine($"  Person: {FindPersonName(item.PrimaryPersonId)}");
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private string BuildOwner()
    {
        var owner = _state.Snapshot.Owner;
        var builder = new StringBuilder();
        builder.AppendLine("Owner");
        builder.AppendLine("=====");
        builder.AppendLine($"{owner.Name} - {owner.Archetype}");
        builder.AppendLine($"Autonomy: {owner.AutonomyLevel}");
        builder.AppendLine($"Trust: {owner.Trust}  Confidence: {owner.Confidence}  Patience: {owner.Patience}");
        builder.AppendLine($"Budget total: {owner.Budget.Total:C0}");
        builder.AppendLine($"Player payroll: {owner.Budget.PlayerPayroll:C0}");
        builder.AppendLine($"Staff: {owner.Budget.Staff:C0}");
        builder.AppendLine($"Scouting: {owner.Budget.Scouting:C0}");
        builder.AppendLine($"Facilities: {owner.Budget.Facilities:C0}");
        builder.AppendLine($"Operations: {owner.Budget.Operations:C0}");
        builder.AppendLine();
        builder.AppendLine("Goals");
        foreach (var goal in owner.Goals.OrderByDescending(goal => goal.Priority))
        {
            builder.AppendLine($"Priority {goal.Priority}: {goal.GoalType} - {goal.Description}");
        }

        return builder.ToString();
    }

    private string BuildStaff()
    {
        var snapshot = _state.Snapshot;
        var builder = new StringBuilder();
        builder.AppendLine("Staff");
        builder.AppendLine("=====");
        builder.AppendLine($"GM: {snapshot.GeneralManager.Identity.DisplayName}");
        builder.AppendLine($"  Reputation: local {snapshot.GeneralManager.Reputation.Local}, league {snapshot.GeneralManager.Reputation.League}, national {snapshot.GeneralManager.Reputation.National}");
        builder.AppendLine($"Scout: {snapshot.Scout.Name}");
        builder.AppendLine($"  Accuracy: {snapshot.Scout.Accuracy}  Diligence: {snapshot.Scout.Diligence}  Bias: {snapshot.Scout.ReportBias}");
        builder.AppendLine($"  Specialties: {string.Join(", ", snapshot.Scout.Specialties)}");
        builder.AppendLine($"Coach: {snapshot.CoachPerson?.Identity.DisplayName ?? "Not assigned"}");
        return builder.ToString();
    }

    private string BuildRoster()
    {
        var snapshot = _state.Snapshot;
        var builder = new StringBuilder();
        builder.AppendLine("Roster");
        builder.AppendLine("======");
        foreach (var player in snapshot.Roster.Players)
        {
            var injury = snapshot.Injuries.FirstOrDefault(injury => injury.PersonId == player.PersonId && injury.IsActive);
            var development = snapshot.DevelopmentProfiles.FirstOrDefault(profile => profile.PersonId == player.PersonId);
            builder.AppendLine($"{FindPersonName(player.PersonId)} - {player.Position} - {player.Status}");
            builder.AppendLine($"  Health: {(injury is null ? "available" : $"{injury.Severity} {injury.InjuryType}, {injury.Status}")}");
            builder.AppendLine($"  Development: {(development is null ? "not tracked" : $"{development.Stage}, last updated {development.LastUpdated:yyyy-MM-dd}")}");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private string BuildRecruits()
    {
        var snapshot = _state.Snapshot;
        var builder = new StringBuilder();
        builder.AppendLine("Recruits");
        builder.AppendLine("========");
        foreach (var recruit in snapshot.Recruits)
        {
            var priorities = recruit.Priorities
                .OrderByDescending(priority => priority.Value)
                .Take(3)
                .Select(priority => $"{priority.Key} {priority.Value}");
            builder.AppendLine($"{FindPersonName(recruit.RecruitPersonId)} - {recruit.Status} - interest {recruit.GetInterest(snapshot.OrganizationId)}");
            builder.AppendLine($"  Priorities: {string.Join(", ", priorities)}");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private string BuildScouting()
    {
        var snapshot = _state.Snapshot;
        var builder = new StringBuilder();
        builder.AppendLine("Scouting");
        builder.AppendLine("========");
        builder.AppendLine($"{snapshot.Scout.Name} leads alpha scouting.");
        builder.AppendLine($"Accuracy {snapshot.Scout.Accuracy}, diligence {snapshot.Scout.Diligence}, specialties: {string.Join(", ", snapshot.Scout.Specialties)}");
        builder.AppendLine();
        builder.AppendLine("Board Notes");
        foreach (var entry in snapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank))
        {
            builder.AppendLine($"#{entry.Rank} {FindPersonName(entry.ProspectPersonId)} - {entry.ScoutingConfidence?.ToString() ?? "Unknown"} confidence");
            builder.AppendLine($"  {entry.ProjectionText}");
        }

        return builder.ToString();
    }

    private string BuildDraftBoard()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Draft Board");
        builder.AppendLine("===========");
        foreach (var entry in _state.Snapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank))
        {
            builder.AppendLine($"#{entry.Rank} {FindPersonName(entry.ProspectPersonId)} - confidence {entry.ScoutingConfidence?.ToString() ?? "Unknown"}");
            builder.AppendLine($"  {entry.ProjectionText}");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private string BuildRelationships()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Relationships");
        builder.AppendLine("=============");
        foreach (var relationship in _state.Snapshot.Relationships.OrderBy(item => item.RelationshipType.ToString(), StringComparer.Ordinal))
        {
            builder.AppendLine($"{relationship.RelationshipType}: {FindPersonName(relationship.FromPersonId)} -> {FindPersonName(relationship.ToPersonId)}");
            builder.AppendLine($"  Trust {relationship.Trust}, Respect {relationship.Respect}, Confidence {relationship.Confidence}, Loyalty {relationship.Loyalty}");
            builder.AppendLine($"  Influence {relationship.Influence}, Friendship {relationship.Friendship}, Rivalry {relationship.Rivalry}");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private string FindPersonName(string personId)
    {
        if (string.Equals(personId, _state.Snapshot.Owner.OwnerId, StringComparison.Ordinal))
        {
            return _state.Snapshot.Owner.Name;
        }

        var person = _state.Snapshot.People.SingleOrDefault(person => person.PersonId == personId);
        return person is null ? personId : person.Identity.DisplayName;
    }
}

internal sealed class AlphaDesktopState
{
    private readonly DailySimulationCoordinator _coordinator = new();
    private readonly EngineRegistry _registry;

    private AlphaDesktopState(EngineRegistry registry, AlphaWorldSnapshot snapshot)
    {
        _registry = registry;
        Snapshot = snapshot;
        LatestSummary = "Alpha world bootstrapped. Use Advance Day to begin.";
    }

    public AlphaWorldSnapshot Snapshot { get; private set; }

    public List<AlphaInboxItem> Inbox { get; } = [];

    public string LatestSummary { get; private set; }

    public int LastProcessedEventCount { get; private set; }

    public static AlphaDesktopState Create()
    {
        var alphaWorld = AlphaWorldBootstrapper.CreateAlphaWorld(new DateOnly(2026, 9, 1));
        return new AlphaDesktopState(alphaWorld.Registry, alphaWorld.Snapshot);
    }

    public void Advance(int days)
    {
        if (days <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(days), "Advance days must be positive.");
        }

        var totalProcessed = 0;
        var newInboxItems = new List<AlphaInboxItem>();
        AlphaSimulationResult? latest = null;

        for (var day = 0; day < days; day++)
        {
            latest = _coordinator.AdvanceOneDay(_registry, Snapshot);
            Snapshot = latest.WorldSnapshot;
            totalProcessed += latest.ProcessedEventCount;
            newInboxItems.AddRange(latest.InboxItems);
        }

        Inbox.AddRange(newInboxItems);
        LastProcessedEventCount = totalProcessed;
        LatestSummary = latest is null
            ? "No simulation result was produced."
            : days == 1
                ? latest.Summary
                : $"Advanced {Snapshot.WorldState.WorldName} over {days} days; processed {totalProcessed} event(s), created {newInboxItems.Count} inbox item(s), and ended on {Snapshot.CurrentDate:yyyy-MM-dd}.";
    }
}
