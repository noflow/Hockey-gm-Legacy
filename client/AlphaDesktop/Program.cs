using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LegacyEngine.Draft;
using LegacyEngine.Integration;
using LegacyEngine.People;
using LegacyEngine.Recruiting;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;
using LegacyEngine.Scouting;
using LegacyEngine.Staff;

namespace AlphaDesktop;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Contains("--smoke-test", StringComparer.OrdinalIgnoreCase))
        {
            var state = AlphaDesktopState.Create();
            Console.WriteLine($"AlphaDesktop smoke test: Hockey GM Legacy Alpha 2.8 {state.Snapshot.CurrentDate:yyyy-MM-dd} draft in {state.ScenarioSnapshot.DaysUntilDraft} days");
            return;
        }

        var app = new Application();
        app.Run(new MainWindow());
    }
}

internal sealed class MainWindow : Window
{
    private sealed record WorkspaceScreen(string Label, UIElement Content);

    private AlphaDesktopState? _state;
    private readonly TextBlock _dateText = new();
    private readonly TextBlock _summaryText = new();
    private readonly TextBlock _processedText = new();
    private readonly Dictionary<string, TextBox> _tabs = [];
    private readonly Dictionary<string, TabItem> _tabItems = [];
    private readonly Dictionary<string, ListBox> _selectableLists = [];
    private readonly Dictionary<string, StackPanel> _selectableDetails = [];
    private readonly Dictionary<string, string> _selectedPeopleByTab = [];
    private TabControl? _mainTabs;
    private StackPanel? _dashboardPanel;
    private TextBox? _rosterSearchInput;
    private ComboBox? _rosterPositionFilter;
    private ComboBox? _rosterStatusFilter;
    private ComboBox? _rosterPlayerTypeFilter;
    private ComboBox? _rosterRoleFilter;
    private ComboBox? _rosterAgeFilter;
    private StackPanel? _inboxCategoryPanel;
    private StackPanel? _inboxListPanel;
    private Border? _inboxReader;
    private CheckBox? _unreadOnlyFilter;
    private CheckBox? _pinnedOnlyFilter;
    private CheckBox? _importantOnlyFilter;
    private ComboBox? _sortOrderFilter;
    private InboxCategory _selectedInboxCategory = InboxCategory.All;
    private string? _selectedInboxItemId;
    private readonly TextBox _firstNameInput = new() { Text = "Jordan" };
    private readonly TextBox _lastNameInput = new() { Text = "Hayes" };
    private readonly TextBox _preferredNameInput = new() { Text = "Jordan" };
    private readonly TextBox _ageInput = new() { Text = "39" };
    private readonly TextBox _nationalityInput = new() { Text = "Canada" };
    private readonly TextBox _birthplaceInput = new() { Text = "Swift Current, SK" };
    private readonly TextBox _strengthsInput = new() { Text = "development planning, communication" };
    private readonly TextBox _weaknessesInput = new() { Text = "limited draft history" };
    private readonly ComboBox _genderInput = new() { ItemsSource = Enum.GetValues<Gender>(), SelectedItem = Gender.NonBinary };
    private readonly ComboBox _backgroundInput = new() { ItemsSource = Enum.GetValues<GmBackground>(), SelectedItem = GmBackground.Operations };
    private readonly ComboBox _styleInput = new() { ItemsSource = Enum.GetValues<GmStyle>(), SelectedItem = GmStyle.Balanced };
    private Border? _draftModalOverlay;

    public MainWindow()
    {
        Title = "Hockey GM Legacy - Alpha Desktop";
        Width = 1180;
        Height = 780;
        MinWidth = 920;
        MinHeight = 620;
        Background = new SolidColorBrush(Color.FromRgb(245, 247, 250));

        Content = BuildCreationLayout();
    }

    private UIElement BuildCreationLayout()
    {
        var root = new Grid { Margin = new Thickness(28) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var title = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
        title.Children.Add(new TextBlock
        {
            Text = "Create Your GM",
            FontSize = 28,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(20, 40, 64))
        });
        title.Children.Add(new TextBlock
        {
            Text = "Alpha 2.8 starts with your created GM inside a cleaner GM Office workspace.",
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromRgb(65, 78, 92)),
            Margin = new Thickness(0, 6, 0, 0)
        });
        Grid.SetRow(title, 0);
        root.Children.Add(title);

