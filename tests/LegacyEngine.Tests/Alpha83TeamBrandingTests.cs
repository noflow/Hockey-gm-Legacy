using LegacyEngine.Integration;

internal sealed class Alpha83TeamBrandingTests
{
    public void EveryOrganizationReceivesBrandingProfile()
    {
        var profile = new MultiLeagueCareerService().GetProfile(LeagueExperience.Nhl);
        var registry = new UiBrandingService().BuildRegistry(profile);

        Assert.Equal(profile.Teams.Count, registry.TeamProfiles.Count);
        foreach (var team in profile.Teams)
        {
            Assert.True(registry.TeamProfiles.ContainsKey(team.OrganizationId), $"{team.TeamName} should have branding.");
        }
    }

    public void BrandingFallbackIsDeterministic()
    {
        var profile = new MultiLeagueCareerService().GetProfile(LeagueExperience.Junior);
        var first = new UiBrandingService().BuildRegistry(profile);
        var second = new UiBrandingService().BuildRegistry(profile);

        var teamId = profile.Teams[0].OrganizationId;
        Assert.Equal(first.TeamProfiles[teamId], second.TeamProfiles[teamId]);
    }

    public void TeamAbbreviationAndMonogramAreGeneratedWithoutNumbers()
    {
        var profile = new MultiLeagueCareerService().GetProfile(LeagueExperience.Junior);
        var registry = new UiBrandingService().BuildRegistry(profile);

        foreach (var brand in registry.TeamProfiles.Values)
        {
            Assert.True(!string.IsNullOrWhiteSpace(brand.TeamAbbreviation), "Team abbreviation should be generated.");
            Assert.True(!string.IsNullOrWhiteSpace(brand.Monogram.Letters), "Team monogram should be generated.");
            Assert.False(brand.TeamAbbreviation.Any(char.IsDigit), "Team abbreviation should not contain numeric suffixes.");
            Assert.False(brand.Monogram.Letters.Any(char.IsDigit), "Team monogram should not contain numeric suffixes.");
        }
    }

    public void ReadableForegroundAndLeagueIdentityAreSelected()
    {
        var profile = new MultiLeagueCareerService().GetProfile(LeagueExperience.Ahl);
        var registry = new UiBrandingService().BuildRegistry(profile);

        Assert.True(registry.LeagueProfiles.ContainsKey(profile.Identity.LeagueId), "League identity should be applied.");
        foreach (var brand in registry.TeamProfiles.Values)
        {
            Assert.True(brand.Palette.ReadableForeground.StartsWith("#", StringComparison.Ordinal), "Readable foreground should be a hex color.");
            Assert.Equal(7, brand.Palette.ReadableForeground.Length);
            Assert.True(!string.IsNullOrWhiteSpace(brand.VisualStyleDescriptor), "Visual style should be set.");
        }
    }

    public void TeamColorsPersistThroughSaveLoad()
    {
        var career = new MultiLeagueCareerService();
        var selection = career.SelectLeagueAndTeam(LeagueExperience.Junior, "org-prairie-falcons");
        var scenario = new UiBrandingService().EnsureBranding(career.CreateScenario(selection).ScenarioSnapshot);
        var budget = new BudgetOverviewService().Build(scenario, scenario.LeagueProfile.Rulebook);
        var service = new SaveGameService();
        var path = Path.Combine(Path.GetTempPath(), $"hgm-alpha83-branding-{Guid.NewGuid():N}.json");

        try
        {
            var save = service.SaveCareer(
                scenario,
                Array.Empty<InboxMessage>(),
                Array.Empty<LeagueTransaction>(),
                new Dictionary<string, ActionCenterStatus>(StringComparer.Ordinal),
                budget,
                path);
            Assert.True(save.Success, save.Message);

            var loaded = service.LoadFromFile(path);
            Assert.True(loaded.Success, loaded.Message);
            var original = scenario.BrandingRegistry.TeamProfiles[scenario.Organization.OrganizationId];
            var restored = loaded.SaveGame!.ScenarioSnapshot.BrandingRegistry.TeamProfiles[scenario.Organization.OrganizationId];
            Assert.Equal(original.Palette.Primary, restored.Palette.Primary);
            Assert.Equal(original.TeamAbbreviation, restored.TeamAbbreviation);
            Assert.Equal(original.Monogram.Letters, restored.Monogram.Letters);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    public void AlphaDesktopUsesBrandingPresentationHooks()
    {
        var program = AlphaDesktopSource("Program.cs");
        var presentation = AlphaDesktopSource("UiPresentation.cs");

        Assert.True(presentation.Contains("UiTeamCrest", StringComparison.Ordinal), "Team crest helper should exist.");
        Assert.True(presentation.Contains("UiTeamHeader", StringComparison.Ordinal), "Reusable team header should exist.");
        Assert.True(presentation.Contains("UiTeamCard", StringComparison.Ordinal), "Reusable team card should exist.");
        Assert.True(presentation.Contains("UiPersonAvatar", StringComparison.Ordinal), "Person avatar placeholder should exist.");
        Assert.True(presentation.Contains("UiIconLabel", StringComparison.Ordinal), "Icon labels should include text/tooltips.");
        Assert.True(program.Contains("CurrentTeamBranding", StringComparison.Ordinal), "Desktop should consume team branding.");
        Assert.True(program.Contains("NavigationHeaderText", StringComparison.Ordinal), "Navigation should have consistent icon labels.");
        Assert.True(program.Contains("BuildTradeCenterTeamContext", StringComparison.Ordinal), "Trade Center should show team identities.");
        Assert.True(program.Contains("UiTeamHeader", StringComparison.Ordinal), "Dashboard/team header should use branding.");
    }

    public void PresentationKeepsSemanticStatusColorsSeparate()
    {
        var presentation = AlphaDesktopSource("UiPresentation.cs");
        var models = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "engine", "LegacyEngine", "Integration", "BrandingModels.cs"));

        Assert.True(presentation.Contains("BadgeBackground", StringComparison.Ordinal), "Semantic badge colors should remain centralized.");
        Assert.True(presentation.Contains("UiTeamCrest", StringComparison.Ordinal), "Team colors should be applied to identity accents.");
        Assert.True(models.Contains("Healthy", StringComparison.Ordinal), "Health status icon should remain semantic.");
        Assert.True(models.Contains("Injured", StringComparison.Ordinal), "Injury status icon should remain semantic.");
        Assert.True(models.Contains("RestrictedFreeAgent", StringComparison.Ordinal), "RFA status icon should remain semantic.");
        Assert.True(models.Contains("UnrestrictedFreeAgent", StringComparison.Ordinal), "UFA status icon should remain semantic.");
    }

    public void NoCopyrightedLogoAssetsOrHiddenTruthExposed()
    {
        var root = FindRepositoryRoot();
        var binaryLogos = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(path => path.Contains($"{Path.DirectorySeparatorChar}client{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var program = AlphaDesktopSource("Program.cs");
        Assert.Equal(0, binaryLogos.Length);
        Assert.False(program.Contains("TrueOverall", StringComparison.Ordinal), "Presentation should not expose true overall ratings.");
        Assert.False(program.Contains("TruePotential", StringComparison.Ordinal), "Presentation should not expose true potential ratings.");
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
