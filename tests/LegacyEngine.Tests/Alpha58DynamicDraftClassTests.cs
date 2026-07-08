using System.Text.RegularExpressions;
using LegacyEngine.Integration;
using LegacyEngine.Names;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;

internal sealed class Alpha58DynamicDraftClassTests
{
    public void DraftClassProfileIsGenerated()
    {
        var profile = new DraftClassGenerator().GenerateProfile(RulebookPresets.CreateJuniorMajor(), 2026, "junior-league-alpha", 60);

        Assert.Equal(2026, profile.Year);
        Assert.Equal(60, profile.TotalProspects);
        Assert.True(profile.Strengths.Count > 0, "Draft class should have strengths.");
        Assert.True(profile.Weaknesses.Count > 0, "Draft class should have weaknesses.");
        Assert.True(!string.IsNullOrWhiteSpace(profile.TopStoryline.Headline), "Draft class should have a storyline.");
    }

    public void ThemeAffectsClassShape()
    {
        var generator = new DraftClassGenerator();
        var defense = generator.GenerateProfile(RulebookPresets.CreateJuniorMajor(), 2027, "junior-league-alpha", 60, DraftClassTheme.DeepDefenseClass);
        var goalies = generator.GenerateProfile(RulebookPresets.CreateJuniorMajor(), 2027, "junior-league-alpha", 60, DraftClassTheme.StrongGoalieClass);
        var boomBust = generator.GenerateProfile(RulebookPresets.CreateJuniorMajor(), 2027, "junior-league-alpha", 60, DraftClassTheme.BoomBustClass);

        Assert.True(defense.PositionalDepth[RosterPosition.Defense] > defense.PositionalDepth[RosterPosition.Center], "Deep defense class should carry more defense depth.");
        Assert.True(goalies.PositionalDepth[RosterPosition.Goalie] >= 8, "Strong goalie class should create more goalie depth.");
        Assert.True(boomBust.ScoutingUncertainty.Contains("High", StringComparison.OrdinalIgnoreCase), "Boom/bust class should increase uncertainty.");
    }

    public void NhlStyleClassUsesNhlDraftRulesAndBroaderSources()
    {
        var settings = new NewGmScenarioSettings(
            WorldName: "NHL Test World",
            LeagueId: "nhl-alpha",
            SeasonId: "season-nhl-2026",
            SeasonYear: 2026,
            OrganizationId: "org-nhl-test",
            RosterId: "roster-nhl-test",
            DraftBoardId: "draft-board-nhl-test",
            PlayerGmPersonId: "person-gm-nhl-test")
        {
            LeagueExperience = LeagueExperience.Nhl,
            TeamName = "Harbor Admirals",
            TeamCity = "Seattle",
            TeamRegion = "WA",
            TeamCountry = "USA"
        };
        var created = NewGmScenarioBootstrapper.CreateScenario(settings, RulebookPresets.CreateNhlStyle());
        var entries = created.ScenarioSnapshot.AlphaSnapshot.DraftBoard.Entries;

        Assert.Equal(7, created.ScenarioSnapshot.LeagueProfile.Rulebook.DraftRules!.Rounds);
        Assert.True(entries.All(entry => created.ScenarioSnapshot.AlphaSnapshot.People.First(person => person.PersonId == entry.ProspectPersonId).CalculateAge(created.ScenarioSnapshot.CurrentDate) is 17 or 18), "NHL-style draft class should mostly use 17-18 year olds.");
        Assert.True(entries.Any(entry => entry.Bio?.Country is not "Canada"), "NHL-style draft class should include broader geography.");
    }

    public void JuniorStyleClassUsesYoungerRegionalProspects()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var entries = created.ScenarioSnapshot.AlphaSnapshot.DraftBoard.Entries;

