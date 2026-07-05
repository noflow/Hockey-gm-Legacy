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
        Assert.True(source.Contains("State.OpenDossier(row.PersonId)", StringComparison.Ordinal), "View Dossier should open the selected person's dossier.");
        Assert.True(source.Contains("Add GM Note", StringComparison.Ordinal), "Selected player detail should expose GM notes.");
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
