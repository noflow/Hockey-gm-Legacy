using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LegacyEngine.Integration;
using LegacyEngine.People;
using LegacyEngine.Rosters;
using LegacyEngine.Scouting;

namespace AlphaDesktop;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Contains("--smoke-test", StringComparer.OrdinalIgnoreCase))
        {
            var state = AlphaDesktopState.Create();
            Console.WriteLine($"AlphaDesktop smoke test: {state.Snapshot.WorldState.WorldName} {state.Snapshot.CurrentDate:yyyy-MM-dd} draft in {state.ScenarioSnapshot.DaysUntilDraft} days");
            return;
        }

        var app = new Application();
        app.Run(new MainWindow());
    }
}

internal sealed class MainWindow : Window
{
    private AlphaDesktopState? _state;
    private readonly TextBlock _dateText = new();
    private readonly TextBlock _summaryText = new();
    private readonly TextBlock _processedText = new();
    private readonly Dictionary<string, TextBox> _tabs = [];
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
            Text = "Alpha 1.6 starts with your created GM preparing for the draft, then unlocks training camp.",
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
        AddInboxTab(tabs);
        AddTab(tabs, "Owner");
        AddTab(tabs, "Staff");
        AddTab(tabs, "Roster");
        AddTab(tabs, "Recruits");
        AddTab(tabs, "Scouting");
        AddTab(tabs, "Pending Actions");
        if (State.IsDraftUiEnabled)
        {
            AddTab(tabs, "Draft Board");
        }
        AddTab(tabs, "Training Camp");
        AddTab(tabs, "Relationships");

        root.Children.Add(tabs);
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
            Text = "Hockey GM Legacy - Alpha 1.6 - Training Camp",
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

        var buttonPanel = new WrapPanel
        {
            Orientation = Orientation.Horizontal
        };

        buttonPanel.Children.Add(CreateButton("Advance Day", () => Advance(1)));
        buttonPanel.Children.Add(CreateButton("Advance 7 Days", () => Advance(7)));
        if (State.IsDraftUiEnabled)
        {
            buttonPanel.Children.Add(CreateButton("Board Up", MoveDraftBoardPlayerUp));
            buttonPanel.Children.Add(CreateButton("Board Down", MoveDraftBoardPlayerDown));
            buttonPanel.Children.Add(CreateButton("Star", StarTopProspect));
            buttonPanel.Children.Add(CreateButton("GM Note", AddDraftNote));
        }
        buttonPanel.Children.Add(CreateButton("Scout Focus", AssignScoutFocus));
        buttonPanel.Children.Add(CreateButton("Offer Recruit", MakeRecruitingOffer));
        buttonPanel.Children.Add(CreateButton("Approve Pending", ApprovePendingAction));
        buttonPanel.Children.Add(CreateButton("Decline Pending", DeclinePendingAction));
        if (State.IsDraftUiEnabled)
        {
            buttonPanel.Children.Add(CreateButton("Start Draft", StartDraft));
            buttonPanel.Children.Add(CreateButton("AI Picks", RunAiDrafting));
            buttonPanel.Children.Add(CreateButton("Draft Top", DraftTopProspect));
        }
        buttonPanel.Children.Add(CreateButton("Open Camp", OpenTrainingCamp));
        buttonPanel.Children.Add(CreateButton("Evaluate Camp", EvaluateTrainingCamp));
        buttonPanel.Children.Add(CreateButton("Keep Camp", KeepTrainingCampPlayer));
        buttonPanel.Children.Add(CreateButton("Cut Camp", CutTrainingCampPlayer));
        buttonPanel.Children.Add(CreateButton("Assign/Return", AssignOrReturnTrainingCampPlayer));
        buttonPanel.Children.Add(CreateButton("Complete Camp", CompleteTrainingCamp));

        panel.Children.Add(buttonPanel);

        header.Child = panel;
        return header;
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