        var form = new Grid();
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var index = 0; index < 7; index++)
        {
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        AddField(form, "First name", _firstNameInput, 0, 0);
        AddField(form, "Last name", _lastNameInput, 0, 1);
        AddField(form, "Preferred name", _preferredNameInput, 0, 2);
        AddField(form, "Gender", _genderInput, 1, 0);
        AddField(form, "Age", _ageInput, 1, 1);
        AddField(form, "Nationality", _nationalityInput, 1, 2);
        AddField(form, "Birthplace", _birthplaceInput, 2, 0);
        AddField(form, "Background", _backgroundInput, 2, 1);
        AddField(form, "GM style", _styleInput, 2, 2);
        AddField(form, "Strengths", _strengthsInput, 3, 0, 2);
        AddField(form, "Weaknesses", _weaknessesInput, 3, 2);

        var button = CreateButton("Start Career", StartCareer);
        button.HorizontalAlignment = HorizontalAlignment.Left;
        button.Margin = new Thickness(0, 24, 0, 0);
        Grid.SetRow(button, 5);
        Grid.SetColumn(button, 0);
        form.Children.Add(button);

        Grid.SetRow(form, 1);
        root.Children.Add(form);
        return root;
    }

    private static void AddField(Grid grid, string label, Control input, int row, int column, int columnSpan = 1)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 16, 14) };
        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 5)
        });

        input.MinHeight = 32;
        input.Margin = new Thickness(0);
        panel.Children.Add(input);

        Grid.SetRow(panel, row);
        Grid.SetColumn(panel, column);
        Grid.SetColumnSpan(panel, columnSpan);
        grid.Children.Add(panel);
    }

    private void StartCareer()
    {
        if (!int.TryParse(_ageInput.Text.Trim(), out var age))
        {
            MessageBox.Show("Please enter a valid age.", "GM Creation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var settings = new GmProfileCreationSettings(
            FirstName: _firstNameInput.Text.Trim(),
            LastName: _lastNameInput.Text.Trim(),
            PreferredName: _preferredNameInput.Text.Trim(),
            Gender: (Gender)(_genderInput.SelectedItem ?? Gender.Unknown),
            BirthDate: null,
            Age: age,
            Nationality: _nationalityInput.Text.Trim(),
            Birthplace: _birthplaceInput.Text.Trim(),
            Background: (GmBackground)(_backgroundInput.SelectedItem ?? GmBackground.Operations),
            Style: (GmStyle)(_styleInput.SelectedItem ?? GmStyle.Balanced),
            Strengths: SplitList(_strengthsInput.Text),
            Weaknesses: SplitList(_weaknessesInput.Text));

        _state = AlphaDesktopState.Create(settings);
        Content = BuildLayout();
        RefreshAll();
    }

    private static IReadOnlyList<string> SplitList(string text) =>
        text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => item.Length > 0)
            .ToArray();

    private UIElement BuildLayout()
    {
        var root = new Grid();
        var app = new DockPanel();

        var header = BuildHeader();
        DockPanel.SetDock(header, Dock.Top);
        app.Children.Add(header);

        _tabs.Clear();
        _tabItems.Clear();
        _selectableLists.Clear();
        _selectableDetails.Clear();

        var tabs = new TabControl
        {
            Margin = new Thickness(12),
            Background = Brushes.White
        };
        _mainTabs = tabs;

        AddWorkspaceTab(tabs, "Dashboard", new[]
        {
            new WorkspaceScreen("Dashboard", CreateDashboardContent()),
            new WorkspaceScreen("Action Center / Pending Decisions", CreateTextScreen("Pending Actions"))
        });

        AddWorkspaceTab(tabs, "Inbox", new[]
        {
            new WorkspaceScreen("GM Inbox", BuildInboxLayout()),
            new WorkspaceScreen("League News / Transaction Wire", CreateTextScreen("League News"))
        });

        AddWorkspaceTab(tabs, "Organization", new[]
        {
            new WorkspaceScreen("Owner", CreateTextScreen("Owner")),
            new WorkspaceScreen("Staff", CreateSelectablePeopleContent("Staff")),
            new WorkspaceScreen("Staff Hiring", CreateSelectablePeopleContent("Staff Hiring")),
            new WorkspaceScreen("Vacancies", CreateSelectablePeopleContent("Vacancies")),
            new WorkspaceScreen("Budget", CreateTextScreen("Budget")),
            new WorkspaceScreen("Organization Health", CreateTextScreen("Organization Health")),
            new WorkspaceScreen("Relationships", CreateTextScreen("Relationships"))
        });

        var hockeyOperations = new List<WorkspaceScreen>
        {
            new("Roster", CreateSelectablePeopleContent("Roster")),
            new("Prospects", CreateSelectablePeopleContent("Prospect List")),
            new("Recruits", CreateSelectablePeopleContent("Recruits")),
            new("Scouting", CreateSelectablePeopleContent("Scouting")),
            new("Scouting Operations", CreateSelectablePeopleContent("Scouting Operations")),
            new("Training Camp", CreateSelectablePeopleContent("Training Camp"))
        };
        if (State.IsDraftUiEnabled)
        {
            hockeyOperations.Insert(5, new WorkspaceScreen("Draft Board", CreateSelectablePeopleContent("Draft Board")));
        }
        AddWorkspaceTab(tabs, "Hockey Operations", hockeyOperations);

        AddWorkspaceTab(tabs, "Season", new[]
        {
            new WorkspaceScreen("Schedule", CreateTextScreen("Schedule")),
            new WorkspaceScreen("Standings", CreateTextScreen("Standings")),
            new WorkspaceScreen("Stats", CreateTextScreen("Stats")),
            new WorkspaceScreen("Monthly Summary", CreateTextScreen("Monthly Summary")),
            new WorkspaceScreen("Season Readiness", CreateTextScreen("Season Readiness"))
        });

        AddWorkspaceTab(tabs, "Reports / History", new[]
        {
            new WorkspaceScreen("Executive Reports", CreateTextScreen("Executive Reports")),
            new WorkspaceScreen("Draft Recaps", CreateTextScreen("Draft Recaps")),
            new WorkspaceScreen("Monthly Summaries", CreateTextScreen("Monthly Summaries")),
            new WorkspaceScreen("Career History", CreateTextScreen("Career History"))
        });

        AddWorkspaceTab(tabs, "Settings placeholder", new[]
        {
            new WorkspaceScreen("Settings", CreateTextScreen("Settings"))
        });

        app.Children.Add(tabs);
        root.Children.Add(app);
        root.Children.Add(BuildDraftModalOverlay());
        return root;
    }

    private UIElement BuildHeader()
    {
        var header = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(20, 40, 64)),
            Padding = new Thickness(16, 12, 16, 12)
        };

        var panel = new StackPanel();
        var textPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        textPanel.Children.Add(new TextBlock
        {
            Text = "Hockey GM Legacy - Alpha 2.8 - GM Office",
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

        panel.Children.Add(textPanel);

        var searchBox = new TextBox
        {
            Text = "Quick search placeholder - players, staff, prospects, messages",
            IsReadOnly = true,
            MinHeight = 30,
            MaxWidth = 520,
            Margin = new Thickness(0, 0, 0, 10),
            Background = new SolidColorBrush(Color.FromRgb(234, 241, 248)),
            Foreground = new SolidColorBrush(Color.FromRgb(78, 92, 108))
        };
        panel.Children.Add(searchBox);

        panel.Children.Add(new TextBlock
        {
            Text = "Grouped advance controls",
            Foreground = new SolidColorBrush(Color.FromRgb(210, 225, 240)),
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 6)
        });

        var buttonPanel = new WrapPanel
        {
            Orientation = Orientation.Horizontal
        };

        buttonPanel.Children.Add(CreateButton("Advance Day", () => Advance(1)));
        buttonPanel.Children.Add(CreateButton("Advance Week", () => Advance(7)));
        buttonPanel.Children.Add(CreateButton("To Next Game", AdvanceToNextGame));
        buttonPanel.Children.Add(CreateButton("To Month End", AdvanceToMonthEnd));
        buttonPanel.Children.Add(CreateButton("Approve Pending", ApprovePendingAction));
        buttonPanel.Children.Add(CreateButton("Decline Pending", DeclinePendingAction));
        buttonPanel.Children.Add(CreateButton("Reviews", GenerateSeasonReadinessReviews));
        buttonPanel.Children.Add(CreateButton("Begin Season", BeginSeason));
        buttonPanel.Children.Add(CreateButton("Front Report", GenerateFrontOfficeReadinessReport));
        buttonPanel.Children.Add(CreateButton("Season Review", GenerateEndOfSeasonExecutiveReview));

        panel.Children.Add(buttonPanel);

        header.Child = panel;
        return header;
    }

    private UIElement BuildDraftModalOverlay()
    {
        _draftModalOverlay = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(225, 10, 24, 38)),
            Padding = new Thickness(28),
            Visibility = Visibility.Collapsed,
            Child = new Grid()
        };
        Panel.SetZIndex(_draftModalOverlay, 20);
        return _draftModalOverlay;
    }

    private Button CreateButton(string text, Action action)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 92,
            Padding = new Thickness(10, 7, 10, 7),
            Margin = new Thickness(0, 0, 8, 8),
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Left
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
        var item = new TabItem
        {
            Header = title,
            Content = CreateTextScreen(title)
        };
        _tabItems[title] = item;
        tabs.Items.Add(item);
    }

    private TextBox CreateTextScreen(string title)
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
        return text;
    }

    private void AddDashboardTab(TabControl tabs)
    {
        var item = new TabItem
        {
            Header = "Dashboard",
            Content = CreateDashboardContent()
        };
        _tabItems["Dashboard"] = item;
        tabs.Items.Add(item);
    }

    private UIElement CreateDashboardContent()
    {
        _dashboardPanel = new StackPanel { Margin = new Thickness(16) };
        return new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = Brushes.White,
            Content = _dashboardPanel
        };
    }

    private void AddSelectablePeopleTab(TabControl tabs, string title)
    {
        var item = new TabItem
        {
            Header = title,
            Content = CreateSelectablePeopleContent(title)
        };
        _tabItems[title] = item;
        tabs.Items.Add(item);
    }

    private UIElement CreateSelectablePeopleContent(string title)
    {
        var root = new Grid { Background = Brushes.White };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var list = new ListBox
        {
            BorderThickness = new Thickness(0, 0, 1, 0),
            BorderBrush = new SolidColorBrush(Color.FromRgb(222, 229, 237)),
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 253)),
            Padding = new Thickness(8),
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        list.SelectionChanged += (_, _) =>
        {
            if (list.SelectedItem is SelectablePersonRow row)
            {
                _selectedPeopleByTab[title] = row.PersonId;
                RenderSelectableDetail(title);
            }
        };

        var detail = new StackPanel { Margin = new Thickness(18) };
        var detailScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = detail
        };

        if (title == "Roster")
        {
            var filters = BuildRosterFilters();
            Grid.SetRow(filters, 0);
            Grid.SetColumnSpan(filters, 2);
            root.Children.Add(filters);
        }

        Grid.SetRow(list, 1);
        Grid.SetColumn(list, 0);
        Grid.SetRow(detailScroll, 1);
        Grid.SetColumn(detailScroll, 1);
        root.Children.Add(list);
        root.Children.Add(detailScroll);

        _selectableLists[title] = list;
        _selectableDetails[title] = detail;
        return root;
    }

    private void AddWorkspaceTab(TabControl tabs, string title, IReadOnlyList<WorkspaceScreen> screens)
    {
        var root = new Grid { Background = Brushes.White };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var navigation = new ListBox
        {
            BorderThickness = new Thickness(0, 0, 1, 0),
            BorderBrush = new SolidColorBrush(Color.FromRgb(222, 229, 237)),
            Background = new SolidColorBrush(Color.FromRgb(239, 243, 248)),
            Padding = new Thickness(8),
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        var contentHost = new ContentControl
        {
            Content = screens.FirstOrDefault()?.Content
        };

        foreach (var screen in screens)
        {
            navigation.Items.Add(new ListBoxItem
            {
                Content = screen.Label,
                Tag = screen.Content,
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 4),
                FontWeight = FontWeights.SemiBold
            });
        }

        navigation.SelectionChanged += (_, _) =>
        {
            if (navigation.SelectedItem is ListBoxItem item && item.Tag is UIElement content)
            {
                contentHost.Content = content;
            }
        };

        if (navigation.Items.Count > 0)
        {
            navigation.SelectedIndex = 0;
        }

        Grid.SetColumn(navigation, 0);
        Grid.SetColumn(contentHost, 1);
        root.Children.Add(navigation);
        root.Children.Add(contentHost);

        var tab = new TabItem
        {
            Header = title,
            Content = root
        };
        _tabItems[title] = tab;
        tabs.Items.Add(tab);
    }

    private UIElement BuildRosterFilters()
    {
        var panel = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(12, 12, 12, 6)
        };

        _rosterSearchInput = new TextBox { Width = 170, MinHeight = 30, Margin = new Thickness(0, 0, 8, 8) };
        _rosterSearchInput.TextChanged += (_, _) => RefreshAll();
        panel.Children.Add(LabeledControl("Search", _rosterSearchInput));

        _rosterPositionFilter = CreateRosterFilter(Enum.GetNames<RosterPosition>().Prepend("All").ToArray());
        panel.Children.Add(LabeledControl("Position", _rosterPositionFilter));

        _rosterStatusFilter = CreateRosterFilter(Enum.GetNames<RosterStatus>().Prepend("All").ToArray());
        panel.Children.Add(LabeledControl("Status", _rosterStatusFilter));

        _rosterPlayerTypeFilter = CreateRosterFilter(new[] { "All", "Goalie", "Defense", "Forward", "Prospect", "Veteran", "Injured" });
        panel.Children.Add(LabeledControl("Player type", _rosterPlayerTypeFilter));

        _rosterRoleFilter = CreateRosterFilter(new[] { "All", "Top Line", "Middle Six", "Depth", "Starter", "Backup", "Development" });
        panel.Children.Add(LabeledControl("Role", _rosterRoleFilter));

        _rosterAgeFilter = CreateRosterFilter(new[] { "All", "Under 18", "18-19", "20+", "Unknown" });
        panel.Children.Add(LabeledControl("Age", _rosterAgeFilter));

        return panel;
    }

    private ComboBox CreateRosterFilter(string[] items)
    {
        var combo = new ComboBox
        {
            ItemsSource = items,
            SelectedIndex = 0,
            MinWidth = 120,
            MinHeight = 30,
            Margin = new Thickness(0, 0, 8, 8)
        };
        combo.SelectionChanged += (_, _) => RefreshAll();
        return combo;
    }

    private static UIElement LabeledControl(string label, Control control)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(70, 84, 102)),
            Margin = new Thickness(0, 0, 0, 3)
        });
        panel.Children.Add(control);
        return panel;
    }

    private void AddInboxTab(TabControl tabs)
    {
        var item = new TabItem
        {
            Header = "Inbox",
            Content = BuildInboxLayout()
        };
        _tabItems["Inbox"] = item;
        tabs.Items.Add(item);
    }

    private UIElement BuildInboxLayout()
    {
        var root = new Grid { Background = Brushes.White };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _inboxCategoryPanel = new StackPanel
        {
            Background = new SolidColorBrush(Color.FromRgb(239, 243, 248)),
            Margin = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Stretch
        };
        Grid.SetColumn(_inboxCategoryPanel, 0);
        root.Children.Add(_inboxCategoryPanel);

        var right = new Grid();
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var filters = BuildInboxFilters();
        Grid.SetRow(filters, 0);
        right.Children.Add(filters);

        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var listScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            BorderThickness = new Thickness(0)
        };
        _inboxListPanel = new StackPanel();
        listScroll.Content = _inboxListPanel;
        Grid.SetColumn(listScroll, 0);
        content.Children.Add(listScroll);

        _inboxReader = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(218, 226, 235)),
            BorderThickness = new Thickness(1, 0, 0, 0),
            Background = new SolidColorBrush(Color.FromRgb(250, 252, 254))
        };
        Grid.SetColumn(_inboxReader, 1);
        content.Children.Add(_inboxReader);

        Grid.SetRow(content, 1);
        right.Children.Add(content);

        Grid.SetColumn(right, 1);
        root.Children.Add(right);
        return root;
    }

    private UIElement BuildInboxFilters()
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(14, 10, 14, 10),
            VerticalAlignment = VerticalAlignment.Center
        };

        _unreadOnlyFilter = CreateFilterBox("Unread only");
        _pinnedOnlyFilter = CreateFilterBox("Pinned only");
        _importantOnlyFilter = CreateFilterBox("Important only");

        panel.Children.Add(_unreadOnlyFilter);
        panel.Children.Add(_pinnedOnlyFilter);
        panel.Children.Add(_importantOnlyFilter);

        panel.Children.Add(new TextBlock
        {
            Text = "Sort",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(16, 5, 6, 0)
        });

        _sortOrderFilter = new ComboBox
        {
            Width = 120,
            ItemsSource = new[] { "Newest first", "Oldest first" },
            SelectedIndex = 0
        };
        _sortOrderFilter.SelectionChanged += (_, _) => RefreshInboxPanels();
        panel.Children.Add(_sortOrderFilter);

        return panel;
    }

    private CheckBox CreateFilterBox(string text)
    {
        var box = new CheckBox
        {
            Content = text,
            Margin = new Thickness(0, 0, 14, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        box.Checked += (_, _) => RefreshInboxPanels();
        box.Unchecked += (_, _) => RefreshInboxPanels();
        return box;
    }

    private AlphaDesktopState State => _state ?? throw new InvalidOperationException("Career has not started.");

    private void Advance(int days) => State.Advance(days);

    private void AdvanceToNextGame() => State.AdvanceToNextGame();

    private void AdvanceToMonthEnd() => State.AdvanceToMonthEnd();

    private void MoveDraftBoardPlayerUp() => State.MoveDraftBoardPlayer(direction: -1);

    private void MoveDraftBoardPlayerDown() => State.MoveDraftBoardPlayer(direction: 1);

    private void AssignScoutFocus() => State.AssignScoutFocus();

    private void AssignScoutToRegion() => State.AssignScoutToRegion();

    private void AssignScoutToPlayer() => State.AssignScoutToPlayer();

    private void GenerateStaffConflictWarning() => State.GenerateStaffConflictWarning();

    private void ReassignStaffRole() => State.ReassignStaffRole();

    private void ReleaseStaff() => State.ReleaseStaff();

    private void HirePlaceholderStaff() => State.HirePlaceholderStaff();

    private void GenerateStaffCandidates() => State.GenerateStaffCandidates();

    private void SetDevelopmentCoachFocus() => State.SetDevelopmentCoachFocus();

    private void SetMedicalStaffFocus() => State.SetMedicalStaffFocus();

    private void SetScoutingDepartmentFocus() => State.SetScoutingDepartmentFocus();

    private void GenerateStaffEvaluation() => State.GenerateStaffEvaluation();

    private void ViewDossier() => State.ViewNextDossier();

    private void AddDossierNote() => State.AddDossierNote();

    private void MakeRecruitingOffer() => State.MakeRecruitingOffer();

    private void StarTopProspect() => State.StarTopProspect();

    private void AddDraftNote() => State.AddDraftNote();

    private void OfferProspectContract() => State.OfferProspectContract();

    private void InviteProspectToCamp() => State.InviteProspectToCamp();

    private void ReturnProspectToJuniorOrYouth() => State.ReturnProspectToJuniorOrYouth();

    private void AssignProspectToAffiliate() => State.AssignProspectToAffiliate();

    private void ReleaseProspectRights() => State.ReleaseProspectRights();

    private void StartDraft() => State.StartDraft();

    private void RunAiDrafting() => State.RunAiDrafting();

    private void DraftTopProspect() => State.DraftTopProspect();

    private void ApprovePendingAction() => State.ApprovePendingAction();

    private void DeclinePendingAction() => State.DeclinePendingAction();

    private void KeepTrainingCampPlayer() => State.KeepTrainingCampPlayer();

    private void CutTrainingCampPlayer() => State.CutTrainingCampPlayer();

    private void ReleaseTrainingCampPlayer() => State.ReleaseTrainingCampPlayer();

    private void ReturnTrainingCampPlayerToJunior() => State.ReturnTrainingCampPlayerToJunior();

    private void AssignOrReturnTrainingCampPlayer() => State.AssignOrReturnTrainingCampPlayer();

    private void PlaceTrainingCampPlayerOnWaivers() => State.PlaceTrainingCampPlayerOnWaivers();

    private void MarkTrainingCampPlayerInjured() => State.MarkTrainingCampPlayerInjured();

    private void CompleteTrainingCamp() => State.CompleteTrainingCamp();

    private void GenerateSeasonReadinessReviews() => State.GenerateSeasonReadinessReviews();

    private void BeginSeason() => State.BeginSeason();

    private void GenerateFrontOfficeReadinessReport() => State.GenerateFrontOfficeReadinessReport();

    private void GenerateEndOfSeasonExecutiveReview() => State.GenerateEndOfSeasonExecutiveReview();

    private void MarkLatestInboxRead() => State.ManageLatestInboxMessage(InboxMessageAction.MarkRead);

    private void PinLatestInboxMessage() => State.ManageLatestInboxMessage(InboxMessageAction.Pin);

    private void ArchiveLatestInboxMessage() => State.ManageLatestInboxMessage(InboxMessageAction.Archive);

    private void DeleteLatestInboxMessage() => State.ManageLatestInboxMessage(InboxMessageAction.Delete);

    private void OpenDossierFor(string personId)
    {
        State.OpenDossier(personId);
        var dossier = State.CurrentDossier;
        if (dossier is null)
        {
            MessageBox.Show(State.LatestSummary, "Player Dossier", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var window = new Window
        {
            Title = $"Dossier - {dossier.PlayerName}",
            Width = 760,
            Height = 680,
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = BuildDossierWindowContent(dossier)
        };
        window.ShowDialog();
        RefreshAll();
    }

    private UIElement BuildDossierWindowContent(PlayerDossierView dossier)
    {
        var root = new Grid { Margin = new Thickness(18) };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var panel = CreateDetailPanel(dossier.PlayerName, $"Age {dossier.Age} | {dossier.Position} | {dossier.Status}");
        AddLine(panel, "Team / rights", dossier.TeamOrRights);
        AddLine(panel, "Source", dossier.Source);

        foreach (var section in dossier.Sections.Where(section => section.Title != "GM Notes"))
        {
            AddSubHeader(panel, section.Title);
            foreach (var line in section.Lines)
            {
                AddParagraph(panel, line);
            }
        }

        AddSubHeader(panel, "GM Notes");
        var notes = new TextBox
        {
            Text = dossier.GmNotes,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 90,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        panel.Children.Add(notes);

        var scroll = new ScrollViewer
        {
            Content = panel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Grid.SetRow(scroll, 0);
        root.Children.Add(scroll);

        var footer = new WrapPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        footer.Children.Add(CreateButton("Save GM Note", () => State.SaveDossierNoteFor(dossier.PersonId, notes.Text)));
        footer.Children.Add(CreateButton("Close", () => Window.GetWindow(root)?.Close()));
        Grid.SetRow(footer, 1);
        root.Children.Add(footer);
        return root;
    }

    private void ShowStaffProfile(string personId)
    {
        State.FocusStaffProfile(personId);
        MessageBox.Show(State.StaffProfileText(personId), "Staff Profile", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SetStaffFocusFor(string personId)
    {
        State.SetStaffFocusFor(personId);
        MessageBox.Show(State.LatestSummary, "Staff Focus", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ShowScoutAssignmentDialog(string? playerPersonId, string? scoutPersonId = null, ScoutingRegionFocus? region = null)
    {
        var availableScouts = State.AvailableScoutProfiles.ToArray();
        if (scoutPersonId is not null)
        {
            var selected = State.ScoutProfiles.FirstOrDefault(profile => profile.ScoutPersonId == scoutPersonId);
            if (selected is not null && availableScouts.All(profile => profile.ScoutPersonId != scoutPersonId))
            {
                availableScouts = availableScouts.Append(selected).ToArray();
            }
        }

        if (availableScouts.Length == 0)
        {
            MessageBox.Show("No scouts are currently available. Deployed scouts return when their assignments complete.", "Assign Scout", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var scoutBox = new ComboBox
        {
            ItemsSource = availableScouts,
            SelectedItem = scoutPersonId is null ? availableScouts.First() : availableScouts.FirstOrDefault(profile => profile.ScoutPersonId == scoutPersonId) ?? availableScouts.First(),
            DisplayMemberPath = nameof(ScoutingOperationScoutProfile.Name),
            MinWidth = 240,
            Margin = new Thickness(0, 0, 0, 8)
        };
        var durationBox = new ComboBox
        {
            ItemsSource = new[] { "1 week", "2 weeks", "3 weeks", "1 month" },
            SelectedIndex = 0,
            MinWidth = 160,
            Margin = new Thickness(0, 0, 0, 8)
        };
        var priorityBox = new ComboBox
        {
            ItemsSource = Enum.GetValues<ScoutingOperationPriority>(),
            SelectedItem = ScoutingOperationPriority.High,
            MinWidth = 160,
            Margin = new Thickness(0, 0, 0, 8)
        };
        var notes = new TextBox
        {
            Text = playerPersonId is not null
                ? $"Scout {FindPersonName(playerPersonId)} with a focused update."
                : $"Area scouting trip for {region?.ToString() ?? "selected region"}.",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 70
        };

        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(new TextBlock
        {
            Text = playerPersonId is not null ? $"Assign Scout: {FindPersonName(playerPersonId)}" : $"Assign Area Scout: {region}",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 12)
        });
        panel.Children.Add(LabeledControl("Scout", scoutBox));
        panel.Children.Add(LabeledControl("Duration", durationBox));
        panel.Children.Add(LabeledControl("Priority", priorityBox));
        panel.Children.Add(LabeledControl("Notes", notes));

        var window = new Window
        {
            Title = "Assign Scout",
            Width = 420,
            Height = 430,
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = panel
        };
        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        actions.Children.Add(CreateButton("Assign", () =>
        {
            if (scoutBox.SelectedItem is not ScoutingOperationScoutProfile scout)
            {
                return;
            }

            var days = durationBox.SelectedItem?.ToString() switch
            {
                "2 weeks" => 14,
                "3 weeks" => 21,
                "1 month" => 30,
                _ => 7
            };
            var priority = priorityBox.SelectedItem is ScoutingOperationPriority selectedPriority ? selectedPriority : ScoutingOperationPriority.High;
            if (playerPersonId is not null)
            {
                State.AssignScoutToSelectedPlayerForDuration(playerPersonId, scout.ScoutPersonId, days, priority, notes.Text);
            }
            else if (region is not null)
            {
                State.AssignScoutToRegionForDuration(scout.ScoutPersonId, region.Value, days, priority, notes.Text);
            }

            window.Close();
        }));
        actions.Children.Add(CreateButton("Cancel", window.Close));
        panel.Children.Add(actions);
        window.ShowDialog();
        RefreshAll();
    }

    private void RefreshAll()
    {
        var snapshot = State.Snapshot;
        _dateText.Text = $"Current date: {snapshot.CurrentDate:yyyy-MM-dd}";
        _summaryText.Text = State.LatestSummary;
        _processedText.Text = $"Last processed events: {State.LastProcessedEventCount} | Inbox items: {State.Inbox.Count}";

        RefreshDashboard();
        RefreshInboxPanels();
        _tabs["Owner"].Text = BuildOwner();
        RefreshSelectableTab("Staff", BuildStaffRows());
        RefreshSelectableTab("Staff Hiring", BuildStaffCandidateRows());
        RefreshSelectableTab("Vacancies", BuildStaffVacancyRows());
        RefreshSelectableTab("Roster", BuildRosterRows());
        RefreshSelectableTab("Recruits", BuildRecruitRows());
        RefreshSelectableTab("Scouting", BuildScoutingRows());
        RefreshSelectableTab("Scouting Operations", BuildScoutingOperationRows());
        _tabs["Pending Actions"].Text = BuildPendingActions();
        _tabs["League News"].Text = BuildLeagueNews();
        _tabs["Budget"].Text = BuildBudgetWorkspace();
        _tabs["Organization Health"].Text = BuildOrganizationHealth();
        RefreshSelectableTab("Player Dossier", BuildDossierRows());
        if (_selectableLists.ContainsKey("Draft Board"))
        {
            RefreshSelectableTab("Draft Board", BuildDraftBoardRows());
        }
        RefreshSelectableTab("Prospect List", BuildProspectRows());
        RefreshSelectableTab("Training Camp", BuildTrainingCampRows());
        _tabs["Season Readiness"].Text = BuildSeasonReadiness();
        _tabs["Schedule"].Text = BuildSchedule();
        _tabs["Standings"].Text = BuildStandings();
        _tabs["Stats"].Text = BuildStats();
        _tabs["Monthly Summary"].Text = BuildMonthlySummary();
        _tabs["Executive Reports"].Text = BuildExecutiveReports();
        _tabs["Draft Recaps"].Text = BuildDraftRecaps();
        _tabs["Monthly Summaries"].Text = BuildMonthlySummaries();
        _tabs["Career History"].Text = BuildCareerHistory();
        _tabs["Settings"].Text = BuildSettings();
        _tabs["Relationships"].Text = BuildRelationships();
        UpdateTabBadges();
        RefreshDraftModal();
    }

    private void RefreshDashboard()
    {
        if (_dashboardPanel is null || _state is null)
        {
            return;
        }

        var snapshot = State.Snapshot;
        var readiness = State.SeasonReadinessReport;
        var roster = readiness.RosterReport;
        var budget = State.BudgetOverview;
        _dashboardPanel.Children.Clear();

        _dashboardPanel.Children.Add(new TextBlock
        {
            Text = "Dashboard",
            FontSize = 26,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(20, 40, 64)),
            Margin = new Thickness(0, 0, 0, 4)
        });
        _dashboardPanel.Children.Add(new TextBlock
        {
            Text = $"{snapshot.Organization?.Name ?? snapshot.OrganizationId} | {snapshot.WorldState.WorldName}",
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromRgb(74, 88, 105)),
            Margin = new Thickness(0, 0, 0, 16)
        });

        var metrics = new WrapPanel { Orientation = Orientation.Horizontal };
        metrics.Children.Add(CreateDashboardMetric("Current Date", snapshot.CurrentDate.ToString("yyyy-MM-dd"), snapshot.Season?.CurrentPhase.ToString() ?? snapshot.WorldState.CurrentPhase.ToString(), false));
        metrics.Children.Add(CreateDashboardMetric("Draft Countdown", State.DraftCountdownText, State.ScenarioSnapshot.DraftExperience?.Status.ToString() ?? "PreDraft", false));
        metrics.Children.Add(CreateDashboardMetric("Training Camp", State.TrainingCampCountdownText, State.TrainingCampStatusText, State.RosterWarningCount > 0));
        metrics.Children.Add(CreateDashboardMetric("Inbox Unread", State.UnreadInboxCount.ToString(), "messages needing review", State.UnreadInboxCount > 0));
        metrics.Children.Add(CreateDashboardMetric("Pending Decisions", State.PendingDecisionCount.ToString(), "GM approval required", State.PendingDecisionCount > 0));
        metrics.Children.Add(CreateDashboardMetric("Urgent Decisions", State.UrgentPendingDecisionCount.ToString(), State.NextDecisionDeadlineText, State.UrgentPendingDecisionCount > 0));
        metrics.Children.Add(CreateDashboardMetric("Roster Issues", State.RosterWarningCount.ToString(), roster.ValidationResult.Message, State.RosterWarningCount > 0));
        metrics.Children.Add(CreateDashboardMetric("Staff Vacancies", State.StaffVacancies.Count.ToString(), State.StaffVacancySummary, State.StaffVacancies.Count > 0));
        metrics.Children.Add(CreateDashboardMetric("Scouting Reports", State.ScoutingReportCount.ToString(), $"{State.ScenarioSnapshot.ScoutingOperations.Count(item => item.IsOpen)} active assignment(s)", false));
        metrics.Children.Add(CreateDashboardMetric("Budget", budget.Status.ToString(), $"{budget.RemainingBudget:C0} remaining", budget.Status == BudgetStatus.OverBudget));
        metrics.Children.Add(CreateDashboardMetric("Owner Mood", OwnerMoodText(), $"Trust {snapshot.Owner.Trust} | Confidence {snapshot.Owner.Confidence}", snapshot.Owner.Trust < 45 || snapshot.Owner.Confidence < 45));
        var nextGame = State.NextGame;
        var lastGame = State.LastGameRecap;
        var record = State.TeamRecordText;
        metrics.Children.Add(CreateDashboardMetric(
            "Next Game",
            nextGame is null ? "None" : nextGame.Date.ToString("yyyy-MM-dd"),
            nextGame is null ? "Season schedule pending" : DescribeGame(nextGame),
            false));
        metrics.Children.Add(CreateDashboardMetric(
            "Last Game",
            lastGame is null ? "None" : lastGame.BoxScore.FinalScore,
            lastGame is null ? "No completed game yet" : lastGame.NarrativeSummary,
            lastGame is not null && lastGame.WinnerOrganizationId != State.ScenarioSnapshot.Organization.OrganizationId));
        metrics.Children.Add(CreateDashboardMetric("Team Record", record, "regular season", false));
        metrics.Children.Add(CreateDashboardMetric("Standings Rank", State.StandingsRankText, "league table", false));
        _dashboardPanel.Children.Add(metrics);

        var lower = new Grid { Margin = new Thickness(0, 14, 0, 0) };
        lower.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.25, GridUnitType.Star) });
        lower.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var actionsCard = CreateDashboardCard("Quick Advance Controls", out var actions);
        AddActions(actions,
            CreateDetailButton("Advance Day", () => Advance(1)),
            CreateDetailButton("Advance Week", () => Advance(7)),
            CreateDetailButton("Advance to Next Game", AdvanceToNextGame),
            CreateDetailButton("Advance to Month End", AdvanceToMonthEnd),
            CreateDetailButton("Review Inbox", () => SelectTab("Inbox")),
            CreateDetailButton("Review Draft Board", () => SelectTab("Hockey Operations")),
            CreateDetailButton("Review Pending Actions", () => SelectTab("Dashboard")));
        Grid.SetColumn(actionsCard, 0);
        lower.Children.Add(actionsCard);

        var summaryCard = CreateDashboardCard("Action Center / Pending Decisions", out var summary);
        AddLine(summary, "Owner", snapshot.Owner.Name);
        AddLine(summary, "GM", snapshot.GeneralManager.Identity.DisplayName);
        AddLine(summary, "Head scout", snapshot.Scout.Name);
        AddLine(summary, "Roster", $"{roster.CurrentRosterSize}/{roster.RequiredRosterSize} opening target");
        AddLine(summary, "Staff vacancies", State.StaffVacancySummary);
        AddLine(summary, "Season readiness", readiness.RosterStatus);
        AddLine(summary, "Last game", lastGame is null ? "No completed game" : lastGame.BoxScore.FinalScore);
        AddLine(summary, "Next game", nextGame is null ? "No scheduled game" : $"{nextGame.Date:yyyy-MM-dd}: {DescribeGame(nextGame)}");
        AddLine(summary, "Team record", record);
        AddLine(summary, "Standings rank", State.StandingsRankText);
        AddLine(summary, "Urgent decisions", $"{State.UrgentPendingDecisionCount} urgent of {State.PendingDecisionCount} open");
        AddLine(summary, "Next stop reason", State.LastStopReason);
        if (State.LatestMonthlySummary is not null)
        {
            AddLine(summary, "Monthly summary", $"{State.LatestMonthlySummary.MonthName}: {State.LatestMonthlySummary.TeamRecordForMonth}");
            AddParagraph(summary, State.LatestMonthlySummary.ExecutiveNarrative);
        }
        AddLine(summary, "Budget", $"{budget.UsedBudget:C0} used of {budget.TotalBudget:C0}");
        AddParagraph(summary, State.LatestSummary);
        Grid.SetColumn(summaryCard, 1);
        lower.Children.Add(summaryCard);
        _dashboardPanel.Children.Add(lower);
    }

    private Border CreateDashboardMetric(string label, string value, string note, bool warning)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(87, 100, 118))
        });
        panel.Children.Add(new TextBlock
        {
            Text = value,
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(warning ? Color.FromRgb(156, 64, 44) : Color.FromRgb(20, 40, 64)),
            Margin = new Thickness(0, 4, 0, 2)
        });
        panel.Children.Add(new TextBlock
        {
            Text = note,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(91, 106, 124))
        });

        return new Border
        {
            Child = panel,
            Width = 190,
            MinHeight = 116,
            Margin = new Thickness(0, 0, 12, 12),
            Padding = new Thickness(14),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(warning ? Color.FromRgb(224, 174, 160) : Color.FromRgb(221, 229, 238)),
            Background = new SolidColorBrush(warning ? Color.FromRgb(255, 247, 244) : Color.FromRgb(248, 250, 253)),
            CornerRadius = new CornerRadius(6)
        };
    }

    private string OwnerMoodText()
    {
        var owner = State.Snapshot.Owner;
        var average = (owner.Trust + owner.Confidence + owner.Patience) / 3;
        return average switch
        {
            >= 75 => "Supportive",
            >= 60 => "Steady",
            >= 45 => "Watchful",
            _ => "Concerned"
        };
    }

    private Border CreateDashboardCard(string title, out StackPanel panel)
    {
        panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 17,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(20, 40, 64)),
            Margin = new Thickness(0, 0, 0, 10)
        });

        return new Border
        {
            Child = panel,
            Margin = new Thickness(0, 0, 12, 0),
            Padding = new Thickness(16),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(221, 229, 238)),
            Background = new SolidColorBrush(Color.FromRgb(252, 253, 255)),
            CornerRadius = new CornerRadius(6)
        };
    }

    private void UpdateTabBadges()
    {
        SetTabHeader("Dashboard", State.PendingDecisionCount > 0 ? $"Dashboard ({State.PendingDecisionCount})" : "Dashboard");
        SetTabHeader("Inbox", $"Inbox ({State.UnreadInboxCount})");
        SetTabHeader("Organization", State.StaffVacancies.Count > 0 ? $"Organization ({State.StaffVacancies.Count})" : "Organization");
        var operationsCount = State.RosterWarningCount + State.ScoutingReportCount;
        SetTabHeader("Hockey Operations", operationsCount > 0 ? $"Hockey Operations ({operationsCount})" : "Hockey Operations");
        SetTabHeader("Season", "Season");
        SetTabHeader("Reports / History", "Reports / History");
        SetTabHeader("Settings placeholder", "Settings");
    }

    private void SetTabHeader(string title, string header)
    {
        if (_tabItems.TryGetValue(title, out var item))
        {
            item.Header = header;
        }
    }

    private void SelectTab(string title)
    {
        if (_mainTabs is not null && _tabItems.TryGetValue(title, out var item))
        {
            _mainTabs.SelectedItem = item;
        }
    }

    private void RefreshSelectableTab(string title, IReadOnlyList<SelectablePersonRow> rows)
    {
        if (!_selectableLists.TryGetValue(title, out var list))
        {
            return;
        }

        var previous = _selectedPeopleByTab.GetValueOrDefault(title);
        list.ItemsSource = null;
        list.ItemsSource = rows;

        var selected = rows.FirstOrDefault(row => string.Equals(row.PersonId, previous, StringComparison.Ordinal))
            ?? rows.FirstOrDefault();
        if (selected is not null)
        {
            list.SelectedItem = selected;
            _selectedPeopleByTab[title] = selected.PersonId;
        }
        else
        {
            _selectedPeopleByTab.Remove(title);
        }

        RenderSelectableDetail(title);
    }

    private void RenderSelectableDetail(string title)
    {
        if (!_selectableDetails.TryGetValue(title, out var detail))
        {
            return;
        }

        var row = _selectableLists.TryGetValue(title, out var list)
            ? list.SelectedItem as SelectablePersonRow
            : null;

        detail.Children.Clear();
        detail.Children.Add(title switch
        {
            "Staff" => BuildStaffDetail(row),
            "Staff Hiring" => BuildStaffDetail(row),
            "Vacancies" => BuildStaffDetail(row),
            "Roster" => BuildPlayerDetail(title, row),
            "Recruits" => BuildPlayerDetail(title, row),
            "Scouting" => BuildPlayerDetail(title, row),
            "Scouting Operations" => BuildScoutingOperationDetail(row),
            "Draft Board" => BuildPlayerDetail(title, row),
            "Prospect List" => BuildPlayerDetail(title, row),
            "Training Camp" => BuildTrainingCampDetail(row),
            "Player Dossier" => BuildDossierDetail(row),
            _ => EmptyDetail(title, "No detail panel is configured for this view.")
        });
    }

    private IReadOnlyList<SelectablePersonRow> BuildStaffRows() =>
        State.StaffProfiles
            .Select(profile => new SelectablePersonRow(
                profile.PersonId,
                profile.Name,
                "Staff",
                $"Current Staff - {profile.CurrentRole} - {profile.Salary.AnnualAmount:C0}",
                $"{profile.Department} | GM relationship {profile.RelationshipWithGm} | salary {profile.Salary.AnnualAmount:C0}",
                profile.Chemistry.Summary))
            .ToArray();

    private IReadOnlyList<SelectablePersonRow> BuildStaffCandidateRows()
    {
        var rows = new List<SelectablePersonRow>();
        rows.Add(new SelectablePersonRow("staff-section:candidates", "Hire Staff / Staff Candidates", "StaffSection", "Available candidates only", "Hire button appears only for candidate rows.", "Generate candidates, select one, then hire from the candidate detail panel."));
        rows.AddRange(State.ScenarioSnapshot.StaffCandidates.Select(candidate => new SelectablePersonRow(
            candidate.Person.PersonId,
            candidate.Person.Identity.DisplayName,
            "Candidate",
            $"Staff Candidate - {candidate.StaffMember.CurrentRole} - ask {candidate.ExpectedSalary.AnnualAmount:C0}",
            $"{candidate.StaffMember.Department} | reputation {candidate.Reputation} | role fit {candidate.RoleFit} | salary ask {candidate.ExpectedSalary.AnnualAmount:C0}",
            $"{candidate.HiringRecommendation} Strengths: {string.Join(", ", candidate.Strengths)}. Risk: {candidate.ChemistryRisk}")));
        return rows;
    }

    private IReadOnlyList<SelectablePersonRow> BuildStaffVacancyRows()
    {
        var rows = new List<SelectablePersonRow>();
        rows.Add(new SelectablePersonRow("staff-section:vacancies", "Vacancies", "StaffSection", "Rulebook staff openings", "Vacant positions and limits from the active rulebook.", State.StaffVacancySummary));
        rows.AddRange(State.StaffVacancies.Select(vacancy => new SelectablePersonRow(
            $"vacancy:{vacancy.Role}",
            StaffRoles.Title(vacancy.Role),
            "Vacancy",
            $"Vacant Position - {vacancy.Department}",
            $"{vacancy.Current}/{vacancy.Required} filled | max {vacancy.Maximum}",
            vacancy.Warning)));

        return rows;
    }

    private IReadOnlyList<SelectablePersonRow> BuildRosterRows() =>
        State.Snapshot.Roster.Players
            .Where(PassesRosterFilters)
            .OrderBy(player => player.Status)
            .ThenBy(player => FindPersonName(player.PersonId), StringComparer.Ordinal)
            .Select(player =>
            {
                return new SelectablePersonRow(
                    player.PersonId,
                    FindPersonName(player.PersonId),
                    "RosterPlayer",
                    $"{player.Position} - age {State.PersonAge(player.PersonId)?.ToString() ?? player.Age?.ToString() ?? "unknown"} - {player.Status}",
                    $"{State.PlayerType(player.PersonId)} | {State.CurrentLineupRole(player.PersonId)} now | {State.PotentialLineupRole(player.PersonId)} potential",
                    $"Contract/rights: {State.ContractRightsStatus(player.PersonId)} | Development: {State.DevelopmentTrend(player.PersonId)} | Injury: {State.InjuryStatus(player.PersonId)}");
            })
            .ToArray();

    private bool PassesRosterFilters(RosterPlayer player)
    {
        var name = FindPersonName(player.PersonId);
        var search = _rosterSearchInput?.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(search) && !name.Contains(search, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!FilterMatches(_rosterPositionFilter, player.Position.ToString()))
        {
            return false;
        }

        if (!FilterMatches(_rosterStatusFilter, player.Status.ToString()))
        {
            return false;
        }

        if (!FilterMatches(_rosterPlayerTypeFilter, State.PlayerType(player.PersonId)))
        {
            return false;
        }

        var roleFilter = SelectedFilter(_rosterRoleFilter);
        if (roleFilter != "All"
            && !State.CurrentLineupRole(player.PersonId).Contains(roleFilter, StringComparison.OrdinalIgnoreCase)
            && !State.PotentialLineupRole(player.PersonId).Contains(roleFilter, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var age = State.PersonAge(player.PersonId) ?? player.Age;
        return SelectedFilter(_rosterAgeFilter) switch
        {
            "Under 18" => age is < 18,
            "18-19" => age is >= 18 and <= 19,
            "20+" => age is >= 20,
            "Unknown" => age is null,
            _ => true
        };
    }

    private static bool FilterMatches(ComboBox? combo, string value)
    {
        var selected = SelectedFilter(combo);
        return selected == "All" || value.Contains(selected, StringComparison.OrdinalIgnoreCase);
    }

    private static string SelectedFilter(ComboBox? combo) => combo?.SelectedItem?.ToString() ?? "All";

    private IReadOnlyList<SelectablePersonRow> BuildRecruitRows() =>
        State.Snapshot.Recruits
            .GroupBy(recruit => recruit.RecruitPersonId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderByDescending(recruit => recruit.GetInterest(State.Snapshot.OrganizationId))
            .ThenBy(recruit => FindPersonName(recruit.RecruitPersonId), StringComparer.Ordinal)
            .Select(recruit =>
            {
                var profile = State.RecruitingProfileFor(recruit.RecruitPersonId);
                return new SelectablePersonRow(
                    recruit.RecruitPersonId,
                    RecruitDisplayName(recruit.RecruitPersonId),
                    "Recruit",
                    $"{profile.Position} - age {profile.Age?.ToString() ?? "unknown"} - {profile.Status}",
                    $"Interest {profile.InterestLevel} | top: {State.RecruitPrioritySummary(recruit.RecruitPersonId, 1)} | offers: {State.RecruitOfferState(recruit.RecruitPersonId)}",
                    $"{profile.RegionOrHometown} | {profile.CurrentTeam} | {profile.ProjectionSummary}");
            })
            .ToArray();

    private IReadOnlyList<SelectablePersonRow> BuildScoutingRows() =>
        State.Snapshot.DraftBoard.Entries
            .GroupBy(entry => entry.ProspectPersonId, StringComparer.Ordinal)
            .Select(group => group.OrderBy(entry => entry.Rank).First())
            .OrderBy(entry => entry.Rank)
            .Select(entry => new SelectablePersonRow(
                entry.ProspectPersonId,
                ScoutingDisplayName(entry.ProspectPersonId),
                "ScoutingProspect",
                $"{State.PersonPosition(entry.ProspectPersonId)} - age {State.PersonAge(entry.ProspectPersonId)?.ToString() ?? "unknown"} - rank #{entry.Rank}",
                $"Confidence {entry.ScoutingConfidence?.ToString() ?? "Unknown"} | Scout: {State.AssignedScoutText(entry.ProspectPersonId)}",
                $"{State.DraftBioSummary(entry)} | {State.ScoutingReportStatus(entry.ProspectPersonId)} | {entry.ProjectionText}"))
            .ToArray();

    private IReadOnlyList<SelectablePersonRow> BuildScoutingOperationRows() =>
        State.ScoutProfiles
            .OrderBy(profile => profile.Workload)
            .ThenBy(profile => profile.Name, StringComparer.Ordinal)
            .Select(profile => new SelectablePersonRow(
                profile.ScoutPersonId,
                profile.Name,
                "Scout",
                profile.Role,
                $"{profile.RegionSpecialty} | workload {profile.Workload}",
                profile.ConflictWarning))
            .ToArray();

    private IReadOnlyList<SelectablePersonRow> BuildDraftBoardRows() =>
        State.Snapshot.DraftBoard.Entries
            .GroupBy(entry => entry.ProspectPersonId, StringComparer.Ordinal)
            .Select(group => group.OrderBy(entry => entry.Rank).First())
            .OrderBy(entry => entry.Rank)
            .Select(entry => new SelectablePersonRow(
                entry.ProspectPersonId,
                ScoutingDisplayName(entry.ProspectPersonId),
                "DraftBoard",
                $"{(entry.IsStarred ? "Starred " : string.Empty)}Rank #{entry.Rank} | {State.PersonPosition(entry.ProspectPersonId)} | {entry.Bio?.CurrentTeam ?? "team n/a"} / {entry.Bio?.League ?? "league n/a"}",
                $"Confidence {entry.ScoutingConfidence?.ToString() ?? "Unknown"} | Projection: {entry.ProjectionText}",
                $"{entry.Bio?.ShootsCatches ?? "Hand unknown"} | {entry.Bio?.HeightDisplay ?? "height n/a"} | {entry.Bio?.WeightDisplay ?? "weight n/a"} | {State.DraftBioSummary(entry)}"))
            .ToArray();

    private IReadOnlyList<SelectablePersonRow> BuildProspectRows() =>
        State.ScenarioSnapshot.ProspectRights
            .OrderBy(prospect => prospect.PickNumber)
            .Select(prospect => new SelectablePersonRow(
                prospect.ProspectPersonId,
                prospect.ProspectName,
                "Prospect",
                $"{prospect.Position} - {prospect.Status}",
                $"Age {prospect.Age} | R{prospect.RoundNumber} P{prospect.PickNumber} | Confidence {prospect.ScoutingConfidence?.ToString() ?? "Unknown"}",
                $"Projection: {prospect.ProjectionText}"))
            .ToArray();

    private IReadOnlyList<SelectablePersonRow> BuildTrainingCampRows() =>
        State.ScenarioSnapshot.TrainingCamp?.Players
            .OrderBy(player => player.Status)
            .ThenBy(player => player.PlayerName, StringComparer.Ordinal)
            .Select(player => new SelectablePersonRow(
                player.PersonId,
                player.PlayerName,
                "CampPlayer",
                $"{player.Position} - {player.Status}",
                $"{player.InviteType} | {player.AcquisitionSource}",
                State.ScenarioSnapshot.TrainingCamp.FindEvaluation(player.PersonId)?.Recommendation ?? "Evaluation pending."))
            .ToArray()
        ?? Array.Empty<SelectablePersonRow>();

    private IReadOnlyList<SelectablePersonRow> BuildDossierRows() =>
        BuildRosterRows()
            .Concat(BuildStaffRows().Where(row => row.Kind == "Staff"))
            .Concat(BuildRecruitRows())
            .Concat(BuildScoutingRows())
            .Concat(BuildProspectRows())
            .Concat(BuildTrainingCampRows())
            .GroupBy(row => row.PersonId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(row => row.Name, StringComparer.Ordinal)
            .ToArray();

    private UIElement BuildStaffDetail(SelectablePersonRow? row)
    {
        if (row is null)
        {
            var empty = EmptyDetail("Staff", "Current Staff are listed with active roles. Staff Candidates appear below once generated. Use Hire Staff to open the candidate pool.");
            AddSubHeader(empty, "Current Staff");
            AddParagraph(empty, "Select an employed staff member to view profile, focus, chemistry, and staff actions.");
            AddSubHeader(empty, "Vacant Positions");
            AddParagraph(empty, State.StaffVacancySummary);
            AddSubHeader(empty, "Available Candidates");
            AddParagraph(empty, "Candidate rows show role fit, department, reputation, strengths, weaknesses, chemistry risk, and recommendation.");
            AddSubHeader(empty, "Hire Staff");
            AddParagraph(empty, "Generate candidates, select a candidate row, then use Hire Candidate in the detail panel.");
            AddActions(empty, CreateDetailButton("Hire Staff", GenerateStaffCandidates), CreateDetailButton("Generate Candidates", GenerateStaffCandidates), CreateDetailButton("Staff Warning", GenerateStaffConflictWarning));
            return empty;
        }

        if (row.Kind == "StaffSection")
        {
            var panel = CreateDetailPanel(row.Name, row.Primary);
            AddLine(panel, "Section", row.Name);
            AddLine(panel, "Scope", row.Secondary);
            AddParagraph(panel, row.Summary);
            if (row.PersonId == "staff-section:candidates")
            {
                AddActions(panel, CreateDetailButton("Generate Candidates", GenerateStaffCandidates));
            }
            else if (row.PersonId == "staff-section:vacancies")
            {
                AddActions(panel, CreateDetailButton("Generate Candidates", GenerateStaffCandidates));
            }

            return panel;
        }

        if (row.Kind == "Vacancy")
        {
            var roleText = row.PersonId.Replace("vacancy:", string.Empty, StringComparison.Ordinal);
            var vacancy = State.StaffVacancies.FirstOrDefault(vacancy => vacancy.Role.ToString() == roleText);
            if (vacancy is null)
            {
                return EmptyDetail("Vacancy", "This vacancy has already been filled.");
            }

            var panel = CreateDetailPanel(StaffRoles.Title(vacancy.Role), "Vacant position");
            AddLine(panel, "Department", vacancy.Department);
            AddLine(panel, "Filled", $"{vacancy.Current}/{vacancy.Required}");
            AddLine(panel, "Maximum", vacancy.Maximum);
            AddLine(panel, "Warning", vacancy.Warning);
            AddParagraph(panel, "Generate candidates, compare fit and salary, then hire a candidate for this role.");
            AddActions(panel,
                CreateDetailButton("Generate Candidates", GenerateStaffCandidates),
                CreateDetailButton("Hire Staff", GenerateStaffCandidates));
            return panel;
        }

        if (row.Kind == "Candidate")
        {
            var candidate = State.ScenarioSnapshot.StaffCandidates.FirstOrDefault(candidate => candidate.Person.PersonId == row.PersonId);
            if (candidate is null)
            {
                return EmptyDetail("Staff Candidate", "This candidate is no longer available.");
            }

            var panel = CreateDetailPanel(candidate.Person.Identity.DisplayName, "Staff candidate");
            AddLine(panel, "Role", candidate.StaffMember.CurrentRole);
            AddLine(panel, "Department", candidate.StaffMember.Department);
            AddLine(panel, "Role fit", candidate.RoleFit);
            AddLine(panel, "Department fit", candidate.DepartmentFit);
            AddLine(panel, "Reputation", candidate.Reputation);
            AddLine(panel, "Salary ask", $"{candidate.ExpectedSalary.AnnualAmount:C0}");
            AddLine(panel, "Current employer", candidate.CurrentEmployer);
            AddLine(panel, "Years experience", candidate.YearsExperience);
            AddLine(panel, "Strengths", string.Join(", ", candidate.Strengths));
            AddLine(panel, "Weaknesses", string.Join(", ", candidate.Weaknesses));
            AddLine(panel, "Chemistry risk", candidate.ChemistryRisk);
            AddLine(panel, "Recommendation", candidate.HiringRecommendation);
            AddParagraph(panel, candidate.HiringRecommendation);
            AddActions(panel,
                CreateDetailButton("Hire Candidate", () => State.HireCandidateFor(row.PersonId)),
                CreateDetailButton("Replace", () => State.HireCandidateFor(row.PersonId)),
                CreateDetailButton("Compare", () => MessageBox.Show(State.CompareCandidateText(row.PersonId), "Compare Candidate", MessageBoxButton.OK, MessageBoxImage.Information)),
                CreateDetailButton("Salary Offer", () => MessageBox.Show("Salary offer is a placeholder for now. Hiring uses the listed salary ask.", "Salary Offer", MessageBoxButton.OK, MessageBoxImage.Information)),
                CreateDetailButton("Generate Candidates", GenerateStaffCandidates));
            return panel;
        }

        var profile = State.StaffProfiles.FirstOrDefault(profile => profile.PersonId == row.PersonId);
        if (profile is null)
        {
            return EmptyDetail("Staff", "This staff member is no longer active.");
        }

        var detail = CreateDetailPanel(profile.Name, "Selected staff profile");
        AddLine(detail, "Role", profile.CurrentRole);
        AddLine(detail, "Department", profile.Department);
        AddLine(detail, "Salary", $"{profile.Salary.AnnualAmount:C0}");
        AddLine(detail, "Contract", profile.ContractStatus);
        AddLine(detail, "GM relationship", $"{profile.RelationshipWithGm}/100");
        AddLine(detail, "Fit / chemistry", profile.Chemistry.Summary);
        AddLine(detail, "Strengths", string.Join(", ", profile.Strengths));
        AddLine(detail, "Weaknesses", string.Join(", ", profile.Weaknesses));
        AddLine(detail, "Assignment", profile.CurrentAssignment);
        AddLine(detail, "Focus", profile.CurrentFocus);
        AddActions(detail,
            CreateDetailButton("View Profile", () => ShowStaffProfile(row.PersonId)),
            CreateDetailButton("View Dossier/Profile", () => OpenDossierFor(row.PersonId)),
            CreateDetailButton("Reassign Role", () => State.ReassignStaffRoleFor(row.PersonId)),
            CreateDetailButton("Release Staff", () => State.ReleaseStaffFor(row.PersonId)),
            CreateDetailButton("Set Focus", () => SetStaffFocusFor(row.PersonId)),
            CreateDetailButton("Generate Evaluation", () => State.GenerateStaffEvaluationFor(row.PersonId)));
        return detail;
    }

    private UIElement BuildScoutingOperationDetail(SelectablePersonRow? row)
    {
        if (row is null)
        {
            return EmptyDetail("Scouting Operations", "Select a scout to assign regional or player-specific work.");
        }

        var profile = State.ScoutProfiles.FirstOrDefault(profile => profile.ScoutPersonId == row.PersonId);
        if (profile is null)
        {
            return EmptyDetail("Scouting Operations", "This scout is no longer available.");
        }

        var panel = CreateDetailPanel(profile.Name, "Selected scout");
        AddLine(panel, "Role", profile.Role);
        AddLine(panel, "Region specialty", profile.RegionSpecialty);
        AddLine(panel, "Reputation", profile.Reputation);
        AddLine(panel, "GM relationship", $"{profile.RelationshipWithGm}/100");
        AddLine(panel, "Current assignment", profile.CurrentAssignment);
        AddLine(panel, "Workload", profile.Workload);
        AddLine(panel, "Strengths", string.Join(", ", profile.Strengths));
        AddLine(panel, "Weaknesses", string.Join(", ", profile.Weaknesses));
        AddLine(panel, "Warning", profile.ConflictWarning);
        AddActions(panel,
            CreateDetailButton("Assign Region", () => ShowScoutAssignmentDialog(null, row.PersonId, ScoutingRegionFocus.WesternCanada), State.IsScoutAvailable(row.PersonId)),
            CreateDetailButton("Assign Player", () => ShowScoutAssignmentDialog(State.NextUnassignedScoutingTargetId(), row.PersonId), State.IsScoutAvailable(row.PersonId) && State.NextUnassignedScoutingTargetId() is not null),
            CreateDetailButton("Set Scouting Focus", () => SetStaffFocusFor(row.PersonId)),
            CreateDetailButton("View Profile", () => ShowStaffProfile(row.PersonId)));

        AddSubHeader(panel, "Active Assignments");
        var assignments = State.ScenarioSnapshot.ScoutingOperations.Where(assignment => assignment.ScoutPersonId == row.PersonId && assignment.IsOpen).ToArray();
        if (assignments.Length == 0)
        {
            AddParagraph(panel, "Available for assignment.");
        }

        foreach (var assignment in assignments)
        {
            AddParagraph(panel, $"{assignment.TargetName} | {assignment.Priority} | duration {assignment.DurationDays} days | return {(assignment.ReturnDate ?? assignment.ExpectedReportDate):yyyy-MM-dd} | {assignment.Notes}");
        }

        return panel;
    }

    private UIElement BuildPlayerDetail(string tab, SelectablePersonRow? row)
    {
        if (row is null)
        {
            return EmptyDetail(tab, "Select a player, recruit, or prospect to see valid actions.");
        }

        var panel = CreateDetailPanel(row.Name, row.Primary);
        AddLine(panel, "Status / role", row.Primary);
        AddLine(panel, "Context", row.Secondary);
        AddLine(panel, "GM relationship", $"{State.RelationshipWithGm(row.PersonId)}/100");
        AddParagraph(panel, row.Summary);

        if (tab == "Roster")
        {
            AddLine(panel, "Name", row.Name);
            AddLine(panel, "Position", State.PersonPosition(row.PersonId));
            AddLine(panel, "Age", State.PersonAge(row.PersonId)?.ToString() ?? "unknown");
            AddLine(panel, "Player type", State.PlayerType(row.PersonId));
            AddLine(panel, "Current lineup role", State.CurrentLineupRole(row.PersonId));
            AddLine(panel, "Potential lineup role", State.PotentialLineupRole(row.PersonId));
            AddLine(panel, "Contract / rights status", State.ContractRightsStatus(row.PersonId));
            AddLine(panel, "Development trend", State.DevelopmentTrend(row.PersonId));
            AddLine(panel, "Injury status", State.InjuryStatus(row.PersonId));
        }

        if (tab == "Recruits")
        {
            var recruit = State.Snapshot.Recruits.FirstOrDefault(recruit => recruit.RecruitPersonId == row.PersonId);
            if (recruit is not null)
            {
                var profile = State.RecruitingProfileFor(row.PersonId);
                AddLine(panel, "Position", profile.Position);
                AddLine(panel, "Age", profile.Age?.ToString() ?? "unknown");
                AddLine(panel, "Region / hometown", profile.RegionOrHometown);
                AddLine(panel, "Current team", profile.CurrentTeam);
                AddLine(panel, "Interest", $"{profile.InterestLevel}/100");
                AddLine(panel, "Relationship / trust", $"{profile.RelationshipWithGm}/100");
                AddLine(panel, "Decision style", profile.DecisionStyle);
                AddLine(panel, "Looking for", State.RecruitLookingFor(row.PersonId));
                AddLine(panel, "Development priority", State.RecruitPriorityValue(row.PersonId, RecruitPriority.Development));
                AddLine(panel, "Ice time priority", State.RecruitPriorityValue(row.PersonId, RecruitPriority.IceTime));
                AddLine(panel, "Coaching priority", State.RecruitPriorityValue(row.PersonId, RecruitPriority.Coaching));
                AddLine(panel, "Facilities priority", State.RecruitPriorityValue(row.PersonId, RecruitPriority.Facilities));
                AddLine(panel, "Pathway priority", State.RecruitPriorityValue(row.PersonId, RecruitPriority.PathwayToHigherHockey));
                AddLine(panel, "Family priorities", State.RecruitFamilyPrioritySummary(row.PersonId));
                AddLine(panel, "Scouting confidence", profile.ScoutingConfidence);
                AddLine(panel, "Projection", profile.ProjectionSummary);
                AddLine(panel, "Risk", profile.RiskSummary);
                AddLine(panel, "Current offers", profile.CurrentOffers.Count == 0 ? "none" : string.Join(", ", profile.CurrentOffers));
                AddLine(panel, "Top competitor", profile.TopCompetitor is null ? "none" : $"{profile.TopCompetitor.TeamName} ({profile.TopCompetitor.InterestStrength}/100)");
                AddLine(panel, "Why they are interested", profile.WhyTheyAreInterested);
                AddLine(panel, "Why they may choose us", profile.WhyTheyMayChooseUs);
                AddLine(panel, "Why they may reject us", profile.WhyTheyMayRejectUs);
                AddLine(panel, "Promises made", profile.PromisesMade.Count == 0 ? "none" : string.Join(", ", profile.PromisesMade));
                AddLine(panel, "GM notes", profile.GmNotes);
            }
        }

        if (tab == "Draft Board")
        {
            var entry = State.Snapshot.DraftBoard.Entries.FirstOrDefault(entry => entry.ProspectPersonId == row.PersonId);
            if (entry is not null)
            {
                AddLine(panel, "Report", entry.ScoutingReportId ?? "none");
                if (entry.Bio is not null)
                {
                    AddLine(panel, "Shoots/Catches", entry.Bio.ShootsCatches);
                    AddLine(panel, "Height / Weight", $"{entry.Bio.HeightDisplay}, {entry.Bio.WeightDisplay}");
                    AddLine(panel, "Birth year", entry.Bio.BirthYear);
                    AddLine(panel, "Hometown", $"{entry.Bio.Hometown}, {entry.Bio.ProvinceState}, {entry.Bio.Country}");
                    AddLine(panel, "Current team", $"{entry.Bio.CurrentTeam} - {entry.Bio.League}");
                    AddLine(panel, "Character", entry.Bio.CharacterSummary);
                    AddLine(panel, "Lineup projection", entry.Bio.PotentialLineupProjection);
                }

                AddLine(panel, "Analytics", string.IsNullOrWhiteSpace(entry.AnalyticsSummary) ? "not available" : entry.AnalyticsSummary);
                AddLine(panel, "GM notes", string.IsNullOrWhiteSpace(entry.PersonalNotes) ? "none" : entry.PersonalNotes);
            }
        }

        if (tab is "Scouting" or "Draft Board")
        {
            AddLine(panel, "Position", State.PersonPosition(row.PersonId));
            AddLine(panel, "Age", State.PersonAge(row.PersonId)?.ToString() ?? "unknown");
            AddLine(panel, "Region/team", State.RegionTeamText(row.PersonId));
            AddLine(panel, "Assigned scout", State.AssignedScoutText(row.PersonId));
            AddLine(panel, "Report status", State.ScoutingReportStatus(row.PersonId));
        }

        if (tab == "Prospect List")
        {
            var prospect = State.ScenarioSnapshot.ProspectRights.FirstOrDefault(prospect => prospect.ProspectPersonId == row.PersonId);
            if (prospect is not null)
            {
                AddLine(panel, "Draft", $"Round {prospect.RoundNumber}, pick {prospect.PickNumber}");
                AddLine(panel, "Rights status", prospect.Status);
                AddLine(panel, "Confidence", prospect.ScoutingConfidence?.ToString() ?? "Unknown");
                AddLine(panel, "GM notes", string.IsNullOrWhiteSpace(prospect.GmNotes) ? "none" : prospect.GmNotes);
            }
        }

        AddActions(panel, BuildPlayerActionButtons(tab, row).ToArray());
        return panel;
    }

    private UIElement BuildTrainingCampDetail(SelectablePersonRow? row)
    {
        var calendar = State.TrainingCampCalendar;
        if (row is null)
        {
            var panel = CreateDetailPanel("Training Camp", State.TrainingCampStatusText);
            AddLine(panel, "Camp Opens", calendar.OpensOn.ToString("yyyy-MM-dd"));
            AddLine(panel, "Camp Closes / Deadline", calendar.ClosesOn.ToString("yyyy-MM-dd"));
            AddLine(panel, "Days until roster deadline", calendar.DaysUntilRosterDeadline);
            AddLine(panel, "Current camp roster count", calendar.CurrentCampRosterCount);
            AddLine(panel, "Required opening roster size", calendar.RequiredOpeningRosterSize);
            AddLine(panel, "Players that must be cut/moved", calendar.PlayersOverLimit);
            AddLine(panel, "Roster status", calendar.IsRosterCompliant ? "Roster Compliant" : calendar.RosterValidationResult.Message);
            AddActions(panel, CreateDetailButton("Complete Camp", CompleteTrainingCamp, State.CanCompleteTrainingCamp));
            return panel;
        }

        var panelWithPlayer = (StackPanel)BuildPlayerDetail("Training Camp", row);
        var camp = State.ScenarioSnapshot.TrainingCamp;
        var evaluation = camp?.FindEvaluation(row.PersonId);
        if (evaluation is not null)
        {
            AddSubHeader(panelWithPlayer, "Camp Evaluation");
            AddLine(panelWithPlayer, "Score", $"{evaluation.CampScore}/100");
            AddLine(panelWithPlayer, "Readiness", evaluation.Readiness);
            AddLine(panelWithPlayer, "Upside", evaluation.DevelopmentUpside);
            AddLine(panelWithPlayer, "Coach note", evaluation.CoachNote);
            AddLine(panelWithPlayer, "Scout note", evaluation.ScoutNote);
            AddLine(panelWithPlayer, "Risk", evaluation.RiskNote);
            AddLine(panelWithPlayer, "Recommendation", evaluation.Recommendation);
        }

        AddActions(panelWithPlayer, CreateDetailButton("Complete Camp", CompleteTrainingCamp, State.CanCompleteTrainingCamp));
        return panelWithPlayer;
    }

    private UIElement BuildDossierDetail(SelectablePersonRow? row)
    {
        if (row is not null)
        {
            State.OpenDossier(row.PersonId);
        }

        var dossier = State.CurrentDossier;
        if (dossier is null)
        {
            return EmptyDetail("Player Dossier", "Select a roster player, recruit, prospect, or camp invitee.");
        }

        var panel = CreateDetailPanel(dossier.PlayerName, $"Age {dossier.Age} | {dossier.Position}");
        AddLine(panel, "Status", dossier.Status);
        AddLine(panel, "Team / rights", dossier.TeamOrRights);
        AddLine(panel, "Source", dossier.Source);
        AddActions(panel, CreateDetailButton("Add GM Note", () => State.AddDossierNoteFor(dossier.PersonId)));
        foreach (var section in dossier.Sections)
        {
            AddSubHeader(panel, section.Title);
            foreach (var line in section.Lines)
            {
                AddParagraph(panel, line);
            }
        }

        return panel;
    }

    private IEnumerable<Button> BuildPlayerActionButtons(string tab, SelectablePersonRow row)
    {
        yield return CreateDetailButton("View Dossier", () => OpenDossierFor(row.PersonId));
        yield return CreateDetailButton("Add GM Note", () => State.AddDossierNoteFor(row.PersonId));

        if (tab == "Recruits")
        {
            yield return CreateDetailButton("Call Recruit", () => State.CallRecruitFor(row.PersonId));
            yield return CreateDetailButton("Call Family", () => State.CallRecruitFamilyFor(row.PersonId));
            yield return CreateDetailButton("Invite Visit", () => State.InviteRecruitVisitFor(row.PersonId));
            yield return CreateDetailButton("Make Offer", () => State.MakeRecruitingOfferFor(row.PersonId), State.CanOfferRecruit(row.PersonId));
            yield return CreateDetailButton("Make Promise", () => State.MakeRecruitingPromiseFor(row.PersonId));
            yield return CreateDetailButton("Education Package", () => State.OfferRecruitEducationPackageFor(row.PersonId));
            yield return CreateDetailButton("Ask Scout", () => State.AskScoutForRecruitFor(row.PersonId));
            yield return CreateDetailButton("Withdraw Offer", () => State.WithdrawRecruitOfferFor(row.PersonId), State.CanWithdrawRecruitOffer(row.PersonId));
            yield break;
        }

        if (tab is "Scouting" or "Draft Board")
        {
            yield return CreateDetailButton("Board Up", () => State.MoveDraftBoardPlayer(row.PersonId, -1), State.IsDraftUiEnabled);
            yield return CreateDetailButton("Board Down", () => State.MoveDraftBoardPlayer(row.PersonId, 1), State.IsDraftUiEnabled);
            yield return CreateDetailButton("Star", () => State.ToggleStarProspect(row.PersonId), State.IsDraftUiEnabled);
            yield return CreateDetailButton("GM Note", () => State.AddDraftNoteFor(row.PersonId), State.IsDraftUiEnabled);
            yield return CreateDetailButton("Assign Scout", () => ShowScoutAssignmentDialog(row.PersonId), State.AvailableScoutProfiles.Count > 0);
        }

        var available = State.AvailableProspectActions(row.PersonId);
        yield return CreateDetailButton("Offer Contract", () => State.OfferProspectContractFor(row.PersonId), available.Contains(ProspectDecisionType.OfferContract));
        yield return CreateDetailButton("Invite Prospect", () => State.InviteProspectToCampFor(row.PersonId), available.Contains(ProspectDecisionType.InviteToCamp));
        yield return CreateDetailButton("Return Prospect", () => State.ReturnProspectToJuniorOrYouthFor(row.PersonId), available.Contains(ProspectDecisionType.ReturnToJunior) || available.Contains(ProspectDecisionType.ReturnToYouthTeam));
        yield return CreateDetailButton("Assign Prospect", () => State.AssignProspectToAffiliateFor(row.PersonId), available.Contains(ProspectDecisionType.AssignToAffiliate));
        yield return CreateDetailButton("Release Rights", () => State.ReleaseProspectRightsFor(row.PersonId), available.Contains(ProspectDecisionType.ReleaseRights));

        if (tab == "Training Camp")
        {
            yield return CreateDetailButton("Keep", () => State.ApplyCampDecisionFor(row.PersonId, TrainingCampDecisionType.Keep), State.CanApplyCampDecision(row.PersonId));
            yield return CreateDetailButton("Cut", () => State.ApplyCampDecisionFor(row.PersonId, TrainingCampDecisionType.Cut), State.CanApplyCampDecision(row.PersonId));
            yield return CreateDetailButton("Release", () => State.ApplyCampDecisionFor(row.PersonId, TrainingCampDecisionType.Release), State.CanApplyCampDecision(row.PersonId));
            yield return CreateDetailButton("Return Junior", () => State.ApplyCampDecisionFor(row.PersonId, TrainingCampDecisionType.ReturnToJuniorTeam), State.CanApplyCampDecision(row.PersonId));
            yield return CreateDetailButton("Assign/Return", () => State.AssignOrReturnTrainingCampPlayerFor(row.PersonId), State.CanApplyCampDecision(row.PersonId));
            yield return CreateDetailButton("Waivers", () => State.ApplyCampDecisionFor(row.PersonId, TrainingCampDecisionType.PlaceOnWaivers), State.CanApplyCampDecision(row.PersonId));
            yield return CreateDetailButton("Mark Injured", () => State.ApplyCampDecisionFor(row.PersonId, TrainingCampDecisionType.MarkInjured), State.CanApplyCampDecision(row.PersonId));
        }
    }

    private StackPanel EmptyDetail(string title, string message)
    {
        var panel = CreateDetailPanel(title, "No selection");
        AddParagraph(panel, message);
        return panel;
    }

    private StackPanel CreateDetailPanel(string title, string subtitle)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(20, 40, 64)),
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(new TextBlock
        {
            Text = subtitle,
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(92, 105, 120)),
            Margin = new Thickness(0, 4, 0, 14),
            TextWrapping = TextWrapping.Wrap
        });
        return panel;
    }

    private static void AddSubHeader(StackPanel panel, string text)
    {
        panel.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(38, 58, 82)),
            Margin = new Thickness(0, 16, 0, 6)
        });
    }

    private static void AddLine(StackPanel panel, string label, object? value)
    {
        panel.Children.Add(new TextBlock
        {
            Text = $"{label}: {value ?? "unknown"}",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 5)
        });
    }

    private static void AddParagraph(StackPanel panel, string text)
    {
        panel.Children.Add(new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(52, 65, 82)),
            Margin = new Thickness(0, 0, 0, 8)
        });
    }

    private static void AddActions(StackPanel panel, params Button[] buttons)
    {
        if (buttons.Length == 0)
        {
            return;
        }

        var actions = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 10, 0, 6)
        };
        foreach (var button in buttons)
        {
            actions.Children.Add(button);
        }

        panel.Children.Add(actions);
    }

    private Button CreateDetailButton(string text, Action action, bool enabled = true)
    {
        var button = CreateButton(text, action);
        button.MinWidth = 118;
        button.IsEnabled = enabled;
        if (!enabled)
        {
            button.ToolTip = "Coming soon";
        }

        return button;
    }

    private void RefreshDraftModal()
    {
        if (_draftModalOverlay is null || _state is null)
        {
            return;
        }

        if (!State.IsDraftModalVisible)
        {
            _draftModalOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        _draftModalOverlay.Visibility = Visibility.Visible;
        _draftModalOverlay.Child = BuildDraftModalContent();
    }

    private UIElement BuildDraftModalContent()
    {
        var shell = new Grid();
        shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var panel = new Border
        {
            Background = Brushes.White,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(22),
            MaxWidth = 1320,
            MaxHeight = 720,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var content = new Grid();
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var title = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        title.Children.Add(new TextBlock
        {
            Text = "Draft Day",
            FontSize = 30,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(20, 40, 64))
        });
        title.Children.Add(new TextBlock
        {
            Text = State.LatestSummary,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(75, 88, 104)),
            Margin = new Thickness(0, 6, 0, 0)
        });
        Grid.SetRow(title, 0);
        content.Children.Add(title);

        if (State.ScenarioSnapshot.DraftExperience is null)
        {
            var startPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            startPanel.Children.Add(new TextBlock
            {
                Text = $"The {State.ScenarioSnapshot.Season.Year} league draft is ready.",
                FontSize = 18,
                Margin = new Thickness(0, 0, 0, 18),
                TextAlignment = TextAlignment.Center
            });
            startPanel.Children.Add(CreateButton("Start Draft", State.StartLiveDraft));
            Grid.SetRow(startPanel, 1);
            content.Children.Add(startPanel);
        }
        else
        {
            var body = BuildLiveDraftBody();
            Grid.SetRow(body, 1);
            content.Children.Add(body);
        }

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };

        if (State.ScenarioSnapshot.DraftExperience?.Status == DraftExperienceStatus.Completed)
        {
            footer.Children.Add(CreateButton("End Draft", State.EndLiveDraftModal));
        }

        Grid.SetRow(footer, 2);
        content.Children.Add(footer);

        panel.Child = content;
        shell.Children.Add(panel);
        return shell;
    }

    private UIElement BuildLiveDraftBody()
    {
        var state = State.ScenarioSnapshot.DraftExperience!;
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var prospectList = new ListBox
        {
            MinHeight = 360,
            BorderBrush = new SolidColorBrush(Color.FromRgb(218, 226, 235)),
            BorderThickness = new Thickness(1)
        };

        foreach (var entry in State.Snapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank))
        {
            prospectList.Items.Add(new ListBoxItem
            {
                Tag = entry.ProspectPersonId,
                Content = BuildLiveDraftMiddleRow(entry)
            });
        }

        if (prospectList.Items.Count > 0)
        {
            prospectList.SelectedIndex = 0;
        }

        var prospectCard = new TextBox
        {
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            BorderBrush = new SolidColorBrush(Color.FromRgb(218, 226, 235)),
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Color.FromRgb(250, 252, 254)),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            Padding = new Thickness(12),
            Text = BuildLiveDraftProspectCard((prospectList.SelectedItem as ListBoxItem)?.Tag as string)
        };
        prospectList.SelectionChanged += (_, _) =>
        {
            prospectCard.Text = BuildLiveDraftProspectCard((prospectList.SelectedItem as ListBoxItem)?.Tag as string);
        };

        var left = new Grid { Margin = new Thickness(0, 0, 16, 0) };
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        AddPanelHeader(left, "Selected Prospect Card");
        Grid.SetRow(prospectCard, 1);
        left.Children.Add(prospectCard);
        Grid.SetColumn(left, 0);
        root.Children.Add(left);

        var middle = new Grid { Margin = new Thickness(0, 0, 16, 0) };
        middle.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        middle.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        AddPanelHeader(middle, "Draft Player List");
        Grid.SetRow(prospectList, 1);
        middle.Children.Add(prospectList);
        Grid.SetColumn(middle, 1);
        root.Children.Add(middle);

        var draftButton = CreateButton("Draft Player", () =>
        {
            var selected = prospectList.SelectedItem as ListBoxItem;
            var prospectId = selected?.Tag as string
                ?? State.Snapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank).FirstOrDefault()?.ProspectPersonId;
            if (!string.IsNullOrWhiteSpace(prospectId))
            {
                State.DraftSelectedProspect(prospectId);
            }
        });
        draftButton.IsEnabled = state.IsPlayerTurn && state.Status == DraftExperienceStatus.AwaitingPlayerPick && prospectList.Items.Count > 0;
        draftButton.MinWidth = 170;
        draftButton.MinHeight = 44;
        draftButton.FontSize = 16;
        draftButton.Margin = new Thickness(0, 0, 12, 12);
        draftButton.Background = new SolidColorBrush(Color.FromRgb(24, 85, 142));
        draftButton.Foreground = Brushes.White;

        var actionBar = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(0, 0, 0, 10)
        };

        if (State.ScenarioSnapshot.DraftExperience?.Status is DraftExperienceStatus.NotStarted or DraftExperienceStatus.PreDraft)
        {
            actionBar.Children.Add(CreateButton("Start Draft", State.StartLiveDraft));
        }

        actionBar.Children.Add(draftButton);
        if (State.ScenarioSnapshot.DraftExperience?.Status == DraftExperienceStatus.Completed)
        {
            actionBar.Children.Add(CreateButton("End Draft", State.EndLiveDraftModal));
        }

        var instruction = new TextBlock
        {
            Text = state.IsPlayerTurn
                ? "Select a prospect, then click Draft Player."
                : "Waiting for your next pick.",
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(55, 70, 88)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 10)
        };
        actionBar.Children.Add(instruction);

        var statusText = new TextBox
        {
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            BorderBrush = new SolidColorBrush(Color.FromRgb(218, 226, 235)),
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Color.FromRgb(250, 252, 254)),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            Padding = new Thickness(12),
            Text = BuildLiveDraftText()
        };

        var right = new Grid();
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        AddPanelHeader(right, "Draft Status");
        Grid.SetRow(actionBar, 1);
        right.Children.Add(actionBar);
        Grid.SetRow(statusText, 2);
        right.Children.Add(statusText);
        Grid.SetColumn(right, 2);
        root.Children.Add(right);

        return root;
    }

    private static void AddPanelHeader(Grid grid, string text)
    {
        var header = new TextBlock
        {
            Text = text,
            FontWeight = FontWeights.SemiBold,
            FontSize = 15,
            Foreground = new SolidColorBrush(Color.FromRgb(20, 40, 64)),
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(header, 0);
        grid.Children.Add(header);
    }

    private string BuildLiveDraftText()
    {
        var draft = State.ScenarioSnapshot.DraftExperience!;
        var builder = new StringBuilder();
        builder.AppendLine($"Status: {draft.Status}");
        builder.AppendLine($"Current round: {draft.CurrentRound}/{draft.TotalRounds}");
        builder.AppendLine($"Current pick: {draft.CurrentPick?.PickNumber.ToString() ?? "complete"}");
        builder.AppendLine($"Overall pick: {draft.OverallPick}");
        builder.AppendLine($"Team selecting: {draft.TeamSelecting}");
        builder.AppendLine($"Your next pick: {draft.PlayerNextPick?.PickNumber.ToString() ?? "none"}");
        builder.AppendLine($"Available players: {State.Snapshot.DraftBoard.Entries.Count}");
        builder.AppendLine();

        builder.AppendLine("Recent Picks");
        foreach (var selection in draft.Selections.OrderByDescending(item => item.PickNumber).Take(8).OrderBy(item => item.PickNumber))
        {
            builder.AppendLine($"  #{selection.PickNumber} {selection.OrganizationName}: {selection.ProspectName}");
        }

        builder.AppendLine();
        builder.AppendLine("Your Selections / Draft Rights");
        var rights = State.ScenarioSnapshot.DraftRights.Count > 0
            ? State.ScenarioSnapshot.DraftRights
            : draft.Selections.Where(item => item.IsPlayerSelection).ToArray();
        if (rights.Count == 0)
        {
            builder.AppendLine("  None yet.");
        }

        foreach (var selection in rights)
        {
            builder.AppendLine($"  R{selection.RoundNumber} #{selection.PickNumber}: {selection.ProspectName}");
        }

        if (draft.Recap is not null)
        {
            builder.AppendLine();
            builder.AppendLine("Draft Recap");
            builder.AppendLine($"  Players drafted: {draft.Recap.PlayersDrafted}");
            builder.AppendLine($"  Owner: {draft.Recap.OwnerReaction}");
            builder.AppendLine($"  Scout: {draft.Recap.HeadScoutReaction}");
        }

        return builder.ToString();
    }

    private string BuildLiveDraftMiddleRow(DraftBoardEntry entry)
    {
        var teamLeague = entry.Bio is null ? State.RegionTeamText(entry.ProspectPersonId) : $"{entry.Bio.CurrentTeam} / {entry.Bio.League}";
        return $"{entry.Rank}. {FindPersonName(entry.ProspectPersonId)} | {State.PersonPosition(entry.ProspectPersonId)} | {teamLeague} | Confidence: {entry.ScoutingConfidence?.ToString() ?? "Unknown"}";
    }

    private string BuildLiveDraftProspectCard(string? prospectId)
    {
        var entry = string.IsNullOrWhiteSpace(prospectId)
            ? State.Snapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank).FirstOrDefault()
            : State.Snapshot.DraftBoard.Entries.FirstOrDefault(entry => entry.ProspectPersonId == prospectId);
        if (entry is null)
        {
            return "No available draft prospect selected.";
        }

        var builder = new StringBuilder();
        builder.AppendLine("Selected Prospect Card");
        builder.AppendLine("======================");
        builder.AppendLine($"Name: {FindPersonName(entry.ProspectPersonId)}");
        builder.AppendLine($"Position: {State.PersonPosition(entry.ProspectPersonId)}");
        builder.AppendLine($"Age: {State.PersonAge(entry.ProspectPersonId)?.ToString() ?? "unknown"}");
        if (entry.Bio is not null)
        {
            builder.AppendLine($"Shoots/Catches: {entry.Bio.ShootsCatches}");
            builder.AppendLine($"Height: {entry.Bio.HeightDisplay}");
            builder.AppendLine($"Weight: {entry.Bio.WeightDisplay}");
            builder.AppendLine($"Birth year: {entry.Bio.BirthYear}");
            builder.AppendLine($"Hometown: {entry.Bio.Hometown}, {entry.Bio.ProvinceState}, {entry.Bio.Country}");
            builder.AppendLine($"Current team: {entry.Bio.CurrentTeam}");
            builder.AppendLine($"Current league: {entry.Bio.League}");
            builder.AppendLine($"Region: {entry.Bio.ProvinceState}, {entry.Bio.Country}");
            builder.AppendLine($"Potential lineup role: {entry.Bio.PotentialLineupProjection}");
            builder.AppendLine($"Character summary: {entry.Bio.CharacterSummary}");
        }
        else
        {
            builder.AppendLine("Shoots/Catches: basic bio pending");
            builder.AppendLine("Height: basic bio pending");
            builder.AppendLine("Weight: basic bio pending");
            builder.AppendLine($"Region: {State.RegionTeamText(entry.ProspectPersonId)}");
        }

        builder.AppendLine($"Scouting confidence: {entry.ScoutingConfidence?.ToString() ?? "Unknown"}");
        builder.AppendLine($"Projection: {entry.ProjectionText}");
        builder.AppendLine($"Player type: {State.PlayerType(entry.ProspectPersonId)}");
        builder.AppendLine($"Risk summary: {DraftRiskSummary(entry)}");
        builder.AppendLine($"GM notes: {(string.IsNullOrWhiteSpace(entry.PersonalNotes) ? "none" : entry.PersonalNotes)}");

        var reports = State.ScenarioSnapshot.CompletedScoutingReports
            .Where(report => report.PlayerId == entry.ProspectPersonId)
            .OrderByDescending(report => report.CreatedOn)
            .ToArray();
        builder.AppendLine("Scouting reports:");
        if (reports.Length == 0)
        {
            builder.AppendLine("  No completed report yet. Basic bio remains visible.");
        }

        foreach (var report in reports.Take(3))
        {
            builder.AppendLine($"  {report.CreatedOn:yyyy-MM-dd} | {report.Confidence} | {report.Recommendation}");
            if (report.Confidence is ScoutingConfidenceLevel.High or ScoutingConfidenceLevel.VeryHigh)
            {
                builder.AppendLine($"    {report.Opinions.FirstOrDefault() ?? report.Observations.FirstOrDefault() ?? "No detailed note."}");
            }
        }

        var recommendation = reports.FirstOrDefault()?.Recommendation.ToString() ?? State.AssignedScoutText(entry.ProspectPersonId);
        builder.AppendLine($"Staff/scout recommendation: {recommendation}");
        return builder.ToString();
    }

    private static string DraftRiskSummary(DraftBoardEntry entry) =>
        !string.IsNullOrWhiteSpace(entry.AnalyticsSummary)
            ? entry.AnalyticsSummary
            : entry.ScoutingConfidence switch
            {
                ScoutingConfidenceLevel.VeryHigh or ScoutingConfidenceLevel.High => "Risk is mostly role-fit and development timeline; staff have useful evidence.",
                ScoutingConfidenceLevel.Medium => "Moderate uncertainty; staff want another viewing before changing the board.",
                ScoutingConfidenceLevel.Low or ScoutingConfidenceLevel.Unknown or null => "High uncertainty; basic bio is known but projection detail is limited.",
                _ => "Risk summary unavailable."
            };

    private string BuildDashboard()
    {
        var snapshot = State.Snapshot;
        var builder = new StringBuilder();
        builder.AppendLine("Dashboard");
        builder.AppendLine("=========");
        builder.AppendLine($"World: {snapshot.WorldState.WorldName}");
        builder.AppendLine($"Organization: {snapshot.Organization?.Name ?? snapshot.OrganizationId}");
        builder.AppendLine($"Date: {snapshot.CurrentDate:yyyy-MM-dd}");
        builder.AppendLine($"Season phase: {snapshot.Season?.CurrentPhase.ToString() ?? snapshot.WorldState.CurrentPhase.ToString()}");
        builder.AppendLine($"Draft date: {State.ScenarioSnapshot.DraftDate:yyyy-MM-dd} ({State.ScenarioSnapshot.DaysUntilDraft} days away)");
        builder.AppendLine($"Draft UI: {(State.IsDraftUiEnabled ? "enabled by rulebook" : "disabled by rulebook")}");
        builder.AppendLine($"Draft status: {State.ScenarioSnapshot.DraftExperience?.Status.ToString() ?? "PreDraft"}");
        builder.AppendLine($"Training camp: {State.TrainingCampStatusText}");
        builder.AppendLine($"Season readiness: {State.SeasonReadinessReport.RosterStatus}");
        builder.AppendLine($"Executive reports archived: {State.ScenarioSnapshot.ExecutiveReports.Reports.Count}");
        builder.AppendLine($"Scouting assignments: {State.ScenarioSnapshot.ScoutingOperations.Count}");
        builder.AppendLine($"Completed scouting reports: {State.ScenarioSnapshot.CompletedScoutingReports.Count}");
        builder.AppendLine($"Pending GM actions: {State.OpenPendingActions.Count}");
        if (State.ScenarioSnapshot.DraftExperience is { } draftState)
        {
            builder.AppendLine($"Draft round: {draftState.CurrentRound}/{draftState.TotalRounds}");
            builder.AppendLine($"Overall pick: {draftState.OverallPick}");
            builder.AppendLine($"Team selecting: {draftState.TeamSelecting}");
        }
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
        builder.AppendLine($"Draft rights / prospects: {State.ScenarioSnapshot.DraftRights.Count}");
        builder.AppendLine($"Relationships: {snapshot.Relationships.Count}");
        builder.AppendLine($"Development profiles: {snapshot.DevelopmentProfiles.Count}");
        builder.AppendLine($"Active injuries: {snapshot.Injuries.Count(injury => injury.IsActive)}");
        builder.AppendLine($"Staff members: {snapshot.StaffMembers.Count}");
        builder.AppendLine($"Contract references: {snapshot.Contracts.Count}");
        builder.AppendLine($"Pending actions: {State.OpenPendingActions.Count}");
        builder.AppendLine();
        builder.AppendLine("Latest Summary");
        builder.AppendLine(State.LatestSummary);
        return builder.ToString();
    }

    private void RefreshInboxPanels()
    {
        if (_inboxCategoryPanel is null || _inboxListPanel is null || _inboxReader is null || _state is null)
        {
            return;
        }

        RefreshInboxCategorySidebar();
        var messages = FilterInboxMessages();
        _inboxListPanel.Children.Clear();

        if (messages.Count == 0)
        {
            _inboxListPanel.Children.Add(new TextBlock
            {
                Text = "No visible messages in this category.",
                Margin = new Thickness(18),
                Foreground = new SolidColorBrush(Color.FromRgb(92, 106, 122))
            });
            _selectedInboxItemId = null;
            RenderInboxReader(null);
            return;
        }

        if (_selectedInboxItemId is null || messages.All(message => message.InboxItemId != _selectedInboxItemId))
        {
            _selectedInboxItemId = messages[0].InboxItemId;
        }

        foreach (var message in messages)
        {
            _inboxListPanel.Children.Add(BuildInboxRow(message));
        }

        RenderInboxReader(messages.SingleOrDefault(message => message.InboxItemId == _selectedInboxItemId));
    }

    private void RefreshInboxCategorySidebar()
    {
        if (_inboxCategoryPanel is null)
        {
            return;
        }

        _inboxCategoryPanel.Children.Clear();
        _inboxCategoryPanel.Children.Add(new TextBlock
        {
            Text = "Inbox",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(14, 14, 14, 10),
            Foreground = new SolidColorBrush(Color.FromRgb(20, 40, 64))
        });

        var counts = State.InboxManager.CountsByCategory();
        foreach (var category in Enum.GetValues<InboxCategory>())
        {
            var count = counts.TryGetValue(category, out var value) ? value : 0;
            var button = new Button
            {
                Content = $"{DisplayCategory(category)}  {count}",
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(8, 2, 8, 2),
                Padding = new Thickness(10, 8, 10, 8),
                FontWeight = category == _selectedInboxCategory ? FontWeights.SemiBold : FontWeights.Normal,
                Background = category == _selectedInboxCategory
                    ? new SolidColorBrush(Color.FromRgb(215, 228, 243))
                    : Brushes.Transparent,
                BorderThickness = new Thickness(0)
            };
            button.Click += (_, _) =>
            {
                _selectedInboxCategory = category;
                _selectedInboxItemId = null;
                RefreshInboxPanels();
            };
            _inboxCategoryPanel.Children.Add(button);
        }
    }

    private IReadOnlyList<InboxMessage> FilterInboxMessages()
    {
        var messages = State.InboxManager.Query(new InboxFilter(
            _selectedInboxCategory,
            UnreadOnly: _unreadOnlyFilter?.IsChecked == true,
            ImportantOnly: _importantOnlyFilter?.IsChecked == true));

        if (_pinnedOnlyFilter?.IsChecked == true)
        {
            messages = messages.Where(message => message.IsPinned).ToArray();
        }

        messages = _sortOrderFilter?.SelectedIndex == 1
            ? messages.OrderBy(message => message.Item.Date).ThenBy(message => message.InboxItemId, StringComparer.Ordinal).ToArray()
            : messages.OrderByDescending(message => message.Item.Date).ThenBy(message => message.InboxItemId, StringComparer.Ordinal).ToArray();

        return messages;
    }

    private UIElement BuildInboxRow(InboxMessage message)
    {
        var isSelected = message.InboxItemId == _selectedInboxItemId;
        var row = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(228, 234, 241)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Background = isSelected
                ? new SolidColorBrush(Color.FromRgb(232, 240, 249))
                : Brushes.White,
            Padding = new Thickness(12, 10, 8, 10),
            Cursor = Cursors.Hand
        };
        row.MouseEnter += (_, _) =>
        {
            if (message.InboxItemId != _selectedInboxItemId)
            {
                row.Background = new SolidColorBrush(Color.FromRgb(246, 249, 253));
            }
        };
        row.MouseLeave += (_, _) =>
        {
            row.Background = message.InboxItemId == _selectedInboxItemId
                ? new SolidColorBrush(Color.FromRgb(232, 240, 249))
                : Brushes.White;
        };
        row.MouseLeftButtonUp += (_, _) =>
        {
            _selectedInboxItemId = message.InboxItemId;
            RefreshInboxPanels();
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(7) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var priority = new Border
        {
            Width = 5,
            CornerRadius = new CornerRadius(3),
            Background = PriorityBrush(message),
            Margin = new Thickness(0, 2, 8, 2)
        };
        Grid.SetColumn(priority, 0);
        grid.Children.Add(priority);

        var textPanel = new StackPanel();
        var topLine = new StackPanel { Orientation = Orientation.Horizontal };
        topLine.Children.Add(new TextBlock
        {
            Text = SenderFor(message),
            FontWeight = message.IsUnread ? FontWeights.Bold : FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 8, 0)
        });
        topLine.Children.Add(new TextBlock
        {
            Text = DisplayCategory(message.Category),
            Foreground = new SolidColorBrush(Color.FromRgb(84, 99, 116)),
            Margin = new Thickness(0, 0, 8, 0)
        });
        topLine.Children.Add(new TextBlock
        {
            Text = message.Item.Date.ToString("MMM d"),
            Foreground = new SolidColorBrush(Color.FromRgb(84, 99, 116))
        });
        textPanel.Children.Add(topLine);

        textPanel.Children.Add(new TextBlock
        {
            Text = $"{(message.IsPinned ? "PIN  " : string.Empty)}{(message.IsUnread ? "Unread  " : string.Empty)}{message.Item.Title}",
            FontWeight = message.IsUnread ? FontWeights.Bold : FontWeights.Normal,
            Margin = new Thickness(0, 3, 0, 2)
        });
        textPanel.Children.Add(new TextBlock
        {
            Text = Preview(message.Item.Summary),
            Foreground = new SolidColorBrush(Color.FromRgb(72, 86, 101)),
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        Grid.SetColumn(textPanel, 1);
        grid.Children.Add(textPanel);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0)
        };
        actions.Children.Add(CreateSmallInboxButton(message.IsUnread ? "Read" : "Unread", () => ApplyInboxAction(message, message.IsUnread ? InboxMessageAction.MarkRead : InboxMessageAction.MarkUnread)));
        actions.Children.Add(CreateSmallInboxButton(message.IsPinned ? "Unpin" : "Pin", () => ApplyInboxAction(message, message.IsPinned ? InboxMessageAction.Unpin : InboxMessageAction.Pin)));
        actions.Children.Add(CreateSmallInboxButton("Archive", () => ApplyInboxAction(message, InboxMessageAction.Archive)));
        actions.Children.Add(CreateSmallInboxButton("Delete", () => ApplyInboxAction(message, InboxMessageAction.Delete)));
        Grid.SetColumn(actions, 2);
        grid.Children.Add(actions);

        row.Child = grid;
        return row;
    }

    private void RenderInboxReader(InboxMessage? message)
    {
        if (_inboxReader is null)
        {
            return;
        }

        if (message is null)
        {
            _inboxReader.Child = new TextBlock
            {
                Text = "Select a message to read.",
                Margin = new Thickness(18),
                Foreground = new SolidColorBrush(Color.FromRgb(92, 106, 122))
            };
            return;
        }

        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(new TextBlock
        {
            Text = message.Item.Title,
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"From: {SenderFor(message)}",
            Margin = new Thickness(0, 12, 0, 0)
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"Date: {message.Item.Date:yyyy-MM-dd HH:mm}",
            Margin = new Thickness(0, 4, 0, 0)
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"Category: {DisplayCategory(message.Category)}",
            Margin = new Thickness(0, 4, 0, 14)
        });
        panel.Children.Add(new TextBlock
        {
            Text = message.Item.Summary,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            Margin = new Thickness(0, 0, 0, 18)
        });

        var actions = new WrapPanel { Margin = new Thickness(0, 0, 0, 16) };
        actions.Children.Add(CreateSmallInboxButton(message.IsUnread ? "Mark Read" : "Mark Unread", () => ApplyInboxAction(message, message.IsUnread ? InboxMessageAction.MarkRead : InboxMessageAction.MarkUnread)));
        actions.Children.Add(CreateSmallInboxButton(message.IsPinned ? "Unpin" : "Pin", () => ApplyInboxAction(message, message.IsPinned ? InboxMessageAction.Unpin : InboxMessageAction.Pin)));
        actions.Children.Add(CreateSmallInboxButton("Archive", () => ApplyInboxAction(message, InboxMessageAction.Archive)));
        actions.Children.Add(CreateSmallInboxButton("Delete", () => ApplyInboxAction(message, InboxMessageAction.Delete)));
        panel.Children.Add(actions);

        panel.Children.Add(new TextBlock
        {
            Text = "Future Actions",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 8, 0, 8)
        });
        var future = new WrapPanel();
        future.Children.Add(CreateDisabledActionButton("Reply"));
        future.Children.Add(CreateDisabledActionButton("Forward"));
        future.Children.Add(CreateDisabledActionButton("Schedule Meeting"));
        future.Children.Add(CreateDisabledActionButton("Assign"));
        panel.Children.Add(future);

        _inboxReader.Child = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = panel
        };
    }

    private Button CreateSmallInboxButton(string text, Action action)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(7, 4, 7, 4),
            Margin = new Thickness(4, 0, 0, 0),
            FontSize = 11,
            MinWidth = 42
        };
        button.Click += (_, _) =>
        {
            action();
            RefreshAll();
        };
        return button;
    }

    private static Button CreateDisabledActionButton(string text) =>
        new()
        {
            Content = text,
            IsEnabled = false,
            Padding = new Thickness(8, 5, 8, 5),
            Margin = new Thickness(0, 0, 6, 6)
        };

    private void ApplyInboxAction(InboxMessage message, InboxMessageAction action)
    {
        State.ManageInboxMessage(message.InboxItemId, action);
        if (action is InboxMessageAction.Archive or InboxMessageAction.Delete)
        {
            _selectedInboxItemId = null;
        }
    }

    private static string DisplayCategory(InboxCategory category) =>
        category == InboxCategory.PlayerDevelopment ? "Player Development" : category.ToString();

    private string SenderFor(InboxMessage message) =>
        message.Category switch
        {
            InboxCategory.Owner => State.Snapshot.Owner.Name,
            InboxCategory.Staff => "Staff Office",
            InboxCategory.Scouting => State.Snapshot.Scout.Name,
            InboxCategory.Recruiting => "Recruiting Desk",
            InboxCategory.PlayerDevelopment => "Development Staff",
            InboxCategory.Medical => "Medical Staff",
            InboxCategory.Contracts => "Contracts Desk",
            InboxCategory.Draft => "Draft Desk",
            InboxCategory.League => "League Office",
            _ => message.Item.PrimaryPersonId is null ? "System" : FindPersonName(message.Item.PrimaryPersonId)
        };

    private static Brush PriorityBrush(InboxMessage message) =>
        message.Item.Severity switch
        {
            LegacyEngine.Events.LegacyEventSeverity.Critical => new SolidColorBrush(Color.FromRgb(190, 42, 42)),
            LegacyEngine.Events.LegacyEventSeverity.Warning => new SolidColorBrush(Color.FromRgb(219, 132, 31)),
            _ when message.IsPinned => new SolidColorBrush(Color.FromRgb(51, 108, 172)),
            _ => Brushes.Transparent
        };

    private static string Preview(string text) =>
        text.Length <= 110 ? text : text[..107] + "...";

    private string BuildOwner()
    {
        var owner = State.Snapshot.Owner;
        var builder = new StringBuilder();
        builder.AppendLine("Owner");
        builder.AppendLine("=====");
        builder.AppendLine($"{owner.Name} - {owner.Archetype}");
        builder.AppendLine($"Organization: {State.ScenarioSnapshot.Organization.Name}");
        builder.AppendLine($"Autonomy: {owner.AutonomyLevel}");
        builder.AppendLine($"Trust: {owner.Trust}  Confidence: {owner.Confidence}  Patience: {owner.Patience}");
        builder.AppendLine($"Budget total: {owner.Budget.Total:C0}");
        builder.AppendLine($"Player payroll: {owner.Budget.PlayerPayroll:C0}");
        builder.AppendLine($"Staff: {owner.Budget.Staff:C0}");
        builder.AppendLine($"Scouting: {owner.Budget.Scouting:C0}");
        builder.AppendLine($"Facilities: {owner.Budget.Facilities:C0}");
        builder.AppendLine($"Operations: {owner.Budget.Operations:C0}");
        builder.AppendLine();
        builder.AppendLine("Budget Overview");
        var budget = State.BudgetOverview;
        builder.AppendLine($"Status: {budget.Status}");
        builder.AppendLine($"Total budget: {budget.TotalBudget:C0}");
        builder.AppendLine($"Used budget: {budget.UsedBudget:C0}");
        builder.AppendLine($"Remaining budget: {budget.RemainingBudget:C0}");
        builder.AppendLine($"Over/under budget: {budget.OverUnderBudget:C0}");
        builder.AppendLine($"Player contracts total: {budget.PlayerContractsTotal:C0}");
        builder.AppendLine($"Staff contracts total: {budget.StaffContractsTotal:C0}");
        builder.AppendLine($"GM salary: {budget.GmSalary:C0}");
        builder.AppendLine($"Coaching salaries: {budget.CoachingSalaries:C0}");
        builder.AppendLine($"Scouting salaries: {budget.ScoutingSalaries:C0}");
        builder.AppendLine($"Medical/training salaries: {budget.MedicalTrainingSalaries:C0}");
        builder.AppendLine($"Staff total: {budget.StaffTotal:C0}");
        builder.AppendLine($"Staff release obligations: {budget.StaffReleaseObligations:C0}");
        builder.AppendLine($"Scouting budget: {budget.ScoutingBudget:C0}");
        builder.AppendLine($"Medical/staff operations placeholder: {budget.MedicalAndStaffOperationsBudget:C0}");
        builder.AppendLine($"Owner status: {budget.OwnerBudgetConfidence}");
        builder.AppendLine();
        builder.AppendLine("Goals");
        foreach (var goal in owner.Goals.OrderByDescending(goal => goal.Priority))
        {
            builder.AppendLine($"Priority {goal.Priority}: {goal.GoalType} - {goal.Description}");
        }

        return builder.ToString();
    }

    private string BuildBudgetWorkspace()
    {
        var budget = State.BudgetOverview;
        var builder = new StringBuilder();
        builder.AppendLine("Budget");
        builder.AppendLine("======");
        builder.AppendLine("Hockey Operations Budget");
        builder.AppendLine($"Owner status: {budget.OwnerBudgetConfidence}");
        builder.AppendLine($"Budget status: {budget.Status}");
        builder.AppendLine($"Total budget: {budget.TotalBudget:C0}");
        builder.AppendLine($"Used budget: {budget.UsedBudget:C0}");
        builder.AppendLine($"Remaining budget: {budget.RemainingBudget:C0}");
        builder.AppendLine($"Over/under budget: {budget.OverUnderBudget:C0}");
        builder.AppendLine();
        builder.AppendLine("Breakdown");
        builder.AppendLine($"GM salary: {budget.GmSalary:C0}");
        builder.AppendLine($"Coaching salaries: {budget.CoachingSalaries:C0}");
        builder.AppendLine($"Scouting salaries: {budget.ScoutingSalaries:C0}");
        builder.AppendLine($"Medical/training salaries: {budget.MedicalTrainingSalaries:C0}");
        builder.AppendLine($"Staff contracts: {budget.StaffContractsTotal:C0}");
        builder.AppendLine($"Staff total: {budget.StaffTotal:C0}");
        builder.AppendLine($"Staff release obligations: {budget.StaffReleaseObligations:C0}");
        builder.AppendLine($"Player contracts: {budget.PlayerContractsTotal:C0}");
        builder.AppendLine($"Scouting budget: {budget.ScoutingBudget:C0}");
        builder.AppendLine($"Medical/staff operations: {budget.MedicalAndStaffOperationsBudget:C0}");
        return builder.ToString();
    }

    private string BuildOrganizationHealth()
    {
        var readiness = State.SeasonReadinessReport;
        var builder = new StringBuilder();
        builder.AppendLine("Organization Health");
        builder.AppendLine("===================");
        builder.AppendLine($"Owner mood: {OwnerMoodText()}");
        builder.AppendLine($"Owner satisfaction: {readiness.OwnerSatisfaction}");
        builder.AppendLine($"Organization health: {readiness.OrganizationHealth}");
        builder.AppendLine($"Roster status: {readiness.RosterStatus}");
        builder.AppendLine($"Staff vacancies: {State.StaffVacancySummary}");
        builder.AppendLine($"Budget: {State.BudgetOverview.Status} ({State.BudgetOverview.RemainingBudget:C0} remaining)");
        builder.AppendLine($"Pending GM decisions: {State.PendingDecisionCount}");
        builder.AppendLine($"Roster warnings: {State.RosterWarningCount}");
        builder.AppendLine($"Scouting reports: {State.ScoutingReportCount}");
        builder.AppendLine();
        builder.AppendLine("Owner Review");
        builder.AppendLine(readiness.OwnerReview);
        builder.AppendLine();
        builder.AppendLine("Staff Recommendations");
        builder.AppendLine(readiness.StaffRecommendations);
        return builder.ToString();
    }

    private string BuildDraftRecaps()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Draft Recaps");
        builder.AppendLine("============");
        var recap = State.ScenarioSnapshot.DraftExperience?.Recap;
        if (recap is null)
        {
            builder.AppendLine("No completed draft recap yet.");
            return builder.ToString();
        }

        builder.AppendLine($"Rounds completed: {recap.RoundsCompleted}");
        builder.AppendLine($"Players drafted: {recap.PlayersDrafted}");
        builder.AppendLine($"Owner reaction: {recap.OwnerReaction}");
        builder.AppendLine($"Head scout reaction: {recap.HeadScoutReaction}");
        builder.AppendLine();
        builder.AppendLine("Your Selections / Draft Rights");
        AppendDraftPickSummaries(builder, recap.YourSelections);
        builder.AppendLine();
        builder.AppendLine("Other Notable Selections");
        AppendDraftPickSummaries(builder, recap.OtherNotableSelections);
        builder.AppendLine();
        builder.AppendLine($"Biggest steal: {DraftPickSummaryText(recap.BiggestSteal)}");
        builder.AppendLine($"Biggest surprise: {DraftPickSummaryText(recap.BiggestSurprise)}");
        return builder.ToString();
    }

    private string BuildMonthlySummaries() => BuildMonthlySummary();

    private string BuildCareerHistory()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Career History");
        builder.AppendLine("==============");
        builder.AppendLine("Future career history placeholder.");
        builder.AppendLine("Executive reports, monthly summaries, draft recaps, and season records will live here as the career grows.");
        return builder.ToString();
    }

    private string BuildSettings()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Settings");
        builder.AppendLine("========");
        builder.AppendLine("Settings placeholder.");
        builder.AppendLine("Save/load, preferences, and accessibility options are intentionally not implemented yet.");
        return builder.ToString();
    }

    private static void AppendDraftPickSummaries(StringBuilder builder, IReadOnlyList<DraftPickSummary> picks)
    {
        if (picks.Count == 0)
        {
            builder.AppendLine("  None.");
            return;
        }

        foreach (var pick in picks)
        {
            builder.AppendLine($"  R{pick.RoundNumber} P{pick.PickNumber}: {pick.ProspectName} - {pick.OrganizationName}");
        }
    }

    private static string DraftPickSummaryText(DraftPickSummary? pick) =>
        pick is null ? "None" : $"R{pick.RoundNumber} P{pick.PickNumber}: {pick.ProspectName} - {pick.OrganizationName}";

    private string BuildStaff()
    {
        var snapshot = State.Snapshot;
        var builder = new StringBuilder();
        builder.AppendLine("Staff");
        builder.AppendLine("=====");
        builder.AppendLine($"GM: {snapshot.GeneralManager.Identity.DisplayName}");
        builder.AppendLine($"  Reputation: local {snapshot.GeneralManager.Reputation.Local}, league {snapshot.GeneralManager.Reputation.League}, national {snapshot.GeneralManager.Reputation.National}");
        builder.AppendLine($"Scout: {snapshot.Scout.Name}");
        builder.AppendLine($"  Accuracy: {snapshot.Scout.Accuracy}  Diligence: {snapshot.Scout.Diligence}  Bias: {snapshot.Scout.ReportBias}");
        builder.AppendLine($"  Specialties: {string.Join(", ", snapshot.Scout.Specialties)}");
        builder.AppendLine($"Coach: {snapshot.CoachPerson?.Identity.DisplayName ?? "Not assigned"}");
        builder.AppendLine();
        builder.AppendLine("Staff Actions");
        builder.AppendLine("Reassign Staff, Release Staff, Hire, Release, Replace, Compare, View Profile, View Dossier, Assign Focus, Development Focus, Medical Focus, Scouting Focus, Staff Evaluation, Generate Evaluation, Salary Offer placeholder.");
        builder.AppendLine();
        builder.AppendLine("Selected Staff Details");
        var selected = State.StaffProfiles.FirstOrDefault();
        if (selected is not null)
        {
            builder.AppendLine($"{selected.Name} - {selected.CurrentRole}");
            builder.AppendLine($"  Department: {selected.Department}");
            builder.AppendLine($"  Salary: {selected.Salary.AnnualAmount:C0}");
            builder.AppendLine($"  Contract: {selected.ContractStatus}");
            builder.AppendLine($"  Strengths: {string.Join(", ", selected.Strengths)}");
            builder.AppendLine($"  Weaknesses: {string.Join(", ", selected.Weaknesses)}");
            builder.AppendLine($"  Relationship with GM: {selected.RelationshipWithGm}");
            builder.AppendLine($"  Chemistry: {selected.Chemistry.Summary}");
            builder.AppendLine($"  Current assignment/focus: {selected.CurrentAssignment}; {selected.CurrentFocus}");
            builder.AppendLine();
        }

        builder.AppendLine("Full Staff List");
        foreach (var member in State.StaffProfiles)
        {
            builder.AppendLine($"{member.Name} - {member.CurrentRole} - {member.Department}");
            builder.AppendLine($"  Salary: {member.Salary.AnnualAmount:C0}");
            builder.AppendLine($"  Contract/status: {member.ContractStatus}");
            builder.AppendLine($"  Strengths: {string.Join(", ", member.Strengths)}");
            builder.AppendLine($"  Weaknesses: {string.Join(", ", member.Weaknesses)}");
            builder.AppendLine($"  GM relationship: {member.RelationshipWithGm}");
            builder.AppendLine($"  Chemistry/conflict: {member.Chemistry.Summary}");
            builder.AppendLine($"  Assignment/focus: {member.CurrentAssignment}; {member.CurrentFocus}");
            builder.AppendLine();
        }

        builder.AppendLine("Vacant Positions");
        if (State.StaffVacancies.Count == 0)
        {
            builder.AppendLine("  No required vacancies.");
        }

        foreach (var vacancy in State.StaffVacancies)
        {
            builder.AppendLine($"{StaffRoles.Title(vacancy.Role)} - {vacancy.Department}");
            builder.AppendLine($"  Filled: {vacancy.Current}/{vacancy.Required}  Maximum: {vacancy.Maximum}");
            builder.AppendLine($"  Warning: {vacancy.Warning}");
        }

        builder.AppendLine();
        builder.AppendLine("Candidate Pool / Available Candidates");
        if (State.ScenarioSnapshot.StaffCandidates.Count == 0)
        {
            builder.AppendLine("  No candidates generated yet. Use Candidates to create the first pool.");
        }

        foreach (var candidate in State.ScenarioSnapshot.StaffCandidates)
        {
            builder.AppendLine($"{candidate.Person.Identity.DisplayName} - {candidate.StaffMember.CurrentRole}");
            builder.AppendLine($"  Role fit: {candidate.RoleFit}  Department fit: {candidate.DepartmentFit}  Reputation: {candidate.Reputation}");
            builder.AppendLine($"  Salary ask: {candidate.ExpectedSalary.AnnualAmount:C0}");
            builder.AppendLine($"  Current employer: {candidate.CurrentEmployer}");
            builder.AppendLine($"  Years experience: {candidate.YearsExperience}");
            builder.AppendLine($"  Strengths: {string.Join(", ", candidate.Strengths)}");
            builder.AppendLine($"  Weaknesses: {string.Join(", ", candidate.Weaknesses)}");
            builder.AppendLine($"  Personality/fit: {candidate.PersonalityFitSummary}");
            builder.AppendLine($"  Chemistry risk: {candidate.ChemistryRisk}");
            builder.AppendLine($"  Recommendation: {candidate.HiringRecommendation}");
            builder.AppendLine();
        }

        builder.AppendLine("Recent Staff Evaluations");
        if (State.ScenarioSnapshot.StaffEvaluations.Count == 0)
        {
            builder.AppendLine("  No staff evaluations generated yet.");
        }

        foreach (var evaluation in State.ScenarioSnapshot.StaffEvaluations.OrderByDescending(item => item.EvaluatedOn).Take(5))
        {
            builder.AppendLine($"{FindPersonName(evaluation.PersonId)} - {evaluation.Role} - {evaluation.Recommendation}");
            builder.AppendLine($"  {evaluation.Summary}");
        }

        return builder.ToString();
    }

    private string BuildRoster()
    {
        var snapshot = State.Snapshot;
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
            builder.AppendLine("  View Dossier button: opens overview, scouting, development, medical, contracts, relationships, and GM notes.");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private string BuildRecruits()
    {
        var snapshot = State.Snapshot;
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
            builder.AppendLine("  View Dossier button: opens recruiting, scouting, facts, and GM notes.");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private string BuildScouting()
    {
        var snapshot = State.Snapshot;
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
            builder.AppendLine("  View Dossier button: opens draft profile, scouting evidence, and GM notes.");
        }

        return builder.ToString();
    }

    private string BuildScoutingOperations()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Scouting Operations");
        builder.AppendLine("===================");
        builder.AppendLine("Scout list on left; selected scout profile on right.");
        builder.AppendLine("Assignment controls: region assignment, player assignment, priority, notes, Assign button.");
        builder.AppendLine();
        builder.AppendLine("Regions / Focuses");
        builder.AppendLine("Western Canada, Eastern Canada, USA, Europe, Goalies, Defensemen, Forwards, Character, Medical");
        builder.AppendLine();

        builder.AppendLine("Scout Profiles");
        foreach (var profile in State.ScoutProfiles)
        {
            builder.AppendLine($"{profile.Name} - {profile.Role}");
            builder.AppendLine($"  Region specialty: {profile.RegionSpecialty}");
            builder.AppendLine($"  Strengths: {string.Join(", ", profile.Strengths)}");
            builder.AppendLine($"  Weaknesses: {string.Join(", ", profile.Weaknesses)}");
            builder.AppendLine($"  Reputation: {profile.Reputation}");
            builder.AppendLine($"  Relationship with GM: {profile.RelationshipWithGm}");
            builder.AppendLine($"  Current assignment: {profile.CurrentAssignment}");
            builder.AppendLine($"  Workload: {profile.Workload}");
            builder.AppendLine($"  Warning: {profile.ConflictWarning}");
            builder.AppendLine();
        }

        builder.AppendLine("Active Scouting Assignments");
        var active = State.ScenarioSnapshot.ScoutingOperations.Where(assignment => assignment.IsOpen).ToArray();
        if (active.Length == 0)
        {
            builder.AppendLine("  No active scouting assignments.");
        }

        foreach (var assignment in active.OrderBy(assignment => assignment.ExpectedReportDate))
        {
            builder.AppendLine($"{assignment.ScoutName} -> {assignment.TargetName}");
            builder.AppendLine($"  Type: {assignment.AssignmentType}  Priority: {assignment.Priority}  Status: {assignment.Status}");
            builder.AppendLine($"  Start: {assignment.StartDate:yyyy-MM-dd}  Expected report: {assignment.ExpectedReportDate:yyyy-MM-dd}");
            builder.AppendLine($"  Workload: {assignment.WorkloadAtAssignment}  Relationship: {assignment.RelationshipQualityAtAssignment}  Communication: {assignment.CommunicationQuality}");
            builder.AppendLine($"  Notes: {assignment.Notes}");
            builder.AppendLine();
        }

        builder.AppendLine("Completed Reports");
        if (State.ScenarioSnapshot.CompletedScoutingReports.Count == 0)
        {
            builder.AppendLine("  No completed reports yet.");
        }

        foreach (var report in State.ScenarioSnapshot.CompletedScoutingReports.OrderByDescending(report => report.CreatedOn).Take(12))
        {
            builder.AppendLine($"{FindPersonName(report.PlayerId)} - {report.Confidence} confidence - {report.Recommendation}");
            builder.AppendLine($"  Assignment: {report.AssignmentId}  Created: {report.CreatedOn:yyyy-MM-dd}");
            builder.AppendLine($"  Facts: {string.Join(" ", report.Facts)}");
            builder.AppendLine($"  Observation: {report.Observations.FirstOrDefault() ?? "No observation."}");
            builder.AppendLine();
        }

        builder.AppendLine("Inbox Updates");
        builder.AppendLine("Completed assignments create Scouting inbox messages and Event Engine records.");
        return builder.ToString();
    }

    private string BuildPendingActions()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Pending GM Actions");
        builder.AppendLine("==================");
        builder.AppendLine("Daily simulation can recommend actions here, but it will not sign contracts or change the roster without approval.");
        builder.AppendLine();

        if (State.ScenarioSnapshot.PendingActions.Count == 0)
        {
            builder.AppendLine("No pending GM actions.");
            return builder.ToString();
        }

        builder.AppendLine("Open Actions");
        var open = State.OpenPendingActions;
        if (open.Count == 0)
        {
            builder.AppendLine("  None.");
        }

        foreach (var action in open)
        {
            builder.AppendLine($"{action.Title}");
            builder.AppendLine($"  Person: {action.PersonName}");
            builder.AppendLine($"  Type: {action.ActionType}");
            builder.AppendLine($"  Created: {action.CreatedOn:yyyy-MM-dd}");
            builder.AppendLine($"  Reason: {action.Reason}");
            builder.AppendLine($"  Recommended action: {action.RecommendedAction}");
            builder.AppendLine($"  Consequence: {PendingActionConsequence(action)}");
            builder.AppendLine($"  Approve button: approves only this kind of pending action; Decline button makes no roster/contract change.");
            builder.AppendLine();
        }

        builder.AppendLine("Recently Resolved");
        foreach (var action in State.ScenarioSnapshot.PendingActions.Where(action => !action.IsOpen).OrderByDescending(action => action.CreatedOn).Take(8))
        {
            builder.AppendLine($"{action.Status}: {action.Title}");
            builder.AppendLine($"  {action.Reason}");
        }

        return builder.ToString();
    }

    private string BuildLeagueNews()
    {
        var builder = new StringBuilder();
        builder.AppendLine("League News / Transaction Wire");
        builder.AppendLine();
        builder.AppendLine("Filters: All | Signings | Roster Moves | Injuries | Draft | Staff");
        builder.AppendLine("Other-team transactions appear here instead of crowding the GM inbox.");
        builder.AppendLine();

        if (State.LeagueTransactions.Count == 0)
        {
            builder.AppendLine("No league transactions have been reported yet.");
            return builder.ToString();
        }

        foreach (var group in State.LeagueTransactions.GroupBy(transaction => transaction.Category).OrderBy(group => group.Key))
        {
            builder.AppendLine(group.Key.ToString());
            foreach (var transaction in group
                .OrderByDescending(transaction => transaction.Date)
                .ThenBy(transaction => transaction.TeamName, StringComparer.Ordinal)
                .ThenBy(transaction => transaction.PersonName, StringComparer.Ordinal))
            {
                builder.AppendLine($"  {transaction.Date:yyyy-MM-dd} | {transaction.TeamName} | {transaction.PersonName} | {transaction.TransactionType}");
                builder.AppendLine($"    {transaction.Description}");
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string PendingActionConsequence(PendingGmAction action) =>
        action.ActionType switch
        {
            PendingGmActionType.SignRecruit or PendingGmActionType.SignDraftPick => "Approve signing or the player remains unsigned.",
            PendingGmActionType.AddToRoster => "Resolve roster issue before the next game.",
            PendingGmActionType.ReleasePlayer or PendingGmActionType.CutPlayer => "Declining keeps the player in the current roster/camp state.",
            PendingGmActionType.AssignToAffiliate or PendingGmActionType.ReturnToParent => "Declining keeps the player in your current decision queue.",
            PendingGmActionType.ApproveContract => "Approve contract or negotiation remains unresolved.",
            PendingGmActionType.DeclineContract => "Decline contract only if you are ready to move on.",
            _ => "No automatic change will happen without GM approval."
        };

    private string BuildDraftBoard()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Draft Board");
        builder.AppendLine("===========");
        if (State.ScenarioSnapshot.DraftExperience is { } draftState)
        {
            builder.AppendLine($"Status: {draftState.Status}");
            builder.AppendLine($"Round: {draftState.CurrentRound}/{draftState.TotalRounds}");
            builder.AppendLine($"Current pick: {draftState.CurrentPick?.PickNumber.ToString() ?? "complete"}");
            builder.AppendLine($"Overall pick: {draftState.OverallPick}");
            builder.AppendLine($"Team selecting: {draftState.TeamSelecting}");
            builder.AppendLine($"Your next pick: {draftState.PlayerNextPick?.PickNumber.ToString() ?? "none"}");
            builder.AppendLine($"Countdown: {draftState.CountdownPlaceholder}");
            builder.AppendLine();
            builder.AppendLine("Recent picks:");
            foreach (var selection in draftState.Selections.OrderByDescending(item => item.PickNumber).Take(8).OrderBy(item => item.PickNumber))
            {
                builder.AppendLine($"  #{selection.PickNumber} {selection.OrganizationName}: {selection.ProspectName}");
            }
            builder.AppendLine();
            builder.AppendLine("Draft Rights / Prospect List:");
            foreach (var selection in State.ScenarioSnapshot.DraftRights)
            {
                builder.AppendLine($"  R{selection.RoundNumber} #{selection.PickNumber}: {selection.ProspectName}");
            }
            if (State.ScenarioSnapshot.DraftRights.Count == 0)
            {
                builder.AppendLine("  None yet.");
            }
            builder.AppendLine();
            if (draftState.Recap is not null)
            {
                builder.AppendLine("Draft Recap");
                builder.AppendLine($"Rounds completed: {draftState.Recap.RoundsCompleted}");
                builder.AppendLine($"Players drafted: {draftState.Recap.PlayersDrafted}");
                builder.AppendLine($"Owner reaction: {draftState.Recap.OwnerReaction}");
                builder.AppendLine($"Head scout reaction: {draftState.Recap.HeadScoutReaction}");
                builder.AppendLine("Your selections:");
                foreach (var selection in draftState.Recap.YourSelections)
                {
                    builder.AppendLine($"  R{selection.RoundNumber} P{selection.PickNumber}: {selection.ProspectName}");
                }
                builder.AppendLine();
            }
        }

        foreach (var entry in State.Snapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank))
        {
            builder.AppendLine($"#{entry.Rank} {(entry.IsStarred ? "[STAR] " : string.Empty)}{FindPersonName(entry.ProspectPersonId)} - confidence {entry.ScoutingConfidence?.ToString() ?? "Unknown"}");
            builder.AppendLine($"  Report: {entry.ScoutingReportId ?? "none"}");
            if (entry.Bio is not null)
            {
                builder.AppendLine($"  Bio: {State.PersonPosition(entry.ProspectPersonId)} | {entry.Bio.ShootsCatches} | {entry.Bio.HeightDisplay}, {entry.Bio.WeightDisplay} | age {State.PersonAge(entry.ProspectPersonId)?.ToString() ?? "unknown"} | born {entry.Bio.BirthYear}");
                builder.AppendLine($"  Hometown: {entry.Bio.Hometown}, {entry.Bio.ProvinceState}, {entry.Bio.Country} | Team: {entry.Bio.CurrentTeam} ({entry.Bio.League})");
                builder.AppendLine($"  Character: {entry.Bio.CharacterSummary}");
                builder.AppendLine($"  Lineup projection: {entry.Bio.PotentialLineupProjection}");
            }

            builder.AppendLine($"  Projection: {entry.ProjectionText}");
            builder.AppendLine($"  Analytics: {(string.IsNullOrWhiteSpace(entry.AnalyticsSummary) ? "not available" : entry.AnalyticsSummary)}");
            builder.AppendLine($"  GM Notes: {(string.IsNullOrWhiteSpace(entry.PersonalNotes) ? "none" : entry.PersonalNotes)}");
            builder.AppendLine("  View Dossier button: opens player dossier without exposing true ratings.");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private string BuildPlayerDossier()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Player Dossier");
        builder.AppendLine("==============");
        builder.AppendLine("Open dossiers from Roster, Recruits, Scouting, Draft Board, Prospect List, and Training Camp.");
        builder.AppendLine("Use View Dossier to cycle to another player and Dossier Note to add a GM note.");
        builder.AppendLine();

        var dossier = State.CurrentDossier;
        if (dossier is null)
        {
            builder.AppendLine("No player dossier is selected yet.");
            return builder.ToString();
        }

        builder.AppendLine($"{dossier.PlayerName} - age {dossier.Age} - {dossier.Position}");
        builder.AppendLine($"Status: {dossier.Status}");
        builder.AppendLine($"Team/Rights: {dossier.TeamOrRights}");
        builder.AppendLine($"Source: {dossier.Source}");
        builder.AppendLine();

        foreach (var section in dossier.Sections)
        {
            builder.AppendLine(section.Title);
            builder.AppendLine(new string('-', section.Title.Length));
            foreach (var line in section.Lines)
            {
                builder.AppendLine($"  {line}");
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private string BuildProspectList()
    {
        var builder = new StringBuilder();
        var summary = State.ProspectSummary;
        builder.AppendLine("Prospect List / Draft Rights");
        builder.AppendLine("============================");
        builder.AppendLine($"Total prospects: {summary.TotalProspects}");
        builder.AppendLine($"Rights held: {summary.RightsHeld}");
        builder.AppendLine($"Contract offered: {summary.ContractOffered}");
        builder.AppendLine($"Signed: {summary.Signed}");
        builder.AppendLine($"Invited to camp: {summary.InvitedToCamp}");
        builder.AppendLine($"Returned: {summary.Returned}");
        builder.AppendLine($"Assigned to affiliate: {summary.AssignedToAffiliate}");
        builder.AppendLine($"Released/declined: {summary.ReleasedOrDeclined}");
        builder.AppendLine();

        if (State.ScenarioSnapshot.ProspectRights.Count == 0)
        {
            builder.AppendLine("No drafted players yet. Complete the draft to populate this list.");
            return builder.ToString();
        }

        foreach (var prospect in State.ScenarioSnapshot.ProspectRights.OrderBy(item => item.PickNumber))
        {
            builder.AppendLine($"{prospect.ProspectName} - {prospect.Position} - age {prospect.Age}");
            builder.AppendLine($"  Draft: round {prospect.RoundNumber}, pick {prospect.PickNumber}");
            builder.AppendLine($"  Rights status: {prospect.Status}");
            builder.AppendLine($"  Projection: {prospect.ProjectionText}");
            builder.AppendLine($"  Confidence: {prospect.ScoutingConfidence?.ToString() ?? "Unknown"}");
            builder.AppendLine($"  GM notes: {(string.IsNullOrWhiteSpace(prospect.GmNotes) ? "none" : prospect.GmNotes)}");
            builder.AppendLine($"  Available actions: {string.Join(", ", State.AvailableProspectActions(prospect.ProspectPersonId))}");
            builder.AppendLine("  View Dossier button: opens contract/rights, scouting, staff opinions, and GM notes.");
            builder.AppendLine();
        }

        builder.AppendLine("Active roster remains separate. Prospect decisions do not add players to the active roster automatically.");
        return builder.ToString();
    }

    private string BuildTrainingCamp()
    {
        var builder = new StringBuilder();
        var calendar = State.TrainingCampCalendar;
        builder.AppendLine("Training Camp");
        builder.AppendLine("=============");
        builder.AppendLine($"Availability: {State.TrainingCampStatusText}");
        builder.AppendLine($"Camp Opens: {calendar.OpensOn:yyyy-MM-dd}");
        builder.AppendLine($"Camp Closes / Roster Deadline: {calendar.ClosesOn:yyyy-MM-dd}");
        builder.AppendLine($"Days until roster deadline: {calendar.DaysUntilRosterDeadline}");
        builder.AppendLine($"Current camp roster count: {calendar.CurrentCampRosterCount}");
        builder.AppendLine($"Required opening roster size: {calendar.RequiredOpeningRosterSize}");
        builder.AppendLine($"Players that must be cut/moved: {calendar.PlayersOverLimit}");
        builder.AppendLine(calendar.IsRosterCompliant
            ? "Roster Compliant"
            : $"WARNING: Roster over limit or invalid - {calendar.RosterValidationResult.Message}");
        builder.AppendLine();

        if (State.ScenarioSnapshot.TrainingCamp is not { } camp)
        {
            builder.AppendLine("Training camp opens automatically from the SeasonEngine calendar.");
            builder.AppendLine();
            builder.AppendLine("Expected camp work once open");
            builder.AppendLine("- Review returning players, drafted prospects, recruits, and invitees");
            builder.AppendLine("- Use staff evaluations before individual cutdown decisions");
            builder.AppendLine("- Reduce the roster before opening night");
            return builder.ToString();
        }

        builder.AppendLine($"Camp ID: {camp.CampId}");
        builder.AppendLine($"Opened: {camp.OpenedOn:yyyy-MM-dd}");
        builder.AppendLine($"Completed: {(camp.CompletedOn is null ? "No" : camp.CompletedOn.Value.ToString("yyyy-MM-dd"))}");
        builder.AppendLine($"Players invited: {camp.Players.Count}");
        builder.AppendLine($"Evaluations: {camp.Evaluations.Count}");
        builder.AppendLine($"Complete Camp availability: {(State.Snapshot.CurrentDate >= calendar.ClosesOn || calendar.IsRosterCompliant ? "Available" : "Locked until roster is compliant or deadline arrives")}");
        builder.AppendLine();

        if (camp.Summary is not null)
        {
            builder.AppendLine("Summary");
            builder.AppendLine($"  Kept: {camp.Summary.PlayersKept}");
            builder.AppendLine($"  Cut/released: {camp.Summary.PlayersCutOrReleased}");
            builder.AppendLine($"  Assigned/returned: {camp.Summary.PlayersAssignedOrReturned}");
            builder.AppendLine($"  Injury concerns: {camp.Summary.InjuryConcerns}");
            builder.AppendLine($"  Roster validation: {(camp.Summary.RosterValidationResult.IsValid ? "Valid" : "Needs attention")} - {camp.Summary.RosterValidationResult.Message}");
            builder.AppendLine($"  Staff: {camp.Summary.StaffSummary}");
            builder.AppendLine();
        }

        builder.AppendLine("Camp Roster");
        foreach (var player in camp.Players.OrderBy(player => player.Status).ThenBy(player => player.PlayerName, StringComparer.Ordinal))
        {
            builder.AppendLine($"{player.PlayerName} - {player.Position} - {player.Status}");
            builder.AppendLine($"  Invite: {player.InviteType}  Source: {player.AcquisitionSource}");
            builder.AppendLine("  View Dossier button: opens camp evaluation, medical, development, and rights context.");

            var evaluation = camp.FindEvaluation(player.PersonId);
            if (evaluation is not null)
            {
                builder.AppendLine($"  Score: {evaluation.CampScore}/100  Readiness: {evaluation.Readiness}");
                builder.AppendLine($"  Upside: {evaluation.DevelopmentUpside}");
                builder.AppendLine($"  Coach: {evaluation.CoachNote}");
                builder.AppendLine($"  Scout: {evaluation.ScoutNote}");
                builder.AppendLine($"  Risk: {evaluation.RiskNote}");
                builder.AppendLine($"  Recommendation: {evaluation.Recommendation}");
            }
            else
            {
                builder.AppendLine("  Evaluation: pending");
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private string BuildRelationships()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Relationships");
        builder.AppendLine("=============");
        foreach (var relationship in State.Snapshot.Relationships.OrderBy(item => item.RelationshipType.ToString(), StringComparer.Ordinal))
        {
            builder.AppendLine($"{relationship.RelationshipType}: {FindPersonName(relationship.FromPersonId)} -> {FindPersonName(relationship.ToPersonId)}");
            builder.AppendLine($"  Trust {relationship.Trust}, Respect {relationship.Respect}, Confidence {relationship.Confidence}, Loyalty {relationship.Loyalty}");
            builder.AppendLine($"  Influence {relationship.Influence}, Friendship {relationship.Friendship}, Rivalry {relationship.Rivalry}");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private string BuildSchedule()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Schedule");
        builder.AppendLine("========");

        var schedule = State.ScenarioSnapshot.Schedule;
        if (schedule is null)
        {
            builder.AppendLine("No regular-season schedule has been generated yet.");
            builder.AppendLine("Complete season readiness and use Begin Season to generate the first schedule.");
            return builder.ToString();
        }

        var next = State.NextGame;
        builder.AppendLine(next is null ? "Next game: none remaining" : $"Next game: {next.Date:yyyy-MM-dd} - {DescribeGame(next)}");
        builder.AppendLine();

        builder.AppendLine("Today's Games");
        builder.AppendLine("-------------");
        AppendScheduleGames(builder, State.TodaysGames, includeResult: true);

        builder.AppendLine();
        builder.AppendLine("Upcoming Games");
        builder.AppendLine("--------------");
        AppendScheduleGames(builder, State.UpcomingGames, includeResult: false);

        builder.AppendLine();
        builder.AppendLine("Recent Results");
        builder.AppendLine("--------------");
        AppendScheduleGames(builder, State.RecentResults, includeResult: true);

        builder.AppendLine();
        builder.AppendLine("Recent Game Recaps");
        builder.AppendLine("------------------");
        foreach (var recap in State.ScenarioSnapshot.GameRecaps
            .OrderByDescending(recap => recap.Date)
            .ThenByDescending(recap => recap.GameId, StringComparer.Ordinal)
            .Take(6))
        {
            builder.AppendLine($"{recap.Date:yyyy-MM-dd} | {recap.BoxScore.FinalScore}");
            builder.AppendLine($"  Winner: {recap.WinnerTeam}");
            builder.AppendLine($"  Three stars: {string.Join("; ", recap.ThreeStars)}");
            builder.AppendLine($"  {recap.NarrativeSummary}");
            if (recap.InjuryNotes.Count > 0)
            {
                builder.AppendLine($"  Medical: {string.Join(" ", recap.InjuryNotes)}");
            }

            if (recap.DevelopmentNotes.Count > 0)
            {
                builder.AppendLine($"  Development: {string.Join(" ", recap.DevelopmentNotes)}");
            }
        }

        return builder.ToString();
    }

    private string BuildStandings()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Standings");
        builder.AppendLine("=========");

        var standings = State.ScenarioSnapshot.Standings;
        if (standings is null)
        {
            builder.AppendLine("Standings will appear after the season schedule is generated.");
            return builder.ToString();
        }

        builder.AppendLine("RK Team                         GP   W   L OTL  PTS   GF   GA DIFF");
        builder.AppendLine("-------------------------------------------------------------------");
        var rank = 1;
        foreach (var team in standings.OrderedTeams())
        {
            var marker = team.OrganizationId == State.ScenarioSnapshot.Organization.OrganizationId ? "*" : " ";
            var diff = team.GoalsFor - team.GoalsAgainst;
            builder.AppendLine($"{rank,2}{marker} {team.TeamName,-28} {team.GamesPlayed,2} {team.Wins,3} {team.Losses,3} {team.OvertimeLosses,3} {team.Points,4} {team.GoalsFor,4} {team.GoalsAgainst,4} {diff,4}");
            rank++;
        }
        builder.AppendLine();
        builder.AppendLine("* Your team");

        return builder.ToString();
    }

    private string BuildStats()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Stats");
        builder.AppendLine("=====");

        if (State.ScenarioSnapshot.TeamStats.Count == 0
            && State.ScenarioSnapshot.PlayerStats.Count == 0
            && State.ScenarioSnapshot.GoalieStats.Count == 0)
        {
            builder.AppendLine("Season stats will appear after the season begins.");
            return builder.ToString();
        }

        var leaders = State.StatLeaders;

        builder.AppendLine("Team Leaders");
        builder.AppendLine("------------");
        AppendLeaders(builder, leaders.TeamLeaders);

        builder.AppendLine();
        builder.AppendLine("League Leaders");
        builder.AppendLine("--------------");
        AppendLeaders(builder, leaders.LeagueLeaders);

        builder.AppendLine();
        builder.AppendLine("Skater Leaders");
        builder.AppendLine("--------------");
        AppendLeaders(builder, leaders.SkaterLeaders);

        builder.AppendLine();
        builder.AppendLine("Goalie Leaders");
        builder.AppendLine("--------------");
        AppendLeaders(builder, leaders.GoalieLeaders);

        builder.AppendLine();
        builder.AppendLine("Team Stats");
        builder.AppendLine("----------");
        foreach (var line in State.ScenarioSnapshot.TeamStats.OrderBy(line => line.TeamName, StringComparer.Ordinal))
        {
            var standing = State.ScenarioSnapshot.Standings?.Teams.FirstOrDefault(team => team.OrganizationId == line.OrganizationId);
            var record = standing is null ? "0-0-0" : $"{standing.Wins}-{standing.Losses}-{standing.OvertimeLosses}";
            var points = standing?.Points ?? 0;
            var diff = line.GoalsFor - line.GoalsAgainst;
            builder.AppendLine($"{line.TeamName,-28} GP {line.GamesPlayed,2}  {record,7}  PTS {points,3}  GF {line.GoalsFor,3}  GA {line.GoalsAgainst,3}  DIFF {diff,3}");
        }

        builder.AppendLine();
        builder.AppendLine("Player Stats");
        builder.AppendLine("------------");
        foreach (var line in State.ScenarioSnapshot.PlayerStats
            .OrderByDescending(line => line.Points)
            .ThenByDescending(line => line.Goals)
            .ThenBy(line => line.PlayerName, StringComparer.Ordinal)
            .Take(30))
        {
            builder.AppendLine($"{line.PlayerName,-24} GP {line.GamesPlayed,2}  G {line.Goals,2}  A {line.Assists,2}  PTS {line.Points,2}  +/- {line.PlusMinus,2}  PIM {line.PenaltyMinutes,2}");
        }

        builder.AppendLine();
        builder.AppendLine("Goalie Stats");
        builder.AppendLine("------------");
        foreach (var line in State.ScenarioSnapshot.GoalieStats.OrderBy(line => line.PlayerName, StringComparer.Ordinal))
        {
            builder.AppendLine($"{line.PlayerName,-24} GP {line.GamesPlayed,2}  W {line.Wins,2}  L {line.Losses,2}  GA {line.GoalsAgainst,3}  SV {line.Saves,3}  SV% {line.SavePercentage:0.000}  GAA {line.GoalsAgainstAverage:0.00}  SO {line.Shutouts,2}");
        }

        return builder.ToString();
    }

    private string BuildMonthlySummary()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Monthly GM Summaries");
        builder.AppendLine("====================");

        if (State.ScenarioSnapshot.MonthlySummaries.Count == 0)
        {
            builder.AppendLine("No monthly summaries yet. Use Advance to Month End once the season is underway.");
            return builder.ToString();
        }

        foreach (var summary in State.ScenarioSnapshot.MonthlySummaries
            .OrderByDescending(summary => summary.Year)
            .ThenByDescending(summary => summary.Month))
        {
            builder.AppendLine($"{summary.MonthName} {summary.Year}");
            builder.AppendLine($"  Month record: {summary.TeamRecordForMonth}");
            builder.AppendLine($"  Overall record: {summary.OverallRecord}");
            builder.AppendLine($"  Standings: {summary.StandingsPosition}");
            builder.AppendLine($"  Best player: {summary.BestPlayer}");
            builder.AppendLine($"  Struggling player: {summary.StrugglingPlayer}");
            builder.AppendLine($"  Top goalie: {summary.TopGoalie}");
            builder.AppendLine($"  Injury concern: {summary.BiggestInjuryConcern}");
            builder.AppendLine($"  Owner mood: {summary.OwnerMood}");
            builder.AppendLine($"  Coach concern: {summary.CoachConcern}");
            builder.AppendLine($"  Scout update: {summary.HeadScoutUpdate}");
            builder.AppendLine($"  Development: {summary.DevelopmentUpdate}");
            builder.AppendLine($"  Roster: {summary.RosterWarning}");
            builder.AppendLine($"  Budget: {summary.BudgetStatus}");
            builder.AppendLine($"  Scouting reports: {summary.ScoutingReportsCompleted}");
            builder.AppendLine($"  Pending GM actions: {summary.PendingGmActions}");
            builder.AppendLine();
            builder.AppendLine(summary.ExecutiveNarrative);
            builder.AppendLine();
            foreach (var section in summary.Sections)
            {
                builder.AppendLine(section.Title);
                foreach (var line in section.Lines)
                {
                    builder.AppendLine($"  - {line}");
                }
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private void AppendScheduleGames(StringBuilder builder, IReadOnlyList<ScheduledGame> games, bool includeResult)
    {
        if (games.Count == 0)
        {
            builder.AppendLine("None.");
            return;
        }

        foreach (var game in games)
        {
            var homeAway = game.HomeOrganizationId == State.ScenarioSnapshot.Organization.OrganizationId
                ? "Home"
                : game.AwayOrganizationId == State.ScenarioSnapshot.Organization.OrganizationId ? "Away" : "League";
            var opponent = game.HomeOrganizationId == State.ScenarioSnapshot.Organization.OrganizationId || game.AwayOrganizationId == State.ScenarioSnapshot.Organization.OrganizationId
                ? OpponentName(game, State.ScenarioSnapshot.Organization.OrganizationId)
                : $"{TeamName(game.AwayOrganizationId)} at {TeamName(game.HomeOrganizationId)}";
            var result = includeResult && game.Result is not null
                ? $"{game.Result.HomeGoals}-{game.Result.AwayGoals}, winner {TeamName(game.Result.WinnerOrganizationId)}"
                : game.Status.ToString();
            builder.AppendLine($"{game.Date:yyyy-MM-dd} | {homeAway,-6} | {opponent,-28} | {result}");
        }
    }

    private static void AppendLeaders(StringBuilder builder, IReadOnlyList<StatLeader> leaders)
    {
        if (leaders.Count == 0)
        {
            builder.AppendLine("No leaders yet.");
            return;
        }

        foreach (var leader in leaders)
        {
            builder.AppendLine($"{leader.Category,-18} {leader.Name,-24} {leader.Value,6:0.###}  {leader.Detail}");
        }
    }

    private string DescribeGame(ScheduledGame game) =>
        $"{TeamName(game.AwayOrganizationId)} at {TeamName(game.HomeOrganizationId)}";

    private string OpponentName(ScheduledGame game, string organizationId) =>
        TeamName(game.HomeOrganizationId == organizationId ? game.AwayOrganizationId : game.HomeOrganizationId);

    private string TeamName(string organizationId)
    {
        if (organizationId == State.ScenarioSnapshot.Organization.OrganizationId)
        {
            return State.ScenarioSnapshot.Organization.Name;
        }

        var standingsName = State.ScenarioSnapshot.Standings?.Teams
            .FirstOrDefault(team => string.Equals(team.OrganizationId, organizationId, StringComparison.Ordinal))
            ?.TeamName;
        if (!string.IsNullOrWhiteSpace(standingsName))
        {
            return standingsName;
        }

        var leagueTeam = SeasonFrameworkService.LeagueTeams(State.ScenarioSnapshot)
            .FirstOrDefault(team => string.Equals(team.OrganizationId, organizationId, StringComparison.Ordinal));
        return string.IsNullOrWhiteSpace(leagueTeam.TeamName) ? organizationId : leagueTeam.TeamName;
    }

    private string BuildSeasonReadiness()
    {
        var report = State.SeasonReadinessReport;
        var roster = report.RosterReport;
        var builder = new StringBuilder();
        builder.AppendLine("Season Readiness");
        builder.AppendLine("================");
        builder.AppendLine($"Status: {(report.CanBeginSeason ? "Ready" : "Not Ready")}");
        builder.AppendLine($"Organization Health: {report.OrganizationHealth}");
        builder.AppendLine($"Roster Compliance: {report.RosterStatus} - {roster.ValidationResult.Message}");
        builder.AppendLine($"Owner Satisfaction: {report.OwnerSatisfaction}");
        builder.AppendLine($"Training Camp Status: {report.TrainingCampStatus}");
        builder.AppendLine();
        builder.AppendLine("Opening Roster");
        builder.AppendLine($"  Current active roster size: {roster.CurrentRosterSize}");
        builder.AppendLine($"  Required opening roster size: {roster.RequiredRosterSize}");
        builder.AppendLine($"  Goalies: {roster.Goalies}");
        builder.AppendLine($"  Defense: {roster.Defense}");
        builder.AppendLine($"  Forwards: {roster.Forwards}");
        builder.AppendLine($"  Prospects: {roster.Prospects}");
        builder.AppendLine($"  Unsigned players: {roster.UnsignedPlayers}");
        builder.AppendLine($"  Training camp invitees: {roster.TrainingCampInvitees}");
        builder.AppendLine($"  Players still requiring decisions: {roster.PlayersRequiringDecisions}");
        builder.AppendLine();
        builder.AppendLine("Opening Day Checklist");
        foreach (var item in report.ChecklistItems)
        {
            builder.AppendLine($"  {(item.IsComplete ? "[x]" : "[ ]")} {item.Text}");
        }

        builder.AppendLine();
        builder.AppendLine("Owner Review");
        builder.AppendLine(report.OwnerReview);
        builder.AppendLine();
        builder.AppendLine("Head Coach Summary");
        builder.AppendLine(report.HeadCoachSummary);
        builder.AppendLine();
        builder.AppendLine("Head Scout Summary");
        builder.AppendLine(report.HeadScoutSummary);
        builder.AppendLine();
        builder.AppendLine("Staff Recommendations");
        builder.AppendLine(report.StaffRecommendations);
        builder.AppendLine();
        builder.AppendLine(report.CanBeginSeason
            ? "Begin Season is available."
            : $"Begin Season blocked: {report.BlockedReason}");
        return builder.ToString();
    }

    private string BuildExecutiveReports()
    {
        var archive = State.ScenarioSnapshot.ExecutiveReports;
        var current = archive.CurrentSeason(State.ScenarioSnapshot.Season.Year);
        var previous = archive.PreviousSeasons(State.ScenarioSnapshot.Season.Year);
        var builder = new StringBuilder();
        builder.AppendLine("Executive Reports");
        builder.AppendLine("=================");
        builder.AppendLine($"Current Season: {State.ScenarioSnapshot.Season.Year}");
        builder.AppendLine($"Current Season Reports: {current.Count}");
        builder.AppendLine($"Previous Season Reports: {previous.Count}");
        builder.AppendLine();
        builder.AppendLine("Views");
        builder.AppendLine("- Current Season");
        builder.AppendLine("- Previous Seasons");
        builder.AppendLine("- Front Office Readiness");
        builder.AppendLine("- End of Season Review");
        builder.AppendLine();

        if (archive.Reports.Count == 0)
        {
            builder.AppendLine("No executive reports have been archived yet.");
            builder.AppendLine("Front Office Readiness is created when Opening Night requirements are complete. End of Season Review is created after the season is completed.");
            return builder.ToString();
        }

        foreach (var report in archive.Reports.OrderByDescending(report => report.SeasonYear).ThenBy(report => report.Kind))
        {
            builder.AppendLine($"{report.SeasonYear} - {report.Title}");
            builder.AppendLine($"  Type: {report.Kind}");
            builder.AppendLine($"  Generated: {report.GeneratedAt:yyyy-MM-dd}");
            builder.AppendLine($"  Organization: {report.OrganizationName}");
            builder.AppendLine($"  League: {report.LeagueId}");
            builder.AppendLine($"  Season: {report.SeasonId}");
            builder.AppendLine($"  GM: {report.GeneralManagerName}");
            builder.AppendLine($"  Owner: {report.OwnerName}");
            builder.AppendLine($"  Organization Health: {report.OrganizationHealthPercent}%");
            builder.AppendLine($"  Recommendation: {report.Recommendation}");
            builder.AppendLine($"  Summary: {report.ExecutiveSummary}");
            builder.AppendLine("  Sections:");
            foreach (var section in report.Sections)
            {
                builder.AppendLine($"    {section.Title}");
                foreach (var item in section.Items)
                {
                    builder.AppendLine($"      {item.Key}: {item.Value}");
                }
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private string FindPersonName(string personId)
    {
        if (string.Equals(personId, State.Snapshot.Owner.OwnerId, StringComparison.Ordinal))
        {
            return State.Snapshot.Owner.Name;
        }

        var person = State.Snapshot.People.SingleOrDefault(person => person.PersonId == personId);
        return person is null ? personId : person.Identity.DisplayName;
    }

    private string RecruitDisplayName(string personId)
    {
        var name = FindPersonName(personId);
        var sameNameCount = State.Snapshot.Recruits
            .Select(recruit => recruit.RecruitPersonId)
            .Distinct(StringComparer.Ordinal)
            .Count(recruitPersonId => string.Equals(FindPersonName(recruitPersonId), name, StringComparison.Ordinal));
        if (sameNameCount <= 1)
        {
            return name;
        }

        return $"{name} ({State.PersonPosition(personId)}, age {State.PersonAge(personId)?.ToString() ?? "unknown"}, {State.RegionTeamText(personId)})";
    }

    private string ScoutingDisplayName(string personId)
    {
        var name = FindPersonName(personId);
        var sameNameCount = State.Snapshot.DraftBoard.Entries
            .Select(entry => entry.ProspectPersonId)
            .Distinct(StringComparer.Ordinal)
            .Count(prospectId => string.Equals(FindPersonName(prospectId), name, StringComparison.Ordinal));
        if (sameNameCount <= 1)
        {
            return name;
        }

        return $"{name} ({State.PersonPosition(personId)}, age {State.PersonAge(personId)?.ToString() ?? "unknown"}, {State.RegionTeamText(personId)})";
    }
}

internal sealed record SelectablePersonRow(
    string PersonId,
    string Name,
    string Kind,
    string Primary,
    string Secondary,
    string Summary)
{
    public override string ToString() => $"{Name}\n{Primary}\n{Secondary}";
}

internal sealed class AlphaDesktopState
{
    private readonly DailySimulationCoordinator _coordinator = new();
    private readonly NewGmScenarioActions _actions = new();
    private readonly AlphaDraftExperienceService _draftExperience = new();
    private readonly TrainingCampService _trainingCamp = new();
    private readonly PendingGmActionService _pendingActions = new();
    private readonly ProspectDecisionService _prospectDecisions = new();
    private readonly SeasonReadinessService _seasonReadiness = new();
    private readonly ExecutiveReportService _executiveReports = new();
    private readonly ScoutingOperationsService _scoutingOperations = new();
    private readonly PlayerDossierService _playerDossiers = new();
    private readonly StaffOfficeService _staffOffice = new();
    private readonly BudgetOverviewService _budgetOverview = new();
    private readonly RecruitingV2Service _recruitingV2 = new();
    private readonly SeasonFrameworkService _seasonFramework = new();
    private readonly GameRecapService _gameRecaps = new();
    private readonly SeasonStatsPolishService _statsPolish = new();
    private readonly FirstMonthAdvanceService _firstMonthAdvance = new();
    private readonly EngineRegistry _registry;
    private readonly List<LeagueTransaction> _leagueTransactions = [];
    private bool _draftModalDismissed;
    private string? _selectedDossierPersonId;
    public NewGmScenarioSnapshot ScenarioSnapshot { get; private set; }

    private AlphaDesktopState(EngineRegistry registry, NewGmScenarioSnapshot scenarioSnapshot)
    {
        _registry = registry;
        ScenarioSnapshot = scenarioSnapshot;
        Snapshot = scenarioSnapshot.AlphaSnapshot;
        _selectedDossierPersonId = FirstDossierPersonId();
        InboxManager.AddRange(scenarioSnapshot.FirstDayInbox);
        LatestSummary = scenarioSnapshot.ScenarioSummary;
    }

    public AlphaWorldSnapshot Snapshot { get; private set; }

    public InboxManager InboxManager { get; } = new();

    public IReadOnlyList<InboxMessage> Inbox => InboxManager.Query(new InboxFilter());

    public IReadOnlyList<LeagueTransaction> LeagueTransactions =>
        _leagueTransactions
            .OrderByDescending(transaction => transaction.Date)
            .ThenBy(transaction => transaction.TeamName, StringComparer.Ordinal)
            .ThenBy(transaction => transaction.PersonName, StringComparer.Ordinal)
            .ToArray();

    public int UnreadInboxCount => Inbox.Count(message => message.IsUnread);

    public IReadOnlyList<PendingGmAction> OpenPendingActions =>
        ScenarioSnapshot.PendingActions
            .Where(action => action.IsOpen)
            .OrderBy(action => action.CreatedOn)
            .ThenBy(action => action.Title, StringComparer.Ordinal)
            .ToArray();

    public bool IsDraftUiEnabled => DraftUiPolicy.IsDraftUiEnabled(_registry.Rulebook);

    public bool IsDraftModalVisible =>
        IsDraftUiEnabled
        && !_draftModalDismissed
        && Snapshot.CurrentDate >= ScenarioSnapshot.DraftDate
        && ScenarioSnapshot.DraftExperience?.Status != DraftExperienceStatus.Disabled;

    public TrainingCampCalendarInfo TrainingCampCalendar => _trainingCamp.GetCalendarInfo(_registry, ScenarioSnapshot);

    public ProspectListSummary ProspectSummary => _prospectDecisions.BuildSummary(ScenarioSnapshot);

    public SeasonReadinessReport SeasonReadinessReport => _seasonReadiness.Evaluate(_registry, ScenarioSnapshot);

    public BudgetSnapshot BudgetOverview => _budgetOverview.Build(ScenarioSnapshot, _registry.Rulebook ?? RulebookPresets.CreateJuniorMajor());

    public ScheduledGame? NextGame => _seasonFramework.NextGame(ScenarioSnapshot);

    public GameRecap? LastGameRecap => _seasonFramework.LastPlayerTeamRecap(ScenarioSnapshot);

    public IReadOnlyList<ScheduledGame> RecentResults => _gameRecaps.RecentResults(ScenarioSnapshot);

    public IReadOnlyList<ScheduledGame> UpcomingGames => _gameRecaps.UpcomingGames(ScenarioSnapshot);

    public IReadOnlyList<ScheduledGame> TodaysGames => _gameRecaps.TodaysGames(ScenarioSnapshot);

    public SeasonStatLeaders StatLeaders => _statsPolish.BuildLeaders(ScenarioSnapshot);

    public string TeamRecordText
    {
        get
        {
            var standing = ScenarioSnapshot.Standings?.Teams.FirstOrDefault(team => team.OrganizationId == ScenarioSnapshot.Organization.OrganizationId);
            return standing is null ? "0-0-0" : $"{standing.Wins}-{standing.Losses}-{standing.OvertimeLosses}, {standing.Points} pts";
        }
    }

    public int UrgentPendingDecisionCount => FirstMonthAdvanceService.UrgentPendingActions(ScenarioSnapshot).Count;

    public string NextDecisionDeadlineText
    {
        get
        {
            var urgent = FirstMonthAdvanceService.UrgentPendingActions(ScenarioSnapshot).FirstOrDefault();
            if (urgent is not null)
            {
                return $"{urgent.PersonName}: {urgent.RecommendedAction}";
            }

            var open = OpenPendingActions.FirstOrDefault();
            return open is null ? "no immediate deadline" : $"{open.PersonName}: {open.RecommendedAction}";
        }
    }

    public string StandingsRankText
    {
        get
        {
            if (ScenarioSnapshot.Standings is null)
            {
                return "n/a";
            }

            var ranked = ScenarioSnapshot.Standings.OrderedTeams()
                .Select((team, index) => new { Team = team, Rank = index + 1 })
                .FirstOrDefault(item => item.Team.OrganizationId == ScenarioSnapshot.Organization.OrganizationId);
            return ranked is null ? "n/a" : $"{ranked.Rank}/{ScenarioSnapshot.Standings.Teams.Count}";
        }
    }

    public MonthlyGmSummary? LatestMonthlySummary =>
        ScenarioSnapshot.MonthlySummaries
            .OrderByDescending(summary => summary.Year)
            .ThenByDescending(summary => summary.Month)
            .FirstOrDefault();

    public string DraftBioSummary(DraftBoardEntry entry) =>
        entry.Bio is null
            ? RegionTeamText(entry.ProspectPersonId)
            : $"{entry.Bio.Hometown}, {entry.Bio.ProvinceState} | {entry.Bio.CurrentTeam} ({entry.Bio.League})";

    public string LastStopReason { get; private set; } = "No advance pause yet.";

    public int PendingDecisionCount => OpenPendingActions.Count;

    public int ScoutingReportCount => ScenarioSnapshot.CompletedScoutingReports.Count;

    public int RosterWarningCount
    {
        get
        {
            var roster = SeasonReadinessReport.RosterReport;
            var warnings = 0;
            if (!roster.ValidationResult.IsValid)
            {
                warnings++;
            }

            if (roster.CurrentRosterSize != roster.RequiredRosterSize)
            {
                warnings++;
            }

            if (roster.UnsignedPlayers > 0)
            {
                warnings++;
            }

            if (roster.PlayersRequiringDecisions > 0)
            {
                warnings++;
            }

            return warnings;
        }
    }

    public string DraftCountdownText =>
        ScenarioSnapshot.DaysUntilDraft switch
        {
            < 0 => "Draft complete",
            0 => "Draft day",
            1 => "1 day",
            var days => $"{days} days"
        };

    public string TrainingCampCountdownText
    {
        get
        {
            var calendar = TrainingCampCalendar;
            if (ScenarioSnapshot.TrainingCamp is { IsCompleted: true })
            {
                return "Complete";
            }

            if (Snapshot.CurrentDate < calendar.OpensOn)
            {
                var days = calendar.OpensOn.DayNumber - Snapshot.CurrentDate.DayNumber;
                return days == 1 ? "opens in 1 day" : $"opens in {days} days";
            }

            var deadline = Math.Max(0, calendar.ClosesOn.DayNumber - Snapshot.CurrentDate.DayNumber);
            return deadline == 0 ? "deadline today" : $"{deadline} days to deadline";
        }
    }

    public IReadOnlyList<ScoutingOperationScoutProfile> ScoutProfiles => _scoutingOperations.BuildScoutProfiles(ScenarioSnapshot);

    public IReadOnlyList<ScoutingOperationScoutProfile> AvailableScoutProfiles =>
        ScoutProfiles
            .Where(profile => ScenarioSnapshot.ScoutingOperations.All(assignment => assignment.ScoutPersonId != profile.ScoutPersonId || !assignment.IsOpen))
            .ToArray();

    public IReadOnlyList<StaffOfficeProfile> StaffProfiles => _staffOffice.BuildStaffProfiles(ScenarioSnapshot, _registry.Rulebook ?? RulebookPresets.CreateJuniorMajor());

    public IReadOnlyList<StaffVacancy> StaffVacancies => _staffOffice.BuildVacancies(ScenarioSnapshot, _registry.Rulebook ?? RulebookPresets.CreateJuniorMajor());

    public string StaffVacancySummary =>
        StaffVacancies.Count == 0
            ? "All required hockey operations positions are covered."
            : string.Join(" ", StaffVacancies.Take(3).Select(vacancy => vacancy.Warning));

    public PlayerDossierView? CurrentDossier =>
        _selectedDossierPersonId is null
            ? null
            : _playerDossiers.CreateDossier(ScenarioSnapshot, _selectedDossierPersonId);

    public string TrainingCampStatusText =>
        ScenarioSnapshot.TrainingCamp switch
        {
            { IsCompleted: true } camp => $"Completed on {camp.CompletedOn:yyyy-MM-dd}",
            { } camp => $"Open with {camp.Players.Count} player(s)",
            _ => ScenarioSnapshot.CurrentDate < TrainingCampCalendar.OpensOn
                ? $"Opens on {TrainingCampCalendar.OpensOn:yyyy-MM-dd}"
                : "Awaiting season calendar"
        };

    public string LatestSummary { get; private set; }

    public int LastProcessedEventCount { get; private set; }

    public static AlphaDesktopState Create()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        return new AlphaDesktopState(scenario.Registry, scenario.ScenarioSnapshot);
    }

    public static AlphaDesktopState Create(GmProfileCreationSettings gmSettings)
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario(new NewGmScenarioSettings
        {
            GmCreationSettings = gmSettings
        });
        return new AlphaDesktopState(scenario.Registry, scenario.ScenarioSnapshot);
    }

    public void Advance(int days)
    {
        if (days <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(days), "Advance days must be positive.");
        }

        ApplyAdvanceResult(_firstMonthAdvance.AdvanceDays(_registry, ScenarioSnapshot, days));
    }

    public void AdvanceToNextGame() =>
        ApplyAdvanceResult(_firstMonthAdvance.AdvanceToNextGame(_registry, ScenarioSnapshot));

    public void AdvanceToMonthEnd() =>
        ApplyAdvanceResult(_firstMonthAdvance.AdvanceToMonthEnd(_registry, ScenarioSnapshot));

    public void MoveDraftBoardPlayer(int direction)
    {
        var ordered = Snapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank).ToArray();
        var target = direction < 0 ? ordered.Skip(1).FirstOrDefault() : ordered.FirstOrDefault();
        if (target is null)
        {
            LatestSummary = "No draft board entry is available to move.";
            return;
        }

        ApplyAction(_actions.MoveDraftBoardPlayer(_registry, ScenarioSnapshot, target.ProspectPersonId, direction));
    }

    public void AssignScoutFocus()
    {
        var focusCycle = Enum.GetValues<DraftPreparationFocus>();
        var focus = focusCycle[ScenarioSnapshot.ScoutingAssignments.Count % focusCycle.Length];
        ApplyAction(_actions.AssignDraftPreparationFocus(_registry, ScenarioSnapshot, focus));
    }

    public void AssignScoutToRegion()
    {
        var scout = ScoutProfiles.OrderBy(profile => profile.Workload).FirstOrDefault();
        if (scout is null)
        {
            LatestSummary = "No scouting staff are available for region assignment.";
            return;
        }

        var regions = Enum.GetValues<ScoutingRegionFocus>();
        var region = regions[ScenarioSnapshot.ScoutingOperations.Count % regions.Length];
        ApplyScoutingOperationResult(_scoutingOperations.AssignScoutToRegion(
            _registry,
            ScenarioSnapshot,
            scout.ScoutPersonId,
            region,
            ScoutingOperationPriority.High,
            $"GM assignment from AlphaDesktop on {Snapshot.CurrentDate:yyyy-MM-dd}."));
    }

    public void AssignScoutToPlayer()
    {
        var scout = ScoutProfiles.OrderBy(profile => profile.Workload).FirstOrDefault();
        var prospect = Snapshot.DraftBoard.Entries
            .OrderBy(entry => entry.Rank)
            .FirstOrDefault(entry => ScenarioSnapshot.ScoutingOperations.All(assignment => assignment.TargetPlayerId != entry.ProspectPersonId || !assignment.IsOpen));
        if (scout is null || prospect is null)
        {
            LatestSummary = "No scout or draft-board prospect is available for player assignment.";
            return;
        }

        ApplyScoutingOperationResult(_scoutingOperations.AssignScoutToPlayer(
            _registry,
            ScenarioSnapshot,
            scout.ScoutPersonId,
            prospect.ProspectPersonId,
            ScoutingOperationPriority.High,
            $"Specific prospect review requested from AlphaDesktop on {Snapshot.CurrentDate:yyyy-MM-dd}."));
    }

    public void AssignScoutToRegionFor(string scoutPersonId)
    {
        var regions = Enum.GetValues<ScoutingRegionFocus>();
        var region = regions[ScenarioSnapshot.ScoutingOperations.Count % regions.Length];
        ApplyScoutingOperationResult(_scoutingOperations.AssignScoutToRegion(
            _registry,
            ScenarioSnapshot,
            scoutPersonId,
            region,
            ScoutingOperationPriority.High,
            $"GM assigned selected scout to {region} on {Snapshot.CurrentDate:yyyy-MM-dd}."));
    }

    public void AssignScoutToPlayerFor(string scoutPersonId)
    {
        var prospect = Snapshot.DraftBoard.Entries
            .OrderBy(entry => entry.Rank)
            .FirstOrDefault(entry => ScenarioSnapshot.ScoutingOperations.All(assignment => assignment.TargetPlayerId != entry.ProspectPersonId || !assignment.IsOpen));
        if (prospect is null)
        {
            LatestSummary = "No unassigned draft-board prospect is available for this scout.";
            return;
        }

        ApplyScoutingOperationResult(_scoutingOperations.AssignScoutToPlayer(
            _registry,
            ScenarioSnapshot,
            scoutPersonId,
            prospect.ProspectPersonId,
            ScoutingOperationPriority.High,
            $"Selected scout assigned to {FindPersonName(prospect.ProspectPersonId)} on {Snapshot.CurrentDate:yyyy-MM-dd}."));
    }

    public void AssignScoutToSelectedPlayer(string playerPersonId)
    {
        var scout = ScoutProfiles.OrderBy(profile => profile.Workload).FirstOrDefault();
        if (scout is null)
        {
            LatestSummary = "No scout is available for a player assignment.";
            return;
        }

        ApplyScoutingOperationResult(_scoutingOperations.AssignScoutToPlayer(
            _registry,
            ScenarioSnapshot,
            scout.ScoutPersonId,
            playerPersonId,
            ScoutingOperationPriority.High,
            $"Selected player review requested from AlphaDesktop on {Snapshot.CurrentDate:yyyy-MM-dd}."));
    }

    public bool IsScoutAvailable(string scoutPersonId) =>
        ScenarioSnapshot.ScoutingOperations.All(assignment => assignment.ScoutPersonId != scoutPersonId || !assignment.IsOpen);

    public string? NextUnassignedScoutingTargetId() =>
        Snapshot.DraftBoard.Entries
            .OrderBy(entry => entry.Rank)
            .FirstOrDefault(entry => ScenarioSnapshot.ScoutingOperations.All(assignment => assignment.TargetPlayerId != entry.ProspectPersonId || !assignment.IsOpen))
            ?.ProspectPersonId;

    public void AssignScoutToSelectedPlayerForDuration(
        string playerPersonId,
        string scoutPersonId,
        int durationDays,
        ScoutingOperationPriority priority,
        string notes)
    {
        if (!IsScoutAvailable(scoutPersonId))
        {
            LatestSummary = "Selected scout is already deployed and unavailable until the current assignment ends.";
            return;
        }

        ApplyScoutingOperationResult(_scoutingOperations.AssignScoutToPlayer(
            _registry,
            ScenarioSnapshot,
            scoutPersonId,
            playerPersonId,
            priority,
            string.IsNullOrWhiteSpace(notes) ? $"Scout {FindPersonName(playerPersonId)} for {durationDays} day(s)." : notes,
            Snapshot.CurrentDate.AddDays(Math.Max(1, durationDays))));
    }

    public void AssignScoutToRegionForDuration(
        string scoutPersonId,
        ScoutingRegionFocus region,
        int durationDays,
        ScoutingOperationPriority priority,
        string notes)
    {
        if (!IsScoutAvailable(scoutPersonId))
        {
            LatestSummary = "Selected scout is already deployed and unavailable until the current assignment ends.";
            return;
        }

        ApplyScoutingOperationResult(_scoutingOperations.AssignScoutToRegion(
            _registry,
            ScenarioSnapshot,
            scoutPersonId,
            region,
            priority,
            string.IsNullOrWhiteSpace(notes) ? $"Scout {region} for {durationDays} day(s)." : notes,
            Snapshot.CurrentDate.AddDays(Math.Max(1, durationDays))));
    }

    public void GenerateStaffConflictWarning() =>
        ApplyStaffOfficeResult(_staffOffice.GenerateChemistryWarning(_registry, ScenarioSnapshot));

    public void ReassignStaffRole()
    {
        var staff = Snapshot.StaffMembers.FirstOrDefault(member => member.CurrentRole == LegacyEngine.Staff.StaffRole.AssistantCoach);
        if (staff is null)
        {
            LatestSummary = "No eligible assistant coach is available for reassignment.";
            return;
        }

        ApplyStaffOfficeResult(_staffOffice.ReassignStaffRole(_registry, ScenarioSnapshot, staff.PersonId, LegacyEngine.Staff.StaffRole.DevelopmentCoach));
    }

    public void ReleaseStaff()
    {
        var staff = Snapshot.StaffMembers
            .Where(member => member.CurrentRole is not LegacyEngine.Staff.StaffRole.HeadCoach and not LegacyEngine.Staff.StaffRole.HeadScout)
            .OrderBy(member => member.Profile.Reputation)
            .FirstOrDefault();
        if (staff is null)
        {
            LatestSummary = "No eligible staff member is available for release.";
            return;
        }

        ApplyStaffOfficeResult(_staffOffice.ReleaseStaff(_registry, ScenarioSnapshot, staff.PersonId, "GM staff control test action."));
    }

    public void HirePlaceholderStaff()
    {
        var current = ScenarioSnapshot;
        if (current.StaffCandidates.Count == 0)
        {
            var generated = _staffOffice.GenerateCandidatePool(_registry, current);
            current = generated.ScenarioSnapshot;
            InboxManager.AddRange(generated.InboxItems);
        }

        var candidate = current.StaffCandidates
            .OrderByDescending(candidate => candidate.RoleFit + candidate.DepartmentFit + candidate.Reputation)
            .FirstOrDefault();
        if (candidate is null)
        {
            LatestSummary = "No staff candidate is available to hire.";
            return;
        }

        ApplyStaffOfficeResult(_staffOffice.HireCandidate(_registry, current, candidate.CandidateId));
    }

    public void GenerateStaffCandidates() =>
        ApplyStaffOfficeResult(_staffOffice.GenerateCandidatePool(_registry, ScenarioSnapshot));

    public void SetDevelopmentCoachFocus()
    {
        var coach = Snapshot.StaffMembers.FirstOrDefault(member => member.Department == LegacyEngine.Staff.StaffDepartment.Coaching && member.EmploymentStatus == LegacyEngine.Staff.StaffEmploymentStatus.Employed);
        if (coach is null)
        {
            LatestSummary = "No coaching staff member is available for development focus.";
            return;
        }

        ApplyStaffOfficeResult(_staffOffice.SetDevelopmentCoachFocus(_registry, ScenarioSnapshot, coach.PersonId, DevelopmentCoachFocus.Confidence));
    }

    public void SetMedicalStaffFocus()
    {
        var medical = Snapshot.StaffMembers.FirstOrDefault(member => member.Department == LegacyEngine.Staff.StaffDepartment.Medical && member.EmploymentStatus == LegacyEngine.Staff.StaffEmploymentStatus.Employed);
        if (medical is null)
        {
            LatestSummary = "No medical staff member is employed yet. Generate candidates and hire a medical candidate first.";
            return;
        }

        ApplyStaffOfficeResult(_staffOffice.SetMedicalStaffFocus(_registry, ScenarioSnapshot, medical.PersonId, MedicalStaffFocus.InjuryPrevention));
    }

    public void SetScoutingDepartmentFocus()
    {
        var scout = Snapshot.StaffMembers.FirstOrDefault(member => member.Department == LegacyEngine.Staff.StaffDepartment.Scouting && member.EmploymentStatus == LegacyEngine.Staff.StaffEmploymentStatus.Employed);
        if (scout is null)
        {
            LatestSummary = "No scouting staff member is available for scouting focus.";
            return;
        }

        ApplyStaffOfficeResult(_staffOffice.SetScoutingDepartmentFocus(_registry, ScenarioSnapshot, scout.PersonId, ScoutingDepartmentFocus.WesternCanada));
    }

    public void GenerateStaffEvaluation()
    {
        var staff = Snapshot.StaffMembers.FirstOrDefault(member => member.EmploymentStatus == LegacyEngine.Staff.StaffEmploymentStatus.Employed);
        if (staff is null)
        {
            LatestSummary = "No employed staff member is available for evaluation.";
            return;
        }

        ApplyStaffOfficeResult(_staffOffice.GenerateStaffEvaluation(_registry, ScenarioSnapshot, staff.PersonId));
    }

    public void FocusStaffProfile(string personId)
    {
        var profile = StaffProfiles.FirstOrDefault(profile => profile.PersonId == personId);
        LatestSummary = profile is null
            ? "Selected staff profile is unavailable."
            : $"{profile.Name}: {profile.CurrentRole}, {profile.Department}, {profile.Chemistry.Summary}";
    }

    public string StaffProfileText(string personId)
    {
        var profile = StaffProfiles.FirstOrDefault(profile => profile.PersonId == personId);
        if (profile is null)
        {
            return "Selected staff profile is unavailable.";
        }

        var builder = new StringBuilder();
        builder.AppendLine(profile.Name);
        builder.AppendLine($"{profile.CurrentRole} | {profile.Department}");
        builder.AppendLine();
        builder.AppendLine($"Contract: {profile.ContractStatus}");
        builder.AppendLine($"Reputation/Fit: {StaffFitSummary(personId)}");
        builder.AppendLine($"GM relationship: {profile.RelationshipWithGm}/100");
        builder.AppendLine($"Communication/loyalty: {StaffQualitySummary(personId)}");
        builder.AppendLine($"Current assignment: {profile.CurrentAssignment}");
        builder.AppendLine($"Current focus: {profile.CurrentFocus}");
        builder.AppendLine();
        builder.AppendLine($"Strengths: {string.Join(", ", profile.Strengths)}");
        builder.AppendLine($"Weaknesses: {string.Join(", ", profile.Weaknesses)}");
        builder.AppendLine();
        builder.AppendLine(profile.Chemistry.Summary);
        if (profile.Chemistry.ConflictWarnings.Count > 0)
        {
            builder.AppendLine($"Warnings: {string.Join(" ", profile.Chemistry.ConflictWarnings)}");
        }

        return builder.ToString();
    }

    public string CompareCandidateText(string personId)
    {
        var candidate = ScenarioSnapshot.StaffCandidates.FirstOrDefault(candidate => candidate.Person.PersonId == personId);
        if (candidate is null)
        {
            return "Candidate is no longer available.";
        }

        var peers = ScenarioSnapshot.StaffCandidates
            .Where(peer => peer.StaffMember.CurrentRole == candidate.StaffMember.CurrentRole)
            .OrderByDescending(peer => peer.RoleFit + peer.DepartmentFit + peer.Reputation)
            .Take(3)
            .Select(peer => $"{peer.Person.Identity.DisplayName}: fit {peer.RoleFit}/{peer.DepartmentFit}, rep {peer.Reputation}, salary {peer.ExpectedSalary.AnnualAmount:C0}")
            .ToArray();

        return $"{candidate.Person.Identity.DisplayName} - {candidate.StaffMember.CurrentRole}\n"
            + $"Expected salary: {candidate.ExpectedSalary.AnnualAmount:C0}\n"
            + $"Current employer: {candidate.CurrentEmployer}\n"
            + $"Experience: {candidate.YearsExperience} years\n"
            + $"Chemistry risk: {candidate.ChemistryRisk}\n\n"
            + $"Comparable candidates:\n{string.Join(Environment.NewLine, peers)}";
    }

    public void ReassignStaffRoleFor(string personId)
    {
        var staff = Snapshot.StaffMembers.FirstOrDefault(member => member.PersonId == personId);
        if (staff is null)
        {
            LatestSummary = "Selected staff member is unavailable for reassignment.";
            return;
        }

        if (staff.CurrentRole is LegacyEngine.Staff.StaffRole.HeadCoach or LegacyEngine.Staff.StaffRole.HeadScout)
        {
            LatestSummary = "Head coach and head scout roles are locked in this alpha pass.";
            return;
        }

        var target = staff.CurrentRole == LegacyEngine.Staff.StaffRole.DevelopmentCoach
            ? LegacyEngine.Staff.StaffRole.AssistantCoach
            : LegacyEngine.Staff.StaffRole.DevelopmentCoach;
        ApplyStaffOfficeResult(_staffOffice.ReassignStaffRole(_registry, ScenarioSnapshot, personId, target));
    }

    public void ReleaseStaffFor(string personId)
    {
        var staff = Snapshot.StaffMembers.FirstOrDefault(member => member.PersonId == personId);
        if (staff is null)
        {
            LatestSummary = "Selected staff member is unavailable for release.";
            return;
        }

        if (staff.CurrentRole is LegacyEngine.Staff.StaffRole.HeadCoach or LegacyEngine.Staff.StaffRole.HeadScout)
        {
            LatestSummary = "Head coach and head scout cannot be released in this alpha pass.";
            return;
        }

        ApplyStaffOfficeResult(_staffOffice.ReleaseStaff(_registry, ScenarioSnapshot, personId, "Released from selected staff detail panel."));
    }

    public void HireCandidateFor(string candidatePersonId)
    {
        var candidate = ScenarioSnapshot.StaffCandidates.FirstOrDefault(candidate => candidate.Person.PersonId == candidatePersonId);
        if (candidate is null)
        {
            LatestSummary = "Selected staff candidate is no longer available.";
            return;
        }

        ApplyStaffOfficeResult(_staffOffice.HireCandidate(_registry, ScenarioSnapshot, candidate.CandidateId));
    }

    public void SetStaffFocusFor(string personId)
    {
        var staff = Snapshot.StaffMembers.FirstOrDefault(member => member.PersonId == personId);
        if (staff is null)
        {
            LatestSummary = "Selected staff member is unavailable for focus assignment.";
            return;
        }

        switch (staff.Department)
        {
            case LegacyEngine.Staff.StaffDepartment.Coaching:
                ApplyStaffOfficeResult(_staffOffice.SetDevelopmentCoachFocus(_registry, ScenarioSnapshot, personId, DevelopmentCoachFocus.Confidence));
                break;
            case LegacyEngine.Staff.StaffDepartment.Medical:
                ApplyStaffOfficeResult(_staffOffice.SetMedicalStaffFocus(_registry, ScenarioSnapshot, personId, MedicalStaffFocus.InjuryPrevention));
                break;
            case LegacyEngine.Staff.StaffDepartment.Scouting:
                ApplyStaffOfficeResult(_staffOffice.SetScoutingDepartmentFocus(_registry, ScenarioSnapshot, personId, ScoutingDepartmentFocus.WesternCanada));
                break;
            default:
                LatestSummary = "No focus control is available for this staff department yet.";
                break;
        }
    }

    public void GenerateStaffEvaluationFor(string personId) =>
        ApplyStaffOfficeResult(_staffOffice.GenerateStaffEvaluation(_registry, ScenarioSnapshot, personId));

    public int? PersonAge(string personId) =>
        Snapshot.People.FirstOrDefault(person => person.PersonId == personId)?.CalculateAge(Snapshot.CurrentDate);

    public RosterPosition PersonPosition(string personId)
    {
        var rosterPosition = Snapshot.Roster.Players.FirstOrDefault(player => player.PersonId == personId)?.Position;
        if (rosterPosition is not null)
        {
            return rosterPosition.Value;
        }

        var prospectPosition = ScenarioSnapshot.ProspectRights.FirstOrDefault(prospect => prospect.ProspectPersonId == personId)?.Position;
        if (prospectPosition is not null)
        {
            return prospectPosition.Value;
        }

        var campPosition = ScenarioSnapshot.TrainingCamp?.Players.FirstOrDefault(player => player.PersonId == personId)?.Position;
        if (campPosition is not null)
        {
            return campPosition.Value;
        }

        try
        {
            return _playerDossiers.CreateDossier(ScenarioSnapshot, personId).Position;
        }
        catch (ArgumentException)
        {
            return RosterPosition.Unknown;
        }
    }

    public string PlayerType(string personId)
    {
        var position = PersonPosition(personId);
        if (InjuryStatus(personId) != "Available")
        {
            return "Injured";
        }

        if (position == RosterPosition.Goalie)
        {
            return "Goalie";
        }

        if (position == RosterPosition.Defense)
        {
            return "Defense";
        }

        var age = PersonAge(personId);
        if (age is <= 17)
        {
            return "Prospect";
        }

        if (age is >= 20)
        {
            return "Veteran";
        }

        return "Forward";
    }

    public string CurrentLineupRole(string personId)
    {
        var position = PersonPosition(personId);
        var development = Snapshot.DevelopmentProfiles.FirstOrDefault(profile => profile.PersonId == personId);
        if (position == RosterPosition.Goalie)
        {
            return development?.CurrentAbility >= 55 ? "Starter" : "Backup";
        }

        return development?.CurrentAbility switch
        {
            >= 65 => "Top Line",
            >= 52 => "Middle Six",
            >= 40 => "Depth",
            _ => "Development"
        };
    }

    public string PotentialLineupRole(string personId)
    {
        var position = PersonPosition(personId);
        var development = Snapshot.DevelopmentProfiles.FirstOrDefault(profile => profile.PersonId == personId);
        if (position == RosterPosition.Goalie)
        {
            return development?.Potential >= 60 ? "Starter" : "Backup";
        }

        return development?.Potential switch
        {
            >= 72 => "Top Line",
            >= 58 => "Middle Six",
            >= 45 => "Depth",
            _ => "Development"
        };
    }

    public string ContractRightsStatus(string personId)
    {
        var contract = ScenarioSnapshot.Contracts.Concat(Snapshot.Contracts)
            .Where(contract => contract.PersonId == personId)
            .OrderByDescending(contract => contract.SignedOn ?? contract.OfferedOn)
            .FirstOrDefault();
        if (contract is not null)
        {
            return $"{contract.ContractType} {contract.Status}";
        }

        var prospect = ScenarioSnapshot.ProspectRights.FirstOrDefault(prospect => prospect.ProspectPersonId == personId);
        return prospect is null ? "No contract/rights record" : $"Draft rights {prospect.Status}";
    }

    public string DevelopmentTrend(string personId)
    {
        var profile = Snapshot.DevelopmentProfiles.FirstOrDefault(profile => profile.PersonId == personId);
        if (profile is null)
        {
            return "No development profile";
        }

        return $"{profile.Stage}, last updated {profile.LastUpdated:yyyy-MM-dd}";
    }

    public string InjuryStatus(string personId)
    {
        var injury = Snapshot.Injuries.FirstOrDefault(injury => injury.PersonId == personId && injury.IsActive);
        return injury is null ? "Available" : $"{injury.Severity} {injury.InjuryType}, {injury.Status}";
    }

    public string RegionTeamText(string personId)
    {
        var role = Snapshot.People.FirstOrDefault(person => person.PersonId == personId)
            ?.ActiveRolesOn(Snapshot.CurrentDate)
            .FirstOrDefault();
        return role?.OrganizationId ?? Snapshot.Organization?.Name ?? Snapshot.OrganizationId;
    }

    public string AssignedScoutText(string personId)
    {
        var assignment = ScenarioSnapshot.ScoutingOperations
            .Where(assignment => assignment.TargetPlayerId == personId && assignment.IsOpen)
            .OrderBy(assignment => assignment.ExpectedReportDate)
            .FirstOrDefault();
        return assignment is null ? "Unassigned" : $"{assignment.ScoutName} until {(assignment.ReturnDate ?? assignment.ExpectedReportDate):yyyy-MM-dd}";
    }

    public string ScoutingReportStatus(string personId)
    {
        var active = ScenarioSnapshot.ScoutingOperations.FirstOrDefault(assignment => assignment.TargetPlayerId == personId && assignment.IsOpen);
        if (active is not null)
        {
            return $"{active.Status}, due {(active.ReturnDate ?? active.ExpectedReportDate):yyyy-MM-dd}";
        }

        var report = ScenarioSnapshot.CompletedScoutingReports
            .Where(report => report.PlayerId == personId)
            .OrderByDescending(report => report.CreatedOn)
            .FirstOrDefault();
        return report is null ? "No report yet" : $"Report complete {report.CreatedOn:yyyy-MM-dd}, confidence {report.Confidence}";
    }

    public int RecruitPriorityValue(string recruitPersonId, RecruitPriority priority) =>
        Snapshot.Recruits.FirstOrDefault(recruit => recruit.RecruitPersonId == recruitPersonId)
            ?.Priorities.GetValueOrDefault(priority) ?? 0;

    public RecruitingV2Profile RecruitingProfileFor(string recruitPersonId) =>
        _recruitingV2.BuildProfile(ScenarioSnapshot, recruitPersonId);

    public string RecruitPrioritySummary(string recruitPersonId, int take = 3)
    {
        var recruit = Snapshot.Recruits.FirstOrDefault(recruit => recruit.RecruitPersonId == recruitPersonId);
        if (recruit is null)
        {
            return "No priority profile";
        }

        return string.Join(", ", recruit.Priorities
            .OrderByDescending(priority => priority.Value)
            .Take(take)
            .Select(priority => $"{DisplayRecruitPriority(priority.Key)} {priority.Value}"));
    }

    public string RecruitLookingFor(string recruitPersonId)
    {
        var summary = RecruitPrioritySummary(recruitPersonId);
        var interest = Snapshot.Recruits.FirstOrDefault(recruit => recruit.RecruitPersonId == recruitPersonId)?.GetInterest(Snapshot.OrganizationId) ?? 0;
        return $"Looking for {summary}; current interest {interest}/100.";
    }

    public string RecruitFamilyPrioritySummary(string recruitPersonId)
    {
        var profile = RecruitingProfileFor(recruitPersonId);
        return string.Join(", ", profile.FamilyPriorities
            .OrderByDescending(priority => priority.Value)
            .Take(3)
            .Select(priority => $"{priority.Key} {priority.Value}"));
    }

    public string RecruitOfferState(string recruitPersonId)
    {
        var profile = RecruitingProfileFor(recruitPersonId);
        return profile.CurrentOffers.Count == 0 ? "none" : string.Join(", ", profile.CurrentOffers);
    }

    private static string DisplayRecruitPriority(RecruitPriority priority) =>
        priority switch
        {
            RecruitPriority.IceTime => "ice time",
            RecruitPriority.DistanceFromHome => "distance from home",
            RecruitPriority.PathwayToHigherHockey => "pathway",
            RecruitPriority.FamilyComfort => "family comfort",
            _ => priority.ToString()
        };

    public void MakeRecruitingOffer()
    {
        var recruit = Snapshot.Recruits.FirstOrDefault(recruit => recruit.Status is not LegacyEngine.Recruiting.RecruitStatus.Offered and not LegacyEngine.Recruiting.RecruitStatus.Committed);
        if (recruit is null)
        {
            LatestSummary = "No available recruit remains for a new offer.";
            return;
        }

        ApplyAction(_actions.MakeRecruitingOffer(_registry, ScenarioSnapshot, recruit.RecruitPersonId));
    }

    public bool CanOfferRecruit(string recruitPersonId) =>
        Snapshot.Recruits.Any(recruit =>
            recruit.RecruitPersonId == recruitPersonId
            && recruit.Status is not LegacyEngine.Recruiting.RecruitStatus.Offered
                and not LegacyEngine.Recruiting.RecruitStatus.Committed);

    public void MakeRecruitingOfferFor(string recruitPersonId)
    {
        if (!CanOfferRecruit(recruitPersonId))
        {
            LatestSummary = "Selected recruit is not available for a new offer.";
            return;
        }

        ApplyAction(_actions.MakeRecruitingOffer(_registry, ScenarioSnapshot, recruitPersonId));
    }

    public bool CanWithdrawRecruitOffer(string recruitPersonId) =>
        Snapshot.Recruits.Any(recruit => recruit.RecruitPersonId == recruitPersonId && recruit.Status == LegacyEngine.Recruiting.RecruitStatus.Offered);

    public void CallRecruitFor(string recruitPersonId) =>
        ApplyRecruitingV2(_recruitingV2.CallRecruit(_registry, ScenarioSnapshot, recruitPersonId));

    public void CallRecruitFamilyFor(string recruitPersonId) =>
        ApplyRecruitingV2(_recruitingV2.CallFamily(_registry, ScenarioSnapshot, recruitPersonId));

    public void InviteRecruitVisitFor(string recruitPersonId) =>
        ApplyRecruitingV2(_recruitingV2.InviteVisit(_registry, ScenarioSnapshot, recruitPersonId));

    public void MakeRecruitingPromiseFor(string recruitPersonId) =>
        ApplyRecruitingV2(_recruitingV2.MakePromise(_registry, ScenarioSnapshot, recruitPersonId, RecruitingPromiseType.TopSixRole));

    public void OfferRecruitEducationPackageFor(string recruitPersonId) =>
        ApplyRecruitingV2(_recruitingV2.OfferEducationPackage(_registry, ScenarioSnapshot, recruitPersonId));

    public void AskScoutForRecruitFor(string recruitPersonId) =>
        ApplyRecruitingV2(_recruitingV2.AskScoutForMoreInformation(_registry, ScenarioSnapshot, recruitPersonId));

    public void WithdrawRecruitOfferFor(string recruitPersonId)
    {
        if (!CanWithdrawRecruitOffer(recruitPersonId))
        {
            LatestSummary = "Selected recruit does not have an active offer to withdraw.";
            return;
        }

        ApplyRecruitingV2(_recruitingV2.WithdrawOffer(_registry, ScenarioSnapshot, recruitPersonId));
    }

    public void ViewNextDossier()
    {
        var ids = DossierPersonIds().ToArray();
        if (ids.Length == 0)
        {
            LatestSummary = "No player or prospect is available for a dossier.";
            _selectedDossierPersonId = null;
            return;
        }

        var currentIndex = _selectedDossierPersonId is null
            ? -1
            : Array.FindIndex(ids, id => string.Equals(id, _selectedDossierPersonId, StringComparison.Ordinal));
        _selectedDossierPersonId = ids[(currentIndex + 1 + ids.Length) % ids.Length];
        var dossier = _playerDossiers.CreateDossier(ScenarioSnapshot, _selectedDossierPersonId);
        LatestSummary = $"Opened dossier for {dossier.PlayerName}.";
    }

    public void AddDossierNote()
    {
        _selectedDossierPersonId ??= FirstDossierPersonId();
        if (_selectedDossierPersonId is null)
        {
            LatestSummary = "No player or prospect is available for a dossier note.";
            return;
        }

        var current = _playerDossiers.CreateDossier(ScenarioSnapshot, _selectedDossierPersonId);
        var note = $"GM note added on {Snapshot.CurrentDate:yyyy-MM-dd}: review development path, rights status, and staff confidence before next decision.";
        var result = _playerDossiers.AddOrUpdateGmNote(ScenarioSnapshot, current.PersonId, note);
        ScenarioSnapshot = result.ScenarioSnapshot;
        Snapshot = result.ScenarioSnapshot.AlphaSnapshot;
        LastProcessedEventCount = 0;
        LatestSummary = result.Message;
    }

    public void OpenDossier(string personId)
    {
        if (!DossierPersonIds().Contains(personId, StringComparer.Ordinal) && Snapshot.People.All(person => person.PersonId != personId))
        {
            LatestSummary = "Selected person does not have a dossier entry yet.";
            return;
        }

        _selectedDossierPersonId = personId;
        var dossier = _playerDossiers.CreateDossier(ScenarioSnapshot, personId);
        LatestSummary = $"Opened dossier for {dossier.PlayerName}.";
    }

    public void AddDossierNoteFor(string personId)
    {
        OpenDossier(personId);
        if (_selectedDossierPersonId is null)
        {
            return;
        }

        AddDossierNote();
    }

    public void SaveDossierNoteFor(string personId, string note)
    {
        try
        {
            var result = _playerDossiers.AddOrUpdateGmNote(ScenarioSnapshot, personId, string.IsNullOrWhiteSpace(note) ? "No GM note." : note);
            ScenarioSnapshot = result.ScenarioSnapshot;
            Snapshot = result.ScenarioSnapshot.AlphaSnapshot;
            LastProcessedEventCount = 0;
            _selectedDossierPersonId = personId;
            LatestSummary = result.Message;
        }
        catch (ArgumentException ex)
        {
            LatestSummary = ex.Message;
        }
    }

    public void ApprovePendingAction()
    {
        var action = OpenPendingActions.FirstOrDefault();
        if (action is null)
        {
            LatestSummary = "No pending GM action is waiting for approval.";
            return;
        }

        ApplyPendingResult(_pendingActions.Approve(_registry, ScenarioSnapshot, action.ActionId));
    }

    public void DeclinePendingAction()
    {
        var action = OpenPendingActions.FirstOrDefault();
        if (action is null)
        {
            LatestSummary = "No pending GM action is waiting for decline.";
            return;
        }

        ApplyPendingResult(_pendingActions.Decline(_registry, ScenarioSnapshot, action.ActionId));
    }

    public void StarTopProspect()
    {
        var prospect = Snapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank).FirstOrDefault();
        if (prospect is null)
        {
            LatestSummary = "No draft board prospect is available to star.";
            return;
        }

        ApplyAction(_draftExperience.StarProspect(_registry, ScenarioSnapshot, prospect.ProspectPersonId, !prospect.IsStarred));
    }

    public void AddDraftNote()
    {
        var prospect = Snapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank).FirstOrDefault();
        if (prospect is null)
        {
            LatestSummary = "No draft board prospect is available for notes.";
            return;
        }

        var note = $"GM note added on {Snapshot.CurrentDate:yyyy-MM-dd}: priority review before draft day.";
        ApplyAction(_draftExperience.UpdatePersonalNotes(_registry, ScenarioSnapshot, prospect.ProspectPersonId, note));
    }

    public IReadOnlyList<ProspectDecisionType> AvailableProspectActions(string prospectPersonId)
    {
        try
        {
            return _prospectDecisions.AvailableDecisions(_registry, ScenarioSnapshot, prospectPersonId);
        }
        catch (ArgumentException)
        {
            return Array.Empty<ProspectDecisionType>();
        }
    }

    public void OfferProspectContract() =>
        ApplyProspectDecisionToNext(ProspectDecisionType.OfferContract, "No prospect is available for a contract offer.");

    public void InviteProspectToCamp() =>
        ApplyProspectDecisionToNext(ProspectDecisionType.InviteToCamp, "No prospect is available for a camp invite.");

    public void ReturnProspectToJuniorOrYouth()
    {
        var prospect = NextActionableProspect(ProspectDecisionType.ReturnToJunior)
            ?? NextActionableProspect(ProspectDecisionType.ReturnToYouthTeam);
        if (prospect is null)
        {
            LatestSummary = "No prospect is available for a junior/youth return.";
            return;
        }

        var decisionType = AvailableProspectActions(prospect.ProspectPersonId).Contains(ProspectDecisionType.ReturnToJunior)
            ? ProspectDecisionType.ReturnToJunior
            : ProspectDecisionType.ReturnToYouthTeam;
        ApplyProspectDecision(prospect, decisionType);
    }

    public void AssignProspectToAffiliate() =>
        ApplyProspectDecisionToNext(ProspectDecisionType.AssignToAffiliate, "No prospect is available for affiliate assignment.");

    public void ReleaseProspectRights() =>
        ApplyProspectDecisionToNext(ProspectDecisionType.ReleaseRights, "No prospect rights are available for release.");

    public void MoveDraftBoardPlayer(string prospectPersonId, int direction)
    {
        if (Snapshot.DraftBoard.Entries.All(entry => entry.ProspectPersonId != prospectPersonId))
        {
            LatestSummary = "Selected prospect is not on the draft board.";
            return;
        }

        ApplyAction(_actions.MoveDraftBoardPlayer(_registry, ScenarioSnapshot, prospectPersonId, direction));
    }

    public void ToggleStarProspect(string prospectPersonId)
    {
        var prospect = Snapshot.DraftBoard.Entries.FirstOrDefault(entry => entry.ProspectPersonId == prospectPersonId);
        if (prospect is null)
        {
            LatestSummary = "Selected prospect is not on the draft board.";
            return;
        }

        ApplyAction(_draftExperience.StarProspect(_registry, ScenarioSnapshot, prospectPersonId, !prospect.IsStarred));
    }

    public void AddDraftNoteFor(string prospectPersonId)
    {
        if (Snapshot.DraftBoard.Entries.All(entry => entry.ProspectPersonId != prospectPersonId))
        {
            LatestSummary = "Selected prospect is not on the draft board.";
            return;
        }

        var note = $"GM note added on {Snapshot.CurrentDate:yyyy-MM-dd}: selected prospect review.";
        ApplyAction(_draftExperience.UpdatePersonalNotes(_registry, ScenarioSnapshot, prospectPersonId, note));
    }

    public void OfferProspectContractFor(string prospectPersonId) =>
        ApplyProspectDecisionFor(prospectPersonId, ProspectDecisionType.OfferContract, "Selected prospect is not available for a contract offer.");

    public void InviteProspectToCampFor(string prospectPersonId) =>
        ApplyProspectDecisionFor(prospectPersonId, ProspectDecisionType.InviteToCamp, "Selected prospect is not available for a camp invite.");

    public void ReturnProspectToJuniorOrYouthFor(string prospectPersonId)
    {
        var available = AvailableProspectActions(prospectPersonId);
        var decision = available.Contains(ProspectDecisionType.ReturnToJunior)
            ? ProspectDecisionType.ReturnToJunior
            : available.Contains(ProspectDecisionType.ReturnToYouthTeam)
                ? ProspectDecisionType.ReturnToYouthTeam
                : (ProspectDecisionType?)null;
        if (decision is null)
        {
            LatestSummary = "Selected prospect is not available for junior/youth return.";
            return;
        }

        ApplyProspectDecisionFor(prospectPersonId, decision.Value, "Selected prospect is not available for junior/youth return.");
    }

    public void AssignProspectToAffiliateFor(string prospectPersonId) =>
        ApplyProspectDecisionFor(prospectPersonId, ProspectDecisionType.AssignToAffiliate, "Selected prospect is not available for affiliate assignment.");

    public void ReleaseProspectRightsFor(string prospectPersonId) =>
        ApplyProspectDecisionFor(prospectPersonId, ProspectDecisionType.ReleaseRights, "Selected prospect rights are not available for release.");

    public void StartDraft()
    {
        if (!IsDraftUiEnabled)
        {
            LatestSummary = "Draft features are disabled by the active league rulebook.";
            return;
        }

        if (Snapshot.CurrentDate < ScenarioSnapshot.DraftDate)
        {
            LatestSummary = $"Draft day has not arrived. {ScenarioSnapshot.DaysUntilDraft} day(s) remain.";
            return;
        }

        if (ScenarioSnapshot.DraftExperience is { Status: not DraftExperienceStatus.NotStarted })
        {
            LatestSummary = $"Draft is already {ScenarioSnapshot.DraftExperience.Status}.";
            return;
        }

        ApplyDraftResult(_draftExperience.StartDraftDay(_registry, ScenarioSnapshot));
    }

    public void StartLiveDraft()
    {
        if (!IsDraftUiEnabled)
        {
            LatestSummary = "Draft features are disabled by the active league rulebook.";
            return;
        }

        if (Snapshot.CurrentDate < ScenarioSnapshot.DraftDate)
        {
            LatestSummary = $"Draft day has not arrived. {ScenarioSnapshot.DaysUntilDraft} day(s) remain.";
            return;
        }

        ApplyDraftResult(_draftExperience.StartLiveDraft(_registry, ScenarioSnapshot));
    }

    public void RunAiDrafting()
    {
        if (!EnsureDraftStarted())
        {
            return;
        }

        ApplyDraftResult(_draftExperience.RunAiPicksUntilPlayerTurn(_registry, ScenarioSnapshot));
    }

    public void DraftTopProspect()
    {
        if (!EnsureDraftStarted())
        {
            return;
        }

        if (ScenarioSnapshot.DraftExperience?.IsPlayerTurn != true)
        {
            LatestSummary = "AI teams are still on the clock. Run AI picks until your turn.";
            return;
        }

        var prospect = Snapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank).FirstOrDefault();
        if (prospect is null)
        {
            LatestSummary = "No available prospect remains on the draft board.";
            return;
        }

        ApplyDraftResult(_draftExperience.MakePlayerSelection(_registry, ScenarioSnapshot, prospect.ProspectPersonId));
    }

    public void DraftSelectedProspect(string prospectPersonId)
    {
        if (ScenarioSnapshot.DraftExperience?.IsPlayerTurn != true)
        {
            LatestSummary = "The draft is paused only when your team is on the clock.";
            return;
        }

        ApplyDraftResult(_draftExperience.MakePlayerSelectionAndContinue(_registry, ScenarioSnapshot, prospectPersonId));
    }

    public void EndLiveDraftModal()
    {
        if (ScenarioSnapshot.DraftExperience?.Status != DraftExperienceStatus.Completed)
        {
            LatestSummary = "End Draft is only available after the draft is complete.";
            return;
        }

        _draftModalDismissed = true;
        LatestSummary = "Draft complete. Returning to the dashboard with recap and pending GM decisions available.";
    }

    public void KeepTrainingCampPlayer()
    {
        var player = NextActionableCampPlayer();
        if (player is null)
        {
            LatestSummary = "No camp player is available for a keep decision.";
            return;
        }

        ApplyCampDecision(_trainingCamp.ApplyDecision(
            _registry,
            ScenarioSnapshot,
            new TrainingCampDecision(player.PersonId, TrainingCampDecisionType.Keep, Snapshot.CurrentDate)));
    }

    public void CutTrainingCampPlayer()
    {
        var player = NextActionableCampPlayer();
        if (player is null)
        {
            LatestSummary = "No camp player is available for a cut decision.";
            return;
        }

        ApplyCampDecision(_trainingCamp.ApplyDecision(
            _registry,
            ScenarioSnapshot,
            new TrainingCampDecision(player.PersonId, TrainingCampDecisionType.Cut, Snapshot.CurrentDate)));
    }

    public void ReleaseTrainingCampPlayer() => ApplyCampDecisionToNext(TrainingCampDecisionType.Release, "No camp player is available for release.");

    public void ReturnTrainingCampPlayerToJunior() => ApplyCampDecisionToNext(TrainingCampDecisionType.ReturnToJuniorTeam, "No camp player is available to return to junior/youth team.");

    public void AssignOrReturnTrainingCampPlayer()
    {
        var camp = ScenarioSnapshot.TrainingCamp;
        if (camp is null)
        {
            LatestSummary = "Training camp is not open yet.";
            return;
        }

        var parentPlayer = camp.Players.FirstOrDefault(player =>
            player.Status is TrainingCampStatus.Invited or TrainingCampStatus.InCamp
            && player.InviteType is TrainingCampInviteType.AssignedFromParentClub
                or TrainingCampInviteType.LoanedFromParentClub
                or TrainingCampInviteType.TwoWayContract);
        var decision = parentPlayer is not null
            ? new TrainingCampDecision(parentPlayer.PersonId, TrainingCampDecisionType.ReturnToParent, Snapshot.CurrentDate)
            : new TrainingCampDecision(
                camp.Players.FirstOrDefault(player => player.Status is TrainingCampStatus.Invited or TrainingCampStatus.InCamp)?.PersonId ?? string.Empty,
                TrainingCampDecisionType.AssignToAffiliate,
                Snapshot.CurrentDate);

        if (string.IsNullOrWhiteSpace(decision.PersonId))
        {
            LatestSummary = "No camp player is available for assignment or return.";
            return;
        }

        ApplyCampDecision(_trainingCamp.ApplyDecision(_registry, ScenarioSnapshot, decision));
    }

    public void PlaceTrainingCampPlayerOnWaivers() => ApplyCampDecisionToNext(TrainingCampDecisionType.PlaceOnWaivers, "No camp player is available for waivers.");

    public void MarkTrainingCampPlayerInjured() => ApplyCampDecisionToNext(TrainingCampDecisionType.MarkInjured, "No camp player is available to mark injured.");

    public bool CanApplyCampDecision(string personId) =>
        ScenarioSnapshot.TrainingCamp?.Players.Any(player =>
            player.PersonId == personId
            && player.Status is TrainingCampStatus.Invited or TrainingCampStatus.InCamp) == true;

    public bool CanCompleteTrainingCamp
    {
        get
        {
            if (ScenarioSnapshot.TrainingCamp is null)
            {
                return false;
            }

            var calendar = TrainingCampCalendar;
            return Snapshot.CurrentDate >= calendar.ClosesOn || calendar.IsRosterCompliant;
        }
    }

    public void ApplyCampDecisionFor(string personId, TrainingCampDecisionType decisionType)
    {
        if (!CanApplyCampDecision(personId))
        {
            LatestSummary = "Selected camp player is not available for this decision.";
            return;
        }

        ApplyCampDecision(_trainingCamp.ApplyDecision(
            _registry,
            ScenarioSnapshot,
            new TrainingCampDecision(personId, decisionType, Snapshot.CurrentDate)));
    }

    public void AssignOrReturnTrainingCampPlayerFor(string personId)
    {
        var player = ScenarioSnapshot.TrainingCamp?.Players.FirstOrDefault(player => player.PersonId == personId);
        if (player is null || !CanApplyCampDecision(personId))
        {
            LatestSummary = "Selected camp player is not available for assignment or return.";
            return;
        }

        var decisionType = player.InviteType is TrainingCampInviteType.AssignedFromParentClub
                or TrainingCampInviteType.LoanedFromParentClub
                or TrainingCampInviteType.TwoWayContract
            ? TrainingCampDecisionType.ReturnToParent
            : TrainingCampDecisionType.AssignToAffiliate;

        ApplyCampDecision(_trainingCamp.ApplyDecision(
            _registry,
            ScenarioSnapshot,
            new TrainingCampDecision(personId, decisionType, Snapshot.CurrentDate)));
    }

    public void CompleteTrainingCamp()
    {
        if (ScenarioSnapshot.TrainingCamp is null)
        {
            LatestSummary = "Training camp has not opened on the season calendar yet.";
            return;
        }

        var calendar = TrainingCampCalendar;
        if (Snapshot.CurrentDate < calendar.ClosesOn && !calendar.IsRosterCompliant)
        {
            LatestSummary = "Complete Camp is locked until the roster is compliant or the roster deadline is reached.";
            return;
        }

        ApplyCampResult(_trainingCamp.CompleteCamp(_registry, ScenarioSnapshot));
    }

    public void GenerateSeasonReadinessReviews() =>
        ApplySeasonReadinessResult(_seasonReadiness.GenerateReviews(_registry, ScenarioSnapshot));

    public void BeginSeason() =>
        ApplySeasonReadinessResult(_seasonReadiness.BeginSeason(_registry, ScenarioSnapshot));

    public void GenerateFrontOfficeReadinessReport() =>
        ApplyExecutiveReportResult(_executiveReports.GenerateFrontOfficeReadinessReport(_registry, ScenarioSnapshot));

    public void GenerateEndOfSeasonExecutiveReview() =>
        ApplyExecutiveReportResult(_executiveReports.GenerateEndOfSeasonExecutiveReview(_registry, ScenarioSnapshot));

    public int RelationshipWithGm(string personId) =>
        Snapshot.Relationships
            .Where(relationship => relationship.FromPersonId == Snapshot.GeneralManager.PersonId && relationship.ToPersonId == personId)
            .Select(relationship => (relationship.Trust + relationship.Respect + relationship.Confidence + relationship.Loyalty) / 4)
            .DefaultIfEmpty(50)
            .First();

    public string StaffFitSummary(string personId)
    {
        var member = Snapshot.StaffMembers.SingleOrDefault(member => member.PersonId == personId);
        if (member is null)
        {
            return "not tracked";
        }

        var departmentFit = member.Department switch
        {
            LegacyEngine.Staff.StaffDepartment.Scouting => member.Attributes.ScoutingAttributes.Count == 0 ? "thin scouting fit" : "scouting fit",
            LegacyEngine.Staff.StaffDepartment.Coaching => member.Attributes.CoachingAttributes.Count == 0 ? "thin coaching fit" : "coaching fit",
            LegacyEngine.Staff.StaffDepartment.Medical => member.Attributes.MedicalAttributes.Count == 0 ? "thin medical fit" : "medical fit",
            _ => "operations fit"
        };
        return $"{departmentFit}, reputation {member.Profile.Reputation}, department {member.Department}";
    }

    public string StaffQualitySummary(string personId)
    {
        var relation = RelationshipWithGm(personId);
        var loyalty = relation >= 60 ? "loyal" : relation >= 45 ? "neutral" : "strained";
        var communication = relation >= 60 ? "clear" : relation >= 45 ? "uneven" : "poor";
        var professionalism = relation >= 40 ? "professional" : "needs attention";
        return $"{communication} communication, {loyalty} loyalty, {professionalism}";
    }

    private void ApplyCampDecisionToNext(TrainingCampDecisionType decisionType, string emptyMessage)
    {
        var player = NextActionableCampPlayer();
        if (player is null)
        {
            LatestSummary = emptyMessage;
            return;
        }

        ApplyCampDecision(_trainingCamp.ApplyDecision(
            _registry,
            ScenarioSnapshot,
            new TrainingCampDecision(player.PersonId, decisionType, Snapshot.CurrentDate)));
    }

    private void ApplyAction(GmActionResult result)
    {
        ScenarioSnapshot = result.ScenarioSnapshot;
        Snapshot = result.AlphaSnapshot;
        EnsureSelectedDossierStillExists();
        InboxManager.AddRange(result.InboxItems);
        LastProcessedEventCount = 0;
        LatestSummary = result.Summary;
    }

    private void ApplyAdvanceResult(FirstMonthAdvanceResult result)
    {
        ScenarioSnapshot = result.ScenarioSnapshot;
        Snapshot = result.ScenarioSnapshot.AlphaSnapshot;
        EnsureSelectedDossierStillExists();
        InboxManager.AddRange(result.InboxItems);
        AddLeagueTransactions(result.LeagueTransactions);
        LastProcessedEventCount = result.ProcessedEventCount;
        LastStopReason = result.StopReason;
        LatestSummary = result.MonthlySummary is not null
            ? $"{result.StopReason} {result.MonthlySummary.ExecutiveNarrative}"
            : result.DaysAdvanced == 0
                ? result.StopReason
                : $"{result.StopReason} Advanced {result.DaysAdvanced} day(s), processed {result.ProcessedEventCount} event(s), and created {result.InboxItems.Count} inbox item(s).";
    }

    private void ApplyRecruitingV2(RecruitingV2Result result)
    {
        ScenarioSnapshot = result.ScenarioSnapshot;
        Snapshot = result.ScenarioSnapshot.AlphaSnapshot;
        EnsureSelectedDossierStillExists();
        InboxManager.AddRange(result.InboxItems);
        LastProcessedEventCount = 0;
        LatestSummary = result.Message;
    }

    private void ApplyDraftResult(DraftExperienceResult result)
    {
        ScenarioSnapshot = result.ScenarioSnapshot;
        Snapshot = result.ScenarioSnapshot.AlphaSnapshot;
        EnsureSelectedDossierStillExists();
        InboxManager.AddRange(result.InboxItems);
        LastProcessedEventCount = 0;
        LatestSummary = result.Summary;
    }

    private void ApplyProspectDecisionToNext(ProspectDecisionType decisionType, string emptyMessage)
    {
        var prospect = NextActionableProspect(decisionType);
        if (prospect is null)
        {
            LatestSummary = emptyMessage;
            return;
        }

        ApplyProspectDecision(prospect, decisionType);
    }

    private void ApplyProspectDecisionFor(string prospectPersonId, ProspectDecisionType decisionType, string emptyMessage)
    {
        var prospect = ScenarioSnapshot.ProspectRights.FirstOrDefault(prospect =>
            prospect.ProspectPersonId == prospectPersonId
            && AvailableProspectActions(prospect.ProspectPersonId).Contains(decisionType));
        if (prospect is null)
        {
            LatestSummary = emptyMessage;
            return;
        }

        ApplyProspectDecision(prospect, decisionType);
    }

    private void ApplyProspectDecision(DraftRightsRecord prospect, ProspectDecisionType decisionType)
    {
        var result = _prospectDecisions.ApplyDecision(
            _registry,
            ScenarioSnapshot,
            new ProspectDecision(prospect.ProspectPersonId, decisionType, Snapshot.CurrentDate));
        ApplyProspectResult(result);
    }

    private void ApplyProspectResult(ProspectDecisionResult result)
    {
        if (result.Success)
        {
            ScenarioSnapshot = result.ScenarioSnapshot;
            Snapshot = result.ScenarioSnapshot.AlphaSnapshot;
            EnsureSelectedDossierStillExists();
            InboxManager.AddRange(result.InboxItems);
        }

        LastProcessedEventCount = 0;
        LatestSummary = result.Message;
    }

    private void ApplyCampResult(TrainingCampResult result)
    {
        ScenarioSnapshot = result.ScenarioSnapshot;
        Snapshot = result.ScenarioSnapshot.AlphaSnapshot;
        EnsureSelectedDossierStillExists();
        InboxManager.AddRange(result.InboxItems);
        LastProcessedEventCount = 0;
        LatestSummary = result.Summary;
    }

    private void ApplyCampDecision(TrainingCampDecisionResult result)
    {
        if (result.Success)
        {
            ScenarioSnapshot = result.ScenarioSnapshot;
            Snapshot = result.ScenarioSnapshot.AlphaSnapshot;
            EnsureSelectedDossierStillExists();
            InboxManager.AddRange(result.InboxItems);
        }

        LastProcessedEventCount = 0;
        LatestSummary = result.Message;
    }

    private void ApplyPendingResult(PendingGmActionResult result)
    {
        if (result.Success)
        {
            ScenarioSnapshot = result.ScenarioSnapshot;
            Snapshot = result.ScenarioSnapshot.AlphaSnapshot;
            EnsureSelectedDossierStillExists();
            InboxManager.AddRange(result.InboxItems);
        }

        LastProcessedEventCount = 0;
        LatestSummary = result.Message;
    }

    private void ApplySeasonReadinessResult(SeasonReadinessResult result)
    {
        ScenarioSnapshot = result.ScenarioSnapshot;
        Snapshot = result.ScenarioSnapshot.AlphaSnapshot;
        EnsureSelectedDossierStillExists();
        InboxManager.AddRange(result.InboxItems);
        LastProcessedEventCount = 0;
        LatestSummary = result.Message;
    }

    private void ApplyExecutiveReportResult(ExecutiveReportGenerationResult result)
    {
        if (result.Success)
        {
            ScenarioSnapshot = result.ScenarioSnapshot;
            Snapshot = result.ScenarioSnapshot.AlphaSnapshot;
            EnsureSelectedDossierStillExists();
            InboxManager.AddRange(result.InboxItems);
        }

        LastProcessedEventCount = 0;
        LatestSummary = result.Message;
    }

    private void ApplyScoutingOperationResult(ScoutingOperationResult result)
    {
        if (result.Success)
        {
            ScenarioSnapshot = result.ScenarioSnapshot;
            Snapshot = result.ScenarioSnapshot.AlphaSnapshot;
            EnsureSelectedDossierStillExists();
            InboxManager.AddRange(result.InboxItems);
        }

        LastProcessedEventCount = 0;
        LatestSummary = result.Message;
    }

    private void ApplyStaffOfficeResult(StaffOfficeResult result)
    {
        if (result.Success)
        {
            ScenarioSnapshot = result.ScenarioSnapshot;
            Snapshot = result.ScenarioSnapshot.AlphaSnapshot;
            EnsureSelectedDossierStillExists();
            InboxManager.AddRange(result.InboxItems);
        }

        LastProcessedEventCount = 0;
        LatestSummary = result.Message;
    }

    private TrainingCampPlayer? NextActionableCampPlayer() =>
        ScenarioSnapshot.TrainingCamp?.Players
            .Where(player => player.Status is TrainingCampStatus.Invited or TrainingCampStatus.InCamp)
            .OrderByDescending(player => ScenarioSnapshot.TrainingCamp!.FindEvaluation(player.PersonId)?.CampScore ?? 0)
            .ThenBy(player => player.PlayerName, StringComparer.Ordinal)
            .FirstOrDefault();

    private DraftRightsRecord? NextActionableProspect(ProspectDecisionType decisionType) =>
        ScenarioSnapshot.ProspectRights
            .Where(prospect => AvailableProspectActions(prospect.ProspectPersonId).Contains(decisionType))
            .OrderBy(prospect => prospect.PickNumber)
            .FirstOrDefault();

    private bool EnsureDraftStarted()
    {
        if (ScenarioSnapshot.DraftExperience is not null)
        {
            return true;
        }

        StartDraft();
        return ScenarioSnapshot.DraftExperience is not null;
    }

    private string FindPersonName(string personId)
    {
        if (string.Equals(personId, Snapshot.Owner.OwnerId, StringComparison.Ordinal))
        {
            return Snapshot.Owner.Name;
        }

        var person = Snapshot.People.FirstOrDefault(person => person.PersonId == personId)
            ?? ScenarioSnapshot.StaffCandidates.Select(candidate => candidate.Person).FirstOrDefault(person => person.PersonId == personId);
        return person?.Identity.DisplayName ?? personId;
    }

    private string? FirstDossierPersonId() => DossierPersonIds().FirstOrDefault();

    private IReadOnlyList<string> DossierPersonIds() =>
        Snapshot.Roster.Players.Select(player => player.PersonId)
            .Concat(Snapshot.StaffMembers.Select(member => member.PersonId))
            .Concat(Snapshot.Recruits.Select(recruit => recruit.RecruitPersonId))
            .Concat(Snapshot.DraftBoard.Entries.Select(entry => entry.ProspectPersonId))
            .Concat(ScenarioSnapshot.ProspectRights.Select(prospect => prospect.ProspectPersonId))
            .Concat(ScenarioSnapshot.TrainingCamp?.Players.Select(player => player.PersonId) ?? Array.Empty<string>())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private void EnsureSelectedDossierStillExists()
    {
        var ids = DossierPersonIds();
        if (_selectedDossierPersonId is null || !ids.Contains(_selectedDossierPersonId, StringComparer.Ordinal))
        {
            _selectedDossierPersonId = ids.FirstOrDefault();
        }
    }

    public void ManageLatestInboxMessage(InboxMessageAction action)
    {
        var latest = Inbox.FirstOrDefault();
        if (latest is null)
        {
            LatestSummary = "No visible inbox message is available for that action.";
            return;
        }

        InboxManager.ApplyAction(latest.InboxItemId, action);
        LatestSummary = $"{action} applied to: {latest.Item.Title}.";
    }

    public void ManageInboxMessage(string inboxItemId, InboxMessageAction action)
    {
        var updated = InboxManager.ApplyAction(inboxItemId, action);
        LatestSummary = $"{action} applied to: {updated.Item.Title}.";
    }

    private void AddLeagueTransactions(IEnumerable<LeagueTransaction> transactions)
    {
        foreach (var transaction in transactions)
        {
            transaction.Validate();
            var existingIndex = _leagueTransactions.FindIndex(existing => existing.TransactionId == transaction.TransactionId);
            if (existingIndex >= 0)
            {
                _leagueTransactions[existingIndex] = transaction;
            }
            else
            {
                _leagueTransactions.Add(transaction);
            }
        }
    }
}
