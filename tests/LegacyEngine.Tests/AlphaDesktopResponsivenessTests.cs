internal sealed class AlphaDesktopResponsivenessTests
{
    public void StartupUsesVisibleWorkspaceRefresh()
    {
        var source = DesktopSource();

        Assert.True(source.Contains("RefreshInitialOfficeView();", StringComparison.Ordinal), "New Career should avoid eagerly rendering every workspace.");
        Assert.True(source.Contains("Populate the visible dashboard now", StringComparison.Ordinal), "Startup performance behavior should be documented in the desktop source.");
    }

    public void ScoutingAssignmentsSkipUnrelatedAssetRecalculation()
    {
        var source = DesktopSource();

        Assert.True(source.Contains("SetScenarioSnapshot(result.ScenarioSnapshot, rebuildAssetEvaluations: false);", StringComparison.Ordinal), "Scouting assignments should not rebuild roster allocation and trade values.");
        Assert.True(source.Contains("refreshBadges: false", StringComparison.Ordinal), "Scouting actions should avoid unnecessary global badge recalculation.");
    }

    public void ContractsAndRightsUseSelectablePlayerRows()
    {
        var source = DesktopSource();

        Assert.True(source.Contains("CreateSelectablePeopleContent(\"Contract Management\")", StringComparison.Ordinal), "Contract Management should use selectable rows.");
        Assert.True(source.Contains("BuildContractManagementDetail(row)", StringComparison.Ordinal), "Contract Management selection should render a detail panel.");
        Assert.True(source.Contains("View Player / Profile", StringComparison.Ordinal), "Contract details should link to the player or staff profile.");
    }

    private static string DesktopSource() =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "engine", "LegacyEngine", "LegacyEngine.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find Hockey GM Legacy repository root.");
    }
}
