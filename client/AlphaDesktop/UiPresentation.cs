using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using LegacyEngine.Integration;

namespace AlphaDesktop;

internal static class UiTheme
{
    public static readonly Brush Surface = Brushes.White;
    public static readonly Brush SurfaceAlt = new SolidColorBrush(Color.FromRgb(248, 250, 253));
    public static readonly Brush Border = new SolidColorBrush(Color.FromRgb(222, 229, 237));
    public static readonly Brush Text = new SolidColorBrush(Color.FromRgb(20, 40, 64));
    public static readonly Brush MutedText = new SolidColorBrush(Color.FromRgb(84, 99, 116));
    public static readonly Brush Selected = new SolidColorBrush(Color.FromRgb(220, 236, 252));
    public static readonly Brush Hover = new SolidColorBrush(Color.FromRgb(241, 247, 253));
    public static readonly Brush Info = new SolidColorBrush(Color.FromRgb(45, 101, 170));
    public static readonly Brush Positive = new SolidColorBrush(Color.FromRgb(45, 132, 82));
    public static readonly Brush Caution = new SolidColorBrush(Color.FromRgb(171, 123, 20));
    public static readonly Brush Attention = new SolidColorBrush(Color.FromRgb(189, 91, 30));
    public static readonly Brush Critical = new SolidColorBrush(Color.FromRgb(180, 52, 52));
    public static readonly Brush Neutral = new SolidColorBrush(Color.FromRgb(111, 122, 135));
    public static readonly Brush Gold = new SolidColorBrush(Color.FromRgb(179, 134, 34));
    public static readonly Brush Purple = new SolidColorBrush(Color.FromRgb(111, 76, 150));
    public static readonly Brush Complete = new SolidColorBrush(Color.FromRgb(35, 35, 35));
}

internal static class UiSpacing
{
    public const double Xs = 4;
    public const double Sm = 8;
    public const double Md = 12;
    public const double Lg = 18;
    public const double Xl = 24;

    public static Thickness CardPadding => new(16);
    public static Thickness RowPadding => new(12, 9, 12, 9);
    public static Thickness SectionMargin => new(0, 14, 0, 8);
}

internal static class UiTypography
{
    public const double ScreenTitle = 24;
    public const double CardTitle = 20;
    public const double SectionTitle = 15;
    public const double Body = 13;
    public const double Small = 12;
}

internal sealed record UiNavigationContext(string Workspace, string Screen, string? PersonId);

internal static class UiPresentation
{
    public static Border UiTeamCrest(TeamBrandingProfile profile, double size = 46)
    {
        var border = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = profile.LogoPlaceholder == TeamLogoPlaceholder.Shield ? new CornerRadius(8, 8, 14, 14) : new CornerRadius(size / 2),
            Background = BrushFromHex(profile.Palette.Primary),
            BorderBrush = BrushFromHex(profile.Palette.Accent),
            BorderThickness = new Thickness(Math.Max(2, size / 16)),
            ToolTip = $"{profile.OrganizationDisplayName} placeholder crest: {profile.LogoPlaceholder}, {profile.VisualStyleDescriptor}",
            Child = new Grid()
        };

        if (border.Child is Grid grid)
        {
            grid.Children.Add(new TextBlock
            {
                Text = profile.Monogram.Letters,
                Foreground = BrushFromHex(profile.Palette.ReadableForeground),
                FontSize = Math.Max(13, size / 3.2),
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });

            if (profile.LogoPlaceholder is TeamLogoPlaceholder.DiagonalStripeBadge or TeamLogoPlaceholder.WaveBadge)
            {
                grid.Children.Add(new Border
                {
                    Height = Math.Max(4, size / 10),
                    Background = BrushFromHex(profile.Palette.Accent),
                    Opacity = 0.75,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(6, 0, 6, size / 5)
                });
            }
        }

