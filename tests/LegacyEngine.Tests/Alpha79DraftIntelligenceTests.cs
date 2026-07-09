using LegacyEngine.Draft;
using LegacyEngine.Integration;
using LegacyEngine.Rosters;
using LegacyEngine.World;

internal sealed class Alpha79DraftIntelligenceTests
{
    public void WarRoomCreated()
    {
        var scenario = Scenario();

        Assert.True(scenario.DraftWarRoom.BoardEntries.Count > 0, "War Room should include draft prospects.");
        Assert.True(scenario.DraftWarRoom.BoardViews.Any(view => view.ViewType == DraftWarRoomViewType.MyBoard), "War Room should include My Board view.");
    }

    public void MyBoardSupportsCustomRanking()
    {
        var scenario = Scenario();
        var second = scenario.DraftWarRoom.BoardEntries.OrderBy(entry => entry.PersonalRank).Skip(1).First();
        var moved = new DraftWarRoomService().MoveToRank(scenario, second.ProspectPersonId, 1);

        Assert.Equal(second.ProspectPersonId, moved.DraftWarRoom.BoardEntries.OrderBy(entry => entry.PersonalRank).First().ProspectPersonId);
    }

    public void ScoutBoardGenerated()
    {
        var scenario = Scenario();
        var board = scenario.DraftWarRoom.BoardViews.First(view => view.ViewType == DraftWarRoomViewType.ScoutBoard);

        Assert.True(board.Rows.Count > 0, "Scout Board should have prospect rows.");
        Assert.True(board.Summary.Contains("Scout", StringComparison.OrdinalIgnoreCase), "Scout Board should explain its source.");
    }

    public void ConsensusBoardGenerated()
    {
        var scenario = Scenario();
        var board = scenario.DraftWarRoom.BoardViews.First(view => view.ViewType == DraftWarRoomViewType.ConsensusBoard);

        Assert.True(board.Rows.Count > 0, "Consensus Board should have prospect rows.");
        Assert.True(board.Summary.Contains("team needs", StringComparison.OrdinalIgnoreCase), "Consensus Board should include needs context.");
    }

    public void ProspectRatingsShowConfidenceColors()
    {
        var scenario = Scenario();
        var card = new DraftIntelligenceService().BuildProspectCard(scenario, ProspectId(scenario));

        Assert.True(Enum.IsDefined(card.RatingConfidenceColor), "Draft prospect card should show rating confidence color.");
        Assert.True(card.RatingDisplay.Contains("OVR", StringComparison.Ordinal) && card.RatingDisplay.Contains("POT", StringComparison.Ordinal), "Draft prospect card should show visible OVR/POT.");
    }

    public void UnscoutedDraftAttributesShowUnknown()
    {
        var raw = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot with
        {
            CompletedScoutingReports = Array.Empty<LegacyEngine.Scouting.ScoutingReport>(),
            ScoutingKnowledgeProfiles = Array.Empty<ScoutingKnowledgeProfile>()
        };
        var scenario = new DraftWarRoomService().EnsureWarRoom(new HockeyIntelligenceRatingService().EnsureRatings(raw));
        var card = new DraftIntelligenceService().BuildProspectCard(scenario, ProspectId(scenario));

        Assert.True(card.Attributes.Any(attribute => attribute.Estimate.Display == "???"), "Unscouted attributes should stay hidden as ???.");
    }

    public void CompareProspectsWorks()
    {
        var scenario = Scenario();
        var ids = scenario.DraftWarRoom.BoardEntries.OrderBy(entry => entry.PersonalRank).Take(4).Select(entry => entry.ProspectPersonId).ToArray();
        var comparison = new DraftIntelligenceService().CompareProspects(scenario, ids);

        Assert.Equal(4, comparison.Prospects.Count);
        Assert.True(comparison.Summary.Contains("Hidden true ratings are not shown", StringComparison.Ordinal), "Comparison should explain uncertainty.");
    }

    public void TeamNeedsGenerated()
    {
        var scenario = Scenario();

        Assert.True(scenario.DraftWarRoom.Needs.Count > 0, "Draft War Room should include team needs.");
        Assert.True(scenario.DraftWarRoom.BoardViews.Any(view => view.ViewType == DraftWarRoomViewType.TeamNeeds), "Team needs should have a War Room view.");
    }

    public void HiddenGemAlertGenerated()
    {
        var scenario = Scenario();

        Assert.True(scenario.DraftWarRoom.IntelligenceAlerts.Any(alert => alert.AlertType == DraftIntelligenceAlertType.HiddenGemCandidate), "War Room should surface hidden gem candidates.");
    }

    public void BustRiskAlertGenerated()
    {
        var scenario = Scenario();

        Assert.True(scenario.DraftWarRoom.IntelligenceAlerts.Any(alert => alert.AlertType == DraftIntelligenceAlertType.BustRisk), "War Room should surface bust-risk alerts.");
    }