        Assert.True(entries.All(entry => created.ScenarioSnapshot.AlphaSnapshot.People.First(person => person.PersonId == entry.ProspectPersonId).CalculateAge(created.ScenarioSnapshot.CurrentDate) is 16 or 17), "Junior-style class should use younger prospects.");
        Assert.True(entries.Any(entry => entry.Bio?.League is "CSSHL U18" or "SMAAAHL" or "AEHL U18"), "Junior-style class should include regional youth/junior sources.");
    }

    public void AhlStyleDraftDisabled()
    {
        var profile = new DraftClassGenerator().GenerateProfile(RulebookPresets.CreateAhlStyle(), 2026, "ahl-alpha", 60);
        var created = NewGmScenarioBootstrapper.CreateScenario(
            new NewGmScenarioSettings(LeagueId: "ahl-alpha", OrganizationId: "org-ahl-test", RosterId: "roster-ahl-test", DraftBoardId: "draft-board-ahl-test")
            {
                LeagueExperience = LeagueExperience.Ahl,
                TeamName = "Valley Comets",
                ParentOrganizationId = "org-parent-nhl"
            },
            RulebookPresets.CreateAhlStyle());

        Assert.Equal(0, profile.TotalProspects);
        Assert.Equal(0, created.ScenarioSnapshot.AlphaSnapshot.DraftBoard.Entries.Count);
    }

    public void ScenarioDraftProspectsHaveClassContextAndNoDuplicateIds()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var ids = scenario.AlphaSnapshot.DraftBoard.Entries.Select(entry => entry.ProspectPersonId).ToArray();

        Assert.True(scenario.CurrentDraftClassProfile is not null, "Scenario should carry current draft class profile.");
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.True(scenario.AlphaSnapshot.DraftBoard.Entries.All(entry => !string.IsNullOrWhiteSpace(entry.ClassContextNote)), "Every board entry should have class context.");
        Assert.True(scenario.AlphaSnapshot.DraftBoard.Entries.All(entry => !string.IsNullOrWhiteSpace(entry.RiskSummary)), "Every board entry should have risk context.");
    }

    public void OpeningScenarioDraftClassIsMostlyScouted()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var boardCount = scenario.AlphaSnapshot.DraftBoard.Entries.Count;
        var reportCount = scenario.CompletedScoutingReports.Count(report => scenario.AlphaSnapshot.DraftBoard.Entries.Any(entry => entry.ProspectPersonId == report.PlayerId));

        Assert.True(reportCount >= boardCount * 2 / 3, "The inherited staff should have already scouted most of the opening draft class.");
    }

    public void PlayerDossierIncludesClassContextWithoutHiddenRatings()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var entry = scenario.AlphaSnapshot.DraftBoard.Entries.First();
        var dossier = new PlayerDossierService().CreateDossier(scenario, entry.ProspectPersonId);
        var text = string.Join("\n", dossier.Sections.SelectMany(section => section.Lines));

        Assert.True(text.Contains("Draft class:", StringComparison.Ordinal), "Dossier should show draft class context.");
        Assert.True(text.Contains("Class context:", StringComparison.Ordinal), "Dossier should show player-specific class context.");
        Assert.True(text.Contains("Risk", StringComparison.Ordinal), "Dossier should show public risk context.");
        Assert.False(text.Contains("CurrentAbility", StringComparison.OrdinalIgnoreCase), "Dossier must not expose hidden current ability.");
        Assert.False(text.Contains("Potential =", StringComparison.OrdinalIgnoreCase), "Dossier must not expose hidden potential ratings.");
    }

    public void DraftClassHistoryPreservesProfile()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var entry = scenario.AlphaSnapshot.DraftBoard.Entries.First();
        var selection = new DraftPickSummary(
            1,
            1,
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            entry.ProspectPersonId,
            scenario.AlphaSnapshot.People.First(person => person.PersonId == entry.ProspectPersonId).Identity.DisplayName,
            true);

        var updated = new CareerHistoryService().RecordDraftCompleted(scenario, new[] { selection });
        var history = updated.DraftClassHistory.First(item => item.Year == scenario.Season.Year);

        Assert.True(history.ClassProfile is not null, "Draft class history should preserve the original class profile.");
        Assert.Equal(scenario.CurrentDraftClassProfile!.Theme, history.ClassProfile!.Theme);
    }

    public void AlphaDesktopExposesDraftClassSummaryAndLiveDraftContext()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("Draft Class Summary", StringComparison.Ordinal), "Draft Board should expose a class summary card.");
        Assert.True(source.Contains("Filters placeholder: position, region, current league, handedness, projection, confidence, risk, class fit", StringComparison.Ordinal), "Draft board should show future filter structure.");
        Assert.True(source.Contains("Best available by position", StringComparison.Ordinal), "Live draft should show best available by position.");
        Assert.True(source.Contains("Class context:", StringComparison.Ordinal), "Live draft prospect card should show class context.");
        Assert.True(source.Contains("Risk summary:", StringComparison.Ordinal), "Live draft prospect card should show risk.");
    }

    public void GeneratedNamesStayCleanAcrossLargePool()
    {
        var generator = new NameGenerator(NameGenerationSettings.CreateDefault(5800));
        var registry = new NameUniquenessRegistry();
        var names = Enumerable.Range(0, 500)
            .Select(index => generator.Generate(registry, "alpha58-name-test", NameOrigin.CanadaEnglish, NameOrigin.CanadaFrench, NameOrigin.Usa, NameOrigin.Finland, NameOrigin.Sweden, NameOrigin.Czechia, NameOrigin.Slovakia, NameOrigin.Germany, NameOrigin.Switzerland, NameOrigin.Latvia, NameOrigin.GenericEuropean).DisplayName)
            .ToArray();

        var duplicateCount = names.Length - names.Distinct(StringComparer.Ordinal).Count();
        Assert.True(duplicateCount <= 20, $"Duplicate rate should stay low for large generated pools. Duplicates: {duplicateCount}.");
        Assert.False(names.Any(name => Regex.IsMatch(name, @"\d")), "Generated display names must not contain numeric suffixes.");
        Assert.False(names.Any(name => name.Equals("Connor McDavid", StringComparison.OrdinalIgnoreCase) || name.Equals("Sidney Crosby", StringComparison.OrdinalIgnoreCase)), "Generated classes should not use real player database names.");
    }

    private static string AlphaDesktopSource() =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var projectPath = Path.Combine(directory.FullName, "engine", "LegacyEngine", "LegacyEngine.csproj");
            if (File.Exists(projectPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find Hockey GM Legacy repository root.");
    }
}
