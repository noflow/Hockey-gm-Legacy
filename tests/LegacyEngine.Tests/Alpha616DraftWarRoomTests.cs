using LegacyEngine.Draft;
using LegacyEngine.Integration;
using LegacyEngine.Rosters;
using LegacyEngine.Scouting;

internal sealed class Alpha616DraftWarRoomTests
{
    public void WarRoomCreatedAllYear()
    {
        var scenario = Scenario();

        Assert.True(scenario.DraftWarRoom.BoardEntries.Count > 0, "Draft War Room should be created from the active draft board.");
        Assert.True(scenario.DraftWarRoom.OriginalBoardSnapshot.Count > 0, "Draft War Room should store original board snapshot for history.");
    }

    public void CustomRankingsCanMoveProspect()
    {
        var scenario = Scenario();
        var service = new DraftWarRoomService();
        var target = scenario.DraftWarRoom.BoardEntries.OrderByDescending(entry => entry.PersonalRank).First();

        var updated = service.MoveToRank(scenario, target.ProspectPersonId, 1);

        Assert.Equal(target.ProspectPersonId, updated.DraftWarRoom.BoardEntries.OrderBy(entry => entry.PersonalRank).First().ProspectPersonId);
    }

    public void WatchListTagsWork()
    {
        var scenario = Scenario();
        var service = new DraftWarRoomService();
        var target = scenario.DraftWarRoom.BoardEntries.First();

        var updated = service.SetWatchTag(scenario, target.ProspectPersonId, DraftWatchTag.Sleeper, true);

        Assert.True(updated.DraftWarRoom.BoardEntries.First(entry => entry.ProspectPersonId == target.ProspectPersonId).Tags.Contains(DraftWatchTag.Sleeper), "Prospect should carry watch-list tag.");
    }

    public void NeedsAnalysisGenerated()
    {
        var scenario = Scenario();

        Assert.True(scenario.DraftWarRoom.Needs.Count > 0, "Draft War Room should show team needs analysis.");
    }

    public void ProspectCompareWorks()
    {
        var scenario = Scenario();
        var ids = scenario.DraftWarRoom.BoardEntries.Take(3).Select(entry => entry.ProspectPersonId).ToArray();
        var comparison = new DraftWarRoomService().CompareProspects(scenario, ids);

        Assert.Equal(3, comparison.Prospects.Count);
        Assert.True(comparison.Summary.Contains("Hidden ratings are not shown", StringComparison.Ordinal), "Compare output should protect hidden ratings.");
    }

    public void ScoutConsensusGenerated()
    {
        var scenario = Scenario();
        var target = scenario.DraftWarRoom.BoardEntries.First();
        var consensus = new DraftWarRoomService().BuildConsensus(scenario, target.ProspectPersonId);

        Assert.True(consensus.Opinions.Count >= 6, "Consensus should include head scout, regional, development, medical, character, and analytics opinions.");
        Assert.True(consensus.AgreementScore is >= 0 and <= 100, "Consensus score should be bounded.");
    }

    public void BestPlayerAvailableOpinionsGenerated()
    {
        var scenario = Scenario();

        Assert.True(scenario.DraftWarRoom.BestPlayerAvailableOpinions.Count >= 4, "War Room should show multiple department best-player opinions.");
    }

    public void AiDraftingUsesNeedsAndStrategy()
    {
        var scenario = Scenario();
        var goalie = Entry(RosterPosition.Goalie, "test-goalie", 8);
        var forward = Entry(RosterPosition.Center, "test-forward", 8);
        var goalieOrg = scenario.OrganizationAiProfiles.First(profile => profile.Personality == OrganizationAiPersonality.GoalieFocused);
        var service = new DraftWarRoomService();

        var goalieScore = service.ScoreAiDraftFit(scenario, goalieOrg.OrganizationId, goalie, 0);
        var forwardScore = service.ScoreAiDraftFit(scenario, goalieOrg.OrganizationId, forward, 0);

        Assert.True(goalieScore > forwardScore, "Goalie-focused AI should value a goalie fit more than an equivalent forward.");
    }