    private void AddInboxTab(TabControl tabs)
    {
        tabs.Items.Add(new TabItem
        {
            Header = "Inbox",
            Content = BuildInboxLayout()
        });
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

    private void MoveDraftBoardPlayerUp() => State.MoveDraftBoardPlayer(direction: -1);

    private void MoveDraftBoardPlayerDown() => State.MoveDraftBoardPlayer(direction: 1);

    private void AssignScoutFocus() => State.AssignScoutFocus();

    private void MakeRecruitingOffer() => State.MakeRecruitingOffer();

    private void StarTopProspect() => State.StarTopProspect();

    private void AddDraftNote() => State.AddDraftNote();

    private void StartDraft() => State.StartDraft();

    private void RunAiDrafting() => State.RunAiDrafting();

    private void DraftTopProspect() => State.DraftTopProspect();

    private void ApprovePendingAction() => State.ApprovePendingAction();

    private void DeclinePendingAction() => State.DeclinePendingAction();

    private void OpenTrainingCamp() => State.OpenTrainingCamp();

    private void EvaluateTrainingCamp() => State.EvaluateTrainingCamp();

    private void KeepTrainingCampPlayer() => State.KeepTrainingCampPlayer();

    private void CutTrainingCampPlayer() => State.CutTrainingCampPlayer();

    private void AssignOrReturnTrainingCampPlayer() => State.AssignOrReturnTrainingCampPlayer();

    private void CompleteTrainingCamp() => State.CompleteTrainingCamp();

    private void MarkLatestInboxRead() => State.ManageLatestInboxMessage(InboxMessageAction.MarkRead);

    private void PinLatestInboxMessage() => State.ManageLatestInboxMessage(InboxMessageAction.Pin);

    private void ArchiveLatestInboxMessage() => State.ManageLatestInboxMessage(InboxMessageAction.Archive);

    private void DeleteLatestInboxMessage() => State.ManageLatestInboxMessage(InboxMessageAction.Delete);

    private void RefreshAll()
    {
        var snapshot = State.Snapshot;
        _dateText.Text = $"Current date: {snapshot.CurrentDate:yyyy-MM-dd}";
        _summaryText.Text = State.LatestSummary;
        _processedText.Text = $"Last processed events: {State.LastProcessedEventCount} | Inbox items: {State.Inbox.Count}";

        _tabs["Dashboard"].Text = BuildDashboard();
        RefreshInboxPanels();
        _tabs["Owner"].Text = BuildOwner();
        _tabs["Staff"].Text = BuildStaff();
        _tabs["Roster"].Text = BuildRoster();
        _tabs["Recruits"].Text = BuildRecruits();
        _tabs["Scouting"].Text = BuildScouting();
        _tabs["Pending Actions"].Text = BuildPendingActions();
        if (_tabs.ContainsKey("Draft Board"))
        {
            _tabs["Draft Board"].Text = BuildDraftBoard();
        }
        _tabs["Training Camp"].Text = BuildTrainingCamp();
        _tabs["Relationships"].Text = BuildRelationships();
    }

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
        builder.AppendLine("Goals");
        foreach (var goal in owner.Goals.OrderByDescending(goal => goal.Priority))
        {
            builder.AppendLine($"Priority {goal.Priority}: {goal.GoalType} - {goal.Description}");
        }

        return builder.ToString();
    }

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
        builder.AppendLine("Staff Room");
        foreach (var member in snapshot.StaffMembers.OrderBy(member => member.Department).ThenBy(member => member.CurrentRole))
        {
            builder.AppendLine($"{FindPersonName(member.PersonId)} - {member.CurrentRole} - {member.EmploymentStatus}");
            builder.AppendLine($"  Department: {member.Department}  Experience: {member.Profile.YearsExperience} years  Reputation: {member.Profile.Reputation}");
            builder.AppendLine($"  Contract: {member.ContractId ?? "reference not assigned"}");
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
        }

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
            builder.AppendLine($"Team selecting: {draftState.TeamSelecting}");
            builder.AppendLine($"Countdown: {draftState.CountdownPlaceholder}");
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
            builder.AppendLine($"  Projection: {entry.ProjectionText}");
            builder.AppendLine($"  Analytics: {(string.IsNullOrWhiteSpace(entry.AnalyticsSummary) ? "not available" : entry.AnalyticsSummary)}");
            builder.AppendLine($"  GM Notes: {(string.IsNullOrWhiteSpace(entry.PersonalNotes) ? "none" : entry.PersonalNotes)}");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private string BuildTrainingCamp()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Training Camp");
        builder.AppendLine("=============");
        builder.AppendLine($"Availability: {State.TrainingCampStatusText}");
        builder.AppendLine();

        if (State.ScenarioSnapshot.TrainingCamp is not { } camp)
        {
            builder.AppendLine(State.CanOpenTrainingCamp
                ? "Camp is ready. Use Open Camp to invite returning players, drafted prospects, and recruits."
                : "Camp unlocks after draft/offseason setup is complete.");
            builder.AppendLine();
            builder.AppendLine("Expected camp work");
            builder.AppendLine("- Review returning roster players");
            builder.AppendLine("- Check drafted prospects and recruits");
            builder.AppendLine("- Use staff evaluations before cuts");
            builder.AppendLine("- Set an opening roster after decisions");
            return builder.ToString();
        }

        builder.AppendLine($"Camp ID: {camp.CampId}");
        builder.AppendLine($"Opened: {camp.OpenedOn:yyyy-MM-dd}");
        builder.AppendLine($"Completed: {(camp.CompletedOn is null ? "No" : camp.CompletedOn.Value.ToString("yyyy-MM-dd"))}");
        builder.AppendLine($"Players invited: {camp.Players.Count}");
        builder.AppendLine($"Evaluations: {camp.Evaluations.Count}");
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

    private string FindPersonName(string personId)
    {
        if (string.Equals(personId, State.Snapshot.Owner.OwnerId, StringComparison.Ordinal))
        {
            return State.Snapshot.Owner.Name;
        }

        var person = State.Snapshot.People.SingleOrDefault(person => person.PersonId == personId);
        return person is null ? personId : person.Identity.DisplayName;
    }
}

