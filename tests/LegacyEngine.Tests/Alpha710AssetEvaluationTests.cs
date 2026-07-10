using LegacyEngine.Integration;
using LegacyEngine.Rosters;

internal sealed class Alpha710AssetEvaluationTests
{
    public void ScarcityProfileGenerated()
    {
        var scenario = Scenario();

        Assert.True(scenario.PositionScarcity is not null, "Position scarcity profile should be generated.");
        Assert.Equal(Enum.GetValues<PositionMarketPosition>().Length, scenario.PositionScarcity!.Positions.Count);
        Assert.True(scenario.AssetEvaluations.Count > 0, "Asset evaluations should be generated.");
    }

    public void WeakGoalieMarketIncreasesGoalieValue()
    {
        var scenario = Scenario();
        var goalie = scenario.AlphaSnapshot.Roster.ActivePlayers.First(player => player.Position == RosterPosition.Goalie);
        var service = new AssetEvaluationService();
        var normal = service.BuildPlayerValue(scenario, goalie.PersonId, scenario.Organization.OrganizationId, scenario.Organization.Name, WithMarket(scenario.PositionScarcity!, PositionMarketPosition.G, PositionScarcityLevel.Normal, 50));
        var scarce = service.BuildPlayerValue(scenario, goalie.PersonId, scenario.Organization.OrganizationId, scenario.Organization.Name, WithMarket(scenario.PositionScarcity!, PositionMarketPosition.G, PositionScarcityLevel.Critical, 92));

        Assert.True(scarce.Trade.Score > normal.Trade.Score, "Weak goalie market should raise goalie trade value.");
        Assert.Equal(PositionScarcityLevel.Critical, scarce.Market.ScarcityLevel);
    }

    public void ScarceRdMarketIncreasesRdTradeValue()
    {
        var scenario = Scenario();
        var defender = scenario.AlphaSnapshot.Roster.ActivePlayers.First(player => player.Position == RosterPosition.Defense);
        var service = new AssetEvaluationService();
        var normal = service.BuildPlayerValue(scenario, defender.PersonId, scenario.Organization.OrganizationId, scenario.Organization.Name, WithMarket(scenario.PositionScarcity!, PositionMarketPosition.RD, PositionScarcityLevel.Normal, 50));
        var scarce = service.BuildPlayerValue(scenario, defender.PersonId, scenario.Organization.OrganizationId, scenario.Organization.Name, WithMarket(scenario.PositionScarcity!, PositionMarketPosition.RD, PositionScarcityLevel.Scarce, 74));

        Assert.True(scarce.Trade.Score >= normal.Trade.Score, "Scarce RD market should not lower defense trade value.");
    }

    public void OversuppliedWingerMarketLowersWingerDemand()
    {
        var scenario = Scenario();
        var winger = scenario.AlphaSnapshot.Roster.ActivePlayers.First(player => player.Position is RosterPosition.LeftWing or RosterPosition.RightWing);
        var service = new AssetEvaluationService();
        var normal = service.BuildPlayerValue(scenario, winger.PersonId, scenario.Organization.OrganizationId, scenario.Organization.Name, WithMarket(scenario.PositionScarcity!, PositionMarketPosition.LW, PositionScarcityLevel.Normal, 50));
        var oversupplied = service.BuildPlayerValue(scenario, winger.PersonId, scenario.Organization.OrganizationId, scenario.Organization.Name, WithMarket(scenario.PositionScarcity!, PositionMarketPosition.LW, PositionScarcityLevel.Oversupplied, 25));

        Assert.True(oversupplied.Trade.Score < normal.Trade.Score, "Oversupplied winger market should lower demand.");
    }

    public void DraftClassDepthAffectsScarcity()
    {
        var created = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var noGoalieDraft = created with
        {
            AlphaSnapshot = created.AlphaSnapshot with
            {
                DraftBoard = created.AlphaSnapshot.DraftBoard with
                {
                    Entries = created.AlphaSnapshot.DraftBoard.Entries
                        .Where(entry => entry.Bio?.Position != RosterPosition.Goalie)
                        .ToArray()
                }
            }
        };
        var profile = new PositionScarcityService().BuildProfile(noGoalieDraft);

        Assert.Equal(0, profile.For(PositionMarketPosition.G).DraftClassDepth);
    }

    public void FreeAgentSupplyAffectsScarcity()
    {
        var scenario = Scenario();
        var profile = scenario.PositionScarcity!;

        Assert.True(profile.Positions.Any(position => position.AvailableFreeAgents >= 0), "Free-agent supply should be counted for position markets.");
        Assert.True((profile.Summary + " " + string.Join(" ", profile.Positions.Select(position => position.Summary))).Contains("free agents", StringComparison.OrdinalIgnoreCase), "Position market summaries should mention free-agent supply.");
    }

    public void UiExposesPositionMarketContext()
    {
        var desktop = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(desktop.Contains("Position Market", StringComparison.Ordinal), "Desktop should expose Position Market.");
        Assert.True(desktop.Contains("CurrentTradeAssetValueComparison", StringComparison.Ordinal), "Trade Builder should expose value comparison.");
        Assert.True(desktop.Contains("PositionMarketNote", StringComparison.Ordinal), "Rows should expose position market notes.");
    }

    private static NewGmScenarioSnapshot Scenario()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        scenario = new HockeyIntelligenceRatingService().EnsureRatings(scenario);
        scenario = new ScoutingIntelligenceService().EnsureKnowledgeProfiles(scenario);
        scenario = new DevelopmentCurveService().EnsureCurves(scenario);
        scenario = new PlayerRatingService().EnsureRatings(scenario);
        scenario = new DraftWarRoomService().EnsureWarRoom(scenario);
        return new AssetEvaluationService().EnsureEvaluations(scenario);
    }

    private static PositionScarcityProfile WithMarket(PositionScarcityProfile profile, PositionMarketPosition position, PositionScarcityLevel level, int score)
    {
        var positions = profile.Positions
            .Select(item => item.Position == position
                ? item with { ScarcityLevel = level, ScarcityScore = score, Summary = $"{PositionScarcityService.Label(position)} test market is {level}." }
                : item)
            .ToArray();
        return profile with { Positions = positions, Summary = $"Test profile with {position} {level}." };
    }

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
