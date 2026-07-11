internal sealed class Alpha85GmOfficeExperienceTests
{
    public void GmOfficeHomeReplacesMetricDashboard()
    {
        var source = AlphaDesktopSource("Program.cs");

        Assert.True(source.Contains("BuildGmOfficeHome", StringComparison.Ordinal), "GM Office home should be built explicitly.");
        Assert.True(source.Contains("GM Office", StringComparison.Ordinal), "Dashboard should present as GM Office.");
        Assert.True(source.Contains("Good Morning", StringComparison.Ordinal), "Morning greeting should exist.");
        Assert.True(source.Contains("Everything below is connected to a live workspace.", StringComparison.Ordinal), "Home should explain connected workflow.");
    }

    public void MorningBriefingPanelsExist()
    {
        var source = AlphaDesktopSource("Program.cs");

        Assert.True(source.Contains("BuildMorningBriefingCard", StringComparison.Ordinal), "Morning briefing card should exist.");
        Assert.True(source.Contains("BuildTodayAgendaCard", StringComparison.Ordinal), "Today's Agenda card should exist.");
        Assert.True(source.Contains("Assistant GM", StringComparison.Ordinal), "Assistant GM panel should exist.");
        Assert.True(source.Contains("Coach Morning Report", StringComparison.Ordinal), "Coach morning report should exist.");
        Assert.True(source.Contains("Head Scout Report", StringComparison.Ordinal), "Head scout report should exist.");
        Assert.True(source.Contains("Medical Report", StringComparison.Ordinal), "Medical report should exist.");
        Assert.True(source.Contains("Owner Report", StringComparison.Ordinal), "Owner report should exist.");
    }

    public void OfficeSidebarSnapshotsExist()
    {
        var source = AlphaDesktopSource("Program.cs");

        Assert.True(source.Contains("BuildOrganizationSnapshotCard", StringComparison.Ordinal), "Organization snapshot should exist.");
        Assert.True(source.Contains("BuildLeagueSnapshotCard", StringComparison.Ordinal), "League snapshot should exist.");
        Assert.True(source.Contains("BuildPlayerOfInterestCard", StringComparison.Ordinal), "Player of Interest card should exist.");
        Assert.True(source.Contains("Organization Snapshot", StringComparison.Ordinal), "Organization snapshot title should be visible.");
        Assert.True(source.Contains("League Snapshot", StringComparison.Ordinal), "League snapshot title should be visible.");
        Assert.True(source.Contains("Player of Interest", StringComparison.Ordinal), "Player of Interest title should be visible.");
    }

    public void OfficeCardsNavigateToWorkspaces()
    {
        var source = AlphaDesktopSource("Program.cs");

        Assert.True(source.Contains("SelectWorkspaceScreen(\"Dashboard\", \"Action Center / Pending Decisions\")", StringComparison.Ordinal), "Action Center card should navigate.");
        Assert.True(source.Contains("SelectWorkspaceScreen(\"Hockey Operations\", \"Lineup\")", StringComparison.Ordinal), "Coach card should navigate to Lineup.");
        Assert.True(source.Contains("SelectWorkspaceScreen(\"Hockey Operations\", \"Scouting\")", StringComparison.Ordinal), "Scout card should navigate to Scouting.");
        Assert.True(source.Contains("SelectWorkspaceScreen(\"Season\", \"Standings\")", StringComparison.Ordinal), "League snapshot should navigate to Standings.");
        Assert.True(source.Contains("OpenUniversalPersonCard(row.PersonId)", StringComparison.Ordinal), "Player of Interest should open a person card.");
        Assert.True(source.Contains("OpenDossierFor(row.PersonId)", StringComparison.Ordinal), "Player of Interest should open dossier.");
    }

    public void CardConsistencyAndNoGameplayChangeDocumented()
    {
        var source = AlphaDesktopSource("Program.cs");
        var doc = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "docs", "06-ui-ux-bible", "ALPHA_8_5_IMMERSIVE_GM_OFFICE.md"));

        Assert.True(source.Contains("UiPresentation.Card(panel)", StringComparison.Ordinal), "Office sections should use the shared card system.");
        Assert.True(doc.Contains("No New Gameplay", StringComparison.Ordinal), "Document should confirm no gameplay systems were added.");
        Assert.True(doc.Contains("card-based", StringComparison.OrdinalIgnoreCase), "Document should mention card-based presentation.");
        Assert.False(doc.Contains("remote telemetry", StringComparison.OrdinalIgnoreCase), "Alpha 8.5 should not add telemetry.");
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
