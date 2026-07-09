internal sealed class Alpha610HockeyOperationsCommandCenterTests
{
    public void CommandCenterIsPrimaryHockeyOperationsScreen()
    {
        var source = DesktopSource();

        Assert.True(source.Contains("new(\"Command Center\", CreateHockeyOperationsCommandCenter())", StringComparison.Ordinal), "Hockey Operations should start with the Command Center.");
        Assert.True(source.Contains("RefreshHockeyOperationsCommandCenter();", StringComparison.Ordinal), "Command Center should refresh with the rest of AlphaDesktop.");
    }

    public void CommandCenterExposesRequestedSourceRails()
    {
        var source = DesktopSource();

        foreach (var label in new[] { "Roster", "Prospects", "AHL", "Junior Rights", "Free Agents", "Trade Targets" })
        {
            Assert.True(source.Contains($"\"{label}\"", StringComparison.Ordinal), $"Command Center should expose {label}.");
        }
    }

    public void CommandCenterExposesRequestedWorkViews()
    {
        var source = DesktopSource();

        foreach (var label in new[] { "Lines", "Roster", "Development", "Contracts", "Scouting", "Trade", "Free Agency" })
        {
            Assert.True(source.Contains($"\"{label}\"", StringComparison.Ordinal), $"Command Center should expose {label} work view.");
        }
    }

    public void SelectedPlayerCardShowsOperationsContext()
    {
        var source = DesktopSource();

        foreach (var label in new[] { "Photo", "Position", "Current role", "Potential role", "Current line", "Contract / rights", "Development", "Medical", "Relationships", "History" })
        {
            Assert.True(source.Contains($"\"{label}\"", StringComparison.Ordinal), $"Selected player card should show {label}.");
        }
    }

    public void QuickActionsAndContextMenuAreAvailable()
    {
        var source = DesktopSource();

        Assert.True(source.Contains("BuildCommandCenterContextMenu", StringComparison.Ordinal), "Command Center should provide a quick action context menu.");
        foreach (var label in new[] { "Dossier", "Assign Line", "Development", "Contract", "Scout", "Trade", "History" })
        {
            Assert.True(source.Contains($"\"{label}\"", StringComparison.Ordinal), $"Command Center should expose {label} action.");
        }
    }

    public void NoForbiddenSystemsAdded()
    {
        var source = DesktopSource();

        Assert.False(source.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Command Center should not add Godot.");
        Assert.False(source.Contains("MediaEngine", StringComparison.OrdinalIgnoreCase), "Command Center should not add media systems.");
        Assert.False(source.Contains("ConversationThread", StringComparison.OrdinalIgnoreCase), "Command Center should not add conversation threads.");
    }

    private static string DesktopSource() =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

    private static string FindRepositoryRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (File.Exists(Path.Combine(directory, "HockeyGmLegacy.slnx")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