    public void AiDraftUsesNeedsAndStrategy()
    {
        var scenario = Scenario();
        var defense = scenario.AlphaSnapshot.DraftBoard.Entries.First(entry => entry.Bio?.Position == RosterPosition.Defense);
        var forward = scenario.AlphaSnapshot.DraftBoard.Entries.First(entry => entry.Bio?.Position is RosterPosition.Center or RosterPosition.LeftWing or RosterPosition.RightWing);
        var organizationId = "test-defense-first";
        var ai = scenario.OrganizationAiProfiles.First() with
        {
            OrganizationId = organizationId,
            TeamName = "Defense First Club",
            Personality = OrganizationAiPersonality.DefenseFirst,
            CurrentNeeds = new[]
            {
                new TeamNeedProfile(TeamNeedType.TopPairDefense, TradePriority.Urgent, "Need a defense prospect.", "Next pick", AiAssetType.Defenseman)
            },
            Summary = "Test defense-first draft profile."
        };
        var withAi = scenario with { OrganizationAiProfiles = scenario.OrganizationAiProfiles.Where(profile => profile.OrganizationId != organizationId).Append(ai).ToArray() };
        var service = new DraftWarRoomService();

        Assert.True(service.ScoreAiDraftFit(withAi, organizationId, defense, 0) > service.ScoreAiDraftFit(withAi, organizationId, forward, 0), "AI draft fit should reward team needs and strategy.");
    }

    public void PostDraftReviewStoresOriginalEstimates()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var scenario = OnDraftDay(Scenario(created.ScenarioSnapshot));
        var draft = new AlphaDraftExperienceService();
        var started = draft.StartDraftDay(created.Registry, scenario);
        var state = draft.RunAiPicksUntilPlayerTurn(created.Registry, started.ScenarioSnapshot);
        var selected = state.ScenarioSnapshot.AlphaSnapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank).First().ProspectPersonId;
        var result = draft.MakePlayerSelection(created.Registry, state.ScenarioSnapshot, selected);
        var completed = draft.SimulateToCompletion(created.Registry, result.ScenarioSnapshot);
        var history = completed.ScenarioSnapshot.DraftPickHistory.First();

        Assert.True(history.OverallEstimateAtDraft.Contains("OVR", StringComparison.Ordinal), "Draft history should store OVR estimate at draft.");
        Assert.True(history.PotentialEstimateAtDraft.Contains("POT", StringComparison.Ordinal), "Draft history should store POT estimate at draft.");
        Assert.True(history.AttributeConfidenceAtDraft.Length > 0, "Draft history should store attribute confidence snapshot.");
    }

    public void DraftHistoryPreservesBoardRanks()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario();
        var scenario = OnDraftDay(Scenario(created.ScenarioSnapshot));
        var draft = new AlphaDraftExperienceService();
        var started = draft.StartDraftDay(created.Registry, scenario);
        var state = draft.RunAiPicksUntilPlayerTurn(created.Registry, started.ScenarioSnapshot);
        var selected = state.ScenarioSnapshot.AlphaSnapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank).First().ProspectPersonId;
        var result = draft.MakePlayerSelection(created.Registry, state.ScenarioSnapshot, selected);
        var completed = draft.SimulateToCompletion(created.Registry, result.ScenarioSnapshot);
        var history = completed.ScenarioSnapshot.DraftPickHistory.First();

        Assert.True(history.OriginalBoardRank > 0, "Draft history should preserve original GM board rank.");
        Assert.True(history.ScoutBoardRank > 0, "Draft history should preserve scout board rank.");
        Assert.True(history.ConsensusBoardRank > 0, "Draft history should preserve consensus board rank.");
    }

    public void NoTrueRatingsExposed()
    {
        var scenario = Scenario();
        var card = new DraftIntelligenceService().BuildProspectCard(scenario, ProspectId(scenario));
        var desktop = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));
        var text = $"{card.RatingDisplay} {card.ScoutRecommendation} {string.Join(" ", card.Attributes.Select(attribute => attribute.DisplayText))}";

        Assert.False(text.Contains("PlayerTrueRatings", StringComparison.Ordinal), "Draft intelligence should not expose hidden rating model names.");
        Assert.False(desktop.Contains("ScenarioSnapshot.TrueRatings", StringComparison.Ordinal), "AlphaDesktop should not render hidden true ratings.");
    }

    public void AlphaDesktopExposesWarRoom()
    {
        var desktop = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(desktop.Contains("Draft War Room", StringComparison.Ordinal), "Desktop should expose the Draft War Room.");
        Assert.True(desktop.Contains("Scout Board", StringComparison.Ordinal), "Desktop should expose Scout Board.");
        Assert.True(desktop.Contains("Consensus Board", StringComparison.Ordinal), "Desktop should expose Consensus Board.");
        Assert.True(desktop.Contains("Hidden Gems / Avoid List", StringComparison.Ordinal), "Desktop should expose hidden gems and avoid list.");
    }

    private static NewGmScenarioSnapshot Scenario() =>
        Scenario(NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot);

    private static NewGmScenarioSnapshot Scenario(NewGmScenarioSnapshot scenario)
    {
        var prepared = new HockeyIntelligenceRatingService().EnsureRatings(scenario);
        prepared = new ScoutingIntelligenceService().EnsureKnowledgeProfiles(prepared);
        prepared = new DevelopmentCurveService().EnsureCurves(prepared);
        prepared = new PlayerRatingService().EnsureRatings(prepared);
        return new DraftWarRoomService().EnsureWarRoom(prepared);
    }

    private static string ProspectId(NewGmScenarioSnapshot scenario) =>
        scenario.AlphaSnapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank).First().ProspectPersonId;

    private static NewGmScenarioSnapshot OnDraftDay(NewGmScenarioSnapshot scenario) =>
        scenario with
        {
            AlphaSnapshot = scenario.AlphaSnapshot with
            {
                WorldState = scenario.AlphaSnapshot.WorldState with
                {
                    Clock = new WorldClock(new WorldDate(scenario.DraftDate))
                }
            }
        };

    private static string FindRepositoryRoot()
    {
        var current = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(current, "HockeyGmLegacy.slnx")))
        {
            current = Directory.GetParent(current)?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
        }

        return current;
    }
}
