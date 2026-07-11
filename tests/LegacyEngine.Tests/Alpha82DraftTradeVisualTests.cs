internal sealed class Alpha82DraftTradeVisualTests
{
    public void DraftWarRoomUsesIntegratedFourPartLayout()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("CreateDraftWarRoomWorkspace", StringComparison.Ordinal), "Draft War Room should be a real workspace.");
        Assert.True(source.Contains("RefreshDraftWarRoomWorkspace", StringComparison.Ordinal), "Draft War Room should refresh after actions.");
        Assert.True(source.Contains("BuildDraftWarRoomLeftPanel", StringComparison.Ordinal), "Draft War Room should have a left panel.");
        Assert.True(source.Contains("BuildDraftWarRoomCenterPanel", StringComparison.Ordinal), "Draft War Room should have a center board.");
        Assert.True(source.Contains("BuildDraftWarRoomProspectPanel", StringComparison.Ordinal), "Draft War Room should have a selected prospect card.");
        Assert.True(source.Contains("BuildDraftWarRoomBottomStrip", StringComparison.Ordinal), "Draft War Room should have a bottom live/recent picks strip.");
        Assert.True(source.Contains("\"My Board\"", StringComparison.Ordinal), "Board selector should include My Board.");
        Assert.True(source.Contains("\"Scout Board\"", StringComparison.Ordinal), "Board selector should include Scout Board.");
        Assert.True(source.Contains("\"Consensus Board\"", StringComparison.Ordinal), "Board selector should include Consensus Board.");
        Assert.True(source.Contains("\"Watch List\"", StringComparison.Ordinal), "Board selector should include Watch List.");
    }

    public void DraftBoardRowsAndProspectCardExposeReadableIntelligence()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("BuildDraftWarRoomVisualRows", StringComparison.Ordinal), "Draft board should build compact visual rows.");
        Assert.True(source.Contains("card.RatingDisplay", StringComparison.Ordinal), "Draft rows should show OVR/POT estimates.");
        Assert.True(source.Contains("consensus.Level", StringComparison.Ordinal), "Draft rows should show consensus status.");
        Assert.True(source.Contains("TeamFitScore", StringComparison.Ordinal), "Draft rows/card should show team fit.");
        Assert.True(source.Contains("BuildScoutOpinionCards", StringComparison.Ordinal), "Selected prospect should show scout opinion cards.");
        Assert.True(source.Contains("BuildDraftTeamFitText", StringComparison.Ordinal), "Selected prospect should explain team fit.");
        Assert.True(source.Contains("BuildCommandCenterSection(\"Ratings\"", StringComparison.Ordinal), "Prospect card should keep ratings as a visible section.");
        Assert.True(source.Contains("BuildCommandCenterSection(\"Scouting\"", StringComparison.Ordinal), "Prospect card should keep scouting as a section.");
        Assert.True(source.Contains("BuildCommandCenterSection(\"GM Notes\"", StringComparison.Ordinal), "Prospect card should show GM notes.");
    }

    public void DraftWarRoomSupportsTagsCompareAndLiveDraftActions()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("ToggleDraftTagAndRefresh", StringComparison.Ordinal), "Draft tags should update the War Room immediately.");
        Assert.True(source.Contains("DraftWatchTag.Favorite", StringComparison.Ordinal), "Favorite tag should be available.");
        Assert.True(source.Contains("DraftWatchTag.Priority", StringComparison.Ordinal), "Priority tag should be available.");
        Assert.True(source.Contains("DraftWatchTag.Sleeper", StringComparison.Ordinal), "Sleeper tag should be available.");
        Assert.True(source.Contains("DraftWatchTag.Avoid", StringComparison.Ordinal), "Avoid tag should be available.");
        Assert.True(source.Contains("ShowDraftComparePopup", StringComparison.Ordinal), "War Room should support a prospect compare popup.");
        Assert.True(source.Contains("DraftSelectedProspect", StringComparison.Ordinal), "War Room should use existing draft selection logic.");
        Assert.True(source.Contains("BuildLiveDraftStatusLine", StringComparison.Ordinal), "Live draft status should appear in the War Room.");
        Assert.True(source.Contains("Recent Picks", StringComparison.Ordinal), "Recent picks should be presented.");
        Assert.True(source.Contains("Upcoming Picks", StringComparison.Ordinal), "Upcoming picks should be presented.");
    }

    public void TradeCenterUsesSeparatedProposalBucketsAndTeamContext()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("CreateTradeCenterWorkspace", StringComparison.Ordinal), "Trades should use the integrated Trade Center workspace.");
        Assert.True(source.Contains("RefreshTradeCenterWorkspace", StringComparison.Ordinal), "Trade Center should refresh after actions.");
        Assert.True(source.Contains("BuildTradeCenterTeamContext", StringComparison.Ordinal), "Trade Center should show selected team context.");
        Assert.True(source.Contains("BuildTradeAssetPanel(\"Your Assets\"", StringComparison.Ordinal), "Trade Center should show your assets.");
        Assert.True(source.Contains("BuildTradeProposalBucket(\"You Give\"", StringComparison.Ordinal), "Trade Center should keep You Give separate.");
        Assert.True(source.Contains("BuildTradeProposalBucket(\"You Receive\"", StringComparison.Ordinal), "Trade Center should keep You Receive separate.");
        Assert.True(source.Contains("BuildTradeAssetPanel(\"Their Assets\"", StringComparison.Ordinal), "Trade Center should show other organization assets.");
        Assert.True(source.Contains("\"Roster\"", StringComparison.Ordinal), "Asset source tabs should include roster.");
        Assert.True(source.Contains("\"Prospects\"", StringComparison.Ordinal), "Asset source tabs should include prospects.");
        Assert.True(source.Contains("\"Draft Picks\"", StringComparison.Ordinal), "Asset source tabs should include draft picks.");
    }

    public void TradeCenterShowsEvaluationCounterAndImpactCards()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("BuildTradeCenterEvaluation", StringComparison.Ordinal), "Trade Center should have an evaluation panel.");
        Assert.True(source.Contains("CurrentTradeEvaluationText", StringComparison.Ordinal), "AI evaluation should be visible.");
        Assert.True(source.Contains("CurrentTradeEvaluationReasons", StringComparison.Ordinal), "AI reasons should be visible.");
        Assert.True(source.Contains("CurrentTradeCounterText", StringComparison.Ordinal), "Counteroffer text should be visible.");
        Assert.True(source.Contains("AcceptCurrentTradeCounter", StringComparison.Ordinal), "Applying a counter should update the proposal only.");
        Assert.True(source.Contains("CurrentTradeRosterImpact", StringComparison.Ordinal), "Roster impact should be visible.");
        Assert.True(source.Contains("CurrentTradeBudgetImpact", StringComparison.Ordinal), "Cap/budget impact should be visible.");
        Assert.True(source.Contains("CurrentTradeScarcityText", StringComparison.Ordinal), "Position scarcity should be visible.");
        Assert.True(source.Contains("ProposeCurrentTrade", StringComparison.Ordinal), "Accepted framework should still go through existing trade proposal flow.");
        Assert.True(source.Contains("Trades never auto-complete", StringComparison.Ordinal), "UI should state trades do not auto-complete.");
    }

    public void ClickableAssetsAndHiddenRatingsRemainSafe()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("OpenUniversalPersonCard", StringComparison.Ordinal), "Players and prospects should open the universal person card.");
        Assert.True(source.Contains("OpenTradeAssetProfile", StringComparison.Ordinal), "Trade assets should have profile access without destroying proposals.");
        Assert.True(source.Contains("TradeAssetRowTemplate", StringComparison.Ordinal), "Trade assets should use compact asset cards.");
        Assert.False(source.Contains("TrueOverall", StringComparison.Ordinal), "Presentation should not expose true overall ratings.");
        Assert.False(source.Contains("TruePotential", StringComparison.Ordinal), "Presentation should not expose true potential ratings.");
    }

    private static string AlphaDesktopSource() =>
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
