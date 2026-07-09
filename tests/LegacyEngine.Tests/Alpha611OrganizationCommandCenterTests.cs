internal sealed class Alpha611OrganizationCommandCenterTests
{
    public void OrganizationCommandCenterIsPrimaryOrganizationScreen()
    {
        var source = DesktopSource();

        Assert.True(source.Contains("new WorkspaceScreen(\"Command Center\", CreateOrganizationCommandCenter())", StringComparison.Ordinal), "Organization workspace should start with the Command Center.");
        Assert.True(source.Contains("RefreshOrganizationCommandCenter();", StringComparison.Ordinal), "Organization Command Center should refresh with the desktop.");
    }

    public void OrganizationCommandCenterExposesDepartments()
    {
        var source = DesktopSource();

        foreach (var label in new[] { "Owner", "Front Office", "Coaching", "Scouting", "Development", "Medical", "Equipment", "Finance", "Facilities" })
        {
            Assert.True(source.Contains($"\"{label}\"", StringComparison.Ordinal), $"Organization Command Center should expose {label}.");
        }
    }

    public void OrganizationCommandCenterShowsDepartmentHealth()
    {
        var source = DesktopSource();

        foreach (var label in new[] { "Department Health", "Grade:", "Strengths:", "Weaknesses:", "Budget:", "Staff Count:", "Vacancies:", "Recommendations:" })
        {
            Assert.True(source.Contains(label, StringComparison.Ordinal), $"Department health should include {label}.");
        }
    }

    public void OrganizationCommandCenterShowsChartBudgetReportsAndActions()
    {
        var source = DesktopSource();

        foreach (var label in new[] { "Organization Chart", "Financial Overview", "Executive Report", "Action Center", "Operating Budget", "Player Payroll", "Staff Payroll", "Future Commitments" })
        {
            Assert.True(source.Contains(label, StringComparison.Ordinal), $"Organization Command Center should include {label}.");
        }
    }

    public void SelectedStaffCardExposesManagementActions()
    {
        var source = DesktopSource();

        foreach (var label in new[] { "Salary", "Years remaining", "Extension recommendation", "Replacement cost", "Relationship", "Performance", "Promote", "Demote", "Move Department", "Set Focus", "Release" })
        {
            Assert.True(source.Contains(label, StringComparison.Ordinal), $"Selected staff card should include {label}.");
        }
    }

    public void OwnerViewAndVacancyWorkflowAreExposed()
    {
        var source = DesktopSource();

        Assert.True(source.Contains("BuildOrganizationOwnerCard", StringComparison.Ordinal), "Owner command card should exist.");
        Assert.True(source.Contains("BuildOrganizationVacancyCard", StringComparison.Ordinal), "Vacancy command card should exist.");
        Assert.True(source.Contains("Owner Workspace", StringComparison.Ordinal), "Owner card should link to owner workspace.");
        Assert.True(source.Contains("Hire Staff", StringComparison.Ordinal), "Vacancy card should route to staff hiring.");
    }

    public void NoForbiddenSystemsAdded()
    {
        var source = DesktopSource();

        Assert.False(source.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Organization Command Center should not add Godot.");
        Assert.False(source.Contains("MediaEngine", StringComparison.OrdinalIgnoreCase), "Organization Command Center should not add media systems.");
        Assert.False(source.Contains("FacilitySimulation", StringComparison.OrdinalIgnoreCase), "Organization Command Center should not add facilities simulation.");
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
