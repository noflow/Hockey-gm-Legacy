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

        Assert.False(source.Contains("AddSelectablePeopleTab(tabs, \"Player Dossier\")", StringComparison.Ordinal), "Player Dossier should no longer be a main navigation tab.");
        Assert.True(source.Contains("View Dossier", StringComparison.Ordinal), "Selected player detail should expose View Dossier.");
        Assert.True(source.Contains("OpenDossierFor(row.PersonId)", StringComparison.Ordinal), "View Dossier should route to the selected person's dossier window.");
        Assert.True(source.Contains("new Window", StringComparison.Ordinal), "View Dossier should open a dedicated dossier window.");
        Assert.True(source.Contains("BuildDossierWindowContent", StringComparison.Ordinal), "The dossier window should render structured dossier content.");
        Assert.True(source.Contains("Add GM Note", StringComparison.Ordinal), "Selected player detail should expose GM notes.");
    }

    public void DossierWindowSupportsEditableGmNotes()
    {
        var source = ReadAlphaDesktopSource();

        Assert.True(source.Contains("Save GM Note", StringComparison.Ordinal), "Dossier window should expose a save note action.");
        Assert.True(source.Contains("State.SaveDossierNoteFor(dossier.PersonId, notes.Text)", StringComparison.Ordinal), "Dossier window should save notes for the selected person.");
        Assert.True(source.Contains("GM Notes", StringComparison.Ordinal), "Dossier window should include the GM Notes section.");
        Assert.True(source.Contains("WindowStartupLocation.CenterOwner", StringComparison.Ordinal), "Dossier window should behave like a focused modal/detail window.");
    }

    public void RosterFiltersAndReadableFieldsAreExposed()
    {
        var source = ReadAlphaDesktopSource();

        Assert.True(source.Contains("BuildRosterFilters", StringComparison.Ordinal), "Roster should include filter controls.");
        Assert.True(source.Contains("_rosterSearchInput", StringComparison.Ordinal), "Roster should support text search.");
        Assert.True(source.Contains("_rosterPositionFilter", StringComparison.Ordinal), "Roster should support position filtering.");
        Assert.True(source.Contains("_rosterStatusFilter", StringComparison.Ordinal), "Roster should support status filtering.");
        Assert.True(source.Contains("_rosterPlayerTypeFilter", StringComparison.Ordinal), "Roster should support player type filtering.");
        Assert.True(source.Contains("_rosterRoleFilter", StringComparison.Ordinal), "Roster should support role filtering.");
        Assert.True(source.Contains("_rosterAgeFilter", StringComparison.Ordinal), "Roster should support age filtering.");
        Assert.True(source.Contains("State.ContractRightsStatus(player.PersonId)", StringComparison.Ordinal), "Roster rows should show contract or rights status.");
        Assert.True(source.Contains("State.DevelopmentTrend(player.PersonId)", StringComparison.Ordinal), "Roster rows should show development trend.");
        Assert.True(source.Contains("State.InjuryStatus(player.PersonId)", StringComparison.Ordinal), "Roster rows should show injury status.");
    }

    public void BudgetOverviewIsShownOnDashboardAndOwnerScreen()
    {
        var source = ReadAlphaDesktopSource();

        Assert.True(source.Contains("BudgetOverview", StringComparison.Ordinal), "AlphaDesktop should read the budget overview service.");
        Assert.True(source.Contains("Budget Overview", StringComparison.Ordinal), "Owner or organization screens should show a budget overview.");
        Assert.True(source.Contains("Player contracts", StringComparison.Ordinal), "Budget overview should include player contract totals.");
        Assert.True(source.Contains("Staff contracts", StringComparison.Ordinal), "Budget overview should include staff contract totals.");
        Assert.True(source.Contains("Scouting budget", StringComparison.Ordinal), "Budget overview should include scouting budget.");
        Assert.True(source.Contains("Owner status", StringComparison.Ordinal), "Budget overview should show owner budget status.");
    }

    public void ScoutingCleanupAndDurationUiIsExposed()
    {
        var source = ReadAlphaDesktopSource();

        Assert.True(source.Contains(".GroupBy(entry => entry.ProspectPersonId", StringComparison.Ordinal), "Scouting list should dedupe prospects by person.");
        Assert.True(source.Contains("ScoutingDisplayName", StringComparison.Ordinal), "Scouting rows should clarify same-name prospects.");
        Assert.True(source.Contains("ShowScoutAssignmentDialog", StringComparison.Ordinal), "Scouting assignment should use a popup/dialog.");
        Assert.True(source.Contains("1 week", StringComparison.Ordinal), "Scouting assignment dialog should support 1 week.");
        Assert.True(source.Contains("2 weeks", StringComparison.Ordinal), "Scouting assignment dialog should support 2 weeks.");
        Assert.True(source.Contains("3 weeks", StringComparison.Ordinal), "Scouting assignment dialog should support 3 weeks.");
        Assert.True(source.Contains("1 month", StringComparison.Ordinal), "Scouting assignment dialog should support 1 month.");
        Assert.True(source.Contains("AvailableScoutProfiles", StringComparison.Ordinal), "Only available scouts should be selectable.");
        Assert.True(source.Contains("ReturnDate", StringComparison.Ordinal), "Scouting operations should show or store scout return dates.");
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
