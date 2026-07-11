using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using LegacyEngine.Contracts;
using LegacyEngine.Draft;
using LegacyEngine.Integration;
using LegacyEngine.People;
using LegacyEngine.Recruiting;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;
using LegacyEngine.Scouting;
using LegacyEngine.Staff;
using Microsoft.Win32;

namespace AlphaDesktop;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Contains("--smoke-test", StringComparer.OrdinalIgnoreCase))
        {
            var state = AlphaDesktopState.Create();
            Console.WriteLine($"AlphaDesktop smoke test: Hockey GM Legacy Alpha 8.4 {state.Snapshot.CurrentDate:yyyy-MM-dd} {state.ScenarioSnapshot.LeagueProfile.Identity.ShortName} draft in {state.ScenarioSnapshot.DaysUntilDraft} days");
            return;
        }

        var app = new Application();
        app.Run(new MainWindow());
    }
}

internal sealed class MainWindow : Window
{
    private sealed record WorkspaceScreen(string Label, UIElement Content);
    private sealed record NavigationSnapshot(string Workspace, string Screen, string? SelectedPersonId);

    private AlphaDesktopState? _state;
    private readonly TextBlock _dateText = new();
    private readonly TextBlock _summaryText = new();
    private readonly TextBlock _processedText = new();
    private readonly TextBlock _feedbackText = new();
    private readonly Dictionary<string, TextBox> _tabs = [];
    private readonly Dictionary<string, TabItem> _tabItems = [];
    private readonly Dictionary<string, ListBox> _workspaceNavigations = [];
    private readonly Dictionary<string, TextBlock> _workspaceBreadcrumbs = [];
    private readonly Dictionary<string, ListBox> _selectableLists = [];
    private readonly Dictionary<string, StackPanel> _selectableDetails = [];
    private readonly Dictionary<string, string> _selectedPeopleByTab = [];
    private readonly Stack<NavigationSnapshot> _backStack = new();
    private readonly Stack<NavigationSnapshot> _forwardStack = new();
    private readonly Dictionary<string, int> _localUxCounters = [];
    private ListBox? _commandCenterSourceList;
    private ListBox? _commandCenterViewList;
    private ListBox? _commandCenterPlayerList;
    private StackPanel? _commandCenterCenterPanel;
    private StackPanel? _commandCenterPlayerCard;
    private TextBox? _commandCenterSearchInput;
    private ComboBox? _commandCenterPositionFilter;
    private string _commandCenterSource = "NHL Roster";
    private string _commandCenterView = "Roster Overview";
    private string? _selectedCommandCenterPersonId;
    private ListBox? _organizationCommandDepartmentList;
    private ListBox? _organizationCommandStaffList;
    private StackPanel? _organizationCommandCenterPanel;
    private StackPanel? _organizationCommandStaffCard;
    private string _organizationCommandDepartment = "Owner";
    private string? _selectedOrganizationStaffPersonId;
    private TabControl? _mainTabs;
    private StackPanel? _dashboardPanel;
    private TextBox? _rosterSearchInput;
    private ComboBox? _rosterPositionFilter;
    private ComboBox? _rosterStatusFilter;
    private ComboBox? _rosterPlayerTypeFilter;
    private ComboBox? _rosterRoleFilter;
    private ComboBox? _rosterAgeFilter;
    private TextBox? _globalSearchInput;
    private StackPanel? _inboxCategoryPanel;
    private StackPanel? _inboxListPanel;
    private Border? _inboxReader;
    private CheckBox? _unreadOnlyFilter;
    private CheckBox? _pinnedOnlyFilter;
    private CheckBox? _importantOnlyFilter;
    private ComboBox? _sortOrderFilter;
    private ListBox? _actionCenterList;
    private StackPanel? _actionCenterDetail;
    private ComboBox? _actionCategoryFilter;
    private ComboBox? _actionPriorityFilter;
    private ComboBox? _actionStatusFilter;
    private InboxCategory _selectedInboxCategory = InboxCategory.All;
    private string? _selectedInboxItemId;
    private string? _selectedActionCenterItemId;
    private readonly TextBox _firstNameInput = new() { Text = "Jordan" };
    private readonly TextBox _lastNameInput = new() { Text = "Hayes" };
    private readonly TextBox _preferredNameInput = new() { Text = "Jordan" };
    private readonly TextBox _ageInput = new() { Text = "39" };
    private readonly TextBox _nationalityInput = new() { Text = "Canada" };
    private readonly TextBox _birthplaceInput = new() { Text = "Swift Current, SK" };
    private readonly TextBox _strengthsInput = new() { Text = "development planning, communication" };
    private readonly TextBox _weaknessesInput = new() { Text = "limited draft history" };
    private readonly MultiLeagueCareerService _careerSetup = new();
    private readonly ComboBox _leagueInput = new() { ItemsSource = Enum.GetValues<LeagueExperience>(), SelectedItem = LeagueExperience.Junior };
    private readonly ComboBox _teamInput = new() { DisplayMemberPath = nameof(TeamSelectionOption.TeamName) };
    private readonly TextBox _teamSearchInput = new() { MinWidth = 160 };
    private readonly ComboBox _teamDivisionFilterInput = new() { MinWidth = 160 };
    private readonly ComboBox _teamSortInput = new() { ItemsSource = new[] { "Team name", "Difficulty", "Budget", "Roster quality", "Prospect strength" }, SelectedItem = "Team name", MinWidth = 160 };
    private readonly TextBlock _leagueSummaryText = new();
    private readonly ComboBox _genderInput = new() { ItemsSource = Enum.GetValues<Gender>(), SelectedItem = Gender.NonBinary };
    private readonly ComboBox _backgroundInput = new() { ItemsSource = Enum.GetValues<GmBackground>(), SelectedItem = GmBackground.Operations };
    private readonly ComboBox _styleInput = new() { ItemsSource = Enum.GetValues<GmStyle>(), SelectedItem = GmStyle.Balanced };
    private Border? _draftModalOverlay;
    private ListBox? _tradeYourAssetsList;
    private ListBox? _tradeOtherAssetsList;
    private ListBox? _tradeYouGiveList;
    private ListBox? _tradeYouReceiveList;
    private Grid? _draftWarRoomPanel;
    private Grid? _tradeCenterPanel;
    private string _draftWarRoomBoard = "My Board";
    private string? _selectedDraftWarRoomProspectId;
    private string? _selectedTradeCenterOrganizationId;
    private bool _layoutReady;
    private bool _isRefreshing;
    private bool _isRestoringNavigation;
    private string _displayDensity = "Comfortable";
    private IInputElement? _lastPopupFocus;

    public MainWindow()
    {
        Title = "Hockey GM Legacy - Alpha Desktop";
        Width = 1180;
        Height = 780;
        MinWidth = 920;
        MinHeight = 620;
        Background = new SolidColorBrush(Color.FromRgb(245, 247, 250));
        KeyDown += HandleGlobalKeyDown;

        Content = BuildCreationLayout();
    }

    private void HandleGlobalKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S)
        {
            e.Handled = true;
            if (_state is not null)
            {
                SaveCareer();
                SetFeedback("Career saved with Ctrl+S.");
            }

            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F)
        {
            e.Handled = true;
            _globalSearchInput?.Focus();
            SetFeedback("Search focused. Type a player, team, role, status, or message.");
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Alt && e.SystemKey == Key.Left)
        {
            e.Handled = true;
            NavigateBack();
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Alt && e.SystemKey == Key.Right)
        {
            e.Handled = true;
            NavigateForward();
        }
    }

    private UIElement BuildCreationLayout()
    {
        var root = new Grid { Margin = new Thickness(28) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var title = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
        title.Children.Add(new TextBlock
        {
            Text = "Start New Career",
            FontSize = 28,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(20, 40, 64))
        });
        title.Children.Add(new TextBlock
        {
            Text = "Choose a league, choose a team, create your GM, then enter the GM Office.",
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromRgb(65, 78, 92)),
            Margin = new Thickness(0, 6, 0, 0)
        });
        Grid.SetRow(title, 0);
        root.Children.Add(title);

        var selection = BuildLeagueSelectionPanel();
        Grid.SetRow(selection, 1);
        root.Children.Add(selection);

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

        var buttons = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 24, 0, 0)
        };
        buttons.Children.Add(CreateButton("Start Career", StartCareer));
        buttons.Children.Add(CreateButton("Load Career", LoadCareerFromStartup));
        Grid.SetRow(buttons, 5);
        Grid.SetColumn(buttons, 0);
        Grid.SetColumnSpan(buttons, 3);
        form.Children.Add(buttons);

        Grid.SetRow(form, 2);
        root.Children.Add(form);
        RefreshTeamChoices();
        return root;
    }

    private UIElement BuildLeagueSelectionPanel()
    {
        _leagueInput.SelectionChanged += (_, _) => RefreshTeamChoices();
        _teamInput.SelectionChanged += (_, _) => RefreshLeagueSummary();
        _teamSearchInput.TextChanged += (_, _) => RefreshTeamChoices(preserveSelection: true);
        _teamDivisionFilterInput.SelectionChanged += (_, _) => RefreshTeamChoices(preserveSelection: true);
        _teamSortInput.SelectionChanged += (_, _) => RefreshTeamChoices(preserveSelection: true);
        var panel = new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(210, 219, 229)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 16)
        };
        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.Children.Add(LabeledControl("League", _leagueInput));
        var teamField = LabeledControl("Team", _teamInput);
        Grid.SetColumn(teamField, 1);
        content.Children.Add(teamField);
        var searchField = LabeledControl("Search", _teamSearchInput);
        Grid.SetColumn(searchField, 2);
        content.Children.Add(searchField);
        var divisionField = LabeledControl("League / Division", _teamDivisionFilterInput);
        Grid.SetColumn(divisionField, 3);
        content.Children.Add(divisionField);
        var sortField = LabeledControl("Sort", _teamSortInput);
        Grid.SetColumn(sortField, 4);
        content.Children.Add(sortField);
        _leagueSummaryText.TextWrapping = TextWrapping.Wrap;
        _leagueSummaryText.Foreground = new SolidColorBrush(Color.FromRgb(44, 58, 74));
        _leagueSummaryText.Margin = new Thickness(16, 20, 0, 0);
        Grid.SetColumn(_leagueSummaryText, 5);
        content.Children.Add(_leagueSummaryText);
        panel.Child = content;
        return panel;
    }

    private void RefreshTeamChoices(bool preserveSelection = false)
    {
        var experience = SelectedLeagueExperience();
        var prior = preserveSelection ? SelectedTeamOption()?.OrganizationId : null;
        var allTeams = _careerSetup.TeamsFor(experience);
        var divisionOptions = new[] { "All" }
            .Concat(allTeams.Select(team => team.DisplayLeagueName).Distinct(StringComparer.Ordinal))
            .Concat(allTeams.Select(team => team.DisplayDivisionConference).Distinct(StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var selectedDivision = _teamDivisionFilterInput.SelectedItem?.ToString();
        _teamDivisionFilterInput.ItemsSource = divisionOptions;
        _teamDivisionFilterInput.SelectedItem = selectedDivision is not null && divisionOptions.Contains(selectedDivision, StringComparer.Ordinal)
            ? selectedDivision
            : "All";

        var search = _teamSearchInput.Text.Trim();
        var filter = _teamDivisionFilterInput.SelectedItem?.ToString() ?? "All";
        var teams = allTeams
            .Where(team => string.IsNullOrWhiteSpace(search)
                || team.TeamName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || team.City.Contains(search, StringComparison.OrdinalIgnoreCase)
                || team.Region.Contains(search, StringComparison.OrdinalIgnoreCase))
            .Where(team => filter == "All"
                || string.Equals(team.DisplayLeagueName, filter, StringComparison.Ordinal)
                || string.Equals(team.DisplayDivisionConference, filter, StringComparison.Ordinal))
            .ToArray();

        teams = (_teamSortInput.SelectedItem?.ToString() ?? "Team name") switch
        {
            "Difficulty" => teams.OrderBy(team => DifficultyRank(team.Difficulty)).ThenBy(team => team.TeamName, StringComparer.Ordinal).ToArray(),
            "Budget" => teams.OrderByDescending(team => team.Budget).ThenBy(team => team.TeamName, StringComparer.Ordinal).ToArray(),
            "Roster quality" => teams.OrderBy(team => QualityRank(team.RosterQuality)).ThenBy(team => team.TeamName, StringComparer.Ordinal).ToArray(),
            "Prospect strength" => teams.OrderBy(team => QualityRank(team.ProspectStrength)).ThenBy(team => team.TeamName, StringComparer.Ordinal).ToArray(),
            _ => teams.OrderBy(team => team.TeamName, StringComparer.Ordinal).ToArray()
        };

        _teamInput.ItemsSource = teams;
        _teamInput.SelectedItem = teams.FirstOrDefault(team => string.Equals(team.OrganizationId, prior, StringComparison.Ordinal))
            ?? teams.FirstOrDefault();
        RefreshLeagueSummary();
    }

    private void RefreshLeagueSummary()
    {
        var profile = _careerSetup.GetProfile(SelectedLeagueExperience());
        var team = SelectedTeamOption();
        var branding = team is null ? null : new UiBrandingService().BuildRegistry(profile).TeamProfiles[team.OrganizationId];
        _leagueSummaryText.Text = team is null
            ? $"{profile.Identity.Name}: {profile.Identity.Description}"
            : $"{profile.Identity.Name} | {profile.Identity.Difficulty} | {profile.Teams.Count} teams\nFocus: {string.Join(", ", profile.Identity.PrimaryGameplayFocus)}\n{branding!.TeamAbbreviation} {team.TeamName}: monogram {branding.Monogram.Letters}, {branding.VisualStyleDescriptor}, {team.DisplayLeagueName}, {team.DisplayDivisionConference}, {team.PreviousRecord}, budget {team.Budget:C0}, roster {team.RosterQuality}, prospects {team.ProspectStrength}, staff {team.DisplayStaffQuality}, difficulty {team.Difficulty}.";
    }

    private static int DifficultyRank(string difficulty) =>
        difficulty switch
        {
            "Approachable" => 0,
            "Medium" => 1,
            "Hard" => 2,
            "Demanding" => 3,
            _ => 4
        };

    private static int QualityRank(string quality) =>
        quality switch
        {
            "Excellent" or "Contender" or "High-end" or "Experienced" => 0,
            "Strong" or "Balanced" => 1,
            "Average" or "Developing" or "Development-focused" => 2,
            "Emerging" or "Raw" or "Needs support" => 3,
            "Thin" => 4,
            _ => 5
        };

    private LeagueExperience SelectedLeagueExperience() =>
        _leagueInput.SelectedItem is LeagueExperience experience ? experience : LeagueExperience.Junior;

    private TeamSelectionOption? SelectedTeamOption() =>
        _teamInput.SelectedItem as TeamSelectionOption;

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

        var team = SelectedTeamOption();
        if (team is null)
        {
            MessageBox.Show("Please choose a team.", "New Career", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _state = AlphaDesktopState.Create(settings, SelectedLeagueExperience(), team.OrganizationId);
        Content = BuildLayout();
        RefreshAll();
    }

    private void LoadCareerFromStartup()
    {
        var path = PromptForLoadPath();
        if (path is null)
        {
            return;
        }

        var result = AlphaDesktopState.LoadCareer(path, out var loaded);
        if (!result.Success || loaded is null)
        {
            MessageBox.Show(result.Message, "Load Career", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _state = loaded;
        Content = BuildLayout();
        RefreshAll();
        MessageBox.Show(result.Message, "Load Career", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SaveCareer()
    {
        var result = State.SaveCareer();
        if (!result.Success)
        {
            MessageBox.Show(result.Message, "Save Career", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        MessageBox.Show($"{result.Message}\n{result.FilePath}", "Save Career", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SaveCareerAs()
    {
        var path = PromptForSavePath();
        if (path is null)
        {
            return;
        }

        var result = State.SaveCareer(path);
        if (!result.Success)
        {
            MessageBox.Show(result.Message, "Save Career", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        MessageBox.Show($"{result.Message}\n{result.FilePath}", "Save Career", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void LoadCareer()
    {
        var path = PromptForLoadPath();
        if (path is null)
        {
            return;
        }

        var result = AlphaDesktopState.LoadCareer(path, out var loaded);
        if (!result.Success || loaded is null)
        {
            MessageBox.Show(result.Message, "Load Career", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _state = loaded;
        Content = BuildLayout();
        RefreshAll();
        MessageBox.Show(result.Message, "Load Career", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static string? PromptForSavePath()
    {
        var service = new SaveGameService();
        Directory.CreateDirectory(service.DefaultSaveFolder);
        var dialog = new SaveFileDialog
        {
            Title = "Save Career",
            InitialDirectory = service.DefaultSaveFolder,
            Filter = "Hockey GM Save (*.json)|*.json",
            DefaultExt = ".json",
            AddExtension = true,
            FileName = "hockey-gm-career.json"
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static string? PromptForLoadPath()
    {
        var service = new SaveGameService();
        Directory.CreateDirectory(service.DefaultSaveFolder);
        var dialog = new OpenFileDialog
        {
            Title = "Load Career",
            InitialDirectory = service.DefaultSaveFolder,
            Filter = "Hockey GM Save (*.json)|*.json",
            DefaultExt = ".json",
            CheckFileExists = true
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static IReadOnlyList<string> SplitList(string text) =>
        text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => item.Length > 0)
            .ToArray();

    private UIElement BuildLayout()
    {
        _layoutReady = false;
        var root = new Grid();
        var app = new DockPanel();

        var header = BuildHeader();
        DockPanel.SetDock(header, Dock.Top);
        app.Children.Add(header);

        _tabs.Clear();
        _tabItems.Clear();
        _workspaceNavigations.Clear();
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
            new WorkspaceScreen("Action Center / Pending Decisions", BuildActionCenterLayout())
        });

        AddWorkspaceTab(tabs, "Inbox", new[]
        {
            new WorkspaceScreen("GM Inbox", BuildInboxLayout()),
            new WorkspaceScreen("League News / Transaction Wire", CreateTextScreen("League News"))
        });

        AddWorkspaceTab(tabs, "League", new[]
        {
            new WorkspaceScreen("League Overview", CreateTextScreen("League Overview")),
            new WorkspaceScreen("League Rules", CreateTextScreen("League Rules")),
            new WorkspaceScreen("Teams", CreateSelectablePeopleContent("Teams")),
            new WorkspaceScreen("Transactions", CreateTextScreen("Transactions")),
            new WorkspaceScreen("Waiver Wire", CreateTextScreen("Waiver Wire")),
            new WorkspaceScreen("League Free Agents", CreateSelectablePeopleContent("League Free Agents")),
            new WorkspaceScreen("League Draft", CreateTextScreen("League Draft")),
            new WorkspaceScreen("Position Market", CreateTextScreen("Position Market")),
            new WorkspaceScreen("League Trade Block", CreateSelectablePeopleContent("League Trade Block")),
            new WorkspaceScreen("League Standings", CreateTextScreen("League Standings"))
        });

        AddWorkspaceTab(tabs, "Organization", new[]
        {
            new WorkspaceScreen("Command Center", CreateOrganizationCommandCenter()),
            new WorkspaceScreen("Owner", CreateTextScreen("Owner")),
            new WorkspaceScreen("Staff", CreateSelectablePeopleContent("Staff")),
            new WorkspaceScreen("Staff Hiring", CreateSelectablePeopleContent("Staff Hiring")),
            new WorkspaceScreen("Vacancies", CreateSelectablePeopleContent("Vacancies")),
            new WorkspaceScreen("Budget", CreateTextScreen("Budget")),
            new WorkspaceScreen("Planning", CreateTextScreen("Organization Planning")),
            new WorkspaceScreen("Organization Health", CreateTextScreen("Organization Health")),
            new WorkspaceScreen("Relationships", CreateTextScreen("Relationships"))
        });

        var hockeyOperations = new List<WorkspaceScreen>
        {
            new("Command Center", CreateHockeyOperationsCommandCenter()),
            new("Roster", CreateSelectablePeopleContent("Roster")),
            new("Lineup", CreateSelectablePeopleContent("Lineup")),
            new("Tactics", CreateSelectablePeopleContent("Tactics")),
            new("Prospects", CreateSelectablePeopleContent("Prospect List")),
            new("Recruits", CreateSelectablePeopleContent("Recruits")),
            new("Free Agents", CreateSelectablePeopleContent("Free Agents")),
            new("Contracts", CreateTextScreen("Contracts")),
            new("Contract Rights", CreateTextScreen("Contract Rights")),
            new("Arbitration", CreateTextScreen("Arbitration")),
            new("Buyouts", CreateTextScreen("Buyouts")),
            new("Offer Sheets", CreateTextScreen("Offer Sheets")),
            new("Waivers", CreateTextScreen("Hockey Waivers")),
            new("Scouting", CreateSelectablePeopleContent("Scouting")),
            new("Scouting Operations", CreateSelectablePeopleContent("Scouting Operations")),
            new("Trades", CreateTradeCenterWorkspace()),
            new("Training Camp", CreateSelectablePeopleContent("Training Camp"))
        };
        if (State.IsDraftUiEnabled)
        {
            hockeyOperations.Insert(5, new WorkspaceScreen("Draft Board", CreateSelectablePeopleContent("Draft Board")));
            hockeyOperations.Insert(5, new WorkspaceScreen("Draft War Room", CreateDraftWarRoomWorkspace()));
        }
        AddWorkspaceTab(tabs, "Hockey Operations", hockeyOperations);

        AddWorkspaceTab(tabs, "Season", new[]
        {
            new WorkspaceScreen("Schedule", CreateTextScreen("Schedule")),
            new WorkspaceScreen("Standings", CreateTextScreen("Standings")),
            new WorkspaceScreen("Playoffs", CreateTextScreen("Playoffs")),
            new WorkspaceScreen("Stats", CreateTextScreen("Stats")),
            new WorkspaceScreen("Monthly Summary", CreateTextScreen("Monthly Summary")),
            new WorkspaceScreen("Season Archive", CreateTextScreen("Season Archive")),
            new WorkspaceScreen("Season Readiness", CreateTextScreen("Season Readiness"))
        });

        AddWorkspaceTab(tabs, "Reports / History", new[]
        {
            new WorkspaceScreen("Executive Reports", CreateTextScreen("Executive Reports")),
            new WorkspaceScreen("Organization Planning", CreateTextScreen("Organization Planning Report")),
            new WorkspaceScreen("Archived Seasons", CreateTextScreen("Archived Seasons")),
            new WorkspaceScreen("GM Career", CreateTextScreen("GM Career")),
            new WorkspaceScreen("Organization History", CreateTextScreen("Organization History")),
            new WorkspaceScreen("Draft History", CreateTextScreen("Draft History")),
            new WorkspaceScreen("Drafted Players", CreateTextScreen("Drafted Players")),
            new WorkspaceScreen("Where Are They Now", CreateTextScreen("Where Are They Now")),
            new WorkspaceScreen("Player Career Timelines", CreateTextScreen("Player Career Timelines")),
            new WorkspaceScreen("Career Milestones", CreateTextScreen("Career Milestones")),
            new WorkspaceScreen("Player Stories", CreateTextScreen("Player Stories")),
            new WorkspaceScreen("Media / News", CreateTextScreen("Media / News")),
            new WorkspaceScreen("Awards", CreateTextScreen("Awards")),
            new WorkspaceScreen("Record Book", CreateTextScreen("Record Book")),
            new WorkspaceScreen("Team Records", CreateTextScreen("Team Records")),
            new WorkspaceScreen("League Records", CreateTextScreen("League Records")),
            new WorkspaceScreen("Staff History", CreateTextScreen("Staff History")),
            new WorkspaceScreen("Staff Careers", CreateTextScreen("Staff Careers")),
            new WorkspaceScreen("Coaching Trees", CreateTextScreen("Coaching Trees")),
            new WorkspaceScreen("Scout History", CreateTextScreen("Scout History")),
            new WorkspaceScreen("Development Staff History", CreateTextScreen("Development Staff History")),
            new WorkspaceScreen("Owner History", CreateTextScreen("Owner History")),
            new WorkspaceScreen("Owner Letters", CreateTextScreen("Owner Letters")),
            new WorkspaceScreen("Job Security History", CreateTextScreen("Job Security History")),
            new WorkspaceScreen("Expectation Results", CreateTextScreen("Expectation Results")),
            new WorkspaceScreen("Transaction History", CreateTextScreen("Transaction History")),
            new WorkspaceScreen("Playoff Archive", CreateTextScreen("Playoff Archive")),
            new WorkspaceScreen("Champions", CreateTextScreen("Champions")),
            new WorkspaceScreen("Draft Recaps", CreateTextScreen("Draft Recaps")),
            new WorkspaceScreen("Monthly Summaries", CreateTextScreen("Monthly Summaries")),
            new WorkspaceScreen("Career History", CreateTextScreen("Career History")),
            new WorkspaceScreen("Journal", CreateTextScreen("Journal")),
            new WorkspaceScreen("Global Search", CreateTextScreen("Global Search")),
            new WorkspaceScreen("Playtest Checklist", CreateTextScreen("Playtest Checklist"))
        });

        AddWorkspaceTab(tabs, "Settings placeholder", new[]
        {
            new WorkspaceScreen("Settings", CreateTextScreen("Settings"))
        });

        app.Children.Add(tabs);
        root.Children.Add(app);
        root.Children.Add(BuildDraftModalOverlay());
        tabs.SelectionChanged += (_, args) =>
        {
            if (ReferenceEquals(args.OriginalSource, tabs))
            {
                RefreshAfterAction();
            }
        };
        _layoutReady = true;
        return root;
    }

    private UIElement BuildHeader()
    {
        var teamBrand = CurrentTeamBranding();
        var leagueBrand = CurrentLeagueBranding();
        var header = new Border
        {
            Background = UiPresentation.BrushFromHex(teamBrand.Palette.DarkBackgroundTint),
            Padding = new Thickness(16, 12, 16, 12)
        };

        var panel = new StackPanel();
        var brandHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 10), LastChildFill = true };
        var crest = UiPresentation.UiTeamCrest(teamBrand, 56);
        crest.Margin = new Thickness(0, 0, 14, 0);
        DockPanel.SetDock(crest, Dock.Left);
        brandHeader.Children.Add(crest);

        var textPanel = new StackPanel();
        textPanel.Children.Add(new TextBlock
        {
            Text = $"Hockey GM Legacy - Alpha 8.4 - GM Office | {teamBrand.OrganizationDisplayName}",
            Foreground = Brushes.White,
            FontSize = 22,
            FontWeight = FontWeights.SemiBold
        });
        textPanel.Children.Add(new TextBlock
        {
            Text = $"{teamBrand.TeamAbbreviation} | {leagueBrand.ShortName} | {teamBrand.ConferenceDivision} | {teamBrand.VisualStyleDescriptor} identity | {teamBrand.ArenaName}",
            Foreground = new SolidColorBrush(Color.FromRgb(210, 225, 240)),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 3, 0, 0)
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

        _feedbackText.Foreground = new SolidColorBrush(Color.FromRgb(236, 245, 255));
        _feedbackText.FontWeight = FontWeights.SemiBold;
        _feedbackText.TextWrapping = TextWrapping.Wrap;
        _feedbackText.Margin = new Thickness(0, 4, 0, 0);
        _feedbackText.ToolTip = "Last action feedback. Use Ctrl+S to save, Ctrl+F to search, Alt+Left/Alt+Right for navigation history.";
        textPanel.Children.Add(_feedbackText);

        brandHeader.Children.Add(textPanel);
        panel.Children.Add(brandHeader);

        var searchPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            MaxWidth = 560,
            Margin = new Thickness(0, 0, 0, 10)
        };
        searchPanel.Children.Add(new TextBlock
        {
            Text = "Quick search placeholder - now active across players, staff, prospects, messages, and history",
            Foreground = new SolidColorBrush(Color.FromRgb(210, 225, 240)),
            Margin = new Thickness(0, 0, 0, 3)
        });
        _globalSearchInput = new TextBox
        {
            Text = string.Empty,
            MinHeight = 30,
            MaxWidth = 520,
            Background = new SolidColorBrush(Color.FromRgb(234, 241, 248)),
            Foreground = new SolidColorBrush(Color.FromRgb(78, 92, 108))
        };
        _globalSearchInput.TextChanged += (_, _) =>
        {
            if (_tabs.ContainsKey("Global Search"))
            {
                _tabs["Global Search"].Text = BuildGlobalSearch();
            }
        };
        var globalSearchRow = new DockPanel { LastChildFill = true };
        var clearSearch = CreateButton("Clear Search", () =>
        {
            _globalSearchInput.Text = string.Empty;
            SetFeedback("Search cleared.");
        });
        clearSearch.MinWidth = 100;
        DockPanel.SetDock(clearSearch, Dock.Right);
        globalSearchRow.Children.Add(clearSearch);
        globalSearchRow.Children.Add(_globalSearchInput);
        searchPanel.Children.Add(globalSearchRow);
        panel.Children.Add(searchPanel);

        var densityPanel = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 8)
        };
        densityPanel.Children.Add(new TextBlock
        {
            Text = "Display density",
            Foreground = new SolidColorBrush(Color.FromRgb(210, 225, 240)),
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 5, 8, 0)
        });
        var densityInput = new ComboBox
        {
            ItemsSource = new[] { "Comfortable", "Compact" },
            SelectedItem = _displayDensity,
            MinWidth = 130,
            MinHeight = 28,
            ToolTip = "Comfortable uses larger rows. Compact reduces padding so more rows are visible."
        };
        densityInput.SelectionChanged += (_, _) =>
        {
            _displayDensity = densityInput.SelectedItem?.ToString() ?? "Comfortable";
            SetFeedback($"Display density changed to {_displayDensity}.");
            RefreshAfterAction();
        };
        densityPanel.Children.Add(densityInput);
        panel.Children.Add(densityPanel);

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
        buttonPanel.Children.Add(CreateButton("Finish Season", FinishSeason));
        buttonPanel.Children.Add(CreateButton("Save Career", SaveCareer));
        buttonPanel.Children.Add(CreateButton("Save As", SaveCareerAs));
        buttonPanel.Children.Add(CreateButton("Load Career", LoadCareer));

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
            Focusable = true,
            MinWidth = 92,
            Padding = new Thickness(10, 7, 10, 7),
            Margin = new Thickness(0, 0, 8, 8),
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Left,
            ToolTip = text
        };

        button.Click += (_, _) =>
        {
            IncrementUxCounter($"button:{text}");
            action();
            SetFeedback($"{text} complete.");
            RefreshAfterAction();
        };

        return button;
    }

    private static T? FindAncestor<T>(DependencyObject? source)
        where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match)
            {
                return match;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
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

    private UIElement CreateDraftWarRoomWorkspace()
    {
        _draftWarRoomPanel = new Grid { Background = Brushes.White, Margin = new Thickness(12) };
        RefreshDraftWarRoomWorkspace();
        return new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = Brushes.White,
            Content = _draftWarRoomPanel
        };
    }

    private UIElement CreateTradeCenterWorkspace()
    {
        _tradeCenterPanel = new Grid { Background = Brushes.White, Margin = new Thickness(12) };
        RefreshTradeCenterWorkspace();
        return new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = Brushes.White,
            Content = _tradeCenterPanel
        };
    }

    private void RefreshDraftWarRoomWorkspace()
    {
        if (_draftWarRoomPanel is null || _state is null)
        {
            return;
        }

        _draftWarRoomPanel.Children.Clear();
        _draftWarRoomPanel.RowDefinitions.Clear();
        _draftWarRoomPanel.ColumnDefinitions.Clear();
        _draftWarRoomPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _draftWarRoomPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _draftWarRoomPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
        _draftWarRoomPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _draftWarRoomPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(430) });

        var rows = BuildDraftWarRoomVisualRows();
        var selected = rows.FirstOrDefault(row => row.PersonId == _selectedDraftWarRoomProspectId) ?? rows.FirstOrDefault();
        _selectedDraftWarRoomProspectId = selected?.PersonId;

        var left = BuildDraftWarRoomLeftPanel();
        var center = BuildDraftWarRoomCenterPanel(rows, selected);
        var right = BuildDraftWarRoomProspectPanel(selected?.PersonId);
        var bottom = BuildDraftWarRoomBottomStrip();

        Grid.SetColumn(left, 0);
        Grid.SetColumn(center, 1);
        Grid.SetColumn(right, 2);
        Grid.SetRow(bottom, 1);
        Grid.SetColumnSpan(bottom, 3);
        _draftWarRoomPanel.Children.Add(left);
        _draftWarRoomPanel.Children.Add(center);
        _draftWarRoomPanel.Children.Add(right);
        _draftWarRoomPanel.Children.Add(bottom);
    }

    private IReadOnlyList<SelectablePersonRow> BuildDraftWarRoomVisualRows()
    {
        var board = State.DraftWarRoom;
        IEnumerable<DraftWarRoomEntry> entries = board.BoardEntries.Where(entry => !entry.IsRemoved);
        entries = _draftWarRoomBoard switch
        {
            "Watch List" => entries.Where(entry => entry.Tags.Count > 0 || entry.IsFavorite || entry.IsPinned),
            "Sleepers" => entries.Where(entry => entry.Tags.Contains(DraftWatchTag.Sleeper)),
            "Avoid List" => entries.Where(entry => entry.Tags.Contains(DraftWatchTag.Avoid)),
            "Medical Concerns" => entries.Where(entry => entry.Tags.Contains(DraftWatchTag.MedicalConcern)),
            "Character Concerns" => entries.Where(entry => entry.Tags.Contains(DraftWatchTag.CharacterConcern)),
            "Late-Round Targets" => entries.Where(entry => entry.Tags.Contains(DraftWatchTag.LateRoundTarget)),
            _ => entries
        };

        return entries
            .OrderBy(entry => BoardRankFor(entry))
            .Take(80)
            .Select(entry =>
            {
                var draftEntry = State.Snapshot.DraftBoard.Entries.FirstOrDefault(item => item.ProspectPersonId == entry.ProspectPersonId);
                var card = State.DraftIntelligenceCard(entry.ProspectPersonId);
                var consensus = State.DraftConsensus(entry.ProspectPersonId);
                var tags = entry.Tags.Count == 0 ? "Watching" : string.Join(", ", entry.Tags);
                var quick = draftEntry is null ? card.CurrentTeamLeague : State.DraftQuickScan(draftEntry);
                var fit = card.TeamFitScore >= 78 ? "Excellent Team Fit" : card.TeamFitScore >= 62 ? "Good Team Fit" : card.TeamFitScore >= 45 ? "Neutral Fit" : "Poor Fit";
                return new SelectablePersonRow(
                    entry.ProspectPersonId,
                    $"{BoardRankFor(entry)}. {entry.ProspectName}",
                    "DraftProspect",
                    $"{State.PositionShortText(card.Position)} | age {card.Age?.ToString() ?? "unknown"} | {quick}",
                    $"{card.RatingDisplay} | {card.Projection} | {consensus.Level}",
                    $"{fit} | Risk: {card.RiskSummary} | Tags: {tags}");
            })
            .ToArray();
    }

    private int BoardRankFor(DraftWarRoomEntry entry)
    {
        var card = State.DraftIntelligenceCard(entry.ProspectPersonId);
        return _draftWarRoomBoard switch
        {
            "Scout Board" => card.ScoutBoardRank,
            "Consensus Board" => card.ConsensusBoardRank,
            "Public Board" => entry.OriginalRank,
            _ => entry.PersonalRank
        };
    }

    private UIElement BuildDraftWarRoomLeftPanel()
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
        panel.Children.Add(UiPresentation.UiSectionHeader("Draft War Room"));
        var boards = new[] { "My Board", "Scout Board", "Consensus Board", "Public Board", "Watch List", "Sleepers", "Avoid List", "Medical Concerns", "Character Concerns", "Late-Round Targets" };
        var boardButtons = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        foreach (var board in boards)
        {
            boardButtons.Children.Add(CreateDetailButton(board, () =>
            {
                _draftWarRoomBoard = board;
                RefreshDraftWarRoomWorkspace();
            }, true));
        }

        panel.Children.Add(UiPresentation.Card(boardButtons));
        panel.Children.Add(UiPresentation.UiMetricCard("Class Theme", State.DraftClassThemeText, State.DraftClassSummaryText));
        panel.Children.Add(UiPresentation.UiMetricCard("Position Value", State.DraftPositionValueText, State.DraftClassPositionDepthText));
        panel.Children.Add(BuildTextCard("Team Needs", string.Join(" | ", State.DraftWarRoom.Needs.Take(3).Select(need => need.Label)), string.Join(Environment.NewLine, State.DraftWarRoom.Needs.Take(4).Select(need => $"{need.Priority}: {need.Reason}"))));
        panel.Children.Add(BuildTextCard("Pick Inventory", "Upcoming picks and rights", BuildDraftPickInventoryText()));
        return panel;
    }

    private UIElement BuildDraftWarRoomCenterPanel(IReadOnlyList<SelectablePersonRow> rows, SelectablePersonRow? selected)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
        panel.Children.Add(UiPresentation.UiSectionHeader($"{_draftWarRoomBoard} Board"));
        if (State.ScenarioSnapshot.DraftExperience is not null)
        {
            panel.Children.Add(UiPresentation.UiAlertBanner(BuildLiveDraftStatusLine(), State.ScenarioSnapshot.DraftExperience.IsPlayerTurn ? "attention" : "info"));
        }

        var list = new ListBox
        {
            ItemsSource = rows,
            SelectedItem = selected,
            MinHeight = 500,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            ItemTemplate = UiPresentation.PersonRowTemplate()
        };
        list.SelectionChanged += (_, _) =>
        {
            if (list.SelectedItem is SelectablePersonRow row)
            {
                _selectedDraftWarRoomProspectId = row.PersonId;
                RefreshDraftWarRoomWorkspace();
            }
        };
        list.MouseDoubleClick += (_, _) =>
        {
            if (list.SelectedItem is SelectablePersonRow row)
            {
                OpenUniversalPersonCard(row.PersonId);
            }
        };
        panel.Children.Add(rows.Count == 0
            ? UiPresentation.UiEmptyState("No prospects match", "No prospects match this board or watch tag. Try My Board or Consensus Board.")
            : list);
        panel.Children.Add(BuildDraftCompareStrip(rows));
        return panel;
    }

    private UIElement BuildDraftCompareStrip(IReadOnlyList<SelectablePersonRow> rows)
    {
        var panel = new StackPanel();
        panel.Children.Add(UiPresentation.UiSectionHeader("Compare Prospects"));
        panel.Children.Add(new TextBlock
        {
            Text = "Compare 2-4 prospects from the current board without exposing hidden true ratings.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = UiTheme.MutedText
        });
        AddActions(panel,
            CreateDetailButton("Compare Top 4", () => ShowDraftComparePopup(rows.Take(4).Select(row => row.PersonId).ToArray()), rows.Count >= 2),
            CreateDetailButton("View Full Profile", () =>
            {
                if (_selectedDraftWarRoomProspectId is not null)
                {
                    OpenUniversalPersonCard(_selectedDraftWarRoomProspectId);
                }
            }, _selectedDraftWarRoomProspectId is not null),
            CreateDetailButton("Add GM Note", () =>
            {
                if (_selectedDraftWarRoomProspectId is not null)
                {
                    State.AddDraftNoteFor(_selectedDraftWarRoomProspectId);
                    RefreshAfterAction();
                }
            }, _selectedDraftWarRoomProspectId is not null));
        return UiPresentation.Card(panel);
    }

    private UIElement BuildDraftWarRoomProspectPanel(string? prospectId)
    {
        if (string.IsNullOrWhiteSpace(prospectId))
        {
            return UiPresentation.UiEmptyState("Selected Prospect", "Select a prospect to see ratings, scouting, fit, story, and actions.");
        }

        var entry = State.Snapshot.DraftBoard.Entries.FirstOrDefault(item => item.ProspectPersonId == prospectId);
        var card = State.DraftIntelligenceCard(prospectId);
        var consensus = State.DraftConsensus(prospectId);
        var war = State.DraftWarRoom.BoardEntries.FirstOrDefault(item => item.ProspectPersonId == prospectId);
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 0) };
        panel.Children.Add(new TextBlock { Text = card.ProspectName, FontSize = UiTypography.CardTitle, FontWeight = FontWeights.SemiBold, Foreground = UiTheme.Text, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = $"{State.PositionShortText(card.Position)} | Age {card.Age?.ToString() ?? "unknown"} | {card.ShootsCatches} | {card.Height}, {card.Weight} | {card.CurrentTeamLeague}", Foreground = UiTheme.MutedText, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(UiPresentation.BadgeRow(
            ($"My #{card.MyBoardRank}", "info"),
            ($"Scout #{card.ScoutBoardRank}", "neutral"),
            ($"Consensus #{card.ConsensusBoardRank}", "neutral"),
            ($"Fit {card.TeamFitScore}/100", card.TeamFitScore >= 70 ? "positive" : card.TeamFitScore < 45 ? "critical" : "neutral"),
            ($"{card.RatingConfidenceColor}", ConfidenceSemantic(card.RatingConfidenceColor.ToString()))));
        panel.Children.Add(UiPresentation.UiInfoRow("OVR / POT", card.RatingDisplay));
        panel.Children.Add(UiPresentation.UiInfoRow("Projection", card.Projection));
        panel.Children.Add(UiPresentation.UiInfoRow("Risk", card.RiskSummary));
        panel.Children.Add(UiPresentation.UiInfoRow("Tags", war is null || war.Tags.Count == 0 ? "Watching" : string.Join(", ", war.Tags)));
        panel.Children.Add(BuildCommandCenterSection("Overview", entry is null ? card.CurrentTeamLeague : State.DraftCurrentPicture(entry), expanded: true));
        panel.Children.Add(BuildCommandCenterSection("Ratings", string.Join(Environment.NewLine, card.Attributes.Take(8).Select(attribute => attribute.DisplayText)), expanded: true));
        panel.Children.Add(BuildCommandCenterSection("Scouting", $"{card.ScoutRecommendation}\nConsensus: {consensus.Level} ({consensus.AgreementScore}/100)\n{consensus.Summary}"));
        panel.Children.Add(BuildScoutOpinionCards(consensus));
        panel.Children.Add(BuildCommandCenterSection("Development", $"Curve: {card.DevelopmentCurve}\nPace: {card.DevelopmentPace}\nETA: {card.Eta}"));
        panel.Children.Add(BuildCommandCenterSection("Medical", card.Alerts.Any(alert => alert.AlertType == DraftIntelligenceAlertType.MedicalRisk) ? string.Join(Environment.NewLine, card.Alerts.Where(alert => alert.AlertType == DraftIntelligenceAlertType.MedicalRisk).Select(alert => alert.Summary)) : "No major medical alert."));
        panel.Children.Add(BuildCommandCenterSection("Character", entry?.Bio?.CharacterSummary ?? "Character report is limited by scouting confidence."));
        panel.Children.Add(BuildCommandCenterSection("Team Fit", BuildDraftTeamFitText(card)));
        panel.Children.Add(BuildCommandCenterSection("Draft Story", entry is null ? card.RiskSummary : $"{State.DraftClassContext(entry)}\n{State.DraftValueContext(entry)}"));
        panel.Children.Add(BuildCommandCenterSection("GM Notes", war is null || string.IsNullOrWhiteSpace(war.GmNotes) ? "No GM note yet." : war.GmNotes));
        AddActions(panel,
            CreateDetailButton("Favorite", () => ToggleDraftTagAndRefresh(prospectId, DraftWatchTag.Favorite)),
            CreateDetailButton("Priority", () => ToggleDraftTagAndRefresh(prospectId, DraftWatchTag.Priority)),
            CreateDetailButton("Sleeper", () => ToggleDraftTagAndRefresh(prospectId, DraftWatchTag.Sleeper)),
            CreateDetailButton("Avoid", () => ToggleDraftTagAndRefresh(prospectId, DraftWatchTag.Avoid)),
            CreateDetailButton("Needs More Scouting", () => State.ScoutAgainFor(prospectId), State.AvailableScoutProfiles.Count > 0),
            CreateDetailButton("Draft Player", () => ConfirmDraftProspect(prospectId), State.ScenarioSnapshot.DraftExperience?.IsPlayerTurn == true, "Your team is not on the clock"));
        return UiPresentation.Card(panel);
    }

    private UIElement BuildScoutOpinionCards(DraftScoutConsensus consensus)
    {
        var grid = new UniformGrid { Columns = 1 };
        foreach (var opinion in consensus.Opinions.Take(4))
        {
            grid.Children.Add(BuildTextCard(opinion.Department, $"{opinion.Confidence} confidence", opinion.Opinion));
        }

        if (consensus.Opinions.Count == 0)
        {
            grid.Children.Add(UiPresentation.UiEmptyState("Scout Opinions", "This prospect has no completed scout disagreement report yet."));
        }

        return UiPresentation.UiExpandableSection("Scout Opinions", grid);
    }

    private string BuildDraftTeamFitText(DraftProspectIntelligenceCard card)
    {
        var label = card.TeamFitScore >= 78 ? "Excellent Fit" : card.TeamFitScore >= 62 ? "Good Fit" : card.TeamFitScore >= 45 ? "Neutral Fit" : "Poor Fit";
        var needs = State.DraftWarRoom.Needs.Take(2).Select(need => $"- {need.Label}: {need.Reason}");
        return $"{label}\n- {card.Projection}\n- {card.PlayerType}\n{string.Join(Environment.NewLine, needs)}";
    }

    private string BuildDraftPickInventoryText()
    {
        if (State.ScenarioSnapshot.DraftExperience?.Draft?.Picks is { } picks)
        {
            return string.Join(Environment.NewLine, picks
                .Where(pick => pick.Selection is null)
                .OrderBy(pick => pick.PickNumber)
                .Take(8)
                .Select(pick => $"#{pick.PickNumber} Round {pick.RoundNumber} - {State.ScenarioSnapshot.DraftExperience.OrganizationNames.GetValueOrDefault(pick.OwningOrganizationId, pick.OwningOrganizationId)}"));
        }

        return State.ScenarioSnapshot.DraftRights.Count == 0
            ? "Draft pick inventory appears when the live draft starts."
            : string.Join(Environment.NewLine, State.ScenarioSnapshot.DraftRights.Take(8).Select(right => $"R{right.RoundNumber} #{right.PickNumber}: {right.ProspectName}"));
    }

    private string BuildLiveDraftStatusLine()
    {
        var draft = State.ScenarioSnapshot.DraftExperience;
        if (draft is null)
        {
            return "Draft is not active. War Room stays available all season.";
        }

        return $"Live draft: pick {draft.OverallPick}, round {draft.CurrentRound}/{draft.TotalRounds}, on clock {draft.TeamSelecting}, your next pick {draft.PlayerNextPick?.PickNumber.ToString() ?? "none"}.";
    }

    private UIElement BuildDraftWarRoomBottomStrip()
    {
        var grid = new UniformGrid { Columns = 4, Margin = new Thickness(0, 12, 0, 0) };
        var draft = State.ScenarioSnapshot.DraftExperience;
        grid.Children.Add(UiPresentation.UiMetricCard("On Clock", draft?.TeamSelecting ?? "Not active", draft is null ? "War Room mode" : $"Overall pick {draft.OverallPick}"));
        grid.Children.Add(UiPresentation.UiMetricCard("Your Next Pick", draft?.PlayerNextPick?.PickNumber.ToString() ?? "none", draft?.CountdownPlaceholder ?? "No timer"));
        grid.Children.Add(BuildTextCard("Recent Picks", "Latest selections", draft is null || draft.Selections.Count == 0 ? "No picks yet." : string.Join(Environment.NewLine, draft.Selections.OrderByDescending(item => item.PickNumber).Take(4).Select(item => $"#{item.PickNumber} {item.OrganizationName}: {item.ProspectName}"))));
        grid.Children.Add(BuildTextCard("Upcoming Picks", "Next teams", draft?.Draft?.Picks is null ? "Draft order appears during live draft." : string.Join(Environment.NewLine, draft.Draft.Picks.Where(pick => pick.Selection is null).OrderBy(pick => pick.PickNumber).Take(4).Select(pick => $"#{pick.PickNumber} {draft.OrganizationNames.GetValueOrDefault(pick.OwningOrganizationId, pick.OwningOrganizationId)}"))));
        return grid;
    }

    private void ToggleDraftTagAndRefresh(string prospectId, DraftWatchTag tag)
    {
        State.ToggleDraftWarRoomTag(prospectId, tag);
        RefreshAfterAction();
    }

    private void ConfirmDraftProspect(string prospectId)
    {
        var name = FindPersonName(prospectId);
        if (MessageBox.Show($"Draft {name} with your current pick?", "Draft Player", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            State.DraftSelectedProspect(prospectId);
            RefreshAfterAction();
        }
    }

    private void ShowDraftComparePopup(IReadOnlyList<string> prospectIds)
    {
        var ids = prospectIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).Take(4).ToArray();
        if (ids.Length < 2)
        {
            ShowConfirmationPopup("Compare Prospects", "Select 2-4 prospects to compare.");
            return;
        }

        var grid = new UniformGrid { Columns = ids.Length, Margin = new Thickness(14) };
        foreach (var id in ids)
        {
            var card = State.DraftIntelligenceCard(id);
            var panel = new StackPanel();
            panel.Children.Add(UiPresentation.UiPersonLink(card.ProspectName, () => OpenUniversalPersonCard(id)));
            panel.Children.Add(UiPresentation.UiInfoRow("OVR / POT", card.RatingDisplay));
            panel.Children.Add(UiPresentation.UiInfoRow("Position", State.PositionShortText(card.Position)));
            panel.Children.Add(UiPresentation.UiInfoRow("Size", $"{card.Height}, {card.Weight}"));
            panel.Children.Add(UiPresentation.UiInfoRow("Projection", card.Projection));
            panel.Children.Add(UiPresentation.UiInfoRow("Curve / ETA", $"{card.DevelopmentCurve}, {card.Eta}"));
            panel.Children.Add(UiPresentation.UiInfoRow("Risk", card.RiskSummary));
            panel.Children.Add(UiPresentation.UiInfoRow("Team Fit", $"{card.TeamFitScore}/100"));
            panel.Children.Add(UiPresentation.UiInfoRow("Scout View", $"{card.ScoutConsensus} ({card.ScoutAgreementScore}/100)"));
            grid.Children.Add(UiPresentation.Card(panel));
        }

        ShowPopup("Compare Prospects", grid, 980, 560);
    }

    private void RefreshTradeCenterWorkspace()
    {
        if (_tradeCenterPanel is null || _state is null)
        {
            return;
        }

        var teams = BuildTradeTeamChoices();
        var selectedTeam = teams.FirstOrDefault(team => team.OrganizationId == _selectedTradeCenterOrganizationId)
            ?? teams.FirstOrDefault();
        _selectedTradeCenterOrganizationId = selectedTeam?.OrganizationId;

        _tradeCenterPanel.Children.Clear();
        _tradeCenterPanel.RowDefinitions.Clear();
        _tradeCenterPanel.ColumnDefinitions.Clear();
        _tradeCenterPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _tradeCenterPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _tradeCenterPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _tradeCenterPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _tradeCenterPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.88, GridUnitType.Star) });
        _tradeCenterPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.88, GridUnitType.Star) });
        _tradeCenterPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var top = BuildTradeCenterTeamContext(teams, selectedTeam);
        Grid.SetColumnSpan(top, 4);
        _tradeCenterPanel.Children.Add(top);

        var yourAssets = BuildTradeAssetPanel("Your Assets", State.ScenarioSnapshot.Organization.Name, State.YourTradeAssetRows(), true);
        var youGive = BuildTradeProposalBucket("You Give", "Assets leaving your organization", State.CurrentTradeYouGiveRows(), true);
        var youReceive = BuildTradeProposalBucket("You Receive", selectedTeam is null ? "Select another team" : $"Assets from {selectedTeam.TeamName}", State.CurrentTradeYouReceiveRows(), false);
        var theirAssets = BuildTradeAssetPanel("Their Assets", selectedTeam?.TeamName ?? "Select another team", selectedTeam is null ? Array.Empty<TradeAssetRow>() : State.OtherTradeAssetRows(selectedTeam.OrganizationId, selectedTeam.TeamName), false);

        Grid.SetRow(yourAssets, 1);
        Grid.SetColumn(yourAssets, 0);
        Grid.SetRow(youGive, 1);
        Grid.SetColumn(youGive, 1);
        Grid.SetRow(youReceive, 1);
        Grid.SetColumn(youReceive, 2);
        Grid.SetRow(theirAssets, 1);
        Grid.SetColumn(theirAssets, 3);
        _tradeCenterPanel.Children.Add(yourAssets);
        _tradeCenterPanel.Children.Add(youGive);
        _tradeCenterPanel.Children.Add(youReceive);
        _tradeCenterPanel.Children.Add(theirAssets);

        var bottom = BuildTradeCenterEvaluation(selectedTeam);
        Grid.SetRow(bottom, 2);
        Grid.SetColumnSpan(bottom, 4);
        _tradeCenterPanel.Children.Add(bottom);
    }

    private IReadOnlyList<TradeTeamChoice> BuildTradeTeamChoices() =>
        State.TradeBlockEntries
            .GroupBy(entry => entry.OrganizationId, StringComparer.Ordinal)
            .Select(group => new TradeTeamChoice(group.Key, group.First().TeamName))
            .OrderBy(team => team.TeamName, StringComparer.Ordinal)
            .ToArray();

    private UIElement BuildTradeCenterTeamContext(IReadOnlyList<TradeTeamChoice> teams, TradeTeamChoice? selectedTeam)
    {
        var panel = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        var combo = new ComboBox
        {
            ItemsSource = teams,
            SelectedItem = selectedTeam,
            DisplayMemberPath = nameof(TradeTeamChoice.TeamName),
            MinWidth = 240,
            Margin = new Thickness(0, 0, 12, 0)
        };
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is TradeTeamChoice team && team.OrganizationId != _selectedTradeCenterOrganizationId)
            {
                _selectedTradeCenterOrganizationId = team.OrganizationId;
                State.ClearTradeBuilder();
                RefreshTradeCenterWorkspace();
            }
        };
        DockPanel.SetDock(combo, Dock.Left);
        panel.Children.Add(combo);

        var context = new StackPanel();
        context.Children.Add(new TextBlock { Text = selectedTeam?.TeamName ?? "Select another team", FontSize = UiTypography.CardTitle, FontWeight = FontWeights.SemiBold, Foreground = UiTheme.Text });
        if (selectedTeam is null)
        {
            context.Children.Add(new TextBlock { Text = "Select another team to browse roster players, prospects, rights, draft picks, and trade block assets.", Foreground = UiTheme.MutedText, TextWrapping = TextWrapping.Wrap });
        }
        else
        {
            var sample = State.TradeBlockEntries.FirstOrDefault(entry => entry.OrganizationId == selectedTeam.OrganizationId);
            var brand = BrandingFor(selectedTeam.OrganizationId);
            context.Children.Add(UiPresentation.UiTeamCard(
                brand,
                $"{brand.TeamAbbreviation} {selectedTeam.TeamName}",
                $"{brand.LeagueName} | {brand.ConferenceDivision}",
                new[]
                {
                    $"Monogram {brand.Monogram.Letters} | Crest {brand.LogoPlaceholder}",
                    $"Trade context: {brand.IdentityPhrase}",
                    $"Arena: {brand.ArenaName}"
                }));
            context.Children.Add(new TextBlock { Text = sample is null ? "Team context pending." : State.TradeTeamNeedsShortText(sample), Foreground = UiTheme.Text, TextWrapping = TextWrapping.Wrap });
            context.Children.Add(new TextBlock { Text = $"Assets shopped: {State.OtherTradeAssetRows(selectedTeam.OrganizationId, selectedTeam.TeamName).Count} | Organization relationship and cooldowns remain tracked by existing trade logic.", Foreground = UiTheme.MutedText, TextWrapping = TextWrapping.Wrap });
        }

        panel.Children.Add(UiPresentation.Card(context));
        return panel;
    }

    private UIElement BuildTradeAssetPanel(string title, string subtitle, IReadOnlyList<TradeAssetRow> rows, bool isYourSide)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
        panel.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = UiTypography.SectionTitle, Foreground = UiTheme.Text });
        panel.Children.Add(new TextBlock { Text = subtitle, Foreground = UiTheme.MutedText, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(BuildTradeSourceTabs());
        var list = new ListBox
        {
            ItemsSource = rows,
            MinHeight = 360,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            ItemTemplate = TradeAssetRowTemplate()
        };
        list.SelectionChanged += (_, _) =>
        {
            if (list.SelectedItem is TradeAssetRow row)
            {
                if (isYourSide)
                {
                    State.SelectYourTradeAsset(row.Asset.AssetId);
                }
                else
                {
                    State.SelectOtherTradeAsset(row.Asset.AssetId);
                }
            }
        };
        list.MouseDoubleClick += (_, _) =>
        {
            if (list.SelectedItem is TradeAssetRow row)
            {
                if (isYourSide)
                {
                    State.AddYourAssetToTradeProposal(row.Asset);
                }
                else
                {
                    State.AddOtherAssetToTradeProposal(row.Asset);
                }

                RefreshAfterAction();
            }
        };
        if (isYourSide)
        {
            _tradeYourAssetsList = list;
        }
        else
        {
            _tradeOtherAssetsList = list;
        }

        panel.Children.Add(rows.Count == 0 ? UiPresentation.UiEmptyState("No assets", "No assets are currently visible for this source.") : list);
        AddActions(panel,
            CreateDetailButton(isYourSide ? "Add Your Selected Asset" : "Add Their Selected Asset", () =>
            {
                if (list.SelectedItem is TradeAssetRow row)
                {
                    if (isYourSide)
                    {
                        State.AddYourAssetToTradeProposal(row.Asset);
                    }
                    else
                    {
                        State.AddOtherAssetToTradeProposal(row.Asset);
                    }

                    RefreshAfterAction();
                }
            }, rows.Count > 0, "Select an asset to add."),
            CreateDetailButton("View Asset", () =>
            {
                if (list.SelectedItem is TradeAssetRow row)
                {
                    OpenTradeAssetProfile(row.Asset);
                }
            }, rows.Count > 0, "Select a player, prospect, or pick."));
        return UiPresentation.Card(panel);
    }

    private UIElement BuildTradeSourceTabs()
    {
        var tabs = new WrapPanel { Margin = new Thickness(0, 8, 0, 8) };
        foreach (var tab in new[] { "Roster", "Prospects", "Rights", "Draft Picks", "Contract Assets", "Trade Block", "Shortlist" })
        {
            tabs.Children.Add(UiPresentation.UiStatusBadge(tab, "neutral"));
        }

        return tabs;
    }

    private UIElement BuildTradeProposalBucket(string title, string subtitle, IReadOnlyList<TradeAssetRow> rows, bool isGiveSide)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
        panel.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = UiTypography.SectionTitle, Foreground = UiTheme.Text });
        panel.Children.Add(new TextBlock { Text = subtitle, Foreground = UiTheme.MutedText, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(UiPresentation.BadgeRow(
            ($"{rows.Sum(row => row.Asset.Value)} value", rows.Count == 0 ? "neutral" : "info"),
            ($"{rows.Sum(row => row.Asset.SalaryImpact):C0} salary", "neutral"),
            ($"{rows.Count} asset(s)", rows.Count == 0 ? "caution" : "positive")));
        var list = new ListBox
        {
            ItemsSource = rows,
            MinHeight = 300,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            ItemTemplate = TradeAssetRowTemplate()
        };
        if (isGiveSide)
        {
            _tradeYouGiveList = list;
        }
        else
        {
            _tradeYouReceiveList = list;
        }

        panel.Children.Add(rows.Count == 0 ? UiPresentation.UiEmptyState(title, "Select an asset from an asset panel, then add it here.") : list);
        AddActions(panel,
            CreateDetailButton("Remove Selected", () =>
            {
                if (list.SelectedItem is TradeAssetRow row)
                {
                    if (isGiveSide)
                    {
                        State.RemoveYourAssetFromTradeProposal(row.Asset);
                    }
                    else
                    {
                        State.RemoveOtherAssetFromTradeProposal(row.Asset);
                    }

                    RefreshAfterAction();
                }
            }, rows.Count > 0),
            CreateDetailButton("Clear Proposal", () =>
            {
                State.ClearTradeBuilder();
                RefreshAfterAction();
            }, State.HasTradeProposalAssets));
        return UiPresentation.Card(panel);
    }

    private UIElement BuildTradeCenterEvaluation(TradeTeamChoice? selectedTeam)
    {
        var grid = new UniformGrid { Columns = 4, Margin = new Thickness(0, 12, 0, 0) };
        grid.Children.Add(BuildTextCard("Trade Evaluation", State.CanProposeCurrentTrade ? State.CurrentTradeEvaluationText : "Both sides need at least one asset.", string.Join(Environment.NewLine, State.CurrentTradeEvaluationReasons.Take(4).DefaultIfEmpty("Add assets from both sides to see why they respond this way."))));
        grid.Children.Add(BuildTextCard("Roster Impact", "Before / after preview", State.CurrentTradeRosterImpact));
        grid.Children.Add(BuildTextCard("Cap / Contract Impact", "Payroll and contract count", $"{State.CurrentTradeBudgetImpact}\n{State.CurrentTradeAssetValueComparison}"));
        grid.Children.Add(BuildTextCard("Position Scarcity", "Market context", State.CurrentTradeScarcityText));

        var counter = BuildTextCard("Counteroffer", State.HasCurrentTradeCounter ? "Countering" : "No active counter", State.CurrentTradeCounterText);
        grid.Children.Add(counter);
        grid.Children.Add(BuildTextCard("Recent Activity", selectedTeam?.TeamName ?? "No team selected", selectedTeam is null ? "Select a team." : BuildTradeRecentActivityText(selectedTeam.OrganizationId)));
        var actions = new StackPanel();
        actions.Children.Add(new TextBlock { Text = "Actions", FontWeight = FontWeights.SemiBold, Foreground = UiTheme.Text });
        AddActions(actions,
            CreateDetailButton("Apply Counter", () =>
            {
                State.AcceptCurrentTradeCounter();
                RefreshAfterAction();
            }, State.HasCurrentTradeCounter),
            CreateDetailButton("Propose Trade", () =>
            {
                if (selectedTeam is not null)
                {
                    State.ProposeCurrentTrade(selectedTeam.OrganizationId, selectedTeam.TeamName);
                    RefreshAfterAction();
                }
            }, selectedTeam is not null && State.CanProposeCurrentTrade && State.TradeDeadlineWindow.TradesAllowed, State.TradeDeadlineWindow.TradesAllowed ? "Both sides need at least one asset." : "Trade deadline has passed"),
            CreateDetailButton("Withdraw", () =>
            {
                State.WithdrawLatestTradeOffer();
                RefreshAfterAction();
            }, State.CanWithdrawLatestTradeOffer),
            CreateDetailButton("Clear Offer", () =>
            {
                State.ClearTradeBuilder();
                RefreshAfterAction();
            }, State.HasTradeProposalAssets));
        grid.Children.Add(UiPresentation.Card(actions));
        grid.Children.Add(BuildTextCard("Warnings", "Invalid / cooldown state", State.CanProposeCurrentTrade ? "Proposal validates through existing trade service before any pending GM action is created." : "Both sides need at least one asset. Trades never auto-complete."));
        return grid;
    }

    private string BuildTradeRecentActivityText(string organizationId)
    {
        var transactions = State.LeagueRecentTransactions(organizationId).Take(4).ToArray();
        if (transactions.Length == 0)
        {
            return "No recent tracked activity with this team.";
        }

        return string.Join(Environment.NewLine, transactions.Select(item => $"{item.Date:yyyy-MM-dd}: {item.Description}"));
    }

    private static DataTemplate TradeAssetRowTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.PaddingProperty, UiSpacing.RowPadding);
        border.SetValue(Border.MarginProperty, new Thickness(0, 0, 0, 5));
        border.SetValue(Border.BorderBrushProperty, UiTheme.Border);
        border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        border.SetValue(Border.BackgroundProperty, UiTheme.SurfaceAlt);
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Label"));
        text.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        text.SetValue(TextBlock.ForegroundProperty, UiTheme.Text);
        border.AppendChild(text);
        return new DataTemplate { VisualTree = border };
    }

    private void OpenTradeAssetProfile(TradeAsset asset)
    {
        if (asset.AssetType is TradeAssetType.Player or TradeAssetType.ProspectRights)
        {
            OpenUniversalPersonCard(asset.AssetId);
            return;
        }

        ShowConfirmationPopup(asset.DisplayName, $"{asset.DisplayName}\n{asset.Summary}\nValue: {asset.Value}\nDraft picks and future considerations are selectable trade assets, but do not have player dossiers.");
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
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            ItemTemplate = UiPresentation.PersonRowTemplate()
        };
        list.SelectionChanged += (_, _) =>
        {
            if (list.SelectedItem is SelectablePersonRow row)
            {
                _selectedPeopleByTab[title] = row.PersonId;
                RenderSelectableDetail(title);
            }
        };
        list.MouseDoubleClick += (_, _) =>
        {
            if (list.SelectedItem is SelectablePersonRow row && IsLikelyPersonRow(row))
            {
                OpenUniversalPersonCard(row.PersonId);
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

    private UIElement CreateHockeyOperationsCommandCenter()
    {
        var root = new Grid { Background = Brushes.White };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(270) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(455) });

        var left = new StackPanel
        {
            Background = new SolidColorBrush(Color.FromRgb(239, 243, 248)),
            Margin = new Thickness(0)
        };
        left.Children.Add(new TextBlock
        {
            Text = "Hockey Operations",
            FontSize = 17,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(20, 40, 64)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(12, 12, 12, 10)
        });
        left.Children.Add(new TextBlock
        {
            Text = "Player sources",
            Foreground = UiTheme.MutedText,
            FontSize = UiTypography.Small,
            Margin = new Thickness(12, 0, 12, 4)
        });
        _commandCenterSearchInput = new TextBox
        {
            MinHeight = 30,
            Margin = new Thickness(12, 0, 12, 10),
            ToolTip = "Search players, prospects, free agents, trade targets, rights, and reports."
        };
        _commandCenterSearchInput.TextChanged += (_, _) => RefreshHockeyOperationsCommandCenter();
        left.Children.Add(_commandCenterSearchInput);
        _commandCenterPositionFilter = new ComboBox
        {
            ItemsSource = new[] { "All Positions", "C", "LW", "RW", "D", "G" },
            SelectedIndex = 0,
            Margin = new Thickness(12, 0, 12, 10),
            MinHeight = 28,
            ToolTip = "Position filter"
        };
        _commandCenterPositionFilter.SelectionChanged += (_, _) => RefreshHockeyOperationsCommandCenter();
        left.Children.Add(_commandCenterPositionFilter);
        _commandCenterSourceList = new ListBox
        {
            ItemsSource = new[]
            {
                "NHL Roster",
                "AHL Roster",
                "Junior / Returned Prospects",
                "Unsigned Rights",
                "Injured Players",
                "Waiver Wire",
                "Free Agents",
                "Trade Targets",
                "Drafted Prospects"
            },
            SelectedItem = _commandCenterSource,
            MinHeight = 270,
            Margin = new Thickness(12, 0, 12, 12)
        };
        _commandCenterSourceList.SelectionChanged += (_, _) =>
        {
            if (_commandCenterSourceList.SelectedItem is string source)
            {
                _commandCenterSource = source;
                RefreshHockeyOperationsCommandCenter();
            }
        };
        left.Children.Add(_commandCenterSourceList);
        Grid.SetColumn(left, 0);
        root.Children.Add(left);

        var center = new Grid { Margin = new Thickness(14) };
        center.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        center.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _commandCenterViewList = new ListBox
        {
            ItemsSource = new[]
            {
                "Roster Overview",
                "Lineup",
                "Depth Chart",
                "Prospects",
                "Development",
                "Contracts",
                "Scouting",
                "Roster Transactions",
                "Tactics",
                "Special Teams"
            },
            SelectedItem = _commandCenterView,
            MinHeight = 44,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            ItemsPanel = HorizontalWrapItemsPanel()
        };
        _commandCenterViewList.SelectionChanged += (_, _) =>
        {
            if (_commandCenterViewList.SelectedItem is string view)
            {
                _commandCenterView = view;
                RefreshHockeyOperationsCommandCenter();
            }
        };
        Grid.SetRow(_commandCenterViewList, 0);
        center.Children.Add(_commandCenterViewList);
        _commandCenterCenterPanel = new StackPanel();
        var centerScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _commandCenterCenterPanel,
            Margin = new Thickness(0, 12, 0, 0)
        };
        Grid.SetRow(centerScroll, 1);
        center.Children.Add(centerScroll);
        Grid.SetColumn(center, 1);
        root.Children.Add(center);

        _commandCenterPlayerCard = new StackPanel { Margin = new Thickness(16) };
        var right = new ScrollViewer
        {
            BorderThickness = new Thickness(1, 0, 0, 0),
            BorderBrush = new SolidColorBrush(Color.FromRgb(222, 229, 237)),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _commandCenterPlayerCard
        };
        Grid.SetColumn(right, 2);
        root.Children.Add(right);
        return root;
    }

    private static ItemsPanelTemplate HorizontalWrapItemsPanel()
    {
        var panel = new FrameworkElementFactory(typeof(WrapPanel));
        panel.SetValue(WrapPanel.OrientationProperty, Orientation.Horizontal);
        return new ItemsPanelTemplate(panel);
    }

    private UIElement CreateOrganizationCommandCenter()
    {
        var root = new Grid { Background = Brushes.White };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(230) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(420) });

        var left = new StackPanel
        {
            Background = new SolidColorBrush(Color.FromRgb(239, 243, 248)),
            Margin = new Thickness(0)
        };
        left.Children.Add(new TextBlock
        {
            Text = "Organization Command Center",
            FontSize = 17,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(20, 40, 64)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(12, 12, 12, 10)
        });
        _organizationCommandDepartmentList = new ListBox
        {
            ItemsSource = new[] { "Owner", "Front Office", "Coaching", "Scouting", "Development", "Medical", "Equipment", "Finance", "Facilities" },
            SelectedItem = _organizationCommandDepartment,
            MinHeight = 320,
            Margin = new Thickness(12, 0, 12, 12)
        };
        _organizationCommandDepartmentList.SelectionChanged += (_, _) =>
        {
            if (_organizationCommandDepartmentList.SelectedItem is string department)
            {
                _organizationCommandDepartment = department;
                RefreshOrganizationCommandCenter();
            }
        };
        left.Children.Add(_organizationCommandDepartmentList);
        Grid.SetColumn(left, 0);
        root.Children.Add(left);

        _organizationCommandCenterPanel = new StackPanel { Margin = new Thickness(14) };
        var centerScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _organizationCommandCenterPanel
        };
        Grid.SetColumn(centerScroll, 1);
        root.Children.Add(centerScroll);

        _organizationCommandStaffCard = new StackPanel { Margin = new Thickness(16) };
        var right = new ScrollViewer
        {
            BorderThickness = new Thickness(1, 0, 0, 0),
            BorderBrush = new SolidColorBrush(Color.FromRgb(222, 229, 237)),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _organizationCommandStaffCard
        };
        Grid.SetColumn(right, 2);
        root.Children.Add(right);
        return root;
    }

    private void AddWorkspaceTab(TabControl tabs, string title, IReadOnlyList<WorkspaceScreen> screens)
    {
        var root = new Grid { Background = Brushes.White };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
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
        var breadcrumb = new TextBlock
        {
            Text = BuildBreadcrumb(title, screens.FirstOrDefault()?.Label ?? string.Empty),
            Foreground = UiTheme.MutedText,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(14, 10, 14, 8),
            ToolTip = "Current location. Use Alt+Left and Alt+Right to move through recent context."
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
                if (_layoutReady && !_isRestoringNavigation)
                {
                    PushNavigationSnapshot();
                }

                contentHost.Content = content;
                breadcrumb.Text = BuildBreadcrumb(title, item.Content?.ToString() ?? string.Empty);
                Keyboard.Focus(navigation);
                if (_layoutReady && item.Content is string screen)
                {
                    RefreshWorkspaceScreen(title, screen);
                }
            }
        };

        if (navigation.Items.Count > 0)
        {
            navigation.SelectedIndex = 0;
        }

        Grid.SetColumn(navigation, 0);
        Grid.SetRowSpan(navigation, 2);
        Grid.SetColumn(breadcrumb, 1);
        Grid.SetRow(breadcrumb, 0);
        Grid.SetColumn(contentHost, 1);
        Grid.SetRow(contentHost, 1);
        root.Children.Add(navigation);
        root.Children.Add(breadcrumb);
        root.Children.Add(contentHost);

        var tab = new TabItem
        {
            Header = NavigationHeaderText(title, title),
            Tag = title,
            Content = root
        };
        _tabItems[title] = tab;
        _workspaceNavigations[title] = navigation;
        _workspaceBreadcrumbs[title] = breadcrumb;
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
        _rosterSearchInput.TextChanged += (_, _) => RefreshVisibleWorkspace();
        panel.Children.Add(LabeledControl("Search", _rosterSearchInput));

        _rosterPositionFilter = CreateRosterFilter(Enum.GetNames<RosterPosition>().Prepend("All").ToArray());
        panel.Children.Add(LabeledControl("Position", _rosterPositionFilter));

        _rosterStatusFilter = CreateRosterFilter(Enum.GetNames<RosterStatus>().Prepend("All").ToArray());
        panel.Children.Add(LabeledControl("Status", _rosterStatusFilter));

        _rosterPlayerTypeFilter = CreateRosterFilter(new[] { "All", "Goalie", "Defense", "Forward", "Prospect", "Veteran", "Injured" });
        panel.Children.Add(LabeledControl("Player type", _rosterPlayerTypeFilter));

        _rosterRoleFilter = CreateRosterFilter(new[] { "All", "Franchise", "First Line", "Top Six", "Middle Six", "Checking", "Fourth Line", "Top Pair", "Second Pair", "Third Pair", "Starter", "Tandem", "Backup", "Depth", "Prospect", "Healthy Scratch" });
        panel.Children.Add(LabeledControl("Role", _rosterRoleFilter));

        _rosterAgeFilter = CreateRosterFilter(new[] { "All", "Under 18", "18-19", "20+", "Unknown" });
        panel.Children.Add(LabeledControl("Age", _rosterAgeFilter));
        panel.Children.Add(CreateDetailButton("Reset Filters", ResetRosterFilters));

        return panel;
    }

    private void ResetRosterFilters()
    {
        _rosterSearchInput!.Text = string.Empty;
        _rosterPositionFilter!.SelectedIndex = 0;
        _rosterStatusFilter!.SelectedIndex = 0;
        _rosterPlayerTypeFilter!.SelectedIndex = 0;
        _rosterRoleFilter!.SelectedIndex = 0;
        _rosterAgeFilter!.SelectedIndex = 0;
        SetFeedback("Roster filters reset.");
        RefreshVisibleWorkspace();
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
        combo.SelectionChanged += (_, _) => RefreshVisibleWorkspace();
        return combo;
    }

    private static UIElement LabeledControl(string label, Control control)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
        control.ToolTip ??= label;
        panel.Children.Add(new Label
        {
            Content = label,
            Target = control,
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(70, 84, 102)),
            Margin = new Thickness(0, 0, 0, 3),
            Padding = new Thickness(0)
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

    private UIElement BuildActionCenterLayout()
    {
        var root = new Grid { Background = Brushes.White };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var filters = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(14, 10, 14, 10)
        };
        _actionCategoryFilter = CreateActionFilter(Enum.GetNames<ActionCenterCategory>().Prepend("All").ToArray());
        _actionPriorityFilter = CreateActionFilter(Enum.GetNames<ActionCenterPriority>().Prepend("All").ToArray());
        _actionStatusFilter = CreateActionFilter(Enum.GetNames<ActionCenterStatus>().Prepend("Open").Prepend("All").Distinct().ToArray());
        _actionStatusFilter.SelectedItem = "Open";
        filters.Children.Add(LabeledControl("Category", _actionCategoryFilter));
        filters.Children.Add(LabeledControl("Priority", _actionPriorityFilter));
        filters.Children.Add(LabeledControl("Status", _actionStatusFilter));
        filters.Children.Add(CreateDetailButton("Reset Filters", ResetActionCenterFilters));
        Grid.SetRow(filters, 0);
        Grid.SetColumnSpan(filters, 2);
        root.Children.Add(filters);

        _actionCenterList = new ListBox
        {
            BorderThickness = new Thickness(0, 1, 1, 0),
            BorderBrush = new SolidColorBrush(Color.FromRgb(222, 229, 237)),
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 253)),
            Padding = new Thickness(8),
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        _actionCenterList.SelectionChanged += (_, _) =>
        {
            if (_actionCenterList.SelectedItem is ActionCenterItem item)
            {
                _selectedActionCenterItemId = item.ActionCenterItemId;
                RenderActionCenterDetail(item);
            }
        };

        _actionCenterDetail = new StackPanel { Margin = new Thickness(18) };
        var detailScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _actionCenterDetail
        };

        Grid.SetRow(_actionCenterList, 1);
        Grid.SetColumn(_actionCenterList, 0);
        Grid.SetRow(detailScroll, 1);
        Grid.SetColumn(detailScroll, 1);
        root.Children.Add(_actionCenterList);
        root.Children.Add(detailScroll);
        return root;
    }

    private ComboBox CreateActionFilter(string[] items)
    {
        var combo = new ComboBox
        {
            ItemsSource = items,
            SelectedIndex = 0,
            MinWidth = 135,
            MinHeight = 30,
            Margin = new Thickness(0, 0, 8, 8)
        };
        combo.SelectionChanged += (_, _) => RefreshActionCenter();
        return combo;
    }

    private void ResetActionCenterFilters()
    {
        if (_actionCategoryFilter is not null)
        {
            _actionCategoryFilter.SelectedItem = "All";
        }

        if (_actionPriorityFilter is not null)
        {
            _actionPriorityFilter.SelectedItem = "All";
        }

        if (_actionStatusFilter is not null)
        {
            _actionStatusFilter.SelectedItem = "Open";
        }

        SetFeedback("Action Center filters reset.");
        RefreshActionCenter();
    }

    private void RefreshActionCenter()
    {
        if (_actionCenterList is null)
        {
            return;
        }

        var rows = FilterActionCenterItems().ToArray();
        var previous = _selectedActionCenterItemId;
        _actionCenterList.ItemsSource = null;
        _actionCenterList.ItemsSource = rows;

        var selected = rows.FirstOrDefault(item => item.ActionCenterItemId == previous)
            ?? rows.FirstOrDefault();
        _actionCenterList.SelectedItem = selected;
        _selectedActionCenterItemId = selected?.ActionCenterItemId;
        RenderActionCenterDetail(selected);
    }

    private IReadOnlyList<ActionCenterItem> FilterActionCenterItems()
    {
        var items = State.ActionCenterItems.AsEnumerable();
        var category = _actionCategoryFilter?.SelectedItem?.ToString();
        if (!string.IsNullOrWhiteSpace(category) && category != "All" && Enum.TryParse<ActionCenterCategory>(category, out var selectedCategory))
        {
            items = items.Where(item => item.Category == selectedCategory);
        }

        var priority = _actionPriorityFilter?.SelectedItem?.ToString();
        if (!string.IsNullOrWhiteSpace(priority) && priority != "All" && Enum.TryParse<ActionCenterPriority>(priority, out var selectedPriority))
        {
            items = items.Where(item => item.Priority == selectedPriority);
        }

        var status = _actionStatusFilter?.SelectedItem?.ToString();
        if (!string.IsNullOrWhiteSpace(status) && status != "All" && Enum.TryParse<ActionCenterStatus>(status, out var selectedStatus))
        {
            items = items.Where(item => item.Status == selectedStatus);
        }

        return items.ToArray();
    }

    private void RenderActionCenterDetail(ActionCenterItem? item)
    {
        if (_actionCenterDetail is null)
        {
            return;
        }

        _actionCenterDetail.Children.Clear();
        if (item is null)
        {
            _actionCenterDetail.Children.Add(UiPresentation.UiEmptyState(
                "No urgent decisions today.",
                "No Action Center item matches the current filters. Reset filters or advance time when you are ready."));
            return;
        }

        var panel = CreateDetailPanel(item.Title, $"{item.Priority} | {item.Category} | {item.Status}");
        _actionCenterDetail.Children.Add(panel);
        AddLine(panel, "Due date", item.DueDate?.ToString("yyyy-MM-dd") ?? "none");
        AddLine(panel, "Related person", item.RelatedPersonName ?? "none");
        AddLine(panel, "Related team", item.RelatedTeamName ?? "none");
        AddSubHeader(panel, "Reason");
        AddParagraph(panel, item.Reason);
        AddSubHeader(panel, "Consequence");
        AddParagraph(panel, item.Consequence);
        AddSubHeader(panel, "Recommended Action");
        AddParagraph(panel, item.RecommendedAction);
        AddActions(panel,
            CreateDetailButton("Go To Related Screen", () => GoToActionRelatedScreen(item)),
            CreateDetailButton("Mark Resolved", () => State.SetActionCenterStatus(item.ActionCenterItemId, ActionCenterStatus.Resolved)),
            CreateDetailButton("Defer", () => State.SetActionCenterStatus(item.ActionCenterItemId, ActionCenterStatus.Deferred)),
            CreateDetailButton("Dismiss", () => State.SetActionCenterStatus(item.ActionCenterItemId, ActionCenterStatus.Dismissed)));
    }

    private void GoToActionRelatedScreen(ActionCenterItem item)
    {
        var target = item.Category switch
        {
            ActionCenterCategory.Contracts => ("Hockey Operations", "Contracts"),
            ActionCenterCategory.Roster => ("Hockey Operations", "Roster"),
            ActionCenterCategory.Recruiting => ("Hockey Operations", "Recruits"),
            ActionCenterCategory.Scouting => ("Hockey Operations", "Scouting Operations"),
            ActionCenterCategory.Medical => ("Hockey Operations", "Roster"),
            ActionCenterCategory.GameDay => ("Season", "Schedule"),
            ActionCenterCategory.Staff => ("Organization", "Command Center"),
            ActionCenterCategory.Owner => ("Organization", "Owner"),
            ActionCenterCategory.Budget => ("Organization", "Budget"),
            ActionCenterCategory.League => ("League", "League Overview"),
            _ => ("Dashboard", "Dashboard")
        };
        if (!string.IsNullOrWhiteSpace(item.RelatedPersonId))
        {
            _selectedPeopleByTab[target.Item2] = item.RelatedPersonId;
        }

        SelectWorkspaceScreen(target.Item1, target.Item2);
        SetFeedback($"Opened related context for {item.Title}.");
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

    private bool IsCompactDensity => string.Equals(_displayDensity, "Compact", StringComparison.Ordinal);

    private TeamBrandingProfile CurrentTeamBranding() =>
        BrandingFor(State.ScenarioSnapshot.Organization.OrganizationId);

    private LeagueBrandingProfile CurrentLeagueBranding() =>
        State.ScenarioSnapshot.BrandingRegistry.LeagueProfiles.TryGetValue(State.ScenarioSnapshot.LeagueProfile.Identity.LeagueId, out var profile)
            ? profile
            : new UiBrandingService().LeagueFor(State.ScenarioSnapshot.LeagueProfile);

    private TeamBrandingProfile BrandingFor(string organizationId) =>
        State.ScenarioSnapshot.BrandingRegistry.TeamProfiles.TryGetValue(organizationId, out var profile)
            ? profile
            : new UiBrandingService().TeamFor(State.ScenarioSnapshot, organizationId);

    private string BrandedTeamLabel(string organizationId, string fallbackName)
    {
        var brand = BrandingFor(organizationId);
        return $"{brand.TeamAbbreviation} {brand.OrganizationDisplayName}";
    }

    private static string MainNavigationIcon(string title) =>
        title switch
        {
            "Dashboard" => "[HOME]",
            "Inbox" => "[MAIL]",
            "Hockey Operations" => "[HO]",
            "Organization" => "[ORG]",
            "League" => "[LG]",
            "Season" => "[SEAS]",
            "Reports / History" => "[REP]",
            "Settings placeholder" => "[SET]",
            _ => "[APP]"
        };

    private static string NavigationHeaderText(string title, string header) =>
        $"{MainNavigationIcon(title)} {header}";

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

    private void FinishSeason() => State.FinishSeasonAndEnterOffseason();

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

        ShowPopup($"Player Dossier - {dossier.PlayerName}", BuildDossierWindowContent(dossier), 760, 680, includeCloseButton: false);
        RefreshAfterAction();
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

    private void ShowPopup(string title, UIElement content, double width = 720, double height = 620, bool includeCloseButton = true)
    {
        _lastPopupFocus = Keyboard.FocusedElement;
        IncrementUxCounter($"modal:{title}");
        var root = new Grid
        {
            Margin = new Thickness(18),
            Focusable = true
        };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        if (includeCloseButton)
        {
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        var scroll = new ScrollViewer
        {
            Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Grid.SetRow(scroll, 0);
        root.Children.Add(scroll);

        if (includeCloseButton)
        {
            var footer = new WrapPanel
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };
            footer.Children.Add(CreateButton("Close", () => Window.GetWindow(root)?.Close()));
            Grid.SetRow(footer, 1);
            root.Children.Add(footer);
        }

        var window = new Window
        {
            Title = title,
            Width = width,
            Height = height,
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = root
        };
        window.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape)
            {
                args.Handled = true;
                window.Close();
            }
        };
        window.Loaded += (_, _) => root.Focus();
        window.Closed += (_, _) =>
        {
            _lastPopupFocus?.Focus();
            SetFeedback($"Closed {title}.");
        };
        window.ShowDialog();
    }

    private void ShowConfirmationPopup(string title, string message, Action? confirmAction = null)
    {
        var panel = CreateDetailPanel(title, "Confirmation");
        AddParagraph(panel, message);
        if (confirmAction is not null)
        {
            AddActions(panel, CreateDetailButton("Confirm", () =>
            {
                confirmAction();
                Window.GetWindow(panel)?.Close();
            }));
        }

        ShowPopup(title, panel, 520, 360);
    }

    private void ConfirmDestructiveAction(string title, string consequence, Action confirmAction)
    {
        ShowConfirmationPopup(title, consequence, () =>
        {
            confirmAction();
            SetFeedback($"{title} confirmed.");
        });
    }

    private void ShowContractOfferPlaceholder(string personId)
    {
        var panel = CreateDetailPanel("Contract Offer", FindPersonName(personId));
        AddParagraph(panel, "Full contract negotiation will use the existing contract engine when this action is ready for the selected player.");
        ShowPopup("Contract Offer", panel, 520, 360);
    }

    private void ShowStaffProfile(string personId)
    {
        State.FocusStaffProfile(personId);
        var panel = CreateDetailPanel(FindPersonName(personId), "Staff Profile");
        AddParagraph(panel, State.StaffProfileText(personId));
        ShowPopup($"Staff Profile - {FindPersonName(personId)}", panel, 640, 560);
    }

    private void OpenUniversalPersonCard(string personId)
    {
        ShowPopup($"Person Card - {FindPersonName(personId)}", BuildUniversalPersonCard(personId), 760, 680);
    }

    private UIElement BuildUniversalPersonCard(string personId)
    {
        var row = BuildUniversalPersonRow(personId);
        var panel = CreateDetailPanel(row.Name, $"{row.Kind} | {row.Primary}");
        panel.Children.Insert(1, UiPresentation.BadgeRow(
            ("Universal Person Card", "info"),
            (State.PersonPosition(personId).ToString(), "neutral"),
            (State.InjuryStatus(personId), StatusSemantic(State.InjuryStatus(personId))),
            (State.ScoutingConfidenceText(personId), ConfidenceSemantic(State.ScoutingConfidenceText(personId)))));

        AddSubHeader(panel, "Overview");
        AddLine(panel, "Age", State.PersonAge(personId)?.ToString() ?? "unknown");
        AddLine(panel, "Organization / rights", State.RegionTeamText(personId));
        AddLine(panel, "Role", State.CurrentLineupRole(personId));
        AddLine(panel, "Rating", State.RatingText(personId));
        AddLine(panel, "Contract", State.ContractRightsStatus(personId));
        AddLine(panel, "Health", State.InjuryStatus(personId));

        var ratings = new StackPanel();
        AddLine(ratings, "OVR / POT", State.RatingContextText(personId));
        AddLine(ratings, "Asset value", State.AssetValueShortText(personId));
        AddLine(ratings, "Position market", State.PositionMarketNote(personId));
        panel.Children.Add(UiPresentation.UiExpandableSection("Ratings", ratings, expanded: false));

        var development = new StackPanel();
        AddLine(development, "Stage", State.DevelopmentStageText(personId));
        AddLine(development, "Trend", State.DevelopmentTrend(personId));
        AddParagraph(development, State.DevelopmentPlanText(personId));
        panel.Children.Add(UiPresentation.UiExpandableSection("Development", development));

        var scouting = new StackPanel();
        AddLine(scouting, "Confidence", State.ScoutingConfidenceText(personId));
        AddParagraph(scouting, State.ScoutingKnowledgeText(personId));
        AddParagraph(scouting, State.ScoutingReportHeadline(personId));
        panel.Children.Add(UiPresentation.UiExpandableSection("Scouting Reports", scouting));

        var medical = new StackPanel();
        AddParagraph(medical, State.MedicalReportText(personId));
        panel.Children.Add(UiPresentation.UiExpandableSection("Medical History", medical));

        var career = new StackPanel();
        AddLine(career, "Last season", State.LastSeasonStats(personId));
        AddLine(career, "Career", State.CareerStatSummary(personId));
        AddParagraph(career, BuildCommandCenterCareerText(personId));
        panel.Children.Add(UiPresentation.UiExpandableSection("Career Timeline", career));

        AddActions(panel,
            CreateDetailButton("View Full Profile", () => OpenDossierFor(personId)),
            CreateDetailButton("Add GM Note", () => State.AddDossierNoteFor(personId)),
            CreateDetailButton("Return", () => Window.GetWindow(panel)?.Close()));
        return panel;
    }

    private SelectablePersonRow BuildUniversalPersonRow(string personId)
    {
        var staff = State.StaffProfiles.FirstOrDefault(profile => profile.PersonId == personId);
        if (staff is not null)
        {
            return new SelectablePersonRow(personId, staff.Name, "Staff", $"{StaffRoles.Title(staff.CurrentRole)} | {staff.Department}", $"{staff.ContractStatus} | relationship {staff.RelationshipWithGm}/100", staff.CurrentAssignment);
        }

        var candidate = State.StaffMarketCandidateFor(personId);
        if (candidate is not null)
        {
            return new SelectablePersonRow(personId, candidate.Candidate.Person.Identity.DisplayName, "Staff Candidate", $"{candidate.Candidate.StaffMember.CurrentRole} | {candidate.Candidate.StaffMember.Department}", $"{candidate.Candidate.Reputation} reputation | ask {candidate.Candidate.ExpectedSalary.AnnualAmount:C0}", candidate.Candidate.HiringRecommendation);
        }

        var owner = State.Snapshot.Owner;
        if (string.Equals(owner.OwnerId, personId, StringComparison.Ordinal) || string.Equals("owner", personId, StringComparison.Ordinal))
        {
            return new SelectablePersonRow(personId, owner.Name, "Owner", $"{owner.Archetype} | confidence {owner.Confidence}", State.OwnerOffice.JobSecurity.Level.ToString(), State.OwnerOffice.Personality.Vision);
        }

        return new SelectablePersonRow(
            personId,
            FindPersonName(personId),
            "Player",
            $"{State.PersonPosition(personId)} | age {State.PersonAge(personId)?.ToString() ?? "unknown"} | {State.RatingText(personId)}",
            $"{State.CurrentLineupRole(personId)} | {State.ContractRightsStatus(personId)} | {State.InjuryStatus(personId)}",
            State.PipelineText(personId));
    }

    private void SetStaffFocusFor(string personId)
    {
        State.SetStaffFocusFor(personId);
        ShowConfirmationPopup("Staff Focus", State.LatestSummary);
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
            Title = "Scouting Assignment",
            Width = 420,
            Height = 430,
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = panel
        };
        window.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape)
            {
                args.Handled = true;
                window.Close();
            }
        };
        window.Loaded += (_, _) => scoutBox.Focus();
        window.Closed += (_, _) => SetFeedback("Closed scouting assignment.");
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
        RefreshAfterAction();
    }

    private void RefreshAfterAction()
    {
        if (!_layoutReady || _state is null || _isRefreshing)
        {
            return;
        }

        RefreshHeaderChrome();
        RefreshVisibleWorkspace();
        UpdateTabBadges();
        RefreshDraftModal();
    }

    private void RefreshHeaderChrome()
    {
        if (_state is null)
        {
            return;
        }

        var snapshot = State.Snapshot;
        _dateText.Text = $"Current date: {snapshot.CurrentDate:yyyy-MM-dd}";
        _summaryText.Text = State.LatestSummary;
        _processedText.Text = $"Last processed events: {State.LastProcessedEventCount} | Inbox items: {State.Inbox.Count}";
        if (string.IsNullOrWhiteSpace(_feedbackText.Text))
        {
            _feedbackText.Text = $"Status: {State.LatestSummary}";
        }
    }

    private void RefreshVisibleWorkspace()
    {
        if (_mainTabs?.SelectedItem is not TabItem tab)
        {
            return;
        }

        // Use the stable Tag, not Header: UpdateTabBadges() rewrites Header to include a
        // live count (e.g. "Inbox (3)"), which would never match the workspace lookup key.
        var workspace = tab.Tag as string ?? tab.Header?.ToString() ?? string.Empty;
        if (_workspaceNavigations.TryGetValue(workspace, out var navigation)
            && navigation.SelectedItem is ListBoxItem item
            && item.Content is string screen)
        {
            RefreshWorkspaceScreen(workspace, screen);
        }
    }

    private void RefreshWorkspaceScreen(string workspace, string screen)
    {
        if (_state is null || _isRefreshing)
        {
            return;
        }

        switch (workspace, screen)
        {
            case ("Dashboard", "Dashboard"):
                RefreshDashboard();
                break;
            case ("Dashboard", "Action Center / Pending Decisions"):
                RefreshActionCenter();
                break;
            case ("Inbox", "GM Inbox"):
                RefreshInboxPanels();
                break;
            case ("Inbox", "League News / Transaction Wire"):
                SetTextTab("League News", BuildLeagueNews());
                break;
            case ("League", "League Overview"):
                SetTextTab("League Overview", BuildLeagueOverview());
                break;
            case ("League", "League Rules"):
                SetTextTab("League Rules", BuildLeagueRules());
                break;
            case ("League", "Teams"):
                RefreshSelectableTab("Teams", BuildTeamRows());
                break;
            case ("League", "Transactions"):
                SetTextTab("Transactions", BuildLeagueNews());
                break;
            case ("League", "Waiver Wire"):
                SetTextTab("Waiver Wire", BuildWaiverWire());
                break;
            case ("League", "League Free Agents"):
                RefreshSelectableTab("League Free Agents", BuildFreeAgentRows());
                break;
            case ("League", "League Draft"):
                SetTextTab("League Draft", BuildDraftHistoryReport());
                break;
            case ("League", "Position Market"):
                SetTextTab("Position Market", BuildPositionMarket());
                break;
            case ("League", "League Trade Block"):
                RefreshSelectableTab("League Trade Block", BuildTradeRows());
                break;
            case ("League", "League Standings"):
                SetTextTab("League Standings", BuildStandings());
                break;
            case ("Organization", "Command Center"):
                RefreshOrganizationCommandCenter();
                break;
            case ("Organization", "Owner"):
                SetTextTab("Owner", BuildOwner());
                break;
            case ("Organization", "Staff"):
                RefreshSelectableTab("Staff", BuildStaffRows());
                break;
            case ("Organization", "Staff Hiring"):
                RefreshSelectableTab("Staff Hiring", BuildStaffCandidateRows());
                break;
            case ("Organization", "Vacancies"):
                RefreshSelectableTab("Vacancies", BuildStaffVacancyRows());
                break;
            case ("Organization", "Budget"):
                SetTextTab("Budget", BuildBudgetWorkspace());
                break;
            case ("Organization", "Planning"):
                SetTextTab("Organization Planning", BuildOrganizationPlanning());
                break;
            case ("Organization", "Organization Health"):
                SetTextTab("Organization Health", BuildOrganizationHealth());
                break;
            case ("Organization", "Relationships"):
                SetTextTab("Relationships", BuildRelationships());
                break;
            case ("Hockey Operations", "Command Center"):
                RefreshHockeyOperationsCommandCenter();
                break;
            case ("Hockey Operations", "Roster"):
                RefreshSelectableTab("Roster", BuildRosterRows());
                break;
            case ("Hockey Operations", "Lineup"):
                RefreshSelectableTab("Lineup", BuildLineupRows());
                break;
            case ("Hockey Operations", "Tactics"):
                RefreshSelectableTab("Tactics", BuildTacticsRows());
                break;
            case ("Hockey Operations", "Prospects"):
                RefreshSelectableTab("Prospect List", BuildProspectRows());
                break;
            case ("Hockey Operations", "Recruits"):
                RefreshSelectableTab("Recruits", BuildRecruitRows());
                break;
            case ("Hockey Operations", "Free Agents"):
                RefreshSelectableTab("Free Agents", BuildFreeAgentRows());
                break;
            case ("Hockey Operations", "Contracts"):
                SetTextTab("Contracts", BuildContractsWorkspace());
                break;
            case ("Hockey Operations", "Contract Rights"):
                SetTextTab("Contract Rights", BuildContractRightsWorkspace());
                break;
            case ("Hockey Operations", "Arbitration"):
                SetTextTab("Arbitration", BuildArbitrationWorkspace());
                break;
            case ("Hockey Operations", "Buyouts"):
                SetTextTab("Buyouts", BuildBuyoutWorkspace());
                break;
            case ("Hockey Operations", "Offer Sheets"):
                SetTextTab("Offer Sheets", BuildOfferSheetWorkspace());
                break;
            case ("Hockey Operations", "Waivers"):
                SetTextTab("Hockey Waivers", BuildWaiverWire());
                break;
            case ("Hockey Operations", "Scouting"):
                RefreshSelectableTab("Scouting", BuildScoutingRows());
                break;
            case ("Hockey Operations", "Scouting Operations"):
                RefreshSelectableTab("Scouting Operations", BuildScoutingOperationRows());
                break;
            case ("Hockey Operations", "Trades"):
                RefreshTradeCenterWorkspace();
                break;
            case ("Hockey Operations", "Draft War Room"):
                RefreshDraftWarRoomWorkspace();
                break;
            case ("Hockey Operations", "Draft Board"):
                RefreshSelectableTab("Draft Board", BuildDraftBoardRows());
                break;
            case ("Hockey Operations", "Training Camp"):
                RefreshSelectableTab("Training Camp", BuildTrainingCampRows());
                break;
            case ("Season", "Schedule"):
                SetTextTab("Schedule", BuildSchedule());
                break;
            case ("Season", "Standings"):
                SetTextTab("Standings", BuildStandings());
                break;
            case ("Season", "Playoffs"):
                SetTextTab("Playoffs", BuildPlayoffs());
                break;
            case ("Season", "Stats"):
                SetTextTab("Stats", BuildStats());
                break;
            case ("Season", "Monthly Summary"):
                SetTextTab("Monthly Summary", BuildMonthlySummary());
                break;
            case ("Season", "Season Archive"):
                SetTextTab("Season Archive", BuildSeasonArchive());
                break;
            case ("Season", "Season Readiness"):
                SetTextTab("Season Readiness", BuildSeasonReadiness());
                break;
            case ("Settings placeholder", "Settings"):
                SetTextTab("Settings", BuildSettings());
                break;
            default:
                RefreshReportsScreen(screen);
                break;
        }
    }

    private void RefreshReportsScreen(string screen)
    {
        switch (screen)
        {
            case "Executive Reports": SetTextTab("Executive Reports", BuildExecutiveReports()); break;
            case "Organization Planning": SetTextTab("Organization Planning Report", BuildOrganizationPlanning()); break;
            case "Archived Seasons": SetTextTab("Archived Seasons", BuildSeasonArchive()); break;
            case "GM Career": SetTextTab("GM Career", BuildGmCareerHistory()); break;
            case "Organization History": SetTextTab("Organization History", BuildOrganizationHistoryReport()); break;
            case "Draft History": SetTextTab("Draft History", BuildDraftHistoryReport()); break;
            case "Drafted Players": SetTextTab("Drafted Players", BuildDraftedPlayersReport()); break;
            case "Where Are They Now": SetTextTab("Where Are They Now", BuildWhereAreTheyNowReport()); break;
            case "Player Career Timelines": SetTextTab("Player Career Timelines", BuildPlayerCareerTimelinesReport()); break;
            case "Career Milestones": SetTextTab("Career Milestones", BuildCareerMilestonesReport()); break;
            case "Player Stories": SetTextTab("Player Stories", BuildPlayerStoriesReport()); break;
            case "Media / News": SetTextTab("Media / News", BuildMediaNews()); break;
            case "Awards": SetTextTab("Awards", BuildAwardsReport()); break;
            case "Record Book": SetTextTab("Record Book", BuildRecordBookReport()); break;
            case "Team Records": SetTextTab("Team Records", BuildTeamRecordsReport()); break;
            case "League Records": SetTextTab("League Records", BuildLeagueRecordsReport()); break;
            case "Staff History": SetTextTab("Staff History", BuildStaffHistoryReport()); break;
            case "Staff Careers": SetTextTab("Staff Careers", BuildStaffCareersReport()); break;
            case "Coaching Trees": SetTextTab("Coaching Trees", BuildCoachingTreesReport()); break;
            case "Scout History": SetTextTab("Scout History", BuildScoutHistoryReport()); break;
            case "Development Staff History": SetTextTab("Development Staff History", BuildDevelopmentStaffHistoryReport()); break;
            case "Owner History": SetTextTab("Owner History", BuildOwnerHistoryReport()); break;
            case "Owner Letters": SetTextTab("Owner Letters", BuildOwnerLettersReport()); break;
            case "Job Security History": SetTextTab("Job Security History", BuildJobSecurityHistoryReport()); break;
            case "Expectation Results": SetTextTab("Expectation Results", BuildExpectationResultsReport()); break;
            case "Transaction History": SetTextTab("Transaction History", BuildTransactionHistoryReport()); break;
            case "Playoff Archive": SetTextTab("Playoff Archive", BuildPlayoffArchive()); break;
            case "Champions": SetTextTab("Champions", BuildChampionsReport()); break;
            case "Draft Recaps": SetTextTab("Draft Recaps", BuildDraftRecaps()); break;
            case "Monthly Summaries": SetTextTab("Monthly Summaries", BuildMonthlySummaries()); break;
            case "Career History": SetTextTab("Career History", BuildCareerHistory()); break;
            case "Journal": SetTextTab("Journal", BuildJournal()); break;
            case "Global Search": SetTextTab("Global Search", BuildGlobalSearch()); break;
            case "Playtest Checklist": SetTextTab("Playtest Checklist", BuildPlaytestChecklist()); break;
        }
    }

    private void SetTextTab(string title, string text)
    {
        if (_tabs.TryGetValue(title, out var tab))
        {
            tab.Text = text;
        }
    }

    private void RefreshAll()
    {
        _isRefreshing = true;
        try
        {
            RefreshHeaderChrome();

            RefreshDashboard();
            RefreshInboxPanels();
            RefreshActionCenter();
            RefreshHockeyOperationsCommandCenter();
            RefreshOrganizationCommandCenter();
            _tabs["Owner"].Text = BuildOwner();
            _tabs["League Overview"].Text = BuildLeagueOverview();
            _tabs["League Rules"].Text = BuildLeagueRules();
            RefreshSelectableTab("Teams", BuildTeamRows());
            _tabs["Transactions"].Text = BuildLeagueNews();
            _tabs["Waiver Wire"].Text = BuildWaiverWire();
            _tabs["Hockey Waivers"].Text = BuildWaiverWire();
            RefreshSelectableTab("League Free Agents", BuildFreeAgentRows());
            _tabs["League Draft"].Text = BuildDraftHistoryReport();
            _tabs["Position Market"].Text = BuildPositionMarket();
            RefreshSelectableTab("League Trade Block", BuildTradeRows());
            _tabs["League Standings"].Text = BuildStandings();
            RefreshSelectableTab("Staff", BuildStaffRows());
            RefreshSelectableTab("Staff Hiring", BuildStaffCandidateRows());
            RefreshSelectableTab("Vacancies", BuildStaffVacancyRows());
            RefreshSelectableTab("Roster", BuildRosterRows());
            RefreshSelectableTab("Lineup", BuildLineupRows());
            RefreshSelectableTab("Tactics", BuildTacticsRows());
            RefreshSelectableTab("Recruits", BuildRecruitRows());
            RefreshSelectableTab("Free Agents", BuildFreeAgentRows());
            _tabs["Contracts"].Text = BuildContractsWorkspace();
            _tabs["Contract Rights"].Text = BuildContractRightsWorkspace();
            _tabs["Arbitration"].Text = BuildArbitrationWorkspace();
            _tabs["Buyouts"].Text = BuildBuyoutWorkspace();
            _tabs["Offer Sheets"].Text = BuildOfferSheetWorkspace();
            RefreshSelectableTab("Scouting", BuildScoutingRows());
            RefreshSelectableTab("Scouting Operations", BuildScoutingOperationRows());
            RefreshTradeCenterWorkspace();
            if (_tabs.ContainsKey("Pending Actions"))
            {
                _tabs["Pending Actions"].Text = BuildPendingActions();
            }
            _tabs["League News"].Text = BuildLeagueNews();
            _tabs["Budget"].Text = BuildBudgetWorkspace();
            _tabs["Organization Planning"].Text = BuildOrganizationPlanning();
            _tabs["Organization Planning Report"].Text = BuildOrganizationPlanning();
            _tabs["Organization Health"].Text = BuildOrganizationHealth();
            RefreshSelectableTab("Player Dossier", BuildDossierRows());
            if (_selectableLists.ContainsKey("Draft Board"))
            {
                RefreshSelectableTab("Draft Board", BuildDraftBoardRows());
            }
            if (_tabs.ContainsKey("Draft War Room"))
            {
                _tabs["Draft War Room"].Text = BuildDraftWarRoom();
            }
            RefreshDraftWarRoomWorkspace();
            RefreshSelectableTab("Prospect List", BuildProspectRows());
            RefreshSelectableTab("Training Camp", BuildTrainingCampRows());
            _tabs["Season Readiness"].Text = BuildSeasonReadiness();
            _tabs["Schedule"].Text = BuildSchedule();
            _tabs["Standings"].Text = BuildStandings();
            _tabs["Playoffs"].Text = BuildPlayoffs();
            _tabs["Stats"].Text = BuildStats();
            _tabs["Monthly Summary"].Text = BuildMonthlySummary();
            _tabs["Season Archive"].Text = BuildSeasonArchive();
            _tabs["Executive Reports"].Text = BuildExecutiveReports();
            _tabs["Archived Seasons"].Text = BuildSeasonArchive();
            _tabs["GM Career"].Text = BuildGmCareerHistory();
            _tabs["Organization History"].Text = BuildOrganizationHistoryReport();
            _tabs["Draft History"].Text = BuildDraftHistoryReport();
            _tabs["Drafted Players"].Text = BuildDraftedPlayersReport();
            _tabs["Where Are They Now"].Text = BuildWhereAreTheyNowReport();
            _tabs["Player Career Timelines"].Text = BuildPlayerCareerTimelinesReport();
            _tabs["Career Milestones"].Text = BuildCareerMilestonesReport();
            _tabs["Player Stories"].Text = BuildPlayerStoriesReport();
            _tabs["Media / News"].Text = BuildMediaNews();
            _tabs["Awards"].Text = BuildAwardsReport();
            _tabs["Record Book"].Text = BuildRecordBookReport();
            _tabs["Team Records"].Text = BuildTeamRecordsReport();
            _tabs["League Records"].Text = BuildLeagueRecordsReport();
            _tabs["Staff History"].Text = BuildStaffHistoryReport();
            _tabs["Staff Careers"].Text = BuildStaffCareersReport();
            _tabs["Coaching Trees"].Text = BuildCoachingTreesReport();
            _tabs["Scout History"].Text = BuildScoutHistoryReport();
            _tabs["Development Staff History"].Text = BuildDevelopmentStaffHistoryReport();
            _tabs["Owner History"].Text = BuildOwnerHistoryReport();
            _tabs["Owner Letters"].Text = BuildOwnerLettersReport();
            _tabs["Job Security History"].Text = BuildJobSecurityHistoryReport();
            _tabs["Expectation Results"].Text = BuildExpectationResultsReport();
            _tabs["Transaction History"].Text = BuildTransactionHistoryReport();
            _tabs["Playoff Archive"].Text = BuildPlayoffArchive();
            _tabs["Champions"].Text = BuildChampionsReport();
            _tabs["Draft Recaps"].Text = BuildDraftRecaps();
            _tabs["Monthly Summaries"].Text = BuildMonthlySummaries();
            _tabs["Career History"].Text = BuildCareerHistory();
            _tabs["Journal"].Text = BuildJournal();
            _tabs["Global Search"].Text = BuildGlobalSearch();
            _tabs["Playtest Checklist"].Text = BuildPlaytestChecklist();
            _tabs["Settings"].Text = BuildSettings();
            _tabs["Relationships"].Text = BuildRelationships();
            UpdateTabBadges();
            RefreshDraftModal();
        }
        finally
        {
            _isRefreshing = false;
        }
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
        var cap = State.SalaryCap;
        _dashboardPanel.Children.Clear();
        _dashboardPanel.Children.Add(UiPresentation.UiTeamHeader(
            CurrentTeamBranding(),
            CurrentLeagueBranding(),
            State.TeamRecordText,
            State.PlayerOrganizationLeagueProfile.CurrentStrategy.ToString(),
            OwnerMoodText(),
            cap.IsEnabled ? cap.Status.ToString() : budget.Status.ToString()));

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
        metrics.Children.Add(CreateDashboardMetric("Open Actions", State.OpenActionCount.ToString(), "Action Center items", State.OpenActionCount > 0));
        metrics.Children.Add(CreateDashboardMetric("Urgent Actions", State.UrgentActionCount.ToString(), "need attention before advancing", State.UrgentActionCount > 0));
        metrics.Children.Add(CreateDashboardMetric("Pending Decisions", State.PendingDecisionCount.ToString(), "GM approval required", State.PendingDecisionCount > 0));
        metrics.Children.Add(CreateDashboardMetric("Urgent Decisions", State.UrgentPendingDecisionCount.ToString(), State.NextDecisionDeadlineText, State.UrgentPendingDecisionCount > 0));
        metrics.Children.Add(CreateDashboardMetric("Roster Issues", State.RosterWarningCount.ToString(), roster.ValidationResult.Message, State.RosterWarningCount > 0));
        metrics.Children.Add(CreateDashboardMetric("Game Usage", State.GameUsageWarningCount.ToString(), State.GameUsageDashboardSummary, State.GameUsageWarningCount > 0));
        metrics.Children.Add(CreateDashboardMetric("Tactical Fit", State.CurrentTactics.FitReport.Grade.ToString(), State.TacticsDashboardSummary, State.TacticsWarningCount > 0));
        metrics.Children.Add(CreateDashboardMetric("Staff Vacancies", State.StaffVacancies.Count.ToString(), State.StaffVacancySummary, State.StaffVacancies.Count > 0));
        metrics.Children.Add(CreateDashboardMetric("Scouting Reports", State.ScoutingReportCount.ToString(), $"{State.ScenarioSnapshot.ScoutingOperations.Count(item => item.IsOpen)} active assignment(s)", false));
        metrics.Children.Add(CreateDashboardMetric("League News", State.LeagueNewsCount.ToString(), "notable league items", false));
        metrics.Children.Add(CreateDashboardMetric(
            "Top Headline",
            State.TopMediaHeadline?.Source.Name ?? "None",
            State.TopMediaHeadline?.Headline ?? "No major media headline yet",
            State.TopMediaHeadline?.Importance >= MediaImportance.Breaking));
        metrics.Children.Add(CreateDashboardMetric("Journal", State.JournalEntries.Count.ToString(), "routine updates archived", false));
        if (State.TradeDeadlineWindow.Status != TradeDeadlineStatus.NotStarted)
        {
            metrics.Children.Add(CreateDashboardMetric("Trade Deadline", State.TradeDeadlineCardTitle, State.TradeDeadlineWindow.Summary, State.TradeDeadlineWindow.Status is TradeDeadlineStatus.DeadlineWeek or TradeDeadlineStatus.DeadlineDay or TradeDeadlineStatus.Closed));
        }

        metrics.Children.Add(CreateDashboardMetric("Budget", budget.Status.ToString(), $"{budget.RemainingBudget:C0} remaining", budget.Status == BudgetStatus.OverBudget));
        metrics.Children.Add(CreateDashboardMetric(
            "Salary Cap",
            cap.Status.ToString(),
            cap.IsEnabled ? $"{cap.AvailableCapSpace:C0} remaining | {cap.CapPercentage:0.##}% used" : "Disabled by rulebook",
            cap.Status is SalaryCapStatus.OverCap or SalaryCapStatus.Violation));
        metrics.Children.Add(CreateDashboardMetric("Owner Mood", OwnerMoodText(), $"Job security {State.OwnerOffice.JobSecurity.Level} | Support {State.OwnerOffice.Confidence.Support}", State.OwnerOffice.JobSecurity.Level is JobSecurityLevel.HotSeat or JobSecurityLevel.Critical));
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
        metrics.Children.Add(CreateDashboardMetric("Playoffs", State.PlayoffStatusText, State.PlayoffDashboardSummary, State.ScenarioSnapshot.Playoffs.Bracket?.Status == PlayoffStatus.InProgress));
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
            CreateDetailButton("Review Pending Actions", () => SelectWorkspaceScreen("Dashboard", "Action Center / Pending Decisions")));
        Grid.SetColumn(actionsCard, 0);
        lower.Children.Add(actionsCard);

        var summaryCard = CreateDashboardCard("Action Center / Pending Decisions", out var summary);
        AddLine(summary, "Owner", snapshot.Owner.Name);
        AddLine(summary, "Job security", State.OwnerOffice.JobSecurity.Level.ToString());
        AddLine(summary, "Owner expectation", State.OwnerOffice.Expectations.FirstOrDefault()?.Description ?? "No active owner expectation.");
        AddLine(summary, "GM", snapshot.GeneralManager.Identity.DisplayName);
        AddLine(summary, "Head scout", snapshot.Scout.Name);
        AddLine(summary, "Roster", $"{roster.CurrentRosterSize}/{roster.RequiredRosterSize} opening target");
        AddLine(summary, "Staff vacancies", State.StaffVacancySummary);
        AddLine(summary, "Season readiness", readiness.RosterStatus);
        AddLine(summary, "Last game", lastGame is null ? "No completed game" : lastGame.BoxScore.FinalScore);
        AddLine(summary, "Last top performer", lastGame?.TopLineSummary ?? "No game report yet");
        AddLine(summary, "Last game concern", lastGame?.KeyConcern ?? "No game concern yet");
        AddLine(summary, "Next game", nextGame is null ? "No scheduled game" : $"{nextGame.Date:yyyy-MM-dd}: {DescribeGame(nextGame)}");
        AddLine(summary, "Team record", record);
        AddLine(summary, "Standings rank", State.StandingsRankText);
        AddLine(summary, "Playoffs", State.PlayoffDashboardSummary);
        AddLine(summary, "Top Headline", State.TopMediaHeadline?.Headline ?? "No major headline yet.");
        AddLine(summary, "Urgent decisions", $"{State.UrgentPendingDecisionCount} urgent of {State.PendingDecisionCount} open");
        AddLine(summary, "Open actions", $"{State.OpenActionCount} open / {State.UrgentActionCount} urgent");
        AddLine(summary, "Inbox focus", State.InboxFocusSummary);
        AddLine(summary, "League news", $"{State.LeagueNewsCount} notable item(s)");
        AddLine(summary, "Journal", $"{State.JournalEntries.Count} routine update(s) archived");
        AddLine(summary, "Trade deadline", State.TradeDeadlineWindow.Summary);
        AddLine(summary, "Last advance result", State.LastStopReason);
        AddLine(summary, "Next stop reason", State.LastStopReason);
        var nextAction = State.ActionCenterItems.FirstOrDefault(item => item.Status == ActionCenterStatus.Open);
        AddLine(summary, "Next recommended action", nextAction?.RecommendedAction ?? "No urgent work queued.");
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

        var workflow = new Grid { Margin = new Thickness(0, 14, 0, 0) };
        workflow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        workflow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        workflow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var agendaCard = CreateDashboardCard("Daily Agenda", out var agenda);
        foreach (var line in State.DailyAgenda)
        {
            AddParagraph(agenda, line);
        }
        Grid.SetColumn(agendaCard, 0);
        workflow.Children.Add(agendaCard);

        var urgentCard = CreateDashboardCard("Top Urgent Actions", out var urgent);
        var topActions = State.ActionCenterItems.Where(item => item.Status == ActionCenterStatus.Open).Take(4).ToArray();
        if (topActions.Length == 0)
        {
            AddParagraph(urgent, "No open Action Center items.");
        }
        foreach (var item in topActions)
        {
            AddLine(urgent, item.Category.ToString(), $"{item.Title} - {item.RecommendedAction}");
        }
        AddActions(urgent, CreateDetailButton("View All Actions", () => SelectWorkspaceScreen("Dashboard", "Action Center / Pending Decisions")));
        Grid.SetColumn(urgentCard, 1);
        workflow.Children.Add(urgentCard);

        var assistantCard = CreateDashboardCard("Assistant GM Recommendations", out var assistant);
        foreach (var recommendation in State.AssistantGmRecommendations)
        {
            AddParagraph(assistant, recommendation);
        }
        AddSubHeader(assistant, "Upcoming Events");
        foreach (var item in State.UpcomingActionEvents)
        {
            AddParagraph(assistant, item);
        }
        Grid.SetColumn(assistantCard, 2);
        workflow.Children.Add(assistantCard);
        _dashboardPanel.Children.Add(workflow);
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
            MinHeight = IsCompactDensity ? 96 : 116,
            Margin = new Thickness(0, 0, 12, 12),
            Padding = IsCompactDensity ? new Thickness(10) : new Thickness(14),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(warning ? Color.FromRgb(224, 174, 160) : Color.FromRgb(221, 229, 238)),
            Background = new SolidColorBrush(warning ? Color.FromRgb(255, 247, 244) : Color.FromRgb(248, 250, 253)),
            CornerRadius = new CornerRadius(6)
        };
    }

    private string OwnerMoodText()
    {
        var ownerOffice = State.OwnerOffice;
        return ownerOffice.Confidence.Support switch
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
        SetTabHeader("Dashboard", State.OpenActionCount > 0 ? $"Dashboard ({State.OpenActionCount})" : "Dashboard");
        SetTabHeader("Inbox", $"Inbox ({State.UnreadInboxCount})");
        SetTabHeader("Organization", State.StaffVacancies.Count > 0 ? $"Organization ({State.StaffVacancies.Count})" : "Organization");
        var operationsCount = State.RosterWarningCount + State.ScoutingReportCount + State.ContractDecisionCount + State.DevelopmentActionCount + State.TacticsWarningCount;
        SetTabHeader("Hockey Operations", operationsCount > 0 ? $"Hockey Operations ({operationsCount})" : "Hockey Operations");
        SetTabHeader("Season", "Season");
        SetTabHeader("Reports / History", "Reports / History");
        SetTabHeader("Settings placeholder", "Settings");
    }

    private void SetTabHeader(string title, string header)
    {
        if (_tabItems.TryGetValue(title, out var item))
        {
            item.Header = NavigationHeaderText(title, header);
        }
    }

    private void SelectTab(string title)
    {
        if (_mainTabs is not null && _tabItems.TryGetValue(title, out var item))
        {
            if (!ReferenceEquals(_mainTabs.SelectedItem, item) && !_isRestoringNavigation)
            {
                PushNavigationSnapshot();
            }

            _mainTabs.SelectedItem = item;
            SetFeedback($"Opened {title}.");
        }
    }

    private void SelectWorkspaceScreen(string title, string screenLabel)
    {
        SelectTab(title);
        if (!_workspaceNavigations.TryGetValue(title, out var navigation))
        {
            return;
        }

        foreach (var item in navigation.Items.OfType<ListBoxItem>())
        {
            if (string.Equals(item.Content?.ToString(), screenLabel, StringComparison.Ordinal))
            {
                navigation.SelectedItem = item;
                SetFeedback($"Opened {BuildBreadcrumb(title, screenLabel)}.");
                break;
            }
        }
    }

    private string BuildBreadcrumb(string workspace, string screen)
    {
        var selected = _selectedPeopleByTab.GetValueOrDefault(screen);
        var person = !string.IsNullOrWhiteSpace(selected) ? $" > {FindPersonName(selected)}" : string.Empty;
        return string.IsNullOrWhiteSpace(screen)
            ? workspace
            : $"{workspace} > {screen}{person}";
    }

    private void PushNavigationSnapshot()
    {
        var snapshot = CurrentNavigationSnapshot();
        if (snapshot is null)
        {
            return;
        }

        if (_backStack.Count > 0 && _backStack.Peek().Equals(snapshot))
        {
            return;
        }

        _backStack.Push(snapshot);
        TrimStack(_backStack, 25);
        _forwardStack.Clear();
    }

    private NavigationSnapshot? CurrentNavigationSnapshot()
    {
        if (_mainTabs?.SelectedItem is not TabItem tab)
        {
            return null;
        }

        var workspace = tab.Tag as string ?? tab.Header?.ToString() ?? string.Empty;
        var screen = _workspaceNavigations.TryGetValue(workspace, out var navigation)
            && navigation.SelectedItem is ListBoxItem item
            ? item.Content?.ToString() ?? string.Empty
            : string.Empty;
        var selectedPerson = !string.IsNullOrWhiteSpace(screen)
            ? _selectedPeopleByTab.GetValueOrDefault(screen)
            : null;
        return string.IsNullOrWhiteSpace(workspace) ? null : new NavigationSnapshot(workspace, screen, selectedPerson);
    }

    private void NavigateBack()
    {
        var current = CurrentNavigationSnapshot();
        if (_backStack.Count == 0)
        {
            SetFeedback("No previous GM Office screen to return to.");
            return;
        }

        if (current is not null)
        {
            _forwardStack.Push(current);
            TrimStack(_forwardStack, 25);
        }

        RestoreNavigationSnapshot(_backStack.Pop(), "Back");
    }

    private void NavigateForward()
    {
        var current = CurrentNavigationSnapshot();
        if (_forwardStack.Count == 0)
        {
            SetFeedback("No forward GM Office screen is available.");
            return;
        }

        if (current is not null)
        {
            _backStack.Push(current);
            TrimStack(_backStack, 25);
        }

        RestoreNavigationSnapshot(_forwardStack.Pop(), "Forward");
    }

    private void RestoreNavigationSnapshot(NavigationSnapshot snapshot, string direction)
    {
        _isRestoringNavigation = true;
        try
        {
            if (!string.IsNullOrWhiteSpace(snapshot.Screen) && !string.IsNullOrWhiteSpace(snapshot.SelectedPersonId))
            {
                _selectedPeopleByTab[snapshot.Screen] = snapshot.SelectedPersonId;
            }

            SelectWorkspaceScreen(snapshot.Workspace, snapshot.Screen);
            SetFeedback($"{direction}: {BuildBreadcrumb(snapshot.Workspace, snapshot.Screen)}.");
        }
        finally
        {
            _isRestoringNavigation = false;
        }
    }

    private static void TrimStack(Stack<NavigationSnapshot> stack, int maxCount)
    {
        if (stack.Count <= maxCount)
        {
            return;
        }

        var retained = stack.Take(maxCount).Reverse().ToArray();
        stack.Clear();
        foreach (var snapshot in retained)
        {
            stack.Push(snapshot);
        }
    }

    private void SetFeedback(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        _feedbackText.Text = $"Status: {message}";
    }

    private void IncrementUxCounter(string key)
    {
        _localUxCounters[key] = _localUxCounters.GetValueOrDefault(key) + 1;
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
            "Lineup" => BuildLineupDetail(row),
            "Tactics" => BuildTacticsDetail(row),
            "Recruits" => BuildPlayerDetail(title, row),
            "Free Agents" => BuildPlayerDetail(title, row),
            "League Free Agents" => BuildPlayerDetail("Free Agents", row),
            "Teams" => BuildTeamDetail(row),
            "Scouting" => BuildPlayerDetail(title, row),
            "Scouting Operations" => BuildScoutingOperationDetail(row),
            "Trades" => BuildTradeDetail(row),
            "League Trade Block" => BuildTradeDetail(row),
            "Draft Board" => BuildPlayerDetail(title, row),
            "Prospect List" => BuildPlayerDetail(title, row),
            "Training Camp" => BuildTrainingCampDetail(row),
            "Player Dossier" => BuildDossierDetail(row),
            _ => EmptyDetail(title, "No detail panel is configured for this view.")
        });
    }

    private void RefreshHockeyOperationsCommandCenter()
    {
        if (_commandCenterCenterPanel is null || _commandCenterPlayerCard is null)
        {
            return;
        }

        var rows = BuildCommandCenterRows();
        var selected = rows.FirstOrDefault(row => string.Equals(row.PersonId, _selectedCommandCenterPersonId, StringComparison.Ordinal))
            ?? rows.FirstOrDefault();
        _selectedCommandCenterPersonId = selected?.PersonId;

        RenderCommandCenterCenter(rows, selected);
        RenderCommandCenterPlayerCard(selected);
    }

    private IReadOnlyList<SelectablePersonRow> BuildCommandCenterRows()
    {
        IReadOnlyList<SelectablePersonRow> sourceRows = _commandCenterSource switch
        {
            "Drafted Prospects" => BuildProspectRows(),
            "Prospects" => BuildProspectRows(),
            "AHL Roster" => BuildProspectRows()
                .Where(row => CommandCenterMatches(row, "AHL", "affiliate", "assigned"))
                .ToArray(),
            "Junior / Returned Prospects" => BuildProspectRows()
                .Where(row => CommandCenterMatches(row, "junior", "youth", "returned"))
                .ToArray(),
            "Unsigned Rights" => BuildProspectRows()
                .Where(row => CommandCenterMatches(row, "rights", "unsigned", "DraftRightsHeld"))
                .ToArray(),
            "Injured Players" => BuildRosterRows()
                .Where(row => row.Kind != "RosterSummary" && !State.InjuryStatus(row.PersonId).Equals("Available", StringComparison.OrdinalIgnoreCase))
                .ToArray(),
            "Waiver Wire" => BuildRosterRows()
                .Where(row => row.Kind != "RosterSummary" && CommandCenterMatches(row, "waiver", "AHL Eligible", "exempt"))
                .ToArray(),
            "Free Agents" => BuildFreeAgentRows(),
            "Trade Targets" => BuildTradeRows(),
            _ => BuildRosterRows()
                .Where(row => row.Kind != "RosterSummary")
                .ToArray()
        };

        var query = _commandCenterSearchInput?.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(query))
        {
            sourceRows = sourceRows
                .Where(row => CommandCenterMatches(row, query))
                .ToArray();
        }

        var position = _commandCenterPositionFilter?.SelectedItem?.ToString();
        if (!string.IsNullOrWhiteSpace(position) && position != "All Positions")
        {
            sourceRows = sourceRows
                .Where(row => RowPositionText(row.PersonId).Equals(position, StringComparison.OrdinalIgnoreCase)
                    || row.Primary.Contains(position, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        return sourceRows
            .GroupBy(row => row.PersonId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    private static bool CommandCenterMatches(SelectablePersonRow row, params string[] terms) =>
        terms.Any(term => !string.IsNullOrWhiteSpace(term)
            && (row.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || row.Kind.Contains(term, StringComparison.OrdinalIgnoreCase)
                || row.Primary.Contains(term, StringComparison.OrdinalIgnoreCase)
                || row.Secondary.Contains(term, StringComparison.OrdinalIgnoreCase)
                || row.Summary.Contains(term, StringComparison.OrdinalIgnoreCase)));

    private UIElement BuildHockeyOperationsTeamSummaryStrip()
    {
        var grid = new UniformGrid { Columns = 4, Margin = new Thickness(0, 0, 0, 12) };
        var active = State.Snapshot.Roster.ActivePlayers;
        var averageAge = active.Count == 0
            ? "n/a"
            : active.Average(player => State.PersonAge(player.PersonId) ?? player.Age ?? 0).ToString("0.0");
        var injured = active.Count(player => !State.InjuryStatus(player.PersonId).Equals("Available", StringComparison.OrdinalIgnoreCase));
        var cap = State.SalaryCap;

        grid.Children.Add(UiPresentation.UiMetricCard("Roster", State.RosterBreakdownTitle, State.RosterBreakdownSecondary));
        grid.Children.Add(UiPresentation.UiMetricCard("Average Age", averageAge, State.RosterAgeBreakdown));
        grid.Children.Add(UiPresentation.UiMetricCard("Player Payroll", cap.IsEnabled ? $"{cap.CapUsed:C0}" : "Budget league", cap.IsEnabled ? $"{cap.CapRemaining:C0} remaining" : "Salary cap disabled"));
        grid.Children.Add(UiPresentation.UiMetricCard("Health", $"{injured} injured", $"{State.ContractDecisionCount} contract decisions"));
        grid.Children.Add(UiPresentation.UiMetricCard("Prospect Pool", $"{BuildProspectRows().Count} tracked", $"{State.ScoutingReportCount} scouting reports"));
        grid.Children.Add(UiPresentation.UiMetricCard("Chemistry", State.LineChemistryReport.Overall.Score.Grade.ToString(), State.LineChemistryReport.Overall.Recommendation));
        grid.Children.Add(UiPresentation.UiMetricCard("Record", State.TeamRecordText, State.NextGame is null ? "No next game scheduled" : $"Next game: {State.NextGame.AwayOrganizationId} at {State.NextGame.HomeOrganizationId}"));
        grid.Children.Add(UiPresentation.UiMetricCard("Top Need", BuildHockeyOperationsTopNeed(), $"{State.PendingDecisionCount} pending decisions"));
        return grid;
    }

    private string BuildHockeyOperationsTopNeed()
    {
        var active = State.Snapshot.Roster.ActivePlayers;
        if (active.Count(player => player.Position == RosterPosition.Goalie) < 2)
        {
            return "Goalie depth";
        }

        if (active.Count(player => player.Position == RosterPosition.Defense) < 6)
        {
            return "Defense depth";
        }

        if (State.ContractDecisionCount > 0)
        {
            return "Contract decisions";
        }

        return State.RosterWarningCount > 0 ? "Roster compliance" : "Depth balance";
    }

    private UIElement BuildHockeyOperationsActiveView(IReadOnlyList<SelectablePersonRow> rows, SelectablePersonRow? selected)
    {
        var panel = new StackPanel();
        panel.Children.Add(UiPresentation.UiSectionHeader($"{_commandCenterView} - {_commandCenterSource}"));
        panel.Children.Add(_commandCenterView switch
        {
            "Lineup" => BuildHockeyOperationsLineupBoard(),
            "Depth Chart" => BuildHockeyOperationsDepthChartBoard(),
            "Prospects" => BuildHockeyOperationsProspectPipeline(rows),
            "Development" => BuildHockeyOperationsDevelopmentBoard(rows),
            "Contracts" => BuildHockeyOperationsContractBoard(rows),
            "Scouting" => BuildHockeyOperationsScoutingBoard(rows),
            "Roster Transactions" => BuildHockeyOperationsTransactionBoard(rows, selected),
            "Tactics" => BuildHockeyOperationsTacticsBoard(),
            "Special Teams" => BuildHockeyOperationsSpecialTeamsBoard(),
            _ => BuildHockeyOperationsRosterOverview(rows)
        });
        return panel;
    }

    private UIElement BuildHockeyOperationsRosterOverview(IReadOnlyList<SelectablePersonRow> rows)
    {
        if (rows.Count == 0)
        {
            return UiPresentation.UiEmptyState("No roster cards", "No players match the current source and filters.");
        }

        var grid = new UniformGrid { Columns = 2 };
        foreach (var row in rows.Take(10))
        {
            grid.Children.Add(BuildHockeyOperationsPlayerCard(row));
        }

        return grid;
    }

    private UIElement BuildHockeyOperationsPlayerCard(SelectablePersonRow row)
    {
        var panel = new StackPanel();
        panel.Children.Add(UiPresentation.UiPersonLink(row.Name, () => OpenUniversalPersonCard(row.PersonId)));
        panel.Children.Add(UiPresentation.BadgeRow(
            (RowPositionText(row.PersonId), "info"),
            ($"Age {State.PersonAge(row.PersonId)?.ToString() ?? "unknown"}", "neutral"),
            (State.InjuryStatus(row.PersonId), StatusSemantic(State.InjuryStatus(row.PersonId))),
            (State.DevelopmentTrend(row.PersonId), StatusSemantic(State.DevelopmentTrend(row.PersonId)))));
        panel.Children.Add(new TextBlock { Text = State.RatingText(row.PersonId), FontWeight = FontWeights.SemiBold, Foreground = UiTheme.Text });
        panel.Children.Add(new TextBlock { Text = $"{State.CurrentLineupRole(row.PersonId)} | {State.CurrentLinePair(row.PersonId)}", TextWrapping = TextWrapping.Wrap, Foreground = UiTheme.MutedText });
        panel.Children.Add(new TextBlock { Text = State.ContractRightsStatus(row.PersonId), TextWrapping = TextWrapping.Wrap, Foreground = UiTheme.MutedText });
        panel.Children.Add(new TextBlock { Text = State.WaiverStatusText(row.PersonId), TextWrapping = TextWrapping.Wrap, Foreground = UiTheme.MutedText });
        return UiPresentation.UiPersonCard(panel);
    }

    private UIElement BuildHockeyOperationsLineupBoard()
    {
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var forwards = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
        forwards.Children.Add(UiPresentation.UiSectionHeader("Forwards"));
        for (var line = 1; line <= 4; line++)
        {
            forwards.Children.Add(BuildLineUnitCard($"Line {line}", ForwardLineSlots(line), $"forward-line:{line}"));
        }

        var right = new StackPanel();
        right.Children.Add(UiPresentation.UiSectionHeader("Defense"));
        for (var pair = 1; pair <= 3; pair++)
        {
            right.Children.Add(BuildLineUnitCard($"Pair {pair}", DefensePairSlots(pair), $"defense-pair:{pair}"));
        }

        right.Children.Add(UiPresentation.UiSectionHeader("Goalies"));
        right.Children.Add(BuildLineUnitCard("Goalies", new[] { LineupSlot.Starter, LineupSlot.Backup }, "goalie-depth"));
        Grid.SetColumn(forwards, 0);
        Grid.SetColumn(right, 1);
        root.Children.Add(forwards);
        root.Children.Add(right);
        return root;
    }

    private UIElement BuildLineUnitCard(string title, IReadOnlyList<LineupSlot> slots, string unitId)
    {
        var panel = new StackPanel();
        var chemistry = State.LineChemistryUnit(unitId);
        panel.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, Foreground = UiTheme.Text });
        panel.Children.Add(UiPresentation.BadgeRow(
            (chemistry is null ? "Chemistry: n/a" : $"Chemistry {chemistry.Score.Grade}", chemistry is null ? "neutral" : ChemistrySemantic(chemistry.Score.Grade.ToString())),
            (chemistry?.Recommendation ?? "Coach has no line note yet.", "info")));
        var slotGrid = new UniformGrid { Columns = slots.Count, Margin = new Thickness(0, 4, 0, 0) };
        foreach (var slot in slots)
        {
            slotGrid.Children.Add(BuildLineSlotCard(slot));
        }

        panel.Children.Add(slotGrid);
        return UiPresentation.Card(panel);
    }

    private UIElement BuildLineSlotCard(LineupSlot slot)
    {
        var playerName = State.LineupSlotPlayerText(slot);
        var assignment = State.CurrentLineup.Assignments.FirstOrDefault(item => item.Slot == slot);
        var personId = assignment?.PersonId;
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = LineupDisplay.SlotLabel(slot), FontSize = UiTypography.Small, Foreground = UiTheme.MutedText });
        if (personId is null)
        {
            panel.Children.Add(new TextBlock { Text = "Open", FontWeight = FontWeights.SemiBold, Foreground = UiTheme.Attention });
            panel.Children.Add(new TextBlock { Text = $"{State.EligibleLineupReplacements(slot).Count} eligible replacements", Foreground = UiTheme.MutedText, TextWrapping = TextWrapping.Wrap });
        }
        else
        {
            panel.Children.Add(UiPresentation.UiPersonLink(playerName, () => OpenUniversalPersonCard(personId)));
            panel.Children.Add(new TextBlock { Text = $"{State.RatingText(personId)} | {State.CurrentLineupRole(personId)}", TextWrapping = TextWrapping.Wrap, Foreground = UiTheme.Text });
            panel.Children.Add(new TextBlock { Text = $"{State.InjuryStatus(personId)} | {State.PromiseStatusText(personId)}", TextWrapping = TextWrapping.Wrap, Foreground = UiTheme.MutedText });
        }

        return new Border
        {
            Background = UiTheme.SurfaceAlt,
            BorderBrush = UiTheme.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 0, 8, 0),
            Child = panel
        };
    }

    private static IReadOnlyList<LineupSlot> ForwardLineSlots(int line) =>
        line switch
        {
            1 => new[] { LineupSlot.Line1LW, LineupSlot.Line1C, LineupSlot.Line1RW },
            2 => new[] { LineupSlot.Line2LW, LineupSlot.Line2C, LineupSlot.Line2RW },
            3 => new[] { LineupSlot.Line3LW, LineupSlot.Line3C, LineupSlot.Line3RW },
            _ => new[] { LineupSlot.Line4LW, LineupSlot.Line4C, LineupSlot.Line4RW }
        };

    private static IReadOnlyList<LineupSlot> DefensePairSlots(int pair) =>
        pair switch
        {
            1 => new[] { LineupSlot.Pair1LD, LineupSlot.Pair1RD },
            2 => new[] { LineupSlot.Pair2LD, LineupSlot.Pair2RD },
            _ => new[] { LineupSlot.Pair3LD, LineupSlot.Pair3RD }
        };

    private UIElement BuildHockeyOperationsDepthChartBoard()
    {
        var grid = new UniformGrid { Columns = 3 };
        foreach (var position in new[] { "C", "LW", "RW", "D", "G", "Future" })
        {
            var rows = BuildCommandCenterRows()
                .Where(row => position == "Future" ? row.Kind.Contains("Prospect", StringComparison.OrdinalIgnoreCase) : RowPositionText(row.PersonId) == position)
                .Take(6)
                .ToArray();
            grid.Children.Add(BuildHockeyOperationsGroupCard(position == "D" ? "Defense" : position, rows, row => $"{State.RatingText(row.PersonId)} | {State.CurrentLinePair(row.PersonId)}"));
        }

        return grid;
    }

    private UIElement BuildHockeyOperationsProspectPipeline(IReadOnlyList<SelectablePersonRow> rows)
    {
        var grid = new UniformGrid { Columns = 2 };
        foreach (var group in new[] { "NHL Ready", "AHL", "Junior", "Unsigned Rights", "Long-Term Project", "At Risk", "Blocked" })
        {
            var groupRows = rows
                .Where(row => ProspectPipelineGroup(row.PersonId).Equals(group, StringComparison.Ordinal))
                .Take(5)
                .ToArray();
            grid.Children.Add(BuildHockeyOperationsGroupCard(group, groupRows, row => $"{State.RatingText(row.PersonId)} | {State.ScoutingConfidenceText(row.PersonId)} | {State.DevelopmentStageText(row.PersonId)}"));
        }

        return grid;
    }

    private string ProspectPipelineGroup(string personId)
    {
        var contract = State.ContractRightsStatus(personId);
        var role = State.CurrentLineupRole(personId);
        var trend = State.DevelopmentTrend(personId);
        if (role.Contains("Top", StringComparison.OrdinalIgnoreCase) || role.Contains("Line", StringComparison.OrdinalIgnoreCase))
        {
            return "NHL Ready";
        }

        if (contract.Contains("rights", StringComparison.OrdinalIgnoreCase) || contract.Contains("unsigned", StringComparison.OrdinalIgnoreCase))
        {
            return "Unsigned Rights";
        }

        if (State.RegionTeamText(personId).Contains("junior", StringComparison.OrdinalIgnoreCase))
        {
            return "Junior";
        }

        if (trend.Contains("risk", StringComparison.OrdinalIgnoreCase) || State.InjuryStatus(personId) != "Available")
        {
            return "At Risk";
        }

        return "Long-Term Project";
    }

    private UIElement BuildHockeyOperationsDevelopmentBoard(IReadOnlyList<SelectablePersonRow> rows)
    {
        var grid = new UniformGrid { Columns = 2 };
        grid.Children.Add(BuildHockeyOperationsGroupCard("Biggest Risers", rows.Where(row => State.DevelopmentTrend(row.PersonId).Contains("improv", StringComparison.OrdinalIgnoreCase)).Take(6).ToArray(), row => $"{State.RatingText(row.PersonId)} | {State.LineupDevelopmentImpactText(row.PersonId)}"));
        grid.Children.Add(BuildHockeyOperationsGroupCard("Plateau / Risk Watch", rows.Where(row => State.DevelopmentTrend(row.PersonId).Contains("plateau", StringComparison.OrdinalIgnoreCase) || State.InjuryStatus(row.PersonId) != "Available").Take(6).ToArray(), row => $"{State.DevelopmentTrend(row.PersonId)} | {State.MedicalReportText(row.PersonId)}"));
        grid.Children.Add(BuildHockeyOperationsGroupCard("Training Focus", rows.Take(6).ToArray(), row => $"{State.DevelopmentStageText(row.PersonId)} | {State.DevelopmentPlanText(row.PersonId)}"));
        grid.Children.Add(BuildHockeyOperationsGroupCard("Coach Recommendations", rows.Take(6).ToArray(), row => State.PlayerCoachFitText(row.PersonId)));
        return grid;
    }

    private UIElement BuildHockeyOperationsContractBoard(IReadOnlyList<SelectablePersonRow> rows)
    {
        var grid = new UniformGrid { Columns = 2 };
        foreach (var group in new[] { "Expiring This Year", "RFA", "UFA", "Long-Term Commitments", "Entry-Level Contracts", "Arbitration", "Offer Sheets", "Buyout Candidates" })
        {
            var groupRows = rows.Where(row => ContractGroup(row.PersonId) == group).Take(5).ToArray();
            grid.Children.Add(BuildHockeyOperationsGroupCard(group, groupRows, row => State.ContractRightsStatus(row.PersonId)));
        }

        return grid;
    }

    private string ContractGroup(string personId)
    {
        var text = State.ContractRightsStatus(personId);
        if (text.Contains("arbitration", StringComparison.OrdinalIgnoreCase)) return "Arbitration";
        if (text.Contains("offer", StringComparison.OrdinalIgnoreCase)) return "Offer Sheets";
        if (State.CanCalculateBuyout(personId) || State.CanConfirmBuyout(personId)) return "Buyout Candidates";
        if (text.Contains("RFA", StringComparison.OrdinalIgnoreCase) || text.Contains("Restricted", StringComparison.OrdinalIgnoreCase)) return "RFA";
        if (text.Contains("UFA", StringComparison.OrdinalIgnoreCase) || text.Contains("Unrestricted", StringComparison.OrdinalIgnoreCase)) return "UFA";
        if (text.Contains("expires", StringComparison.OrdinalIgnoreCase) || text.Contains("expired", StringComparison.OrdinalIgnoreCase)) return "Expiring This Year";
        if (text.Contains("JuniorPlayerAgreement", StringComparison.OrdinalIgnoreCase) || text.Contains("Entry", StringComparison.OrdinalIgnoreCase)) return "Entry-Level Contracts";
        return "Long-Term Commitments";
    }

    private UIElement BuildHockeyOperationsScoutingBoard(IReadOnlyList<SelectablePersonRow> rows)
    {
        var grid = new UniformGrid { Columns = 2 };
        grid.Children.Add(BuildHockeyOperationsGroupCard("High Confidence", rows.Where(row => State.ScoutingConfidenceText(row.PersonId).Contains("High", StringComparison.OrdinalIgnoreCase)).Take(6).ToArray(), row => $"{State.RatingText(row.PersonId)} | {State.ScoutingReportHeadline(row.PersonId)}"));
        grid.Children.Add(BuildHockeyOperationsGroupCard("Needs Another Look", rows.Where(row => !State.ScoutingConfidenceText(row.PersonId).Contains("High", StringComparison.OrdinalIgnoreCase)).Take(6).ToArray(), row => $"{State.ScoutingConfidenceText(row.PersonId)} | {State.ScoutingKnowledgeText(row.PersonId)}"));
        grid.Children.Add(BuildHockeyOperationsGroupCard("Disagreement / Watch", rows.Take(6).ToArray(), row => State.ScoutingComparisonText(row.PersonId)));
        grid.Children.Add(BuildHockeyOperationsGroupCard("Recommended Assignments", rows.Take(6).ToArray(), row => $"Next: assign scout | {State.AssignedScoutText(row.PersonId)}"));
        return grid;
    }

    private UIElement BuildHockeyOperationsTransactionBoard(IReadOnlyList<SelectablePersonRow> rows, SelectablePersonRow? selected)
    {
        var panel = new StackPanel();
        if (selected is not null)
        {
            panel.Children.Add(UiPresentation.UiAlertBanner($"{selected.Name}: {State.WaiverStatusText(selected.PersonId)} | {State.ContractRightsStatus(selected.PersonId)}", "info"));
        }

        panel.Children.Add(BuildHockeyOperationsGroupCard("Immediate Decisions", rows.Where(row => State.ContractRightsStatus(row.PersonId).Contains("decision", StringComparison.OrdinalIgnoreCase) || State.PendingDecisionCount > 0).Take(6).ToArray(), row => $"{State.ContractRightsStatus(row.PersonId)} | {State.WaiverStatusText(row.PersonId)}"));
        panel.Children.Add(BuildHockeyOperationsGroupCard("Waiver / Assignment Check", rows.Take(8).ToArray(), row => $"{State.WaiverStatusText(row.PersonId)} | roster impact: manual GM approval required"));
        return panel;
    }

    private UIElement BuildHockeyOperationsSpecialTeamsBoard()
    {
        var grid = new UniformGrid { Columns = 2 };
        foreach (var unit in State.CurrentGameUsage.SpecialTeams.PowerPlayUnits)
        {
            grid.Children.Add(BuildSpecialTeamUnitCard($"PP{unit.UnitNumber}", new[] { unit.LeftWing?.PlayerName, unit.Center?.PlayerName, unit.RightWing?.PlayerName, unit.QuarterbackDefense?.PlayerName, unit.NetFrontOrSecondDefense?.PlayerName }, "Power play personnel group. Use offensive skill, puck movement, and net-front balance."));
        }

        foreach (var unit in State.CurrentGameUsage.SpecialTeams.PenaltyKillUnits)
        {
            grid.Children.Add(BuildSpecialTeamUnitCard($"PK{unit.UnitNumber}", new[] { unit.LeftWing?.PlayerName, unit.RightWing?.PlayerName, unit.LeftDefense?.PlayerName, unit.RightDefense?.PlayerName }, "Penalty kill personnel group. Use defensive awareness, trust, and veteran support."));
        }

        grid.Children.Add(BuildSpecialTeamUnitCard("Extra Attacker", State.CurrentGameUsage.SpecialTeams.ExtraAttacker.Players.Select(player => player.PlayerName).ToArray(), State.CurrentGameUsage.SpecialTeams.ExtraAttacker.Summary));
        grid.Children.Add(BuildSpecialTeamUnitCard("Shootout", State.CurrentGameUsage.SpecialTeams.ShootoutOrder.Shooters.Select(player => player.PlayerName).ToArray(), State.CurrentGameUsage.SpecialTeams.ShootoutOrder.Summary));
        return grid;
    }

    private UIElement BuildSpecialTeamUnitCard(string title, IEnumerable<string?> names, string summary)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, Foreground = UiTheme.Text });
        panel.Children.Add(new TextBlock { Text = string.Join(" | ", names.Where(name => !string.IsNullOrWhiteSpace(name)).DefaultIfEmpty("Open")), TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = summary, TextWrapping = TextWrapping.Wrap, Foreground = UiTheme.MutedText });
        return UiPresentation.Card(panel);
    }

    private UIElement BuildHockeyOperationsTacticsBoard()
    {
        var tactics = State.CurrentTactics;
        var grid = new UniformGrid { Columns = 2 };
        grid.Children.Add(BuildTextCard("Attack", $"Style {TacticsService.Display(tactics.Style)} | Shots {TacticsService.Display(tactics.Settings.ShotPreference)}", tactics.FitReport.Summary));
        grid.Children.Add(BuildTextCard("Transition", $"Forecheck {TacticsService.Display(tactics.Settings.Forecheck)} | Breakout {TacticsService.Display(tactics.Settings.Breakout)}", tactics.FitReport.CoachRecommendation));
        grid.Children.Add(BuildTextCard("Defense", $"NZ {TacticsService.Display(tactics.Settings.NeutralZone)} | DZ {TacticsService.Display(tactics.Settings.DefensiveZone)}", $"Risk {tactics.Settings.RiskLevel} | Physicality {tactics.Settings.Physicality}"));
        grid.Children.Add(BuildTextCard("Special Teams", $"PP {TacticsService.Display(tactics.Settings.PowerPlayStyle)} | PK {TacticsService.Display(tactics.Settings.PenaltyKillStyle)}", tactics.Summary));
        return grid;
    }

    private UIElement BuildTextCard(string title, string headline, string detail)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, Foreground = UiTheme.Text });
        panel.Children.Add(new TextBlock { Text = headline, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = detail, TextWrapping = TextWrapping.Wrap, Foreground = UiTheme.MutedText });
        return UiPresentation.Card(panel);
    }

    private UIElement BuildHockeyOperationsGroupCard(string title, IReadOnlyList<SelectablePersonRow> rows, Func<SelectablePersonRow, string> detail)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, Foreground = UiTheme.Text });
        if (rows.Count == 0)
        {
            panel.Children.Add(new TextBlock { Text = "No players in this group.", Foreground = UiTheme.MutedText, Margin = new Thickness(0, 6, 0, 0) });
        }
        else
        {
            foreach (var row in rows)
            {
                var item = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
                item.Children.Add(UiPresentation.UiPersonLink(row.Name, () => OpenUniversalPersonCard(row.PersonId)));
                item.Children.Add(new TextBlock { Text = $"{RowPositionText(row.PersonId)} | {detail(row)}", TextWrapping = TextWrapping.Wrap, Foreground = UiTheme.MutedText });
                panel.Children.Add(item);
            }
        }

        return UiPresentation.Card(panel);
    }

    private string RowPositionText(string personId)
    {
        var position = State.PersonPosition(personId);
        return position switch
        {
            RosterPosition.Center => "C",
            RosterPosition.LeftWing => "LW",
            RosterPosition.RightWing => "RW",
            RosterPosition.Defense => "D",
            RosterPosition.Goalie => "G",
            _ => "Unknown"
        };
    }

    private static string ChemistrySemantic(string grade) =>
        grade.Contains("Excellent", StringComparison.OrdinalIgnoreCase) || grade.Contains("Good", StringComparison.OrdinalIgnoreCase)
            ? "positive"
            : grade.Contains("Poor", StringComparison.OrdinalIgnoreCase) || grade.Contains("Problem", StringComparison.OrdinalIgnoreCase)
                ? "critical"
                : "neutral";

    private void RenderCommandCenterCenter(IReadOnlyList<SelectablePersonRow> rows, SelectablePersonRow? selected)
    {
        if (_commandCenterCenterPanel is null)
        {
            return;
        }

        _commandCenterCenterPanel.Children.Clear();
        _commandCenterCenterPanel.Children.Add(BuildHockeyOperationsTeamSummaryStrip());
        _commandCenterCenterPanel.Children.Add(BuildHockeyOperationsActiveView(rows, selected));

        AddSubHeader(_commandCenterCenterPanel, $"{_commandCenterSource} Players");
        _commandCenterPlayerList = new ListBox
        {
            ItemsSource = rows,
            SelectedItem = selected,
            MinHeight = 300,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            ItemTemplate = UiPresentation.PersonRowTemplate(),
            ContextMenu = BuildCommandCenterContextMenu()
        };
        _commandCenterPlayerList.SelectionChanged += (_, _) =>
        {
            if (_commandCenterPlayerList.SelectedItem is SelectablePersonRow row)
            {
                _selectedCommandCenterPersonId = row.PersonId;
                RenderCommandCenterPlayerCard(row);
            }
        };
        _commandCenterPlayerList.MouseDoubleClick += (_, _) =>
        {
            if (_commandCenterPlayerList.SelectedItem is SelectablePersonRow row && IsLikelyPersonRow(row))
            {
                OpenUniversalPersonCard(row.PersonId);
            }
        };
        _commandCenterCenterPanel.Children.Add(_commandCenterPlayerList);

        if (rows.Count == 0)
        {
            _commandCenterCenterPanel.Children.Add(UiPresentation.UiEmptyState("No players match", "No players match this source, search term, and position filter."));
        }
    }

    private IReadOnlyList<string> BuildCommandCenterViewLines()
    {
        var lines = new List<string>();
        switch (_commandCenterView)
        {
            case "Lines":
                lines.AddRange(BuildLineup().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
                break;
            case "Development":
                lines.Add("Development focus, role fit, and ice-time risk for the selected operations source.");
                lines.AddRange(BuildCommandCenterRows()
                    .Take(10)
                    .Select(row => $"{row.Name}: {State.DevelopmentStageText(row.PersonId)} | {State.DevelopmentTrend(row.PersonId)} | {State.LineupDevelopmentImpactText(row.PersonId)}"));
                break;
            case "Contracts":
                lines.AddRange(BuildContractsWorkspace().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
                break;
            case "Scouting":
                lines.AddRange(BuildScouting().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
                break;
            case "Trade":
                lines.Add("Review trade candidates beside roster impact and budget context. Select a player for dossier, contract, and trade actions.");
                lines.AddRange(BuildTradeRows().Take(8).Select(row => $"{row.Name}: {row.Primary} | {row.Secondary}"));
                break;
            case "Free Agency":
                lines.Add("Free-agent market view with fit, ask, agent, and interest context.");
                lines.AddRange(BuildFreeAgentRows().Take(8).Select(row => $"{row.Name}: {row.Primary} | {row.Secondary}"));
                break;
            default:
                lines.Add("Depth chart and roster breakdown.");
                lines.Add($"Roster count: {State.RosterBreakdownTitle}");
                lines.Add($"Position breakdown: {State.RosterBreakdownSecondary}");
                lines.Add($"Age mix: {State.RosterAgeBreakdown}");
                lines.Add($"Contracts: {State.RosterContractBreakdown}");
                lines.AddRange(BuildCommandCenterDepthChartLines());
                break;
        }

        return lines;
    }

    private IEnumerable<string> BuildCommandCenterDepthChartLines()
    {
        var active = State.Snapshot.Roster.ActivePlayers;
        foreach (var group in active.GroupBy(player => player.Position).OrderBy(group => group.Key.ToString(), StringComparer.Ordinal))
        {
            yield return $"{group.Key}: {string.Join(", ", group.Take(8).Select(player => $"{FindPersonName(player.PersonId)} ({State.CurrentLineupRole(player.PersonId)})"))}";
        }
    }

    private void RenderCommandCenterPlayerCard(SelectablePersonRow? row)
    {
        if (_commandCenterPlayerCard is null)
        {
            return;
        }

        _commandCenterPlayerCard.Children.Clear();
        if (row is null)
        {
            _commandCenterPlayerCard.Children.Add(EmptyDetail("Selected Player", "Select a player, prospect, free agent, or trade target."));
            return;
        }

        var tab = CommandCenterTabForRow(row);
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = row.Name,
            FontSize = UiTypography.CardTitle,
            FontWeight = FontWeights.SemiBold,
            Foreground = UiTheme.Text,
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"{RowPositionText(row.PersonId)} | Age {State.PersonAge(row.PersonId)?.ToString() ?? "unknown"} | {State.RegionTeamText(row.PersonId)}",
            Foreground = UiTheme.MutedText,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 8)
        });
        panel.Children.Add(UiPresentation.BadgeRow(
            (State.RatingText(row.PersonId), "info"),
            (State.CurrentLineupRole(row.PersonId), "neutral"),
            (State.InjuryStatus(row.PersonId), StatusSemantic(State.InjuryStatus(row.PersonId))),
            (State.ScoutingConfidenceText(row.PersonId), ConfidenceSemantic(State.ScoutingConfidenceText(row.PersonId)))));

        panel.Children.Add(UiPresentation.UiInfoRow("Line / Pair", State.CurrentLinePair(row.PersonId)));
        panel.Children.Add(UiPresentation.UiInfoRow("Contract", State.ContractRightsStatus(row.PersonId)));
        panel.Children.Add(UiPresentation.UiInfoRow("Development", $"{State.DevelopmentStageText(row.PersonId)} | {State.DevelopmentTrend(row.PersonId)}"));
        panel.Children.Add(UiPresentation.UiInfoRow("Waiver / Rights", State.WaiverStatusText(row.PersonId)));
        panel.Children.Add(BuildCommandCenterSection("Ratings", State.RatingContextText(row.PersonId), expanded: true));
        panel.Children.Add(BuildCommandCenterSection("Development", $"{State.DevelopmentPlanText(row.PersonId)}\n{State.LineupDevelopmentImpactText(row.PersonId)}"));
        panel.Children.Add(BuildCommandCenterSection("Contract", State.PlayerContractDetailsText(row.PersonId)));
        panel.Children.Add(BuildCommandCenterSection("Usage", $"{State.CurrentLinePair(row.PersonId)}\n{State.PlayerCoachFitText(row.PersonId)}\n{State.RoleSatisfactionText(row.PersonId)}"));
        panel.Children.Add(BuildCommandCenterSection("Scouting", $"{State.ScoutingKnowledgeText(row.PersonId)}\n{State.ScoutingReportHeadline(row.PersonId)}\n{State.ScoutingComparisonText(row.PersonId)}"));
        panel.Children.Add(BuildCommandCenterSection("Medical", State.MedicalReportText(row.PersonId)));
        panel.Children.Add(BuildCommandCenterSection("Relationships", BuildCommandCenterRelationshipText(row.PersonId)));
        panel.Children.Add(BuildCommandCenterSection("Career", BuildCommandCenterCareerText(row.PersonId)));
        AddSubHeader(panel, "Context Actions");
        AddActions(panel, BuildCommandCenterActionButtons(row, tab).ToArray());
        _commandCenterPlayerCard.Children.Add(panel);
    }

    private static Expander BuildCommandCenterSection(string title, string text, bool expanded = false)
    {
        return UiPresentation.UiExpandableSection(title, new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(text) ? "No information available yet." : text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = UiTheme.Text
        }, expanded);
    }

    private string BuildCommandCenterBioText(string personId)
    {
        var draftEntry = State.Snapshot.DraftBoard.Entries.FirstOrDefault(entry => entry.ProspectPersonId == personId);
        if (draftEntry?.Bio is not null)
        {
            var bio = draftEntry.Bio;
            return $"{State.DraftQuickScan(draftEntry)} | born {bio.BirthYear} | {bio.Hometown}, {bio.ProvinceState}, {bio.Country} | {bio.CharacterSummary} | {bio.PotentialLineupProjection}";
        }

        var freeAgent = State.FreeAgentFor(personId);
        if (freeAgent is not null)
        {
            return $"{freeAgent.Position} | {freeAgent.ShootsCatches} | {freeAgent.HeightDisplay}, {freeAgent.WeightDisplay} | {freeAgent.Nationality} | {freeAgent.Hometown} | {freeAgent.PlayerType}";
        }

        return $"{State.PersonPosition(personId)} | age {State.PersonAge(personId)?.ToString() ?? "unknown"} | {State.RegionTeamText(personId)} | {State.PlayerType(personId)}";
    }

    private string BuildCommandCenterRelationshipText(string personId)
    {
        var profiles = State.RelationshipProfiles
            .Where(profile => string.Equals(profile.SourceId, personId, StringComparison.Ordinal) || string.Equals(profile.TargetId, personId, StringComparison.Ordinal))
            .Take(4)
            .ToArray();
        if (profiles.Length == 0)
        {
            return $"GM relationship: {State.RelationshipWithGm(personId)}/100.";
        }

        return string.Join(Environment.NewLine, profiles.Select(profile =>
        {
            var other = string.Equals(profile.SourceId, personId, StringComparison.Ordinal) ? profile.TargetName : profile.SourceName;
            return $"{other}: {profile.Label}, trust {profile.Trust}, respect {profile.Respect}, loyalty {profile.Loyalty}, conflict {profile.Conflict}, communication {profile.CommunicationQuality}, trend {profile.Trend}. {profile.Summary}";
        }));
    }

    private string BuildCommandCenterCareerText(string personId)
    {
        var lines = new List<string>();
        lines.Add(State.CareerStatSummary(personId));
        foreach (var timeline in State.ScenarioSnapshot.PlayerCareerTimelines.Where(timeline => timeline.PersonId == personId).Take(1))
        {
            lines.AddRange(timeline.Entries.Take(4));
        }

        lines.AddRange(State.ScenarioSnapshot.CareerTimeline.ForPerson(personId)
            .Take(4)
            .Select(entry => $"{entry.Date:yyyy-MM-dd}: {entry.Title} - {entry.Description}"));
        return string.Join(Environment.NewLine, lines.Where(line => !string.IsNullOrWhiteSpace(line)).DefaultIfEmpty("No career timeline tracked yet."));
    }

    private IEnumerable<Button> BuildCommandCenterActionButtons(SelectablePersonRow row, string tab)
    {
        yield return CreateDetailButton("Dossier", () => OpenDossierFor(row.PersonId));
        yield return CreateDetailButton("Assign Line", () => SelectWorkspaceScreen("Hockey Operations", "Lineup"));
        yield return CreateDetailButton("Development", () => MessageBox.Show(State.DevelopmentReviewText(row.PersonId), "Development Review", MessageBoxButton.OK, MessageBoxImage.Information), CommandCenterHasDevelopmentProfile(row.PersonId), "No development profile is tracked for this person");
        yield return CreateDetailButton("Contract", () => SelectWorkspaceScreen("Hockey Operations", "Contracts"));
        yield return CreateDetailButton("Scout", () => ShowScoutAssignmentDialog(row.PersonId), State.AvailableScoutProfiles.Count > 0);
        yield return CreateDetailButton("Trade", () => SelectWorkspaceScreen("Hockey Operations", "Trades"));
        yield return CreateDetailButton("History", () => MessageBox.Show(BuildCommandCenterCareerText(row.PersonId), "Player History", MessageBoxButton.OK, MessageBoxImage.Information));
        foreach (var button in BuildPlayerActionButtons(tab, row))
        {
            yield return button;
        }
    }

    private bool CommandCenterHasDevelopmentProfile(string personId) =>
        State.Snapshot.DevelopmentProfiles.Any(profile => string.Equals(profile.PersonId, personId, StringComparison.Ordinal));

    private ContextMenu BuildCommandCenterContextMenu()
    {
        var menu = new ContextMenu();
        AddCommandCenterMenuItem(menu, "View Dossier", row => OpenDossierFor(row.PersonId));
        AddCommandCenterMenuItem(menu, "Assign Line", _ => SelectWorkspaceScreen("Hockey Operations", "Lineup"));
        AddCommandCenterMenuItem(menu, "Development Review", row =>
        {
            if (CommandCenterHasDevelopmentProfile(row.PersonId))
            {
                MessageBox.Show(State.DevelopmentReviewText(row.PersonId), "Development Review", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("No development profile is tracked for this person.", "Development Review", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        });
        AddCommandCenterMenuItem(menu, "Contract View", _ => SelectWorkspaceScreen("Hockey Operations", "Contracts"));
        AddCommandCenterMenuItem(menu, "Assign Scout", row => ShowScoutAssignmentDialog(row.PersonId));
        AddCommandCenterMenuItem(menu, "Trade View", _ => SelectWorkspaceScreen("Hockey Operations", "Trades"));
        return menu;
    }

    private void AddCommandCenterMenuItem(ContextMenu menu, string label, Action<SelectablePersonRow> action)
    {
        var item = new MenuItem { Header = label };
        item.Click += (_, _) =>
        {
            if (_commandCenterPlayerList?.SelectedItem is SelectablePersonRow row)
            {
                action(row);
            }
        };
        menu.Items.Add(item);
    }

    private static string CommandCenterTabForRow(SelectablePersonRow row) =>
        row.Kind switch
        {
            "FreeAgent" => "Free Agents",
            "TradeBlock" => "Trades",
            "ScoutingProspect" => "Scouting",
            "DraftBoard" => "Draft Board",
            "Prospect" => "Prospect List",
            "CampPlayer" => "Training Camp",
            _ => "Roster"
        };

    private void RefreshOrganizationCommandCenter()
    {
        if (_organizationCommandCenterPanel is null || _organizationCommandStaffCard is null)
        {
            return;
        }

        var rows = BuildOrganizationCommandRows();
        var selected = rows.FirstOrDefault(row => string.Equals(row.PersonId, _selectedOrganizationStaffPersonId, StringComparison.Ordinal))
            ?? rows.FirstOrDefault();
        _selectedOrganizationStaffPersonId = selected?.PersonId;

        RenderOrganizationCommandCenter(rows, selected);
        RenderOrganizationStaffCard(selected);
    }

    private IReadOnlyList<SelectablePersonRow> BuildOrganizationCommandRows()
    {
        if (_organizationCommandDepartment == "Owner")
        {
            return new[]
            {
                new SelectablePersonRow(
                    "owner",
                    State.Snapshot.Owner.Name,
                    "Owner",
                    $"{State.Snapshot.Owner.Archetype} owner",
                    $"Confidence {State.OwnerOffice.Confidence.Confidence} | Job security {State.OwnerOffice.JobSecurity.Level}",
                    State.OwnerOffice.Personality.Vision)
            };
        }

        if (_organizationCommandDepartment == "Finance" || _organizationCommandDepartment == "Facilities")
        {
            return Array.Empty<SelectablePersonRow>();
        }

        var rows = StaffRowsForOrganizationDepartment(_organizationCommandDepartment).ToList();
        rows.AddRange(VacancyRowsForOrganizationDepartment(_organizationCommandDepartment));
        return rows
            .GroupBy(row => row.PersonId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    private IEnumerable<SelectablePersonRow> StaffRowsForOrganizationDepartment(string department)
    {
        return State.StaffProfiles
            .Where(profile => MatchesOrganizationDepartment(profile, department))
            .OrderBy(profile => OrganizationRoleSort(profile.CurrentRole))
            .ThenBy(profile => profile.Name, StringComparer.Ordinal)
            .Select(profile => new SelectablePersonRow(
                profile.PersonId,
                profile.Name,
                "Staff",
                $"{StaffRoles.Title(profile.CurrentRole)} | {profile.Department}",
                $"{profile.ContractStatus} | salary {profile.Salary.AnnualAmount:C0} | relationship {profile.RelationshipWithGm}/100",
                $"{profile.Chemistry.Summary} Focus: {profile.CurrentFocus}"));
    }

    private IEnumerable<SelectablePersonRow> VacancyRowsForOrganizationDepartment(string department)
    {
        return State.StaffVacancies
            .Where(vacancy => MatchesOrganizationDepartment(vacancy, department))
            .Select(vacancy => new SelectablePersonRow(
                $"vacancy:{vacancy.Role}",
                StaffRoles.Title(vacancy.Role),
                "Vacancy",
                $"Vacancy | {vacancy.Department}",
                $"{vacancy.Current}/{vacancy.Required} filled | max {vacancy.Maximum}",
                vacancy.Warning));
    }

    private static bool MatchesOrganizationDepartment(StaffOfficeProfile profile, string department) =>
        department switch
        {
            "Front Office" => profile.Department is StaffDepartment.Executive or StaffDepartment.Management,
            "Coaching" => profile.Department == StaffDepartment.Coaching && profile.CurrentRole is not StaffRole.DevelopmentCoach and not StaffRole.SkillsCoach and not StaffRole.StrengthCoach and not StaffRole.StrengthConditioningCoach,
            "Development" => profile.CurrentRole is StaffRole.DevelopmentCoach or StaffRole.SkillsCoach or StaffRole.StrengthCoach or StaffRole.StrengthConditioningCoach,
            "Scouting" => profile.Department == StaffDepartment.Scouting,
            "Medical" => profile.Department == StaffDepartment.Medical,
            "Equipment" => profile.Department == StaffDepartment.Equipment,
            _ => false
        };

    private static bool MatchesOrganizationDepartment(StaffVacancy vacancy, string department) =>
        department switch
        {
            "Front Office" => vacancy.Department is StaffDepartment.Executive or StaffDepartment.Management,
            "Coaching" => vacancy.Department == StaffDepartment.Coaching && vacancy.Role is not StaffRole.DevelopmentCoach and not StaffRole.SkillsCoach and not StaffRole.StrengthCoach and not StaffRole.StrengthConditioningCoach,
            "Development" => vacancy.Role is StaffRole.DevelopmentCoach or StaffRole.SkillsCoach or StaffRole.StrengthCoach or StaffRole.StrengthConditioningCoach,
            "Scouting" => vacancy.Department == StaffDepartment.Scouting,
            "Medical" => vacancy.Department == StaffDepartment.Medical,
            "Equipment" => vacancy.Department == StaffDepartment.Equipment,
            _ => false
        };

    private static int OrganizationRoleSort(StaffRole role) =>
        role switch
        {
            StaffRole.GeneralManager => 0,
            StaffRole.AssistantGM => 1,
            StaffRole.DirectorOfHockeyOperations => 2,
            StaffRole.HeadCoach => 10,
            StaffRole.AssistantCoach => 11,
            StaffRole.GoalieCoach or StaffRole.GoaltendingCoach => 12,
            StaffRole.DevelopmentCoach => 20,
            StaffRole.DirectorOfScouting => 30,
            StaffRole.HeadScout => 31,
            StaffRole.Scout or StaffRole.RegionalScout or StaffRole.AmateurScout or StaffRole.ProfessionalScout or StaffRole.EuropeanScout or StaffRole.GoaltendingScout => 32,
            StaffRole.HeadAthleticTherapist => 40,
            StaffRole.TeamDoctor => 41,
            StaffRole.AthleticTherapist or StaffRole.AssistantTrainer or StaffRole.Physiotherapist or StaffRole.MassageTherapist => 42,
            StaffRole.HeadEquipmentManager => 50,
            StaffRole.EquipmentManager => 51,
            StaffRole.AssistantEquipmentManager => 52,
            _ => 99
        };

    private void RenderOrganizationCommandCenter(IReadOnlyList<SelectablePersonRow> rows, SelectablePersonRow? selected)
    {
        if (_organizationCommandCenterPanel is null)
        {
            return;
        }

        _organizationCommandCenterPanel.Children.Clear();
        var header = CreateDetailPanel($"{_organizationCommandDepartment} Department", State.ScenarioSnapshot.Organization.Name);
        AddLine(header, "Owner mood", OwnerMoodText());
        AddLine(header, "Organization health", State.SeasonReadinessReport.OrganizationHealth);
        AddLine(header, "Budget", $"{State.BudgetOverview.Status} | {State.BudgetOverview.RemainingBudget:C0} remaining");
        AddLine(header, "Vacancies", State.StaffVacancySummary);
        _organizationCommandCenterPanel.Children.Add(header);

        AddOrganizationCommandCard(_organizationCommandCenterPanel, "Franchise Overview", BuildFranchiseOverviewLines());
        AddOrganizationCommandCard(_organizationCommandCenterPanel, "Current Organization Story", BuildOrganizationStoryLines());
        AddOrganizationCommandCard(_organizationCommandCenterPanel, "Media Coverage", BuildOrganizationMediaLines());
        AddOrganizationCommandCard(_organizationCommandCenterPanel, "Team Awards & Records", BuildOrganizationAwardRecordLines());
        AddOrganizationCommandCard(_organizationCommandCenterPanel, "Organization Needs", BuildOrganizationNeedsLines());
        AddOrganizationCommandCard(_organizationCommandCenterPanel, "Department Health", BuildDepartmentHealthLines(_organizationCommandDepartment));
        AddOrganizationCommandCard(_organizationCommandCenterPanel, "Organization Chart", BuildOrganizationChartLines());
        AddOrganizationCommandCard(_organizationCommandCenterPanel, "Financial Overview", BuildFinancialOverviewLines());
        AddOrganizationCommandCard(_organizationCommandCenterPanel, "Executive Report", BuildOrganizationReportLines());
        AddOrganizationCommandCard(_organizationCommandCenterPanel, "Action Center", BuildOrganizationActionLines());

        AddSubHeader(_organizationCommandCenterPanel, $"{_organizationCommandDepartment} People");
        _organizationCommandStaffList = new ListBox
        {
            ItemsSource = rows,
            SelectedItem = selected,
            MinHeight = 250,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            ItemTemplate = UiPresentation.PersonRowTemplate()
        };
        _organizationCommandStaffList.SelectionChanged += (_, _) =>
        {
            if (_organizationCommandStaffList.SelectedItem is SelectablePersonRow row)
            {
                _selectedOrganizationStaffPersonId = row.PersonId;
                RenderOrganizationStaffCard(row);
            }
        };
        _organizationCommandStaffList.MouseDoubleClick += (_, _) =>
        {
            if (_organizationCommandStaffList.SelectedItem is SelectablePersonRow row && IsLikelyPersonRow(row))
            {
                OpenUniversalPersonCard(row.PersonId);
            }
        };
        _organizationCommandCenterPanel.Children.Add(_organizationCommandStaffList);
        if (rows.Count == 0)
        {
            AddParagraph(_organizationCommandCenterPanel, _organizationCommandDepartment == "Facilities"
                ? "Facilities are a placeholder in this alpha. No facilities simulation has been added."
                : "No staff rows are assigned to this department yet.");
        }
    }

    private void AddOrganizationCommandCard(StackPanel parent, string title, IEnumerable<string> lines)
    {
        var content = new StackPanel();
        content.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(38, 58, 82)),
            Margin = new Thickness(0, 0, 0, 6)
        });
        foreach (var line in lines.Where(line => !string.IsNullOrWhiteSpace(line)).DefaultIfEmpty("No items."))
        {
            AddParagraph(content, line);
        }

        parent.Children.Add(new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(222, 229, 237)),
            Background = new SolidColorBrush(Color.FromRgb(250, 252, 255)),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 0, 0, 10),
            Child = content
        });
    }

    private IEnumerable<string> BuildFranchiseOverviewLines()
    {
        foreach (var line in new FranchiseIdentityService().BuildCommandCenterLines(State.ScenarioSnapshot, State.ScenarioSnapshot.Organization.OrganizationId))
        {
            yield return line;
        }

        var latestShift = State.PlayerFranchiseIdentity.IdentityShifts.OrderByDescending(shift => shift.Date).FirstOrDefault();
        yield return latestShift is null
            ? "Identity change: stable; no recent shift tracked."
            : $"Identity change: {latestShift.Date:yyyy-MM-dd} - {latestShift.VisibleExplanation}";
        yield return $"Franchise history: {State.PlayerFranchiseIdentity.History.PlayoffAppearances} playoff appearances, {State.PlayerFranchiseIdentity.History.Championships} championship(s), longest streak {State.PlayerFranchiseIdentity.History.LongestPlayoffStreak}.";
    }

    private IEnumerable<string> BuildOrganizationStoryLines()
    {
        foreach (var line in new StoryService().BuildOrganizationLines(State.ScenarioSnapshot))
        {
            yield return line;
        }
    }

    private IEnumerable<string> BuildOrganizationMediaLines()
    {
        foreach (var line in new MediaService().BuildOrganizationLines(State.ScenarioSnapshot, State.LeagueTransactions))
        {
            yield return line;
        }
    }

    private IEnumerable<string> BuildOrganizationAwardRecordLines()
    {
        var organizationId = State.ScenarioSnapshot.Organization.OrganizationId;
        var awards = State.ScenarioSnapshot.AwardHistory.Awards
            .Where(award => string.Equals(award.Winner.OrganizationId, organizationId, StringComparison.Ordinal))
            .OrderByDescending(award => award.SeasonYear)
            .Take(4)
            .ToArray();
        var records = State.ScenarioSnapshot.RecordBook.ForOrganization(organizationId)
            .Take(4)
            .ToArray();

        if (awards.Length == 0 && records.Length == 0)
        {
            yield return "No team awards or records have been tracked yet.";
            yield break;
        }

        foreach (var award in awards)
        {
            yield return $"Award: {award.SeasonYear} {Readable(award.AwardType)} - {award.Winner.RecipientName}";
        }

        foreach (var record in records)
        {
            yield return $"Record: {Readable(record.RecordType)} {record.Value} - {record.HolderName}";
        }
    }

    private IEnumerable<string> BuildOrganizationNeedsLines()
    {
        foreach (var vacancy in State.StaffVacancies.Take(4))
        {
            yield return $"Need {StaffRoles.Title(vacancy.Role)}: {vacancy.Warning}";
        }

        foreach (var grade in State.DepartmentGrades.Where(report => report.Score < 60).Take(3))
        {
            yield return $"Weak department: {grade.DepartmentName} is {grade.Grade} - {grade.Summary}";
        }

        if (State.BudgetOverview.RemainingBudget < 0)
        {
            yield return $"Budget issue: over budget by {Math.Abs(State.BudgetOverview.RemainingBudget):C0}.";
        }

        foreach (var item in State.ActionCenterItems.Where(item => item.Status == ActionCenterStatus.Open && item.Category is ActionCenterCategory.Staff or ActionCenterCategory.Owner or ActionCenterCategory.Budget).Take(3))
        {
            yield return $"{item.Priority}: {item.Title} - {item.Reason}";
        }
    }

    private IEnumerable<string> BuildDepartmentHealthLines(string department)
    {
        var grade = DepartmentGradeFor(department);
        yield return $"Grade: {grade.Grade} ({grade.Score}/100)";
        yield return grade.Summary;
        foreach (var evidence in grade.Evidence.Take(3))
        {
            yield return $"Evidence: {evidence}";
        }

        var staff = StaffRowsForOrganizationDepartment(department).ToArray();
        var vacancies = VacancyRowsForOrganizationDepartment(department).ToArray();
        yield return $"Staff Count: {staff.Length}";
        yield return $"Vacancies: {vacancies.Length}";
        yield return $"Budget: {DepartmentBudgetText(department)}";
        yield return $"Strengths: {DepartmentStrengthText(department, staff)}";
        yield return $"Weaknesses: {DepartmentWeaknessText(department, vacancies)}";
        yield return $"Recommendations: {DepartmentRecommendationText(department, grade, vacancies.Length)}";
    }

    private DepartmentGradeReport DepartmentGradeFor(string department)
    {
        var report = State.DepartmentGrades.FirstOrDefault(report => report.DepartmentName.Contains(department, StringComparison.OrdinalIgnoreCase));
        if (report is not null)
        {
            return report;
        }

        var budget = State.BudgetOverview;
        if (department == "Finance")
        {
            var score = budget.RemainingBudget < 0 ? 45 : budget.RemainingBudget > budget.TotalBudget * 0.15m ? 82 : 68;
            return new DepartmentGradeReport("Finance", ScoreToDepartmentGrade(score), score, $"{budget.Status}; {budget.OwnerBudgetConfidence}", new[] { $"Remaining budget {budget.RemainingBudget:C0}", $"Staff payroll {budget.StaffTotal:C0}", $"Player payroll {budget.PlayerContractsTotal:C0}" });
        }

        if (department == "Owner")
        {
            var score = Math.Clamp((State.OwnerOffice.Confidence.Confidence + State.OwnerOffice.Confidence.Trust + State.OwnerOffice.Confidence.Support) / 3, 0, 100);
            return new DepartmentGradeReport("Owner", ScoreToDepartmentGrade(score), score, State.OwnerOffice.JobSecurity.Explanation, State.OwnerOffice.Confidence.Drivers.Take(3).ToArray());
        }

        return new DepartmentGradeReport(department, DepartmentGrade.C, 65, department == "Facilities" ? "Facilities placeholder only; no facilities simulation is active." : "Department grade is being assembled from staff, budget, and vacancies.", Array.Empty<string>());
    }

    private static DepartmentGrade ScoreToDepartmentGrade(int score) =>
        score >= 85 ? DepartmentGrade.A :
        score >= 70 ? DepartmentGrade.B :
        score >= 55 ? DepartmentGrade.C :
        score >= 40 ? DepartmentGrade.D :
        DepartmentGrade.F;

    private string DepartmentBudgetText(string department)
    {
        var budget = State.BudgetOverview;
        return department switch
        {
            "Front Office" => $"GM salary {budget.GmSalary:C0}; staff total {budget.StaffTotal:C0}",
            "Coaching" or "Development" => $"Coaching salaries {budget.CoachingSalaries:C0}",
            "Scouting" => $"Scouting salaries {budget.ScoutingSalaries:C0}; scouting budget {budget.ScoutingBudget:C0}",
            "Medical" => $"Medical/training salaries {budget.MedicalTrainingSalaries:C0}; operations {budget.MedicalAndStaffOperationsBudget:C0}",
            "Finance" => $"Total {budget.TotalBudget:C0}; used {budget.UsedBudget:C0}; remaining {budget.RemainingBudget:C0}",
            _ => $"Staff total {budget.StaffTotal:C0}"
        };
    }

    private static string DepartmentStrengthText(string department, IReadOnlyList<SelectablePersonRow> staff) =>
        staff.Count == 0
            ? $"{department} has no assigned staff strength yet."
            : string.Join("; ", staff.Take(3).Select(row => row.Summary));

    private static string DepartmentWeaknessText(string department, IReadOnlyList<SelectablePersonRow> vacancies) =>
        vacancies.Count == 0
            ? $"{department} has no open rulebook vacancy."
            : string.Join("; ", vacancies.Take(3).Select(row => row.Summary));

    private static string DepartmentRecommendationText(string department, DepartmentGradeReport grade, int vacancies) =>
        vacancies > 0 ? $"Fill {department} vacancies before expanding responsibilities." :
        grade.Score < 60 ? $"Review leadership and fit in {department}." :
        grade.Score >= 80 ? $"Maintain continuity and monitor extension candidates in {department}." :
        $"Keep developing staff chemistry and performance in {department}.";

    private IEnumerable<string> BuildOrganizationChartLines()
    {
        yield return $"Owner: {State.Snapshot.Owner.Name}";
        yield return $"General Manager: {State.Snapshot.GeneralManager.Identity.DisplayName}";
        foreach (var node in State.OrganizationChart.OrderBy(node => OrganizationRoleSort(RoleFromText(node.Role))).Take(16))
        {
            yield return $"{node.Name} - {node.Role} | reports to {FindPersonName(node.ReportsToPersonId)} | {node.Responsibilities} | {node.SalaryText}";
        }

        yield return "President: future placeholder";
        yield return "Facilities Director: placeholder only";
    }

    private static StaffRole RoleFromText(string text) =>
        Enum.GetValues<StaffRole>().FirstOrDefault(role => text.Contains(role.ToString(), StringComparison.OrdinalIgnoreCase));

    private IEnumerable<string> BuildFinancialOverviewLines()
    {
        var budget = State.BudgetOverview;
        yield return $"Operating Budget: {budget.TotalBudget:C0}";
        yield return $"Player Payroll: {budget.PlayerContractsTotal:C0}";
        yield return $"Staff Payroll: {budget.StaffTotal:C0}";
        yield return $"Scouting Budget: {budget.ScoutingBudget:C0}";
        yield return $"Medical Budget: {budget.MedicalAndStaffOperationsBudget:C0}";
        yield return "Travel: placeholder";
        yield return $"Future Commitments: {State.SalaryCap.CommittedFutureSalary:C0}";
        yield return $"Over/Under Budget: {budget.OverUnderBudget:C0}";
    }

    private IEnumerable<string> BuildOrganizationReportLines()
    {
        yield return $"Department Grades: {State.DepartmentGradesText().Replace(Environment.NewLine, " | ", StringComparison.Ordinal)}";
        yield return $"Staff Performance: {State.StaffProfiles.Count} active profile(s); {State.ScenarioSnapshot.StaffCareerSummaries.Count} career summary record(s).";
        yield return $"Budget Health: {State.BudgetOverview.Status} - {State.BudgetOverview.OwnerBudgetConfidence}";
        yield return $"Organization Health: {State.SeasonReadinessReport.OrganizationHealth}";
        yield return $"Franchise Direction: {State.PlayerFranchiseIdentity.Summary}";
        yield return $"Recommendations: {DepartmentRecommendationText(_organizationCommandDepartment, DepartmentGradeFor(_organizationCommandDepartment), VacancyRowsForOrganizationDepartment(_organizationCommandDepartment).Count())}";
    }

    private IEnumerable<string> BuildOrganizationActionLines()
    {
        foreach (var item in State.ActionCenterItems
            .Where(item => item.Status == ActionCenterStatus.Open && item.Category is ActionCenterCategory.Staff or ActionCenterCategory.Owner or ActionCenterCategory.Budget)
            .Take(6))
        {
            yield return $"{item.Priority} | {item.Category}: {item.Title} - {item.RecommendedAction}";
        }
    }

    private void RenderOrganizationStaffCard(SelectablePersonRow? row)
    {
        if (_organizationCommandStaffCard is null)
        {
            return;
        }

        _organizationCommandStaffCard.Children.Clear();
        if (row is null)
        {
            _organizationCommandStaffCard.Children.Add(EmptyDetail("Selected Staff", "Select a staff member, vacancy, or owner record."));
            return;
        }

        if (row.Kind == "Owner")
        {
            _organizationCommandStaffCard.Children.Add(BuildOrganizationOwnerCard());
            return;
        }

        if (row.Kind == "Vacancy")
        {
            _organizationCommandStaffCard.Children.Add(BuildOrganizationVacancyCard(row));
            return;
        }

        var profile = State.StaffProfiles.FirstOrDefault(profile => profile.PersonId == row.PersonId);
        if (profile is null)
        {
            _organizationCommandStaffCard.Children.Add(EmptyDetail("Selected Staff", "Selected staff member is no longer available."));
            return;
        }

        var panel = CreateDetailPanel(profile.Name, $"{StaffRoles.Title(profile.CurrentRole)} | {profile.Department}");
        AddLine(panel, "Salary", $"{profile.Salary.AnnualAmount:C0}");
        AddLine(panel, "Years remaining", YearsRemainingFromContract(profile.ContractStatus));
        AddLine(panel, "Contract", profile.ContractStatus);
        AddLine(panel, "Extension recommendation", ExtensionRecommendation(profile));
        AddLine(panel, "Replacement cost", $"{profile.Salary.AnnualAmount * 1.15m:C0} estimated");
        AddLine(panel, "Relationship", $"{profile.RelationshipWithGm}/100");
        AddLine(panel, "Franchise fit", new FranchiseIdentityService().EvaluateStaffFit(State.ScenarioSnapshot, profile.PersonId).Summary);
        AddLine(panel, "Performance", StaffPerformanceOutcomeText(profile.PersonId));
        AddLine(panel, "Current assignment", profile.CurrentAssignment);
        AddLine(panel, "Current focus", profile.CurrentFocus);
        AddSubHeader(panel, "Strengths");
        AddParagraph(panel, string.Join(", ", profile.Strengths.DefaultIfEmpty("No strength noted.")));
        AddSubHeader(panel, "Weaknesses");
        AddParagraph(panel, string.Join(", ", profile.Weaknesses.DefaultIfEmpty("No weakness noted.")));
        AddSubHeader(panel, "History");
        AddParagraph(panel, StaffHistoryText(profile.PersonId));
        AddSubHeader(panel, "Actions");
        AddActions(panel,
            CreateDetailButton("Person Card", () => OpenUniversalPersonCard(profile.PersonId)),
            CreateDetailButton("View Profile", () => ShowStaffProfile(profile.PersonId)),
            CreateDetailButton("Promote", () => State.ReassignStaffRoleFor(profile.PersonId)),
            CreateDetailButton("Demote", () => State.ReassignStaffRoleFor(profile.PersonId)),
            CreateDetailButton("Move Department", () => State.SetStaffFocusFor(profile.PersonId)),
            CreateDetailButton("Set Focus", () => SetStaffFocusFor(profile.PersonId)),
            CreateDetailButton("Performance Review", () => MessageBox.Show(State.StaffPerformanceReviewText(profile.PersonId), "Staff Performance Review", MessageBoxButton.OK, MessageBoxImage.Information)),
            CreateDetailButton("Release", () => ConfirmDestructiveAction(
                "Release Staff Member",
                $"{profile.Name} will be released from the staff if permitted by role rules. Continue?",
                () => State.ReleaseStaffFor(profile.PersonId))));
        _organizationCommandStaffCard.Children.Add(panel);
    }

    private UIElement BuildOrganizationOwnerCard()
    {
        var owner = State.Snapshot.Owner;
        var office = State.OwnerOffice;
        var panel = CreateDetailPanel(owner.Name, $"{owner.Archetype} owner");
        AddLine(panel, "Budget", $"{State.BudgetOverview.TotalBudget:C0} total | {State.BudgetOverview.RemainingBudget:C0} remaining");
        AddLine(panel, "Expectations", office.Expectations.FirstOrDefault()?.Description ?? "No active expectation.");
        AddLine(panel, "Confidence", $"{office.Confidence.Confidence}/100");
        AddLine(panel, "Trust", $"{office.Confidence.Trust}/100");
        AddLine(panel, "Patience", $"{office.Confidence.Patience}/100");
        AddLine(panel, "Job security", $"{office.JobSecurity.Level} ({office.JobSecurity.Score}/100)");
        AddLine(panel, "Department grades", State.DepartmentGradesText().Replace(Environment.NewLine, " | ", StringComparison.Ordinal));
        AddLine(panel, "Long-term vision", office.Personality.Vision);
        AddLine(panel, "Franchise identity", State.PlayerFranchiseIdentity.Summary);
        AddLine(panel, "Current era", $"{State.PlayerFranchiseIdentity.CurrentEra.Name} ({State.PlayerFranchiseIdentity.CurrentEra.StartYear}-present)");
        AddSubHeader(panel, "Recommendations");
        foreach (var recommendation in State.AssistantGmRecommendations.Take(4))
        {
            AddParagraph(panel, recommendation);
        }

        AddActions(panel,
            CreateDetailButton("Person Card", () => OpenUniversalPersonCard("owner")),
            CreateDetailButton("Owner Workspace", () => SelectWorkspaceScreen("Organization", "Owner")),
            CreateDetailButton("Budget View", () => SelectWorkspaceScreen("Organization", "Budget")),
            CreateDetailButton("Owner History", () => SelectWorkspaceScreen("Reports / History", "Owner History")));
        return panel;
    }

    private UIElement BuildOrganizationVacancyCard(SelectablePersonRow row)
    {
        var panel = CreateDetailPanel(row.Name, row.Primary);
        AddLine(panel, "Department", _organizationCommandDepartment);
        AddLine(panel, "Status", row.Secondary);
        AddParagraph(panel, row.Summary);
        AddSubHeader(panel, "Recommended Action");
        AddParagraph(panel, "Review the staff market and hire a candidate whose role fit, department fit, salary, personality, and chemistry match the organization.");
        AddActions(panel,
            CreateDetailButton("Hire Staff", () => SelectWorkspaceScreen("Organization", "Staff Hiring")),
            CreateDetailButton("View Vacancies", () => SelectWorkspaceScreen("Organization", "Vacancies")));
        return panel;
    }

    private string StaffPerformanceOutcomeText(string personId)
    {
        var review = State.StaffPerformanceReviewText(personId);
        var outcomeLine = review.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(line => line.StartsWith("Outcome:", StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(outcomeLine) ? "Performance review available" : outcomeLine.Replace("Outcome:", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
    }

    private string ExtensionRecommendation(StaffOfficeProfile profile)
    {
        var review = State.StaffPerformanceReviewText(profile.PersonId);
        var recommendationLine = review.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(line => line.StartsWith("Recommendation:", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(recommendationLine))
        {
            return recommendationLine.Replace("Recommendation:", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        }

        return profile.RelationshipWithGm >= 70 ? "Extension candidate" : "Review fit before extension";
    }

    private string StaffHistoryText(string personId)
    {
        var history = State.ScenarioSnapshot.StaffCareerHistory.FirstOrDefault(history => history.PersonId == personId);
        var summary = State.ScenarioSnapshot.StaffCareerSummaries.FirstOrDefault(summary => summary.PersonId == personId);
        var lines = new List<string>();
        if (history is not null)
        {
            lines.Add($"Current organization: {history.CurrentOrganization}");
            lines.Add($"Previous roles: {string.Join(", ", history.PreviousRoles.Take(4))}");
            lines.AddRange(history.NotableHistory.Take(4));
            lines.Add(history.EvaluationSummary);
        }

        if (summary is not null)
        {
            lines.Add($"Organizations: {string.Join(", ", summary.Organizations.Take(4))}");
            lines.Add($"Roles: {string.Join(", ", summary.Roles.Take(5))}");
            if (summary.PlayersDeveloped.Count > 0)
            {
                lines.Add($"Players developed: {string.Join(", ", summary.PlayersDeveloped.Take(4))}");
            }

            if (summary.PlayersDiscovered.Count > 0)
            {
                lines.Add($"Players scouted: {string.Join(", ", summary.PlayersDiscovered.Take(4))}");
            }
        }

        return string.Join(Environment.NewLine, lines.DefaultIfEmpty("No staff history is recorded yet."));
    }

    private static string YearsRemainingFromContract(string contractStatus)
    {
        var match = System.Text.RegularExpressions.Regex.Match(contractStatus, @"through\s+(\d{4})-(\d{2})-(\d{2})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!match.Success
            || !int.TryParse(match.Groups[1].Value, out var year)
            || !int.TryParse(match.Groups[2].Value, out var month)
            || !int.TryParse(match.Groups[3].Value, out var day))
        {
            return "contract term not tracked";
        }

        var end = new DateOnly(year, month, day);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var remaining = Math.Max(0, end.Year - today.Year - (end.DayOfYear < today.DayOfYear ? 1 : 0));
        return remaining == 1 ? "1 year" : $"{remaining} years";
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

    private IReadOnlyList<SelectablePersonRow> BuildTeamRows() =>
        State.ScenarioSnapshot.LeagueProfile.Teams
            .OrderBy(team => team.TeamName, StringComparer.Ordinal)
            .Select(team =>
            {
                var brand = BrandingFor(team.OrganizationId);
                return new SelectablePersonRow(
                    team.OrganizationId,
                    $"{brand.TeamAbbreviation} {team.TeamName}",
                    "Team",
                    $"{brand.Monogram.Letters} crest | {team.DisplayLeagueName} | {team.DisplayDivisionConference} | {team.City}, {team.Region}",
                    $"Record {team.PreviousRecord} | budget {team.Budget:C0} | roster {team.RosterQuality} | prospects {team.ProspectStrength}",
                    $"{brand.VisualStyleDescriptor} | {team.DisplayCurrentStrategy} | staff {team.DisplayStaffQuality} | difficulty {team.Difficulty}");
            })
            .ToArray();

    private UIElement BuildTeamDetail(SelectablePersonRow? row)
    {
        if (row is null)
        {
            return EmptyDetail("Teams", "Select a league team to review its front office, roster, needs, and trade direction.");
        }

        var team = State.TeamOptionFor(row.PersonId);
        if (team is null)
        {
            return EmptyDetail("Teams", "Selected team is no longer available.");
        }

        var roster = State.OtherTeamTradeRoster(team.OrganizationId);
        var brand = BrandingFor(team.OrganizationId);
        var panel = CreateDetailPanel(team.TeamName, $"{team.City}, {team.Region}, {team.Country}");
        panel.Children.Insert(0, UiPresentation.UiTeamCard(
            brand,
            $"{brand.TeamAbbreviation} {brand.OrganizationDisplayName}",
            $"{brand.LeagueName} | {brand.ConferenceDivision}",
            new[]
            {
                $"Monogram: {brand.Monogram.Letters} | Crest: {brand.LogoPlaceholder}",
                $"Style: {brand.VisualStyleDescriptor} | Banner: {brand.BannerStyle}",
                $"Arena: {brand.ArenaName}",
                $"Record: {team.PreviousRecord} | Strategy: {team.DisplayCurrentStrategy}"
            },
            selected: team.OrganizationId == State.ScenarioSnapshot.Organization.OrganizationId));
        AddSubHeader(panel, "Team Overview");
        AddLine(panel, "League / division", $"{team.DisplayLeagueName} | {team.DisplayDivisionConference}");
        AddLine(panel, "Arena", team.DisplayArena);
        AddLine(panel, "Previous record", team.PreviousRecord);
        AddLine(panel, "Owner expectations", team.OwnerExpectations);
        AddLine(panel, "Budget status", $"{team.Budget:C0} placeholder");
        AddLine(panel, "Roster", team.RosterQuality);
        AddLine(panel, "Prospects", team.ProspectStrength);
        AddLine(panel, "Staff", team.DisplayStaffQuality);
        AddLine(panel, "Difficulty", team.Difficulty);
        AddLine(panel, "Current strategy", team.DisplayCurrentStrategy);
        AddLine(panel, "Parent", string.IsNullOrWhiteSpace(team.ParentOrganizationId) ? "none" : team.ParentOrganizationId);
        AddLine(panel, "Affiliate", string.IsNullOrWhiteSpace(team.AffiliateOrganizationId) ? "none" : team.AffiliateOrganizationId);
        AddLine(panel, "Pipeline players", State.PipelineCountForOrganization(team.OrganizationId));
        AddSubHeader(panel, "AI Strategy");
        AddParagraph(panel, State.OrganizationAiTextForOrganization(team.OrganizationId, team.TeamName));
        AddSubHeader(panel, "AI Front Office");
        AddParagraph(panel, State.AiFrontOfficeTextForOrganization(team.OrganizationId, team.TeamName));
        AddSubHeader(panel, "Current Needs / Trade Direction");
        AddParagraph(panel, State.TeamNeedsTextForOrganization(team.OrganizationId, team.TeamName));
        AddSubHeader(panel, "Roster Browse");
        if (roster.Count == 0)
        {
            AddParagraph(panel, "No trade-browsable roster entries are available yet.");
        }
        else
        {
            foreach (var player in roster.Take(8))
            {
                AddParagraph(panel, $"{player.Name} | {State.PositionShortText(player.Position)} | age {player.Age} | {player.CurrentRole} | potential {State.TradePotentialRole(player)} | {State.TradeTargetType(player)} | {player.InterestLevel}");
            }
        }

        AddSubHeader(panel, "Recent Transactions");
        var transactions = State.LeagueRecentTransactions(team.OrganizationId).Take(5).ToArray();
        if (transactions.Length == 0)
        {
            AddParagraph(panel, "No recent transactions for this team.");
        }
        else
        {
            foreach (var transaction in transactions)
            {
                AddParagraph(panel, $"{transaction.Date:yyyy-MM-dd}: {transaction.Description}");
            }
        }

        AddActions(panel, CreateDetailButton("Open Team Window", () => ShowOrganizationPopup(team.OrganizationId)));
        return panel;
    }

    private void ShowOrganizationPopup(string organizationId)
    {
        var team = State.TeamOptionFor(organizationId);
        if (team is null)
        {
            ShowConfirmationPopup("Organization / Team", "Selected team is no longer available.");
            return;
        }

        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var rosterRows = State.OtherTeamTradeRoster(team.OrganizationId)
            .Select(entry => new SelectablePersonRow(
                entry.PersonId,
                entry.Name,
                "OtherTeamPlayer",
                $"{State.PositionShortText(entry.Position)} | age {entry.Age} | {entry.CurrentRole} | potential {State.TradePotentialRole(entry)}",
                $"{entry.ContractStatus} | salary {entry.SalaryImpact:C0}",
                $"{entry.TeamName} | {State.TradeTargetType(entry)} | trade interest {entry.InterestLevel} | ask: {entry.AskingPriceSummary}"))
            .ToArray();
        var list = new ListBox
        {
            ItemsSource = rosterRows,
            Margin = new Thickness(0, 0, 14, 0)
        };
        Grid.SetColumn(list, 0);
        root.Children.Add(list);

        var detail = new StackPanel();
        Grid.SetColumn(detail, 1);
        root.Children.Add(detail);

        void RenderPlayer(SelectablePersonRow? selected)
        {
            detail.Children.Clear();
            detail.Children.Add(BuildOtherTeamPlayerDetail(team, selected));
        }

        list.SelectionChanged += (_, _) => RenderPlayer(list.SelectedItem as SelectablePersonRow);
        list.SelectedItem = rosterRows.FirstOrDefault();
        RenderPlayer(list.SelectedItem as SelectablePersonRow);

        ShowPopup($"Organization / Team - {team.TeamName}", root, 980, 680);
    }

    private UIElement BuildOtherTeamPlayerDetail(TeamSelectionOption team, SelectablePersonRow? row)
    {
        var panel = CreateDetailPanel(team.TeamName, "Organization / Team");
        AddLine(panel, "Record", team.PreviousRecord);
        AddLine(panel, "League / division", $"{team.DisplayLeagueName} | {team.DisplayDivisionConference}");
        AddLine(panel, "Arena", team.DisplayArena);
        AddLine(panel, "Budget status", $"{team.Budget:C0} placeholder");
        AddLine(panel, "Staff", team.DisplayStaffQuality);
        AddLine(panel, "Current strategy", team.DisplayCurrentStrategy);
        AddLine(panel, "Draft picks", "Draft picks placeholder");
        AddLine(panel, "Parent club", string.IsNullOrWhiteSpace(team.ParentOrganizationId) ? "none" : team.ParentOrganizationId);
        AddLine(panel, "Affiliate club", string.IsNullOrWhiteSpace(team.AffiliateOrganizationId) ? "none" : team.AffiliateOrganizationId);
        AddLine(panel, "Needs", State.TeamNeedsShortTextForOrganization(team.OrganizationId, team.TeamName));
        AddLine(panel, "AI profile", State.OrganizationAiShortTextForOrganization(team.OrganizationId, team.TeamName));

        if (row is null)
        {
            AddSubHeader(panel, "Roster");
            AddParagraph(panel, "Select a player from this team roster.");
            return panel;
        }

        var entry = State.TradeBlockEntryFor(row.PersonId);
        if (entry is null)
        {
            AddParagraph(panel, "Selected player is no longer available.");
            return panel;
        }

        AddSubHeader(panel, "Selected Player");
        AddLine(panel, "Name", entry.Name);
        AddLine(panel, "Bio", $"{State.PositionShortText(entry.Position)} | age {entry.Age} | {State.RegionTeamText(entry.PersonId)}");
        AddLine(panel, "Role", entry.CurrentRole);
        AddLine(panel, "Potential role", State.TradePotentialRole(entry));
        AddLine(panel, "Trade target type", State.TradeTargetType(entry));
        AddLine(panel, "Contract", entry.ContractStatus);
        AddLine(panel, "Last-season stats", State.LastSeasonStats(entry.PersonId));
        AddLine(panel, "Career summary", State.CareerStatSummary(entry.PersonId));
        AddLine(panel, "Scouting confidence", State.ScoutingConfidenceText(entry.PersonId));
        AddLine(panel, "Trade interest", $"{entry.InterestLevel}: {entry.ReasonAvailable}");
        AddLine(panel, "Pipeline", State.PipelineText(entry.PersonId));
        AddActions(
            panel,
            CreateDetailButton("View Dossier", () => OpenDossierFor(entry.PersonId)),
            CreateDetailButton("Add to Trade Proposal", () => ShowTradeBuilderPopup(entry.PersonId)),
            CreateDetailButton("Scout Player", () => ShowScoutAssignmentDialog(entry.PersonId), State.AvailableScoutProfiles.Count > 0),
            CreateDetailButton("Add to Watchlist", () => ShowConfirmationPopup("Watchlist", "Watchlist placeholder: this player would be flagged for future review.")));
        return panel;
    }

    private IReadOnlyList<SelectablePersonRow> BuildStaffCandidateRows()
    {
        var rows = new List<SelectablePersonRow>();
        rows.Add(new SelectablePersonRow("staff-section:candidates", "Hire Staff / Staff Market", "StaffSection", "Living market candidates", "Hire button appears only for available/interested candidates.", "Select a market candidate, review status and interest, then hire or approach from the candidate detail panel."));
        rows.AddRange(State.StaffMarketCandidates.Select(market => new SelectablePersonRow(
            market.PersonId,
            market.Name,
            "Candidate",
            $"Staff Candidate - {market.DesiredRole} - {market.Status} - ask {market.SalaryAsk.AnnualAmount:C0}",
            $"{market.Department} | reputation {market.Reputation} | interest {market.HiringInterest}/100 | {market.CurrentEmployer}",
            $"{market.AvailabilitySummary} Strengths: {string.Join(", ", market.Candidate.Strengths)}. Risk: {market.Candidate.ChemistryRisk}")));
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

    private IReadOnlyList<SelectablePersonRow> BuildRosterRows()
    {
        var rows = new List<SelectablePersonRow>
        {
            new(
                "roster-summary",
                "Roster Breakdown",
                "RosterSummary",
                State.RosterBreakdownTitle,
                State.RosterBreakdownSecondary,
                State.RosterBreakdownSummary)
        };

        rows.AddRange(State.Snapshot.Roster.Players
            .Where(PassesRosterFilters)
            .OrderBy(player => player.Status)
            .ThenBy(player => FindPersonName(player.PersonId), StringComparer.Ordinal)
            .Select(player =>
            {
                return new SelectablePersonRow(
                    player.PersonId,
                    FindPersonName(player.PersonId),
                    "RosterPlayer",
                    $"{State.PositionShortText(player.Position)} | {State.PersonAge(player.PersonId)?.ToString() ?? player.Age?.ToString() ?? "unknown"} | {State.RatingText(player.PersonId)}",
                    $"{State.CurrentLineupRole(player.PersonId)} | {State.CurrentLinePair(player.PersonId)} | {State.ContractRightsStatus(player.PersonId)}",
                    $"{State.PlayerType(player.PersonId)} | {State.DevelopmentTrend(player.PersonId)} | {State.InjuryStatus(player.PersonId)}");
            })
            .ToArray());

        return rows;
    }

    private IReadOnlyList<SelectablePersonRow> BuildLineupRows()
    {
        var lineup = State.CurrentLineup;
        var rows = new List<SelectablePersonRow>
        {
            new(
                "lineup-summary",
                "Lineup Summary",
                "LineupSummary",
                $"{State.LineupValidationText} | Chemistry: {State.LineChemistryReport.Overall.Score.Grade}",
                lineup.Summary,
                "Select a lineup slot to assign, remove, swap, view dossier, or auto-fill.")
        };

        foreach (var unit in State.LineChemistryReport.ForwardLines.Concat(State.LineChemistryReport.DefensePairs).Append(State.LineChemistryReport.GoalieDepth))
        {
            rows.Add(new SelectablePersonRow(
                unit.UnitId,
                unit.Label,
                "LineChemistry",
                $"Chemistry: {unit.Score.Grade} ({unit.Score.Value})",
                $"{string.Join(" | ", unit.PlayerNames)}",
                $"{unit.Recommendation}"));
        }

        rows.Add(new SelectablePersonRow(
            "game-usage-summary",
            "Game Usage Summary",
            "GameUsage",
            State.CurrentGameUsage.Summary,
            "Even Strength | Power Play | Penalty Kill | Goalies | Extra Attacker | Three-on-Three | Shootout",
            "Personnel deployment only. No tactics engine."));
        foreach (var unit in State.CurrentGameUsage.SpecialTeams.PowerPlayUnits)
        {
            rows.Add(new SelectablePersonRow(
                $"power-play:{unit.UnitNumber}",
                $"Power Play Unit {unit.UnitNumber}",
                "GameUsage",
                $"LW {unit.LeftWing?.PlayerName ?? "open"} | C {unit.Center?.PlayerName ?? "open"} | RW {unit.RightWing?.PlayerName ?? "open"}",
                $"QB {unit.QuarterbackDefense?.PlayerName ?? "open"} | Net-front/2D {unit.NetFrontOrSecondDefense?.PlayerName ?? "open"}",
                "Assign, remove, swap, or auto-fill special teams usage."));
        }

        foreach (var unit in State.CurrentGameUsage.SpecialTeams.PenaltyKillUnits)
        {
            rows.Add(new SelectablePersonRow(
                $"penalty-kill:{unit.UnitNumber}",
                $"Penalty Kill Unit {unit.UnitNumber}",
                "GameUsage",
                $"LW {unit.LeftWing?.PlayerName ?? "open"} | RW {unit.RightWing?.PlayerName ?? "open"}",
                $"LD {unit.LeftDefense?.PlayerName ?? "open"} | RD {unit.RightDefense?.PlayerName ?? "open"}",
                "Use responsible forwards, veteran support, and trusted defense pairs."));
        }

        rows.Add(new SelectablePersonRow(
            "goalie-usage",
            "Goalies",
            "GameUsage",
            string.Join(" | ", State.CurrentGameUsage.GoalieUsage.Select(goalie => $"{goalie.UsageRole}: {goalie.PlayerName}")),
            string.Join(" | ", State.CurrentGameUsage.GoalieUsage.Select(goalie => $"{goalie.GamesStarted}/{goalie.ExpectedStarts} starts, {goalie.Workload}")),
            "Track starter, backup, expected starts, and rest recommendations."));
        rows.Add(new SelectablePersonRow(
            "extra-attacker",
            "Extra Attacker",
            "GameUsage",
            string.Join(" | ", State.CurrentGameUsage.SpecialTeams.ExtraAttacker.Players.Select(player => player.PlayerName)),
            State.CurrentGameUsage.SpecialTeams.ExtraAttacker.Summary,
            "Six-on-five personnel group."));
        rows.Add(new SelectablePersonRow(
            "three-on-three",
            "Three-on-Three",
            "GameUsage",
            $"{State.CurrentGameUsage.SpecialTeams.ThreeOnThree.Combination}: {string.Join(" | ", State.CurrentGameUsage.SpecialTeams.ThreeOnThree.Players.Select(player => player.PlayerName))}",
            State.CurrentGameUsage.SpecialTeams.ThreeOnThree.Summary,
            "Placeholder personnel deployment only."));
        rows.Add(new SelectablePersonRow(
            "shootout",
            "Shootout Order",
            "GameUsage",
            string.Join(" | ", State.CurrentGameUsage.SpecialTeams.ShootoutOrder.Shooters.Select((player, index) => $"{index + 1}. {player.PlayerName}")),
            State.CurrentGameUsage.SpecialTeams.ShootoutOrder.Summary,
            "Order can be adjusted without adding a shootout tactics engine."));

        foreach (var slot in State.LineupSlots)
        {
            var assignment = lineup.Assignments.FirstOrDefault(assignment => assignment.Slot == slot);
            var usage = assignment is null ? null : lineup.Usage.FirstOrDefault(usage => usage.PersonId == assignment.PersonId);
            rows.Add(new SelectablePersonRow(
                $"lineup-slot:{slot}",
                LineupDisplay.SlotLabel(slot),
                "LineupSlot",
                assignment is null ? "Open slot" : $"{assignment.PlayerName} | {assignment.Position} | {LineupDisplay.Role(assignment.CurrentRole)}",
                assignment is null ? "No player assigned" : $"{State.ChemistryTextForSlot(slot)} | Expected {LineupDisplay.Role(usage?.ExpectedRole ?? assignment.CurrentRole)} | promise {usage?.PromiseStatus.ToString() ?? "NotYetEvaluated"} | {usage?.Satisfaction.ToString() ?? "Neutral"}",
                assignment is null ? $"Eligible replacements: {State.EligibleLineupReplacements(slot).Count}" : $"{assignment.CoachNote} | {State.LineupDevelopmentImpactText(assignment.PersonId)}"));
        }

        return rows;
    }

    private IReadOnlyList<SelectablePersonRow> BuildTacticsRows()
    {
        var tactics = State.CurrentTactics;
        var rows = new List<SelectablePersonRow>
        {
            new(
                "tactics-summary",
                "Tactical Identity",
                "TacticsSummary",
                $"{TacticsService.Display(tactics.Style)} | Fit {tactics.FitReport.Grade} ({tactics.FitReport.Score})",
                $"Coach: {tactics.CoachName} | Philosophy: {tactics.CoachPhilosophy}",
                tactics.Summary),
            new(
                "even-strength",
                "Even Strength System",
                "TacticsSettings",
                $"Forecheck {TacticsService.Display(tactics.Settings.Forecheck)} | NZ {TacticsService.Display(tactics.Settings.NeutralZone)} | DZ {TacticsService.Display(tactics.Settings.DefensiveZone)}",
                $"Breakout {TacticsService.Display(tactics.Settings.Breakout)} | Shots {TacticsService.Display(tactics.Settings.ShotPreference)} | Risk {tactics.Settings.RiskLevel}",
                "Set forecheck, neutral-zone pressure, defensive-zone structure, breakout, shot preference, physicality, and risk."),
            new(
                "special-teams-tactics",
                "Special Teams Tactics",
                "TacticsSettings",
                $"PP {TacticsService.Display(tactics.Settings.PowerPlayStyle)} | PK {TacticsService.Display(tactics.Settings.PenaltyKillStyle)}",
                "Uses Alpha 6.6 units; this changes style only, not assignments.",
                "Set PP/PK tactical preference without rebuilding special teams personnel."),
            new(
                "tactical-fit",
                "Tactical Fit Report",
                "TacticsReport",
                $"{tactics.FitReport.Grade} ({tactics.FitReport.Score}/100)",
                tactics.FitReport.Summary,
                tactics.FitReport.CoachRecommendation),
            new(
                "tactical-modifiers",
                "Future Simulator Modifiers",
                "TacticsReport",
                $"Off {tactics.ModifierProfile.OffenseTendency:+#;-#;0} | Def {tactics.ModifierProfile.DefenseTendency:+#;-#;0} | Pace {tactics.ModifierProfile.PaceTendency:+#;-#;0}",
                $"Physical {tactics.ModifierProfile.PhysicalityTendency:+#;-#;0} | Risk {tactics.ModifierProfile.RiskTendency:+#;-#;0} | ST {tactics.ModifierProfile.SpecialTeamsTendency:+#;-#;0}",
                tactics.ModifierProfile.Summary)
        };

        rows.AddRange(tactics.Recommendations.Select(recommendation => new SelectablePersonRow(
            recommendation.RecommendationId,
            recommendation.Title,
            "TacticsRecommendation",
            recommendation.IsImportant ? "Important" : "Note",
            recommendation.Reason,
            recommendation.SuggestedAction)));

        return rows;
    }

    private UIElement BuildLineupDetail(SelectablePersonRow? row)
    {
        if (row is null)
        {
            return EmptyDetail("Lineup", "Select a lineup slot to manage usage and role promises.");
        }

        if (row.Kind == "LineupSummary")
        {
            var summary = CreateDetailPanel("Lineup Summary", State.LineupValidationText);
            AddParagraph(summary, BuildLineup());
            AddActions(summary, CreateDetailButton("Auto-fill", () => State.AutoFillLineup()));
            return summary;
        }

        if (row.Kind == "LineChemistry")
        {
            return BuildLineChemistryDetail(row.PersonId);
        }

        if (row.Kind == "GameUsage")
        {
            return BuildGameUsageDetail(row.PersonId);
        }

        if (!TryLineupSlot(row.PersonId, out var slot))
        {
            return EmptyDetail("Lineup", "Selected lineup row is not a slot.");
        }

        var assignment = State.CurrentLineup.Assignments.FirstOrDefault(assignment => assignment.Slot == slot);
        var panel = CreateDetailPanel(LineupDisplay.SlotLabel(slot), assignment?.PlayerName ?? "Open slot");
        AddLine(panel, "Lineup position", State.LineupPositionText(slot));
        var chemistry = State.ChemistryForSlot(slot);
        if (chemistry is not null)
        {
            AddLine(panel, "Chemistry", $"{chemistry.Score.Grade} ({chemistry.Score.Value}, {chemistry.Score.ScoreBand})");
            AddLine(panel, "Chemistry note", chemistry.Recommendation);
        }

        AddLine(panel, "Current player", assignment?.PlayerName ?? "none");
        if (assignment is not null)
        {
            var usage = State.LineupUsageFor(assignment.PersonId);
            AddLine(panel, "Current role", LineupDisplay.Role(assignment.CurrentRole));
            AddLine(panel, "Expected role", usage is null ? "Not established" : LineupDisplay.Role(usage.ExpectedRole));
            AddLine(panel, "Promised role", State.PromisedRoleText(assignment.PersonId));
            AddLine(panel, "Coach recommended role", usage is null ? LineupDisplay.Role(assignment.CurrentRole) : LineupDisplay.Role(usage.CoachRecommendedRole));
            AddLine(panel, "Potential role", LineupDisplay.Role(assignment.PotentialRole));
            AddLine(panel, "Promise status", usage?.PromiseStatus.ToString() ?? "NotYetEvaluated");
            AddLine(panel, "Role satisfaction", usage?.Satisfaction.ToString() ?? "Neutral");
            AddLine(panel, "Development impact", usage?.DevelopmentImpactNote ?? State.LineupDevelopmentImpactText(assignment.PersonId));
            AddLine(panel, "Morale note", usage?.MoraleNote ?? "No role morale issue tracked.");
            AddLine(panel, "Coach note", assignment.CoachNote);
        }

        AddSubHeader(panel, "Eligible Replacements");
        var replacements = State.EligibleLineupReplacements(slot).Take(8).ToArray();
        if (replacements.Length == 0)
        {
            AddParagraph(panel, "No eligible replacement is available for this slot.");
        }
        else
        {
            foreach (var replacement in replacements)
            {
                AddParagraph(panel, $"{replacement.PlayerName} | {replacement.Position} | {LineupDisplay.Role(replacement.CurrentRole)} | {replacement.SlotLabel}");
            }
        }

        AddActions(panel,
            CreateDetailButton("Assign", () => ShowLineupAssignmentPopup(slot), replacements.Length > 0, "No eligible replacement is available"),
            CreateDetailButton("Remove", () => State.RemoveLineupSlot(slot), assignment is not null, "No player assigned"),
            CreateDetailButton("Swap", () => ShowLineupSwapPopup(slot), assignment is not null, "No player assigned"),
            CreateDetailButton("View Dossier", () => OpenDossierFor(assignment!.PersonId), assignment is not null, "No player assigned"),
            CreateDetailButton("Auto-fill", () => State.AutoFillLineup()));
        return panel;
    }

    private UIElement BuildTacticsDetail(SelectablePersonRow? row)
    {
        if (row is null)
        {
            return EmptyDetail("Tactics", "Select a tactical area to review or adjust.");
        }

        var tactics = State.CurrentTactics;
        var panel = CreateDetailPanel(row.Name, row.Summary);
        if (row.Kind == "TacticsSummary")
        {
            AddLine(panel, "Style", TacticsService.Display(tactics.Style));
            AddLine(panel, "System", TacticsService.Display(tactics.System));
            AddLine(panel, "Coach philosophy", $"{tactics.CoachName} - {tactics.CoachPhilosophy}");
            AddLine(panel, "Tactical fit", $"{tactics.FitReport.Grade} ({tactics.FitReport.Score}/100)");
            AddLine(panel, "Coach recommendation", tactics.FitReport.CoachRecommendation);
            AddSubHeader(panel, "Style Actions");
            AddActions(panel,
                CreateDetailButton("Set Balanced", () => State.SetTacticalStyle(TacticalStyle.Balanced)),
                CreateDetailButton("Set Offensive", () => State.SetTacticalStyle(TacticalStyle.Offensive)),
                CreateDetailButton("Set Defensive", () => State.SetTacticalStyle(TacticalStyle.Defensive)),
                CreateDetailButton("Set Speed", () => State.SetTacticalStyle(TacticalStyle.Speed)),
                CreateDetailButton("Set Development", () => State.SetTacticalStyle(TacticalStyle.YouthDevelopment)),
                CreateDetailButton("Auto Set From Coach", () => State.AutoSetTacticsFromCoach()));
        }
        else if (row.PersonId == "even-strength")
        {
            AddLine(panel, "Forecheck", TacticsService.Display(tactics.Settings.Forecheck));
            AddLine(panel, "Neutral zone", TacticsService.Display(tactics.Settings.NeutralZone));
            AddLine(panel, "Defensive zone", TacticsService.Display(tactics.Settings.DefensiveZone));
            AddLine(panel, "Breakout", TacticsService.Display(tactics.Settings.Breakout));
            AddLine(panel, "Shot preference", TacticsService.Display(tactics.Settings.ShotPreference));
            AddLine(panel, "Physicality", tactics.Settings.Physicality.ToString());
            AddLine(panel, "Risk", tactics.Settings.RiskLevel.ToString());
            AddActions(panel,
                CreateDetailButton("Set Forecheck", State.CycleForecheck),
                CreateDetailButton("Set Neutral Zone", State.CycleNeutralZone),
                CreateDetailButton("Set Defensive Zone", State.CycleDefensiveZone),
                CreateDetailButton("Set Breakout", State.CycleBreakout),
                CreateDetailButton("Set Shot Preference", State.CycleShotPreference),
                CreateDetailButton("Set Physicality", State.CyclePhysicality),
                CreateDetailButton("Set Risk", State.CycleRisk));
        }
        else if (row.PersonId == "special-teams-tactics")
        {
            AddLine(panel, "Power play style", TacticsService.Display(tactics.Settings.PowerPlayStyle));
            AddLine(panel, "Penalty kill style", TacticsService.Display(tactics.Settings.PenaltyKillStyle));
            AddLine(panel, "PP units", string.Join(" | ", State.CurrentGameUsage.SpecialTeams.PowerPlayUnits.Select(unit => $"PP{unit.UnitNumber} {unit.Players.Count} players")));
            AddLine(panel, "PK units", string.Join(" | ", State.CurrentGameUsage.SpecialTeams.PenaltyKillUnits.Select(unit => $"PK{unit.UnitNumber} {unit.Players.Count} players")));
            AddActions(panel,
                CreateDetailButton("Set PP Style", State.CyclePowerPlayTactic),
                CreateDetailButton("Set PK Style", State.CyclePenaltyKillTactic),
                CreateDetailButton("Review Game Usage", () => SelectWorkspaceScreen("Hockey Operations", "Lineup")));
        }
        else if (row.Kind == "TacticsReport")
        {
            AddSubHeader(panel, "Strengths");
            foreach (var strength in tactics.FitReport.Strengths)
            {
                AddParagraph(panel, $"- {strength}");
            }

            AddSubHeader(panel, "Weaknesses");
            foreach (var weakness in tactics.FitReport.Weaknesses)
            {
                AddParagraph(panel, $"- {weakness}");
            }

            AddSubHeader(panel, "Risk Warnings");
            foreach (var warning in tactics.FitReport.RiskWarnings.DefaultIfEmpty("No major tactical risk warning."))
            {
                AddParagraph(panel, $"- {warning}");
            }
        }
        else if (row.Kind == "TacticsRecommendation")
        {
            var recommendation = tactics.Recommendations.FirstOrDefault(item => item.RecommendationId == row.PersonId);
            if (recommendation is not null)
            {
                AddLine(panel, "Priority", recommendation.IsImportant ? "Important" : "Normal");
                AddLine(panel, "Reason", recommendation.Reason);
                AddLine(panel, "Suggested action", recommendation.SuggestedAction);
            }
        }

        AddSubHeader(panel, "Player Impact Preview");
        foreach (var impact in tactics.PlayerImpacts.Take(8))
        {
            AddParagraph(panel, $"{impact.PlayerName}: role {impact.RoleSatisfactionModifier:+#;-#;0}, development {impact.DevelopmentModifier:+#;-#;0}, confidence {impact.ConfidenceModifier:+#;-#;0}. {impact.Summary}");
        }

        return panel;
    }

    private UIElement BuildGameUsageDetail(string unitId)
    {
        var usage = State.CurrentGameUsage;
        var panel = CreateDetailPanel("Game Usage", unitId);
        if (unitId == "game-usage-summary")
        {
            AddParagraph(panel, usage.Summary);
            AddSubHeader(panel, "Coach Recommendations");
            foreach (var recommendation in usage.CoachRecommendations)
            {
                AddParagraph(panel, $"{(recommendation.IsImportant ? "Important" : "Note")}: {recommendation.PlayerName} - {recommendation.Reason} Recommended: {recommendation.SuggestedAction}");
            }
        }
        else if (unitId.StartsWith("power-play:", StringComparison.Ordinal) && int.TryParse(unitId["power-play:".Length..], out var ppUnit))
        {
            var unit = usage.SpecialTeams.PowerPlayUnits.FirstOrDefault(unit => unit.UnitNumber == ppUnit);
            if (unit is not null)
            {
                AddLine(panel, "LW", unit.LeftWing?.PlayerName ?? "open");
                AddLine(panel, "C", unit.Center?.PlayerName ?? "open");
                AddLine(panel, "RW", unit.RightWing?.PlayerName ?? "open");
                AddLine(panel, "QB Defense", unit.QuarterbackDefense?.PlayerName ?? "open");
                AddLine(panel, "Net Front / Second Defense", unit.NetFrontOrSecondDefense?.PlayerName ?? "open");
            }
        }
        else if (unitId.StartsWith("penalty-kill:", StringComparison.Ordinal) && int.TryParse(unitId["penalty-kill:".Length..], out var pkUnit))
        {
            var unit = usage.SpecialTeams.PenaltyKillUnits.FirstOrDefault(unit => unit.UnitNumber == pkUnit);
            if (unit is not null)
            {
                AddLine(panel, "LW", unit.LeftWing?.PlayerName ?? "open");
                AddLine(panel, "RW", unit.RightWing?.PlayerName ?? "open");
                AddLine(panel, "LD", unit.LeftDefense?.PlayerName ?? "open");
                AddLine(panel, "RD", unit.RightDefense?.PlayerName ?? "open");
            }
        }
        else if (unitId == "goalie-usage")
        {
            foreach (var goalie in usage.GoalieUsage)
            {
                AddParagraph(panel, $"{goalie.UsageRole}: {goalie.PlayerName} | Starts {goalie.GamesStarted}/{goalie.ExpectedStarts} | {goalie.Workload}. {goalie.RestRecommendation}");
            }
        }
        else if (unitId == "extra-attacker")
        {
            AddParagraph(panel, string.Join(Environment.NewLine, usage.SpecialTeams.ExtraAttacker.Players.Select(player => $"- {player.PlayerName} | {player.Position} | {LineupDisplay.Role(player.CurrentRole)}")));
        }
        else if (unitId == "three-on-three")
        {
            AddLine(panel, "Combination", usage.SpecialTeams.ThreeOnThree.Combination);
            AddParagraph(panel, string.Join(Environment.NewLine, usage.SpecialTeams.ThreeOnThree.Players.Select(player => $"- {player.PlayerName} | {player.Position} | {LineupDisplay.Role(player.CurrentRole)}")));
        }
        else if (unitId == "shootout")
        {
            foreach (var shooter in usage.SpecialTeams.ShootoutOrder.Shooters.Select((player, index) => new { player, index }))
            {
                AddParagraph(panel, $"{shooter.index + 1}. {shooter.player.PlayerName} | {shooter.player.Position} | {LineupDisplay.Role(shooter.player.CurrentRole)}");
            }
        }

        AddSubHeader(panel, "Player Usage Summary");
        foreach (var profile in usage.PlayerProfiles.Take(8))
        {
            AddParagraph(panel, $"{profile.PlayerName}: {profile.CurrentLine} | {profile.PowerPlayUsage} | {profile.PenaltyKillUsage} | {profile.ShootoutUsage}. {profile.CoachComment}");
        }

        AddActions(panel,
            CreateDetailButton("Assign", () => State.AssignNextGameUsage(unitId)),
            CreateDetailButton("Remove", () => State.RemoveGameUsage(unitId)),
            CreateDetailButton("Swap", () => State.SwapGameUsage(unitId)),
            CreateDetailButton("Auto Fill", () => State.AutoFillGameUsage()));
        return panel;
    }

    private UIElement BuildLineChemistryDetail(string unitId)
    {
        var chemistry = State.LineChemistryUnit(unitId);
        if (chemistry is null)
        {
            return EmptyDetail("Line Chemistry", "Select a line, pair, or goalie room to view chemistry.");
        }

        var panel = CreateDetailPanel(chemistry.Label, $"Chemistry: {chemistry.Score.Grade} ({chemistry.Score.Value}, {chemistry.Score.ScoreBand})");
        AddLine(panel, "Players", chemistry.PlayerNames.Count == 0 ? "None assigned" : string.Join(", ", chemistry.PlayerNames));
        AddLine(panel, "Coach recommendation", chemistry.Recommendation);
        AddLine(panel, "Development note", chemistry.DevelopmentNote);
        AddLine(panel, "Relationship note", chemistry.RelationshipNote);
        AddLine(panel, "Role promise note", chemistry.RolePromiseNote);

        AddSubHeader(panel, "Strengths");
        foreach (var strength in chemistry.Strengths)
        {
            AddParagraph(panel, strength);
        }

        AddSubHeader(panel, "Weaknesses");
        foreach (var weakness in chemistry.Weaknesses)
        {
            AddParagraph(panel, weakness);
        }

        AddSubHeader(panel, "Factors");
        foreach (var factor in chemistry.Factors)
        {
            AddParagraph(panel, $"{factor.FactorType}: {factor.Modifier:+#;-#;0} - {factor.Summary}");
        }

        AddActions(panel, CreateDetailButton("Auto-fill", () => State.AutoFillLineup()));
        return panel;
    }

    private static bool TryLineupSlot(string value, out LineupSlot slot)
    {
        slot = default;
        const string prefix = "lineup-slot:";
        return value.StartsWith(prefix, StringComparison.Ordinal)
            && Enum.TryParse(value[prefix.Length..], out slot);
    }

    private void ShowLineupAssignmentPopup(LineupSlot slot)
    {
        var replacements = State.EligibleLineupReplacements(slot);
        var panel = CreateDetailPanel($"Assign {LineupDisplay.SlotLabel(slot)}", "Eligible replacements");
        AddParagraph(panel, "Choose one eligible player for this lineup slot. Invalid positions, injured players, unsigned prospects, and duplicate active assignments are filtered out.");
        var list = new ListBox
        {
            ItemsSource = replacements,
            DisplayMemberPath = nameof(LineupRoleAssignment.PlayerName),
            MinHeight = 260,
            Margin = new Thickness(0, 8, 0, 12)
        };
        list.SelectedItem = replacements.FirstOrDefault();
        panel.Children.Add(list);
        AddActions(panel,
            CreateDetailButton("Assign", () =>
            {
                if (list.SelectedItem is LineupRoleAssignment selected)
                {
                    State.AssignLineupSlot(slot, selected.PersonId);
                    Window.GetWindow(panel)?.Close();
                }
            }, replacements.Count > 0, "No eligible replacement is available"),
            CreateDetailButton("Cancel", () => Window.GetWindow(panel)?.Close()));
        ShowPopup($"Assign {LineupDisplay.SlotLabel(slot)}", panel, 560, 520, includeCloseButton: false);
    }

    private void ShowLineupSwapPopup(LineupSlot slot)
    {
        var slots = State.LineupSlots
            .Where(other => other != slot)
            .Select(other => new LineupSlotChoice(other, $"{LineupDisplay.SlotLabel(other)} - {State.LineupSlotPlayerText(other)}"))
            .ToArray();
        var panel = CreateDetailPanel($"Swap {LineupDisplay.SlotLabel(slot)}", "Lineup slots");
        AddParagraph(panel, "Choose another occupied slot. The engine validates position fit before changing the lineup.");
        var list = new ListBox
        {
            ItemsSource = slots,
            DisplayMemberPath = nameof(LineupSlotChoice.Display),
            MinHeight = 260,
            Margin = new Thickness(0, 8, 0, 12)
        };
        list.SelectedItem = slots.FirstOrDefault();
        panel.Children.Add(list);
        AddActions(panel,
            CreateDetailButton("Swap", () =>
            {
                if (list.SelectedItem is LineupSlotChoice selected)
                {
                    State.SwapLineupSlots(slot, selected.Slot);
                    Window.GetWindow(panel)?.Close();
                }
            }, slots.Length > 0, "No other slot is available"),
            CreateDetailButton("Cancel", () => Window.GetWindow(panel)?.Close()));
        ShowPopup($"Swap {LineupDisplay.SlotLabel(slot)}", panel, 560, 520, includeCloseButton: false);
    }

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
                    $"{profile.Position} - age {profile.Age?.ToString() ?? "unknown"} - {State.RatingText(recruit.RecruitPersonId)} - {profile.Status}",
                    $"Interest {profile.InterestLevel} | top: {State.RecruitPrioritySummary(recruit.RecruitPersonId, 1)} | offers: {State.RecruitOfferState(recruit.RecruitPersonId)}",
                    $"{profile.RegionOrHometown} | {profile.CurrentTeam} | {profile.ProjectionSummary}");
            })
            .ToArray();

    private IReadOnlyList<SelectablePersonRow> BuildFreeAgentRows() =>
        State.FreeAgents
            .OrderByDescending(agent => agent.IsShortlisted)
            .ThenByDescending(agent => agent.FitSummary.FitScore)
            .ThenBy(agent => agent.Name, StringComparer.Ordinal)
            .Select(agent => new SelectablePersonRow(
                agent.PersonId,
                agent.Name,
                "FreeAgent",
                $"{agent.Position} - age {agent.Age} - {State.RatingText(agent.PersonId)} - {agent.Status}",
                $"{agent.PreviousTeam} | ask {agent.ContractAsk.AnnualAmount:C0} | {State.AgentSummary(agent.PersonId)}",
                $"Interest {agent.Interest.PlayerOrganizationInterest}/100 | {State.PositionMarketNote(agent.PersonId)} | {State.FreeAgentOfferResponseText(agent.PersonId)} | {State.FreeAgentCompetitionSummary(agent.PersonId)}"))
            .ToArray();

    private IReadOnlyList<SelectablePersonRow> BuildTradeRows() =>
        State.TradeBlockEntries
            .OrderByDescending(entry => entry.InterestLevel)
            .ThenByDescending(entry => entry.AssetValue)
            .ThenBy(entry => entry.Name, StringComparer.Ordinal)
            .Select(entry => new SelectablePersonRow(
                entry.PersonId,
                entry.Name,
                "TradeBlock",
                $"{entry.TeamName} | {State.PositionShortText(entry.Position)} | age {entry.Age} | {State.RatingText(entry.PersonId)} | {entry.CurrentRole}",
                $"Salary {entry.SalaryImpact:C0} | Ask: {entry.AskingPriceSummary}",
                $"{State.TradeTeamNeedsShortText(entry)} | {State.PositionMarketNote(entry.PersonId)} | {entry.ReasonAvailable} | Interest {entry.InterestLevel} | {entry.PlayerType} | trade target type: {State.TradeTargetType(entry)}"))
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
                State.DraftIntelligenceRowText(entry),
                $"{State.ScoutingConfidenceSummary(entry.ProspectPersonId)} | {State.PositionMarketNote(entry.ProspectPersonId)} | Scout: {State.AssignedScoutText(entry.ProspectPersonId)}",
                $"{State.ScoutingReportHeadline(entry.ProspectPersonId)} | {State.DraftFuturePicture(entry)} | {State.DraftClassContext(entry)}"))
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
                $"{(entry.IsStarred ? "Starred " : string.Empty)}{State.DraftIntelligenceRowText(entry)}",
                $"Confidence {entry.ScoutingConfidence?.ToString() ?? "Unknown"} | {State.PositionMarketNote(entry.ProspectPersonId)} | Projection: {entry.ProjectionText}",
                $"{State.DraftCurrentPicture(entry)} | Risk: {State.DraftRiskText(entry)} | {State.DraftClassContext(entry)}"))
            .ToArray();

    private IReadOnlyList<SelectablePersonRow> BuildProspectRows() =>
        State.ScenarioSnapshot.ProspectRights
            .OrderBy(prospect => prospect.PickNumber)
            .Select(prospect =>
            {
                var card = State.DraftIntelligenceCard(prospect.ProspectPersonId);
                return new SelectablePersonRow(
                    prospect.ProspectPersonId,
                    prospect.ProspectName,
                    "Prospect",
                    $"{prospect.Position} - {card.RatingDisplay} - {prospect.Status} | {State.PipelineText(prospect.ProspectPersonId)}",
                    $"Age {prospect.Age} | {prospect.CurrentTeam} {prospect.CurrentLeague} | {State.PositionMarketNote(prospect.ProspectPersonId)} | scout #{card.ScoutBoardRank} | consensus #{card.ConsensusBoardRank}",
                    $"Recommended assignment: {State.PipelineRecommendationText(prospect.ProspectPersonId)} | Projection: {prospect.ProjectionText} | Fit {card.TeamFitScore}/100");
            })
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
            .Concat(BuildFreeAgentRows())
            .Concat(BuildTradeRows())
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
            var empty = EmptyDetail("Staff", "Current Staff are listed with active roles. Staff Market candidates appear in Hire Staff.");
            AddSubHeader(empty, "Current Staff");
            AddParagraph(empty, "Select an employed staff member to view profile, focus, chemistry, and staff actions.");
            AddSubHeader(empty, "Vacant Positions");
            AddParagraph(empty, State.StaffVacancySummary);
            AddSubHeader(empty, "Living Staff Market");
            AddParagraph(empty, "Market rows show availability, current employer, interest, salary ask, strengths, weaknesses, chemistry risk, and recommendation.");
            AddActions(empty, CreateDetailButton("Review Staff Market", () => State.RefreshStaffMarket()), CreateDetailButton("Staff Warning", GenerateStaffConflictWarning));
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
                AddActions(panel, CreateDetailButton("Refresh Market", () => State.RefreshStaffMarket()));
            }
            else if (row.PersonId == "staff-section:vacancies")
            {
                AddActions(panel, CreateDetailButton("Review Staff Market", () => State.RefreshStaffMarket()));
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
            AddParagraph(panel, "Review the staff market, compare fit and salary, then hire a candidate for this role.");
            AddActions(panel,
                CreateDetailButton("Review Staff Market", () => State.RefreshStaffMarket()));
            return panel;
        }

        if (row.Kind == "Candidate")
        {
            var market = State.StaffMarketCandidateFor(row.PersonId);
            if (market is null)
            {
                return EmptyDetail("Staff Candidate", "This candidate is no longer available.");
            }

            var candidate = market.Candidate;
            var panel = CreateDetailPanel(candidate.Person.Identity.DisplayName, "Staff candidate");
            AddLine(panel, "Role", candidate.StaffMember.CurrentRole);
            AddLine(panel, "Department", candidate.StaffMember.Department);
            AddLine(panel, "Market status", market.Status);
            AddLine(panel, "Hiring interest", $"{market.HiringInterest}/100");
            AddLine(panel, "Reason available", market.ReasonAvailable);
            AddLine(panel, "Role fit", candidate.RoleFit);
            AddLine(panel, "Department fit", candidate.DepartmentFit);
            AddLine(panel, "Reputation", candidate.Reputation);
            AddLine(panel, "Salary ask", $"{candidate.ExpectedSalary.AnnualAmount:C0}");
            AddLine(panel, "Current employer", market.CurrentEmployer);
            AddLine(panel, "Years experience", candidate.YearsExperience);
            AddLine(panel, "Strengths", string.Join(", ", candidate.Strengths));
            AddLine(panel, "Weaknesses", string.Join(", ", candidate.Weaknesses));
            AddLine(panel, "Chemistry risk", candidate.ChemistryRisk);
            AddLine(panel, "Recommendation", candidate.HiringRecommendation);
            AddSubHeader(panel, "Career History");
            foreach (var line in market.CareerHistory.Take(4))
            {
                AddParagraph(panel, line);
            }

            AddSubHeader(panel, "Hiring Fit");
            AddParagraph(panel, State.CandidateHiringFitText(row.PersonId));
            AddParagraph(panel, candidate.HiringRecommendation);
            AddActions(panel,
                CreateDetailButton("Person Card", () => OpenUniversalPersonCard(row.PersonId)),
                CreateDetailButton("Hire Candidate", () => State.HireCandidateFor(row.PersonId), market.CanBeHired, market.Status == StaffMarketStatus.Hired ? "Already hired" : "Candidate is employed elsewhere"),
                CreateDetailButton("Approach Candidate", () => ShowConfirmationPopup("Approach Candidate", "Approach Candidate is a placeholder for employed staff interest."), !market.CanBeHired, "Available candidates can be hired directly"),
                CreateDetailButton("Compare", () => MessageBox.Show(State.CompareCandidateText(row.PersonId), "Compare Candidate", MessageBoxButton.OK, MessageBoxImage.Information)),
                CreateDetailButton("Salary Offer", () => MessageBox.Show("Salary offer is a placeholder for now. Hiring uses the listed salary ask.", "Salary Offer", MessageBoxButton.OK, MessageBoxImage.Information)));
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
        AddSubHeader(detail, "Coaching Philosophy");
        AddParagraph(detail, State.StaffCoachingProfileText(row.PersonId));
        AddSubHeader(detail, "Staff Chemistry");
        AddParagraph(detail, State.StaffChemistryText(row.PersonId));
        AddSubHeader(detail, "Organization Chart");
        AddParagraph(detail, State.OrganizationChartText());
        AddSubHeader(detail, "Department Grades");
        AddParagraph(detail, State.DepartmentGradesText());
        AddActions(detail,
            CreateDetailButton("Person Card", () => OpenUniversalPersonCard(row.PersonId)),
            CreateDetailButton("View Profile", () => ShowStaffProfile(row.PersonId)),
            CreateDetailButton("View Dossier/Profile", () => OpenDossierFor(row.PersonId)),
            CreateDetailButton("Reassign Role", () => State.ReassignStaffRoleFor(row.PersonId)),
            CreateDetailButton("Release Staff", () => ConfirmDestructiveAction(
                "Release Staff Member",
                $"{row.Name} will be released from the staff if permitted by role rules. Continue?",
                () => State.ReleaseStaffFor(row.PersonId))),
            CreateDetailButton("Set Focus", () => SetStaffFocusFor(row.PersonId)),
            CreateDetailButton("Generate Evaluation", () => State.GenerateStaffEvaluationFor(row.PersonId)),
            CreateDetailButton("Performance Review", () => MessageBox.Show(State.StaffPerformanceReviewText(row.PersonId), "Staff Performance Review", MessageBoxButton.OK, MessageBoxImage.Information)),
            CreateDetailButton("Staff Meeting", () => MessageBox.Show(State.MonthlyStaffMeetingText(), "Monthly Staff Meeting", MessageBoxButton.OK, MessageBoxImage.Information)));
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
        AddSubHeader(panel, "Scout Intelligence");
        AddParagraph(panel, State.ScoutIntelligenceProfileText(row.PersonId));
        AddParagraph(panel, State.ScoutCareerText(row.PersonId));
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
        AddLine(panel, "Pipeline", State.PipelineText(row.PersonId));
        AddLine(panel, "GM relationship", $"{State.RelationshipWithGm(row.PersonId)}/100");
        AddParagraph(panel, row.Summary);

        if (row.Kind == "RosterSummary")
        {
            AddLine(panel, "Roster count", State.RosterBreakdownTitle);
            AddLine(panel, "Position breakdown", State.RosterBreakdownSecondary);
            AddLine(panel, "Age mix", State.RosterAgeBreakdown);
            AddLine(panel, "Contracts", State.RosterContractBreakdown);
            AddParagraph(panel, "Drafted prospects stay on the prospect/draft-rights list until you explicitly offer a contract, invite them to camp, return them to junior/youth while retaining rights where allowed, assign them to an affiliate where valid, or release their rights.");
            return panel;
        }

        AddLine(panel, "Ratings", State.RatingContextText(row.PersonId));
        AddLine(panel, "Asset value", State.AssetValueShortText(row.PersonId));
        AddLine(panel, "Position market", State.PositionMarketNote(row.PersonId));

        if (tab == "Roster")
        {
            AddLine(panel, "Name", row.Name);
            AddLine(panel, "Position", State.PersonPosition(row.PersonId));
            AddLine(panel, "Age", State.PersonAge(row.PersonId)?.ToString() ?? "unknown");
            AddLine(panel, "Player type", State.PlayerType(row.PersonId));
            AddLine(panel, "Current lineup role", State.CurrentLineupRole(row.PersonId));
            AddLine(panel, "Expected role", State.ExpectedRoleText(row.PersonId));
            AddLine(panel, "Promised role", State.PromisedRoleText(row.PersonId));
            AddLine(panel, "Promise status", State.PromiseStatusText(row.PersonId));
            AddLine(panel, "Role satisfaction", State.RoleSatisfactionText(row.PersonId));
            AddLine(panel, "Potential lineup role", State.PotentialLineupRole(row.PersonId));
            AddLine(panel, "Current line/pair", State.CurrentLinePair(row.PersonId));
            AddLine(panel, "Development stage", State.DevelopmentStageText(row.PersonId));
            AddLine(panel, "Lineup development impact", State.LineupDevelopmentImpactText(row.PersonId));
            AddLine(panel, "Coach note", State.LineupCoachNote(row.PersonId));
            AddLine(panel, "Last-season stats", State.LastSeasonStats(row.PersonId));
            AddLine(panel, "Career summary", State.CareerStatSummary(row.PersonId));
            AddLine(panel, "Contract / rights status", State.ContractRightsStatus(row.PersonId));
            AddLine(panel, "Waiver status", State.WaiverStatusText(row.PersonId));
            AddLine(panel, "Development trend", State.DevelopmentTrend(row.PersonId));
            AddLine(panel, "Injury status", State.InjuryStatus(row.PersonId));
            AddSubHeader(panel, "Contract");
            AddParagraph(panel, State.PlayerContractDetailsText(row.PersonId));
            AddSubHeader(panel, "Health & Medical");
            AddParagraph(panel, State.HealthProfileText(row.PersonId));
            AddSubHeader(panel, "Development Plan");
            AddParagraph(panel, State.DevelopmentPlanText(row.PersonId));
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

        if (tab == "Free Agents")
        {
            var agent = State.FreeAgentFor(row.PersonId);
            if (agent is not null)
            {
                var market = State.FreeAgencyState;
                var staffRecommendations = State.FreeAgentStaffRecommendations(row.PersonId);
                AddLine(panel, "Market phase", $"{market.Window.Phase} | opens {market.Window.OpensOn:yyyy-MM-dd} | closes {market.Window.ClosesOn:yyyy-MM-dd}");
                AddLine(panel, "Position", agent.Position);
                AddLine(panel, "Age", agent.Age);
                AddLine(panel, "Shoots/Catches", agent.ShootsCatches);
                AddLine(panel, "Height / Weight", $"{agent.HeightDisplay}, {agent.WeightDisplay}");
                AddLine(panel, "Nationality / hometown", $"{agent.Nationality} / {agent.Hometown}");
                AddLine(panel, "Previous team", agent.PreviousTeam);
                AddLine(panel, "Last-season stats", agent.LastSeasonStats.SummaryText);
                AddLine(panel, "Career summary", agent.CareerStats.DisplaySummary);
                AddLine(panel, "Player type", agent.PlayerType);
                AddLine(panel, "Projected role", agent.ProjectedLineupRole);
                AddLine(panel, "Contract ask", $"{agent.ContractAsk.TermYears} year(s), {agent.ContractAsk.AnnualAmount:C0} {agent.ContractAsk.Currency} - {agent.ContractAsk.Notes}");
                AddLine(panel, "Agent Card", State.AgentSummary(row.PersonId));
                AddLine(panel, "Agent comments", State.AgentOfferComment(row.PersonId));
                AddLine(panel, "Interest", $"{agent.Interest.PlayerOrganizationInterest}/100 - {agent.Interest.MotivationSummary}");
                AddLine(panel, "Top motivations", State.FreeAgentTopMotivations(row.PersonId));
                AddLine(panel, "Pending response", State.FreeAgentOfferResponseText(row.PersonId));
                AddLine(panel, "Offer likelihood", State.FreeAgentOfferLikelihood(row.PersonId));
                AddLine(panel, "Competing offers", State.FreeAgentCompetitionSummary(row.PersonId));
                AddLine(panel, "Competing interest", agent.Interest.CompetingInterest);
                AddLine(panel, "Budget impact", State.FreeAgentBudgetImpact(row.PersonId));
                AddLine(panel, "Staff recommendation", agent.FitSummary.StaffRecommendation);
                AddLine(panel, "Head coach", staffRecommendations.HeadCoach);
                AddLine(panel, "Scout", staffRecommendations.Scout);
                AddLine(panel, "Medical", staffRecommendations.Medical);
                AddLine(panel, "Owner", staffRecommendations.Owner);
                AddLine(panel, "Assistant GM", staffRecommendations.AssistantGm);
                AddLine(panel, "Fit / risk", $"{agent.FitSummary.RosterNeed} {agent.FitSummary.RiskSummary}");
                AddLine(panel, "Scouting confidence", agent.ScoutingConfidence);
                AddLine(panel, "Rights / eligibility", agent.RightsEligibilityNotes);
            }
        }

        if (tab is "Scouting" or "Draft Board")
        {
            var entry = State.Snapshot.DraftBoard.Entries.FirstOrDefault(entry => entry.ProspectPersonId == row.PersonId);
            if (entry is not null)
            {
                AddLine(panel, "Known position", State.DraftPositionText(entry));
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

                AddLine(panel, "Current picture", State.DraftCurrentPicture(entry));
                AddLine(panel, "Future projection", State.DraftFuturePicture(entry));
                AddLine(panel, "Class context", State.DraftClassContext(entry));
                AddLine(panel, "Risk", State.DraftRiskText(entry));
                AddLine(panel, "Report", entry.ScoutingReportId ?? "none");
                AddLine(panel, "Analytics", string.IsNullOrWhiteSpace(entry.AnalyticsSummary) ? "not available" : entry.AnalyticsSummary);
                AddLine(panel, "GM notes", string.IsNullOrWhiteSpace(entry.PersonalNotes) ? "none" : entry.PersonalNotes);
                AddSubHeader(panel, "Scouting Intelligence");
                AddParagraph(panel, State.ScoutingKnowledgeText(row.PersonId));
                AddParagraph(panel, State.ScoutingReportsText(row.PersonId));
                AddSubHeader(panel, "Report Comparison");
                AddParagraph(panel, State.ScoutingComparisonText(row.PersonId));
            }
        }

        if (tab is "Scouting" or "Draft Board")
        {
            AddLine(panel, "Position", State.PersonPosition(row.PersonId));
            AddLine(panel, "Age", State.PersonAge(row.PersonId)?.ToString() ?? "unknown");
            AddLine(panel, "Region/team", State.RegionTeamText(row.PersonId));
            AddLine(panel, "Assigned scout", State.AssignedScoutText(row.PersonId));
            AddLine(panel, "Report status", State.ScoutingReportStatus(row.PersonId));
            AddLine(panel, "Budget coverage", State.ScoutingBudgetText);
        }

        if (tab == "Prospect List")
        {
            var prospect = State.ScenarioSnapshot.ProspectRights.FirstOrDefault(prospect => prospect.ProspectPersonId == row.PersonId);
            if (prospect is not null)
            {
                AddLine(panel, "Draft", $"Round {prospect.RoundNumber}, pick {prospect.PickNumber}");
                AddLine(panel, "Rights status", prospect.Status);
                AddLine(panel, "Current team", string.IsNullOrWhiteSpace(prospect.CurrentTeam) ? "unknown" : prospect.CurrentTeam);
                AddLine(panel, "Current level", State.PipelineDevelopmentLevelText(row.PersonId));
                AddLine(panel, "Signed?", State.PipelineSignedText(row.PersonId));
                AddLine(panel, "AHL eligible?", State.PipelineAhlEligibleText(row.PersonId));
                AddLine(panel, "Junior eligible?", State.PipelineJuniorEligibleText(row.PersonId));
                AddLine(panel, "Contract slide", State.PipelineSlideText(row.PersonId));
                AddLine(panel, "Recommended assignment", State.PipelineRecommendationText(row.PersonId));
                AddLine(panel, "Confidence", prospect.ScoutingConfidence?.ToString() ?? "Unknown");
                AddLine(panel, "GM notes", string.IsNullOrWhiteSpace(prospect.GmNotes) ? "none" : prospect.GmNotes);
                AddSubHeader(panel, "Development Plan");
                AddParagraph(panel, State.DevelopmentPlanText(row.PersonId));
            }
        }

        AddSubHeader(panel, "Coach Opinion");
        AddParagraph(panel, State.PlayerCoachFitText(row.PersonId));
        AddSubHeader(panel, "Medical Report");
        AddParagraph(panel, State.MedicalReportText(row.PersonId));

        AddActions(panel, BuildPlayerActionButtons(tab, row).ToArray());
        return panel;
    }

    private UIElement BuildTradeDetail(SelectablePersonRow? row)
    {
        if (row is null)
        {
            var empty = (StackPanel)EmptyDetail("Trades", "Select a player on the league trade block to review team needs, trade value, and possible counters.");
            AddSubHeader(empty, "Trade Workspace");
            AddLine(empty, "Left", "Trade block");
            AddLine(empty, "Middle", "Trade builder and projected package");
            AddLine(empty, "Right", "Selected player, evaluation, budget impact, roster impact, reasons, and counter");
            AddSubHeader(empty, "Deadline Status");
            AddLine(empty, "Status", State.TradeDeadlineWindow.Status);
            AddLine(empty, "Deadline date", State.TradeDeadlineWindow.DeadlineDate.ToString("yyyy-MM-dd"));
            AddLine(empty, "Days remaining", State.TradeDeadlineWindow.DaysRemaining);
            AddLine(empty, "Buyer/seller read", State.TradeDeadlineAssessmentSummary);
            AddLine(empty, "Trade block", State.TradeDeadlineBlockSummary);
            AddSubHeader(empty, "Rumors");
            foreach (var rumor in State.DeadlineRumors.Take(4))
            {
                AddParagraph(empty, $"{rumor.TeamName}: {rumor.Summary} ({rumor.Confidence})");
            }

            return empty;
        }

        var entry = State.TradeBlockEntryFor(row.PersonId);
        if (entry is null)
        {
            return EmptyDetail("Trades", "Selected trade block player is no longer available.");
        }

        var panel = CreateDetailPanel(entry.Name, $"{entry.TeamName} | {State.PositionShortText(entry.Position)} | age {entry.Age}");
        AddSubHeader(panel, "Deadline Status");
        AddLine(panel, "Status", State.TradeDeadlineWindow.Status);
        AddLine(panel, "Deadline date", State.TradeDeadlineWindow.DeadlineDate.ToString("yyyy-MM-dd"));
        AddLine(panel, "Days remaining", State.TradeDeadlineWindow.DaysRemaining);
        AddLine(panel, "Buyer/seller read", State.TradeDeadlineAssessmentSummary);
        AddLine(panel, "Rumor count", State.DeadlineRumors.Count);
        AddSubHeader(panel, "Target");
        AddLine(panel, "Player type", entry.PlayerType);
        AddLine(panel, "Current role", entry.CurrentRole);
        AddLine(panel, "Potential role", State.TradePotentialRole(entry));
        AddLine(panel, "Trade target type", State.TradeTargetType(entry));
        AddLine(panel, "Contract", entry.ContractStatus);
        AddLine(panel, "Salary / budget impact", $"{entry.SalaryImpact:C0}");
        AddLine(panel, "Asking price", entry.AskingPriceSummary);
        AddLine(panel, "Reason available", entry.ReasonAvailable);
        AddLine(panel, "Interest level", entry.InterestLevel);
        AddSubHeader(panel, "Other Team Strategy");
        AddParagraph(panel, State.TradeTeamNeedsText(entry.PersonId));
        AddSubHeader(panel, "Trade Value Screen");
        AddParagraph(panel, State.TradeValueText(entry.PersonId));
        AddSubHeader(panel, "Trade Builder");
        AddLine(panel, "Projected offer", State.TradeProjectedOfferText(entry.PersonId));
        AddLine(panel, "Projected roster impact", State.TradeProjectedRosterImpact(entry.PersonId));
        AddLine(panel, "Projected budget impact", State.TradeProjectedBudgetImpact(entry.PersonId));
        AddLine(panel, "Latest response", State.LatestTradeResponseText);
        AddLine(panel, "Counter", State.LatestTradeCounterText);
        AddSubHeader(panel, "Evaluation Reasons");
        foreach (var reason in State.LatestTradeReasons.Take(5))
        {
            AddParagraph(panel, reason);
        }

        AddSubHeader(panel, "Staff / Player Reaction");
        foreach (var reaction in State.LatestTradeReactions.Take(5))
        {
            AddParagraph(panel, reaction);
        }

        AddActions(
            panel,
            CreateDetailButton("View Dossier", () => OpenDossierFor(entry.PersonId)),
            CreateDetailButton("Add to Trade Proposal", () => ShowTradeBuilderPopup(entry.PersonId)),
            CreateDetailButton("Propose Trade", () => ShowTradeBuilderPopup(entry.PersonId), State.TradeDeadlineWindow.TradesAllowed, "Trade deadline has passed"),
            CreateDetailButton("Withdraw Offer", () => State.WithdrawLatestTradeOffer(), State.CanWithdrawLatestTradeOffer),
            CreateDetailButton("Remove from Offer", () => State.ClearTradeBuilder(), State.HasTradeBuilderSelection));
        return panel;
    }

    private void ShowTradeBuilderPopup(string otherPlayerPersonId)
    {
        var entry = State.TradeBlockEntryFor(otherPlayerPersonId);
        if (entry is null)
        {
            ShowConfirmationPopup("Trade Builder", "Selected player is no longer available for a trade proposal.");
            return;
        }

        State.SelectTradeTarget(otherPlayerPersonId);
        ShowPopup($"Trade Builder - {entry.TeamName}", BuildTradeBuilderPopupContent(entry), 1100, 760);
        RefreshAfterAction();
    }

    private UIElement BuildTradeBuilderPopupContent(TradeBlockEntry entry)
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.05, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.85, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.85, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.05, GridUnitType.Star) });

        void Reopen()
        {
            Window.GetWindow(root)?.Close();
            ShowTradeBuilderPopup(entry.PersonId);
        }

        var yourAssets = CreateDetailPanel("Your assets", State.ScenarioSnapshot.Organization.Name);
        AddParagraph(yourAssets, "Select an asset, then add it to You Give.");
        _tradeYourAssetsList = new ListBox
        {
            ItemsSource = State.YourTradeAssetRows(),
            MinHeight = 330
        };
        _tradeYourAssetsList.SelectionChanged += (_, _) =>
        {
            if (_tradeYourAssetsList.SelectedItem is TradeAssetRow row)
            {
                State.SelectYourTradeAsset(row.Asset.AssetId);
            }
        };
        _tradeYourAssetsList.MouseDoubleClick += (_, _) =>
        {
            if (_tradeYourAssetsList.SelectedItem is TradeAssetRow row)
            {
                State.AddYourAssetToTradeProposal(row.Asset);
                Reopen();
            }
        };
        yourAssets.Children.Add(_tradeYourAssetsList);
        AddActions(yourAssets, CreateDetailButton("Add Selected From Your Team", () =>
        {
            if (_tradeYourAssetsList.SelectedItem is TradeAssetRow row)
            {
                State.AddYourAssetToTradeProposal(row.Asset);
                Reopen();
            }
        }));
        Grid.SetColumn(yourAssets, 0);
        root.Children.Add(yourAssets);

        var youGive = CreateDetailPanel("You Give", "Assets leaving your organization");
        _tradeYouGiveList = new ListBox
        {
            ItemsSource = State.CurrentTradeYouGiveRows(),
            MinHeight = 190
        };
        youGive.Children.Add(_tradeYouGiveList);
        AddActions(youGive,
            CreateDetailButton("Remove From You Give", () =>
            {
                if (_tradeYouGiveList.SelectedItem is TradeAssetRow row)
                {
                    State.RemoveYourAssetFromTradeProposal(row.Asset);
                    Reopen();
                }
            }, State.HasTradePlayerGives, "Remove Selected From Offer - You Give"));
        Grid.SetColumn(youGive, 1);
        root.Children.Add(youGive);

        var youReceive = CreateDetailPanel("Trade proposal", $"You Receive - {entry.TeamName} assets coming back");
        _tradeYouReceiveList = new ListBox
        {
            ItemsSource = State.CurrentTradeYouReceiveRows(),
            MinHeight = 190
        };
        youReceive.Children.Add(_tradeYouReceiveList);
        AddActions(youReceive,
            CreateDetailButton("Remove From You Receive", () =>
            {
                if (_tradeYouReceiveList.SelectedItem is TradeAssetRow row)
                {
                    State.RemoveOtherAssetFromTradeProposal(row.Asset);
                    Reopen();
                }
            }, State.HasTradePlayerReceives, "Remove Selected From Offer - You Receive"));
        AddSubHeader(youReceive, "Live Evaluation");
        AddLine(youReceive, "Roster impact", State.CurrentTradeRosterImpact);
        AddLine(youReceive, "Budget / cap impact", State.CurrentTradeBudgetImpact);
        AddLine(youReceive, "Value comparison", State.CurrentTradeAssetValueComparison);
        AddLine(youReceive, "Position scarcity", State.CurrentTradeScarcityText);
        AddLine(youReceive, "AI evaluation", State.CurrentTradeEvaluationText);
        AddSubHeader(youReceive, "Reasons");
        var reasons = State.CurrentTradeEvaluationReasons.Take(5).ToArray();
        if (reasons.Length == 0)
        {
            AddParagraph(youReceive, "Add assets from both sides to preview the other team's evaluation.");
        }
        else
        {
            foreach (var reason in reasons)
            {
                AddParagraph(youReceive, reason);
            }
        }

        AddSubHeader(youReceive, "Counter");
        AddParagraph(youReceive, State.CurrentTradeCounterText);
        AddActions(youReceive,
            CreateDetailButton("Accept Counter Into Offer", () =>
            {
                State.AcceptCurrentTradeCounter();
                Reopen();
            }, State.HasCurrentTradeCounter),
            CreateDetailButton("Clear Offer", () =>
            {
                State.ClearTradeBuilder();
                Reopen();
            }, State.HasTradeProposalAssets));
        Grid.SetColumn(youReceive, 2);
        root.Children.Add(youReceive);

        var otherAssets = CreateDetailPanel("Other team assets", entry.TeamName);
        AddParagraph(otherAssets, "Select an asset, then add it to You Receive.");
        _tradeOtherAssetsList = new ListBox
        {
            ItemsSource = State.OtherTradeAssetRows(entry.OrganizationId, entry.TeamName),
            MinHeight = 330
        };
        _tradeOtherAssetsList.SelectionChanged += (_, _) =>
        {
            if (_tradeOtherAssetsList.SelectedItem is TradeAssetRow row)
            {
                State.SelectOtherTradeAsset(row.Asset.AssetId);
            }
        };
        _tradeOtherAssetsList.MouseDoubleClick += (_, _) =>
        {
            if (_tradeOtherAssetsList.SelectedItem is TradeAssetRow row)
            {
                State.AddOtherAssetToTradeProposal(row.Asset);
                Reopen();
            }
        };
        otherAssets.Children.Add(_tradeOtherAssetsList);
        AddActions(otherAssets, CreateDetailButton("Add Selected From Other Team", () =>
        {
            if (_tradeOtherAssetsList.SelectedItem is TradeAssetRow row)
            {
                State.AddOtherAssetToTradeProposal(row.Asset);
                Reopen();
            }
        }));
        Grid.SetColumn(otherAssets, 3);
        root.Children.Add(otherAssets);

        var actions = new WrapPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };
        actions.Children.Add(CreateDetailButton("View Dossier", () =>
        {
            var selected = (_tradeOtherAssetsList?.SelectedItem as TradeAssetRow)?.Asset
                ?? (_tradeYourAssetsList?.SelectedItem as TradeAssetRow)?.Asset
                ?? State.CurrentTradeProposalAssets.FirstOrDefault();
            OpenDossierFor(selected?.AssetId ?? entry.PersonId);
        }));
        actions.Children.Add(CreateDetailButton("Contract Offer", () => ShowContractOfferPlaceholder(entry.PersonId), false, "Coming soon"));
        actions.Children.Add(CreateDetailButton("Propose Trade", () =>
        {
            State.ProposeCurrentTrade(entry.OrganizationId, entry.TeamName);
            RefreshAfterAction();
            Window.GetWindow(root)?.Close();
        }, State.CanProposeCurrentTrade && State.TradeDeadlineWindow.TradesAllowed, State.TradeDeadlineWindow.TradesAllowed ? "Trade must include assets on both sides" : "Trade deadline has passed"));
        actions.Children.Add(CreateDetailButton("Withdraw", () =>
        {
            State.WithdrawLatestTradeOffer();
            RefreshAfterAction();
            Window.GetWindow(root)?.Close();
        }, State.CanWithdrawLatestTradeOffer));
        actions.Children.Add(CreateDetailButton("Clear", () =>
        {
            State.ClearTradeBuilder();
            Reopen();
        }, State.HasTradeProposalAssets));
        Grid.SetRow(actions, 1);
        Grid.SetColumnSpan(actions, 4);
        root.Children.Add(actions);
        return root;
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
        yield return CreateDetailButton("Person Card", () => OpenUniversalPersonCard(row.PersonId));
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

        if (tab == "Free Agents")
        {
            var agent = State.FreeAgentFor(row.PersonId);
            yield return CreateDetailButton(agent?.IsShortlisted == true ? "Remove Shortlist" : "Shortlist", () => State.ToggleFreeAgentShortlist(row.PersonId), agent is not null);
            yield return CreateDetailButton("Offer Contract", () => State.OfferFreeAgentContractFor(row.PersonId), State.CanOfferFreeAgent(row.PersonId));
            yield return CreateDetailButton("Improve Offer", () => MessageBox.Show(State.AgentOfferComment(row.PersonId), "Improve Offer", MessageBoxButton.OK, MessageBoxImage.Information), agent is not null);
            yield return CreateDetailButton("Compare", () => MessageBox.Show(State.FreeAgentCompetitionSummary(row.PersonId), "Compare Offers", MessageBoxButton.OK, MessageBoxImage.Information), agent is not null);
            yield return CreateDetailButton("View Agent", () => MessageBox.Show(State.AgentDetails(row.PersonId), "Agent Card", MessageBoxButton.OK, MessageBoxImage.Information), agent is not null);
            yield return CreateDetailButton("Invite to Camp", () => State.InviteFreeAgentToCampFor(row.PersonId), agent is not null && agent.Status is not FreeAgentStatus.Signed and not FreeAgentStatus.Unavailable);
            yield return CreateDetailButton("Withdraw Offer", () => State.WithdrawFreeAgentOfferFor(row.PersonId), agent is not null && (agent.Status is FreeAgentStatus.Offered or FreeAgentStatus.Negotiating));
            yield break;
        }

        if (tab is "Scouting" or "Draft Board")
        {
            yield return CreateDetailButton("Board Up", () => State.MoveDraftBoardPlayer(row.PersonId, -1), State.IsDraftUiEnabled);
            yield return CreateDetailButton("Board Down", () => State.MoveDraftBoardPlayer(row.PersonId, 1), State.IsDraftUiEnabled);
            yield return CreateDetailButton("Star", () => State.ToggleStarProspect(row.PersonId), State.IsDraftUiEnabled);
            yield return CreateDetailButton("GM Note", () => State.AddDraftNoteFor(row.PersonId), State.IsDraftUiEnabled);
            yield return CreateDetailButton("Pin", () => State.ToggleDraftWarRoomTag(row.PersonId, DraftWatchTag.Pinned), State.IsDraftUiEnabled);
            yield return CreateDetailButton("Favorite", () => State.ToggleDraftWarRoomTag(row.PersonId, DraftWatchTag.Favorite), State.IsDraftUiEnabled);
            yield return CreateDetailButton("Priority", () => State.ToggleDraftWarRoomTag(row.PersonId, DraftWatchTag.Priority), State.IsDraftUiEnabled);
            yield return CreateDetailButton("Sleeper", () => State.ToggleDraftWarRoomTag(row.PersonId, DraftWatchTag.Sleeper), State.IsDraftUiEnabled);
            yield return CreateDetailButton("Avoid", () => State.ToggleDraftWarRoomTag(row.PersonId, DraftWatchTag.Avoid), State.IsDraftUiEnabled);
            yield return CreateDetailButton("Remove Board", () => State.RemoveFromDraftWarRoom(row.PersonId), State.IsDraftUiEnabled);
            yield return CreateDetailButton("Consensus", () => MessageBox.Show(State.DraftConsensusText(row.PersonId), "Scout Consensus", MessageBoxButton.OK, MessageBoxImage.Information), State.IsDraftUiEnabled);
            yield return CreateDetailButton("Compare", () => MessageBox.Show(State.CompareWithNearbyProspectsText(row.PersonId), "Prospect Compare", MessageBoxButton.OK, MessageBoxImage.Information), State.IsDraftUiEnabled);
            yield return CreateDetailButton("Assign Scout", () => ShowScoutAssignmentDialog(row.PersonId), State.AvailableScoutProfiles.Count > 0);
            yield return CreateDetailButton("Scout Again", () => State.ScoutAgainFor(row.PersonId), State.AvailableScoutProfiles.Count > 0);
            yield return CreateDetailButton("Tournament", () => State.TournamentScoutFor(row.PersonId), State.AvailableScoutProfiles.Count > 0);
            yield return CreateDetailButton("Compare Reports", () => MessageBox.Show(State.ScoutingComparisonText(row.PersonId), "Compare Reports", MessageBoxButton.OK, MessageBoxImage.Information));
        }

        if (tab is "Roster" or "Prospect List" or "Training Camp")
        {
            yield return CreateDetailButton("Balanced Plan", () => State.SetDevelopmentPlanFor(row.PersonId, DevelopmentPlanFocus.Balanced));
            yield return CreateDetailButton("Skating Plan", () => State.SetDevelopmentPlanFor(row.PersonId, DevelopmentPlanFocus.Skating));
            yield return CreateDetailButton("Shooting Plan", () => State.SetDevelopmentPlanFor(row.PersonId, DevelopmentPlanFocus.Shooting));
            yield return CreateDetailButton("Defensive Plan", () => State.SetDevelopmentPlanFor(row.PersonId, DevelopmentPlanFocus.Defensive));
            yield return CreateDetailButton("Confidence Plan", () => State.SetDevelopmentPlanFor(row.PersonId, DevelopmentPlanFocus.Confidence));
            yield return CreateDetailButton("Attribute Report", () => State.GenerateAttributeReportFor(row.PersonId));
            yield return CreateDetailButton("Increase Ice Time", () => State.SetDevelopmentRoleFor(row.PersonId, DevelopmentIceTimeRole.TopSix));
            yield return CreateDetailButton("Yearly Review", () => MessageBox.Show(State.DevelopmentReviewText(row.PersonId), "Development Review", MessageBoxButton.OK, MessageBoxImage.Information));
            yield return CreateDetailButton("Medical Report", () => MessageBox.Show(State.MedicalReportText(row.PersonId), "Medical Report", MessageBoxButton.OK, MessageBoxImage.Information));
            yield return CreateDetailButton("Return Now", () => State.ApplyMedicalDecisionFor(row.PersonId, ReturnToPlayOption.ReturnImmediately), State.HasActiveInjury(row.PersonId));
            yield return CreateDetailButton("Limited Minutes", () => State.ApplyMedicalDecisionFor(row.PersonId, ReturnToPlayOption.LimitedMinutes), State.HasActiveInjury(row.PersonId));
            yield return CreateDetailButton("More Recovery", () => State.ApplyMedicalDecisionFor(row.PersonId, ReturnToPlayOption.AdditionalRecovery), State.HasActiveInjury(row.PersonId));
            yield return CreateDetailButton("Conditioning", () => State.ApplyMedicalDecisionFor(row.PersonId, ReturnToPlayOption.ConditioningAssignment), State.HasActiveInjury(row.PersonId));
            yield return CreateDetailButton("Medical Clearance", () => State.ApplyMedicalDecisionFor(row.PersonId, ReturnToPlayOption.MedicalClearance), State.HasActiveInjury(row.PersonId));
            yield return CreateDetailButton("Assign Affiliate", () => State.AssignPlayerToAffiliateFor(row.PersonId), State.CanAssignPlayerToAffiliate(row.PersonId));
            yield return CreateDetailButton("Place On Waivers", () => ConfirmDestructiveAction(
                "Place Player On Waivers",
                $"{row.Name} will be exposed to waiver claims before assignment. Continue?",
                () => State.PlacePlayerOnWaiversFor(row.PersonId)), State.CanPlacePlayerOnWaivers(row.PersonId));
            yield return CreateDetailButton("Recall", () => State.RecallPlayerFromAffiliateFor(row.PersonId), State.CanRecallPlayerFromAffiliate(row.PersonId));
        }

        yield return CreateDetailButton("Qualify", () => State.QualifyRightsFor(row.PersonId), State.CanQualifyRights(row.PersonId));
        yield return CreateDetailButton("Do Not Qualify", () => State.DeclineRightsFor(row.PersonId), State.CanDeclineRights(row.PersonId));
        yield return CreateDetailButton("Negotiate Contract", () => SelectWorkspaceScreen("Hockey Operations", "Contracts"), State.HasContractRightsDecision(row.PersonId));
        yield return CreateDetailButton("File Arbitration", () => State.FileArbitrationFor(row.PersonId), State.CanFileArbitration(row.PersonId));
        yield return CreateDetailButton("Settle Arbitration", () => State.SettleArbitrationFor(row.PersonId), State.CanSettleArbitration(row.PersonId));
        yield return CreateDetailButton("Accept Award", () => State.AcceptArbitrationAwardFor(row.PersonId), State.CanAcceptArbitrationAward(row.PersonId));
        yield return CreateDetailButton("Walk Away", () => ConfirmDestructiveAction(
            "Walk Away From Arbitration",
            $"{row.Name} may become available to other teams if you walk away. Continue?",
            () => State.WalkAwayArbitrationFor(row.PersonId)), State.CanWalkAwayArbitration(row.PersonId));
        yield return CreateDetailButton("Calculate Buyout", () => State.CalculateBuyoutFor(row.PersonId), State.CanCalculateBuyout(row.PersonId));
        yield return CreateDetailButton("Confirm Buyout", () => ConfirmDestructiveAction(
            "Confirm Contract Buyout",
            $"{row.Name}'s contract will be bought out and future cap/budget penalties may remain. Continue?",
            () => State.ConfirmBuyoutFor(row.PersonId)), State.CanConfirmBuyout(row.PersonId));
        yield return CreateDetailButton("Cancel Buyout", () => State.CancelBuyoutFor(row.PersonId), State.CanCancelBuyout(row.PersonId));
        yield return CreateDetailButton("Submit Offer Sheet", () => State.SubmitOfferSheetFor(row.PersonId), State.CanSubmitOfferSheet(row.PersonId));
        yield return CreateDetailButton("Match Offer", () => State.MatchOfferSheetFor(row.PersonId), State.CanMatchOfferSheet(row.PersonId));
        yield return CreateDetailButton("Take Compensation", () => State.DeclineOfferSheetFor(row.PersonId), State.CanDeclineOfferSheet(row.PersonId));

        var available = State.AvailableProspectActions(row.PersonId);
        yield return CreateDetailButton("Offer Contract", () => State.OfferProspectContractFor(row.PersonId), available.Contains(ProspectDecisionType.OfferContract));
        yield return CreateDetailButton("Invite Prospect", () => State.InviteProspectToCampFor(row.PersonId), available.Contains(ProspectDecisionType.InviteToCamp));
        yield return CreateDetailButton("Return Prospect", () => State.ReturnProspectToJuniorOrYouthFor(row.PersonId), available.Contains(ProspectDecisionType.ReturnToJunior) || available.Contains(ProspectDecisionType.ReturnToYouthTeam));
        yield return CreateDetailButton("Assign Prospect", () => State.AssignProspectToAffiliateFor(row.PersonId), available.Contains(ProspectDecisionType.AssignToAffiliate));
        yield return CreateDetailButton("Release Rights", () => ConfirmDestructiveAction(
            "Release Prospect Rights",
            $"{row.Name}'s rights will be released if the league rules allow it. Continue?",
            () => State.ReleaseProspectRightsFor(row.PersonId)), available.Contains(ProspectDecisionType.ReleaseRights));

        if (tab == "Training Camp")
        {
            yield return CreateDetailButton("Keep", () => State.ApplyCampDecisionFor(row.PersonId, TrainingCampDecisionType.Keep), State.CanApplyCampDecision(row.PersonId));
            yield return CreateDetailButton("Cut", () => State.ApplyCampDecisionFor(row.PersonId, TrainingCampDecisionType.Cut), State.CanApplyCampDecision(row.PersonId));
            yield return CreateDetailButton("Release", () => ConfirmDestructiveAction(
                "Release Camp Player",
                $"{row.Name} will be released from camp. Continue?",
                () => State.ApplyCampDecisionFor(row.PersonId, TrainingCampDecisionType.Release)), State.CanApplyCampDecision(row.PersonId));
            yield return CreateDetailButton("Return Junior", () => State.ApplyCampDecisionFor(row.PersonId, TrainingCampDecisionType.ReturnToJuniorTeam), State.CanApplyCampDecision(row.PersonId));
            yield return CreateDetailButton("Assign/Return", () => State.AssignOrReturnTrainingCampPlayerFor(row.PersonId), State.CanApplyCampDecision(row.PersonId));
            yield return CreateDetailButton("Waivers", () => ConfirmDestructiveAction(
                "Place Camp Player On Waivers",
                $"{row.Name} will be placed on waivers before assignment. Continue?",
                () => State.ApplyCampDecisionFor(row.PersonId, TrainingCampDecisionType.PlaceOnWaivers)), State.CanApplyCampDecision(row.PersonId));
            yield return CreateDetailButton("Mark Injured", () => State.ApplyCampDecisionFor(row.PersonId, TrainingCampDecisionType.MarkInjured), State.CanApplyCampDecision(row.PersonId));
        }
    }

    private StackPanel EmptyDetail(string title, string message)
    {
        var panel = new StackPanel();
        panel.Children.Add(UiPresentation.UiEmptyState(title, message));
        return panel;
    }

    private StackPanel CreateDetailPanel(string title, string subtitle)
    {
        var panel = new StackPanel();
        var header = new StackPanel();
        header.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = UiTypography.CardTitle,
            FontWeight = FontWeights.SemiBold,
            Foreground = UiTheme.Text,
            TextWrapping = TextWrapping.Wrap
        });
        header.Children.Add(new TextBlock
        {
            Text = subtitle,
            FontSize = UiTypography.Body,
            Foreground = UiTheme.MutedText,
            Margin = new Thickness(0, 4, 0, 8),
            TextWrapping = TextWrapping.Wrap
        });
        header.Children.Add(UiPresentation.BadgeRow(("Clickable Profile", "info"), ("Universal Person Card shell", "neutral")));
        panel.Children.Add(UiPresentation.UiPersonCard(header));
        return panel;
    }

    private static void AddSubHeader(StackPanel panel, string text)
    {
        panel.Children.Add(UiPresentation.UiSectionHeader(text));
    }

    private static void AddLine(StackPanel panel, string label, object? value)
    {
        panel.Children.Add(UiPresentation.UiInfoRow(label, value));
    }

    private static void AddParagraph(StackPanel panel, string text)
    {
        panel.Children.Add(new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = UiTheme.MutedText,
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

    private Button CreateDetailButton(string text, Action action, bool enabled = true, string? disabledTooltip = null)
    {
        var button = CreateButton(text, action);
        button.MinWidth = 118;
        button.IsEnabled = enabled;
        if (!enabled)
        {
            button.ToolTip = disabledTooltip ?? "Coming soon";
        }

        return button;
    }

    private static bool IsLikelyPersonRow(SelectablePersonRow row) =>
        !row.PersonId.Contains(':', StringComparison.Ordinal)
        && !row.PersonId.EndsWith("-summary", StringComparison.OrdinalIgnoreCase)
        && row.Kind is not ("RosterSummary" or "LineupSummary" or "GameUsage" or "LineChemistry" or "LineupSlot" or "TacticsSummary" or "StaffSection");

    private static string StatusSemantic(string text)
    {
        if (text.Contains("injured", StringComparison.OrdinalIgnoreCase))
        {
            return "injured";
        }

        if (text.Contains("recover", StringComparison.OrdinalIgnoreCase) || text.Contains("risk", StringComparison.OrdinalIgnoreCase))
        {
            return "attention";
        }

        if (text.Contains("healthy", StringComparison.OrdinalIgnoreCase))
        {
            return "healthy";
        }

        return "neutral";
    }

    private static string ConfidenceSemantic(string text)
    {
        if (text.Contains("Black", StringComparison.OrdinalIgnoreCase) || text.Contains("VeryHigh", StringComparison.OrdinalIgnoreCase))
        {
            return "black";
        }

        if (text.Contains("Blue", StringComparison.OrdinalIgnoreCase) || text.Contains("High", StringComparison.OrdinalIgnoreCase))
        {
            return "info";
        }

        if (text.Contains("Green", StringComparison.OrdinalIgnoreCase) || text.Contains("Medium", StringComparison.OrdinalIgnoreCase))
        {
            return "positive";
        }

        if (text.Contains("Red", StringComparison.OrdinalIgnoreCase) || text.Contains("Low", StringComparison.OrdinalIgnoreCase))
        {
            return "critical";
        }

        return "neutral";
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
        prospectList.MouseDoubleClick += (_, _) =>
        {
            var prospectId = (prospectList.SelectedItem as ListBoxItem)?.Tag as string;
            if (!string.IsNullOrWhiteSpace(prospectId))
            {
                OpenUniversalPersonCard(prospectId);
            }
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
        var skipButton = CreateButton("Skip", () =>
        {
            if (state.IsPlayerTurn)
            {
                State.SkipDraftPick();
            }
        });
        skipButton.IsEnabled = state.IsPlayerTurn && state.Status == DraftExperienceStatus.AwaitingPlayerPick && prospectList.Items.Count > 0;
        actionBar.Children.Add(skipButton);
        actionBar.Children.Add(CreateButton("View Dossier", () =>
        {
            var prospectId = (prospectList.SelectedItem as ListBoxItem)?.Tag as string;
            if (!string.IsNullOrWhiteSpace(prospectId))
            {
                OpenDossierFor(prospectId);
            }
        }));
        actionBar.Children.Add(CreateButton("Person Card", () =>
        {
            var prospectId = (prospectList.SelectedItem as ListBoxItem)?.Tag as string;
            if (!string.IsNullOrWhiteSpace(prospectId))
            {
                OpenUniversalPersonCard(prospectId);
            }
        }));
        actionBar.Children.Add(CreateButton("Compare", () =>
        {
            var prospectId = (prospectList.SelectedItem as ListBoxItem)?.Tag as string;
            if (!string.IsNullOrWhiteSpace(prospectId))
            {
                MessageBox.Show(State.CompareWithNearbyProspectsText(prospectId), "Prospect Compare", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }));
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
        builder.AppendLine($"Class theme: {State.DraftClassThemeText}");
        builder.AppendLine($"Position depth: {State.DraftClassPositionDepthText}");
        builder.AppendLine($"Best available by position: {State.DraftClassRemainingBestByPositionText}");
        builder.AppendLine();
        builder.AppendLine("Board Toggle Snapshot");
        builder.AppendLine("  My Board | Scout Board | Consensus Board");
        builder.AppendLine(State.DraftBoardViewText(DraftWarRoomViewType.MyBoard).Split(Environment.NewLine).Skip(2).FirstOrDefault() ?? "  My Board pending.");
        builder.AppendLine(State.DraftBoardViewText(DraftWarRoomViewType.ScoutBoard).Split(Environment.NewLine).Skip(2).FirstOrDefault() ?? "  Scout Board pending.");
        builder.AppendLine(State.DraftBoardViewText(DraftWarRoomViewType.ConsensusBoard).Split(Environment.NewLine).Skip(2).FirstOrDefault() ?? "  Consensus Board pending.");
        builder.AppendLine();
        builder.AppendLine("Team Needs");
        foreach (var need in State.DraftWarRoom.Needs.Take(4))
        {
            builder.AppendLine($"  {need.Priority}: {need.Label}");
        }
        builder.AppendLine();

        builder.AppendLine("Recent Picks");
        foreach (var selection in draft.Selections.OrderByDescending(item => item.PickNumber).Take(8).OrderBy(item => item.PickNumber))
        {
            builder.AppendLine($"  #{selection.PickNumber} {selection.OrganizationName}: {selection.ProspectName}");
        }

        builder.AppendLine();
        builder.AppendLine("Upcoming Picks");
        if (draft.Draft?.Picks is not null)
        {
            foreach (var pick in draft.Draft.Picks.Where(item => item.Selection is null).OrderBy(item => item.PickNumber).Take(8))
            {
                var name = draft.OrganizationNames.GetValueOrDefault(pick.OwningOrganizationId, pick.OwningOrganizationId);
                builder.AppendLine($"  #{pick.PickNumber} {name}");
            }
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
        var quickBio = State.DraftQuickScan(entry);
        var position = entry.Bio?.Position;
        var currentTeam = entry.Bio is null ? State.RegionTeamText(entry.ProspectPersonId) : entry.Bio.CurrentTeam;
        return $"{State.DraftIntelligenceRowText(entry)} | Bio: {quickBio} | Public position: {position?.ToString() ?? "Unknown"} | Team: {currentTeam} | Confidence: {entry.ScoutingConfidence?.ToString() ?? "Unknown"}";
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
        builder.AppendLine($"Position: {State.DraftPositionText(entry)}");
        builder.AppendLine($"Age: {State.PersonAge(entry.ProspectPersonId)?.ToString() ?? "unknown"}");
        builder.AppendLine($"Ratings: {State.RatingContextText(entry.ProspectPersonId)}");
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
        var intelligence = State.DraftIntelligenceCard(entry.ProspectPersonId);
        var consensus = State.DraftConsensus(entry.ProspectPersonId);
        var warRoomEntry = State.DraftWarRoom.BoardEntries.FirstOrDefault(item => item.ProspectPersonId == entry.ProspectPersonId);
        builder.AppendLine($"War room rank: #{warRoomEntry?.PersonalRank.ToString() ?? entry.Rank.ToString()} | Tags: {(warRoomEntry is null || warRoomEntry.Tags.Count == 0 ? "Watching" : string.Join(", ", warRoomEntry.Tags))}");
        builder.AppendLine($"Scout consensus: {consensus.Level} ({consensus.AgreementScore}/100) | scout board #{intelligence.ScoutBoardRank} | consensus board #{intelligence.ConsensusBoardRank}");
        builder.AppendLine($"Visible ratings: {intelligence.RatingDisplay}");
        builder.AppendLine($"Team fit: {intelligence.TeamFitScore}/100");
        builder.AppendLine($"Draft value context: {State.DraftValueContext(entry)}");
        builder.AppendLine($"Development curve: {intelligence.DevelopmentCurve}; pace {intelligence.DevelopmentPace}; ETA {intelligence.Eta}");
        builder.AppendLine($"Current picture: {State.DraftCurrentPicture(entry)}");
        builder.AppendLine($"Future picture: {State.DraftFuturePicture(entry)}");
        builder.AppendLine($"Projection: {entry.ProjectionText}");
        builder.AppendLine($"Player type: {State.PlayerType(entry.ProspectPersonId)}");
        builder.AppendLine($"Class context: {State.DraftClassContext(entry)}");
        builder.AppendLine($"Risk summary: {DraftRiskSummary(entry)}");
        builder.AppendLine($"GM notes: {(string.IsNullOrWhiteSpace(entry.PersonalNotes) ? "none" : entry.PersonalNotes)}");
        builder.AppendLine("Key attribute intelligence:");
        foreach (var line in intelligence.Attributes.OrderBy(attribute => attribute.Estimate.IsUnknown).ThenByDescending(attribute => attribute.Estimate.Midpoint).Take(10))
        {
            builder.AppendLine($"  {line.DisplayText}");
        }

        builder.AppendLine("Draft intelligence alerts:");
        if (intelligence.Alerts.Count == 0)
        {
            builder.AppendLine("  No major alert.");
        }

        foreach (var alert in intelligence.Alerts)
        {
            builder.AppendLine($"  {alert.AlertType}: {alert.Summary}");
        }

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
        !string.IsNullOrWhiteSpace(entry.RiskSummary)
            ? entry.RiskSummary
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
        builder.AppendLine($"Playoffs: {State.PlayoffDashboardSummary}");
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

    private string BuildLineup()
    {
        var lineup = State.CurrentLineup;
        var chemistry = State.LineChemistryReport;
        var builder = new StringBuilder();
        builder.AppendLine("Lineup");
        builder.AppendLine("======");
        builder.AppendLine($"{lineup.OrganizationName} | created {lineup.CreatedOn:yyyy-MM-dd}");
        builder.AppendLine(lineup.Summary);
        builder.AppendLine();
        builder.AppendLine("Team Chemistry");
        builder.AppendLine($"Overall: {chemistry.Overall.Score.Grade} ({chemistry.Overall.Score.Value})");
        builder.AppendLine($"Best unit: {chemistry.BestLine}");
        builder.AppendLine($"Worst unit: {chemistry.WorstLine}");
        builder.AppendLine($"Biggest concern: {chemistry.MajorConcerns.FirstOrDefault() ?? "No major chemistry concern."}");
        builder.AppendLine();
        builder.AppendLine("Forward Lines");
        builder.AppendLine("Line slots: Line 1, Line 2, Line 3, Line 4");
        foreach (var line in lineup.ForwardLines.OrderBy(line => line.LineNumber))
        {
            var lineChemistry = chemistry.ForwardLines.FirstOrDefault(unit => unit.UnitId == $"forward-line:{line.LineNumber}");
            builder.AppendLine($"Line {line.LineNumber}: Chemistry {lineChemistry?.Score.Grade.ToString() ?? "Not evaluated"}");
            builder.AppendLine($"  LW {LineupPlayerText(line.LeftWing)}");
            builder.AppendLine($"  C  {LineupPlayerText(line.Center)}");
            builder.AppendLine($"  RW {LineupPlayerText(line.RightWing)}");
        }

        builder.AppendLine();
        builder.AppendLine("Defense Pairs");
        builder.AppendLine("Pair slots: Pair 1, Pair 2, Pair 3");
        foreach (var pair in lineup.DefensePairs.OrderBy(pair => pair.PairNumber))
        {
            var pairChemistry = chemistry.DefensePairs.FirstOrDefault(unit => unit.UnitId == $"defense-pair:{pair.PairNumber}");
            builder.AppendLine($"Pair {pair.PairNumber}: Chemistry {pairChemistry?.Score.Grade.ToString() ?? "Not evaluated"}");
            builder.AppendLine($"  LD {LineupPlayerText(pair.LeftDefense)}");
            builder.AppendLine($"  RD {LineupPlayerText(pair.RightDefense)}");
        }

        builder.AppendLine();
        builder.AppendLine("Goalies");
        builder.AppendLine($"Goalie room chemistry: {chemistry.GoalieDepth.Score.Grade}");
        builder.AppendLine($"Starter: {LineupPlayerText(lineup.Goalies.Starter)}");
        builder.AppendLine($"Backup:  {LineupPlayerText(lineup.Goalies.Backup)}");
        builder.AppendLine();
        builder.AppendLine("Game Usage");
        var gameUsage = State.CurrentGameUsage;
        foreach (var unit in gameUsage.SpecialTeams.PowerPlayUnits)
        {
            builder.AppendLine($"Power Play Unit {unit.UnitNumber}: LW {LineupPlayerText(unit.LeftWing)} | C {LineupPlayerText(unit.Center)} | RW {LineupPlayerText(unit.RightWing)} | QB {LineupPlayerText(unit.QuarterbackDefense)} | NF/2D {LineupPlayerText(unit.NetFrontOrSecondDefense)}");
        }

        foreach (var unit in gameUsage.SpecialTeams.PenaltyKillUnits)
        {
            builder.AppendLine($"Penalty Kill Unit {unit.UnitNumber}: LW {LineupPlayerText(unit.LeftWing)} | RW {LineupPlayerText(unit.RightWing)} | LD {LineupPlayerText(unit.LeftDefense)} | RD {LineupPlayerText(unit.RightDefense)}");
        }

        builder.AppendLine($"Extra Attacker: {string.Join(" | ", gameUsage.SpecialTeams.ExtraAttacker.Players.Select(player => player.PlayerName))}");
        builder.AppendLine($"Three-on-Three: {gameUsage.SpecialTeams.ThreeOnThree.Combination} - {string.Join(" | ", gameUsage.SpecialTeams.ThreeOnThree.Players.Select(player => player.PlayerName))}");
        builder.AppendLine($"Shootout: {string.Join(" | ", gameUsage.SpecialTeams.ShootoutOrder.Shooters.Select((player, index) => $"{index + 1}. {player.PlayerName}"))}");
        foreach (var goalie in gameUsage.GoalieUsage)
        {
            builder.AppendLine($"{goalie.UsageRole} goalie usage: {goalie.PlayerName}, starts {goalie.GamesStarted}/{goalie.ExpectedStarts}, {goalie.Workload}. {goalie.RestRecommendation}");
        }

        builder.AppendLine();
        builder.AppendLine("Tactics");
        var tactics = State.CurrentTactics;
        builder.AppendLine($"Style: {TacticsService.Display(tactics.Style)} | System: {TacticsService.Display(tactics.System)} | Fit: {tactics.FitReport.Grade} ({tactics.FitReport.Score}/100)");
        builder.AppendLine($"Forecheck: {TacticsService.Display(tactics.Settings.Forecheck)} | Neutral zone: {TacticsService.Display(tactics.Settings.NeutralZone)} | Defensive zone: {TacticsService.Display(tactics.Settings.DefensiveZone)}");
        builder.AppendLine($"Breakout: {TacticsService.Display(tactics.Settings.Breakout)} | Shots: {TacticsService.Display(tactics.Settings.ShotPreference)} | Physicality: {tactics.Settings.Physicality} | Risk: {tactics.Settings.RiskLevel}");
        builder.AppendLine($"PP tactic: {TacticsService.Display(tactics.Settings.PowerPlayStyle)} | PK tactic: {TacticsService.Display(tactics.Settings.PenaltyKillStyle)}");
        builder.AppendLine($"Coach: {tactics.CoachName} ({tactics.CoachPhilosophy}) - {tactics.FitReport.CoachRecommendation}");
        builder.AppendLine();
        builder.AppendLine("Coach Recommendations");
        if (lineup.CoachRecommendations.Count == 0)
        {
            builder.AppendLine("No major lineup concerns today.");
        }
        else
        {
            foreach (var recommendation in lineup.CoachRecommendations)
            {
                builder.AppendLine($"- {(recommendation.IsImportant ? "Important" : "Note")}: {recommendation.PlayerName} - {recommendation.Reason}");
                builder.AppendLine($"  Recommended action: {recommendation.SuggestedAction}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Game Usage Recommendations");
        foreach (var recommendation in gameUsage.CoachRecommendations)
        {
            builder.AppendLine($"- {(recommendation.IsImportant ? "Important" : "Note")}: {recommendation.PlayerName} - {recommendation.SuggestedAction}");
        }

        builder.AppendLine();
        builder.AppendLine("Development Context");
        foreach (var assignment in lineup.Assignments
            .Where(assignment => assignment.Slot != LineupSlot.HealthyScratch)
            .Take(8))
        {
            builder.AppendLine($"- {assignment.PlayerName}: {State.LineupDevelopmentImpactText(assignment.PersonId)}");
        }

        return builder.ToString();
    }

    private static string LineupPlayerText(LineupRoleAssignment? assignment) =>
        assignment is null
            ? "unassigned"
            : $"{assignment.PlayerName} | {assignment.Position} | {LineupDisplay.Role(assignment.CurrentRole)} | potential {LineupDisplay.Role(assignment.PotentialRole)}";

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
        row.PreviewMouseLeftButtonDown += (_, args) =>
        {
            if (FindAncestor<Button>(args.OriginalSource as DependencyObject) is not null)
            {
                return;
            }

            _selectedInboxItemId = message.InboxItemId;
            RefreshInboxPanels();
            args.Handled = true;
        };
        row.MouseLeftButtonUp += (_, args) =>
        {
            if (FindAncestor<Button>(args.OriginalSource as DependencyObject) is not null)
            {
                return;
            }

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
            Focusable = false,
            Padding = new Thickness(7, 4, 7, 4),
            Margin = new Thickness(4, 0, 0, 0),
            FontSize = 11,
            MinWidth = 42
        };
        button.Click += (_, _) =>
        {
            action();
            RefreshAfterAction();
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
        var office = State.OwnerOffice;
        var builder = new StringBuilder();
        builder.AppendLine("Owner");
        builder.AppendLine("=====");
        builder.AppendLine($"{owner.Name} - {owner.Archetype}");
        builder.AppendLine($"Organization: {State.ScenarioSnapshot.Organization.Name}");
        builder.AppendLine($"Personality: {office.Personality.PersonalityType}");
        builder.AppendLine($"Vision: {office.Personality.Vision}");
        builder.AppendLine($"Budget philosophy: {office.Personality.BudgetPhilosophy}");
        builder.AppendLine($"Relationship style: {office.Personality.RelationshipStyle}");
        builder.AppendLine($"Winning expectation: {office.Personality.WinningExpectation}");
        builder.AppendLine($"Prospect expectation: {office.Personality.ProspectExpectation}");
        builder.AppendLine($"Autonomy: {owner.AutonomyLevel}");
        builder.AppendLine($"Trust: {office.Confidence.Trust}  Confidence: {office.Confidence.Confidence}  Patience: {office.Confidence.Patience}");
        builder.AppendLine($"Pressure: {office.Confidence.Pressure}  Support: {office.Confidence.Support}");
        builder.AppendLine($"Job security: {office.JobSecurity.Level} ({office.JobSecurity.Score}/100)");
        builder.AppendLine(office.JobSecurity.Explanation);
        var ownerRelationship = State.RelationshipProfiles.FirstOrDefault(profile => profile.RelationshipType == ExpandedRelationshipType.GmOwner);
        if (ownerRelationship is not null)
        {
            builder.AppendLine($"Relationship: {ownerRelationship.Label} | Trust {ownerRelationship.Trust}, Respect {ownerRelationship.Respect}, Loyalty {ownerRelationship.Loyalty}, Communication {ownerRelationship.CommunicationQuality}, Trend {ownerRelationship.Trend}");
            builder.AppendLine($"Relationship note: {ownerRelationship.Summary}");
        }

        if (State.ScenarioSnapshot.OwnerCareerSummary is { } lifeCycle)
        {
            builder.AppendLine();
            builder.AppendLine("Owner Life Cycle");
            builder.AppendLine($"Life stage: {lifeCycle.LifeStage}");
            builder.AppendLine($"Confidence trend: {lifeCycle.ConfidenceTrend}");
            builder.AppendLine($"Current personality: {lifeCycle.CurrentPersonality}");
            builder.AppendLine($"Career summary: {lifeCycle.CareerSummaryText}");
            builder.AppendLine($"Personality evolution: {lifeCycle.PersonalityEvolution}");
            builder.AppendLine($"Budget relationship: {lifeCycle.BudgetRelationship}");
            builder.AppendLine($"Owner legacy: {lifeCycle.LegacyProfile.LegacySummary}");
            builder.AppendLine($"Organization era: {lifeCycle.OrganizationHistorySummary}");
        }

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
        builder.AppendLine("Season Expectations");
        foreach (var expectation in office.Expectations)
        {
            builder.AppendLine($"Priority {expectation.Priority} | Difficulty {expectation.Difficulty} | {expectation.ExpectationType}");
            builder.AppendLine($"  Progress {expectation.CurrentProgress}% by {expectation.Deadline:yyyy-MM-dd}: {expectation.Description}");
        }

        builder.AppendLine();
        builder.AppendLine("Owner Confidence Drivers");
        foreach (var driver in office.Confidence.Drivers)
        {
            builder.AppendLine($"- {driver}");
        }

        builder.AppendLine();
        builder.AppendLine("Owner Decisions / Mandates");
        foreach (var decision in office.Decisions)
        {
            builder.AppendLine($"- {decision.DecisionType}: {decision.Reason} Impact: {decision.Impact}");
        }

        builder.AppendLine();
        builder.AppendLine("Letters");
        foreach (var letter in State.ScenarioSnapshot.OwnerLetters.Count == 0 ? office.Letters : State.ScenarioSnapshot.OwnerLetters)
        {
            builder.AppendLine($"{letter.Date:yyyy-MM-dd} - {letter.Subject}");
            builder.AppendLine(letter.Body);
        }

        builder.AppendLine();
        builder.AppendLine("Meeting History / Schedule");
        if (State.ScenarioSnapshot.OwnerMeetingHistory.Count > 0)
        {
            foreach (var meeting in State.ScenarioSnapshot.OwnerMeetingHistory.OrderBy(item => item.Date))
            {
                builder.AppendLine($"{meeting.Date:yyyy-MM-dd} - {meeting.MeetingType}");
                builder.AppendLine($"  Topic: {meeting.Topic}");
                builder.AppendLine($"  Outcome: {meeting.Outcome}");
                builder.AppendLine($"  Confidence impact: {meeting.ConfidenceImpact}");
            }
        }
        else
        {
            foreach (var meeting in office.Meetings)
            {
                builder.AppendLine($"{meeting.ScheduledDate:yyyy-MM-dd} - {meeting.MeetingType}");
                builder.AppendLine($"  {meeting.Summary}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Performance Review");
        builder.AppendLine($"Overall GM grade: {office.PerformanceReview.OverallGrade}");
        builder.AppendLine(office.PerformanceReview.Narrative);
        builder.AppendLine($"Recommendation: {office.PerformanceReview.Recommendation}");
        foreach (var grade in office.PerformanceReview.CategoryGrades)
        {
            builder.AppendLine($"- {grade.Key}: {grade.Value}");
        }

        builder.AppendLine();
        builder.AppendLine("Legacy Goals");
        foreach (var goal in owner.Goals.OrderByDescending(goal => goal.Priority))
        {
            builder.AppendLine($"Priority {goal.Priority}: {goal.GoalType} - {goal.Description}");
        }

        return builder.ToString();
    }

    private string BuildBudgetWorkspace()
    {
        var budget = State.BudgetOverview;
        var cap = State.SalaryCap;
        var builder = new StringBuilder();
        builder.AppendLine("Budget");
        builder.AppendLine("======");
        builder.AppendLine("Hockey Operations Staff Budget");
        builder.AppendLine("This budget covers the GM, coaches, scouts, medical/training staff, front office, and released-staff obligations. Player payroll is tracked separately under Salary Cap / Player Payroll.");
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
        builder.AppendLine($"Player payroll/cap commitments (separate): {budget.PlayerContractsTotal:C0}");
        builder.AppendLine($"Scouting budget: {budget.ScoutingBudget:C0}");
        builder.AppendLine($"Medical/staff operations: {budget.MedicalAndStaffOperationsBudget:C0}");
        builder.AppendLine();
        builder.AppendLine("Salary Cap / Player Payroll");
        builder.AppendLine("Owner reaction to player spending depends on cap rules, playoff expectations, and team results rather than the staff operating budget.");
        builder.AppendLine($"Cap enabled: {(cap.IsEnabled ? "Yes" : "No - this league uses operating budgets")}");
        builder.AppendLine($"Cap status: {cap.Status}");
        builder.AppendLine($"Cap amount: {cap.Profile.CapAmount:C0}");
        builder.AppendLine($"Cap used: {cap.CapUsed:C0}");
        builder.AppendLine($"Cap remaining: {cap.CapRemaining:C0}");
        builder.AppendLine($"Cap percentage: {cap.CapPercentage:0.##}%");
        builder.AppendLine($"Floor: {cap.Profile.SalaryFloor:C0}");
        builder.AppendLine($"Contract count: {cap.ContractCount}/{(cap.Profile.MaximumContracts == int.MaxValue ? "unlimited" : cap.Profile.MaximumContracts.ToString())}");
        builder.AppendLine($"Expiring contracts: {cap.ExpiringSalary:C0}");
        builder.AppendLine($"Future commitments: {cap.CommittedFutureSalary:C0}");
        builder.AppendLine($"Buyout/dead cap penalties: {cap.DeadCapPlaceholder:C0}");
        foreach (var warning in cap.Warnings)
        {
            builder.AppendLine($"Warning: {warning}");
        }

        builder.AppendLine();
        builder.AppendLine("Current Contracts");
        if (cap.ContractCommitments.Count == 0)
        {
            builder.AppendLine("No cap-counted player contracts yet.");
        }
        else
        {
            foreach (var contract in cap.ContractCommitments.OrderByDescending(item => item.CapHit).ThenBy(item => item.PersonName))
            {
                builder.AppendLine($"{contract.PersonName}: {contract.CapHit:C0} cap hit | {contract.YearsRemaining} year(s) | expires {contract.ExpiresOn:yyyy-MM-dd}");
            }
        }

        if (cap.BuyoutPenalties.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Buyout Penalty Schedule");
            foreach (var penalty in cap.BuyoutPenalties.OrderBy(item => item.SeasonYear).ThenBy(item => item.Description, StringComparer.Ordinal))
            {
                builder.AppendLine($"{penalty.SeasonYear}: {penalty.Amount:C0} - {penalty.Description}");
            }
        }

        return builder.ToString();
    }

    private string BuildContractsWorkspace()
    {
        var summary = State.ContractManagement;
        var builder = new StringBuilder();
        builder.AppendLine("Contracts");
        builder.AppendLine("=========");
        builder.AppendLine("Contracts v2 keeps final signings under GM control. Accepted terms create pending decisions; they do not auto-sign.");
        builder.AppendLine("Agent Engine v1 routes contract talks through representatives. Contract windows expose an Agent Card, Client, Relationship, Negotiation Style, Agent comments, History, Improve Offer, Withdraw, Submit, Compare, and View Agent controls.");
        builder.AppendLine();
        builder.AppendLine("Budget Impact");
        builder.AppendLine($"  Total budget: {summary.Budget.TotalBudget:C0}");
        builder.AppendLine($"  Used budget: {summary.Budget.UsedBudget:C0}");
        builder.AppendLine($"  Remaining budget: {summary.Budget.RemainingBudget:C0}");
        builder.AppendLine($"  Player contracts: {summary.Budget.PlayerContractsTotal:C0}");
        builder.AppendLine($"  Staff contracts: {summary.Budget.StaffContractsTotal:C0}");
        builder.AppendLine($"  Status: {summary.Budget.Status} - {summary.Budget.OwnerBudgetConfidence}");
        builder.AppendLine();

        AppendContractSection(builder, "Expiring Player Contracts", summary.ExpiringPlayers);
        AppendContractSection(builder, "Expiring Staff Contracts", summary.ExpiringStaff);
        AppendContractSection(builder, "Unsigned Prospects / Draft Rights", summary.UnsignedProspects);
        AppendContractSection(builder, "Pending Contract Decisions", summary.PendingOffers);
        AppendContractSection(builder, "Accepted Offers Awaiting GM Approval", summary.AcceptedOffersAwaitingApproval);
        AppendContractSection(builder, "Rejected / Walk-Away Log", summary.RejectedOffers);
        builder.AppendLine(BuildContractRightsWorkspace());
        builder.AppendLine(BuildArbitrationWorkspace());
        builder.AppendLine(BuildBuyoutWorkspace());
        builder.AppendLine(BuildOfferSheetWorkspace());

        builder.AppendLine("Offer Builder Guidance");
        builder.AppendLine("----------------------");
        builder.AppendLine("Offer inputs supported by the engine: salary, term, role promise, development promise, camp invite promise, staff role/focus promise, and notes.");
        builder.AppendLine("Evaluation output includes total cost, annual cost, common expiry date, budget before/after, likelihood, risk warning, and plain-language reasons.");
        builder.AppendLine("Use Free Agents, Prospects, Recruits, Staff, or Pending Decisions to pick the person, then approve only the deals you actually want signed.");
        return builder.ToString();
    }

    private string BuildOfferSheetWorkspace()
    {
        var eligibility = State.OfferSheetEligibility;
        var sheets = State.OfferSheets;
        var builder = new StringBuilder();
        builder.AppendLine("Offer Sheets");
        builder.AppendLine("============");
        builder.AppendLine(State.OfferSheetRuleSummary);
        builder.AppendLine();
        builder.AppendLine("Use selected player detail panels to Submit Offer Sheet, Match Offer, Take Compensation, or View Dossier.");
        builder.AppendLine("Offer sheets do not auto-complete: the rights holder must explicitly match or decline.");
        builder.AppendLine();

        builder.AppendLine("Eligible RFAs");
        builder.AppendLine("-------------");
        var eligible = eligibility.Where(item => item.Status == OfferSheetStatus.Eligible).ToArray();
        if (eligible.Length == 0)
        {
            builder.AppendLine("  No eligible RFAs are currently available for offer-sheet pressure.");
        }
        else
        {
            foreach (var item in eligible)
            {
                builder.AppendLine($"  {item.PlayerName} | {State.PositionShortText(item.Position)} | age {item.Age?.ToString() ?? "unknown"} | rights: {item.RightsHolderTeamName}");
                builder.AppendLine($"    Offering team: {item.OfferingTeamName} | agent: {item.AgentInterest}");
                builder.AppendLine($"    Recommendation: {item.Recommendation}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Active / Historical Offer Sheets");
        builder.AppendLine("--------------------------------");
        if (sheets.Count == 0)
        {
            builder.AppendLine("  No offer sheets are active or recorded.");
            return builder.ToString();
        }

        foreach (var sheet in sheets.OrderByDescending(item => item.IsActive).ThenBy(item => item.ResponseDeadline).ThenBy(item => item.PlayerName, StringComparer.Ordinal))
        {
            builder.AppendLine($"  {sheet.PlayerName} | {State.DisplayOfferSheetStatus(sheet.Status)} | {sheet.AnnualSalary:C0} x {sheet.TermYears}");
            builder.AppendLine($"    Offer: {sheet.OfferingTeamName} -> rights holder {sheet.RightsHolderTeamName} | deadline {sheet.ResponseDeadline:yyyy-MM-dd}");
            builder.AppendLine($"    Compensation: {sheet.Compensation.Summary}");
            builder.AppendLine($"    Agent: {sheet.AgentComment}");
            builder.AppendLine($"    Recommendation: {sheet.RightsHolderRecommendation}");
        }

        return builder.ToString();
    }

    private string BuildBuyoutWorkspace()
    {
        var eligibility = State.BuyoutEligibility;
        var buyouts = State.ContractBuyouts;
        var window = State.BuyoutWindow;
        var builder = new StringBuilder();
        builder.AppendLine("Contract Buyouts");
        builder.AppendLine("================");
        builder.AppendLine(State.BuyoutRuleSummary);
        builder.AppendLine(window.Summary);
        builder.AppendLine();
        builder.AppendLine("Use selected contracted player detail panels to Calculate Buyout, Confirm Buyout, Cancel, or View Dossier.");
        builder.AppendLine();

        builder.AppendLine("Eligible / Contracted Players");
        builder.AppendLine("-----------------------------");
        if (eligibility.Count == 0)
        {
            builder.AppendLine("  No signed player contracts are currently available for buyout review.");
        }
        else
        {
            foreach (var item in eligibility.OrderByDescending(item => item.Status == BuyoutStatus.Eligible).ThenBy(item => item.PlayerName, StringComparer.Ordinal))
            {
                builder.AppendLine($"  {item.PlayerName} | {State.PositionShortText(item.Position)} | age {item.Age?.ToString() ?? "unknown"} | {item.YearsRemaining} year(s) | {State.DisplayBuyoutStatus(item.Status)}");
                builder.AppendLine($"    {item.Reason}");
                builder.AppendLine($"    Recommendation: {item.Recommendation}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Pending / Completed Buyouts");
        builder.AppendLine("---------------------------");
        if (buyouts.Count == 0)
        {
            builder.AppendLine("  No buyout calculations or completed buyouts.");
            return builder.ToString();
        }

        foreach (var buyout in buyouts.OrderByDescending(item => item.CreatedOn).ThenBy(item => item.PlayerName, StringComparer.Ordinal))
        {
            builder.AppendLine($"  {buyout.PlayerName} | {State.DisplayBuyoutStatus(buyout.Status)} | cost {buyout.Calculation.BuyoutCost:C0}");
            builder.AppendLine($"    Annual penalty: {buyout.Calculation.AnnualPenalty:C0} for {buyout.Calculation.PenaltySeasons} season(s) | current cap impact {buyout.Calculation.CurrentSeasonCapImpact:C0}");
            builder.AppendLine($"    Schedule: {string.Join(", ", buyout.Calculation.Penalties.Select(penalty => $"{penalty.SeasonYear}: {penalty.Amount:C0}"))}");
            builder.AppendLine($"    Warning: {buyout.Calculation.Warning}");
            builder.AppendLine($"    Recommendation: {buyout.Recommendation}");
        }

        return builder.ToString();
    }

    private string BuildArbitrationWorkspace()
    {
        var cases = State.ArbitrationCases;
        var eligibility = State.ArbitrationEligibility;
        var builder = new StringBuilder();
        builder.AppendLine("Salary Arbitration");
        builder.AppendLine("==================");
        builder.AppendLine(State.ArbitrationRuleSummary);
        builder.AppendLine();
        builder.AppendLine("Use selected player detail panels to File Arbitration, Negotiate Settlement, Accept Award, Walk Away, or View Dossier.");
        builder.AppendLine();

        builder.AppendLine("Eligible Players");
        builder.AppendLine("----------------");
        var eligible = eligibility.Where(item => item.Status == ArbitrationEligibilityStatus.Eligible).ToArray();
        if (eligible.Length == 0)
        {
            builder.AppendLine("  No eligible RFAs are available for arbitration.");
        }
        else
        {
            foreach (var item in eligible)
            {
                builder.AppendLine($"  {item.PlayerName} | {State.PositionShortText(item.Position)} | age {item.Age?.ToString() ?? "unknown"} | accrued {item.AccruedSeasons} | filing deadline {item.FilingDeadline?.ToString("yyyy-MM-dd") ?? "n/a"}");
                builder.AppendLine($"    {item.Reason}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Filed / Active Cases");
        builder.AppendLine("--------------------");
        if (cases.Count == 0)
        {
            builder.AppendLine("  No arbitration cases are active.");
            return builder.ToString();
        }

        foreach (var arbitrationCase in cases.OrderBy(item => item.HearingDate ?? item.FilingDeadline ?? DateOnly.MaxValue).ThenBy(item => item.PlayerName, StringComparer.Ordinal))
        {
            builder.AppendLine($"  {arbitrationCase.PlayerName} | {State.DisplayArbitrationStatus(arbitrationCase.Status)} | {State.PositionShortText(arbitrationCase.Position)}");
            builder.AppendLine($"    Filing deadline: {arbitrationCase.FilingDeadline?.ToString("yyyy-MM-dd") ?? "n/a"} | hearing: {arbitrationCase.HearingDate?.ToString("yyyy-MM-dd") ?? "not scheduled"}");
            if (arbitrationCase.Award is not null)
            {
                builder.AppendLine($"    Ask: {arbitrationCase.Award.PlayerAsk:C0} | team offer: {arbitrationCase.Award.TeamOffer:C0} | projected: {arbitrationCase.Award.ProjectedAwardLow:C0}-{arbitrationCase.Award.ProjectedAwardHigh:C0} | final: {arbitrationCase.Award.FinalAward:C0}");
                builder.AppendLine($"    Cap/budget: {arbitrationCase.Award.CapImpact}");
                builder.AppendLine($"    Explanation: {arbitrationCase.Award.Explanation}");
            }

            builder.AppendLine($"    Agent: {arbitrationCase.AgentComment}");
            builder.AppendLine($"    Recommendation: {arbitrationCase.Recommendation}");
        }

        return builder.ToString();
    }

    private string BuildContractRightsWorkspace()
    {
        var decisions = State.ContractRightsDecisions;
        var builder = new StringBuilder();
        builder.AppendLine("Contract Rights / Expiring Contracts");
        builder.AppendLine("====================================");
        builder.AppendLine(State.ContractRightsRuleSummary);
        builder.AppendLine();
        builder.AppendLine("Filters available for this view: All, Pending RFA, RFA, Pending UFA, UFA, Qualified, Not Qualified, Rights Released.");
        builder.AppendLine("Use player detail panels to Qualify, Do Not Qualify, Negotiate Contract, or View Dossier.");
        builder.AppendLine();
        if (decisions.Count == 0)
        {
            builder.AppendLine("No RFA/UFA rights decisions are active for this rulebook.");
            return builder.ToString();
        }

        foreach (var decision in decisions.OrderBy(decision => decision.ExpiryRule?.Deadline ?? DateOnly.MaxValue).ThenBy(decision => decision.PlayerName, StringComparer.Ordinal))
        {
            builder.AppendLine($"{decision.PlayerName} | {State.DisplayRightsStatus(decision.RightsStatus)} | {State.PositionShortText(decision.Position)} | age {decision.Age?.ToString() ?? "unknown"} | accrued {decision.AccruedSeasons}");
            builder.AppendLine($"  Contract expiry: {decision.ContractExpiryDate?.ToString("yyyy-MM-dd") ?? "unknown"} | QO required: {(decision.QualifyingOfferRequired ? "yes" : "no")} | deadline: {decision.ExpiryRule?.Deadline.ToString("yyyy-MM-dd") ?? "n/a"}");
            if (decision.QualifyingOffer is not null)
            {
                builder.AppendLine($"  Qualifying offer: {decision.QualifyingOffer.RequiredSalary:C0} {decision.QualifyingOffer.Currency} | {(decision.QualifyingOffer.IsIssued ? "issued" : "not issued")}");
            }

            builder.AppendLine($"  Recommendation: {decision.Recommendation}");
            builder.AppendLine($"  Agent note: {decision.AgentNote}");
        }

        return builder.ToString();
    }

    private static void AppendContractSection(StringBuilder builder, string title, IReadOnlyList<ContractAsk> asks)
    {
        builder.AppendLine(title);
        builder.AppendLine(new string('-', title.Length));
        if (asks.Count == 0)
        {
            builder.AppendLine("  None.");
            builder.AppendLine();
            return;
        }

        foreach (var ask in asks.OrderBy(ask => ask.PersonName, StringComparer.Ordinal))
        {
            builder.AppendLine($"  {ask.PersonName} | {ask.AskType} | ask {ask.RequestedSalary:C0} x {ask.RequestedTermYears} year(s)");
            builder.AppendLine($"    Desired role: {ask.DesiredRole}");
            builder.AppendLine($"    Interest: {ask.InterestLevel} | org fit {ask.PreferredOrganizationFit}/100 | trust {ask.RelationshipTrustImpact}/100");
            builder.AppendLine($"    Budget after ask: {ask.BudgetImpact:C0}");
            builder.AppendLine($"    Priority: {ask.SigningPriority}");
            builder.AppendLine($"    Development/pathway: {ask.DevelopmentPathwayConcern}");
            builder.AppendLine($"    Staff/coach confidence: {ask.StaffCoachConfidence}");
        }

        builder.AppendLine();
    }

    private string BuildOrganizationPlanning()
    {
        var plan = State.CurrentOrganizationPlan;
        var builder = new StringBuilder();
        builder.AppendLine("Organization Planning");
        builder.AppendLine("=====================");
        builder.AppendLine(plan.Summary);
        builder.AppendLine($"Window: {OrganizationPlanningService.Readable(plan.Window)}");
        builder.AppendLine($"Horizon: {OrganizationPlanningService.Readable(plan.Horizon)}");
        builder.AppendLine($"Updated: {plan.LastUpdated:yyyy-MM-dd}");
        builder.AppendLine();

        builder.AppendLine("Future Needs");
        foreach (var need in plan.RosterPlan.FutureNeeds.Take(8))
        {
            builder.AppendLine($"- {need}");
        }

        builder.AppendLine();
        builder.AppendLine("Current Depth Chart");
        foreach (var slot in plan.DepthPlan.CurrentDepth.Take(14))
        {
            builder.AppendLine($"- {slot.Slot}: {slot.PlayerName} | {slot.Position} | {slot.Role} | age {slot.Age?.ToString() ?? "unknown"}");
        }

        builder.AppendLine();
        builder.AppendLine("Future Depth Chart");
        foreach (var slot in plan.DepthPlan.FutureDepth.Take(14))
        {
            builder.AppendLine($"- {slot.Year} {slot.Slot}: {slot.PlayerName} | {slot.Position} | {slot.Role}");
            builder.AppendLine($"  {slot.Summary}");
        }

        builder.AppendLine();
        builder.AppendLine("Prospect Pipeline");
        foreach (var prospect in plan.ProspectPlan.Prospects.Take(10))
        {
            builder.AppendLine($"- {prospect.PlayerName} | {prospect.Position} | ETA {prospect.ExpectedArrivalYear} | {prospect.ProjectedRole}");
            builder.AppendLine($"  Path: {string.Join(" -> ", prospect.Path)}");
            builder.AppendLine($"  {prospect.Recommendation}");
        }

        if (plan.ProspectPlan.PipelineRisks.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Pipeline Risks");
            foreach (var risk in plan.ProspectPlan.PipelineRisks)
            {
                builder.AppendLine($"- {risk}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Succession / Blocking");
        foreach (var line in plan.RosterPlan.SuccessionPlans.Concat(plan.RosterPlan.BlockedProspects).DefaultIfEmpty("No major succession or blocking issue flagged."))
        {
            builder.AppendLine($"- {line}");
        }

        builder.AppendLine();
        builder.AppendLine("Contract Planning");
        builder.AppendLine(plan.ContractPlan.Summary);
        builder.AppendLine(plan.ContractPlan.CapBudgetSummary);
        foreach (var item in plan.ContractPlan.ExpiringContracts.Take(8))
        {
            builder.AppendLine($"- {item.PlayerName}: expires {item.ExpiryYear}, {item.Salary:C0}, {item.Recommendation}");
        }

        builder.AppendLine();
        builder.AppendLine("Free Agency Plan");
        foreach (var line in plan.FreeAgencyTargets.DefaultIfEmpty("No external signing pressure right now."))
        {
            builder.AppendLine($"- {line}");
        }

        builder.AppendLine();
        builder.AppendLine("Trade Plan");
        foreach (var line in plan.TradeTargets.DefaultIfEmpty("No urgent trade pressure right now."))
        {
            builder.AppendLine($"- {line}");
        }

        builder.AppendLine();
        builder.AppendLine("Planning Reports");
        foreach (var line in plan.Reports)
        {
            builder.AppendLine($"- {line}");
        }

        builder.AppendLine();
        builder.AppendLine("League AI Organization Plans");
        foreach (var leaguePlan in State.OrganizationPlans.Take(8))
        {
            builder.AppendLine($"- {leaguePlan.OrganizationName}: {OrganizationPlanningService.Readable(leaguePlan.Window)} | {leaguePlan.RosterPlan.FutureNeeds.FirstOrDefault() ?? leaguePlan.Summary}");
        }

        return builder.ToString();
    }

    private string BuildOrganizationHealth()
    {
        var readiness = State.SeasonReadinessReport;
        var leagueProfile = State.PlayerOrganizationLeagueProfile;
        var builder = new StringBuilder();
        builder.AppendLine("Organization Health");
        builder.AppendLine("===================");
        builder.AppendLine("Relationship Chemistry");
        foreach (var line in State.RelationshipChemistry.SummaryLines)
        {
            builder.AppendLine($"- {line}");
        }

        foreach (var conflict in State.RelationshipConflicts.Where(conflict => conflict.IsMajor && conflict.IsActive).Take(3))
        {
            var profile = State.RelationshipProfiles.FirstOrDefault(profile => profile.RelationshipProfileId == conflict.RelationshipProfileId);
            builder.AppendLine($"Conflict warning: {profile?.TargetName ?? "Unknown"} - {conflict.VisibleExplanation}");
        }

        builder.AppendLine();
        builder.AppendLine("Team Identity");
        builder.AppendLine($"Identity: {leagueProfile.Identity}");
        builder.AppendLine($"Current strategy: {leagueProfile.CurrentStrategy}");
        builder.AppendLine($"GM personality: {leagueProfile.GmPersonality}");
        builder.AppendLine($"Owner philosophy: {leagueProfile.OwnerPhilosophy}");
        builder.AppendLine($"Budget style: {leagueProfile.BudgetStyle}");
        builder.AppendLine($"Draft style: {leagueProfile.DraftStyle}");
        builder.AppendLine($"Scouting focus: {leagueProfile.ScoutingFocus}");
        builder.AppendLine($"Development grade: {leagueProfile.DevelopmentGrade}");
        builder.AppendLine($"Recent direction: {leagueProfile.RecentDirection}");
        builder.AppendLine();
        builder.AppendLine("League AI v2");
        var aiProfile = State.PlayerOrganizationAiProfile;
        builder.AppendLine($"AI personality: {aiProfile.Personality}");
        builder.AppendLine($"Strategy phase: {aiProfile.Strategy.Phase}");
        builder.AppendLine($"Draft philosophy: {aiProfile.Strategy.DraftPhilosophy}");
        builder.AppendLine($"Trade behavior: {aiProfile.Strategy.TradeBehavior}");
        builder.AppendLine($"Free agency behavior: {aiProfile.Strategy.FreeAgencyBehavior}");
        builder.AppendLine($"Budget behavior: {aiProfile.Strategy.BudgetBehavior}");
        builder.AppendLine($"Scouting philosophy: {aiProfile.Strategy.ScoutingBehavior}");
        builder.AppendLine($"Staff behavior: {aiProfile.Strategy.StaffBehavior}");
        builder.AppendLine("Recent strategy changes:");
        foreach (var change in aiProfile.StrategyHistory.TakeLast(3))
        {
            builder.AppendLine($"- {change.Date:yyyy-MM-dd}: {change.FromPhase} -> {change.ToPhase} - {change.Reason}");
        }

        builder.AppendLine();
        builder.AppendLine("Current Needs");
        foreach (var need in leagueProfile.CurrentNeeds)
        {
            builder.AppendLine($"- {need.Priority}: {need.Need} - {need.Reason}");
        }

        builder.AppendLine("AI Need Profiles");
        foreach (var need in aiProfile.CurrentNeeds)
        {
            builder.AppendLine($"- {need.Priority}: {need.NeedType} | urgency {need.Urgency} | target {need.SuggestedAssetType} - {need.Reason}");
        }

        builder.AppendLine();
        builder.AppendLine("Behavior");
        builder.AppendLine(leagueProfile.Behavior.DraftBehavior);
        builder.AppendLine(leagueProfile.Behavior.TradeBehavior);
        builder.AppendLine(leagueProfile.Behavior.FreeAgencyBehavior);
        builder.AppendLine(leagueProfile.Behavior.ScoutingBehavior);
        builder.AppendLine(leagueProfile.Behavior.DevelopmentBehavior);
        builder.AppendLine(leagueProfile.Behavior.StaffHiringBehavior);
        builder.AppendLine();
        builder.AppendLine("League Identity Snapshot");
        foreach (var profile in State.LeagueOrganizationProfiles.OrderBy(profile => profile.TeamName, StringComparer.Ordinal))
        {
            builder.AppendLine($"- {profile.TeamName}: {profile.Identity}, {profile.CurrentStrategy}, needs {string.Join(", ", profile.CurrentNeeds.Take(2).Select(need => need.Need))}");
        }

        builder.AppendLine();
        builder.AppendLine($"Owner mood: {OwnerMoodText()}");
        builder.AppendLine($"Owner satisfaction: {readiness.OwnerSatisfaction}");
        builder.AppendLine($"Organization health: {readiness.OrganizationHealth}");
        builder.AppendLine($"Roster status: {readiness.RosterStatus}");
        builder.AppendLine($"Staff vacancies: {State.StaffVacancySummary}");
        builder.AppendLine($"Budget: {State.BudgetOverview.Status} ({State.BudgetOverview.RemainingBudget:C0} remaining)");
        builder.AppendLine();
        builder.AppendLine("Medical Summary");
        builder.AppendLine(State.MedicalSummaryText());
        builder.AppendLine();
        builder.AppendLine($"Pending GM decisions: {State.PendingDecisionCount}");
        builder.AppendLine($"Roster warnings: {State.RosterWarningCount}");
        builder.AppendLine($"Scouting reports: {State.ScoutingReportCount}");
        if (State.ScenarioSnapshot.OrganizationHistory is not null)
        {
            builder.AppendLine($"Prior season: {State.ScenarioSnapshot.OrganizationHistory.RecordText}");
            builder.AppendLine($"Previous champion: {State.ScenarioSnapshot.OrganizationHistory.PreviousLeagueChampion}");
        }
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

    private string BuildPlayoffArchive()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Playoff Archive");
        builder.AppendLine("===============");
        var bracket = State.ScenarioSnapshot.Playoffs.Bracket;
        if (bracket is null)
        {
            builder.AppendLine("No playoff bracket has been archived for the current season yet.");
        }
        else
        {
            builder.AppendLine($"{bracket.SeasonYear} {bracket.Format.FormatType} | Status: {bracket.Status}");
            builder.AppendLine($"Champion: {bracket.ChampionTeamName ?? "pending"}");
            builder.AppendLine($"Runner-up: {bracket.RunnerUpTeamName ?? "pending"}");
            builder.AppendLine($"MVP placeholder: {bracket.PlayoffMvpPlaceholder ?? "pending"}");
            builder.AppendLine();
            builder.AppendLine("Series Results");
            if (bracket.Results.Count == 0)
            {
                builder.AppendLine("  No series has been decided yet.");
            }

            foreach (var result in bracket.Results.OrderBy(result => result.RoundNumber).ThenBy(result => result.SeriesId, StringComparer.Ordinal))
            {
                builder.AppendLine($"  Round {result.RoundNumber}: {result.Summary}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Archived Season Champions");
        foreach (var archive in State.ScenarioSnapshot.SeasonRollover.SeasonArchives.OrderByDescending(archive => archive.SeasonYear))
        {
            builder.AppendLine($"  {archive.SeasonYear}: {archive.ChampionTeamName}");
        }

        return builder.ToString();
    }

    private string BuildChampionsReport()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Champions");
        builder.AppendLine("=========");
        var bracket = State.ScenarioSnapshot.Playoffs.Bracket;
        if (bracket?.Status == PlayoffStatus.Completed)
        {
            builder.AppendLine($"{bracket.SeasonYear}: {bracket.ChampionTeamName} defeated {bracket.RunnerUpTeamName}");
            builder.AppendLine($"Playoff MVP: {bracket.PlayoffMvpPlaceholder ?? "pending"}");
        }
        else
        {
            builder.AppendLine("Current season champion: pending.");
        }

        foreach (var archive in State.ScenarioSnapshot.SeasonRollover.SeasonArchives.OrderByDescending(archive => archive.SeasonYear))
        {
            builder.AppendLine($"{archive.SeasonYear}: {archive.ChampionTeamName}");
        }

        return builder.ToString();
    }

    private string BuildMonthlySummaries() => BuildMonthlySummary();

    private string BuildGmCareerHistory()
    {
        var builder = new StringBuilder();
        builder.AppendLine("GM Career");
        builder.AppendLine("=========");
        var history = State.ScenarioSnapshot.GmCareerHistory;
        if (history is null)
        {
            builder.AppendLine("No GM career history has been recorded yet.");
            return builder.ToString();
        }

        builder.AppendLine($"{history.GmName} - {history.OrganizationName}");
        builder.AppendLine($"Hire date: {history.HireDate:yyyy-MM-dd}");
        builder.AppendLine($"Seasons completed: {history.SeasonsCompleted}");
        builder.AppendLine($"Record: {history.Record}");
        builder.AppendLine($"Playoff record: {history.PlayoffRecordPlaceholder}");
        builder.AppendLine($"Draft picks made: {history.DraftPicksMade}");
        builder.AppendLine($"Trades made: {history.TradesMade}");
        builder.AppendLine($"Free agents signed: {history.FreeAgentsSigned}");
        builder.AppendLine($"Staff hired: {history.StaffHired}");
        builder.AppendLine();
        builder.AppendLine("Owner confidence history");
        foreach (var line in history.OwnerConfidenceHistory)
        {
            builder.AppendLine($"  {line}");
        }

        builder.AppendLine();
        builder.AppendLine("Career notes");
        foreach (var line in history.CareerNotes)
        {
            builder.AppendLine($"  {line}");
        }

        return builder.ToString();
    }

    private string BuildOrganizationHistoryReport()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Organization History");
        builder.AppendLine("====================");
        if (State.ScenarioSnapshot.OrganizationHistory is not null)
        {
            var previous = State.ScenarioSnapshot.OrganizationHistory;
            builder.AppendLine("Existing World Snapshot");
            builder.AppendLine($"{previous.OrganizationName} {previous.PriorSeasonYear}: {previous.RecordText}");
            builder.AppendLine($"Playoffs: {previous.PlayoffResult}");
            builder.AppendLine($"Previous champion: {previous.PreviousLeagueChampion}");
            builder.AppendLine(previous.Summary);
            builder.AppendLine();
        }

        foreach (var season in State.ScenarioSnapshot.OrganizationSeasonHistory.OrderByDescending(item => item.SeasonYear))
        {
            builder.AppendLine($"{season.SeasonYear} - {season.OrganizationName}");
            builder.AppendLine($"  Record: {season.Record}");
            builder.AppendLine($"  Playoffs: {season.PlayoffResult}");
            builder.AppendLine($"  Draft class: {season.DraftClassSummary}");
            builder.AppendLine($"  Notable players: {season.NotablePlayers}");
            builder.AppendLine($"  Staff: {season.StaffHistorySummary}");
            builder.AppendLine($"  Owner changes: {season.OwnerChanges}");
            builder.AppendLine($"  Championships: {season.Championships}");
            builder.AppendLine($"  Summary: {season.Summary}");
        }

        return builder.ToString();
    }

    private string BuildDraftHistoryReport()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Draft History");
        builder.AppendLine("=============");
        builder.AppendLine("Current GM Draft Classes");
        foreach (var draftClass in State.ScenarioSnapshot.DraftClassHistory.OrderByDescending(item => item.Year))
        {
            builder.AppendLine($"{draftClass.Year}: {draftClass.Summary}");
            if (draftClass.ClassProfile is not null)
            {
                builder.AppendLine($"  Theme: {draftClass.ClassProfile.ReadableTheme}");
                builder.AppendLine($"  Strengths: {string.Join("; ", draftClass.ClassProfile.Strengths.Select(item => $"{item.Category}: {item.Description}"))}");
                builder.AppendLine($"  Weaknesses: {string.Join("; ", draftClass.ClassProfile.Weaknesses.Select(item => $"{item.Category}: {item.Description}"))}");
                builder.AppendLine($"  Original class read: {draftClass.ClassProfile.PreviewText}");
            }

            foreach (var pick in draftClass.Picks)
            {
                builder.AppendLine($"  R{pick.Round} P{pick.OverallPick}: {pick.PlayerName} ({pick.Position}) - original board #{pick.OriginalBoardRank} - {pick.Outcome}");
                builder.AppendLine($"    Class context: {pick.DraftClassContext}");
            }
        }

        if (State.ScenarioSnapshot.DraftClassHistory.Count == 0)
        {
            builder.AppendLine("No current-GM draft class has been completed yet.");
        }

        builder.AppendLine();
        builder.AppendLine("Prior Organization Draft History");
        foreach (var record in State.ScenarioSnapshot.DraftHistory.Take(12))
        {
            builder.AppendLine($"  {record.SeasonYear} R{record.Round} P{record.Pick}: {record.ProspectName} - {record.OutcomeSummary}");
        }

        return builder.ToString();
    }

    private string BuildDraftedPlayersReport()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Drafted Players");
        builder.AppendLine("===============");
        if (State.ScenarioSnapshot.DraftPickHistory.Count == 0)
        {
            builder.AppendLine("No current-GM drafted players are tracked yet.");
            return builder.ToString();
        }

        foreach (var pick in State.ScenarioSnapshot.DraftPickHistory.OrderByDescending(item => item.Year).ThenBy(item => item.Round).ThenBy(item => item.OverallPick))
        {
            builder.AppendLine($"{pick.PlayerName} | {pick.Position} | {pick.Year} R{pick.Round} P{pick.OverallPick}");
            builder.AppendLine($"  Drafted from: {pick.TeamDraftedFrom}");
            builder.AppendLine($"  Projection: {pick.ScoutingProjectionAtDraft}");
            builder.AppendLine($"  Confidence: {pick.ScoutConfidenceAtDraft?.ToString() ?? "Unknown"}");
            builder.AppendLine($"  GM notes at draft: {pick.GmNotesAtDraft}");
            builder.AppendLine($"  Status: {pick.CurrentStatus}; outcome so far: {pick.Outcome}");
        }

        return builder.ToString();
    }

    private string BuildWhereAreTheyNowReport()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Where Are They Now");
        builder.AppendLine("==================");
        var records = State.WhereAreTheyNow;
        if (records.Count == 0)
        {
            builder.AppendLine("No current-GM drafted players are tracked yet.");
            return builder.ToString();
        }

        foreach (var record in records)
        {
            builder.AppendLine($"{record.PlayerName} | {record.Position} | {record.DraftYear} R{record.Round} P{record.Pick}");
            builder.AppendLine($"  Current team/status: {record.CurrentTeamOrStatus}");
            builder.AppendLine($"  Current role: {record.CurrentRole}");
            builder.AppendLine($"  Latest stats: {record.LatestStats}");
            builder.AppendLine($"  Development trend: {record.DevelopmentTrend}");
            builder.AppendLine($"  Injury status: {record.InjuryStatus}");
            builder.AppendLine($"  Staff opinion: {record.StaffOpinion}");
            builder.AppendLine($"  Draft class context: {record.DraftClassContext}");
            builder.AppendLine($"  Outcome so far: {record.OutcomeSoFar}");
        }

        return builder.ToString();
    }

    private string BuildPlayerCareerTimelinesReport()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Player Career Timelines");
        builder.AppendLine("=======================");
        foreach (var group in State.ScenarioSnapshot.CareerTimeline.Entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.PersonId))
            .GroupBy(entry => entry.PersonId)
            .Take(30))
        {
            var name = State.FindPersonNameForDisplay(group.Key!);
            builder.AppendLine(name);
            foreach (var entry in group.OrderByDescending(item => item.Date).Take(6))
            {
                builder.AppendLine($"  {entry.Date:yyyy-MM-dd} [{entry.EntryType}] {entry.Title} - {entry.Description}");
            }
        }

        if (State.ScenarioSnapshot.CareerTimeline.Entries.Count == 0)
        {
            builder.AppendLine("No career timeline entries have been recorded yet.");
        }

        return builder.ToString();
    }

    private string BuildCareerMilestonesReport()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Career Milestones");
        builder.AppendLine("=================");
        if (State.ScenarioSnapshot.PlayerMilestones.Count == 0)
        {
            builder.AppendLine("No player milestones have been tracked yet.");
            return builder.ToString();
        }

        foreach (var group in State.ScenarioSnapshot.PlayerMilestones
            .OrderByDescending(item => item.Date)
            .GroupBy(item => item.PlayerName)
            .Take(40))
        {
            builder.AppendLine(group.Key);
            foreach (var milestone in group.Take(8))
            {
                builder.AppendLine($"  {milestone.Date:yyyy-MM-dd} [{milestone.MilestoneType}] {milestone.Summary}");
            }
        }

        return builder.ToString();
    }

    private string BuildPlayerStoriesReport()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Player Stories");
        builder.AppendLine("==============");
        if (State.ScenarioSnapshot.PlayerCareerSummaries.Count == 0)
        {
            builder.AppendLine("No player stories have been built yet.");
            return builder.ToString();
        }

        foreach (var summary in State.ScenarioSnapshot.PlayerCareerSummaries
            .OrderByDescending(item => item.LegacyScore)
            .ThenBy(item => item.PlayerName, StringComparer.Ordinal)
            .Take(40))
        {
            builder.AppendLine($"{summary.PlayerName} - {summary.LifeStage} / {summary.CareerPhase} / {summary.Reputation}");
            builder.AppendLine($"  Legacy score: {summary.LegacyScore}");
            builder.AppendLine($"  {summary.CareerSummaryText}");
            foreach (var story in summary.CareerStory.Take(5))
            {
                builder.AppendLine($"  Story: {story}");
            }

            if (summary.InfluentialStaff.Count > 0)
            {
                builder.AppendLine($"  Influential staff: {string.Join(", ", summary.InfluentialStaff)}");
            }
        }

        return builder.ToString();
    }

    private string BuildStaffHistoryReport()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Staff History");
        builder.AppendLine("=============");
        foreach (var staff in State.ScenarioSnapshot.StaffCareerHistory.OrderBy(item => item.StaffName))
        {
            builder.AppendLine($"{staff.StaffName} - {staff.CurrentRole}");
            builder.AppendLine($"  Current organization: {staff.CurrentOrganization}");
            builder.AppendLine($"  GM relationship: {staff.RelationshipWithGm}");
            builder.AppendLine($"  Evaluation: {staff.EvaluationSummary}");
            builder.AppendLine("  Previous roles:");
            foreach (var role in staff.PreviousRoles)
            {
                builder.AppendLine($"    {role}");
            }

            builder.AppendLine("  Notable history:");
            foreach (var note in staff.NotableHistory)
            {
                builder.AppendLine($"    {note}");
            }
        }

        return builder.ToString();
    }

    private string BuildStaffCareersReport()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Staff Careers");
        builder.AppendLine("=============");
        var summaries = State.ScenarioSnapshot.StaffCareerSummaries;
        if (summaries.Count == 0)
        {
            builder.AppendLine("No staff career summaries have been built yet.");
            return builder.ToString();
        }

        foreach (var summary in summaries.OrderByDescending(item => item.LegacyScore).ThenBy(item => item.StaffName, StringComparer.Ordinal))
        {
            builder.AppendLine($"{summary.StaffName} - {summary.CurrentRole} / {summary.LifeStage} / {summary.Reputation}");
            builder.AppendLine($"  Department: {summary.Department}");
            builder.AppendLine($"  Career phase: {summary.CareerPhase}");
            builder.AppendLine($"  Legacy score: {summary.LegacyScore}");
            builder.AppendLine($"  {summary.CareerSummaryText}");
            builder.AppendLine($"  Personal legacy: {summary.PersonalLegacy}");
            builder.AppendLine($"  Promotion: {summary.PromotionReadiness}");
            builder.AppendLine($"  Concern: {summary.ConcernSummary}");
            foreach (var story in summary.CareerStory.Take(5))
            {
                builder.AppendLine($"  Story: {story}");
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private string BuildCoachingTreesReport()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Coaching Trees");
        builder.AppendLine("==============");
        var coaches = State.ScenarioSnapshot.StaffCareerSummaries
            .Where(item => item.Department == StaffDepartment.Coaching || item.CurrentRole is StaffRole.GeneralManager or StaffRole.AssistantGM)
            .OrderByDescending(item => item.CoachingTree.Count)
            .ThenBy(item => item.StaffName, StringComparer.Ordinal)
            .ToArray();
        if (coaches.Length == 0)
        {
            builder.AppendLine("No coaching tree data has been recorded yet.");
            return builder.ToString();
        }

        foreach (var coach in coaches)
        {
            builder.AppendLine($"{coach.StaffName} - {coach.CurrentRole}");
            if (coach.CoachingTree.Count == 0)
            {
                builder.AppendLine("  No tree links yet.");
            }
            else
            {
                foreach (var link in coach.CoachingTree)
                {
                    builder.AppendLine($"  {link}");
                }
            }
        }

        return builder.ToString();
    }

    private string BuildScoutHistoryReport()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Scout History");
        builder.AppendLine("=============");
        var scouts = State.ScenarioSnapshot.StaffCareerSummaries
            .Where(item => item.Department == StaffDepartment.Scouting)
            .OrderByDescending(item => item.LegacyScore)
            .ThenBy(item => item.StaffName, StringComparer.Ordinal)
            .ToArray();
        if (scouts.Length == 0)
        {
            builder.AppendLine("No scout career data has been recorded yet.");
            return builder.ToString();
        }

        foreach (var scout in scouts)
        {
            builder.AppendLine($"{scout.StaffName} - {scout.CurrentRole}");
            builder.AppendLine($"  Reputation: {scout.Reputation}; legacy {scout.LegacyScore}");
            builder.AppendLine($"  {scout.PersonalLegacy}");
            builder.AppendLine("  Players discovered / recommended:");
            foreach (var player in scout.PlayersDiscovered.DefaultIfEmpty("No credited discoveries yet.").Take(8))
            {
                builder.AppendLine($"    {player}");
            }
        }

        return builder.ToString();
    }

    private string BuildDevelopmentStaffHistoryReport()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Development Staff History");
        builder.AppendLine("=========================");
        var developmentStaff = State.ScenarioSnapshot.StaffCareerSummaries
            .Where(item => item.Department == StaffDepartment.Coaching || item.CurrentRole is StaffRole.DevelopmentCoach or StaffRole.SkillsCoach or StaffRole.GoalieCoach or StaffRole.GoaltendingCoach)
            .OrderByDescending(item => item.PlayersDeveloped.Count)
            .ThenByDescending(item => item.LegacyScore)
            .ThenBy(item => item.StaffName, StringComparer.Ordinal)
            .ToArray();
        if (developmentStaff.Length == 0)
        {
            builder.AppendLine("No development staff history has been recorded yet.");
            return builder.ToString();
        }

        foreach (var staff in developmentStaff)
        {
            builder.AppendLine($"{staff.StaffName} - {staff.CurrentRole}");
            builder.AppendLine($"  Phase: {staff.CareerPhase}; reputation {staff.Reputation}; legacy {staff.LegacyScore}");
            builder.AppendLine("  Players developed:");
            foreach (var player in staff.PlayersDeveloped.DefaultIfEmpty("No credited development influence yet.").Take(8))
            {
                builder.AppendLine($"    {player}");
            }
        }

        return builder.ToString();
    }

    private string BuildOwnerHistoryReport()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Owner History");
        builder.AppendLine("=============");
        var summary = State.ScenarioSnapshot.OwnerCareerSummary;
        if (summary is null)
        {
            builder.AppendLine("No owner life-cycle history has been generated yet.");
            return builder.ToString();
        }

        builder.AppendLine($"{summary.OwnerName} - {summary.LifeStage}");
        builder.AppendLine($"Personality: {summary.CurrentPersonality}");
        builder.AppendLine($"Confidence trend: {summary.ConfidenceTrend}");
        builder.AppendLine(summary.CareerSummaryText);
        builder.AppendLine();
        builder.AppendLine("Legacy Profile");
        builder.AppendLine($"Tenure: {summary.LegacyProfile.TenureYears} year(s)");
        builder.AppendLine($"Philosophy era: {summary.LegacyProfile.PhilosophyEra}");
        builder.AppendLine($"Budget era: {summary.LegacyProfile.BudgetEra}");
        builder.AppendLine($"GM relationship era: {summary.LegacyProfile.GmRelationshipEra}");
        builder.AppendLine($"Competitive era: {summary.LegacyProfile.CompetitiveEra}");
        builder.AppendLine($"Summary: {summary.LegacyProfile.LegacySummary}");
        builder.AppendLine();
        builder.AppendLine("Milestones");
        foreach (var milestone in summary.Milestones.OrderByDescending(item => item.Date).Take(20))
        {
            builder.AppendLine($"{milestone.Date:yyyy-MM-dd} [{milestone.MilestoneType}] {milestone.Summary}");
        }

        return builder.ToString();
    }

    private string BuildOwnerLettersReport()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Owner Letters");
        builder.AppendLine("=============");
        var letters = State.ScenarioSnapshot.OwnerLetters;
        if (letters.Count == 0)
        {
            builder.AppendLine("No owner letters have been stored yet.");
            return builder.ToString();
        }

        foreach (var letter in letters.OrderByDescending(item => item.Date))
        {
            builder.AppendLine($"{letter.Date:yyyy-MM-dd} - {letter.Subject}{(letter.IsWarning ? " [Warning]" : string.Empty)}");
            builder.AppendLine(letter.Body);
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private string BuildJobSecurityHistoryReport()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Job Security History");
        builder.AppendLine("====================");
        var history = State.ScenarioSnapshot.OwnerJobSecurityHistory;
        if (history.Count == 0)
        {
            builder.AppendLine("No job-security history has been stored yet.");
            return builder.ToString();
        }

        foreach (var item in history.OrderByDescending(item => item.Date))
        {
            builder.AppendLine($"{item.Date:yyyy-MM-dd} - {item.Level} ({item.Score}/100), trend {item.Trend}");
            builder.AppendLine($"  {item.Reason}");
        }

        return builder.ToString();
    }

    private string BuildExpectationResultsReport()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Expectation Results");
        builder.AppendLine("===================");
        var history = State.ScenarioSnapshot.OwnerExpectationHistory;
        if (history.Count == 0)
        {
            builder.AppendLine("No owner expectation history has been stored yet.");
            return builder.ToString();
        }

        foreach (var item in history.OrderByDescending(item => item.SeasonYear).ThenByDescending(item => item.Priority))
        {
            builder.AppendLine($"{item.SeasonYear} - {item.ExpectationType} | Priority {item.Priority} | Difficulty {item.Difficulty}");
            builder.AppendLine($"  Result: {item.Result}; progress {item.Progress}/100");
            builder.AppendLine($"  Owner reaction: {item.OwnerReaction}");
            builder.AppendLine($"  GM performance: {item.GmPerformanceSummary}");
        }

        return builder.ToString();
    }

    private string BuildTransactionHistoryReport()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Transaction History");
        builder.AppendLine("===================");
        if (State.ScenarioSnapshot.TransactionHistory.Count == 0)
        {
            builder.AppendLine("No current-GM transaction history has been recorded yet.");
            return builder.ToString();
        }

        foreach (var transaction in State.ScenarioSnapshot.TransactionHistory.OrderByDescending(item => item.Date))
        {
            builder.AppendLine($"{transaction.Date:yyyy-MM-dd} [{transaction.TransactionType}] {transaction.PersonName}");
            builder.AppendLine($"  {transaction.Summary}");
        }

        return builder.ToString();
    }

    private string BuildCareerHistory()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Career History");
        builder.AppendLine("==============");
        if (State.ScenarioSnapshot.OrganizationHistory is not null)
        {
            var history = State.ScenarioSnapshot.OrganizationHistory;
            builder.AppendLine("Organization Prior Season");
            builder.AppendLine($"{history.OrganizationName}: {history.RecordText}");
            builder.AppendLine($"Playoffs: {history.PlayoffResult}");
            builder.AppendLine($"Previous champion: {history.PreviousLeagueChampion}");
            builder.AppendLine(history.Summary);
            builder.AppendLine();
        }

        builder.AppendLine("Returning Player History");
        foreach (var stat in State.ScenarioSnapshot.PriorSeasonStats
            .Where(stat => State.Snapshot.Roster.Players.Any(player => player.PersonId == stat.PersonId))
            .Take(12))
        {
            builder.AppendLine($"  {stat.PlayerName}: {stat.SummaryText}");
        }

        builder.AppendLine();
        builder.AppendLine("Recent Draft History");
        foreach (var record in State.ScenarioSnapshot.DraftHistory.Take(8))
        {
            builder.AppendLine($"  {record.SeasonYear} R{record.Round} P{record.Pick}: {record.ProspectName} - {record.OutcomeSummary}");
        }

        return builder.ToString();
    }

    private string BuildSettings()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Settings");
        builder.AppendLine("========");
        builder.AppendLine("Save / Load");
        var gmName = State.ScenarioSnapshot.GeneralManagerProfile.Person.Identity.DisplayName;
        builder.AppendLine($"Save name: {gmName} - {State.ScenarioSnapshot.Organization.Name}");
        builder.AppendLine($"GM: {gmName}");
        builder.AppendLine($"Team: {State.ScenarioSnapshot.Organization.Name}");
        builder.AppendLine($"League: {State.ScenarioSnapshot.LeagueProfile.Identity.Name}");
        builder.AppendLine($"Current date: {State.Snapshot.CurrentDate:yyyy-MM-dd}");
        builder.AppendLine($"Season: {State.ScenarioSnapshot.Season.Year}");
        builder.AppendLine($"Record: {State.TeamRecordText}");
        builder.AppendLine($"Save version: {SaveGameVersion.Current.SaveFormatVersion} ({SaveGameVersion.Current.GameVersionLabel})");
        builder.AppendLine($"Compatibility: Current save format supported");
        builder.AppendLine($"Save folder: {State.SaveFolder}");
        builder.AppendLine($"Current save file: {State.CurrentSavePath ?? "not saved yet"}");
        builder.AppendLine($"Last saved: {State.LastSavedText}");
        builder.AppendLine();
        builder.AppendLine("Preferences, accessibility options, cloud sync, and database settings are intentionally not implemented yet.");
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
            builder.AppendLine("  Coaching Philosophy:");
            builder.AppendLine($"    {State.StaffCoachingProfileText(selected.PersonId).Replace("\n", "\n    ", StringComparison.Ordinal)}");
            builder.AppendLine();
        }

        builder.AppendLine("Organization Chart");
        builder.AppendLine(State.OrganizationChartText());
        builder.AppendLine();
        builder.AppendLine("Department Grades");
        builder.AppendLine(State.DepartmentGradesText());
        builder.AppendLine();
        builder.AppendLine("Monthly Staff Meeting");
        builder.AppendLine(State.MonthlyStaffMeetingText());
        builder.AppendLine();

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
        builder.AppendLine("Living Staff Market");
        if (State.StaffMarketCandidates.Count == 0)
        {
            builder.AppendLine("  No staff market candidates are available yet.");
        }

        foreach (var market in State.StaffMarketCandidates)
        {
            var candidate = market.Candidate;
            builder.AppendLine($"{candidate.Person.Identity.DisplayName} - {candidate.StaffMember.CurrentRole}");
            builder.AppendLine($"  Market status: {market.Status}  Interest: {market.HiringInterest}/100  Reason: {market.ReasonAvailable}");
            builder.AppendLine($"  Role fit: {candidate.RoleFit}  Department fit: {candidate.DepartmentFit}  Reputation: {candidate.Reputation}");
            builder.AppendLine($"  Salary ask: {candidate.ExpectedSalary.AnnualAmount:C0}");
            builder.AppendLine($"  Current employer: {market.CurrentEmployer}");
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
        builder.AppendLine("Scouting v2 is intelligence, not hidden ratings: multiple scout reports, confidence, viewings, evidence, disagreements, and recommendations.");
        builder.AppendLine("Assignment controls: region assignment, player assignment, priority, notes, Assign button, Scout Again, Tournament, Compare Reports.");
        builder.AppendLine();
        builder.AppendLine("Regions / Focuses");
        builder.AppendLine("Western Canada, Eastern Canada, USA, Europe, Goalies, Defensemen, Forwards, Character, Medical");
        builder.AppendLine("Tournament options include WHL Cup, Memorial Cup, World Juniors, U18 Championships, Provincial Championships, and European Tournament coverage.");
        builder.AppendLine($"Budget support: {State.ScoutingBudgetText}");
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
            builder.AppendLine($"  Intelligence: {State.ScoutIntelligenceProfileText(profile.ScoutPersonId)}");
            builder.AppendLine($"  Career: {State.ScoutCareerText(profile.ScoutPersonId)}");
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
            builder.AppendLine($"  Report cards: {State.ScoutingReportsText(report.PlayerId)}");
            builder.AppendLine($"  Compare Reports: {State.ScoutingComparisonText(report.PlayerId)}");
            builder.AppendLine();
        }

        builder.AppendLine("Inbox Updates");
        builder.AppendLine("Completed assignments create useful Scouting inbox messages and Event Engine records; assigning a scout does not spam the inbox.");
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
        builder.AppendLine("Filters: All | Signings | Roster Moves | Injuries | Draft | Staff | Trades | Free Agency | Rumors");
        builder.AppendLine("League AI filters: Contender | Rebuilding | Buyer | Seller | Budget Pressure | Needs");
        builder.AppendLine("Other-team transactions appear here instead of crowding the GM inbox.");
        builder.AppendLine("Routine league churn stays quiet unless it affects your team, the standings, or a major roster story.");
        builder.AppendLine();
        builder.AppendLine("League Direction");
        if (State.LeagueIdentityNews.Count == 0)
        {
            builder.AppendLine("No major team identity headlines today.");
        }
        else
        {
            foreach (var news in State.LeagueIdentityNews)
            {
                builder.AppendLine($"  {news.Date:yyyy-MM-dd} | {news.TeamName} | {news.Description}");
            }
        }

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

    private string BuildWaiverWire()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Waiver Wire");
        builder.AppendLine("===========");
        builder.AppendLine(State.WaiverRuleSummary);
        builder.AppendLine();
        builder.AppendLine("Open Waivers");
        if (State.WaiverWire.OpenTransactions.Count == 0)
        {
            builder.AppendLine("  No players are currently on waivers.");
        }
        else
        {
            foreach (var transaction in State.WaiverWire.OpenTransactions)
            {
                builder.AppendLine($"  {transaction.PlayerName} | {transaction.Position} | age {transaction.Age?.ToString() ?? "unknown"} | {transaction.OriginTeamName}");
                builder.AppendLine($"    Reason: {transaction.Reason}");
                builder.AppendLine($"    Deadline: {transaction.ClaimDeadline:yyyy-MM-dd HH:mm} | Claims: {State.WaiverClaimCount(transaction.TransactionId)}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Waiver Priority");
        foreach (var priority in State.WaiverWire.Priority.Take(16))
        {
            builder.AppendLine($"  {priority.Rank}. {priority.TeamName}");
        }

        builder.AppendLine();
        builder.AppendLine("Recent Waiver / Assignment History");
        foreach (var entry in State.ScenarioSnapshot.WaiverHistory.Entries.OrderByDescending(entry => entry.Date).ThenBy(entry => entry.PlayerName, StringComparer.Ordinal).Take(16))
        {
            builder.AppendLine($"  {entry.Date:yyyy-MM-dd} | {entry.TeamName} | {entry.PlayerName} | {entry.Status}");
            builder.AppendLine($"    {entry.Summary}");
        }

        if (State.ScenarioSnapshot.WaiverHistory.Entries.Count == 0)
        {
            builder.AppendLine("  No waiver or affiliate assignment history yet.");
        }

        return builder.ToString();
    }

    private string BuildMediaNews()
    {
        var feed = State.MediaFeed;
        var builder = new StringBuilder();
        builder.AppendLine("Media / News");
        builder.AppendLine("============");
        builder.AppendLine("Media is the short article/story layer. League News remains the raw transaction/event wire.");
        builder.AppendLine("Filters available in engine: type, source, team, player, and importance.");
        builder.AppendLine();
        builder.AppendLine("Sources");
        foreach (var source in feed.Sources.OrderBy(source => source.Name, StringComparer.Ordinal))
        {
            builder.AppendLine($"  {source.Name} | Focus: {source.Focus} | Tone: {source.ToneBias} | Credibility: {source.Credibility}/100");
        }

        builder.AppendLine();
        if (feed.Articles.Count == 0)
        {
            builder.AppendLine("No media articles have been generated yet.");
            return builder.ToString();
        }

        builder.AppendLine("Top Headlines");
        foreach (var article in feed.Articles
            .Where(article => article.Importance >= MediaImportance.Major)
            .OrderByDescending(article => article.Importance)
            .ThenByDescending(article => article.Date)
            .Take(5))
        {
            AppendArticle(builder, article);
        }

        builder.AppendLine();
        foreach (var group in feed.Articles
            .GroupBy(article => article.ArticleType)
            .OrderBy(group => group.Key))
        {
            builder.AppendLine(group.Key.ToString());
            foreach (var article in group
                .OrderByDescending(article => article.Date)
                .ThenByDescending(article => article.Importance)
                .Take(8))
            {
                AppendArticle(builder, article);
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static void AppendArticle(StringBuilder builder, MediaArticle article)
    {
        builder.AppendLine($"  {article.Date:yyyy-MM-dd} | {article.Source.Name} | {article.Importance} | {article.Tone}");
        builder.AppendLine($"  {article.Headline}");
        builder.AppendLine($"    {article.ShortSummary}");
        if (article.ArticleType == MediaArticleType.Rumor)
        {
            builder.AppendLine($"    Rumor confidence: {article.RumorConfidence}");
        }

        if (article.TeamNames.Count > 0)
        {
            builder.AppendLine($"    Teams: {string.Join(", ", article.TeamNames)}");
        }

        if (article.PersonNames.Count > 0)
        {
            builder.AppendLine($"    People: {string.Join(", ", article.PersonNames)}");
        }
    }

    private static string Readable(Enum value)
    {
        var text = value.ToString();
        return string.Concat(text.Select((letter, index) => index > 0 && char.IsUpper(letter) ? $" {letter}" : letter.ToString()));
    }

    private string BuildAwardsReport()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Awards");
        builder.AppendLine("======");
        var awards = State.ScenarioSnapshot.AwardHistory.Awards
            .OrderByDescending(award => award.SeasonYear)
            .ThenBy(award => award.Category)
            .ThenBy(award => award.AwardType)
            .ToArray();
        if (awards.Length == 0)
        {
            builder.AppendLine("No awards have been handed out yet. Awards are generated at season end.");
            return builder.ToString();
        }

        foreach (var group in awards.GroupBy(award => award.SeasonYear).OrderByDescending(group => group.Key))
        {
            builder.AppendLine(group.Key.ToString());
            foreach (var award in group)
            {
                builder.AppendLine($"  {Readable(award.AwardType)} | {award.Category} | Winner: {award.Winner.RecipientName}");
                builder.AppendLine($"    Reasoning: {award.Reasoning}");
                if (award.Finalists.Count > 1)
                {
                    builder.AppendLine($"    Finalists: {string.Join(", ", award.Finalists.Select(finalist => finalist.RecipientName).Distinct(StringComparer.Ordinal).Take(4))}");
                }
            }
        }

        return builder.ToString();
    }

    private string BuildRecordBookReport()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Record Book");
        builder.AppendLine("===========");
        var records = State.ScenarioSnapshot.RecordBook.Records
            .OrderBy(record => record.Scope)
            .ThenBy(record => record.RecordType)
            .ToArray();
        if (records.Length == 0)
        {
            builder.AppendLine("No records are tracked yet. Records are established or broken from season, career, team, and playoff stats.");
            return builder.ToString();
        }

        foreach (var record in records)
        {
            builder.AppendLine($"{Readable(record.Scope)} | {Readable(record.RecordType)} | {record.Value}");
            builder.AppendLine($"  Holder: {record.HolderName} ({record.HolderKind}) | Set: {record.DateSet:yyyy-MM-dd}");
            builder.AppendLine($"  {record.Summary}");
        }

        return builder.ToString();
    }

    private string BuildTeamRecordsReport()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Team Records");
        builder.AppendLine("============");
        var records = State.ScenarioSnapshot.RecordBook.ForOrganization(State.ScenarioSnapshot.Organization.OrganizationId)
            .OrderBy(record => record.Scope)
            .ThenBy(record => record.RecordType)
            .ToArray();
        if (records.Length == 0)
        {
            builder.AppendLine("No team records are tracked yet.");
            return builder.ToString();
        }

        foreach (var record in records)
        {
            builder.AppendLine($"{Readable(record.RecordType)}: {record.Value} - {record.HolderName}");
            builder.AppendLine($"  {record.Summary}");
        }

        return builder.ToString();
    }

    private string BuildLeagueRecordsReport()
    {
        var builder = new StringBuilder();
        builder.AppendLine("League Records");
        builder.AppendLine("==============");
        var records = State.ScenarioSnapshot.RecordBook.Records
            .Where(record => record.Scope is RecordScope.League or RecordScope.SingleSeason or RecordScope.Playoff or RecordScope.Career)
            .OrderBy(record => record.Scope)
            .ThenBy(record => record.RecordType)
            .ToArray();
        if (records.Length == 0)
        {
            builder.AppendLine("No league records are tracked yet.");
            return builder.ToString();
        }

        foreach (var record in records)
        {
            builder.AppendLine($"{Readable(record.Scope)} {Readable(record.RecordType)}: {record.Value} - {record.HolderName}");
        }

        return builder.ToString();
    }

    private string BuildLeagueOverview()
    {
        var profile = State.ScenarioSnapshot.LeagueProfile;
        var team = State.ScenarioSnapshot.TeamSelection;
        var builder = new StringBuilder();
        builder.AppendLine("League Overview");
        builder.AppendLine("===============");
        builder.AppendLine($"{profile.Identity.Name} ({profile.Identity.ShortName})");
        builder.AppendLine(profile.Identity.Description);
        builder.AppendLine();
        builder.AppendLine($"Difficulty: {profile.Identity.Difficulty}");
        builder.AppendLine($"Primary focus: {string.Join(", ", profile.Identity.PrimaryGameplayFocus)}");
        builder.AppendLine($"Current champion: {profile.Identity.CurrentChampion}");
        builder.AppendLine($"History: {profile.Identity.HistorySummary}");
        builder.AppendLine();
        builder.AppendLine("Your Team");
        var brand = CurrentTeamBranding();
        var leagueBrand = CurrentLeagueBranding();
        builder.AppendLine($"{brand.TeamAbbreviation} {team.TeamName} | Monogram {brand.Monogram.Letters} | Crest {brand.LogoPlaceholder}");
        builder.AppendLine($"League identity: {leagueBrand.VisualStyleDescriptor} | Header: {leagueBrand.HeaderTreatment}");
        builder.AppendLine($"Team visual identity: {brand.VisualStyleDescriptor} | {brand.BannerStyle} | {brand.JerseyStripePattern}");
        builder.AppendLine($"Previous record: {team.PreviousRecord}");
        builder.AppendLine($"Owner expectations: {team.OwnerExpectations}");
        builder.AppendLine($"Budget: {team.Budget:C0}");
        builder.AppendLine($"Current GM reputation: {team.CurrentGmReputation}/100");
        builder.AppendLine($"Prospect strength: {team.ProspectStrength}");
        builder.AppendLine($"Difficulty: {team.Difficulty}");
        builder.AppendLine($"Roster quality: {team.RosterQuality}");
        if (!string.IsNullOrWhiteSpace(team.ParentOrganizationId))
        {
            builder.AppendLine($"Parent organization: {team.ParentOrganizationId}");
        }

        if (!string.IsNullOrWhiteSpace(team.AffiliateOrganizationId))
        {
            builder.AppendLine($"Affiliate organization: {team.AffiliateOrganizationId}");
        }

        return builder.ToString();
    }

    private string BuildPositionMarket()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Position Market");
        builder.AppendLine("===============");
        builder.AppendLine(State.PositionMarketText());
        builder.AppendLine();
        builder.AppendLine("Impact");
        builder.AppendLine("- Trade Builder: scarce positions increase asset demand and value explanations.");
        builder.AppendLine("- Draft War Room: positional scarcity is included in team-needs context.");
        builder.AppendLine("- Free Agency: market supply changes demand notes and contract pressure.");
        builder.AppendLine("- Scouting: thin positions should receive higher scouting priority.");
        return builder.ToString();
    }

    private string BuildLeagueRules()
    {
        var profile = State.ScenarioSnapshot.LeagueProfile;
        var rulebook = profile.Rulebook;
        var builder = new StringBuilder();
        builder.AppendLine("League Rules");
        builder.AppendLine("============");
        builder.AppendLine($"Rulebook: {rulebook.RulebookId} ({rulebook.LeagueType})");
        builder.AppendLine($"Roster: {RuleList(profile.RosterRulesSummary)}");
        builder.AppendLine($"Draft: {RuleList(profile.DraftRulesSummary)}");
        builder.AppendLine($"Trades: {RuleList(profile.TradeRulesSummary)}");
        builder.AppendLine($"Contracts: {RuleList(profile.ContractRulesSummary)}");
        builder.AppendLine($"Budget: {RuleList(profile.BudgetRulesSummary)}");
        builder.AppendLine($"Development: {RuleList(profile.DevelopmentRulesSummary)}");
        builder.AppendLine($"Affiliate: {RuleList(profile.AffiliateRulesSummary)}");
        builder.AppendLine($"Recruiting: {RuleList(profile.RecruitingRulesSummary)}");
        builder.AppendLine();
        builder.AppendLine($"Draft enabled: {rulebook.DraftRules?.DraftEnabled}");
        builder.AppendLine($"Draft rounds: {rulebook.DraftRules?.Rounds ?? 0}");
        builder.AppendLine($"Roster target: {rulebook.RosterRules?.ActiveRoster ?? 0}");
        builder.AppendLine($"Max roster: {rulebook.RosterRules?.MaxRoster ?? 0}");
        builder.AppendLine($"Current champion: {profile.Identity.CurrentChampion}");
        return builder.ToString();
    }

    private string BuildLeagueTeams()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Teams");
        builder.AppendLine("=====");
        foreach (var team in State.ScenarioSnapshot.LeagueProfile.Teams)
        {
            builder.AppendLine($"{team.TeamName} ({team.City}, {team.Region})");
            builder.AppendLine($"  Previous record: {team.PreviousRecord}");
            builder.AppendLine($"  Owner: {team.OwnerExpectations}");
            builder.AppendLine($"  Budget: {team.Budget:C0} | Prospects: {team.ProspectStrength} | Roster: {team.RosterQuality} | Difficulty: {team.Difficulty}");
            if (!string.IsNullOrWhiteSpace(team.ParentOrganizationId))
            {
                builder.AppendLine($"  Parent: {team.ParentOrganizationId}");
            }

            if (!string.IsNullOrWhiteSpace(team.AffiliateOrganizationId))
            {
                builder.AppendLine($"  Affiliate: {team.AffiliateOrganizationId}");
            }
        }

        return builder.ToString();
    }

    private static string RuleList(IReadOnlyList<string> rules) =>
        string.Join("; ", rules);

    private string BuildJournal()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Journal");
        builder.AppendLine("=======");
        builder.AppendLine("Routine simulation updates live here for history and search without interrupting the GM inbox.");
        builder.AppendLine();
        if (State.JournalEntries.Count == 0)
        {
            builder.AppendLine("No routine journal entries yet.");
            return builder.ToString();
        }

        foreach (var group in State.JournalEntries.GroupBy(entry => entry.Category).OrderBy(group => group.Key))
        {
            builder.AppendLine(group.Key.ToString());
            foreach (var entry in group.Take(25))
            {
                builder.AppendLine($"  {entry.Date:yyyy-MM-dd HH:mm} | {entry.Title}");
                builder.AppendLine($"    {entry.Summary}");
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private string BuildGlobalSearch()
    {
        var query = _globalSearchInput?.Text?.Trim() ?? string.Empty;
        var builder = new StringBuilder();
        builder.AppendLine("Global Search");
        builder.AppendLine("=============");
        builder.AppendLine("Search players, staff, prospects, recruits, free agents, messages, league news, and history.");
        builder.AppendLine();
        if (query.Length < 2)
        {
            builder.AppendLine("Type at least two characters in the top search box.");
            return builder.ToString();
        }

        var results = State.Search(query);
        builder.AppendLine($"Query: {query}");
        builder.AppendLine($"Results: {results.Count}");
        builder.AppendLine();
        if (results.Count == 0)
        {
            builder.AppendLine("No matching career items found.");
            return builder.ToString();
        }

        foreach (var result in results)
        {
            builder.AppendLine($"{result.ResultType}: {result.Title}");
            builder.AppendLine($"  {result.Subtitle}");
            builder.AppendLine($"  Open: {result.TargetWorkspace}");
            builder.AppendLine($"  {result.Summary}");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private string BuildPlaytestChecklist()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Playtest Checklist");
        builder.AppendLine("==================");
        builder.AppendLine("Quick regression pass for first-month GM flow. This is a checklist, not a new gameplay system.");
        builder.AppendLine();
        builder.AppendLine("Alpha 8.4 UX Pass");
        builder.AppendLine("-----------------");
        foreach (var line in Alpha84PlaytestChecklistLines())
        {
            builder.AppendLine(line);
        }

        builder.AppendLine();
        builder.AppendLine("Local UX counters");
        builder.AppendLine("-----------------");
        if (_localUxCounters.Count == 0)
        {
            builder.AppendLine("No local developer-only UX counters recorded this session.");
        }
        else
        {
            foreach (var counter in _localUxCounters.OrderBy(item => item.Key, StringComparer.Ordinal).Take(30))
            {
                builder.AppendLine($"{counter.Key}: {counter.Value}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Existing checklist");
        builder.AppendLine("------------------");
        foreach (var item in State.PlaytestChecklist)
        {
            builder.AppendLine($"{(item.IsPassing ? "PASS" : "CHECK")} | {item.Area}");
            builder.AppendLine($"  Question: {item.Question}");
            builder.AppendLine($"  Expected: {item.ExpectedOutcome}");
            builder.AppendLine($"  Notes: {item.Notes}");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static IReadOnlyList<string> Alpha84PlaytestChecklistLines() =>
        new[]
        {
            "Not Tested | New Career | Choose league/team, create GM, and verify the selected team preview is readable.",
            "Not Tested | Dashboard | Confirm Action Center, inbox, save, advance, next game, and roster warnings are understandable.",
            "Not Tested | Action Center | Use Go To, Resolve, Defer, Dismiss, Reset Filters, and back navigation.",
            "Not Tested | Roster | Select player, open person card, view contract/health, reset filters, and verify no blank panel.",
            "Not Tested | Lineup | Select slot/player, change usage, and confirm routine choices do not ask for destructive confirmation.",
            "Not Tested | Development | Set a focus and confirm success feedback appears.",
            "Not Tested | Contracts | Review rights/arbitration/buyout actions and confirm destructive actions explain consequences.",
            "Not Tested | Scouting | Assign scout, close with Escape, verify focus returns and report workflow remains clear.",
            "Not Tested | Free Agency | Offer/sign workflow shows why action is blocked or completed.",
            "Not Tested | Trades | Build offer, remove asset, clear proposal, apply counter, and preserve proposal after profile view.",
            "Not Tested | Draft | Switch boards, compare prospects, draft player, and verify selected prospect context remains.",
            "Not Tested | Season | Review schedule, standings, result recap, and branded team names.",
            "Not Tested | Save / Load | Ctrl+S save, Load Career, and Settings save metadata card.",
            "Fixed | Navigation | Breadcrumbs, bounded Back/Forward history, and current-section selection preservation added.",
            "Fixed | Accessibility | Buttons are focusable, labels target controls, Escape closes safe popups, Ctrl+F/Ctrl+S supported.",
            "Fixed | Feedback | Action status banner added; reset filters and empty states give player-facing text.",
            "Retest Needed | Resolution | Minimum supported desktop remains 920x620; manually review 1366x768 and 1920x1080."
        };

    private static string PendingActionConsequence(PendingGmAction action) =>
        action.ActionType switch
        {
            PendingGmActionType.SignRecruit or PendingGmActionType.SignDraftPick or PendingGmActionType.SignFreeAgent => "Approve signing or the player remains unsigned.",
            PendingGmActionType.AddToRoster => "Resolve roster issue before the next game.",
            PendingGmActionType.ReleasePlayer or PendingGmActionType.CutPlayer => "Declining keeps the player in the current roster/camp state.",
            PendingGmActionType.AssignToAffiliate or PendingGmActionType.ReturnToParent => "Declining keeps the player in your current decision queue.",
            PendingGmActionType.ApproveContract => "Approve contract or negotiation remains unresolved.",
            PendingGmActionType.DeclineContract => "Decline contract only if you are ready to move on.",
            PendingGmActionType.ApproveTrade => "Approve trade or the accepted framework remains unresolved.",
            PendingGmActionType.DeclineTrade => "Decline trade to leave rosters and rights unchanged.",
            _ => "No automatic change will happen without GM approval."
        };

    private string BuildDraftWarRoom()
    {
        var board = State.DraftWarRoom;
        var builder = new StringBuilder();
        builder.AppendLine("Draft War Room");
        builder.AppendLine("==============");
        builder.AppendLine(State.DraftWarRoomSummaryText);
        builder.AppendLine();

        builder.AppendLine("Draft Class Summary");
        builder.AppendLine($"  Theme: {State.DraftClassThemeText}");
        builder.AppendLine($"  Overview: {State.DraftClassSummaryText}");
        builder.AppendLine($"  Strengths: {State.DraftClassStrengthText}");
        builder.AppendLine($"  Weaknesses: {State.DraftClassWeaknessText}");
        builder.AppendLine($"  Position depth: {State.DraftClassPositionDepthText}");
        builder.AppendLine($"  Regional mix: {State.DraftClassRegionalText}");
        builder.AppendLine($"  Scout quote: {State.DraftClassScoutQuoteText}");
        builder.AppendLine($"  Board realism: {State.DraftBoardRealismText}");
        builder.AppendLine($"  Position value: {State.DraftPositionValueText}");
        builder.AppendLine();

        builder.AppendLine("War Room Views");
        builder.AppendLine("  My Board | Scout Board | Consensus Board | Watch List | Team Needs | Picks | Compare Prospects | Draft Class Summary | Hidden Gems / Avoid List");
        builder.AppendLine();

        builder.AppendLine("Scout Board");
        builder.AppendLine(State.DraftBoardViewText(DraftWarRoomViewType.ScoutBoard));
        builder.AppendLine();

        builder.AppendLine("Consensus Board");
        builder.AppendLine(State.DraftBoardViewText(DraftWarRoomViewType.ConsensusBoard));
        builder.AppendLine();

        builder.AppendLine("Hidden Gems / Avoid List");
        builder.AppendLine(State.DraftBoardViewText(DraftWarRoomViewType.HiddenGemsAvoidList));
        builder.AppendLine();

        builder.AppendLine("My Draft Board");
        foreach (var entry in board.BoardEntries.Where(entry => !entry.IsRemoved).OrderBy(entry => entry.PersonalRank).Take(30))
        {
            var boardEntry = State.Snapshot.DraftBoard.Entries.FirstOrDefault(item => item.ProspectPersonId == entry.ProspectPersonId);
            var quick = boardEntry is null ? State.RegionTeamText(entry.ProspectPersonId) : State.DraftQuickScan(boardEntry);
            var card = State.DraftIntelligenceCard(entry.ProspectPersonId);
            var tags = entry.Tags.Count == 0 ? "watching" : string.Join(", ", entry.Tags);
            builder.AppendLine($"  #{entry.PersonalRank} {entry.ProspectName} | {quick} | {card.RatingDisplay} | Consensus #{card.ConsensusBoardRank} | Fit {card.TeamFitScore}/100 | tags: {tags}");
            builder.AppendLine($"     Draft value context: {State.DraftValueContext(boardEntry)}");
            builder.AppendLine($"     Group: {entry.GroupName} | Notes: {(string.IsNullOrWhiteSpace(entry.GmNotes) ? "none" : entry.GmNotes)}");
        }
        builder.AppendLine();

        builder.AppendLine("Watch List");
        var watched = board.BoardEntries
            .Where(entry => entry.Tags.Count > 0 && !entry.IsRemoved)
            .OrderBy(entry => entry.PersonalRank)
            .Take(20)
            .ToArray();
        if (watched.Length == 0)
        {
            builder.AppendLine("  No tagged prospects yet. Use Priority, Sleeper, Avoid, Favorite, or Pin from the Scouting/Draft Board detail panel.");
        }
        foreach (var entry in watched)
        {
            builder.AppendLine($"  {entry.ProspectName}: {string.Join(", ", entry.Tags)}");
        }
        builder.AppendLine();

        builder.AppendLine("Needs Analysis");
        foreach (var need in board.Needs)
        {
            builder.AppendLine($"  {need.Priority}: {need.Label} - {need.Reason}");
        }
        builder.AppendLine();

        builder.AppendLine("Best Player Available Opinions");
        foreach (var opinion in board.BestPlayerAvailableOpinions)
        {
            builder.AppendLine($"  {opinion.Department}: {opinion.ProspectName} ({opinion.Confidence}) - {opinion.Opinion}");
        }
        builder.AppendLine();

        builder.AppendLine("Scout Consensus Snapshot");
        foreach (var entry in board.BoardEntries.Where(entry => !entry.IsRemoved).OrderBy(entry => entry.PersonalRank).Take(5))
        {
            var consensus = State.DraftConsensus(entry.ProspectPersonId);
            builder.AppendLine($"  {entry.ProspectName}: {consensus.Level} ({consensus.AgreementScore}/100) - {consensus.Summary}");
        }
        builder.AppendLine();

        builder.AppendLine("Prospect Compare");
        builder.AppendLine(State.DraftComparisonText(board.BoardEntries.Where(entry => !entry.IsRemoved).OrderBy(entry => entry.PersonalRank).Take(4).Select(entry => entry.ProspectPersonId).ToArray()));
        builder.AppendLine();

        builder.AppendLine("Attribute Scouting Snapshot");
        foreach (var entry in board.BoardEntries.Where(entry => !entry.IsRemoved).OrderBy(entry => entry.PersonalRank).Take(5))
        {
            builder.AppendLine($"  {entry.ProspectName}");
            foreach (var line in State.DraftAttributeLinesText(entry.ProspectPersonId, 5).Split(Environment.NewLine))
            {
                builder.AppendLine($"     {line}");
            }
        }
        builder.AppendLine();

        builder.AppendLine("Draft Storylines");
        foreach (var storyline in board.Storylines)
        {
            builder.AppendLine($"  {storyline.Headline}: {storyline.Summary}");
        }
        builder.AppendLine();

        builder.AppendLine("Team Draft History");
        var history = State.ScenarioSnapshot.DraftHistory
            .OrderByDescending(record => record.SeasonYear)
            .ThenBy(record => record.Round)
            .ThenBy(record => record.Pick)
            .Take(8)
            .ToArray();
        if (history.Length == 0)
        {
            builder.AppendLine("  No completed tracked drafts yet.");
        }
        foreach (var record in history)
        {
            builder.AppendLine($"  {record.SeasonYear} R{record.Round} #{record.Pick}: {record.ProspectName} - {record.OutcomeSummary}");
        }
        builder.AppendLine();

        builder.AppendLine("Draft Picks / Rights");
        if (State.ScenarioSnapshot.DraftRights.Count == 0)
        {
            builder.AppendLine("  No current player-team selections yet.");
        }
        foreach (var right in State.ScenarioSnapshot.DraftRights)
        {
            builder.AppendLine($"  R{right.RoundNumber} #{right.PickNumber}: {right.ProspectName} ({right.OrganizationName})");
        }
        builder.AppendLine();

        builder.AppendLine("Original Board Snapshot");
        foreach (var snapshot in board.OriginalBoardSnapshot.Take(12))
        {
            builder.AppendLine($"  #{snapshot.Rank} {snapshot.ProspectName} | {State.PositionShortText(snapshot.Position)} | {snapshot.Confidence?.ToString() ?? "Unknown"} | {snapshot.Projection}");
        }
        builder.AppendLine();

        if (board.PostDraftReview is not null)
        {
            builder.AppendLine("Post-Draft Review");
            builder.AppendLine($"  Head scout: {board.PostDraftReview.HeadScoutReview}");
            builder.AppendLine($"  Owner: {board.PostDraftReview.OwnerReview}");
            builder.AppendLine($"  Coach: {board.PostDraftReview.CoachReview}");
            builder.AppendLine($"  League grade: {board.PostDraftReview.LeagueGrade}");
            foreach (var impact in board.PostDraftReview.PlayerImpactSummaries)
            {
                builder.AppendLine($"  Impact: {impact}");
            }
        }
        else
        {
            builder.AppendLine("Post-Draft Review");
            builder.AppendLine("  Available after the live draft completes.");
        }

        return builder.ToString();
    }

    private string BuildDraftBoard()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Draft Board");
        builder.AppendLine("===========");
        builder.AppendLine("Draft Class Summary");
        builder.AppendLine($"  Theme: {State.DraftClassThemeText}");
        builder.AppendLine($"  Overview: {State.DraftClassSummaryText}");
        builder.AppendLine($"  Strengths: {State.DraftClassStrengthText}");
        builder.AppendLine($"  Weaknesses: {State.DraftClassWeaknessText}");
        builder.AppendLine($"  Positional depth: {State.DraftClassPositionDepthText}");
        builder.AppendLine($"  Regional mix: {State.DraftClassRegionalText}");
        builder.AppendLine($"  Scout quote: {State.DraftClassScoutQuoteText}");
        builder.AppendLine($"  Players to watch: {State.DraftClassPlayersToWatchText}");
        builder.AppendLine($"  Board realism: {State.DraftBoardRealismText}");
        builder.AppendLine($"  Position value: {State.DraftPositionValueText}");
        builder.AppendLine("  Filters placeholder: position, region, current league, handedness, projection, confidence, risk, class fit.");
        builder.AppendLine();
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
            var card = State.DraftIntelligenceCard(entry.ProspectPersonId);
            builder.AppendLine($"#{entry.Rank} {(entry.IsStarred ? "[STAR] " : string.Empty)}{State.DraftIntelligenceRowText(entry)}");
            builder.AppendLine($"  Report: {entry.ScoutingReportId ?? "none"}");
            if (entry.Bio is not null)
            {
                builder.AppendLine($"  Bio: {State.PersonPosition(entry.ProspectPersonId)} | {entry.Bio.ShootsCatches} | {entry.Bio.HeightDisplay}, {entry.Bio.WeightDisplay} | age {State.PersonAge(entry.ProspectPersonId)?.ToString() ?? "unknown"} | born {entry.Bio.BirthYear}");
                builder.AppendLine($"  Hometown: {entry.Bio.Hometown}, {entry.Bio.ProvinceState}, {entry.Bio.Country} | Team: {entry.Bio.CurrentTeam} ({entry.Bio.League})");
                builder.AppendLine($"  Character: {entry.Bio.CharacterSummary}");
                builder.AppendLine($"  Lineup projection: {entry.Bio.PotentialLineupProjection}");
            }

            builder.AppendLine($"  Projection: {entry.ProjectionText}");
            builder.AppendLine($"  War Room: my #{card.MyBoardRank}, scout #{card.ScoutBoardRank}, consensus #{card.ConsensusBoardRank}; team fit {card.TeamFitScore}/100");
            builder.AppendLine($"  Draft value context: {State.DraftValueContext(entry)}");
            builder.AppendLine($"  Attributes: {string.Join("; ", card.Attributes.OrderBy(attribute => attribute.Estimate.IsUnknown).ThenByDescending(attribute => attribute.Estimate.Midpoint).Take(5).Select(attribute => attribute.DisplayText))}");
            builder.AppendLine($"  Class context: {State.DraftClassContext(entry)}");
            builder.AppendLine($"  Risk: {State.DraftRiskText(entry)}");
            builder.AppendLine($"  Intelligence alerts: {(card.Alerts.Count == 0 ? "none" : string.Join("; ", card.Alerts.Select(alert => $"{alert.AlertType}: {alert.Summary}")))}");
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
        builder.AppendLine("Filters: AHL eligible | Junior return candidates | Unsigned rights | Slide eligible | NHL-ready");
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
            builder.AppendLine($"  Current team/level: {(string.IsNullOrWhiteSpace(prospect.CurrentTeam) ? "unknown" : prospect.CurrentTeam)} / {State.PipelineDevelopmentLevelText(prospect.ProspectPersonId)}");
            builder.AppendLine($"  Signed: {State.PipelineSignedText(prospect.ProspectPersonId)}");
            builder.AppendLine($"  AHL eligible: {State.PipelineAhlEligibleText(prospect.ProspectPersonId)}");
            builder.AppendLine($"  Junior eligible: {State.PipelineJuniorEligibleText(prospect.ProspectPersonId)}");
            builder.AppendLine($"  Contract slide: {State.PipelineSlideText(prospect.ProspectPersonId)}");
            builder.AppendLine($"  Recommended assignment: {State.PipelineRecommendationText(prospect.ProspectPersonId)}");
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
        builder.AppendLine("Chemistry Summary");
        foreach (var line in State.RelationshipChemistry.SummaryLines)
        {
            builder.AppendLine($"- {line}");
        }

        builder.AppendLine();
        builder.AppendLine("Expanded Relationship Profiles");
        foreach (var profile in State.RelationshipProfiles
            .OrderBy(profile => profile.RelationshipType)
            .ThenBy(profile => profile.TargetName, StringComparer.Ordinal)
            .Take(60))
        {
            builder.AppendLine($"{profile.RelationshipType}: {profile.SourceName} -> {profile.TargetName}");
            builder.AppendLine($"  {profile.Label} | Trust {profile.Trust}, Respect {profile.Respect}, Loyalty {profile.Loyalty}, Conflict {profile.Conflict}, Communication {profile.CommunicationQuality}, Trend {profile.Trend}");
            builder.AppendLine($"  {profile.Summary}");
            foreach (var moment in profile.KeyMoments.TakeLast(2))
            {
                builder.AppendLine($"  Moment: {moment}");
            }

            builder.AppendLine();
        }

        builder.AppendLine("Active Conflicts");
        foreach (var conflict in State.RelationshipConflicts.Where(conflict => conflict.IsActive).Take(12))
        {
            var profile = State.RelationshipProfiles.FirstOrDefault(profile => profile.RelationshipProfileId == conflict.RelationshipProfileId);
            builder.AppendLine($"{conflict.ConflictType} | Severity {conflict.Severity}/100 | {(conflict.IsMajor ? "Major" : "Minor")} | {profile?.TargetName ?? "Unknown"}");
            builder.AppendLine($"  {conflict.VisibleExplanation}");
        }

        builder.AppendLine();
        builder.AppendLine("Legacy Directional Links");
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
            builder.AppendLine($"  Top line: {recap.TopLineSummary}");
            builder.AppendLine($"  Special teams: {recap.SpecialTeamsNote}");
            builder.AppendLine($"  Tactics: {recap.TacticalNote}");
            builder.AppendLine($"  Chemistry: {recap.ChemistryNote}");
            builder.AppendLine($"  Goalie usage: {recap.GoalieUsageNote}");
            builder.AppendLine($"  Key concern: {recap.KeyConcern}");
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
            builder.AppendLine($"{rank,2}{marker} {TeamName(team.OrganizationId),-28} {team.GamesPlayed,2} {team.Wins,3} {team.Losses,3} {team.OvertimeLosses,3} {team.Points,4} {team.GoalsFor,4} {team.GoalsAgainst,4} {diff,4}");
            rank++;
        }
        builder.AppendLine();
        builder.AppendLine("* Your team");

        return builder.ToString();
    }

    private string BuildPlayoffs()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Playoffs");
        builder.AppendLine("========");
        var state = State.ScenarioSnapshot.Playoffs;
        var bracket = state.Bracket;
        if (bracket is null)
        {
            builder.AppendLine("No playoff bracket has been generated yet.");
            builder.AppendLine("Once the regular-season schedule is complete, the daily loop seeds the bracket from the standings.");
            return builder.ToString();
        }

        builder.AppendLine($"Status: {bracket.Status}");
        builder.AppendLine($"Format: {bracket.Format.FormatType} | {bracket.Seeds.Count} team(s) | best-of-{bracket.Format.BestOf}");
        if (!string.IsNullOrWhiteSpace(bracket.ChampionTeamName))
        {
            builder.AppendLine($"Champion: {bracket.ChampionTeamName}");
            builder.AppendLine($"Runner-up: {bracket.RunnerUpTeamName ?? "not recorded"}");
            builder.AppendLine($"Playoff MVP: {bracket.PlayoffMvpPlaceholder ?? "pending"}");
        }

        builder.AppendLine();
        builder.AppendLine("Seeds");
        foreach (var seed in bracket.Seeds.OrderBy(seed => seed.Seed))
        {
            var marker = seed.OrganizationId == State.ScenarioSnapshot.Organization.OrganizationId ? "*" : " ";
            builder.AppendLine($"{marker}#{seed.Seed} {seed.TeamName} - {seed.RegularSeasonPoints} pts, {seed.Wins} win(s)");
        }

        if (bracket.MissedPlayoffs.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Missed Playoffs");
            foreach (var seed in bracket.MissedPlayoffs.OrderBy(seed => seed.Seed))
            {
                builder.AppendLine($"  #{seed.Seed} {seed.TeamName}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Bracket");
        foreach (var round in bracket.Rounds.OrderBy(round => round.RoundNumber))
        {
            builder.AppendLine($"{round.Name} - {round.Status}");
            foreach (var series in round.Series.OrderBy(series => series.SeriesNumber))
            {
                builder.AppendLine($"  {series.HigherSeed.TeamName} vs {series.LowerSeed.TeamName} | {series.HigherSeedWins}-{series.LowerSeedWins} | {series.Status}");
                foreach (var game in series.GamesOrEmpty.OrderBy(game => game.GameNumber))
                {
                    var score = game.Result is null ? "scheduled" : $"{game.Result.HomeGoals}-{game.Result.AwayGoals}";
                    builder.AppendLine($"    Game {game.GameNumber}: {game.Date:yyyy-MM-dd} | {TeamName(game.HomeOrganizationId)} vs {TeamName(game.AwayOrganizationId)} | {score}");
                }
            }
        }

        builder.AppendLine();
        builder.AppendLine("Upcoming / Current");
        var current = bracket.CurrentSeries;
        if (current is null)
        {
            builder.AppendLine(bracket.Status == PlayoffStatus.Completed ? "Playoffs complete." : "No active series.");
        }
        else
        {
            var nextGame = current.GamesOrEmpty.Count + 1;
            builder.AppendLine($"{current.HigherSeed.TeamName} vs {current.LowerSeed.TeamName}");
            builder.AppendLine($"Series score: {current.HigherSeed.TeamName} {current.HigherSeedWins}, {current.LowerSeed.TeamName} {current.LowerSeedWins}");
            builder.AppendLine($"Next playoff game: Game {nextGame}");
        }

        builder.AppendLine();
        builder.AppendLine("Recent Playoff Recaps");
        foreach (var recap in state.PlayoffGameRecaps.OrderByDescending(recap => recap.Date).ThenByDescending(recap => recap.GameId, StringComparer.Ordinal).Take(8))
        {
            builder.AppendLine($"{recap.Date:yyyy-MM-dd} | {recap.BoxScore.FinalScore}");
            builder.AppendLine($"  {recap.NarrativeSummary}");
            builder.AppendLine($"  Three stars: {string.Join("; ", recap.ThreeStars)}");
        }

        builder.AppendLine();
        builder.AppendLine("Playoff Stat Leaders");
        foreach (var stat in state.PlayoffSkaterStats.OrderByDescending(stat => stat.Points).ThenByDescending(stat => stat.Goals).Take(8))
        {
            builder.AppendLine($"  {stat.PlayerName}: {stat.Goals}-{stat.Assists}-{stat.Points} in {stat.GamesPlayed} GP");
        }

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

    private string BuildSeasonArchive()
    {
        var rollover = State.ScenarioSnapshot.SeasonRollover;
        var builder = new StringBuilder();
        builder.AppendLine("Season Archive / Offseason");
        builder.AppendLine("==========================");
        builder.AppendLine($"Current season: {State.ScenarioSnapshot.Season.Year}");
        builder.AppendLine($"Current phase: {State.ScenarioSnapshot.Season.CurrentPhase}");
        builder.AppendLine($"Schedule complete: {(State.CanCompleteSeason ? "Yes" : "No")}");
        builder.AppendLine($"Archived seasons: {rollover.SeasonArchives.Count}");
        builder.AppendLine();

        if (rollover.LastTransition is not null)
        {
            builder.AppendLine("Last Season Transition");
            builder.AppendLine($"  From: {rollover.LastTransition.FromSeasonYear} ({rollover.LastTransition.FromSeasonId})");
            builder.AppendLine($"  To: {rollover.LastTransition.ToSeasonYear} ({rollover.LastTransition.ToSeasonId})");
            builder.AppendLine($"  Transition date: {rollover.LastTransition.TransitionDate:yyyy-MM-dd}");
            builder.AppendLine($"  Next draft date: {rollover.LastTransition.NextDraftDate:yyyy-MM-dd}");
            builder.AppendLine($"  Summary: {rollover.LastTransition.Summary}");
            builder.AppendLine();
        }

        builder.AppendLine("Offseason Checklist");
        if (rollover.Checklist.Count == 0)
        {
            builder.AppendLine("  No rollover checklist yet. Finish the completed season to enter the offseason.");
        }
        else
        {
            foreach (var item in rollover.Checklist)
            {
                builder.AppendLine($"  [ ] {item}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Contract Decisions");
        if (rollover.ExpiringContracts.Count == 0)
        {
            builder.AppendLine("  No rollover contract decisions recorded.");
        }
        else
        {
            foreach (var personId in rollover.ExpiringContracts)
            {
                builder.AppendLine($"  {FindPersonName(personId)}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Next Draft Class");
        builder.AppendLine(string.IsNullOrWhiteSpace(rollover.DraftClassSummary) ? "  Not generated yet." : $"  {rollover.DraftClassSummary}");
        builder.AppendLine($"  Current draft board entries: {State.Snapshot.DraftBoard.Entries.Count}");
        builder.AppendLine();

        builder.AppendLine("Archived Seasons");
        if (rollover.SeasonArchives.Count == 0)
        {
            builder.AppendLine("  No seasons archived yet.");
            return builder.ToString();
        }

        foreach (var archive in rollover.SeasonArchives.OrderByDescending(archive => archive.SeasonYear))
        {
            var standing = archive.PlayerTeamStanding;
            builder.AppendLine($"{archive.SeasonYear} - {archive.OrganizationName}");
            builder.AppendLine($"  Completed: {archive.CompletedOn:yyyy-MM-dd}");
            builder.AppendLine($"  Record: {(standing is null ? "not available" : $"{standing.Wins}-{standing.Losses}-{standing.OvertimeLosses}, {standing.Points} pts")}");
            builder.AppendLine($"  Champion: {archive.ChampionTeamName}");
            builder.AppendLine($"  Games archived: {archive.GameResults.Count}");
            builder.AppendLine($"  Player stat lines: {archive.PlayerStats.Count}");
            builder.AppendLine($"  Goalie stat lines: {archive.GoalieStats.Count}");
            builder.AppendLine($"  Summary: {archive.Summary}");
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
        var standingsName = State.ScenarioSnapshot.Standings?.Teams
            .FirstOrDefault(team => string.Equals(team.OrganizationId, organizationId, StringComparison.Ordinal))
            ?.TeamName;
        if (!string.IsNullOrWhiteSpace(standingsName))
        {
            return BrandedTeamLabel(organizationId, standingsName);
        }

        var leagueTeam = SeasonFrameworkService.LeagueTeams(State.ScenarioSnapshot)
            .FirstOrDefault(team => string.Equals(team.OrganizationId, organizationId, StringComparison.Ordinal));
        return BrandedTeamLabel(organizationId, string.IsNullOrWhiteSpace(leagueTeam.TeamName) ? organizationId : leagueTeam.TeamName);
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

internal sealed record TradeAssetRow(TradeAsset Asset, string Label)
{
    public override string ToString() => Label;
}

internal sealed record TradeTeamChoice(string OrganizationId, string TeamName)
{
    public override string ToString() => TeamName;
}

internal sealed record LineupSlotChoice(LineupSlot Slot, string Display)
{
    public override string ToString() => Display;
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
    private readonly ScoutingIntelligenceService _scoutingIntelligence = new();
    private readonly DevelopmentPlanningService _developmentPlanning = new();
    private readonly PlayerDossierService _playerDossiers = new();
    private readonly StaffOfficeService _staffOffice = new();
    private readonly StaffCoachingService _staffCoaching = new();
    private readonly MedicalHealthService _medicalHealth = new();
    private readonly OwnerOfficeService _ownerOffice = new();
    private readonly LeagueAiService _leagueAi = new();
    private readonly OrganizationAiService _organizationAi = new();
    private readonly FranchiseIdentityService _franchiseIdentity = new();
    private readonly BudgetOverviewService _budgetOverview = new();
    private readonly RecruitingV2Service _recruitingV2 = new();
    private readonly SeasonFrameworkService _seasonFramework = new();
    private readonly GameRecapService _gameRecaps = new();
    private readonly SeasonStatsPolishService _statsPolish = new();
    private readonly FirstMonthAdvanceService _firstMonthAdvance = new();
    private readonly ActionCenterService _actionCenter = new();
    private readonly FreeAgentMarketService _freeAgents = new();
    private readonly FreeAgencyV2Service _freeAgencyV2 = new();
    private readonly TradeService _trades = new();
    private readonly TradeStrategyService _tradeStrategy = new();
    private readonly TradeDeadlineService _tradeDeadline = new();
    private readonly SaveGameService _saveGameService = new();
    private readonly SeasonRolloverService _seasonRollover = new();
    private readonly ContractManagementService _contracts = new();
    private readonly PlayabilityPolishService _playability = new();
    private readonly AgentEngine _agents = new();
    private readonly PlayerLifeCycleService _lifeCycle = new();
    private readonly StaffLifeCycleService _staffLifeCycle = new();
    private readonly OwnerLifeCycleService _ownerLifeCycle = new();
    private readonly RelationshipExpansionService _relationships = new();
    private readonly LineupService _lineups = new();
    private readonly LineChemistryService _lineChemistry = new();
    private readonly GameUsageService _gameUsage = new();
    private readonly TacticsService _tactics = new();
    private readonly StoryService _stories = new();
    private readonly MediaService _media = new();
    private readonly DraftWarRoomService _warRoom = new();
    private readonly PlayerRatingService _ratings = new();
    private readonly WaiverService _waivers = new();
    private readonly RfaUfaService _rfaUfa = new();
    private readonly ArbitrationService _arbitration = new();
    private readonly BuyoutService _buyouts = new();
    private readonly OfferSheetService _offerSheets = new();
    private readonly DraftIntelligenceService _draftIntelligence = new();
    private readonly AssetEvaluationService _assetEvaluation = new();
    private readonly OrganizationPlanningService _organizationPlanning = new();
    private readonly AiFrontOfficeDecisionService _aiFrontOffice = new();
    private readonly EngineRegistry _registry;
    private readonly List<LeagueTransaction> _leagueTransactions = [];
    private readonly List<JournalEntry> _journalEntries = [];
    private readonly Dictionary<string, ActionCenterStatus> _actionCenterStatuses = [];
    private string? _currentSavePath;
    private SaveGameMetadata? _lastSaveMetadata;
    private bool _draftModalDismissed;
    private string? _selectedDossierPersonId;
    private string? _selectedTradeTargetPersonId;
    private string? _selectedYourTradeAssetId;
    private string? _selectedOtherTradeAssetId;
    private readonly List<TradeAsset> _tradePlayerGives = [];
    private readonly List<TradeAsset> _tradePlayerReceives = [];
    public NewGmScenarioSnapshot ScenarioSnapshot { get; private set; }

    private AlphaDesktopState(EngineRegistry registry, NewGmScenarioSnapshot scenarioSnapshot, bool addFirstDayInbox = true)
    {
        _registry = registry;
        var prepared = _developmentPlanning.EnsureScenarioPlans(scenarioSnapshot);
        prepared = _agents.EnsureAgents(prepared);
        prepared = _organizationAi.EnsureProfiles(prepared);
        prepared = _franchiseIdentity.EnsureIdentities(prepared);
        prepared = _lifeCycle.EnsureLifeCycle(prepared, registry);
        prepared = _staffLifeCycle.EnsureLifeCycle(prepared, registry);
        prepared = _ownerLifeCycle.EnsureLifeCycle(prepared, registry);
        prepared = _relationships.EnsureExpansion(prepared, registry);
        prepared = _stories.EnsureStories(prepared, registry);
        prepared = _media.EnsureMediaFeed(prepared, Array.Empty<LeagueTransaction>(), registry);
        prepared = _lineups.EnsureLineup(prepared);
        prepared = _lineChemistry.EnsureChemistry(prepared);
        prepared = _gameUsage.EnsureGameUsage(prepared);
        prepared = new HockeyIntelligenceRatingService().EnsureRatings(prepared);
        prepared = _scoutingIntelligence.EnsureKnowledgeProfiles(prepared);
        prepared = new DevelopmentCurveService().EnsureCurves(prepared);
        prepared = _ratings.EnsureRatings(prepared);
        prepared = _warRoom.EnsureWarRoom(prepared);
        prepared = _assetEvaluation.EnsureEvaluations(prepared);
        prepared = _organizationPlanning.EnsurePlans(prepared);
        prepared = _tactics.EnsureTactics(prepared);
        prepared = _rfaUfa.EnsureRights(prepared, registry.Rulebook ?? prepared.LeagueProfile.Rulebook);
        prepared = _arbitration.EnsureArbitration(prepared, registry.Rulebook ?? prepared.LeagueProfile.Rulebook);
        prepared = new UiBrandingService().EnsureBranding(prepared);
        ScenarioSnapshot = prepared;
        Snapshot = ScenarioSnapshot.AlphaSnapshot;
        _selectedDossierPersonId = FirstDossierPersonId();
        if (addFirstDayInbox)
        {
            AddInboxItems(scenarioSnapshot.FirstDayInbox);
        }

        LatestSummary = scenarioSnapshot.ScenarioSummary;
    }

    public AlphaWorldSnapshot Snapshot { get; private set; }

    public InboxManager InboxManager { get; } = new();

    public IReadOnlyList<InboxMessage> Inbox => InboxManager.Query(new InboxFilter());

    public IReadOnlyList<LeagueTransaction> LeagueTransactions =>
        _leagueTransactions
            .Concat(ScenarioSnapshot.PlayerLifeCycleNews)
            .Concat(ScenarioSnapshot.StaffLifeCycleNews)
            .Concat(ScenarioSnapshot.OwnerLifeCycleNews)
            .Concat(FranchiseIdentityNews)
            .Concat(StoryLeagueNews)
            .Concat(ScenarioSnapshot.Playoffs.PlayoffLeagueNews)
            .OrderByDescending(transaction => transaction.Date)
            .ThenBy(transaction => transaction.TeamName, StringComparer.Ordinal)
            .ThenBy(transaction => transaction.PersonName, StringComparer.Ordinal)
            .ToArray();

    public IReadOnlyList<JournalEntry> JournalEntries =>
        _journalEntries
            .OrderByDescending(entry => entry.Date)
            .ThenBy(entry => entry.Title, StringComparer.Ordinal)
            .ToArray();

    public IReadOnlyList<FreeAgent> FreeAgents =>
        ScenarioSnapshot.FreeAgentMarket?.FreeAgents ?? Array.Empty<FreeAgent>();

    public FreeAgencyMarketState FreeAgencyState
    {
        get
        {
            var next = _freeAgencyV2.EnsureMarketState(_registry, ScenarioSnapshot);
            ScenarioSnapshot = next;
            Snapshot = next.AlphaSnapshot;
            return next.FreeAgencyMarketState!;
        }
    }

    public IReadOnlyList<TradeBlockEntry> TradeBlockEntries =>
        ScenarioSnapshot.TradeBlock?.Entries ?? Array.Empty<TradeBlockEntry>();

    public IReadOnlyList<TeamNeedsProfile> LeagueTradeNeeds => _tradeStrategy.BuildLeagueNeeds(ScenarioSnapshot);

    public TradeDeadlineWindow TradeDeadlineWindow => _tradeDeadline.GetWindow(ScenarioSnapshot, _registry.Rulebook);

    public IReadOnlyList<DeadlineRumor> DeadlineRumors =>
        ScenarioSnapshot.TradeDeadlineState?.Rumors ?? Array.Empty<DeadlineRumor>();

    public string TradeDeadlineCardTitle =>
        TradeDeadlineWindow.Status switch
        {
            TradeDeadlineStatus.DeadlineDay => "Today",
            TradeDeadlineStatus.Closed => "Closed",
            _ => $"{Math.Max(0, TradeDeadlineWindow.DaysRemaining)} day(s)"
        };

    public string TradeDeadlineAssessmentSummary =>
        ScenarioSnapshot.TradeDeadlineState?.BuyerSellerAssessment?.Summary
        ?? _tradeDeadline.AssessBuyerSeller(ScenarioSnapshot).Summary;

    public string TradeDeadlineBlockSummary =>
        ScenarioSnapshot.TradeDeadlineState?.LastTradeBlockUpdate?.Summary
        ?? "No deadline expansion yet.";

    public IReadOnlyList<WhereAreTheyNowRecord> WhereAreTheyNow =>
        new CareerHistoryService().BuildWhereAreTheyNow(ScenarioSnapshot);

    public string LatestTradeResponseText =>
        ScenarioSnapshot.TradeOffers
            .OrderByDescending(offer => offer.ProposedOn)
            .ThenByDescending(offer => offer.TradeOfferId, StringComparer.Ordinal)
            .Select(offer => offer.Evaluation?.Explanation ?? $"{offer.Status}: {offer.OtherOrganizationName}")
            .FirstOrDefault() ?? "No trade proposal has been sent yet.";

    public string LatestTradeCounterText =>
        ScenarioSnapshot.TradeOffers
            .OrderByDescending(offer => offer.ProposedOn)
            .ThenByDescending(offer => offer.TradeOfferId, StringComparer.Ordinal)
            .Select(offer => offer.Evaluation?.CounterSuggestion)
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text))
        ?? "No counter request yet.";

    public IReadOnlyList<string> LatestTradeReasons =>
        ScenarioSnapshot.TradeOffers
            .OrderByDescending(offer => offer.ProposedOn)
            .ThenByDescending(offer => offer.TradeOfferId, StringComparer.Ordinal)
            .Select(offer => offer.Evaluation?.Reasons)
            .FirstOrDefault(reasons => reasons is not null)
        ?? Array.Empty<string>();

    public IReadOnlyList<string> LatestTradeReactions =>
        ScenarioSnapshot.TradeOffers
            .OrderByDescending(offer => offer.ProposedOn)
            .ThenByDescending(offer => offer.TradeOfferId, StringComparer.Ordinal)
            .Select(offer => offer.Evaluation is null
                ? null
                : offer.Evaluation.StaffReactionNotes.Concat(offer.Evaluation.PlayerReactionNotes).ToArray())
            .FirstOrDefault(reactions => reactions is not null)
        ?? Array.Empty<string>();

    public bool HasTradeBuilderSelection => _selectedTradeTargetPersonId is not null;

    public bool CanWithdrawLatestTradeOffer =>
        ScenarioSnapshot.TradeOffers.Any(offer => offer.Status is TradeOfferStatus.Proposed or TradeOfferStatus.Accepted or TradeOfferStatus.Countered);

    public int UnreadInboxCount => Inbox.Count(message => message.IsUnread);

    public IReadOnlyList<PendingGmAction> OpenPendingActions =>
        ScenarioSnapshot.PendingActions
            .Where(action => action.IsOpen)
            .OrderBy(action => action.CreatedOn)
            .ThenBy(action => action.Title, StringComparer.Ordinal)
            .ToArray();

    public bool IsDraftUiEnabled => DraftUiPolicy.IsDraftUiEnabled(_registry.Rulebook);

    public WaiverWire WaiverWire
    {
        get
        {
            var wire = _waivers.EnsureWire(ScenarioSnapshot, _registry.Rulebook ?? ScenarioSnapshot.LeagueProfile.Rulebook);
            if (!ReferenceEquals(wire, ScenarioSnapshot.WaiverWire))
            {
                ScenarioSnapshot = ScenarioSnapshot with { WaiverWire = wire };
            }

            return ScenarioSnapshot.WaiverWire;
        }
    }

    public string WaiverRuleSummary
    {
        get
        {
            var rulebook = _registry.Rulebook ?? ScenarioSnapshot.LeagueProfile.Rulebook;
            var rules = rulebook.WaiverRules;
            if (rules is null || !rules.WaiversEnabled)
            {
                return "Waivers are disabled by this league rulebook. Junior-style leagues do not use the waiver wire.";
            }

            return $"Waivers enabled | Claim window: {rules.ClaimWindowHours} hour(s) | Order: {rules.WaiverOrder} | Exemptions: age <= {rules.ExemptAgeCutoff}, under {rules.ExemptProfessionalSeasons} pro seasons and {rules.ExemptGamesPlayed} games.";
        }
    }

    public int WaiverClaimCount(string transactionId) =>
        WaiverWire.Claims.Count(claim => claim.TransactionId == transactionId);

    public bool IsDraftModalVisible =>
        IsDraftUiEnabled
        && !_draftModalDismissed
        && Snapshot.CurrentDate >= ScenarioSnapshot.DraftDate
        && ScenarioSnapshot.DraftExperience?.Status != DraftExperienceStatus.Disabled;

    public TrainingCampCalendarInfo TrainingCampCalendar => _trainingCamp.GetCalendarInfo(_registry, ScenarioSnapshot);

    public ProspectListSummary ProspectSummary => _prospectDecisions.BuildSummary(ScenarioSnapshot);

    public SeasonReadinessReport SeasonReadinessReport => _seasonReadiness.Evaluate(_registry, ScenarioSnapshot);

    public BudgetSnapshot BudgetOverview => _budgetOverview.Build(ScenarioSnapshot, _registry.Rulebook ?? RulebookPresets.CreateJuniorMajor());

    public SalaryCapSnapshot SalaryCap => new SalaryCapService().BuildSnapshot(ScenarioSnapshot, _registry.Rulebook ?? ScenarioSnapshot.LeagueProfile.Rulebook);

    public OwnerOfficeSummary OwnerOffice => _ownerOffice.BuildSummary(ScenarioSnapshot, BudgetOverview);

    public RelationshipChemistrySummary RelationshipChemistry =>
        ScenarioSnapshot.RelationshipChemistry
        ?? _relationships.EnsureExpansion(ScenarioSnapshot, _registry).RelationshipChemistry!;

    public IReadOnlyList<ExpandedRelationshipProfile> RelationshipProfiles =>
        ScenarioSnapshot.RelationshipProfiles.Count > 0
            ? ScenarioSnapshot.RelationshipProfiles
            : _relationships.EnsureExpansion(ScenarioSnapshot, _registry).RelationshipProfiles;

    public IReadOnlyList<RelationshipConflict> RelationshipConflicts =>
        ScenarioSnapshot.RelationshipConflicts.Count > 0
            ? ScenarioSnapshot.RelationshipConflicts
            : _relationships.EnsureExpansion(ScenarioSnapshot, _registry).RelationshipConflicts;

    public LeagueAiReport LeagueAiReport => _leagueAi.BuildReport(ScenarioSnapshot, BudgetOverview);

    public IReadOnlyList<OrganizationLeagueProfile> LeagueOrganizationProfiles => LeagueAiReport.Profiles;

    public IReadOnlyList<OrganizationAiProfile> OrganizationAiProfiles =>
        ScenarioSnapshot.OrganizationAiProfiles.Count > 0 ? ScenarioSnapshot.OrganizationAiProfiles : LeagueAiReport.AiProfiles;

    public OrganizationLeagueProfile PlayerOrganizationLeagueProfile =>
        LeagueOrganizationProfiles.First(profile => profile.OrganizationId == ScenarioSnapshot.Organization.OrganizationId);

    public OrganizationAiProfile PlayerOrganizationAiProfile =>
        OrganizationAiProfileFor(ScenarioSnapshot.Organization.OrganizationId, ScenarioSnapshot.Organization.Name);

    public OrganizationPlan CurrentOrganizationPlan
    {
        get
        {
            if (ScenarioSnapshot.CurrentOrganizationPlan is null || ScenarioSnapshot.OrganizationPlans.Count == 0)
            {
                var prepared = _organizationPlanning.EnsurePlans(ScenarioSnapshot);
                ScenarioSnapshot = prepared;
                Snapshot = prepared.AlphaSnapshot;
            }

            return ScenarioSnapshot.CurrentOrganizationPlan!;
        }
    }

    public IReadOnlyList<OrganizationPlan> OrganizationPlans
    {
        get
        {
            _ = CurrentOrganizationPlan;
            return ScenarioSnapshot.OrganizationPlans;
        }
    }

    public string OrganizationPlanningReportText() =>
        _organizationPlanning.BuildPlanningReport(ScenarioSnapshot);

    public IReadOnlyList<FranchiseIdentity> FranchiseIdentities =>
        ScenarioSnapshot.FranchiseIdentities.Count > 0
            ? ScenarioSnapshot.FranchiseIdentities
            : _franchiseIdentity.EnsureIdentities(ScenarioSnapshot).FranchiseIdentities;

    public FranchiseIdentity PlayerFranchiseIdentity =>
        FranchiseIdentities.First(identity => identity.OrganizationId == ScenarioSnapshot.Organization.OrganizationId);

    public IReadOnlyList<LeagueTransaction> FranchiseIdentityNews => _franchiseIdentity.BuildLeagueNews(ScenarioSnapshot);

    public IReadOnlyList<Story> Stories =>
        ScenarioSnapshot.Stories.Count > 0
            ? ScenarioSnapshot.Stories
            : _stories.EnsureStories(ScenarioSnapshot, _registry).Stories;

    public IReadOnlyList<LeagueTransaction> StoryLeagueNews => _stories.BuildLeagueNews(ScenarioSnapshot);

    public IReadOnlyList<LeagueTransaction> LeagueIdentityNews => LeagueAiReport.LeagueNews;

    public int LeagueNewsCount => LeagueTransactions.Count + LeagueIdentityNews.Count;

    public MediaFeed MediaFeed => _media.BuildFeed(ScenarioSnapshot, LeagueTransactions, _registry);

    public MediaArticle? TopMediaHeadline => _media.TopHeadline(ScenarioSnapshot, LeagueTransactions, _registry);

    public string PlayoffStatusText => ScenarioSnapshot.Playoffs.Bracket?.Status.ToString() ?? "Not Started";

    public string PlayoffDashboardSummary
    {
        get
        {
            var bracket = ScenarioSnapshot.Playoffs.Bracket;
            if (bracket is null)
            {
                return "Bracket pending regular-season completion.";
            }

            if (bracket.Status == PlayoffStatus.Completed)
            {
                return $"Champion: {bracket.ChampionTeamName}; runner-up: {bracket.RunnerUpTeamName}.";
            }

            var current = bracket.CurrentSeries;
            return current is null
                ? $"{bracket.Status}: {bracket.Seeds.Count} qualified team(s)."
                : $"{current.HigherSeed.TeamName} vs {current.LowerSeed.TeamName}, series {current.HigherSeedWins}-{current.LowerSeedWins}.";
        }
    }

    public string InboxFocusSummary =>
        $"{Inbox.Count} decision-focused inbox item(s), {JournalEntries.Count} routine item(s) journaled.";

    public ContractManagementSummary ContractManagement => _contracts.BuildSummary(ScenarioSnapshot, _registry.Rulebook);

    public IReadOnlyList<PlayerRightsDecision> ContractRightsDecisions
    {
        get
        {
            var updated = _rfaUfa.EnsureRights(ScenarioSnapshot, _registry.Rulebook ?? ScenarioSnapshot.LeagueProfile.Rulebook);
            if (!ReferenceEquals(updated, ScenarioSnapshot))
            {
                ScenarioSnapshot = updated;
                Snapshot = updated.AlphaSnapshot;
            }

            return ScenarioSnapshot.PlayerRightsDecisions;
        }
    }

    public string ContractRightsRuleSummary
    {
        get
        {
            var rules = (_registry.Rulebook ?? ScenarioSnapshot.LeagueProfile.Rulebook).FreeAgentRightsRules;
            if (rules is null || !rules.RfaUfaSystemEnabled)
            {
                return "RFA/UFA rights are disabled by this rulebook. Junior-style leagues do not use NHL-style rights unless enabled.";
            }

            return $"RFA/UFA enabled | UFA age {rules.UfaAge} | UFA accrued seasons {rules.UfaAccruedSeasonsThreshold} | QO deadline +{rules.QualifyingOfferDeadlineDaysAfterExpiry} day(s) | tender window {rules.ContractTenderWindowDays} day(s).";
        }
    }

    public IReadOnlyList<ArbitrationEligibility> ArbitrationEligibility
    {
        get
        {
            var updated = _arbitration.EnsureArbitration(ScenarioSnapshot, _registry.Rulebook ?? ScenarioSnapshot.LeagueProfile.Rulebook);
            if (!ReferenceEquals(updated, ScenarioSnapshot))
            {
                ScenarioSnapshot = updated;
                Snapshot = updated.AlphaSnapshot;
            }

            return _arbitration.BuildEligibility(ScenarioSnapshot, _registry.Rulebook ?? ScenarioSnapshot.LeagueProfile.Rulebook);
        }
    }

    public IReadOnlyList<ArbitrationCase> ArbitrationCases
    {
        get
        {
            var updated = _arbitration.EnsureArbitration(ScenarioSnapshot, _registry.Rulebook ?? ScenarioSnapshot.LeagueProfile.Rulebook);
            if (!ReferenceEquals(updated, ScenarioSnapshot))
            {
                ScenarioSnapshot = updated;
                Snapshot = updated.AlphaSnapshot;
            }

            return ScenarioSnapshot.ArbitrationCases;
        }
    }

    public string ArbitrationRuleSummary =>
        _arbitration.BuildRuleSummary(_registry.Rulebook ?? ScenarioSnapshot.LeagueProfile.Rulebook);

    public BuyoutWindow BuyoutWindow =>
        _buyouts.BuildWindow(ScenarioSnapshot, _registry.Rulebook ?? ScenarioSnapshot.LeagueProfile.Rulebook);

    public IReadOnlyList<BuyoutEligibility> BuyoutEligibility =>
        _buyouts.BuildEligibility(ScenarioSnapshot, _registry.Rulebook ?? ScenarioSnapshot.LeagueProfile.Rulebook);

    public IReadOnlyList<ContractBuyout> ContractBuyouts => ScenarioSnapshot.ContractBuyouts;

    public string BuyoutRuleSummary =>
        _buyouts.BuildRuleSummary(_registry.Rulebook ?? ScenarioSnapshot.LeagueProfile.Rulebook);

    public IReadOnlyList<OfferSheetEligibility> OfferSheetEligibility =>
        _offerSheets.BuildEligibility(ScenarioSnapshot, _registry.Rulebook ?? ScenarioSnapshot.LeagueProfile.Rulebook);

    public IReadOnlyList<OfferSheet> OfferSheets => ScenarioSnapshot.OfferSheets;

    public string OfferSheetRuleSummary =>
        _offerSheets.BuildRuleSummary(_registry.Rulebook ?? ScenarioSnapshot.LeagueProfile.Rulebook);

    public ScheduledGame? NextGame => _seasonFramework.NextGame(ScenarioSnapshot);

    public GameRecap? LastGameRecap => _seasonFramework.LastPlayerTeamRecap(ScenarioSnapshot);

    public IReadOnlyList<ScheduledGame> RecentResults => _gameRecaps.RecentResults(ScenarioSnapshot);

    public IReadOnlyList<ScheduledGame> UpcomingGames => _gameRecaps.UpcomingGames(ScenarioSnapshot);

    public IReadOnlyList<ScheduledGame> TodaysGames => _gameRecaps.TodaysGames(ScenarioSnapshot);

    public SeasonStatLeaders StatLeaders => _statsPolish.BuildLeaders(ScenarioSnapshot);

    public bool CanCompleteSeason => _seasonRollover.IsRegularSeasonComplete(ScenarioSnapshot);

    public string TeamRecordText
    {
        get
        {
            var standing = ScenarioSnapshot.Standings?.Teams.FirstOrDefault(team => team.OrganizationId == ScenarioSnapshot.Organization.OrganizationId);
            return standing is null ? "0-0-0" : $"{standing.Wins}-{standing.Losses}-{standing.OvertimeLosses}, {standing.Points} pts";
        }
    }

    public int UrgentPendingDecisionCount => FirstMonthAdvanceService.UrgentPendingActions(ScenarioSnapshot).Count;

    public IReadOnlyList<ActionCenterItem> ActionCenterItems
    {
        get
        {
            EnsureLifeCycleState();
            return _playability.CleanActionCenterItems(_actionCenter.BuildItems(ScenarioSnapshot, InboxManager.AllMessages, BudgetOverview, SeasonReadinessReport, StaffVacancies, _actionCenterStatuses));
        }
    }

    public int OpenActionCount => ActionCenterItems.Count(item => item.Status == ActionCenterStatus.Open);

    public int UrgentActionCount => ActionCenterItems.Count(item => item.Status == ActionCenterStatus.Open && item.Priority == ActionCenterPriority.Urgent);

    public IReadOnlyList<string> DailyAgenda => _actionCenter.BuildDailyAgenda(ScenarioSnapshot, ActionCenterItems, BudgetOverview);

    public IReadOnlyList<string> AssistantGmRecommendations => _actionCenter.BuildAssistantGmRecommendations(ScenarioSnapshot, ActionCenterItems, BudgetOverview);

    public int GameUsageWarningCount => CurrentGameUsage.CoachRecommendations.Count(recommendation => recommendation.IsImportant);

    public string GameUsageDashboardSummary =>
        CurrentGameUsage.CoachRecommendations.FirstOrDefault(recommendation => recommendation.IsImportant)?.SuggestedAction
        ?? "PP, PK, goalie, extra-attacker, three-on-three, and shootout usage are set.";

    public int TacticsWarningCount => CurrentTactics.Recommendations.Count(recommendation => recommendation.IsImportant);

    public string TacticsDashboardSummary =>
        CurrentTactics.Recommendations.FirstOrDefault(recommendation => recommendation.IsImportant)?.SuggestedAction
        ?? CurrentTactics.FitReport.CoachRecommendation;

    public IReadOnlyList<string> UpcomingActionEvents => _actionCenter.BuildUpcomingEvents(ScenarioSnapshot);

    public IReadOnlyList<GlobalSearchResult> Search(string query) =>
        _playability.Search(
            ScenarioSnapshot,
            InboxManager.AllMessages,
            LeagueTransactions.Concat(LeagueIdentityNews).ToArray(),
            JournalEntries,
            query);

    public IReadOnlyList<PlaytestChecklistItem> PlaytestChecklist =>
        _playability.BuildPlaytestChecklist(ScenarioSnapshot, ActionCenterItems);

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

    public int ContractDecisionCount => ContractManagement.ExpiringPlayers.Count
        + ContractManagement.ExpiringStaff.Count
        + ContractManagement.UnsignedProspects.Count
        + ContractManagement.AcceptedOffersAwaitingApproval.Count
        + ContractRightsDecisions.Count(decision => decision.IsOpenDecision)
        + ArbitrationCases.Count(item => item.IsOpen)
        + ContractBuyouts.Count(item => item.Status == BuyoutStatus.PendingConfirmation)
        + OfferSheets.Count(item => item.IsActive);

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

    public string RosterBreakdownTitle
    {
        get
        {
            var active = Snapshot.Roster.ActivePlayers.Count;
            var required = _registry.Rulebook?.RosterRules?.ActiveRoster ?? active;
            return $"{active}/{required} active players";
        }
    }

    public string RosterBreakdownSecondary
    {
        get
        {
            var active = Snapshot.Roster.ActivePlayers;
            var goalies = active.Count(player => player.Position == RosterPosition.Goalie);
            var defense = active.Count(player => player.Position == RosterPosition.Defense);
            var forwards = active.Count(player => player.Position is RosterPosition.Center or RosterPosition.LeftWing or RosterPosition.RightWing);
            return $"{goalies} G | {defense} D | {forwards} F";
        }
    }

    public string RosterAgeBreakdown
    {
        get
        {
            var active = Snapshot.Roster.ActivePlayers;
            var under18 = active.Count(player => (PersonAge(player.PersonId) ?? player.Age ?? 0) < 18);
            var middle = active.Count(player =>
            {
                var age = PersonAge(player.PersonId) ?? player.Age ?? 0;
                return age is >= 18 and <= 19;
            });
            var overage = active.Count(player => (PersonAge(player.PersonId) ?? player.Age ?? 0) >= 20);
            return $"{under18} under 18 | {middle} age 18-19 | {overage} age 20+";
        }
    }

    public string RosterContractBreakdown
    {
        get
        {
            var activeIds = Snapshot.Roster.ActivePlayers.Select(player => player.PersonId).ToHashSet(StringComparer.Ordinal);
            var contracts = ScenarioSnapshot.Contracts
                .Concat(Snapshot.Contracts)
                .DistinctBy(contract => contract.ContractId)
                .Where(contract => activeIds.Contains(contract.PersonId) && contract.ContractType == ContractType.JuniorPlayerAgreement)
                .ToArray();
            var expired = contracts.Count(contract => contract.Status == ContractStatus.Expired || contract.Term.EndDate < Snapshot.CurrentDate);
            var expiring = contracts.Count(contract => contract.Status == ContractStatus.Signed
                && contract.Term.EndDate >= Snapshot.CurrentDate
                && contract.Term.EndDate <= Snapshot.CurrentDate.AddDays(30));
            var unsigned = activeIds.Count - contracts.Count(contract => contract.Status == ContractStatus.Signed && contract.Term.EndDate >= Snapshot.CurrentDate);
            return $"{contracts.Length} inherited agreements | {expired} expired | {expiring} expiring soon | {unsigned} need renewal/walk-away review";
        }
    }

    public string RosterBreakdownSummary =>
        $"{RosterBreakdownTitle} | {RosterBreakdownSecondary} | {RosterAgeBreakdown} | {RosterContractBreakdown}";

    public string DraftCountdownText =>
        ScenarioSnapshot.DaysUntilDraft switch
        {
            < 0 => "Draft complete",
            0 => "Draft day",
            1 => "1 day",
            var days => $"{days} days"
        };

    private DraftClassProfile? DraftClassProfile => ScenarioSnapshot.CurrentDraftClassProfile;

    public string DraftClassThemeText => DraftClassProfile?.ReadableTheme ?? "draft class profile not generated";

    public string DraftClassSummaryText => DraftClassProfile?.PreviewText ?? "No draft class summary is available yet.";

    public string DraftClassStrengthText =>
        DraftClassProfile is null
            ? "not available"
            : string.Join("; ", DraftClassProfile.Strengths.Select(strength => $"{strength.Category}: {strength.Description}"));

    public string DraftClassWeaknessText =>
        DraftClassProfile is null
            ? "not available"
            : string.Join("; ", DraftClassProfile.Weaknesses.Select(weakness => $"{weakness.Category}: {weakness.Description}"));

    public string DraftClassPositionDepthText =>
        DraftClassProfile is null
            ? "not available"
            : string.Join(" | ", DraftClassProfile.PositionalDepth
                .Where(item => item.Value > 0)
                .Select(item => $"{PositionShort(item.Key)} {item.Value}"));

    public string DraftClassRegionalText =>
        DraftClassProfile is null
            ? "not available"
            : string.Join(" | ", DraftClassProfile.RegionalDistribution.Select(item => $"{item.Key} {item.Value}"));

    public string DraftClassScoutQuoteText => DraftClassProfile?.ScoutQuote ?? "No scout quote is available yet.";

    public string DraftClassPlayersToWatchText
    {
        get
        {
            if (DraftClassProfile is null)
            {
                return "not available";
            }

            var summary = new DraftClassGenerator().BuildSummary(DraftClassProfile, Snapshot.DraftBoard);
            return summary.PlayersToWatch.Count == 0 ? "none listed" : string.Join(" | ", summary.PlayersToWatch);
        }
    }

    public DraftWarRoomState DraftWarRoom
    {
        get
        {
            var updated = _warRoom.EnsureWarRoom(ScenarioSnapshot);
            ScenarioSnapshot = updated;
            Snapshot = updated.AlphaSnapshot;
            return updated.DraftWarRoom;
        }
    }

    public string DraftWarRoomSummaryText => _warRoom.BuildWarRoomSummary(ScenarioSnapshot);

    public string DraftBoardRealismText
    {
        get
        {
            var state = DraftWarRoom;
            var validation = state.RealismValidation;
            var rebalancing = state.RebalancingResult;
            var status = validation is null
                ? "not evaluated"
                : validation.IsValid ? "playable" : "needs review";
            var moves = rebalancing is null || rebalancing.Moves.Count == 0
                ? "no comparable rebalancing moves"
                : $"{rebalancing.Moves.Count} comparable move(s)";
            return $"{status}: {validation?.Summary ?? "No realism report yet."} {moves}.";
        }
    }

    public string DraftPositionValueText
    {
        get
        {
            var profile = DraftWarRoom.PositionValueProfile;
            if (profile is null)
            {
                return "Position value profile not available.";
            }

            return $"{profile.Summary} {string.Join(" | ", profile.Adjustments.Select(adjustment => $"{DraftPositionValueService.PositionLabel(adjustment.Position)} {Signed(adjustment.TotalAdjustment)}"))}";
        }
    }

    public string DraftValueContext(DraftBoardEntry? entry)
    {
        if (entry is null)
        {
            return "Draft value context unavailable.";
        }

        var evaluation = new DraftPositionValueService().EvaluateEntry(ScenarioSnapshot, entry, DraftWarRoom.PositionValueProfile);
        var validation = DraftWarRoom.RealismValidation;
        var status = validation?.IsValid == true ? "board profile valid" : "board profile under review";
        return $"{DraftPositionValueService.PositionLabel(evaluation.Position)} market and class context considered; {status}. {evaluation.Explanation}";
    }

    public DraftScoutConsensus DraftConsensus(string prospectPersonId) =>
        _warRoom.BuildConsensus(ScenarioSnapshot, prospectPersonId);

    public string DraftConsensusText(string prospectPersonId)
    {
        var consensus = DraftConsensus(prospectPersonId);
        var builder = new StringBuilder();
        builder.AppendLine($"{consensus.ProspectName}: {consensus.Level} ({consensus.AgreementScore}/100)");
        builder.AppendLine(consensus.Summary);
        builder.AppendLine();
        foreach (var opinion in consensus.Opinions)
        {
            builder.AppendLine($"{opinion.Department}: {opinion.Opinion} Confidence: {opinion.Confidence}");
        }

        return builder.ToString();
    }

    public string DraftComparisonText(IReadOnlyList<string> prospectIds)
    {
        try
        {
            var comparison = _warRoom.CompareProspects(ScenarioSnapshot, prospectIds);
            var builder = new StringBuilder();
            builder.AppendLine(comparison.Summary);
            builder.AppendLine();
            foreach (var item in comparison.Prospects)
            {
                builder.AppendLine($"{item.ProspectName} | {PositionShort(item.Position)} | age {item.Age?.ToString() ?? "unknown"} | {item.Height}, {item.Weight} | {item.CurrentTeamLeague}");
                builder.AppendLine($"  Confidence: {item.Confidence?.ToString() ?? "Unknown"} | Projection: {item.Projection}");
                builder.AppendLine($"  Character: {item.Character}");
                builder.AppendLine($"  Development: {item.Development}");
                builder.AppendLine($"  Medical: {item.Medical}");
                builder.AppendLine($"  Story: {item.DraftStory}");
            }

            return builder.ToString();
        }
        catch (ArgumentException ex)
        {
            return ex.Message;
        }
    }

    public string DraftClassRemainingBestByPositionText
    {
        get
        {
            var drafted = ScenarioSnapshot.DraftExperience?.Selections
                .Select(selection => selection.ProspectPersonId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);
            var best = Snapshot.DraftBoard.Entries
                .Where(entry => !drafted.Contains(entry.ProspectPersonId))
                .OrderBy(entry => entry.Rank)
                .GroupBy(entry => entry.Bio?.Position ?? PersonPosition(entry.ProspectPersonId))
                .Select(group =>
                {
                    var entry = group.First();
                    return $"{PositionShort(group.Key)}: #{entry.Rank} {FindPersonNameForDisplay(entry.ProspectPersonId)}";
                });
            var text = string.Join(" | ", best);
            return string.IsNullOrWhiteSpace(text) ? "none remaining" : text;
        }
    }

    public string DraftClassContext(DraftBoardEntry entry) =>
        string.IsNullOrWhiteSpace(entry.ClassContextNote)
            ? "Class context still being built."
            : entry.ClassContextNote;

    public string DraftRiskText(DraftBoardEntry entry) =>
        string.IsNullOrWhiteSpace(entry.RiskSummary)
            ? "Risk read still being built."
            : entry.RiskSummary;

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

    public IReadOnlyList<ScoutIntelligenceProfile> ScoutIntelligenceProfiles => _scoutingIntelligence.BuildScoutProfiles(ScenarioSnapshot, _registry.Rulebook);

    public string ScoutingBudgetText
    {
        get
        {
            var impact = _scoutingIntelligence.BuildBudgetImpact(ScenarioSnapshot, _registry.Rulebook);
            return $"{impact.TravelCoverage} {impact.TournamentCoverage} {impact.InternationalCoverage}";
        }
    }

    public IReadOnlyList<ScoutingOperationScoutProfile> AvailableScoutProfiles =>
        ScoutProfiles
            .Where(profile => ScenarioSnapshot.ScoutingOperations.All(assignment => assignment.ScoutPersonId != profile.ScoutPersonId || !assignment.IsOpen))
            .ToArray();

    public IReadOnlyList<StaffOfficeProfile> StaffProfiles => _staffOffice.BuildStaffProfiles(ScenarioSnapshot, _registry.Rulebook ?? RulebookPresets.CreateJuniorMajor());

    public StaffMarket StaffMarket
    {
        get
        {
            var next = new StaffMarketService().EnsureMarket(_registry, ScenarioSnapshot);
            ScenarioSnapshot = next;
            Snapshot = next.AlphaSnapshot;
            return next.StaffMarket!;
        }
    }

    public IReadOnlyList<StaffMarketCandidate> StaffMarketCandidates =>
        StaffMarket.Candidates
            .OrderBy(candidate => candidate.Status)
            .ThenByDescending(candidate => candidate.HiringInterest)
            .ThenBy(candidate => candidate.Name, StringComparer.Ordinal)
            .ToArray();

    public StaffMarketCandidate? StaffMarketCandidateFor(string personId) =>
        StaffMarket.FindByPersonId(personId);

    public IReadOnlyList<CoachingStaffProfile> CoachingStaffProfiles => _staffCoaching.BuildCoachProfiles(ScenarioSnapshot);

    public IReadOnlyList<DepartmentGradeReport> DepartmentGrades => _staffCoaching.BuildDepartmentGrades(ScenarioSnapshot);

    public IReadOnlyList<OrganizationChartNode> OrganizationChart => _staffCoaching.BuildOrganizationChart(ScenarioSnapshot);

    public StaffMeetingReport MonthlyStaffMeeting => _staffCoaching.GenerateMonthlyMeetingReport(ScenarioSnapshot);

    public string StaffCoachingProfileText(string personId)
    {
        var profile = CoachingStaffProfiles.FirstOrDefault(profile => profile.PersonId == personId);
        if (profile is null)
        {
            return "No coaching philosophy profile is available for this staff member.";
        }

        return $"Coaching Philosophy: {profile.Philosophy}\n"
            + $"Coach Specialties: {string.Join(", ", profile.Specialties)}\n"
            + $"Coach Personality: {profile.Personality}\n"
            + $"{profile.PhilosophySummary}\n"
            + $"Development impact: {profile.PlayerDevelopmentImpact}\n"
            + $"Roster recommendations: {profile.RosterRecommendationStyle}\n"
            + $"Responsibilities: {profile.CurrentResponsibilities}\n"
            + $"Career: {profile.CareerSummary}";
    }

    public string StaffChemistryText(string personId)
    {
        var links = _staffCoaching.BuildStaffChemistry(ScenarioSnapshot)
            .Where(link => link.FromPersonId == personId || link.ToPersonId == personId)
            .Take(4)
            .Select(link => $"{link.FromName} -> {link.ToName}: trust {link.Trust}, respect {link.Respect}, confidence {link.Confidence}. {link.Summary}");
        var text = string.Join("\n", links);
        return string.IsNullOrWhiteSpace(text) ? "Staff Chemistry: no direct relationship signal yet." : $"Staff Chemistry:\n{text}";
    }

    public string DepartmentGradesText() =>
        string.Join("\n", DepartmentGrades.Select(report => $"{report.DepartmentName}: {report.Grade} ({report.Score}/100) - {report.Summary}"));

    public string OrganizationChartText() =>
        string.Join("\n", OrganizationChart.Select(node => $"{node.Name} - {node.Role} | reports to {FindPersonNameForDisplay(node.ReportsToPersonId)} | {node.Responsibilities} | {node.SalaryText}"));

    public string MonthlyStaffMeetingText()
    {
        var meeting = MonthlyStaffMeeting;
        return $"Staff Meeting - {meeting.MeetingDate:yyyy-MM-dd}\n"
            + $"Head coach: {meeting.HeadCoachName}\n"
            + $"{meeting.Summary}\n\n"
            + "Recommendations:\n"
            + string.Join("\n", meeting.Recommendations.Select(item => $"- {item}"))
            + "\n\nDevelopment:\n"
            + string.Join("\n", meeting.DevelopmentNotes.Select(item => $"- {item}"))
            + "\n\nRoster:\n"
            + string.Join("\n", meeting.RosterNotes.Select(item => $"- {item}"))
            + "\n\nMedical:\n"
            + string.Join("\n", meeting.MedicalNotes.Select(item => $"- {item}"));
    }

    public string StaffPerformanceReviewText(string personId)
    {
        var review = _staffCoaching.BuildPerformanceReview(ScenarioSnapshot, personId);
        return $"Performance Review: {review.StaffName}\n"
            + $"Outcome: {review.Outcome}\n"
            + $"{review.Summary}\n\n"
            + $"Strengths: {string.Join(", ", review.Strengths.DefaultIfEmpty("none"))}\n"
            + $"Concerns: {string.Join(", ", review.Concerns.DefaultIfEmpty("none"))}\n"
            + $"Recommendation: {review.Recommendation}";
    }

    public string CandidateHiringFitText(string personId)
    {
        var market = StaffMarketCandidateFor(personId);
        if (market is null)
        {
            return "Candidate is no longer available.";
        }

        var candidate = market.Candidate;
        var fit = _staffCoaching.EvaluateHiringFit(ScenarioSnapshot, candidate);
        return $"Hiring fit: {fit.FitScore}/100\n"
            + $"Market status: {market.Status}; interest {market.HiringInterest}/100; current employer {market.CurrentEmployer}\n"
            + $"{fit.SalaryImpact}\n"
            + $"{fit.ChemistryRisk}\n"
            + $"{fit.ExperienceSummary}\n"
            + $"{fit.Recommendation}\n"
            + string.Join("\n", fit.Reasons.Select(reason => $"- {reason}"));
    }

    public string PlayerCoachFitText(string personId)
    {
        var fit = _staffCoaching.EvaluatePlayerFit(ScenarioSnapshot, personId);
        return $"Coach Opinion: {fit.FitGrade} fit with {fit.CoachName}.\n"
            + $"{fit.Summary}\n"
            + string.Join("\n", fit.Reasons.Select(reason => $"- {reason}"));
    }

    public IReadOnlyList<PlayerHealthProfile> HealthProfiles => _medicalHealth.BuildHealthProfiles(ScenarioSnapshot);

    public MedicalSummaryReport MedicalSummary => _medicalHealth.BuildMedicalSummary(ScenarioSnapshot);

    public string HealthProfileText(string personId)
    {
        var profile = _medicalHealth.BuildHealthProfile(ScenarioSnapshot, personId);
        return $"Current Health: {profile.CurrentHealth}\n"
            + $"Durability {profile.Durability}/100 | Fatigue {profile.Fatigue}/100 | Recovery rate {profile.RecoveryRate}/100\n"
            + $"Injury Risk {profile.InjuryRisk}/100 | Wear & tear {profile.WearAndTear}/100 | Recurring risk {profile.RecurringInjuryRisk}/100\n"
            + $"Conditioning: {profile.Conditioning} | Medical confidence {profile.MedicalConfidence}/100\n"
            + profile.Summary;
    }

    public string MedicalReportText(string personId)
    {
        var report = _medicalHealth.BuildMedicalReport(ScenarioSnapshot, personId);
        return $"{report.PlayerName} ({report.Position})\n"
            + $"Health: {report.HealthStatus} | Conditioning: {report.ConditioningStatus}\n"
            + $"{report.ExpectedReturn}\n"
            + $"Why: {report.WhyItMatters}\n"
            + $"Staff: {report.StaffComment}\n"
            + $"Recommendation: {report.ReturnRecommendation}\n"
            + $"Options: {string.Join(", ", report.AvailableOptions)}";
    }

    public string MedicalSummaryText()
    {
        var summary = MedicalSummary;
        return $"Current Injuries: {summary.ActiveInjuries}\n"
            + $"Returning Soon: {summary.ReturningSoon}\n"
            + $"High Risk: {summary.HighRiskPlayers}\n"
            + $"Conditioning: {summary.ConditioningAssignments}\n"
            + $"Games Lost to Injury: {summary.GamesLostToInjury}\n"
            + $"Most Significant Injury: {summary.MostSignificantInjury}\n"
            + $"Medical Staff Grade: {summary.MedicalDepartmentGrade}\n"
            + $"Medical Budget: {summary.MedicalBudgetImpact}\n"
            + string.Join("\n", summary.PlayerNotes.Select(note => $"- {note}"));
    }

    public bool HasActiveInjury(string personId) =>
        ScenarioSnapshot.AlphaSnapshot.Injuries.Any(injury => injury.PersonId == personId && injury.IsActive);

    public IReadOnlyList<StaffVacancy> StaffVacancies => _staffOffice.BuildVacancies(ScenarioSnapshot, _registry.Rulebook ?? RulebookPresets.CreateJuniorMajor());

    public string StaffVacancySummary =>
        StaffVacancies.Count == 0
            ? "All required hockey operations positions are covered."
            : string.Join(" ", StaffVacancies.Take(3).Select(vacancy => vacancy.Warning));

    public PlayerDossierView? CurrentDossier
    {
        get
        {
            EnsureLifeCycleState();
            return _selectedDossierPersonId is null
                ? null
                : _playerDossiers.CreateDossier(ScenarioSnapshot, _selectedDossierPersonId);
        }
    }

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

    public string SaveFolder => _saveGameService.DefaultSaveFolder;

    public string? CurrentSavePath => _currentSavePath;

    public string LastSavedText =>
        _lastSaveMetadata is null
            ? "not saved yet"
            : $"{_lastSaveMetadata.LastSavedAt:yyyy-MM-dd HH:mm} UTC";

    public static AlphaDesktopState Create()
    {
        var selection = new MultiLeagueCareerService().SelectLeagueAndTeam(LeagueExperience.Junior, "org-prairie-falcons");
        var scenario = new MultiLeagueCareerService().CreateScenario(selection);
        return new AlphaDesktopState(scenario.Registry, scenario.ScenarioSnapshot);
    }

    public static AlphaDesktopState Create(GmProfileCreationSettings gmSettings)
    {
        return Create(gmSettings, LeagueExperience.Junior, "org-prairie-falcons");
    }

    public static AlphaDesktopState Create(GmProfileCreationSettings gmSettings, LeagueExperience leagueExperience, string organizationId)
    {
        var service = new MultiLeagueCareerService();
        var selection = service.SelectLeagueAndTeam(leagueExperience, organizationId, gmSettings);
        var scenario = service.CreateScenario(selection);
        return new AlphaDesktopState(scenario.Registry, scenario.ScenarioSnapshot);
    }

    public static SaveLoadResult LoadCareer(string filePath, out AlphaDesktopState? state)
    {
        var service = new SaveGameService();
        var result = service.LoadFromFile(filePath);
        if (!result.Success || result.SaveGame is null)
        {
            state = null;
            return result;
        }

        state = FromSaveGame(result.SaveGame, result.Registry ?? service.RestoreRegistry(result.SaveGame.ScenarioSnapshot), filePath);
        state.LatestSummary = result.CompatibilityWarning is null
            ? result.Message
            : $"{result.Message} {result.CompatibilityWarning}";
        return result;
    }

    public SaveLoadResult SaveCareer(string? filePath = null)
    {
        ScenarioSnapshot = _media.EnsureMediaFeed(ScenarioSnapshot, LeagueTransactions, _registry);
        Snapshot = ScenarioSnapshot.AlphaSnapshot;
        var result = _saveGameService.SaveCareer(
            ScenarioSnapshot,
            InboxManager.AllMessages,
            LeagueTransactions,
            _actionCenterStatuses,
            BudgetOverview,
            filePath ?? _currentSavePath,
            fileDisplayName: $"{ScenarioSnapshot.GeneralManagerProfile.Person.Identity.DisplayName} - {ScenarioSnapshot.Organization.Name}",
            previousMetadata: _lastSaveMetadata);

        if (result.Success && result.SaveGame is not null)
        {
            _currentSavePath = result.FilePath;
            _lastSaveMetadata = result.SaveGame.Metadata;
            LatestSummary = $"Save successful. Last saved {result.SaveGame.Metadata.LastSavedAt:yyyy-MM-dd HH:mm} UTC.";
        }
        else
        {
            LatestSummary = result.Message;
        }

        return result;
    }

    private static AlphaDesktopState FromSaveGame(SaveGame saveGame, EngineRegistry registry, string? filePath)
    {
        var state = new AlphaDesktopState(registry, saveGame.ScenarioSnapshot, addFirstDayInbox: false)
        {
            _currentSavePath = filePath,
            _lastSaveMetadata = saveGame.Metadata
        };
        state.InboxManager.ReplaceAll(saveGame.InboxMessages);
        state.AddLeagueTransactions(saveGame.LeagueTransactions);
        foreach (var status in saveGame.ActionCenterStatuses)
        {
            state._actionCenterStatuses[status.Key] = status.Value;
        }

        state.EnsureSelectedDossierStillExists();
        return state;
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

    public void SetActionCenterStatus(string itemId, ActionCenterStatus status)
    {
        _actionCenterStatuses[itemId] = status;
        LatestSummary = $"Action Center item marked {status}.";
    }

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

    public void ScoutAgainFor(string playerPersonId)
    {
        var scout = AvailableScoutProfiles.OrderBy(profile => profile.Workload).FirstOrDefault()
            ?? ScoutProfiles.OrderBy(profile => profile.Workload).FirstOrDefault();
        if (scout is null)
        {
            LatestSummary = "No scouting staff are available for another viewing.";
            return;
        }

        ApplyScoutingOperationResult(_scoutingOperations.AssignScoutToPlayer(
            _registry,
            ScenarioSnapshot,
            scout.ScoutPersonId,
            playerPersonId,
            ScoutingOperationPriority.Normal,
            $"Scout again: build a larger viewing sample and compare against prior reports for {FindPersonName(playerPersonId)}.",
            Snapshot.CurrentDate.AddDays(5)));
    }

    public void TournamentScoutFor(string playerPersonId)
    {
        var scout = AvailableScoutProfiles
            .OrderByDescending(profile => profile.RegionSpecialty.Contains("Europe", StringComparison.OrdinalIgnoreCase))
            .ThenBy(profile => profile.Workload)
            .FirstOrDefault()
            ?? ScoutProfiles.OrderBy(profile => profile.Workload).FirstOrDefault();
        if (scout is null)
        {
            LatestSummary = "No scouting staff are available for tournament scouting.";
            return;
        }

        ApplyScoutingOperationResult(_scoutingOperations.AssignScoutToPlayer(
            _registry,
            ScenarioSnapshot,
            scout.ScoutPersonId,
            playerPersonId,
            ScoutingOperationPriority.High,
            $"Tournament scouting: evaluate pressure, leadership, consistency, and big-game performance for {FindPersonName(playerPersonId)}.",
            Snapshot.CurrentDate.AddDays(7)));
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
        var candidate = StaffMarket.AvailableCandidates
            .OrderByDescending(candidate => candidate.Candidate.RoleFit + candidate.Candidate.DepartmentFit + candidate.Reputation)
            .FirstOrDefault();
        if (candidate is null)
        {
            LatestSummary = "No staff candidate is available to hire.";
            return;
        }

        ApplyStaffOfficeResult(_staffOffice.HireCandidate(_registry, ScenarioSnapshot, candidate.CandidateId));
    }

    public void GenerateStaffCandidates() =>
        RefreshStaffMarket();

    public void RefreshStaffMarket()
    {
        var next = new StaffMarketService().EnsureMarket(_registry, ScenarioSnapshot);
        ScenarioSnapshot = next;
        Snapshot = next.AlphaSnapshot;
        LatestSummary = $"Staff market available: {StaffMarket.AvailableCandidates.Count} candidate(s), {StaffMarket.MovementHistory.Count} movement record(s).";
    }

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
            LatestSummary = "No medical staff member is employed yet. Review the staff market and hire a medical candidate first.";
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
        var lifeCycle = ScenarioSnapshot.StaffCareerSummaries.FirstOrDefault(summary => summary.PersonId == personId)
            ?? _staffLifeCycle.FindSummary(_staffLifeCycle.EnsureLifeCycle(ScenarioSnapshot), personId);
        if (lifeCycle is not null)
        {
            builder.AppendLine("Career");
            builder.AppendLine($"Life stage: {lifeCycle.LifeStage}");
            builder.AppendLine($"Career phase: {lifeCycle.CareerPhase}");
            builder.AppendLine($"Career reputation: {lifeCycle.Reputation}");
            builder.AppendLine($"Legacy score: {lifeCycle.LegacyScore}");
            builder.AppendLine($"Summary: {lifeCycle.CareerSummaryText}");
            builder.AppendLine($"Personal legacy: {lifeCycle.PersonalLegacy}");
            builder.AppendLine($"Promotion readiness: {lifeCycle.PromotionReadiness}");
            builder.AppendLine($"Career concern: {lifeCycle.ConcernSummary}");
            builder.AppendLine($"Organizations: {string.Join(", ", lifeCycle.Organizations.Take(4))}");
            builder.AppendLine($"Roles: {string.Join(", ", lifeCycle.Roles.Take(5))}");
            if (lifeCycle.PlayersDeveloped.Count > 0)
            {
                builder.AppendLine($"Players developed: {string.Join(", ", lifeCycle.PlayersDeveloped.Take(4))}");
            }

            if (lifeCycle.PlayersDiscovered.Count > 0)
            {
                builder.AppendLine($"Players discovered: {string.Join(", ", lifeCycle.PlayersDiscovered.Take(4))}");
            }

            if (lifeCycle.CoachingTree.Count > 0)
            {
                builder.AppendLine($"Coaching tree: {string.Join(", ", lifeCycle.CoachingTree.Take(4))}");
            }

            builder.AppendLine();
        }

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
        var market = StaffMarketCandidateFor(personId);
        if (market is null)
        {
            return "Candidate is no longer available.";
        }

        var candidate = market.Candidate;
        var peers = StaffMarketCandidates
            .Where(peer => peer.DesiredRole == candidate.StaffMember.CurrentRole)
            .OrderByDescending(peer => peer.Candidate.RoleFit + peer.Candidate.DepartmentFit + peer.Reputation)
            .Take(3)
            .Select(peer => $"{peer.Name}: {peer.Status}, fit {peer.Candidate.RoleFit}/{peer.Candidate.DepartmentFit}, rep {peer.Reputation}, salary {peer.SalaryAsk.AnnualAmount:C0}")
            .ToArray();

        return $"{candidate.Person.Identity.DisplayName} - {candidate.StaffMember.CurrentRole}\n"
            + $"Expected salary: {candidate.ExpectedSalary.AnnualAmount:C0}\n"
            + $"Current employer: {market.CurrentEmployer}\n"
            + $"Market status: {market.Status}; reason {market.ReasonAvailable}\n"
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
        var market = StaffMarketCandidateFor(candidatePersonId);
        if (market is null)
        {
            LatestSummary = "Selected staff candidate is no longer available.";
            return;
        }

        if (!market.CanBeHired)
        {
            LatestSummary = $"{market.Name} is {market.Status} with {market.CurrentEmployer}. Approach Candidate is a placeholder for this alpha pass.";
            return;
        }

        ApplyStaffOfficeResult(_staffOffice.HireCandidate(_registry, ScenarioSnapshot, market.CandidateId));
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

        var draftBioPosition = ScenarioSnapshot.AlphaSnapshot.DraftBoard.Entries
            .FirstOrDefault(entry => entry.ProspectPersonId == personId)?.Bio?.Position;
        if (draftBioPosition is not null)
        {
            return draftBioPosition.Value;
        }

        var freeAgentPosition = ScenarioSnapshot.FreeAgentMarket?.Find(personId)?.Position;
        if (freeAgentPosition is not null)
        {
            return freeAgentPosition.Value;
        }

        var tradeBlockPosition = ScenarioSnapshot.TradeBlock?.Find(personId)?.Position;
        if (tradeBlockPosition is not null)
        {
            return tradeBlockPosition.Value;
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
        var assignment = LineupAssignment(personId);
        if (assignment is not null)
        {
            return assignment.PlayerType;
        }

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

    public Lineup CurrentLineup
    {
        get
        {
            if (ScenarioSnapshot.CurrentLineup is not null)
            {
                return ScenarioSnapshot.CurrentLineup;
            }

            var updated = _lineups.EnsureLineup(ScenarioSnapshot);
            ScenarioSnapshot = updated;
            Snapshot = updated.AlphaSnapshot;
            return updated.CurrentLineup!;
        }
    }

    public LineChemistryReport LineChemistryReport
    {
        get
        {
            if (ScenarioSnapshot.CurrentLineChemistry is not null)
            {
                return ScenarioSnapshot.CurrentLineChemistry;
            }

            var updated = _lineChemistry.EnsureChemistry(ScenarioSnapshot);
            ScenarioSnapshot = updated;
            Snapshot = updated.AlphaSnapshot;
            return updated.CurrentLineChemistry!;
        }
    }

    public GameUsage CurrentGameUsage
    {
        get
        {
            if (ScenarioSnapshot.CurrentGameUsage is not null)
            {
                return ScenarioSnapshot.CurrentGameUsage;
            }

            var updated = _gameUsage.EnsureGameUsage(ScenarioSnapshot);
            ScenarioSnapshot = updated;
            Snapshot = updated.AlphaSnapshot;
            return updated.CurrentGameUsage!;
        }
    }

    public TeamTactics CurrentTactics
    {
        get
        {
            if (ScenarioSnapshot.CurrentTactics is not null)
            {
                return ScenarioSnapshot.CurrentTactics;
            }

            var updated = _tactics.EnsureTactics(ScenarioSnapshot);
            ScenarioSnapshot = updated;
            Snapshot = updated.AlphaSnapshot;
            return updated.CurrentTactics!;
        }
    }

    public LineChemistry? LineChemistryUnit(string unitId) =>
        LineChemistryReport.Units.FirstOrDefault(unit => unit.UnitId == unitId);

    public LineChemistry? ChemistryForSlot(LineupSlot slot)
    {
        var unitId = slot switch
        {
            LineupSlot.Line1LW or LineupSlot.Line1C or LineupSlot.Line1RW => "forward-line:1",
            LineupSlot.Line2LW or LineupSlot.Line2C or LineupSlot.Line2RW => "forward-line:2",
            LineupSlot.Line3LW or LineupSlot.Line3C or LineupSlot.Line3RW => "forward-line:3",
            LineupSlot.Line4LW or LineupSlot.Line4C or LineupSlot.Line4RW => "forward-line:4",
            LineupSlot.Pair1LD or LineupSlot.Pair1RD => "defense-pair:1",
            LineupSlot.Pair2LD or LineupSlot.Pair2RD => "defense-pair:2",
            LineupSlot.Pair3LD or LineupSlot.Pair3RD => "defense-pair:3",
            LineupSlot.Starter or LineupSlot.Backup => "goalie-depth",
            _ => string.Empty
        };
        return string.IsNullOrWhiteSpace(unitId) ? null : LineChemistryUnit(unitId);
    }

    public string ChemistryTextForSlot(LineupSlot slot)
    {
        var chemistry = ChemistryForSlot(slot);
        return chemistry is null ? "Chemistry: Not evaluated" : $"Chemistry: {chemistry.Score.Grade} ({chemistry.Score.Value})";
    }

    public string LineChemistryTextForPerson(string personId)
    {
        var chemistry = _lineChemistry.FindChemistryForPerson(LineChemistryReport, personId);
        return chemistry is null
            ? "Line chemistry: Not in lineup"
            : $"Line chemistry: {chemistry.Score.Grade} ({chemistry.Score.Value}) on {chemistry.Label}";
    }

    public LineupRoleAssignment? LineupAssignment(string personId) =>
        CurrentLineup.Assignments.FirstOrDefault(assignment => assignment.PersonId == personId);

    public string CurrentLineupRole(string personId)
    {
        var assignment = LineupAssignment(personId);
        return assignment is null ? "Unassigned" : LineupDisplay.Role(assignment.CurrentRole);
    }

    public string PotentialLineupRole(string personId)
    {
        var assignment = LineupAssignment(personId);
        return assignment is null ? "Unassigned" : LineupDisplay.Role(assignment.PotentialRole);
    }

    public string CurrentLinePair(string personId)
    {
        var assignment = LineupAssignment(personId);
        return assignment?.SlotLabel ?? "Not in lineup";
    }

    public string DevelopmentStageText(string personId)
    {
        var assignment = LineupAssignment(personId);
        if (assignment?.DevelopmentStage is not null)
        {
            return assignment.DevelopmentStage.Value.ToString();
        }

        return Snapshot.DevelopmentProfiles.FirstOrDefault(profile => profile.PersonId == personId)?.Stage.ToString() ?? "Unknown";
    }

    public string LineupDevelopmentImpactText(string personId) =>
        $"{_lineups.BuildDevelopmentImpact(ScenarioSnapshot, personId).Summary} {GameUsageDevelopmentImpactText(personId)} {TacticsDevelopmentImpactText(personId)}";

    public string GameUsageDevelopmentImpactText(string personId) =>
        _gameUsage.BuildDevelopmentImpact(ScenarioSnapshot, personId).Summary;

    public string TacticsDevelopmentImpactText(string personId) =>
        _tactics.BuildPlayerImpact(ScenarioSnapshot, personId).Summary;

    public string LineupCoachNote(string personId) =>
        LineupAssignment(personId)?.CoachNote ?? "No coach lineup note yet.";

    public IReadOnlyList<LineupSlot> LineupSlots { get; } = new[]
    {
        LineupSlot.Line1LW,
        LineupSlot.Line1C,
        LineupSlot.Line1RW,
        LineupSlot.Line2LW,
        LineupSlot.Line2C,
        LineupSlot.Line2RW,
        LineupSlot.Line3LW,
        LineupSlot.Line3C,
        LineupSlot.Line3RW,
        LineupSlot.Line4LW,
        LineupSlot.Line4C,
        LineupSlot.Line4RW,
        LineupSlot.Pair1LD,
        LineupSlot.Pair1RD,
        LineupSlot.Pair2LD,
        LineupSlot.Pair2RD,
        LineupSlot.Pair3LD,
        LineupSlot.Pair3RD,
        LineupSlot.Starter,
        LineupSlot.Backup
    };

    public string LineupValidationText => _lineups.ValidateLineup(ScenarioSnapshot).Message;

    public string LineupPositionText(LineupSlot slot) =>
        LineupDisplay.Position(slot switch
        {
            LineupSlot.Line1LW or LineupSlot.Line2LW or LineupSlot.Line3LW or LineupSlot.Line4LW => LineupPosition.LeftWing,
            LineupSlot.Line1C or LineupSlot.Line2C or LineupSlot.Line3C or LineupSlot.Line4C => LineupPosition.Center,
            LineupSlot.Line1RW or LineupSlot.Line2RW or LineupSlot.Line3RW or LineupSlot.Line4RW => LineupPosition.RightWing,
            LineupSlot.Pair1LD or LineupSlot.Pair2LD or LineupSlot.Pair3LD => LineupPosition.LeftDefense,
            LineupSlot.Pair1RD or LineupSlot.Pair2RD or LineupSlot.Pair3RD => LineupPosition.RightDefense,
            LineupSlot.Starter => LineupPosition.StarterGoalie,
            LineupSlot.Backup => LineupPosition.BackupGoalie,
            _ => LineupPosition.HealthyScratch
        });

    public IReadOnlyList<LineupRoleAssignment> EligibleLineupReplacements(LineupSlot slot) =>
        _lineups.EligiblePlayersForSlot(ScenarioSnapshot, slot);

    public PlayerLineupUsage? LineupUsageFor(string personId) =>
        CurrentLineup.Usage.FirstOrDefault(usage => usage.PersonId == personId);

    public string PromisedRoleText(string personId)
    {
        var promise = CurrentLineup.RolePromises.FirstOrDefault(promise => promise.PersonId == personId);
        return promise is null ? "No explicit role promise" : $"{LineupDisplay.Role(promise.PromisedRole)} ({promise.Status})";
    }

    public string ExpectedRoleText(string personId)
    {
        var usage = LineupUsageFor(personId);
        return usage is null ? "Not established" : LineupDisplay.Role(usage.ExpectedRole);
    }

    public string RoleSatisfactionText(string personId) =>
        LineupUsageFor(personId)?.Satisfaction.ToString() ?? "Neutral";

    public string PromiseStatusText(string personId) =>
        LineupUsageFor(personId)?.PromiseStatus.ToString() ?? "NotYetEvaluated";

    public string LineupSlotPlayerText(LineupSlot slot) =>
        CurrentLineup.Assignments.FirstOrDefault(assignment => assignment.Slot == slot)?.PlayerName ?? "open";

    public void AutoFillLineup() => ApplyLineupResult(_lineups.AutoFillLineup(ScenarioSnapshot));

    public void AutoFillGameUsage() => ApplyGameUsageResult(_gameUsage.AutoFillGameUsage(ScenarioSnapshot));

    public void AutoSetTacticsFromCoach() => ApplyTacticsResult(_tactics.AutoSetFromCoach(ScenarioSnapshot));

    public void SetTacticalStyle(TacticalStyle style) => ApplyTacticsResult(_tactics.SetStyle(ScenarioSnapshot, style));

    public void CycleForecheck() =>
        ApplyTacticsResult(_tactics.SetForecheck(ScenarioSnapshot, Next(CurrentTactics.Settings.Forecheck)));

    public void CycleNeutralZone() =>
        ApplyTacticsResult(_tactics.SetNeutralZone(ScenarioSnapshot, Next(CurrentTactics.Settings.NeutralZone)));

    public void CycleDefensiveZone() =>
        ApplyTacticsResult(_tactics.SetDefensiveZone(ScenarioSnapshot, Next(CurrentTactics.Settings.DefensiveZone)));

    public void CycleBreakout() =>
        ApplyTacticsResult(_tactics.SetBreakout(ScenarioSnapshot, Next(CurrentTactics.Settings.Breakout)));

    public void CycleShotPreference() =>
        ApplyTacticsResult(_tactics.SetShotPreference(ScenarioSnapshot, Next(CurrentTactics.Settings.ShotPreference)));

    public void CyclePhysicality() =>
        ApplyTacticsResult(_tactics.SetPhysicality(ScenarioSnapshot, Next(CurrentTactics.Settings.Physicality)));

    public void CycleRisk() =>
        ApplyTacticsResult(_tactics.SetRisk(ScenarioSnapshot, Next(CurrentTactics.Settings.RiskLevel)));

    public void CyclePowerPlayTactic() =>
        ApplyTacticsResult(_tactics.SetPowerPlayStyle(ScenarioSnapshot, Next(CurrentTactics.Settings.PowerPlayStyle)));

    public void CyclePenaltyKillTactic() =>
        ApplyTacticsResult(_tactics.SetPenaltyKillStyle(ScenarioSnapshot, Next(CurrentTactics.Settings.PenaltyKillStyle)));

    public void AssignNextGameUsage(string unitId)
    {
        try
        {
            ApplyGameUsageResult(unitId switch
            {
                "power-play:1" => _gameUsage.AssignPowerPlaySlot(ScenarioSnapshot, 1, PowerPlaySlot.Center, NextForwardForUsage().PersonId),
                "power-play:2" => _gameUsage.AssignPowerPlaySlot(ScenarioSnapshot, 2, PowerPlaySlot.RightWing, NextForwardForUsage(skip: 1).PersonId),
                "penalty-kill:1" => _gameUsage.AssignPenaltyKillSlot(ScenarioSnapshot, 1, PenaltyKillSlot.LeftWing, NextCheckingForwardForUsage().PersonId),
                "penalty-kill:2" => _gameUsage.AssignPenaltyKillSlot(ScenarioSnapshot, 2, PenaltyKillSlot.RightWing, NextCheckingForwardForUsage(skip: 1).PersonId),
                "shootout" => _gameUsage.MoveShootoutPlayer(ScenarioSnapshot, NextForwardForUsage(skip: 2).PersonId, -1),
                _ => _gameUsage.AutoFillGameUsage(ScenarioSnapshot)
            });
        }
        catch (Exception ex)
        {
            LatestSummary = ex.Message;
        }
    }

    public void RemoveGameUsage(string unitId)
    {
        ApplyGameUsageResult(_gameUsage.AutoFillGameUsage(ScenarioSnapshot));
        LatestSummary = $"Reset {unitId} from current lineup.";
    }

    public void SwapGameUsage(string unitId)
    {
        try
        {
            ApplyGameUsageResult(unitId switch
            {
                "shootout" => _gameUsage.MoveShootoutPlayer(ScenarioSnapshot, CurrentGameUsage.SpecialTeams.ShootoutOrder.Shooters.Skip(1).FirstOrDefault()?.PersonId ?? NextForwardForUsage().PersonId, -1),
                "power-play:1" => _gameUsage.AssignPowerPlaySlot(ScenarioSnapshot, 1, PowerPlaySlot.LeftWing, NextForwardForUsage(skip: 2).PersonId),
                "power-play:2" => _gameUsage.AssignPowerPlaySlot(ScenarioSnapshot, 2, PowerPlaySlot.LeftWing, NextForwardForUsage(skip: 3).PersonId),
                "penalty-kill:1" => _gameUsage.AssignPenaltyKillSlot(ScenarioSnapshot, 1, PenaltyKillSlot.LeftWing, NextCheckingForwardForUsage(skip: 2).PersonId),
                "penalty-kill:2" => _gameUsage.AssignPenaltyKillSlot(ScenarioSnapshot, 2, PenaltyKillSlot.LeftWing, NextCheckingForwardForUsage(skip: 3).PersonId),
                _ => _gameUsage.AutoFillGameUsage(ScenarioSnapshot)
            });
        }
        catch (Exception ex)
        {
            LatestSummary = ex.Message;
        }
    }

    public void AssignLineupSlot(LineupSlot slot, string personId) =>
        ApplyLineupResult(_lineups.AssignPlayerToSlot(ScenarioSnapshot, slot, personId));

    public void RemoveLineupSlot(LineupSlot slot) =>
        ApplyLineupResult(_lineups.RemovePlayerFromSlot(ScenarioSnapshot, slot));

    public void SwapLineupSlots(LineupSlot sourceSlot, LineupSlot targetSlot) =>
        ApplyLineupResult(_lineups.SwapPlayers(ScenarioSnapshot, sourceSlot, targetSlot));

    private void ApplyLineupResult(LineupManagementResult result)
    {
        if (result.Success)
        {
            ScenarioSnapshot = _tactics.EnsureTactics(_gameUsage.EnsureGameUsage(_lineChemistry.EnsureChemistry(result.ScenarioSnapshot)));
            Snapshot = ScenarioSnapshot.AlphaSnapshot;
            EnsureSelectedDossierStillExists();
        }

        LastProcessedEventCount = 0;
        LatestSummary = result.Validation.IsValid ? result.Message : $"{result.Message} {result.Validation.Message}";
    }

    private void ApplyGameUsageResult(GameUsageManagementResult result)
    {
        if (result.Success)
        {
            ScenarioSnapshot = _tactics.EnsureTactics(result.ScenarioSnapshot with { CurrentTactics = null });
            Snapshot = ScenarioSnapshot.AlphaSnapshot;
            EnsureSelectedDossierStillExists();
        }

        LastProcessedEventCount = 0;
        LatestSummary = result.Message;
    }

    private void ApplyTacticsResult(TacticsManagementResult result)
    {
        if (result.Success)
        {
            ScenarioSnapshot = result.ScenarioSnapshot;
            Snapshot = result.ScenarioSnapshot.AlphaSnapshot;
            EnsureSelectedDossierStillExists();
        }

        LastProcessedEventCount = 0;
        LatestSummary = result.Message;
    }

    private static TEnum Next<TEnum>(TEnum current)
        where TEnum : struct, Enum
    {
        var values = Enum.GetValues<TEnum>();
        var index = Array.IndexOf(values, current);
        return values[(index + 1) % values.Length];
    }

    private LineupRoleAssignment NextForwardForUsage(int skip = 0) =>
        CurrentLineup.Assignments
            .Where(assignment => assignment.Slot != LineupSlot.HealthyScratch && assignment.Position is LegacyEngine.Rosters.RosterPosition.Center or LegacyEngine.Rosters.RosterPosition.LeftWing or LegacyEngine.Rosters.RosterPosition.RightWing)
            .OrderBy(assignment => assignment.Slot)
            .Skip(skip)
            .First();

    private LineupRoleAssignment NextCheckingForwardForUsage(int skip = 0) =>
        CurrentLineup.Assignments
            .Where(assignment => assignment.Slot != LineupSlot.HealthyScratch && assignment.Position is LegacyEngine.Rosters.RosterPosition.Center or LegacyEngine.Rosters.RosterPosition.LeftWing or LegacyEngine.Rosters.RosterPosition.RightWing)
            .OrderByDescending(assignment => assignment.PlayerType.Contains("Checking", StringComparison.OrdinalIgnoreCase) || assignment.PlayerType.Contains("Defensive", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(assignment => assignment.Age ?? 0)
            .Skip(skip)
            .First();

    public string LineupRoleTradeText(string personId)
    {
        var assignment = LineupAssignment(personId);
        if (assignment is null)
        {
            return "Trade target type: unassigned/buried";
        }

        var type = assignment.CurrentRole switch
        {
            LineupRole.FranchiseForward or LineupRole.FirstLineForward or LineupRole.TopSixForward => "top-line player",
            LineupRole.FranchiseDefenseman or LineupRole.TopPairDefenseman => "top-pair defenseman",
            LineupRole.ProspectForward or LineupRole.ProspectDefenseman or LineupRole.ProspectGoalie => "prospect",
            LineupRole.DepthForward or LineupRole.DepthDefenseman or LineupRole.DepthGoalie => "depth player",
            _ when assignment.Slot == LineupSlot.HealthyScratch => "buried player",
            _ => "roster player"
        };
        return $"Lineup role: {LineupDisplay.Role(assignment.CurrentRole)} | {assignment.SlotLabel} | Trade target type: {type}";
    }

    public string ContractRightsStatus(string personId)
    {
        var rights = ContractRightsDecisions.FirstOrDefault(decision => decision.PersonId == personId);
        if (rights is not null)
        {
            var deadline = rights.ExpiryRule is null ? "no deadline" : $"deadline {rights.ExpiryRule.Deadline:yyyy-MM-dd}";
            var offer = rights.QualifyingOffer is null ? string.Empty : $" | QO {rights.QualifyingOffer.RequiredSalary:C0}";
            return $"{DisplayRightsStatus(rights.RightsStatus)} | holder {rights.RightsHolderTeamName} | {deadline}{offer}";
        }

        var contract = ScenarioSnapshot.Contracts.Concat(Snapshot.Contracts)
            .DistinctBy(contract => contract.ContractId)
            .Where(contract => contract.PersonId == personId)
            .OrderByDescending(contract => contract.SignedOn ?? contract.OfferedOn)
            .FirstOrDefault();
        if (contract is not null)
        {
            if (contract.Status == ContractStatus.Expired || contract.Term.EndDate < Snapshot.CurrentDate)
            {
                return $"{contract.ContractType} expired {contract.Term.EndDate:yyyy-MM-dd} - renew or walk away.";
            }

            if (contract.Status == ContractStatus.Signed && contract.Term.EndDate <= Snapshot.CurrentDate.AddDays(30))
            {
                return $"{contract.ContractType} expires {contract.Term.EndDate:yyyy-MM-dd} - renewal decision soon.";
            }

            return $"{contract.ContractType} {contract.Status} through {contract.Term.EndDate:yyyy-MM-dd}";
        }

        var prospect = ScenarioSnapshot.ProspectRights.FirstOrDefault(prospect => prospect.ProspectPersonId == personId);
        return prospect is null ? "No contract/rights record" : $"Draft rights {prospect.Status}";
    }

    public string PlayerContractDetailsText(string personId)
    {
        var contract = ScenarioSnapshot.Contracts.Concat(Snapshot.Contracts)
            .DistinctBy(contract => contract.ContractId)
            .Where(contract => contract.PersonId == personId)
            .OrderByDescending(contract => contract.Status == ContractStatus.Signed && contract.Term.EndDate > Snapshot.CurrentDate)
            .ThenByDescending(contract => contract.Term.EndDate)
            .FirstOrDefault();

        var rightsText = ContractRightsStatus(personId);
        if (contract is null)
        {
            return $"No active contract found.\nRights: {rightsText}";
        }

        var active = contract.Status == ContractStatus.Signed && contract.Term.EndDate > Snapshot.CurrentDate;
        var status = active
            ? "Active"
            : contract.Status == ContractStatus.Signed && contract.Term.EndDate <= Snapshot.CurrentDate
                ? "Expired"
                : contract.Status.ToString();
        var clauses = contract.Clauses.Count == 0
            ? "none"
            : string.Join(", ", contract.Clauses.Select(clause => $"{clause.ClauseType}: {clause.Description}"));
        var bonus = contract.Money.SigningBonus > 0 ? $"\nSigning bonus: {contract.Money.SigningBonus:C0}" : string.Empty;
        var expiryNote = active
            ? $"Counts against player payroll/cap through {contract.Term.EndDate:yyyy-MM-dd}."
            : "Does not count against current player payroll/cap; review FA/RFA rights status.";

        return $"Type: {contract.ContractType}\nStatus: {status}\nStart: {contract.Term.StartDate:yyyy-MM-dd}\nEnd: {contract.Term.EndDate:yyyy-MM-dd}\nSalary/stipend: {contract.Money.SalaryOrStipend:C0} {contract.Money.Currency}{bonus}\nClauses: {clauses}\nRights: {rightsText}\nCap/payroll note: {expiryNote}";
    }

    public bool HasContractRightsDecision(string personId) =>
        ContractRightsDecisions.Any(decision => decision.PersonId == personId);

    public bool CanQualifyRights(string personId) =>
        ContractRightsDecisions.Any(decision => decision.PersonId == personId
            && decision.RightsStatus is FreeAgentRightsStatus.PendingRfa or FreeAgentRightsStatus.RestrictedFreeAgent or FreeAgentRightsStatus.RightsHeld);

    public bool CanDeclineRights(string personId) =>
        ContractRightsDecisions.Any(decision => decision.PersonId == personId
            && decision.RightsStatus is FreeAgentRightsStatus.PendingRfa or FreeAgentRightsStatus.PendingUfa or FreeAgentRightsStatus.RestrictedFreeAgent);

    public bool CanFileArbitration(string personId) =>
        ArbitrationCases.Any(item => item.PersonId == personId && item.Status == ArbitrationCaseStatus.Eligible);

    public bool CanSettleArbitration(string personId) =>
        ArbitrationCases.Any(item => item.PersonId == personId && item.IsOpen);

    public bool CanAcceptArbitrationAward(string personId) =>
        ArbitrationCases.Any(item => item.PersonId == personId && item.IsOpen && item.Award is not null);

    public bool CanWalkAwayArbitration(string personId)
    {
        var rules = (_registry.Rulebook ?? ScenarioSnapshot.LeagueProfile.Rulebook).ArbitrationRules;
        return rules?.WalkAwayAllowed == true
            && ArbitrationCases.Any(item => item.PersonId == personId && item.IsOpen);
    }

    public bool CanCalculateBuyout(string personId) =>
        BuyoutEligibility.Any(item => item.PersonId == personId && item.Status == BuyoutStatus.Eligible);

    public bool CanConfirmBuyout(string personId) =>
        ScenarioSnapshot.ContractBuyouts.Any(item => item.PersonId == personId && item.Status == BuyoutStatus.PendingConfirmation);

    public bool CanCancelBuyout(string personId) =>
        CanConfirmBuyout(personId);

    public bool CanSubmitOfferSheet(string personId) =>
        OfferSheetEligibility.Any(item => item.PersonId == personId && item.Status == OfferSheetStatus.Eligible);

    public bool CanMatchOfferSheet(string personId) =>
        OfferSheets.Any(item => item.PersonId == personId && item.IsActive && item.RightsHolderOrganizationId == ScenarioSnapshot.Organization.OrganizationId);

    public bool CanDeclineOfferSheet(string personId) =>
        CanMatchOfferSheet(personId);

    public string DisplayRightsStatus(FreeAgentRightsStatus status) =>
        status switch
        {
            FreeAgentRightsStatus.UnderContract => "Under Contract",
            FreeAgentRightsStatus.PendingRfa => "Pending RFA",
            FreeAgentRightsStatus.RestrictedFreeAgent => "Restricted Free Agent",
            FreeAgentRightsStatus.PendingUfa => "Pending UFA",
            FreeAgentRightsStatus.UnrestrictedFreeAgent => "Unrestricted Free Agent",
            FreeAgentRightsStatus.NotQualified => "Not Qualified",
            FreeAgentRightsStatus.RightsHeld => "Rights Held",
            FreeAgentRightsStatus.RightsReleased => "Rights Released",
            FreeAgentRightsStatus.SignedElsewhere => "Signed Elsewhere",
            _ => status.ToString()
        };

    public string DisplayArbitrationStatus(ArbitrationCaseStatus status) =>
        status switch
        {
            ArbitrationCaseStatus.NotEligible => "Not Eligible",
            ArbitrationCaseStatus.PlayerFiled => "Player Filed",
            ArbitrationCaseStatus.TeamFiled => "Team Filed",
            ArbitrationCaseStatus.HearingScheduled => "Hearing Scheduled",
            ArbitrationCaseStatus.SettledBeforeHearing => "Settled Before Hearing",
            ArbitrationCaseStatus.AwardIssued => "Award Issued",
            ArbitrationCaseStatus.WalkedAway => "Walked Away",
            _ => status.ToString()
        };

    public string DisplayBuyoutStatus(BuyoutStatus status) =>
        status switch
        {
            BuyoutStatus.NotEligible => "Not Eligible",
            BuyoutStatus.PendingConfirmation => "Pending Confirmation",
            BuyoutStatus.ExpiredWindow => "Expired Window",
            _ => status.ToString()
        };

    public string DisplayOfferSheetStatus(OfferSheetStatus status) =>
        status switch
        {
            OfferSheetStatus.NotEligible => "Not Eligible",
            OfferSheetStatus.AcceptedByPlayer => "Accepted By Player",
            OfferSheetStatus.MatchedByTeam => "Matched By Team",
            OfferSheetStatus.DeclinedByPlayer => "Declined By Player",
            OfferSheetStatus.CompensationRequired => "Compensation Required",
            _ => status.ToString()
        };

    public WaiverEligibility WaiverEligibilityFor(string personId) =>
        _waivers.EvaluateEligibility(ScenarioSnapshot, personId, _registry.Rulebook ?? ScenarioSnapshot.LeagueProfile.Rulebook);

    public string WaiverStatusText(string personId)
    {
        var eligibility = WaiverEligibilityFor(personId);
        return $"{eligibility.Status} - {eligibility.Reason}";
    }

    public bool CanAssignPlayerToAffiliate(string personId)
    {
        var eligibility = WaiverEligibilityFor(personId);
        return eligibility.CanAssignToAffiliate && !eligibility.CanRecallFromAffiliate;
    }

    public bool CanPlacePlayerOnWaivers(string personId)
    {
        var eligibility = WaiverEligibilityFor(personId);
        return eligibility.WaiversEnabled && eligibility.RequiresWaivers && !ScenarioSnapshot.WaiverWire.OpenTransactions.Any(transaction => transaction.PersonId == personId);
    }

    public bool CanRecallPlayerFromAffiliate(string personId) =>
        WaiverEligibilityFor(personId).CanRecallFromAffiliate;

    public void AssignPlayerToAffiliateFor(string personId) =>
        ApplyWaiverResult(_waivers.AssignToAffiliate(_registry, ScenarioSnapshot, personId, "GM approved affiliate assignment."));

    public void PlacePlayerOnWaiversFor(string personId) =>
        ApplyWaiverResult(_waivers.PlaceOnWaivers(_registry, ScenarioSnapshot, personId, "GM requested affiliate assignment; rulebook requires waivers."));

    public void RecallPlayerFromAffiliateFor(string personId) =>
        ApplyWaiverResult(_waivers.RecallFromAffiliate(_registry, ScenarioSnapshot, personId, "GM recalled player from affiliate."));

    public void QualifyRightsFor(string personId) =>
        ApplyRightsResult(_rfaUfa.IssueQualifyingOffer(_registry, ScenarioSnapshot, personId));

    public void DeclineRightsFor(string personId) =>
        ApplyRightsResult(_rfaUfa.DeclineQualifyingOffer(_registry, ScenarioSnapshot, personId));

    public void FileArbitrationFor(string personId) =>
        ApplyArbitrationResult(_arbitration.FileTeamArbitration(_registry, ScenarioSnapshot, personId));

    public void SettleArbitrationFor(string personId) =>
        ApplyArbitrationResult(_arbitration.NegotiateSettlement(_registry, ScenarioSnapshot, personId));

    public void AcceptArbitrationAwardFor(string personId) =>
        ApplyArbitrationResult(_arbitration.AcceptAward(_registry, ScenarioSnapshot, personId));

    public void WalkAwayArbitrationFor(string personId) =>
        ApplyArbitrationResult(_arbitration.WalkAway(_registry, ScenarioSnapshot, personId));

    public void CalculateBuyoutFor(string personId) =>
        ApplyBuyoutResult(_buyouts.CalculateBuyout(_registry, ScenarioSnapshot, personId));

    public void ConfirmBuyoutFor(string personId) =>
        ApplyBuyoutResult(_buyouts.ConfirmBuyout(_registry, ScenarioSnapshot, personId));

    public void CancelBuyoutFor(string personId) =>
        ApplyBuyoutResult(_buyouts.CancelBuyout(_registry, ScenarioSnapshot, personId));

    public void SubmitOfferSheetFor(string personId) =>
        ApplyOfferSheetResult(_offerSheets.SubmitOfferSheet(_registry, ScenarioSnapshot, personId));

    public void MatchOfferSheetFor(string personId) =>
        ApplyOfferSheetResult(_offerSheets.MatchOffer(_registry, ScenarioSnapshot, personId));

    public void DeclineOfferSheetFor(string personId) =>
        ApplyOfferSheetResult(_offerSheets.DeclineAndTakeCompensation(_registry, ScenarioSnapshot, personId));

    private void ApplyRightsResult(RightsDecisionResult result)
    {
        if (result.Success)
        {
            ScenarioSnapshot = result.ScenarioSnapshot;
            Snapshot = result.ScenarioSnapshot.AlphaSnapshot;
            AddInboxItems(result.InboxItems);
            AddLeagueTransactions(result.LeagueTransactions);
            EnsureSelectedDossierStillExists();
        }

        LastProcessedEventCount = 0;
        LatestSummary = result.Message;
    }

    private void ApplyArbitrationResult(ArbitrationResult result)
    {
        if (result.Success)
        {
            ScenarioSnapshot = result.ScenarioSnapshot;
            Snapshot = result.ScenarioSnapshot.AlphaSnapshot;
            AddInboxItems(result.InboxItems);
            AddLeagueTransactions(result.LeagueTransactions);
            EnsureSelectedDossierStillExists();
        }

        LastProcessedEventCount = 0;
        LatestSummary = result.Message;
    }

    private void ApplyBuyoutResult(BuyoutResult result)
    {
        if (result.Success)
        {
            ScenarioSnapshot = result.ScenarioSnapshot;
            Snapshot = result.ScenarioSnapshot.AlphaSnapshot;
            AddInboxItems(result.InboxItems);
            AddLeagueTransactions(result.LeagueTransactions);
            EnsureSelectedDossierStillExists();
        }

        LastProcessedEventCount = 0;
        LatestSummary = result.Message;
    }

    private void ApplyOfferSheetResult(OfferSheetResult result)
    {
        if (result.Success)
        {
            ScenarioSnapshot = result.ScenarioSnapshot;
            Snapshot = result.ScenarioSnapshot.AlphaSnapshot;
            AddInboxItems(result.InboxItems);
            AddLeagueTransactions(result.LeagueTransactions);
            EnsureSelectedDossierStillExists();
        }

        LastProcessedEventCount = 0;
        LatestSummary = result.Message;
    }

    private void ApplyWaiverResult(WaiverResult result)
    {
        ScenarioSnapshot = result.ScenarioSnapshot;
        Snapshot = ScenarioSnapshot.AlphaSnapshot;
        AddInboxItems(result.InboxItems);
        AddLeagueTransactions(result.LeagueTransactions);
        LatestSummary = result.Message;
    }

    public string LastSeasonStats(string personId)
    {
        var stat = ScenarioSnapshot.PriorSeasonStats
            .Where(stat => stat.PersonId == personId)
            .OrderByDescending(stat => stat.SeasonYear)
            .FirstOrDefault();
        return stat?.SummaryText ?? "No prior stats tracked";
    }

    public string CareerStatSummary(string personId)
    {
        var summary = ScenarioSnapshot.CareerStatSummaries.FirstOrDefault(summary => summary.PersonId == personId);
        return summary?.DisplaySummary ?? "No career summary tracked";
    }

    public string DevelopmentTrend(string personId)
    {
        var profile = Snapshot.DevelopmentProfiles.FirstOrDefault(profile => profile.PersonId == personId);
        if (profile is null)
        {
            return "No development profile";
        }

        var plan = DevelopmentPlanFor(personId);
        var planText = plan is null ? "no plan" : $"{string.Join("/", plan.FocusAreas)}; {plan.IceTimeRole}; morale {plan.Morale}";
        return $"{profile.Stage}, last updated {profile.LastUpdated:yyyy-MM-dd}; {planText}";
    }

    public PlayerRatingSnapshot RatingFor(string personId)
    {
        var rating = ScenarioSnapshot.PlayerRatings.FirstOrDefault(item => item.PersonId == personId);
        if (rating is not null)
        {
            return rating;
        }

        var updated = _ratings.EnsureRatings(new DevelopmentCurveService().EnsureCurves(new HockeyIntelligenceRatingService().EnsureRatings(ScenarioSnapshot)));
        ScenarioSnapshot = updated;
        Snapshot = updated.AlphaSnapshot;
        return updated.PlayerRatings.FirstOrDefault(item => item.PersonId == personId)
            ?? _ratings.BuildSnapshot(updated, personId);
    }

    public string RatingText(string personId)
    {
        var rating = RatingFor(personId);
        var intelligence = ScenarioSnapshot.ScoutedRatings.FirstOrDefault(item => item.PersonId == personId);
        return intelligence is null
            ? $"OVR {rating.Overall.Display} | POT {rating.Potential.Display}"
            : $"OVR {intelligence.Overall.Display} | POT {intelligence.Potential.Display} | {intelligence.ConfidenceColor}";
    }

    public string RatingContextText(string personId)
    {
        var rating = RatingFor(personId);
        return $"{rating.ShortDisplay} | {rating.RoleLabel} | source {rating.RatingSource}";
    }

    public int DevelopmentActionCount =>
        ScenarioSnapshot.DevelopmentRecommendations.Count(recommendation => recommendation.IsActive);

    public string DevelopmentPlanText(string personId)
    {
        var plan = DevelopmentPlanFor(personId);
        if (plan is null)
        {
            return "No development plan is currently tracked.";
        }

        var review = ScenarioSnapshot.DevelopmentReviews
            .Where(item => item.PersonId == personId)
            .OrderByDescending(item => item.ReviewDate)
            .FirstOrDefault();
        var recommendation = ScenarioSnapshot.DevelopmentRecommendations
            .Where(item => item.PersonId == personId && item.IsActive)
            .OrderByDescending(item => item.CreatedOn)
            .FirstOrDefault();
        var builder = new StringBuilder();
        builder.AppendLine($"Plan: {string.Join(", ", plan.FocusAreas)}");
        builder.AppendLine($"Ice-time role: {plan.IceTimeRole}");
        builder.AppendLine($"Confidence: {ConfidenceText(plan.Confidence)}");
        builder.AppendLine($"Morale: {plan.Morale}");
        builder.AppendLine($"Coach comments: {plan.CoachComment}");
        builder.AppendLine($"Progress graph: placeholder for monthly development trend.");
        if (review is not null)
        {
            builder.AppendLine($"Latest yearly review: {review.FutureProjection}");
        }

        if (recommendation is not null)
        {
            builder.AppendLine($"Coach recommendation: {recommendation.RecommendedAction}");
        }

        var attributeLines = new AttributeDevelopmentService().BuildDossierLines(ScenarioSnapshot, personId);
        foreach (var line in attributeLines.Take(7))
        {
            builder.AppendLine(line);
        }

        return builder.ToString().Trim();
    }

    public string DevelopmentReviewText(string personId)
    {
        var review = _developmentPlanning.GenerateYearlyReview(ScenarioSnapshot, personId);
        ScenarioSnapshot = _developmentPlanning.StoreYearlyReview(ScenarioSnapshot, review);
        Snapshot = ScenarioSnapshot.AlphaSnapshot;
        var builder = new StringBuilder();
        builder.AppendLine($"{review.PlayerName} - {review.SeasonYear} Development Review");
        builder.AppendLine($"Improved: {string.Join(", ", review.ImprovedThemes)}");
        builder.AppendLine($"Regressions: {(review.RegressionThemes.Count == 0 ? "none" : string.Join(", ", review.RegressionThemes))}");
        builder.AppendLine($"Strengths: {string.Join(", ", review.Strengths)}");
        builder.AppendLine($"Weaknesses: {string.Join(", ", review.Weaknesses)}");
        builder.AppendLine($"Coach: {review.CoachComment}");
        builder.AppendLine($"Scout: {review.ScoutComment}");
        builder.AppendLine($"GM: {review.GmComment}");
        builder.AppendLine($"Future projection: {review.FutureProjection}");
        LatestSummary = $"Development review generated for {review.PlayerName}.";
        return builder.ToString();
    }

    public void SetDevelopmentPlanFor(string personId, DevelopmentPlanFocus focus)
    {
        var existing = DevelopmentPlanFor(personId);
        var role = existing?.IceTimeRole ?? DevelopmentIceTimeRole.MiddleSix;
        var result = _developmentPlanning.SetPlan(_registry, ScenarioSnapshot, personId, new[] { focus, DevelopmentPlanFocus.Confidence }, role, $"GM set {focus} development focus.");
        ApplyDevelopmentResult(result);
    }

    public void SetDevelopmentRoleFor(string personId, DevelopmentIceTimeRole role)
    {
        var existing = DevelopmentPlanFor(personId);
        var focus = existing?.FocusAreas ?? new[] { DevelopmentPlanFocus.Balanced, DevelopmentPlanFocus.Confidence };
        var result = _developmentPlanning.SetPlan(_registry, ScenarioSnapshot, personId, focus, role, $"GM adjusted development role to {role}.");
        ApplyDevelopmentResult(result);
    }

    public void GenerateAttributeReportFor(string personId)
    {
        var result = new AttributeDevelopmentService().ApplyMonthlyDevelopment(
            _registry,
            ScenarioSnapshot,
            personId,
            new AttributeDevelopmentModifier(
                PersonAge(personId) ?? 18,
                Math.Max(0, ScenarioSnapshot.Season.Year - 2026),
                ScenarioSnapshot.LeagueProfile.Experience,
                DevelopmentPlanFor(personId)?.IceTimeRole ?? DevelopmentIceTimeRole.MiddleSix,
                PowerPlayUsage: false,
                PenaltyKillUsage: false,
                CoachSpecialty: null,
                DevelopmentStaffQuality: 65,
                Morale: 60,
                RelationshipTrust: 55,
                InjuryPenalty: HasActiveInjury(personId) ? 20 : 0,
                FatiguePenalty: 0,
                WorkEthic: 65,
                Coachability: 65,
                Professionalism: 60,
                TeamCulture: 60,
                RushedTooEarly: false,
                PoorRole: false,
                UpdateVisibleEstimate: true));
        ScenarioSnapshot = result.ScenarioSnapshot;
        Snapshot = ScenarioSnapshot.AlphaSnapshot;
        AddInboxItems(result.InboxItems);
        LatestSummary = result.Summary;
        EnsureSelectedDossierStillExists();
    }

    public void ApplyMedicalDecisionFor(string personId, ReturnToPlayOption option)
    {
        var result = _medicalHealth.ApplyReturnDecision(_registry, ScenarioSnapshot, personId, option);
        ScenarioSnapshot = result.ScenarioSnapshot;
        Snapshot = ScenarioSnapshot.AlphaSnapshot;
        AddInboxItems(result.InboxItems);
        LatestSummary = result.Message;
        EnsureSelectedDossierStillExists();
    }

    private PlayerDevelopmentPlan? DevelopmentPlanFor(string personId)
    {
        ScenarioSnapshot = _developmentPlanning.EnsureScenarioPlans(ScenarioSnapshot);
        Snapshot = ScenarioSnapshot.AlphaSnapshot;
        return ScenarioSnapshot.DevelopmentPlans.FirstOrDefault(plan => plan.PersonId == personId);
    }

    private void ApplyDevelopmentResult(DevelopmentV2Result result)
    {
        ScenarioSnapshot = result.ScenarioSnapshot;
        Snapshot = ScenarioSnapshot.AlphaSnapshot;
        AddInboxItems(result.InboxItems);
        LatestSummary = result.Message;
        EnsureSelectedDossierStillExists();
    }

    private static string ConfidenceText(int confidence) =>
        confidence >= 80 ? "Excellent" :
        confidence >= 65 ? "Good" :
        confidence >= 45 ? "Average" :
        confidence >= 30 ? "Poor" :
        "Terrible";

    public string InjuryStatus(string personId)
    {
        var injury = Snapshot.Injuries.FirstOrDefault(injury => injury.PersonId == personId && injury.IsActive);
        return injury is null ? "Available" : $"{injury.Severity} {injury.InjuryType}, {injury.Status}";
    }

    public string RegionTeamText(string personId)
    {
        var freeAgent = ScenarioSnapshot.FreeAgentMarket?.Find(personId);
        if (freeAgent is not null)
        {
            return freeAgent.PreviousTeam;
        }

        var tradeBlock = ScenarioSnapshot.TradeBlock?.Find(personId);
        if (tradeBlock is not null)
        {
            return $"{tradeBlock.TeamName} / trade block";
        }

        var draftBio = ScenarioSnapshot.AlphaSnapshot.DraftBoard.Entries
            .FirstOrDefault(entry => entry.ProspectPersonId == personId)?.Bio;
        if (draftBio is not null)
        {
            return $"{draftBio.CurrentTeam} / {draftBio.League}";
        }

        var role = Snapshot.People.FirstOrDefault(person => person.PersonId == personId)
            ?.ActiveRolesOn(Snapshot.CurrentDate)
            .FirstOrDefault();
        return role?.OrganizationId ?? Snapshot.Organization?.Name ?? Snapshot.OrganizationId;
    }

    public string DraftPositionText(DraftBoardEntry entry) =>
        PositionShort(entry.Bio?.Position ?? PersonPosition(entry.ProspectPersonId));

    public string DraftQuickScan(DraftBoardEntry entry)
    {
        var position = DraftPositionText(entry);
        var age = PersonAge(entry.ProspectPersonId)?.ToString() ?? "unknown";
        if (entry.Bio is null)
        {
            return $"{position} | Age {age} | {RegionTeamText(entry.ProspectPersonId)}";
        }

        return $"{position} | {entry.Bio.ShootsCatches} | {entry.Bio.HeightDisplay} | {entry.Bio.WeightDisplay} | Age {age} | {entry.Bio.CurrentTeam} / {entry.Bio.League}";
    }

    public DraftProspectIntelligenceCard DraftIntelligenceCard(string prospectPersonId) =>
        _draftIntelligence.BuildProspectCard(ScenarioSnapshot, prospectPersonId);

    public string DraftIntelligenceRowText(DraftBoardEntry entry) =>
        _draftIntelligence.BuildRowText(ScenarioSnapshot, entry);

    public string DraftAttributeLinesText(string prospectPersonId, int count = 8) =>
        string.Join(Environment.NewLine, _draftIntelligence.BuildAttributeSummaryLines(ScenarioSnapshot, prospectPersonId, count));

    public string DraftBoardViewText(DraftWarRoomViewType viewType)
    {
        var view = ScenarioSnapshot.DraftWarRoom.BoardViews.FirstOrDefault(view => view.ViewType == viewType)
            ?? _draftIntelligence.BuildBoardViews(ScenarioSnapshot).FirstOrDefault(view => view.ViewType == viewType);
        if (view is null)
        {
            return "No draft board view is available yet.";
        }

        var builder = new StringBuilder();
        builder.AppendLine(view.Title);
        builder.AppendLine(view.Summary);
        foreach (var row in view.Rows.Take(16))
        {
            builder.AppendLine($"  {row}");
        }

        return builder.ToString().Trim();
    }

    public string DraftAlertsText()
    {
        var alerts = ScenarioSnapshot.DraftWarRoom.IntelligenceAlerts.Count == 0
            ? _draftIntelligence.BuildAlerts(ScenarioSnapshot)
            : ScenarioSnapshot.DraftWarRoom.IntelligenceAlerts;
        if (alerts.Count == 0)
        {
            return "No draft intelligence alerts yet.";
        }

        return string.Join(
            Environment.NewLine,
            alerts.Take(12).Select(alert => $"{alert.AlertType}: {alert.ProspectName} - {alert.Summary} Recommended: {alert.RecommendedAction}"));
    }

    public string DraftCurrentPicture(DraftBoardEntry entry)
    {
        var position = DraftPositionText(entry);
        var rankBand = entry.Rank switch
        {
            <= 3 => "top-of-board",
            <= 10 => "high-priority",
            <= 25 => "draftable",
            _ => "watch-list"
        };

        return entry.ScoutingConfidence switch
        {
            ScoutingConfidenceLevel.VeryHigh or ScoutingConfidenceLevel.High => $"Current picture: clear {rankBand} {position}; staff have enough viewings to describe his present role.",
            ScoutingConfidenceLevel.Medium => $"Current picture: working read on a {rankBand} {position}; staff want another viewing to firm up present ability.",
            ScoutingConfidenceLevel.Low or ScoutingConfidenceLevel.Unknown or null => $"Current picture: basic {position} bio is known, but present ability is still lightly scouted.",
            _ => $"Current picture: staff are still building the read on this {position}."
        };
    }

    public string DraftFuturePicture(DraftBoardEntry entry)
    {
        var role = entry.Bio?.PotentialLineupProjection ?? "future role still forming";
        return $"Future projection: {role}; {entry.ProjectionText}";
    }

    public string PositionShortText(RosterPosition position) => PositionShort(position);

    private static string Signed(int value) => value >= 0 ? $"+{value}" : value.ToString();

    private static string PositionShort(RosterPosition position) =>
        position switch
        {
            RosterPosition.Center => "C",
            RosterPosition.LeftWing => "LW",
            RosterPosition.RightWing => "RW",
            RosterPosition.Defense => "D",
            RosterPosition.Goalie => "G",
            _ => "Unknown"
        };

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

    public string ScoutingConfidenceSummary(string personId)
    {
        var report = _scoutingIntelligence.BuildReportCards(ScenarioSnapshot, personId, _registry.Rulebook)
            .OrderByDescending(report => report.CreatedOn)
            .FirstOrDefault();
        return report is null ? "Confidence * Very Low" : $"Confidence {report.ConfidenceStars}";
    }

    public string ScoutingConfidenceText(string personId)
    {
        var report = ScenarioSnapshot.CompletedScoutingReports
            .Where(report => report.PlayerId == personId)
            .OrderByDescending(report => report.CreatedOn)
            .FirstOrDefault();
        return report is null ? ScoutingConfidenceSummary(personId) : $"{report.Confidence} confidence";
    }

    public string ScoutingReportHeadline(string personId)
    {
        var report = _scoutingIntelligence.BuildReportCards(ScenarioSnapshot, personId, _registry.Rulebook)
            .OrderByDescending(report => report.CreatedOn)
            .FirstOrDefault();
        return report is null ? "No intelligence report yet; assign a scout for evidence." : $"{report.Source}: {report.Recommendation}";
    }

    public string ScoutingKnowledgeText(string personId)
    {
        var lines = _scoutingIntelligence.BuildKnowledgeDossierLines(ScenarioSnapshot, personId)
            .Take(7)
            .ToArray();
        return string.Join(Environment.NewLine, lines);
    }

    public string ScoutingReportsText(string personId)
    {
        var reports = _scoutingIntelligence.BuildReportCards(ScenarioSnapshot, personId, _registry.Rulebook)
            .OrderByDescending(report => report.CreatedOn)
            .Take(5)
            .ToArray();
        if (reports.Length == 0)
        {
            return "No intelligence reports yet. Assign Scout, Scout Again, or Tournament to build evidence over time.";
        }

        var builder = new StringBuilder();
        foreach (var report in reports)
        {
            builder.AppendLine($"{report.Source} - {report.ScoutName} - {report.ConfidenceStars}");
            builder.AppendLine($"  Current: {report.CurrentPicture}");
            builder.AppendLine($"  Future: {report.FutureProjection}");
            builder.AppendLine($"  Recommendation: {report.Recommendation}");
            builder.AppendLine($"  Evidence: {string.Join(" ", report.Evidence.Take(2))}");
            builder.AppendLine($"  Concerns: {string.Join(" ", report.Concerns.Take(2))}");
            builder.AppendLine($"  Workload/Budget: {report.WorkloadNote} {report.BudgetNote}");
        }

        return builder.ToString().Trim();
    }

    public string ScoutingComparisonText(string personId)
    {
        var comparison = _scoutingIntelligence.CompareReports(ScenarioSnapshot, personId, _registry.Rulebook);
        var builder = new StringBuilder();
        builder.AppendLine($"{comparison.PlayerName}");
        builder.AppendLine(comparison.ConfidenceSummary);
        builder.AppendLine(comparison.RecommendationSummary);
        builder.AppendLine($"Agreements: {string.Join(" ", comparison.Agreements)}");
        builder.AppendLine($"Disagreements: {string.Join(" ", comparison.Disagreements)}");
        builder.AppendLine();
        builder.AppendLine("Reports compared:");
        foreach (var report in comparison.Reports)
        {
            builder.AppendLine($"- {report.ScoutName} ({report.Source}) {report.ConfidenceStars}: {report.Recommendation}");
        }

        return builder.ToString().Trim();
    }

    public string ScoutIntelligenceProfileText(string scoutPersonId)
    {
        var profile = ScoutIntelligenceProfiles.FirstOrDefault(profile => profile.ScoutPersonId == scoutPersonId);
        if (profile is null)
        {
            return "Scout intelligence profile unavailable.";
        }

        return $"{profile.Summary}\nTraits: {string.Join(", ", profile.Traits)}\nKnown regions: {string.Join(", ", profile.KnownRegions)}\nSpecialties: {string.Join(", ", profile.Specialties)}\nExperience: {profile.ExperiencePoints}; workload {profile.Workload}.\nBudget: {profile.BudgetSupport}";
    }

    public string ScoutCareerText(string scoutPersonId)
    {
        var career = _scoutingIntelligence.BuildScoutCareer(ScenarioSnapshot, scoutPersonId);
        var discoveries = career.DiscoveredPlayers.Count == 0
            ? "No credited discoveries yet."
            : string.Join(" ", career.DiscoveredPlayers.Take(3).Select(discovery => $"{discovery.PlayerName}: {discovery.Outcome}."));
        var development = _scoutingIntelligence.BuildScoutDevelopment(ScenarioSnapshot, scoutPersonId);
        return $"{career.Summary} {discoveries} {development.Summary}";
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

    public FreeAgent? FreeAgentFor(string personId) =>
        ScenarioSnapshot.FreeAgentMarket?.Find(personId);

    public string FreeAgentBudgetImpact(string personId)
    {
        var agent = FreeAgentFor(personId);
        if (agent is null)
        {
            return "No free-agent budget impact.";
        }

        var remaining = BudgetOverview.RemainingBudget;
        var afterAsk = remaining - agent.ContractAsk.AnnualAmount;
        var budgetText = afterAsk < 0
            ? $"Ask {agent.ContractAsk.AnnualAmount:C0}; would put hockey operations {Math.Abs(afterAsk):C0} over budget."
            : $"Ask {agent.ContractAsk.AnnualAmount:C0}; would leave {afterAsk:C0} in hockey operations budget.";
        var cap = new SalaryCapService().ProjectAfterSigning(
            ScenarioSnapshot,
            _registry.Rulebook ?? ScenarioSnapshot.LeagueProfile.Rulebook,
            agent.ContractAsk.AnnualAmount,
            agent.ContractAsk.TermYears);
        var capText = cap.Before.IsEnabled
            ? $" Current cap: {cap.Before.CapUsed:C0} used, {cap.Before.CapRemaining:C0} remaining. Cap after signing: {cap.After.CapUsed:C0} used, {cap.After.CapRemaining:C0} remaining. Cap warning: {(cap.IsCompliant ? "None" : string.Join(" ", cap.Reasons))}"
            : " Salary cap disabled by rulebook.";
        return budgetText + capText;
    }

    public string FreeAgentTopMotivations(string personId)
    {
        _ = FreeAgencyState;
        var motivations = _freeAgencyV2.TopMotivations(ScenarioSnapshot, personId)
            .Take(3)
            .Select(score => $"{MotivationLabel(score.Motivation)} {score.Importance}/100");
        return string.Join(", ", motivations);
    }

    public string FreeAgentCompetitionSummary(string personId)
    {
        var competitions = _freeAgencyV2.Competitions(ScenarioSnapshot, personId).Take(2).ToArray();
        return competitions.Length == 0
            ? "No known competing offers."
            : string.Join("; ", competitions.Select(competition => $"{competition.TeamName}: {competition.EstimatedSalary:C0} x {competition.EstimatedTermYears}, {competition.RoleOffered}, interest {competition.PlayerInterest}/100"));
    }

    public string FreeAgentOfferResponseText(string personId)
    {
        var offer = ScenarioSnapshot.FreeAgencyMarketState?.FindOffer(personId);
        offer = offer?.IsPendingResponse == true ? offer : null;
        return offer is null
            ? "No pending response."
            : $"Response due {offer.ResponseDate:yyyy-MM-dd}; status {offer.ResponseStatus}; {offer.Explanation}";
    }

    public string FreeAgentOfferLikelihood(string personId)
    {
        var agent = FreeAgentFor(personId);
        if (agent is null)
        {
            return "No offer available.";
        }

        var evaluation = _freeAgencyV2.BuildOffer(_registry, ScenarioSnapshot, personId, agent.ContractAsk.AnnualAmount, agent.ContractAsk.TermYears);
        return $"{evaluation.Likelihood} ({evaluation.DecisionScore}/100): {evaluation.Explanation.Summary}";
    }

    public string AgentSummary(string personId)
    {
        var representation = _agents.FindRepresentation(ScenarioSnapshot, personId);
        var agent = representation?.AgentId is null ? null : _agents.FindAgent(ScenarioSnapshot, representation.AgentId);
        return representation is null
            ? "Agent: untracked"
            : agent is null
                ? $"{representation.RepresentationType}: no formal agency"
                : $"Agent: {agent.Name} ({agent.Profile.AgencyName}) | {agent.NegotiationStyle} | GM relationship {agent.GmRelationship.Score}/100";
    }

    public string AgentOfferComment(string personId)
    {
        var freeAgent = FreeAgentFor(personId);
        if (freeAgent is not null)
        {
            var evaluation = _freeAgencyV2.BuildOffer(_registry, ScenarioSnapshot, personId, freeAgent.ContractAsk.AnnualAmount, freeAgent.ContractAsk.TermYears);
            return $"{evaluation.AgentOpinion} Biggest concern: {evaluation.AgentBiggestConcern} Requested improvement: {evaluation.AgentRequestedImprovement} Risk: {evaluation.AgentRisk}";
        }

        var representation = _agents.FindRepresentation(ScenarioSnapshot, personId);
        return representation is null
            ? "No agent comment is available."
            : $"{AgentSummary(personId)} Representation history: {string.Join("; ", representation.RepresentationHistory.Take(2))}";
    }

    public string AgentDetails(string personId)
    {
        var representation = _agents.FindRepresentation(ScenarioSnapshot, personId);
        if (representation is null)
        {
            return "No agent or representation record is available.";
        }

        var agent = representation.AgentId is null ? null : _agents.FindAgent(ScenarioSnapshot, representation.AgentId);
        var builder = new StringBuilder();
        builder.AppendLine("Agent Card");
        builder.AppendLine("==========");
        builder.AppendLine($"Client: {representation.PersonName}");
        builder.AppendLine($"Representation: {representation.RepresentationType}");
        builder.AppendLine($"Start: {representation.RepresentationStart:yyyy-MM-dd}");
        if (agent is null)
        {
            builder.AppendLine("Agent: No formal agent");
            builder.AppendLine("Agency: Family / advisor only");
            return builder.ToString();
        }

        builder.AppendLine($"Agent: {agent.Name}");
        builder.AppendLine($"Agency: {agent.Profile.AgencyName}");
        builder.AppendLine($"Nationality: {agent.Profile.Nationality}");
        builder.AppendLine($"Age / experience: {agent.Profile.Age} / {agent.Profile.YearsExperience} year(s)");
        builder.AppendLine($"Personality: {agent.Profile.Personality}");
        builder.AppendLine($"Negotiation Style: {agent.NegotiationStyle}");
        builder.AppendLine($"Relationship: GM {agent.GmRelationship.Score}/100; organization {agent.OrganizationRelationship.Score}/100");
        builder.AppendLine($"Reputation: {agent.Reputation.Overall}/100 - {agent.Reputation.Summary}");
        builder.AppendLine($"Current clients: {agent.ClientList.Clients.Count}");
        builder.AppendLine("History:");
        foreach (var history in ScenarioSnapshot.AgentHistory.Where(history => history.AgentId == agent.AgentId).Take(6))
        {
            builder.AppendLine($"- {history.Date:yyyy-MM-dd} {history.Category}: {history.Summary}");
        }

        return builder.ToString();
    }

    public FreeAgencyStaffRecommendations FreeAgentStaffRecommendations(string personId) =>
        _freeAgencyV2.BuildStaffRecommendations(ScenarioSnapshot, personId);

    public bool CanOfferFreeAgent(string personId)
    {
        var agent = FreeAgentFor(personId);
        var phase = FreeAgencyState.Window.Phase;
        return agent is not null
            && agent.Status is FreeAgentStatus.Available or FreeAgentStatus.Negotiating
            && phase is not FreeAgencyPhase.NotOpen and not FreeAgencyPhase.Closed
            && ScenarioSnapshot.FreeAgencyMarketState?.FindOffer(personId)?.IsPendingResponse is not true
            && OpenPendingActions.All(action => action.PersonId != personId || action.ActionType is not (PendingGmActionType.SignFreeAgent or PendingGmActionType.ApproveContract));
    }

    public void ToggleFreeAgentShortlist(string personId)
    {
        var agent = FreeAgentFor(personId);
        if (agent is null)
        {
            LatestSummary = "Selected free agent is no longer available.";
            return;
        }

        ApplyFreeAgentResult(agent.IsShortlisted
            ? _freeAgents.RemoveFromShortlist(_registry, ScenarioSnapshot, personId)
            : _freeAgents.Shortlist(_registry, ScenarioSnapshot, personId));
    }

    public void OfferFreeAgentContractFor(string personId)
    {
        if (!CanOfferFreeAgent(personId))
        {
            LatestSummary = "Selected free agent is not available for a contract offer.";
            return;
        }

        var agent = FreeAgentFor(personId);
        ApplyFreeAgencyV2Result(_freeAgencyV2.SubmitOffer(_registry, ScenarioSnapshot, personId, agent?.ContractAsk.AnnualAmount, agent?.ContractAsk.TermYears));
    }

    public void InviteFreeAgentToCampFor(string personId)
    {
        var agent = FreeAgentFor(personId);
        if (agent is null || agent.Status is FreeAgentStatus.Signed or FreeAgentStatus.Unavailable)
        {
            LatestSummary = "Selected free agent is not available for a camp invite.";
            return;
        }

        ApplyFreeAgentResult(_freeAgents.InviteToCamp(_registry, ScenarioSnapshot, personId));
    }

    public void WithdrawFreeAgentOfferFor(string personId)
    {
        var agent = FreeAgentFor(personId);
        if (agent is null || agent.Status is not (FreeAgentStatus.Offered or FreeAgentStatus.Negotiating))
        {
            LatestSummary = "Selected free agent does not have an active offer to withdraw.";
            return;
        }

        ApplyFreeAgentResult(_freeAgents.WithdrawOffer(_registry, ScenarioSnapshot, personId));
    }

    private static string MotivationLabel(FreeAgentMotivation motivation) =>
        motivation switch
        {
            FreeAgentMotivation.TeamReputation => "team rep",
            FreeAgentMotivation.RelationshipWithGm => "GM trust",
            FreeAgentMotivation.RelationshipWithCoachStaff => "staff trust",
            FreeAgentMotivation.PathwayToHigherLeague => "pathway",
            FreeAgentMotivation.FamilyHome => "family/home",
            _ => motivation.ToString()
        };

    public TradeBlockEntry? TradeBlockEntryFor(string personId) =>
        ScenarioSnapshot.TradeBlock?.Find(personId);

    public TeamSelectionOption? TeamOptionFor(string organizationId) =>
        ScenarioSnapshot.LeagueProfile.Teams.FirstOrDefault(team => team.OrganizationId == organizationId);

    public IReadOnlyList<TradeBlockEntry> OtherTeamTradeRoster(string organizationId)
    {
        var team = TeamOptionFor(organizationId);
        var byId = TradeBlockEntries
            .Where(entry => entry.OrganizationId == organizationId)
            .OrderBy(entry => entry.Position)
            .ThenBy(entry => entry.Name, StringComparer.Ordinal)
            .ToArray();
        if (byId.Length > 0)
        {
            return byId;
        }

        var byName = TradeBlockEntries
            .Where(entry => team is not null && string.Equals(entry.TeamName, team.TeamName, StringComparison.Ordinal))
            .OrderBy(entry => entry.Position)
            .ThenBy(entry => entry.Name, StringComparer.Ordinal)
            .ToArray();
        if (byName.Length > 0)
        {
            return byName;
        }

        return TradeBlockEntries
            .OrderBy(entry => entry.TeamName, StringComparer.Ordinal)
            .ThenBy(entry => entry.Name, StringComparer.Ordinal)
            .Take(10)
            .ToArray();
    }

    public IReadOnlyList<LeagueTransaction> LeagueRecentTransactions(string organizationId) =>
        LeagueTransactions
            .Where(transaction => transaction.OrganizationId == organizationId)
            .OrderByDescending(transaction => transaction.Date)
            .ToArray();

    public string TeamNeedsShortTextForOrganization(string organizationId, string teamName)
    {
        var profile = _tradeStrategy.BuildTeamNeedsProfile(ScenarioSnapshot, organizationId, teamName);
        return $"{profile.Direction} | needs {string.Join(", ", profile.Needs.Take(2).Select(need => need.Need))}";
    }

    public OrganizationAiProfile OrganizationAiProfileFor(string organizationId, string teamName) =>
        OrganizationAiProfiles.FirstOrDefault(profile => profile.OrganizationId == organizationId)
        ?? _organizationAi.BuildProfile(ScenarioSnapshot, _leagueAi.BuildOrganizationProfile(ScenarioSnapshot, organizationId, teamName, BudgetOverview), BudgetOverview);

    public string OrganizationAiShortTextForOrganization(string organizationId, string teamName)
    {
        var profile = OrganizationAiProfileFor(organizationId, teamName);
        return $"{profile.Personality} | {profile.Strategy.Phase} | top need {profile.CurrentNeeds.First().NeedType}";
    }

    public string OrganizationAiTextForOrganization(string organizationId, string teamName)
    {
        var profile = OrganizationAiProfileFor(organizationId, teamName);
        var builder = new StringBuilder();
        builder.AppendLine(profile.Summary);
        builder.AppendLine($"AI personality: {profile.Personality}");
        builder.AppendLine($"Strategy phase: {profile.Strategy.Phase}");
        builder.AppendLine($"Draft philosophy: {profile.Strategy.DraftPhilosophy}");
        builder.AppendLine($"Trade behavior: {profile.Strategy.TradeBehavior}");
        builder.AppendLine($"Free agency behavior: {profile.Strategy.FreeAgencyBehavior}");
        builder.AppendLine($"Budget behavior: {profile.Strategy.BudgetBehavior}");
        builder.AppendLine($"Scouting philosophy: {profile.Strategy.ScoutingBehavior}");
        builder.AppendLine($"Staff behavior: {profile.Strategy.StaffBehavior}");
        builder.AppendLine("Current AI needs:");
        foreach (var need in profile.CurrentNeeds.Take(5))
        {
            builder.AppendLine($"- {need.Priority}: {need.NeedType} | urgency {need.Urgency} | target {need.SuggestedAssetType} - {need.Reason}");
        }

        if (profile.StrategyHistory.Count > 0)
        {
            builder.AppendLine("Recent strategy changes:");
            foreach (var change in profile.StrategyHistory.TakeLast(3))
            {
                builder.AppendLine($"- {change.Date:yyyy-MM-dd}: {change.FromPhase} -> {change.ToPhase} - {change.Reason}");
            }
        }

        return builder.ToString().Trim();
    }

    public string AiFrontOfficeTextForOrganization(string organizationId, string teamName)
    {
        if (ScenarioSnapshot.LatestAiDecisionCycle is null && ScenarioSnapshot.AiTransactionPlans.Count == 0)
        {
            var result = _aiFrontOffice.RunCycle(ScenarioSnapshot, force: true);
            ScenarioSnapshot = result.ScenarioSnapshot;
            Snapshot = result.ScenarioSnapshot.AlphaSnapshot;
        }

        return _aiFrontOffice.BuildFrontOfficeText(ScenarioSnapshot, organizationId, teamName);
    }

    public string TeamNeedsTextForOrganization(string organizationId, string teamName)
    {
        var profile = _tradeStrategy.BuildTeamNeedsProfile(ScenarioSnapshot, organizationId, teamName);
        var builder = new StringBuilder();
        builder.AppendLine(profile.Summary);
        builder.AppendLine($"Trade direction: {profile.Direction}");
        builder.AppendLine($"AI GM personality: {profile.GmPersonality}");
        builder.AppendLine($"Asset preferences: {string.Join(", ", profile.AssetPreferences)}");
        builder.AppendLine();
        builder.AppendLine(OrganizationAiTextForOrganization(organizationId, teamName));
        builder.AppendLine();
        builder.AppendLine("Trade need read:");
        foreach (var need in profile.Needs.Take(5))
        {
            builder.AppendLine($"- {need.Priority}: {need.Need} - {need.Reason}");
        }

        return builder.ToString().Trim();
    }

    public string PipelineCountForOrganization(string organizationId)
    {
        var count = ScenarioSnapshot.PlayerPipeline.Count(record => record.CurrentOrganizationId == organizationId || record.RightsHolderOrganizationId == organizationId);
        return count == 0 ? "none tracked yet" : count.ToString();
    }

    public string PipelineText(string personId)
    {
        var record = ScenarioSnapshot.PlayerPipeline.FirstOrDefault(record => record.PersonId == personId);
        if (record is null)
        {
            return "No pipeline status tracked yet.";
        }

        return $"{record.CurrentLevel} | {record.PipelineStatus} | rights {record.RightsHolderTeamName ?? "none"} | parent {record.ParentOrganization?.TeamName ?? "none"} | affiliate {record.AffiliateOrganization?.TeamName ?? "none"}";
    }

    public string PipelineSignedText(string personId) =>
        PipelineRecord(personId)?.IsSigned == true ? "Yes" : "No";

    public string PipelineAhlEligibleText(string personId) =>
        PipelineRecord(personId)?.IsAhlEligible == true ? "Yes" : "No";

    public string PipelineJuniorEligibleText(string personId) =>
        PipelineRecord(personId)?.IsJuniorEligible == true ? "Yes" : "No";

    public string PipelineDevelopmentLevelText(string personId) =>
        PipelineRecord(personId)?.DevelopmentLevel.ToString() ?? "Unknown";

    public string PipelineSlideText(string personId) =>
        PipelineRecord(personId)?.ContractSlideSummary ?? "No slide status tracked.";

    public string PipelineRecommendationText(string personId) =>
        PipelineRecord(personId)?.RecommendedAssignment ?? "Review development path.";

    private PlayerPipelineRecord? PipelineRecord(string personId) =>
        ScenarioSnapshot.PlayerPipeline.FirstOrDefault(record => record.PersonId == personId);

    public string TradeTeamNeedsShortText(TradeBlockEntry entry)
    {
        var profile = _tradeStrategy.BuildTeamNeedsProfile(ScenarioSnapshot, entry.OrganizationId, entry.TeamName);
        return $"{profile.Direction} | needs {string.Join(", ", profile.Needs.Take(2).Select(need => need.Need))}";
    }

    public string TradePotentialRole(TradeBlockEntry entry) =>
        entry.Position switch
        {
            RosterPosition.Goalie when entry.Age <= 20 => "Prospect Goalie",
            RosterPosition.Goalie => entry.AssetValue >= 66 ? "Starting Goalie" : "Backup Goalie",
            RosterPosition.Defense when entry.AssetValue >= 68 => "Top Pair Defenseman",
            RosterPosition.Defense when entry.Age <= 20 => "Prospect Defenseman",
            RosterPosition.Defense => "Second Pair Defenseman",
            _ when entry.AssetValue >= 68 => "Top Six Forward",
            _ when entry.Age <= 20 => "Prospect Forward",
            _ => "Middle Six Forward"
        };

    public string TradeTargetType(TradeBlockEntry entry)
    {
        if (entry.CurrentRole.Contains("top", StringComparison.OrdinalIgnoreCase) || entry.CurrentRole.Contains("first", StringComparison.OrdinalIgnoreCase))
        {
            return entry.Position == RosterPosition.Defense ? "top-pair defenseman" : "top-line player";
        }

        if (entry.CurrentRole.Contains("prospect", StringComparison.OrdinalIgnoreCase) || entry.PlayerType.Contains("prospect", StringComparison.OrdinalIgnoreCase))
        {
            return "prospect";
        }

        if (entry.CurrentRole.Contains("scratch", StringComparison.OrdinalIgnoreCase) || entry.CurrentRole.Contains("buried", StringComparison.OrdinalIgnoreCase))
        {
            return "buried player";
        }

        return entry.CurrentRole.Contains("depth", StringComparison.OrdinalIgnoreCase) ? "depth player" : "roster player";
    }

    public string TradeTeamNeedsText(string personId)
    {
        var entry = TradeBlockEntryFor(personId);
        if (entry is null)
        {
            return "Team needs unavailable.";
        }

        var profile = _tradeStrategy.BuildTeamNeedsProfile(ScenarioSnapshot, entry.OrganizationId, entry.TeamName);
        var builder = new StringBuilder();
        builder.AppendLine(profile.Summary);
        builder.AppendLine($"AI GM personality: {profile.GmPersonality}");
        builder.AppendLine($"Asset preferences: {string.Join(", ", profile.AssetPreferences)}");
        builder.AppendLine();
        builder.AppendLine("Other team AI strategy:");
        builder.AppendLine(OrganizationAiTextForOrganization(entry.OrganizationId, entry.TeamName));
        builder.AppendLine();
        builder.AppendLine("Trade need read:");
        foreach (var need in profile.Needs)
        {
            builder.AppendLine($"- {need.Priority}: {need.Need} - {need.Reason}");
        }

        return builder.ToString().Trim();
    }

    public string TradeValueText(string personId)
    {
        var value = _tradeStrategy.BuildTradeValueSummary(ScenarioSnapshot, personId);
        var assetText = _assetEvaluation.BuildAssetValueText(ScenarioSnapshot, personId);
        var builder = new StringBuilder();
        builder.AppendLine($"{value.PlayerName}, age {value.Age}");
        builder.AppendLine($"Role: {value.Role}");
        builder.AppendLine(TradeBlockEntryFor(personId) is { } entry
            ? $"Lineup role: {entry.CurrentRole}; potential role: {TradePotentialRole(entry)}; trade target type: {TradeTargetType(entry)}"
            : LineupRoleTradeText(personId));
        builder.AppendLine($"Contract: {value.Contract}");
        builder.AppendLine($"Budget: {value.BudgetImpact:C0}; years remaining: {value.YearsRemaining}");
        builder.AppendLine($"Development: {value.DevelopmentSummary}");
        builder.AppendLine($"Prospect value: {value.ProspectValue}");
        builder.AppendLine($"Estimated league value: {value.EstimatedLeagueValue}");
        builder.AppendLine($"Opinion: {value.Opinion}");
        builder.AppendLine();
        builder.AppendLine("Player Value & Asset Evaluation");
        builder.AppendLine(assetText);
        return builder.ToString().Trim();
    }

    public string PositionMarketText() =>
        _assetEvaluation.BuildPositionMarketText(ScenarioSnapshot);

    public string PositionMarketNote(string personId)
    {
        var value = ScenarioSnapshot.PlayerAssetValues.FirstOrDefault(item => item.PersonId == personId)
            ?? _assetEvaluation.BuildPlayerValue(ScenarioSnapshot, personId, ScenarioSnapshot.Organization.OrganizationId, ScenarioSnapshot.Organization.Name);
        return $"Position Market: {PositionScarcityService.Label(value.Market.MarketPosition)} {value.Market.ScarcityLevel}";
    }

    public string AssetValueShortText(string personId)
    {
        var value = ScenarioSnapshot.PlayerAssetValues.FirstOrDefault(item => item.PersonId == personId)
            ?? _assetEvaluation.BuildPlayerValue(ScenarioSnapshot, personId, ScenarioSnapshot.Organization.OrganizationId, ScenarioSnapshot.Organization.Name);
        return $"Current {value.Current.Band} | Future {value.Future.Band} | Trade {value.Trade.Band} | Fit {value.Organizational.Band}";
    }

    public IReadOnlyList<TradeAssetRow> YourTradeAssetRows()
    {
        return _trades.BuildPlayerOrganizationAssets(ScenarioSnapshot)
            .Select(asset => new TradeAssetRow(asset, AssetLabel(asset)))
            .GroupBy(row => row.Asset.AssetId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    public IReadOnlyList<TradeAssetRow> OtherTradeAssetRows(string organizationId, string teamName)
    {
        return _trades.BuildOtherOrganizationAssets(ScenarioSnapshot, organizationId, teamName)
            .Select(asset => new TradeAssetRow(asset, AssetLabel(asset)))
            .GroupBy(row => row.Asset.AssetId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    public IReadOnlyList<TradeAssetRow> CurrentTradeProposalRows() =>
        _tradePlayerGives
            .Select(asset => new TradeAssetRow(asset, $"You Give: {AssetLabel(asset)}"))
            .Concat(_tradePlayerReceives.Select(asset => new TradeAssetRow(asset, $"You Receive: {AssetLabel(asset)}")))
            .ToArray();

    public IReadOnlyList<TradeAssetRow> CurrentTradeYouGiveRows() =>
        _tradePlayerGives
            .Select(asset => new TradeAssetRow(asset, AssetLabel(asset)))
            .ToArray();

    public IReadOnlyList<TradeAssetRow> CurrentTradeYouReceiveRows() =>
        _tradePlayerReceives
            .Select(asset => new TradeAssetRow(asset, AssetLabel(asset)))
            .ToArray();

    public IReadOnlyList<TradeAsset> CurrentTradeProposalAssets =>
        _tradePlayerGives.Concat(_tradePlayerReceives).ToArray();

    public bool HasTradeProposalAssets => _tradePlayerGives.Count > 0 || _tradePlayerReceives.Count > 0;

    public bool HasTradePlayerGives => _tradePlayerGives.Count > 0;

    public bool HasTradePlayerReceives => _tradePlayerReceives.Count > 0;

    public bool CanProposeCurrentTrade => _tradePlayerGives.Count > 0 && _tradePlayerReceives.Count > 0;

    public IReadOnlyList<string> CurrentTradeEvaluationReasons => CurrentTradeEvaluation?.Reasons ?? Array.Empty<string>();

    public string CurrentTradeEvaluationText => CurrentTradeEvaluation?.Explanation ?? "Trade must include assets on both sides before evaluation.";

    public string CurrentTradeCounterText => CurrentTradeEvaluation?.CounterSuggestion ?? "No counter request yet.";

    public bool HasCurrentTradeCounter => CurrentTradeCounterOffer is not null && CurrentTradeCounterOffer.RequestedAssets.Count > 0;

    public string CurrentTradeAssetValueComparison
    {
        get
        {
            if (!CanProposeCurrentTrade)
            {
                return "Add assets from both teams to compare contextual value.";
            }

            var give = _tradePlayerGives.Sum(asset => asset.Value);
            var receive = _tradePlayerReceives.Sum(asset => asset.Value);
            var gap = give - receive;
            return $"You Give {give}; You Receive {receive}; gap {gap:+#;-#;0}. Values include current value, future value, contract value, team fit, and position scarcity.";
        }
    }

    public string CurrentTradeScarcityText
    {
        get
        {
            if (!HasTradeProposalAssets)
            {
                return "No assets selected yet.";
            }

            var notes = CurrentTradeProposalAssets
                .Where(asset => !string.IsNullOrWhiteSpace(asset.AssetId) && asset.AssetType is TradeAssetType.Player or TradeAssetType.ProspectRights)
                .Select(asset => $"{asset.DisplayName}: {PositionMarketNote(asset.AssetId)}")
                .Distinct(StringComparer.Ordinal)
                .Take(4)
                .ToArray();
            return notes.Length == 0 ? "Selected assets are picks or future considerations." : string.Join(" | ", notes);
        }
    }

    private TradeCounterOffer? CurrentTradeCounterOffer
    {
        get
        {
            var evaluation = CurrentTradeEvaluation;
            if (evaluation is null || evaluation.Decision != TradeOfferStatus.Countered)
            {
                return null;
            }

            var primaryOther = _tradePlayerReceives.First();
            var offer = _trades.CreateOffer(
                ScenarioSnapshot,
                primaryOther.OrganizationId,
                primaryOther.OrganizationName,
                _tradePlayerGives.ToArray(),
                _tradePlayerReceives.ToArray());
            return _tradeStrategy.BuildCounterOffer(ScenarioSnapshot, offer, evaluation);
        }
    }

    public string CurrentTradeRosterImpact =>
        !CanProposeCurrentTrade
            ? "Add assets from both teams to preview roster impact."
            : $"You give {_tradePlayerGives.Count} asset(s) and receive {_tradePlayerReceives.Count} asset(s). Active roster changes only after pending GM approval.";

    public string CurrentTradeBudgetImpact
    {
        get
        {
            if (!CanProposeCurrentTrade)
            {
                return "Add assets from both teams to preview budget impact.";
            }

            var impact = _tradePlayerReceives.Sum(asset => asset.SalaryImpact) - _tradePlayerGives.Sum(asset => asset.SalaryImpact);
            var primaryOther = _tradePlayerReceives.First();
            var offer = _trades.CreateOffer(
                ScenarioSnapshot,
                primaryOther.OrganizationId,
                primaryOther.OrganizationName,
                _tradePlayerGives.ToArray(),
                _tradePlayerReceives.ToArray());
            var cap = new SalaryCapService().ProjectAfterTrade(ScenarioSnapshot, _registry.Rulebook ?? ScenarioSnapshot.LeagueProfile.Rulebook, offer);
            var budgetImpact = impact >= 0 ? $"Adds about {impact:C0}." : $"Saves about {Math.Abs(impact):C0}.";
            if (!cap.Before.IsEnabled)
            {
                return $"{budgetImpact} Salary cap disabled by rulebook.";
            }

            var indicator = cap.IsCompliant ? "Green" : "Red";
            return $"{budgetImpact} Cap before: {cap.Before.CapUsed:C0} used, {cap.Before.CapRemaining:C0} remaining. Cap after: {cap.After.CapUsed:C0} used, {cap.After.CapRemaining:C0} remaining. Cap indicator: {indicator}.";
        }
    }

    private TradeEvaluation? CurrentTradeEvaluation
    {
        get
        {
            if (!CanProposeCurrentTrade)
            {
                return null;
            }

            var primaryOther = _tradePlayerReceives.First();
            var offer = _trades.CreateOffer(
                ScenarioSnapshot,
                primaryOther.OrganizationId,
                primaryOther.OrganizationName,
                _tradePlayerGives.ToArray(),
                _tradePlayerReceives.ToArray());
            return _trades.EvaluateTrade(ScenarioSnapshot, offer);
        }
    }

    public void SelectYourTradeAsset(string assetId)
    {
        _selectedYourTradeAssetId = assetId;
        LatestSummary = $"Selected your trade asset: {assetId}.";
    }

    public void SelectOtherTradeAsset(string assetId)
    {
        _selectedOtherTradeAssetId = assetId;
        LatestSummary = $"Selected other-team trade asset: {assetId}.";
    }

    public void AddYourAssetToTradeProposal(TradeAsset asset)
    {
        if (_tradePlayerGives.All(existing => existing.AssetId != asset.AssetId))
        {
            _tradePlayerGives.Add(asset);
        }

        LatestSummary = $"{asset.DisplayName} added to You Give.";
    }

    public void AddOtherAssetToTradeProposal(TradeAsset asset)
    {
        if (_tradePlayerReceives.All(existing => existing.AssetId != asset.AssetId))
        {
            _tradePlayerReceives.Add(asset);
        }

        _selectedTradeTargetPersonId = asset.AssetId;
        LatestSummary = $"{asset.DisplayName} added to You Receive.";
    }

    public void RemoveAssetFromTradeProposal(TradeAsset asset)
    {
        _tradePlayerGives.RemoveAll(existing => existing.AssetId == asset.AssetId);
        _tradePlayerReceives.RemoveAll(existing => existing.AssetId == asset.AssetId);
        LatestSummary = $"{asset.DisplayName} removed from trade offer.";
    }

    public void RemoveYourAssetFromTradeProposal(TradeAsset asset)
    {
        _tradePlayerGives.RemoveAll(existing => existing.AssetId == asset.AssetId);
        LatestSummary = $"{asset.DisplayName} removed from You Give.";
    }

    public void RemoveOtherAssetFromTradeProposal(TradeAsset asset)
    {
        _tradePlayerReceives.RemoveAll(existing => existing.AssetId == asset.AssetId);
        LatestSummary = $"{asset.DisplayName} removed from You Receive.";
    }

    public void AcceptCurrentTradeCounter()
    {
        var counter = CurrentTradeCounterOffer;
        if (counter is null)
        {
            LatestSummary = "No active counter offer is available.";
            return;
        }

        _tradePlayerGives.Clear();
        _tradePlayerGives.AddRange(counter.RevisedPlayerGives);
        _tradePlayerReceives.Clear();
        _tradePlayerReceives.AddRange(counter.RevisedPlayerReceives);
        LatestSummary = $"Counter added to proposal: {counter.Message}";
    }

    public void ProposeCurrentTrade(string fallbackOrganizationId, string fallbackTeamName)
    {
        if (!TradeDeadlineWindow.TradesAllowed)
        {
            LatestSummary = "Trade deadline has passed. New trade proposals are locked.";
            return;
        }

        if (!CanProposeCurrentTrade)
        {
            LatestSummary = "Trade must include assets on both sides.";
            return;
        }

        var primaryOther = _tradePlayerReceives.FirstOrDefault(asset => asset.Side == TradeSide.OtherOrganization);
        var offer = _trades.CreateOffer(
            ScenarioSnapshot,
            primaryOther?.OrganizationId ?? fallbackOrganizationId,
            primaryOther?.OrganizationName ?? fallbackTeamName,
            _tradePlayerGives.ToArray(),
            _tradePlayerReceives.ToArray());
        ApplyTradeResult(_trades.ProposeTrade(_registry, ScenarioSnapshot, offer));
    }

    public void SelectTradeTarget(string personId)
    {
        var entry = TradeBlockEntryFor(personId);
        if (entry is null)
        {
            LatestSummary = "Selected trade target is no longer on the trade block.";
            return;
        }

        _selectedTradeTargetPersonId = personId;
        if (_tradePlayerReceives.All(asset => asset.AssetId != personId))
        {
            _tradePlayerReceives.Add(_trades.CreateRosterPlayerAsset(ScenarioSnapshot, personId, TradeSide.OtherOrganization));
        }

        LatestSummary = $"{entry.Name} added to the trade builder. Review projected impact, then propose trade.";
    }

    public void ClearTradeBuilder()
    {
        _selectedTradeTargetPersonId = null;
        _selectedYourTradeAssetId = null;
        _selectedOtherTradeAssetId = null;
        _tradePlayerGives.Clear();
        _tradePlayerReceives.Clear();
        LatestSummary = "Trade builder cleared.";
    }

    public string TradeProjectedOfferText(string personId)
    {
        var entry = TradeBlockEntryFor(personId);
        var outgoing = SuggestedOutgoingTradePlayer(entry);
        if (entry is null || outgoing is null)
        {
            return "No valid basic offer is available.";
        }

        return $"Give {FindPersonName(outgoing.PersonId)} for {entry.Name}.";
    }

    public string TradeProjectedRosterImpact(string personId)
    {
        var entry = TradeBlockEntryFor(personId);
        var outgoing = SuggestedOutgoingTradePlayer(entry);
        if (entry is null || outgoing is null)
        {
            return "Roster impact unavailable.";
        }

        return $"One-for-one roster move: out {outgoing.Position}, in {entry.Position}. Active count stays {Snapshot.Roster.ActivePlayers.Count}.";
    }

    public string TradeProjectedBudgetImpact(string personId)
    {
        var entry = TradeBlockEntryFor(personId);
        var outgoing = SuggestedOutgoingTradePlayer(entry);
        if (entry is null || outgoing is null)
        {
            return "Budget impact unavailable.";
        }

        var outgoingSalary = ScenarioSnapshot.Contracts.Concat(Snapshot.Contracts)
            .Where(contract => contract.PersonId == outgoing.PersonId && contract.Status == ContractStatus.Signed)
            .OrderByDescending(contract => contract.SignedOn ?? contract.OfferedOn)
            .Select(contract => contract.Money.SalaryOrStipend)
            .FirstOrDefault();
        var impact = entry.SalaryImpact - outgoingSalary;
        return impact >= 0 ? $"Adds about {impact:C0}." : $"Saves about {Math.Abs(impact):C0}.";
    }

    private static string AssetLabel(TradeAsset asset)
    {
        var bio = asset.Position is null
            ? asset.AssetType.ToString()
            : $"{asset.Position} | age {asset.Age?.ToString() ?? "unknown"}";
        return $"{asset.DisplayName} | {bio} | {asset.Summary} | value {asset.Value}";
    }

    public void ProposeTradeFor(string personId)
    {
        var entry = TradeBlockEntryFor(personId);
        if (entry is null)
        {
            LatestSummary = "Selected trade target is no longer on the trade block.";
            return;
        }

        if (!TradeDeadlineWindow.TradesAllowed)
        {
            LatestSummary = "Trade deadline has passed. New trade proposals are locked.";
            return;
        }

        ApplyTradeResult(_trades.ProposeSimpleTradeForBlockEntry(_registry, ScenarioSnapshot, personId));
        _selectedTradeTargetPersonId = personId;
    }

    public void WithdrawLatestTradeOffer()
    {
        var offer = ScenarioSnapshot.TradeOffers
            .Where(offer => offer.Status is TradeOfferStatus.Proposed or TradeOfferStatus.Accepted or TradeOfferStatus.Countered)
            .OrderByDescending(offer => offer.ProposedOn)
            .ThenByDescending(offer => offer.TradeOfferId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (offer is null)
        {
            LatestSummary = "No active trade offer is available to withdraw.";
            return;
        }

        ApplyTradeResult(_trades.WithdrawTrade(_registry, ScenarioSnapshot, offer.TradeOfferId));
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
        ScenarioSnapshot = _warRoom.UpdateGmNotes(ScenarioSnapshot, prospectPersonId, note);
        Snapshot = ScenarioSnapshot.AlphaSnapshot;
    }

    public void ToggleDraftWarRoomTag(string prospectPersonId, DraftWatchTag tag)
    {
        if (Snapshot.DraftBoard.Entries.All(entry => entry.ProspectPersonId != prospectPersonId))
        {
            LatestSummary = "Selected prospect is not on the draft board.";
            return;
        }

        var current = DraftWarRoom.BoardEntries.FirstOrDefault(entry => entry.ProspectPersonId == prospectPersonId);
        var enabled = current?.Tags.Contains(tag) != true;
        ScenarioSnapshot = _warRoom.SetWatchTag(ScenarioSnapshot, prospectPersonId, tag, enabled);
        Snapshot = ScenarioSnapshot.AlphaSnapshot;
        LatestSummary = $"{FindPersonName(prospectPersonId)} {(enabled ? "tagged" : "untagged")} as {tag}.";
    }

    public void RemoveFromDraftWarRoom(string prospectPersonId)
    {
        ScenarioSnapshot = _warRoom.RemoveFromPersonalBoard(ScenarioSnapshot, prospectPersonId);
        Snapshot = ScenarioSnapshot.AlphaSnapshot;
        LatestSummary = $"{FindPersonName(prospectPersonId)} removed from your personal draft board.";
    }

    public string CompareWithNearbyProspectsText(string prospectPersonId)
    {
        var ranked = DraftWarRoom.BoardEntries
            .Where(entry => !entry.IsRemoved)
            .OrderBy(entry => entry.PersonalRank)
            .Select(entry => entry.ProspectPersonId)
            .ToList();
        if (!ranked.Contains(prospectPersonId, StringComparer.Ordinal))
        {
            ranked.Insert(0, prospectPersonId);
        }

        var index = ranked.FindIndex(id => string.Equals(id, prospectPersonId, StringComparison.Ordinal));
        var ids = ranked
            .Skip(Math.Max(0, index - 1))
            .Take(4)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (ids.Length < 2)
        {
            ids = ranked.Take(4).ToArray();
        }

        return DraftComparisonText(ids);
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

    public void SkipDraftPick()
    {
        if (ScenarioSnapshot.DraftExperience?.IsPlayerTurn != true)
        {
            LatestSummary = "Skip is only available when your team is on the clock.";
            return;
        }

        var prospect = DraftWarRoom.BoardEntries
            .Where(entry => !entry.IsRemoved && !entry.Tags.Contains(DraftWatchTag.Avoid))
            .OrderBy(entry => entry.PersonalRank)
            .Select(entry => Snapshot.DraftBoard.Entries.FirstOrDefault(board => board.ProspectPersonId == entry.ProspectPersonId))
            .FirstOrDefault(entry => entry is not null)
            ?? Snapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank).FirstOrDefault();
        if (prospect is null)
        {
            LatestSummary = "No available prospect remains on the draft board.";
            return;
        }

        ApplyDraftResult(_draftExperience.MakePlayerSelectionAndContinue(_registry, ScenarioSnapshot, prospect.ProspectPersonId));
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

    public void FinishSeasonAndEnterOffseason() =>
        ApplySeasonCompletionResult(_seasonRollover.CompleteSeasonAndEnterOffseason(_registry, ScenarioSnapshot));

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

    private void AddInboxItems(IEnumerable<AlphaInboxItem> items)
    {
        var incoming = items.ToArray();
        if (incoming.Length == 0)
        {
            return;
        }

        var journal = _playability.BuildJournalEntries(incoming);
        if (journal.Count > 0)
        {
            _journalEntries.Clear();
            _journalEntries.AddRange(_playability.MergeJournalEntries(_journalEntries, journal));
        }

        InboxManager.AddRange(_playability.FilterInboxItems(incoming));
    }

    private void ApplyAction(GmActionResult result)
    {
        SetScenarioSnapshot(result.ScenarioSnapshot);
        EnsureSelectedDossierStillExists();
        AddInboxItems(result.InboxItems);
        LastProcessedEventCount = 0;
        LatestSummary = result.Summary;
    }

    private void SetScenarioSnapshot(NewGmScenarioSnapshot scenario)
    {
        ScenarioSnapshot = _assetEvaluation.EnsureEvaluations(scenario);
        Snapshot = ScenarioSnapshot.AlphaSnapshot;
    }

    private void ApplyAdvanceResult(FirstMonthAdvanceResult result)
    {
        SetScenarioSnapshot(result.ScenarioSnapshot);
        EnsureSelectedDossierStillExists();
        AddInboxItems(result.InboxItems);
        AddLeagueTransactions(result.LeagueTransactions);
        var freeAgency = _freeAgencyV2.ProgressMarket(_registry, ScenarioSnapshot);
        if (freeAgency.Success)
        {
            SetScenarioSnapshot(freeAgency.ScenarioSnapshot);
            AddInboxItems(freeAgency.InboxItems);
            AddLeagueTransactions(freeAgency.LeagueTransactions);
        }

        LastProcessedEventCount = result.ProcessedEventCount;
        LastStopReason = result.StopReason;
        var baseSummary = result.MonthlySummary is not null
            ? $"{result.StopReason} {result.MonthlySummary.ExecutiveNarrative}"
            : result.DaysAdvanced == 0
                ? result.StopReason
                : $"{result.StopReason} Advanced {result.DaysAdvanced} day(s), processed {result.ProcessedEventCount} event(s), and created {result.InboxItems.Count} inbox item(s).";
        LatestSummary = freeAgency.Success && (freeAgency.InboxItems.Count > 0 || freeAgency.LeagueTransactions.Count > 0)
            ? $"{baseSummary} {freeAgency.Message}"
            : baseSummary;
    }

    private void ApplyRecruitingV2(RecruitingV2Result result)
    {
        SetScenarioSnapshot(result.ScenarioSnapshot);
        EnsureSelectedDossierStillExists();
        AddInboxItems(result.InboxItems);
        LastProcessedEventCount = 0;
        LatestSummary = result.Message;
    }

    private void ApplyDraftResult(DraftExperienceResult result)
    {
        SetScenarioSnapshot(result.ScenarioSnapshot);
        EnsureSelectedDossierStillExists();
        AddInboxItems(result.InboxItems);
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
            SetScenarioSnapshot(result.ScenarioSnapshot);
            EnsureSelectedDossierStillExists();
            AddInboxItems(result.InboxItems);
        }

        LastProcessedEventCount = 0;
        LatestSummary = result.Message;
    }

    private void ApplyCampResult(TrainingCampResult result)
    {
        SetScenarioSnapshot(result.ScenarioSnapshot);
        EnsureSelectedDossierStillExists();
        AddInboxItems(result.InboxItems);
        LastProcessedEventCount = 0;
        LatestSummary = result.Summary;
    }

    private void ApplyCampDecision(TrainingCampDecisionResult result)
    {
        if (result.Success)
        {
            SetScenarioSnapshot(result.ScenarioSnapshot);
            EnsureSelectedDossierStillExists();
            AddInboxItems(result.InboxItems);
        }

        LastProcessedEventCount = 0;
        LatestSummary = result.Message;
    }

    private void ApplyPendingResult(PendingGmActionResult result)
    {
        if (result.Success)
        {
            SetScenarioSnapshot(result.ScenarioSnapshot);
            EnsureSelectedDossierStillExists();
            AddInboxItems(result.InboxItems);
            AddLeagueTransactions(result.LeagueTransactions ?? Array.Empty<LeagueTransaction>());
        }

        LastProcessedEventCount = 0;
        LatestSummary = result.Message;
    }

    private void ApplySeasonReadinessResult(SeasonReadinessResult result)
    {
        SetScenarioSnapshot(result.ScenarioSnapshot);
        EnsureSelectedDossierStillExists();
        AddInboxItems(result.InboxItems);
        LastProcessedEventCount = 0;
        LatestSummary = result.Message;
    }

    private void ApplyExecutiveReportResult(ExecutiveReportGenerationResult result)
    {
        if (result.Success)
        {
            SetScenarioSnapshot(result.ScenarioSnapshot);
            EnsureSelectedDossierStillExists();
            AddInboxItems(result.InboxItems);
        }

        LastProcessedEventCount = 0;
        LatestSummary = result.Message;
    }

    private void ApplySeasonCompletionResult(SeasonCompletionResult result)
    {
        if (result.Success)
        {
            SetScenarioSnapshot(result.ScenarioSnapshot);
            EnsureSelectedDossierStillExists();
            AddInboxItems(result.InboxItems);
            AddLeagueTransactions(result.LeagueTransactions);
        }

        LastProcessedEventCount = 0;
        LatestSummary = result.Message;
    }

    private void ApplyScoutingOperationResult(ScoutingOperationResult result)
    {
        if (result.Success)
        {
            SetScenarioSnapshot(result.ScenarioSnapshot);
            EnsureSelectedDossierStillExists();
            AddInboxItems(result.InboxItems);
        }

        LastProcessedEventCount = 0;
        LatestSummary = result.Message;
    }

    private void ApplyStaffOfficeResult(StaffOfficeResult result)
    {
        if (result.Success)
        {
            SetScenarioSnapshot(result.ScenarioSnapshot);
            EnsureSelectedDossierStillExists();
            AddInboxItems(result.InboxItems);
            AddLeagueTransactions(result.LeagueTransactions);
        }

        LastProcessedEventCount = 0;
        LatestSummary = result.Message;
    }

    private void ApplyFreeAgentResult(FreeAgentMarketResult result)
    {
        if (result.Success)
        {
            SetScenarioSnapshot(result.ScenarioSnapshot);
            EnsureSelectedDossierStillExists();
            AddInboxItems(result.InboxItems);
            AddLeagueTransactions(result.LeagueTransactions);
        }

        LastProcessedEventCount = 0;
        LatestSummary = result.Message;
    }

    private void ApplyFreeAgencyV2Result(FreeAgencyV2Result result)
    {
        if (result.Success)
        {
            SetScenarioSnapshot(result.ScenarioSnapshot);
            EnsureSelectedDossierStillExists();
            AddInboxItems(result.InboxItems);
            AddLeagueTransactions(result.LeagueTransactions);
        }

        LastProcessedEventCount = 0;
        LatestSummary = result.Message;
    }

    private void ApplyTradeResult(TradeDecisionResult result)
    {
        if (result.Success)
        {
            SetScenarioSnapshot(result.ScenarioSnapshot);
            EnsureSelectedDossierStillExists();
            AddInboxItems(result.InboxItems);
            AddLeagueTransactions(result.LeagueTransactions);
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

    private RosterPlayer? SuggestedOutgoingTradePlayer(TradeBlockEntry? entry)
    {
        if (entry is null)
        {
            return null;
        }

        return Snapshot.Roster.ActivePlayers
            .OrderBy(player => player.Age ?? PersonAge(player.PersonId) ?? 18)
            .ThenBy(player => player.PersonId, StringComparer.Ordinal)
            .FirstOrDefault(player => player.Position == entry.Position)
            ?? Snapshot.Roster.ActivePlayers
                .OrderBy(player => player.Age ?? PersonAge(player.PersonId) ?? 18)
                .ThenBy(player => player.PersonId, StringComparer.Ordinal)
                .FirstOrDefault();
    }

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
        return person?.Identity.DisplayName ?? ScenarioSnapshot.FreeAgentMarket?.Find(personId)?.Name ?? personId;
    }

    public string FindPersonNameForDisplay(string personId) => FindPersonName(personId);

    private string? FirstDossierPersonId() => DossierPersonIds().FirstOrDefault();

    private IReadOnlyList<string> DossierPersonIds() =>
        Snapshot.Roster.Players.Select(player => player.PersonId)
            .Concat(Snapshot.StaffMembers.Select(member => member.PersonId))
            .Concat(Snapshot.Recruits.Select(recruit => recruit.RecruitPersonId))
            .Concat(Snapshot.DraftBoard.Entries.Select(entry => entry.ProspectPersonId))
            .Concat(ScenarioSnapshot.ProspectRights.Select(prospect => prospect.ProspectPersonId))
            .Concat(ScenarioSnapshot.FreeAgentMarket?.FreeAgents.Select(agent => agent.PersonId) ?? Array.Empty<string>())
            .Concat(ScenarioSnapshot.TradeBlock?.Entries.Select(entry => entry.PersonId) ?? Array.Empty<string>())
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

    private void EnsureLifeCycleState()
    {
        var updated = _franchiseIdentity.EnsureIdentities(ScenarioSnapshot);
        updated = _lifeCycle.EnsureLifeCycle(updated, _registry);
        updated = _staffLifeCycle.EnsureLifeCycle(updated, _registry);
        updated = _ownerLifeCycle.EnsureLifeCycle(updated, _registry);
        updated = _relationships.EnsureExpansion(updated, _registry);
        updated = _stories.EnsureStories(updated, _registry);
        updated = _media.EnsureMediaFeed(updated, LeagueTransactions, _registry);
        updated = _lineups.EnsureLineup(updated);
        updated = _warRoom.EnsureWarRoom(updated);
        updated = new HockeyIntelligenceRatingService().EnsureRatings(updated);
        updated = new DevelopmentCurveService().EnsureCurves(updated);
        updated = _ratings.EnsureRatings(updated);
        updated = _rfaUfa.EnsureRights(updated, _registry.Rulebook ?? updated.LeagueProfile.Rulebook);
        updated = _arbitration.EnsureArbitration(updated, _registry.Rulebook ?? updated.LeagueProfile.Rulebook);
        if (!ReferenceEquals(updated, ScenarioSnapshot))
        {
            ScenarioSnapshot = updated;
            Snapshot = updated.AlphaSnapshot;
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