    public void DraftReviewGeneratedAfterCompletion()
    {
        var result = NewGmScenarioBootstrapper.CreateScenario();
        var draftDateScenario = MoveToDraftDate(result);
        var completed = new AlphaDraftExperienceService().SimulateToCompletion(result.Registry, draftDateScenario).ScenarioSnapshot;

        Assert.True(completed.DraftWarRoom.PostDraftReview is not null, "Completed draft should create a post-draft War Room review.");
    }

    public void OriginalBoardHistoryStored()
    {
        var scenario = Scenario();
        var service = new DraftWarRoomService();
        var moved = service.MoveToRank(scenario, scenario.DraftWarRoom.BoardEntries.Last().ProspectPersonId, 1);

        Assert.Equal(scenario.DraftWarRoom.OriginalBoardSnapshot.First().ProspectPersonId, moved.DraftWarRoom.OriginalBoardSnapshot.First().ProspectPersonId);
    }

    public void WhereAreTheyNowStillGenerated()
    {
        var result = NewGmScenarioBootstrapper.CreateScenario();
        var draftDateScenario = MoveToDraftDate(result);
        var completed = new AlphaDraftExperienceService().SimulateToCompletion(result.Registry, draftDateScenario).ScenarioSnapshot;

        Assert.True(completed.DraftPickHistory.Count > 0, "Draft completion should still feed draft history and Where Are They Now.");
    }

    public void AlphaDesktopExposesWarRoomUi()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("new WorkspaceScreen(\"Draft War Room\"", StringComparison.Ordinal), "AlphaDesktop should expose Draft War Room in Hockey Operations.");
        Assert.True(source.Contains("BuildDraftWarRoom", StringComparison.Ordinal), "AlphaDesktop should render a Draft War Room screen.");
        Assert.True(source.Contains("Scout Consensus", StringComparison.Ordinal), "AlphaDesktop should expose consensus actions.");
        Assert.True(source.Contains("Compare", StringComparison.Ordinal), "AlphaDesktop should expose compare actions.");
    }

    public void NoForbiddenSystemsAdded()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "engine", "LegacyEngine", "Integration", "DraftWarRoomService.cs"));

        Assert.False(source.Contains("MockDraft", StringComparison.OrdinalIgnoreCase), "Draft V4 should not build mock drafts.");
        Assert.False(source.Contains("DraftLottery", StringComparison.OrdinalIgnoreCase), "Draft V4 should not build draft lottery.");
        Assert.False(source.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Draft V4 should not reference Godot.");
    }

    private static NewGmScenarioSnapshot Scenario() =>
        new DraftWarRoomService().EnsureWarRoom(NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot);

    private static NewGmScenarioSnapshot MoveToDraftDate(NewGmScenarioResult result)
    {
        var days = result.ScenarioSnapshot.DraftDate.DayNumber - result.ScenarioSnapshot.CurrentDate.DayNumber;
        if (days > 0)
        {
            result.Registry.WorldEngine.AdvanceDays(days);
        }

        return result.ScenarioSnapshot with
        {
            AlphaSnapshot = result.ScenarioSnapshot.AlphaSnapshot with
            {
                WorldState = result.Registry.WorldEngine.State
            }
        };
    }

    private static DraftBoardEntry Entry(RosterPosition position, string id, int rank) =>
        new(
            id,
            rank,
            null,
            ScoutingConfidenceLevel.High,
            "Visible projection only.",
            Bio: new DraftProspectBio(
                position,
                position == RosterPosition.Goalie ? "Catches L" : "Shoots L",
                73,
                185,
                2008,
                "Testville",
                "BC",
                "Canada",
                "Test U18",
                "CSSHL U18",
                "Competitive and coachable.",
                position == RosterPosition.Goalie ? "starter upside" : "top-six upside"),
            RiskSummary: "No major risk.",
            ClassContextNote: "Test prospect.");

    private static string AlphaDesktopSource() =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HockeyGmLegacy.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
