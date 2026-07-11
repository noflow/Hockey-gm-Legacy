internal sealed class Alpha80PresentationLayerTests
{
    public void SharedPresentationComponentsExist()
    {
        var source = AlphaDesktopSource();
        var presentation = PresentationSource();

        Assert.True(presentation.Contains("internal static class UiTheme", StringComparison.Ordinal), "UiTheme should define shared colors.");
        Assert.True(presentation.Contains("internal static class UiSpacing", StringComparison.Ordinal), "UiSpacing should define shared spacing.");
        Assert.True(presentation.Contains("internal static class UiTypography", StringComparison.Ordinal), "UiTypography should define shared type scale.");
        Assert.True(presentation.Contains("UiStatusBadge", StringComparison.Ordinal), "Status badges should be reusable.");
        Assert.True(presentation.Contains("UiSummaryCard", StringComparison.Ordinal), "Summary cards should be reusable.");
        Assert.True(presentation.Contains("UiSectionHeader", StringComparison.Ordinal), "Section headers should be reusable.");
        Assert.True(presentation.Contains("UiPersonLink", StringComparison.Ordinal), "Person links should be reusable.");
        Assert.True(presentation.Contains("UiPersonCard", StringComparison.Ordinal), "Universal person card shell should be reusable.");
        Assert.True(presentation.Contains("UiInfoRow", StringComparison.Ordinal), "Info rows should be reusable.");
        Assert.True(presentation.Contains("UiMetricCard", StringComparison.Ordinal), "Metric cards should be reusable.");
        Assert.True(presentation.Contains("UiExpandableSection", StringComparison.Ordinal), "Expandable sections should be reusable.");
        Assert.True(presentation.Contains("UiEmptyState", StringComparison.Ordinal), "Empty states should be reusable.");
        Assert.True(presentation.Contains("UiAlertBanner", StringComparison.Ordinal), "Alert banners should be reusable.");
        Assert.True(presentation.Contains("UiNavigationContext", StringComparison.Ordinal), "Navigation context should be modeled.");
        Assert.True(source.Contains("UiPresentation.PersonRowTemplate()", StringComparison.Ordinal), "AlphaDesktop should use the shared row template.");
    }

    public void PeopleRowsAreClickableAcrossCoreWorkspaces()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("CreateSelectablePeopleContent", StringComparison.Ordinal), "Person-driven screens should use selectable content.");
        Assert.True(source.Contains("ItemTemplate = UiPresentation.PersonRowTemplate()", StringComparison.Ordinal), "Selectable rows should use the clickable person row template.");
        Assert.True(source.Contains("list.MouseDoubleClick", StringComparison.Ordinal), "Selectable list rows should open person cards on double-click.");
        Assert.True(source.Contains("_commandCenterPlayerList.MouseDoubleClick", StringComparison.Ordinal), "Hockey Operations command center players should be clickable.");
        Assert.True(source.Contains("_organizationCommandStaffList.MouseDoubleClick", StringComparison.Ordinal), "Organization staff rows should be clickable.");
        Assert.True(source.Contains("prospectList.MouseDoubleClick", StringComparison.Ordinal), "Live draft prospects should be clickable.");
        Assert.True(source.Contains("OpenUniversalPersonCard(row.PersonId)", StringComparison.Ordinal), "Rows should route to the universal person card.");
        Assert.True(source.Contains("OpenUniversalPersonCard(prospectId)", StringComparison.Ordinal), "Draft prospects should route to the universal person card.");
    }

    public void UniversalPersonCardUsesSharedShellAndPreservesContext()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("OpenUniversalPersonCard", StringComparison.Ordinal), "Universal person card should have a shared open method.");
        Assert.True(source.Contains("BuildUniversalPersonCard", StringComparison.Ordinal), "Universal person card content should be built by one shell.");
        Assert.True(source.Contains("BuildUniversalPersonRow", StringComparison.Ordinal), "Universal person card should resolve player, staff, candidate, and owner rows.");
        Assert.True(source.Contains("Universal Person Card", StringComparison.Ordinal), "Card should clearly identify the shared profile surface.");
        Assert.True(source.Contains("View Full Profile", StringComparison.Ordinal), "Card should allow full profile/dossier navigation.");
        Assert.True(source.Contains("Return", StringComparison.Ordinal), "Card should support returning to prior context without full navigation.");
        Assert.True(source.Contains("ShowPopup($\"Person Card", StringComparison.Ordinal), "Card should open as an overlay/popup, preserving workspace context.");
    }

    public void PlayerCardShowsQuickSummaryAndCollapsedDetails()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("OVR / POT", StringComparison.Ordinal), "Player card should show OVR/POT summary.");
        Assert.True(source.Contains("Role", StringComparison.Ordinal), "Player card should show current role.");
        Assert.True(source.Contains("Contract", StringComparison.Ordinal), "Player card should show contract status.");
        Assert.True(source.Contains("Health", StringComparison.Ordinal), "Player card should show health status.");
        Assert.True(source.Contains("UiExpandableSection(\"Ratings\", ratings, expanded: false)", StringComparison.Ordinal), "Detailed ratings should be collapsed by default.");
        Assert.True(source.Contains("UiExpandableSection(\"Scouting Reports\"", StringComparison.Ordinal), "Scouting detail should be progressively disclosed.");
        Assert.True(source.Contains("UiExpandableSection(\"Medical History\"", StringComparison.Ordinal), "Medical detail should be progressively disclosed.");
        Assert.True(source.Contains("UiExpandableSection(\"Career Timeline\"", StringComparison.Ordinal), "Career detail should be progressively disclosed.");
    }

    public void ContextualActionsAndDisabledReasonsRemainVisible()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("Person Card", StringComparison.Ordinal), "Contextual actions should include Person Card.");
        Assert.True(source.Contains("CreateDetailButton(\"Offer Contract\"", StringComparison.Ordinal), "Player-specific contract actions should remain contextual.");
        Assert.True(source.Contains("CreateDetailButton(\"Hire Candidate\"", StringComparison.Ordinal), "Staff candidate action should remain contextual.");
        Assert.True(source.Contains("button.ToolTip = disabledTooltip ?? \"Coming soon\"", StringComparison.Ordinal), "Disabled actions should explain why.");
        Assert.True(source.Contains("You Give", StringComparison.Ordinal), "Trade proposal buckets should remain separated.");
        Assert.True(source.Contains("You Receive", StringComparison.Ordinal), "Trade proposal buckets should remain separated.");
    }

    public void HiddenTrueRatingsAreNotExposedByPresentationLayer()
    {
        var source = AlphaDesktopSource();
        var presentation = PresentationSource();

        Assert.False(presentation.Contains("HiddenTrue", StringComparison.Ordinal), "Presentation helpers should not expose hidden true ratings.");
        Assert.False(source.Contains("TrueOverall", StringComparison.Ordinal), "AlphaDesktop should not render true overall ratings.");
        Assert.False(source.Contains("TruePotential", StringComparison.Ordinal), "AlphaDesktop should not render true potential ratings.");
        Assert.True(source.Contains("ScoutingConfidenceText", StringComparison.Ordinal), "Visible ratings should preserve scouting confidence.");
    }

    public void RosterRowsUseCompactCardPreviewText()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("State.PositionShortText(player.Position)", StringComparison.Ordinal), "Roster row should show compact position.");
        Assert.True(source.Contains("State.RatingText(player.PersonId)", StringComparison.Ordinal), "Roster row should show visible rating text.");
        Assert.True(source.Contains("State.CurrentLineupRole(player.PersonId)", StringComparison.Ordinal), "Roster row should show current role.");
        Assert.True(source.Contains("State.CurrentLinePair(player.PersonId)", StringComparison.Ordinal), "Roster row should show line/pair.");
        Assert.True(source.Contains("State.ContractRightsStatus(player.PersonId)", StringComparison.Ordinal), "Roster row should show contract status.");
        Assert.True(source.Contains("State.InjuryStatus(player.PersonId)", StringComparison.Ordinal), "Roster row should show health/status.");
    }

    private static string AlphaDesktopSource() =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

    private static string PresentationSource() =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "UiPresentation.cs"));

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
