internal sealed class Alpha28GmOfficeNavigationTests
{
    public void DashboardLoadsAsGmOfficeWorkspace()
    {
        var source = ReadAlphaDesktopSource();

        Assert.True(source.Contains("Hockey GM Legacy - Alpha 2.9 - GM Office", StringComparison.Ordinal), "Desktop should identify the current Alpha GM Office.");
        Assert.True(source.Contains("AddWorkspaceTab(tabs, \"Dashboard\"", StringComparison.Ordinal), "Dashboard should be a top-level workspace.");
        Assert.True(source.Contains("Action Center / Pending Decisions", StringComparison.Ordinal), "Dashboard should expose the Action Center.");
        Assert.True(source.Contains("Quick search placeholder", StringComparison.Ordinal), "Header should expose a quick search placeholder.");
        Assert.True(source.Contains("Grouped advance controls", StringComparison.Ordinal), "Header should group advance controls.");
        Assert.True(source.Contains("Owner Mood", StringComparison.Ordinal), "Dashboard should show owner mood.");
    }

    public void OrganizationWorkspaceExposesOwnerStaffBudgetAndHealth()
    {
        var source = ReadAlphaDesktopSource();

        Assert.True(source.Contains("AddWorkspaceTab(tabs, \"Organization\"", StringComparison.Ordinal), "Organization should be a top-level workspace.");
        Assert.True(source.Contains("new WorkspaceScreen(\"Owner\", CreateTextScreen(\"Owner\"))", StringComparison.Ordinal), "Organization should expose Owner.");
        Assert.True(source.Contains("new WorkspaceScreen(\"Staff\", CreateSelectablePeopleContent(\"Staff\"))", StringComparison.Ordinal), "Organization should expose Staff.");
        Assert.True(source.Contains("new WorkspaceScreen(\"Staff Hiring\", CreateSelectablePeopleContent(\"Staff Hiring\"))", StringComparison.Ordinal), "Organization should expose Staff Hiring.");
        Assert.True(source.Contains("new WorkspaceScreen(\"Vacancies\", CreateSelectablePeopleContent(\"Vacancies\"))", StringComparison.Ordinal), "Organization should expose Vacancies.");
        Assert.True(source.Contains("new WorkspaceScreen(\"Budget\", CreateTextScreen(\"Budget\"))", StringComparison.Ordinal), "Organization should expose Budget.");
        Assert.True(source.Contains("new WorkspaceScreen(\"Organization Health\", CreateTextScreen(\"Organization Health\"))", StringComparison.Ordinal), "Organization should expose Organization Health.");
    }

    public void HockeyOperationsWorkspaceExposesRosterProspectsRecruitingScoutingDraftAndCamp()
    {
        var source = ReadAlphaDesktopSource();

        Assert.True(source.Contains("AddWorkspaceTab(tabs, \"Hockey Operations\"", StringComparison.Ordinal), "Hockey Operations should be a top-level workspace.");
        Assert.True(source.Contains("new(\"Roster\", CreateSelectablePeopleContent(\"Roster\"))", StringComparison.Ordinal), "Hockey Operations should expose Roster.");
        Assert.True(source.Contains("new(\"Prospects\", CreateSelectablePeopleContent(\"Prospect List\"))", StringComparison.Ordinal), "Hockey Operations should expose Prospects.");
        Assert.True(source.Contains("new(\"Recruits\", CreateSelectablePeopleContent(\"Recruits\"))", StringComparison.Ordinal), "Hockey Operations should expose Recruits.");
        Assert.True(source.Contains("new(\"Scouting\", CreateSelectablePeopleContent(\"Scouting\"))", StringComparison.Ordinal), "Hockey Operations should expose Scouting.");
        Assert.True(source.Contains("new(\"Scouting Operations\", CreateSelectablePeopleContent(\"Scouting Operations\"))", StringComparison.Ordinal), "Hockey Operations should expose Scouting Operations.");
        Assert.True(source.Contains("new WorkspaceScreen(\"Draft Board\", CreateSelectablePeopleContent(\"Draft Board\"))", StringComparison.Ordinal), "Hockey Operations should expose Draft Board when enabled.");
        Assert.True(source.Contains("new(\"Training Camp\", CreateSelectablePeopleContent(\"Training Camp\"))", StringComparison.Ordinal), "Hockey Operations should expose Training Camp.");
    }