        return border;
    }

    public static Border UiTeamHeader(
        TeamBrandingProfile team,
        LeagueBrandingProfile league,
        string record,
        string strategy,
        string ownerMood,
        string budgetStatus)
    {
        var root = new Border
        {
            Background = BrushFromHex(team.Palette.LightBackgroundTint),
            BorderBrush = BrushFromHex(team.Palette.Primary),
            BorderThickness = new Thickness(4, 0, 0, 0),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14),
            Margin = new Thickness(0, 0, 0, 12)
        };

        var dock = new DockPanel { LastChildFill = true };
        var crest = UiTeamCrest(team, 52);
        crest.Margin = new Thickness(0, 0, 12, 0);
        DockPanel.SetDock(crest, Dock.Left);
        dock.Children.Add(crest);

        var content = new StackPanel();
        content.Children.Add(new TextBlock
        {
            Text = $"{team.OrganizationDisplayName} ({team.TeamAbbreviation})",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = UiTheme.Text
        });
        content.Children.Add(new TextBlock
        {
            Text = $"{league.ShortName} | {team.ConferenceDivision} | {team.ArenaName}",
            Foreground = UiTheme.MutedText,
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(BadgeRow(
            ($"Record {record}", "neutral"),
            (strategy, "info"),
            (ownerMood, ownerMood.Contains("warning", StringComparison.OrdinalIgnoreCase) ? "caution" : "neutral"),
            (budgetStatus, budgetStatus.Contains("over", StringComparison.OrdinalIgnoreCase) || budgetStatus.Contains("violation", StringComparison.OrdinalIgnoreCase) ? "critical" : "positive")));
        dock.Children.Add(content);
        root.Child = dock;
        return root;
    }

    public static Border UiTeamCard(TeamBrandingProfile profile, string title, string subtitle, IReadOnlyList<string> facts, bool selected = false)
    {
        var root = new Border
        {
            Background = UiTheme.Surface,
            BorderBrush = selected ? BrushFromHex(profile.Palette.Accent) : UiTheme.Border,
            BorderThickness = selected ? new Thickness(2) : new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 10)
        };

        var panel = new StackPanel();
        var header = new DockPanel { LastChildFill = true };
        var crest = UiTeamCrest(profile, 42);
        crest.Margin = new Thickness(0, 0, 10, 0);
        DockPanel.SetDock(crest, Dock.Left);
        header.Children.Add(crest);
        var titlePanel = new StackPanel();
        titlePanel.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = 15, Foreground = UiTheme.Text, TextWrapping = TextWrapping.Wrap });
        titlePanel.Children.Add(new TextBlock { Text = subtitle, FontSize = UiTypography.Small, Foreground = UiTheme.MutedText, TextWrapping = TextWrapping.Wrap });
        header.Children.Add(titlePanel);
        panel.Children.Add(header);

        foreach (var fact in facts.Where(fact => !string.IsNullOrWhiteSpace(fact)).Take(6))
        {
            panel.Children.Add(new TextBlock
            {
                Text = fact,
                Foreground = UiTheme.MutedText,
                FontSize = UiTypography.Small,
                Margin = new Thickness(0, 5, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });
        }

        root.Child = panel;
        return root;
    }

    public static Border UiPersonAvatar(string displayName, string role, TeamBrandingProfile? profile, string position = "")
    {
        var initials = InitialsFor(displayName);
        var teamProfile = profile ?? FallbackTeamBranding();
        var roleText = role.ToLowerInvariant() switch
        {
            var value when value.Contains("goalie", StringComparison.Ordinal) => "G",
            var value when value.Contains("coach", StringComparison.Ordinal) => "CO",
            var value when value.Contains("scout", StringComparison.Ordinal) => "SC",
            var value when value.Contains("medical", StringComparison.Ordinal) || value.Contains("trainer", StringComparison.Ordinal) => "MED",
            var value when value.Contains("owner", StringComparison.Ordinal) => "OWN",
            var value when value.Contains("agent", StringComparison.Ordinal) => "AG",
            var value when value.Contains("gm", StringComparison.Ordinal) || value.Contains("general manager", StringComparison.Ordinal) => "GM",
            _ => string.IsNullOrWhiteSpace(position) ? "PL" : position
        };

        var grid = new Grid();
        grid.Children.Add(new TextBlock
        {
            Text = initials,
            Foreground = BrushFromHex(teamProfile.Palette.ReadableForeground),
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });
        grid.Children.Add(new Border
        {
            Background = BrushFromHex(teamProfile.Palette.Accent),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(4, 1, 4, 1),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Child = new TextBlock
            {
                Text = roleText,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            }
        });

        return new Border
        {
            Width = 42,
            Height = 42,
            CornerRadius = new CornerRadius(21),
            Background = BrushFromHex(teamProfile.Palette.Primary),
            BorderBrush = BrushFromHex(teamProfile.Palette.Secondary),
            BorderThickness = new Thickness(2),
            ToolTip = $"{displayName} placeholder avatar: {roleText}. No real portrait is used.",
            Child = grid
        };
    }

    public static Border UiIconLabel(UiVisualIdentity identity)
    {
        var row = new DockPanel { LastChildFill = true };
        var icon = new Border
        {
            Background = BadgeBackground(identity.Semantic),
            BorderBrush = BadgeForeground(identity.Semantic),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(5, 2, 5, 2),
            Margin = new Thickness(0, 0, 7, 0),
            Child = new TextBlock
            {
                Text = IconToken(identity.Icon),
                Foreground = BadgeForeground(identity.Semantic),
                FontWeight = FontWeights.Bold,
                FontSize = 10
            }
        };
        DockPanel.SetDock(icon, Dock.Left);
        row.Children.Add(icon);
        row.Children.Add(new TextBlock { Text = identity.Label, Foreground = UiTheme.Text, VerticalAlignment = VerticalAlignment.Center });
        return new Border { Child = row, ToolTip = identity.Tooltip };
    }

    public static Border UiFinanceBar(string title, decimal used, decimal total, string status)
    {
        var percent = total <= 0 ? 0 : Math.Clamp((double)(used / total), 0, 1);
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, Foreground = UiTheme.Text });
        panel.Children.Add(new TextBlock { Text = $"{used:C0} / {total:C0} | {Math.Max(0, total - used):C0} remaining | {status}", Foreground = UiTheme.MutedText, FontSize = UiTypography.Small });
        panel.Children.Add(new Border
        {
            Height = 10,
            Background = new SolidColorBrush(Color.FromRgb(230, 235, 241)),
            CornerRadius = new CornerRadius(5),
            Margin = new Thickness(0, 7, 0, 0),
            Child = new Grid
            {
                Children =
                {
                    new Border
                    {
                        Width = 220 * percent,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Background = BadgeForeground(status.Contains("over", StringComparison.OrdinalIgnoreCase) ? "critical" : percent > 0.9 ? "caution" : "positive"),
                        CornerRadius = new CornerRadius(5)
                    }
                }
            }
        });
        return Card(panel);
    }

    public static DataTemplate PersonRowTemplate()
    {
        var root = new FrameworkElementFactory(typeof(Border));
        root.SetValue(Border.PaddingProperty, UiSpacing.RowPadding);
        root.SetValue(Border.BorderBrushProperty, UiTheme.Border);
        root.SetValue(Border.BorderThicknessProperty, new Thickness(0, 0, 0, 1));
        root.SetValue(Border.CursorProperty, Cursors.Hand);
        root.SetValue(Border.BackgroundProperty, UiTheme.SurfaceAlt);
        root.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, 4));

        var stack = new FrameworkElementFactory(typeof(StackPanel));
        stack.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);

        var header = new FrameworkElementFactory(typeof(DockPanel));
        header.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, 3));

        var badge = new FrameworkElementFactory(typeof(Border));
        badge.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        badge.SetValue(Border.PaddingProperty, new Thickness(7, 2, 7, 2));
        badge.SetValue(Border.BackgroundProperty, UiTheme.Selected);
        badge.SetValue(DockPanel.DockProperty, Dock.Right);
        var badgeText = new FrameworkElementFactory(typeof(TextBlock));
        badgeText.SetBinding(TextBlock.TextProperty, new Binding("Kind"));
        badgeText.SetValue(TextBlock.FontSizeProperty, UiTypography.Small);
        badgeText.SetValue(TextBlock.ForegroundProperty, UiTheme.Info);
        badge.AppendChild(badgeText);
        header.AppendChild(badge);

        var name = new FrameworkElementFactory(typeof(TextBlock));
        name.SetBinding(TextBlock.TextProperty, new Binding("Name"));
        name.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        name.SetValue(TextBlock.FontSizeProperty, 14.0);
        name.SetValue(TextBlock.ForegroundProperty, UiTheme.Info);
        name.SetValue(TextBlock.TextDecorationsProperty, TextDecorations.Underline);
        name.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        name.SetValue(FrameworkElement.ToolTipProperty, "Click to open the person card.");
        header.AppendChild(name);

        var primary = new FrameworkElementFactory(typeof(TextBlock));
        primary.SetBinding(TextBlock.TextProperty, new Binding("Primary"));
        primary.SetValue(TextBlock.ForegroundProperty, UiTheme.Text);
        primary.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);

        var secondary = new FrameworkElementFactory(typeof(TextBlock));
        secondary.SetBinding(TextBlock.TextProperty, new Binding("Secondary"));
        secondary.SetValue(TextBlock.ForegroundProperty, UiTheme.MutedText);
        secondary.SetValue(TextBlock.FontSizeProperty, UiTypography.Small);
        secondary.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        secondary.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 3, 0, 0));

        stack.AppendChild(header);
        stack.AppendChild(primary);
        stack.AppendChild(secondary);
        root.AppendChild(stack);

        return new DataTemplate { VisualTree = root };
    }

    public static Border UiStatusBadge(string text, string semantic = "neutral") =>
        new()
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 0, 6, 6),
            Background = BadgeBackground(semantic),
            Child = new TextBlock
            {
                Text = text,
                FontSize = UiTypography.Small,
                Foreground = BadgeForeground(semantic),
                FontWeight = FontWeights.SemiBold
            }
        };

    public static Border UiSummaryCard(string title, string value, string detail)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = title, FontSize = UiTypography.Small, Foreground = UiTheme.MutedText });
        panel.Children.Add(new TextBlock { Text = value, FontSize = 18, FontWeight = FontWeights.SemiBold, Foreground = UiTheme.Text, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = detail, FontSize = UiTypography.Small, Foreground = UiTheme.MutedText, TextWrapping = TextWrapping.Wrap });
        return Card(panel);
    }

    public static TextBlock UiSectionHeader(string text) =>
        new()
        {
            Text = text,
            FontSize = UiTypography.SectionTitle,
            FontWeight = FontWeights.SemiBold,
            Foreground = UiTheme.Text,
            Margin = UiSpacing.SectionMargin
        };

    public static Button UiPersonLink(string name, Action action)
    {
        var button = new Button
        {
            Content = name,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Foreground = UiTheme.Info,
            FontWeight = FontWeights.SemiBold,
            Cursor = Cursors.Hand,
            ToolTip = "Open person card"
        };
        button.Click += (_, _) => action();
        return button;
    }

    public static Border UiPersonCard(StackPanel content) => Card(content);

    public static UIElement UiInfoRow(string label, object? value)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var left = new TextBlock { Text = label, Foreground = UiTheme.MutedText, FontSize = UiTypography.Small };
        var right = new TextBlock { Text = value?.ToString() ?? "unknown", Foreground = UiTheme.Text, TextWrapping = TextWrapping.Wrap };
        Grid.SetColumn(right, 1);
        grid.Children.Add(left);
        grid.Children.Add(right);
        return grid;
    }

    public static Border UiMetricCard(string title, string value, string detail) => UiSummaryCard(title, value, detail);

    public static Expander UiExpandableSection(string title, UIElement content, bool expanded = false) =>
        new()
        {
            Header = title,
            IsExpanded = expanded,
            Margin = new Thickness(0, 6, 0, 6),
            Content = content,
            Foreground = UiTheme.Text
        };

    public static Border UiEmptyState(string title, string message)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = UiTypography.CardTitle, Foreground = UiTheme.Text });
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Foreground = UiTheme.MutedText, Margin = new Thickness(0, 6, 0, 0) });
        return Card(panel);
    }

    public static Border UiAlertBanner(string message, string semantic = "caution") =>
        new()
        {
            Background = BadgeBackground(semantic),
            BorderBrush = BadgeForeground(semantic),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 0, 0, 10),
            Child = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Foreground = BadgeForeground(semantic), FontWeight = FontWeights.SemiBold }
        };

    public static WrapPanel BadgeRow(params (string Text, string Semantic)[] badges)
    {
        var row = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 8) };
        foreach (var badge in badges.Where(badge => !string.IsNullOrWhiteSpace(badge.Text)))
        {
            row.Children.Add(UiStatusBadge(badge.Text, badge.Semantic));
        }

        return row;
    }

    public static Border Card(UIElement child) =>
        new()
        {
            Background = UiTheme.Surface,
            BorderBrush = UiTheme.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = UiSpacing.CardPadding,
            Margin = new Thickness(0, 0, 0, 12),
            Child = child
        };

    public static Brush BrushFromHex(string hex)
    {
        try
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }
        catch (FormatException)
        {
            return UiTheme.Neutral;
        }
    }

    private static string IconToken(UiIcon icon) =>
        icon switch
        {
            UiIcon.Dashboard => "HOME",
            UiIcon.Inbox => "MAIL",
            UiIcon.HockeyOperations => "HO",
            UiIcon.Organization => "ORG",
            UiIcon.League => "LG",
            UiIcon.Season => "SEAS",
            UiIcon.Reports => "REP",
            UiIcon.Settings => "SET",
            UiIcon.Roster => "ROS",
            UiIcon.Scouting => "SCT",
            UiIcon.Trade => "TRD",
            UiIcon.Sign => "SIGN",
            UiIcon.Injured => "MED",
            UiIcon.Warning => "WARN",
            UiIcon.Critical => "CRIT",
            UiIcon.Goalie => "G",
            UiIcon.Coach => "CO",
            UiIcon.Scout => "SC",
            UiIcon.Owner => "OWN",
            UiIcon.Agent => "AG",
            _ => icon.ToString().ToUpperInvariant()[..Math.Min(4, icon.ToString().Length)]
        };

    private static string InitialsFor(string displayName)
    {
        var parts = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => part.Any(char.IsLetter))
            .ToArray();
        if (parts.Length >= 2)
        {
            return $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
        }

        return parts.Length == 1 ? parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant() : "HG";
    }

    private static TeamBrandingProfile FallbackTeamBranding() =>
        new(
            "ui-fallback",
            "Hockey Club",
            "Hockey",
            "fallback",
            "League",
            "Division TBD",
            "Placeholder Arena",
            "HGM",
            new TeamMonogram("HG"),
            TeamLogoPlaceholder.RingMonogram,
            new BrandColorPalette("#2A4558", "#F2F4F6", "#A85F2E", "#EEF4F7", "#1A2E3B", "#FFFFFF"),
            "Clean",
            "compact color edge",
            "two-stripe placeholder",
            "Fallback hockey identity");

    private static Brush BadgeBackground(string semantic) =>
        semantic.ToLowerInvariant() switch
        {
            "positive" or "healthy" or "improving" => new SolidColorBrush(Color.FromRgb(224, 243, 232)),
            "caution" or "expiring" or "plateau" => new SolidColorBrush(Color.FromRgb(255, 245, 215)),
            "attention" or "important" or "recovering" => new SolidColorBrush(Color.FromRgb(255, 233, 218)),
            "critical" or "injured" or "urgent" => new SolidColorBrush(Color.FromRgb(255, 225, 225)),
            "gold" or "elite" => new SolidColorBrush(Color.FromRgb(255, 241, 196)),
            "purple" or "franchise" => new SolidColorBrush(Color.FromRgb(239, 231, 250)),
            "black" => new SolidColorBrush(Color.FromRgb(225, 225, 225)),
            "info" or "selected" => new SolidColorBrush(Color.FromRgb(221, 236, 252)),
            _ => new SolidColorBrush(Color.FromRgb(236, 240, 245))
        };

    private static Brush BadgeForeground(string semantic) =>
        semantic.ToLowerInvariant() switch
        {
            "positive" or "healthy" or "improving" => UiTheme.Positive,
            "caution" or "expiring" or "plateau" => UiTheme.Caution,
            "attention" or "important" or "recovering" => UiTheme.Attention,
            "critical" or "injured" or "urgent" => UiTheme.Critical,
            "gold" or "elite" => UiTheme.Gold,
            "purple" or "franchise" => UiTheme.Purple,
            "black" => UiTheme.Complete,
            "info" or "selected" => UiTheme.Info,
            _ => UiTheme.Neutral
        };
}

internal static class UiElementExtensions
{
    public static T Also<T>(this T item, Action<T> configure)
    {
        configure(item);
        return item;
    }
}
