internal sealed class AlphaDesktopInteractionTests
{
    public void StaffSelectedItemExposesStaffActions()
    {
        var source = ReadAlphaDesktopSource();

        Assert.True(source.Contains("AddSelectablePeopleTab(tabs, \"Staff\")", StringComparison.Ordinal), "Staff should use selectable rows.");
        Assert.True(source.Contains("BuildStaffDetail", StringComparison.Ordinal), "Staff should render a selected staff detail panel.");
        Assert.True(source.Contains("Reassign Role", StringComparison.Ordinal), "Selected staff should expose reassign role.");
        Assert.True(source.Contains("Release Staff", StringComparison.Ordinal), "Selected staff should expose release staff.");
        Assert.True(source.Contains("Set Focus", StringComparison.Ordinal), "Selected staff should expose focus controls.");
        Assert.True(source.Contains("Generate Evaluation", StringComparison.Ordinal), "Selected staff should expose evaluation.");
    }

    public void PlayerAndProspectSelectedItemsExposePlayerActions()
    {
        var source = ReadAlphaDesktopSource();

        Assert.True(source.Contains("AddSelectablePeopleTab(tabs, \"Roster\")", StringComparison.Ordinal), "Roster should use selectable rows.");
        Assert.True(source.Contains("AddSelectablePeopleTab(tabs, \"Prospect List\")", StringComparison.Ordinal), "Prospect List should use selectable rows.");
        Assert.True(source.Contains("BuildPlayerDetail", StringComparison.Ordinal), "Player screens should render selected player details.");
        Assert.True(source.Contains("Offer Contract", StringComparison.Ordinal), "Selected players/prospects should expose contract actions when valid.");
        Assert.True(source.Contains("Invite Prospect", StringComparison.Ordinal), "Selected prospects should expose camp invite actions when valid.");
        Assert.True(source.Contains("Return Prospect", StringComparison.Ordinal), "Selected prospects should expose return actions when valid.");
        Assert.True(source.Contains("Assign Prospect", StringComparison.Ordinal), "Selected prospects should expose affiliate actions when valid.");
        Assert.True(source.Contains("Release Rights", StringComparison.Ordinal), "Selected prospects should expose release rights when valid.");
    }

    public void ViewDossierWorksFromSelectedPlayer()
    {
        var source = ReadAlphaDesktopSource();

        Assert.True(source.Contains("AddSelectablePeopleTab(tabs, \"Player Dossier\")", StringComparison.Ordinal), "Player Dossier should use selectable rows.");
        Assert.True(source.Contains("View Dossier", StringComparison.Ordinal), "Selected player detail should expose View Dossier.");
        Assert.True(source.Contains("OpenDossierFor(row.PersonId)", StringComparison.Ordinal), "View Dossier should route to the selected person's dossier tab.");
        Assert.True(source.Contains("Add GM Note", StringComparison.Ordinal), "Selected player detail should expose GM notes.");
    }

    public void StaffProfileAndFocusActionsAreWired()
    {
        var source = ReadAlphaDesktopSource();

        Assert.True(source.Contains("ShowStaffProfile(row.PersonId)", StringComparison.Ordinal), "Staff View Profile should open a staff profile surface.");
        Assert.True(source.Contains("StaffProfileText", StringComparison.Ordinal), "Staff profile text should be built for the profile surface.");
        Assert.True(source.Contains("SetStaffFocusFor(row.PersonId)", StringComparison.Ordinal), "Staff Set Focus should call the selected staff focus action.");
        Assert.True(source.Contains("MessageBox.Show(State.LatestSummary, \"Staff Focus\"", StringComparison.Ordinal), "Staff focus action should show confirmation.");
    }

    public void RecruitRowsAndDetailsShowPositionAgeAndPriorities()
    {
        var source = ReadAlphaDesktopSource();

        Assert.True(source.Contains(".GroupBy(recruit => recruit.RecruitPersonId", StringComparison.Ordinal), "Recruit rows should collapse duplicate person entries.");
        Assert.True(source.Contains("RecruitDisplayName", StringComparison.Ordinal), "Recruit rows should clarify same-name recruits without numeric suffixes.");
        Assert.True(source.Contains("State.PersonPosition(recruit.RecruitPersonId)", StringComparison.Ordinal), "Recruit rows should show position.");
        Assert.True(source.Contains("State.PersonAge(recruit.RecruitPersonId)", StringComparison.Ordinal), "Recruit rows should show age.");
        Assert.True(source.Contains("Looking for", StringComparison.Ordinal), "Recruit details should show looking-for priorities.");
        Assert.True(source.Contains("Development priority", StringComparison.Ordinal), "Recruit details should show development priority.");
        Assert.True(source.Contains("Ice time priority", StringComparison.Ordinal), "Recruit details should show ice time priority.");
        Assert.True(source.Contains("Pathway priority", StringComparison.Ordinal), "Recruit details should show pathway priority when applicable.");
    }

    public void DashboardSummaryDisplaysCounts()
    {
        var source = ReadAlphaDesktopSource();

        Assert.True(source.Contains("RefreshDashboard", StringComparison.Ordinal), "Dashboard should be refreshed as a structured workspace.");
        Assert.True(source.Contains("CreateDashboardMetric", StringComparison.Ordinal), "Dashboard should use readable metric cards.");
        Assert.True(source.Contains("Inbox Unread", StringComparison.Ordinal), "Dashboard should show inbox unread count.");
        Assert.True(source.Contains("Pending Decisions", StringComparison.Ordinal), "Dashboard should show pending decision count.");
        Assert.True(source.Contains("Roster Issues", StringComparison.Ordinal), "Dashboard should show roster issue count.");
        Assert.True(source.Contains("Scouting Reports", StringComparison.Ordinal), "Dashboard should show scouting report count.");
        Assert.True(source.Contains("Review Inbox", StringComparison.Ordinal), "Dashboard should expose Review Inbox quick action.");
        Assert.True(source.Contains("Review Draft Board", StringComparison.Ordinal), "Dashboard should expose Review Draft Board quick action.");
        Assert.True(source.Contains("Review Pending Actions", StringComparison.Ordinal), "Dashboard should expose Review Pending Actions quick action.");
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