internal sealed class AlphaDesktopState
{
    private readonly DailySimulationCoordinator _coordinator = new();
    private readonly NewGmScenarioActions _actions = new();
    private readonly AlphaDraftExperienceService _draftExperience = new();
    private readonly TrainingCampService _trainingCamp = new();
    private readonly PendingGmActionService _pendingActions = new();
    private readonly EngineRegistry _registry;
    public NewGmScenarioSnapshot ScenarioSnapshot { get; private set; }

    private AlphaDesktopState(EngineRegistry registry, NewGmScenarioSnapshot scenarioSnapshot)
    {
        _registry = registry;
        ScenarioSnapshot = scenarioSnapshot;
        Snapshot = scenarioSnapshot.AlphaSnapshot;
        InboxManager.AddRange(scenarioSnapshot.FirstDayInbox);
        LatestSummary = scenarioSnapshot.ScenarioSummary;
    }

    public AlphaWorldSnapshot Snapshot { get; private set; }

    public InboxManager InboxManager { get; } = new();

    public IReadOnlyList<InboxMessage> Inbox => InboxManager.Query(new InboxFilter());

    public IReadOnlyList<PendingGmAction> OpenPendingActions =>
        ScenarioSnapshot.PendingActions
            .Where(action => action.IsOpen)
            .OrderBy(action => action.CreatedOn)
            .ThenBy(action => action.Title, StringComparer.Ordinal)
            .ToArray();

    public bool IsDraftUiEnabled => DraftUiPolicy.IsDraftUiEnabled(_registry.Rulebook);

    public bool CanOpenTrainingCamp => _trainingCamp.CanOpenCamp(_registry, ScenarioSnapshot);

    public string TrainingCampStatusText =>
        ScenarioSnapshot.TrainingCamp switch
        {
            { IsCompleted: true } camp => $"Completed on {camp.CompletedOn:yyyy-MM-dd}",
            { } camp => $"Open with {camp.Players.Count} player(s)",
            _ when CanOpenTrainingCamp => "Ready to open",
            _ => "Locked until draft/offseason setup is complete"
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

        var totalProcessed = 0;
        var newInboxItems = new List<AlphaInboxItem>();
        AlphaSimulationResult? latest = null;

        for (var day = 0; day < days; day++)
        {
            latest = _coordinator.AdvanceOneDay(_registry, Snapshot);
            Snapshot = latest.WorldSnapshot;
            ScenarioSnapshot = ScenarioSnapshot with
            {
                AlphaSnapshot = Snapshot,
                Season = Snapshot.Season ?? ScenarioSnapshot.Season
            };
            totalProcessed += latest.ProcessedEventCount;
            newInboxItems.AddRange(latest.InboxItems);
        }

        InboxManager.AddRange(newInboxItems);
        LastProcessedEventCount = totalProcessed;
        LatestSummary = latest is null
            ? "No simulation result was produced."
            : days == 1
                ? latest.Summary
                : $"Advanced {Snapshot.WorldState.WorldName} over {days} days; processed {totalProcessed} event(s), created {newInboxItems.Count} inbox item(s), and ended on {Snapshot.CurrentDate:yyyy-MM-dd}.";
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

    public void OpenTrainingCamp()
    {
        if (!CanOpenTrainingCamp)
        {
            LatestSummary = "Training camp unlocks after draft/offseason setup is complete.";
            return;
        }

        ApplyCampResult(_trainingCamp.OpenCamp(_registry, ScenarioSnapshot));
    }

    public void EvaluateTrainingCamp()
    {
        if (ScenarioSnapshot.TrainingCamp is null)
        {
            LatestSummary = "Open training camp before requesting evaluations.";
            return;
        }

        ApplyCampResult(_trainingCamp.EvaluateCamp(_registry, ScenarioSnapshot));
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

    public void CompleteTrainingCamp()
    {
        if (ScenarioSnapshot.TrainingCamp is null)
        {
            LatestSummary = "Open training camp before completing it.";
            return;
        }

        ApplyCampResult(_trainingCamp.CompleteCamp(_registry, ScenarioSnapshot));
    }

    private void ApplyAction(GmActionResult result)
    {
        ScenarioSnapshot = result.ScenarioSnapshot;
        Snapshot = result.AlphaSnapshot;
        InboxManager.AddRange(result.InboxItems);
        LastProcessedEventCount = 0;
        LatestSummary = result.Summary;
    }

    private void ApplyDraftResult(DraftExperienceResult result)
    {
        ScenarioSnapshot = result.ScenarioSnapshot;
        Snapshot = result.ScenarioSnapshot.AlphaSnapshot;
        InboxManager.AddRange(result.InboxItems);
        LastProcessedEventCount = 0;
        LatestSummary = result.Summary;
    }

    private void ApplyCampResult(TrainingCampResult result)
    {
        ScenarioSnapshot = result.ScenarioSnapshot;
        Snapshot = result.ScenarioSnapshot.AlphaSnapshot;
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

    private bool EnsureDraftStarted()
    {
        if (ScenarioSnapshot.DraftExperience is not null)
        {
            return true;
        }

        StartDraft();
        return ScenarioSnapshot.DraftExperience is not null;
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
}
