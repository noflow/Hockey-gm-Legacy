internal sealed class Alpha84UxNavigationTests
{
    public void PrimaryNavigationHasBreadcrumbsAndHistory()
    {
        var source = AlphaDesktopSource("Program.cs");

        Assert.True(source.Contains("BuildBreadcrumb", StringComparison.Ordinal), "Workspace breadcrumbs should exist.");
        Assert.True(source.Contains("NavigationSnapshot", StringComparison.Ordinal), "Navigation history snapshot should exist.");
        Assert.True(source.Contains("NavigateBack", StringComparison.Ordinal), "Back navigation should exist.");
        Assert.True(source.Contains("NavigateForward", StringComparison.Ordinal), "Forward navigation should exist.");
        Assert.True(source.Contains("_selectedPeopleByTab", StringComparison.Ordinal), "Selected person context should be preserved.");
        Assert.True(source.Contains("TrimStack(_backStack, 25)", StringComparison.Ordinal), "Navigation history should be bounded.");
    }

    public void KeyboardAndFocusSupportAreWired()
    {
        var source = AlphaDesktopSource("Program.cs");

        Assert.True(source.Contains("HandleGlobalKeyDown", StringComparison.Ordinal), "Global keyboard handler should exist.");
        Assert.True(source.Contains("Key.S", StringComparison.Ordinal), "Ctrl+S save shortcut should exist.");
        Assert.True(source.Contains("Key.F", StringComparison.Ordinal), "Ctrl+F search shortcut should exist.");
        Assert.True(source.Contains("Key.Escape", StringComparison.Ordinal), "Escape should close safe popups.");
        Assert.True(source.Contains("Focusable = true", StringComparison.Ordinal), "Buttons/popups should be focusable.");
        Assert.True(source.Contains("_lastPopupFocus?.Focus()", StringComparison.Ordinal), "Popup close should return focus.");
        Assert.True(source.Contains("Label", StringComparison.Ordinal) && source.Contains("Target = control", StringComparison.Ordinal), "Labels should target controls.");
    }

    public void EmptyFeedbackAndFilterStatesExist()
    {
        var source = AlphaDesktopSource("Program.cs");

        Assert.True(source.Contains("_feedbackText", StringComparison.Ordinal), "Persistent action feedback should exist.");
        Assert.True(source.Contains("SetFeedback", StringComparison.Ordinal), "Success feedback helper should exist.");
        Assert.True(source.Contains("ResetRosterFilters", StringComparison.Ordinal), "Roster filter reset should exist.");
        Assert.True(source.Contains("ResetActionCenterFilters", StringComparison.Ordinal), "Action Center filter reset should exist.");
        Assert.True(source.Contains("No urgent decisions today.", StringComparison.Ordinal), "Action Center empty state should be player-facing.");
        Assert.True(source.Contains("Clear Search", StringComparison.Ordinal), "Global search should have a clear action.");
    }

    public void DestructiveActionsRequireConfirmation()
    {
        var source = AlphaDesktopSource("Program.cs");

        Assert.True(source.Contains("ConfirmDestructiveAction", StringComparison.Ordinal), "Destructive action confirmation helper should exist.");
        Assert.True(source.Contains("Release Staff Member", StringComparison.Ordinal), "Staff release should require confirmation.");
        Assert.True(source.Contains("Place Player On Waivers", StringComparison.Ordinal), "Waiver placement should require confirmation.");
        Assert.True(source.Contains("Confirm Contract Buyout", StringComparison.Ordinal), "Buyout completion should require confirmation.");
        Assert.True(source.Contains("Release Prospect Rights", StringComparison.Ordinal), "Prospect rights release should require confirmation.");
        Assert.True(source.Contains("Walk Away From Arbitration", StringComparison.Ordinal), "Arbitration walk-away should require confirmation.");
    }

    public void SaveLoadAndDensityUxAreVisible()
    {
        var source = AlphaDesktopSource("Program.cs");

        Assert.True(source.Contains("Display density", StringComparison.Ordinal), "Display density control should be visible.");
        Assert.True(source.Contains("Comfortable", StringComparison.Ordinal), "Comfortable density should exist.");
        Assert.True(source.Contains("Compact", StringComparison.Ordinal), "Compact density should exist.");
        Assert.True(source.Contains("Save name:", StringComparison.Ordinal), "Save/load metadata should include save name.");
        Assert.True(source.Contains("Compatibility: Current save format supported", StringComparison.Ordinal), "Save/load compatibility should be shown.");
        Assert.True(source.Contains("Save version:", StringComparison.Ordinal), "Save version should be shown.");
    }

    public void ActionCenterGoToOpensSpecificContext()
    {
        var source = AlphaDesktopSource("Program.cs");

        Assert.True(source.Contains("ActionCenterCategory.Contracts => (\"Hockey Operations\", \"Contracts\")", StringComparison.Ordinal), "Contract actions should open Contracts.");
        Assert.True(source.Contains("ActionCenterCategory.Scouting => (\"Hockey Operations\", \"Scouting Operations\")", StringComparison.Ordinal), "Scouting actions should open Scouting Operations.");
        Assert.True(source.Contains("ActionCenterCategory.GameDay => (\"Season\", \"Schedule\")", StringComparison.Ordinal), "Game-day actions should open Schedule.");
        Assert.True(source.Contains("Opened related context", StringComparison.Ordinal), "Action Center navigation should give feedback.");
    }

    public void PlaytestFindingsAreDocumented()
    {
        var doc = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "docs", "06-ui-ux-bible", "ALPHA_8_4_UX_PLAYTEST_FINDINGS.md"));

        Assert.True(doc.Contains("Highest-Priority Findings Fixed", StringComparison.Ordinal), "UX findings should document fixed issues.");
        Assert.True(doc.Contains("Minimum Supported Resolution", StringComparison.Ordinal), "Minimum resolution should be documented.");
        Assert.True(doc.Contains("No New Gameplay", StringComparison.Ordinal), "Document should confirm no gameplay systems were added.");
    }

    public void NoHiddenRatingsOrRemoteTelemetryAdded()
    {
        var source = AlphaDesktopSource("Program.cs");
        var doc = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "docs", "06-ui-ux-bible", "ALPHA_8_4_UX_PLAYTEST_FINDINGS.md"));

        Assert.False(source.Contains("TrueOverall", StringComparison.Ordinal), "Presentation should not expose true overall ratings.");
        Assert.False(source.Contains("TruePotential", StringComparison.Ordinal), "Presentation should not expose true potential ratings.");
        Assert.False(source.Contains("HttpClient", StringComparison.Ordinal), "Alpha 8.4 should not add remote telemetry.");
        Assert.True(doc.Contains("not transmitted", StringComparison.OrdinalIgnoreCase), "Local UX counters should be documented as non-telemetry.");
    }

    private static string AlphaDesktopSource(string fileName) =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", fileName));

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "HockeyGmLegacy.slnx")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new InvalidOperationException("Repository root could not be located.");
    }
}