    public void SeasonWorkspaceExposesScheduleStandingsStatsMonthlySummaryAndReadiness()
    {
        var source = ReadAlphaDesktopSource();

        Assert.True(source.Contains("AddWorkspaceTab(tabs, \"Season\"", StringComparison.Ordinal), "Season should be a top-level workspace.");
        Assert.True(source.Contains("new WorkspaceScreen(\"Schedule\", CreateTextScreen(\"Schedule\"))", StringComparison.Ordinal), "Season should expose Schedule.");
        Assert.True(source.Contains("new WorkspaceScreen(\"Standings\", CreateTextScreen(\"Standings\"))", StringComparison.Ordinal), "Season should expose Standings.");
        Assert.True(source.Contains("new WorkspaceScreen(\"Stats\", CreateTextScreen(\"Stats\"))", StringComparison.Ordinal), "Season should expose Stats.");
        Assert.True(source.Contains("new WorkspaceScreen(\"Monthly Summary\", CreateTextScreen(\"Monthly Summary\"))", StringComparison.Ordinal), "Season should expose Monthly Summary.");
        Assert.True(source.Contains("new WorkspaceScreen(\"Season Readiness\", CreateTextScreen(\"Season Readiness\"))", StringComparison.Ordinal), "Season should expose Season Readiness.");
    }

    public void ReportsWorkspaceExposesReportsSummariesAndHistoryPlaceholder()
    {
        var source = ReadAlphaDesktopSource();

        Assert.True(source.Contains("AddWorkspaceTab(tabs, \"Reports / History\"", StringComparison.Ordinal), "Reports / History should be a top-level workspace.");
        Assert.True(source.Contains("new WorkspaceScreen(\"Executive Reports\", CreateTextScreen(\"Executive Reports\"))", StringComparison.Ordinal), "Reports should expose Executive Reports.");
        Assert.True(source.Contains("new WorkspaceScreen(\"Draft Recaps\", CreateTextScreen(\"Draft Recaps\"))", StringComparison.Ordinal), "Reports should expose Draft Recaps.");
        Assert.True(source.Contains("new WorkspaceScreen(\"Monthly Summaries\", CreateTextScreen(\"Monthly Summaries\"))", StringComparison.Ordinal), "Reports should expose Monthly Summaries.");
        Assert.True(source.Contains("new WorkspaceScreen(\"Career History\", CreateTextScreen(\"Career History\"))", StringComparison.Ordinal), "Reports should expose a career history placeholder.");
    }

    public void MainNavigationIsReducedToGmOfficeWorkspaces()
    {
        var source = ReadAlphaDesktopSource();

        Assert.True(source.Contains("AddWorkspaceTab(tabs, \"Settings placeholder\"", StringComparison.Ordinal), "Settings placeholder should be a top-level workspace.");
        Assert.False(source.Contains("AddTab(tabs, \"Owner\")", StringComparison.Ordinal), "Owner should no longer be a crowded top-level tab.");
        Assert.False(source.Contains("AddSelectablePeopleTab(tabs, \"Roster\")", StringComparison.Ordinal), "Roster should live inside Hockey Operations.");
        Assert.False(source.Contains("AddTab(tabs, \"Schedule\")", StringComparison.Ordinal), "Schedule should live inside Season.");
        Assert.False(source.Contains("AddTab(tabs, \"League News\")", StringComparison.Ordinal), "League News should live near Inbox.");
    }

    private static string ReadAlphaDesktopSource() =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

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
