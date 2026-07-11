using LegacyEngine.Draft;
using LegacyEngine.Integration;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;

internal sealed class Alpha711DraftBoardRealismTests
{
    public void NhlTopSixDoesNotContainFiveGoalies()
    {
        var scenario = WarRoom(NhlScenario(), RosterPosition.Goalie, RosterPosition.Goalie, RosterPosition.Goalie, RosterPosition.Goalie, RosterPosition.Goalie, RosterPosition.Center, RosterPosition.Defense, RosterPosition.LeftWing, RosterPosition.RightWing);
        var topSixGoalies = scenario.AlphaSnapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank).Take(6).Count(entry => entry.Bio?.Position == RosterPosition.Goalie);

        Assert.True(topSixGoalies < 5, "NHL realism pass should prevent five goalies in the top six.");
        Assert.True(scenario.DraftWarRoom.RealismValidation is not null, "War Room should store realism validation.");
    }

    public void TopTenIncludesMultipleSkaterGroups()
    {
        var scenario = WarRoom(NhlScenario(), RosterPosition.Goalie, RosterPosition.Goalie, RosterPosition.Goalie, RosterPosition.Goalie, RosterPosition.Goalie, RosterPosition.Defense, RosterPosition.Center, RosterPosition.LeftWing, RosterPosition.RightWing);
        var groups = scenario.AlphaSnapshot.DraftBoard.Entries
            .OrderBy(entry => entry.Rank)
            .Take(10)
            .Select(entry => DraftPositionGroupMapper.FromRosterPosition(entry.Bio?.Position ?? RosterPosition.Unknown))
            .Distinct()
            .Count();

        Assert.True(groups >= 3, "Top ten should include multiple skater groups.");
    }

    public void FirstRoundHasBelievableForwardDefenseMix()
    {
        var scenario = WarRoom(WithTheme(NhlScenario(), DraftClassTheme.BalancedClass), BalancedNhlPositions());
        var firstRound = scenario.AlphaSnapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank).Take(32).ToArray();
        var forwards = firstRound.Count(entry => entry.Bio?.Position is RosterPosition.Center or RosterPosition.LeftWing or RosterPosition.RightWing);
        var defense = firstRound.Count(entry => entry.Bio?.Position == RosterPosition.Defense);

        Assert.True(forwards >= 12, "NHL-style first round should include a believable forward group.");
        Assert.True(defense is >= 6 and <= 18, "NHL-style first round should include defensemen without becoming all defense.");
    }

    public void GeneratedNhlTopTwentyIncludesForwardsAndLimitsGoalieRun()
    {
        var scenario = NhlScenario();
        var topTwenty = scenario.AlphaSnapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank).Take(20).ToArray();
        var forwards = topTwenty.Count(entry => entry.Bio?.Position is RosterPosition.Center or RosterPosition.LeftWing or RosterPosition.RightWing);
        var defense = topTwenty.Count(entry => entry.Bio?.Position == RosterPosition.Defense);
        var goalies = topTwenty.Count(entry => entry.Bio?.Position == RosterPosition.Goalie);

        Assert.True(forwards >= 10, $"Fresh NHL draft boards should show forwards early; found {forwards} forward(s) in the top 20.");
        Assert.True(defense <= 9, $"Fresh NHL draft boards should not be all defense after goalies; found {defense} defensemen in the top 20.");
        Assert.True(goalies <= 3, $"Fresh NHL draft boards should not front-load goalies; found {goalies} goalie(s) in the top 20.");
    }

    public void LowGoalieCountIsNormalForNhlBoards()
    {
        var scenario = WarRoom(WithTheme(NhlScenario(), DraftClassTheme.BalancedClass), BalancedNhlPositions());
        var topTenGoalies = scenario.AlphaSnapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank).Take(10).Count(entry => entry.Bio?.Position == RosterPosition.Goalie);

        Assert.True(topTenGoalies <= 1, "NHL top ten should normally have zero or one goalie.");
    }

    public void StrongGoalieClassMayPlaceGoalieHigherButRejectsExtremeClustering()
    {
        var scenario = WithTheme(NhlScenario(), DraftClassTheme.StrongGoalieClass);
        var badBoard = WithPositions(scenario, RosterPosition.Goalie, RosterPosition.Goalie, RosterPosition.Goalie, RosterPosition.Goalie, RosterPosition.Goalie, RosterPosition.Center, RosterPosition.Defense);
        var profile = new DraftPositionValueService().BuildRealismProfile(badBoard);
        var result = new DraftBoardRealismValidator().ValidateBoard(badBoard.AlphaSnapshot.DraftBoard.Entries, profile, badBoard.CurrentDraftClassProfile);

        Assert.False(result.IsValid, "Strong goalie classes still should reject extreme goalie clustering.");
        Assert.True(result.Issues.Any(issue => issue.Code.Contains("Goalie", StringComparison.OrdinalIgnoreCase) || issue.Code == "StrongGoalieCluster"), "Validator should identify goalie clustering.");
    }

    public void DeepDefenseClassIncreasesDefenseWithoutTakingWholeTopBoard()
    {
        var scenario = WarRoom(WithTheme(NhlScenario(), DraftClassTheme.DeepDefenseClass));
        var topTen = scenario.AlphaSnapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank).Take(10).ToArray();
        var defense = topTen.Count(entry => entry.Bio?.Position == RosterPosition.Defense);

        Assert.True(defense >= 2, "Deep defense class should lift defensemen.");
        Assert.True(defense < 10, "Deep defense class should not make the entire top ten defensemen.");
    }

    public void JuniorBoardAllowsBroaderMix()
    {
        var scenario = WarRoom(NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot);
        var profile = scenario.DraftWarRoom.RealismProfile!;

        Assert.True(profile.HistoricalDistribution.ProfileType != DraftDistributionProfileType.NhlEntryDraft, "Junior profile should not use NHL Entry distribution.");
        Assert.True(profile.HistoricalDistribution.FirstRoundGoalies.Maximum >= 4, "Junior boards should discount goalies less than NHL boards.");
    }

    public void WhlOhlQmjhlProfilesDiffer()
    {
        var whl = HistoricalDraftDistributionProfile.ForLeague("junior", "whl-west");
        var ohl = HistoricalDraftDistributionProfile.ForLeague("junior", "ohl-central");
        var qmjhl = HistoricalDraftDistributionProfile.ForLeague("junior", "qmjhl-east");

        Assert.True(whl.ProfileType != ohl.ProfileType, "WHL and OHL profiles should differ.");
        Assert.True(ohl.ProfileType != qmjhl.ProfileType, "OHL and QMJHL profiles should differ.");
    }

    public void LongPositionRunIsFlagged()
    {
        var scenario = WithPositions(NhlScenario(), RosterPosition.Defense, RosterPosition.Defense, RosterPosition.Defense, RosterPosition.Defense, RosterPosition.Defense, RosterPosition.Defense, RosterPosition.Defense, RosterPosition.Center);
        var result = new DraftBoardRealismValidator().ValidateBoard(
            scenario.AlphaSnapshot.DraftBoard.Entries,
            new DraftPositionValueService().BuildRealismProfile(scenario),
            scenario.CurrentDraftClassProfile);

        Assert.True(result.Issues.Any(issue => issue.Code == "ConsecutiveRun"), "Validator should flag long same-position runs.");
    }

    public void MissingGroupsAreFlagged()
    {
        var scenario = WithPositions(NhlScenario(), Enumerable.Repeat(RosterPosition.Center, 60).ToArray());
        var result = new DraftBoardRealismValidator().ValidateBoard(
            scenario.AlphaSnapshot.DraftBoard.Entries,
            new DraftPositionValueService().BuildRealismProfile(scenario),
            scenario.CurrentDraftClassProfile);

        Assert.True(result.Issues.Any(issue => issue.Code == "MissingPositionGroup"), "Validator should flag missing major position groups.");
    }

    public void RebalanceMovesComparableProspectsAndTerminates()
    {
        var scenario = WithPositions(NhlScenario(), RosterPosition.Goalie, RosterPosition.Goalie, RosterPosition.Goalie, RosterPosition.Goalie, RosterPosition.Center, RosterPosition.Defense, RosterPosition.LeftWing, RosterPosition.RightWing);
        scenario = new HockeyIntelligenceRatingService().EnsureRatings(scenario);
        scenario = new PlayerRatingService().EnsureRatings(scenario);
        var service = new DraftPositionValueService();
        var profile = service.BuildRealismProfile(scenario);
        var value = service.BuildPositionValueProfile(scenario);
        var result = new DraftBoardRealismValidator().RebalanceBoard(scenario, profile, value);

        Assert.True(result.Passes <= profile.MaximumPasses, "Rebalancing must terminate within max passes.");
        Assert.True(result.Moves.Count > 0, "Rebalancing should move comparable prospects when a board is unrealistic.");
    }

    public void EliteExceptionIsPreserved()
    {
        var scenario = WarRoom(WithTheme(NhlScenario(), DraftClassTheme.EliteTopEnd));
        var first = scenario.DraftWarRoom.OriginalBoardSnapshot.OrderBy(item => item.Rank).First();
        var final = scenario.DraftWarRoom.FinalBoardSnapshot.First(item => item.ProspectPersonId == first.ProspectPersonId);

        Assert.True(final.Rank <= 3, "Elite top-board prospect should not be buried only to satisfy distribution.");
    }

    public void DeterministicHundredClassProfileVariationExists()
    {
        var generator = new DraftClassGenerator();
        var profiles = Enumerable.Range(0, 100)
            .Select(index => generator.GenerateProfile(RulebookPresets.CreateNhlStyle(), 2030 + index, $"nhl-{index}", 224))
            .ToArray();

        Assert.True(profiles.Select(profile => profile.Theme).Distinct().Count() >= 6, "100 deterministic class profiles should vary themes.");
        Assert.True(profiles.Select(profile => profile.PositionalDepth.GetValueOrDefault(RosterPosition.Goalie)).Distinct().Count() >= 2, "100 deterministic class profiles should vary goalie depth.");
    }

    public void AiDraftReachIsBoundedByDraftValue()
    {
        var scenario = WarRoom(NhlScenario());
        var service = new DraftWarRoomService();
        var top = scenario.AlphaSnapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank).First();
        var late = scenario.AlphaSnapshot.DraftBoard.Entries.OrderByDescending(entry => entry.Rank).First();
        var organizationId = scenario.OrganizationAiProfiles.First().OrganizationId;

        Assert.True(service.ScoreAiDraftFit(scenario, organizationId, top, 0) > service.ScoreAiDraftFit(scenario, organizationId, late, 0), "AI needs can bend the board but should stay bounded by draft value.");
    }

    public void HiddenTrueRatingsAreNotExposed()
    {
        var scenario = WarRoom(NhlScenario());
        var entry = scenario.AlphaSnapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank).First();
        var evaluation = new DraftPositionValueService().EvaluateEntry(scenario, entry, scenario.DraftWarRoom.PositionValueProfile);
        var desktop = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.False(evaluation.Explanation.Contains("PlayerTrueRatings", StringComparison.Ordinal), "Draft value explanation should not expose hidden true ratings.");
        Assert.False(desktop.Contains("ScenarioSnapshot.TrueRatings", StringComparison.Ordinal), "AlphaDesktop should not render hidden true ratings.");
    }

    public void AlphaDesktopExposesDraftRealismContext()
    {
        var desktop = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(desktop.Contains("DraftBoardRealismText", StringComparison.Ordinal), "Desktop should expose draft board realism context.");
        Assert.True(desktop.Contains("DraftPositionValueText", StringComparison.Ordinal), "Desktop should expose position value context.");
        Assert.True(desktop.Contains("DraftValueContext", StringComparison.Ordinal), "Desktop should expose draft value explanations.");
    }

    private static NewGmScenarioSnapshot NhlScenario() =>
        NewGmScenarioBootstrapper.CreateScenario(rulebook: RulebookPresets.CreateNhlStyle()).ScenarioSnapshot;

    private static NewGmScenarioSnapshot WarRoom(NewGmScenarioSnapshot scenario, params RosterPosition[] positions)
    {
        if (positions.Length > 0)
        {
            scenario = WithPositions(scenario, positions);
        }

        scenario = new HockeyIntelligenceRatingService().EnsureRatings(scenario);
        scenario = new ScoutingIntelligenceService().EnsureKnowledgeProfiles(scenario);
        scenario = new DevelopmentCurveService().EnsureCurves(scenario);
        scenario = new PlayerRatingService().EnsureRatings(scenario);
        return new DraftWarRoomService().EnsureWarRoom(scenario);
    }

    private static NewGmScenarioSnapshot WithTheme(NewGmScenarioSnapshot scenario, DraftClassTheme theme) =>
        scenario.CurrentDraftClassProfile is null
            ? scenario
            : scenario with
            {
                CurrentDraftClassProfile = scenario.CurrentDraftClassProfile with { Theme = theme },
                DraftWarRoom = DraftWarRoomState.Empty,
                PlayerRatings = Array.Empty<PlayerRatingSnapshot>(),
                ScoutedRatings = Array.Empty<PlayerScoutedRatings>(),
                TrueRatings = Array.Empty<PlayerTrueRatings>()
            };

    private static NewGmScenarioSnapshot WithPositions(NewGmScenarioSnapshot scenario, params RosterPosition[] positions)
    {
        if (positions.Length == 1 && positions[0] == RosterPosition.Unknown)
        {
            return scenario;
        }

        var entries = scenario.AlphaSnapshot.DraftBoard.Entries
            .OrderBy(entry => entry.Rank)
            .Select((entry, index) =>
            {
                var position = index < positions.Length ? positions[index] : entry.Bio?.Position ?? RosterPosition.Center;
                return entry with
                {
                    Rank = index + 1,
                    Bio = BioWithPosition(entry, position)
                };
            })
            .ToArray();
        return scenario with
        {
            AlphaSnapshot = scenario.AlphaSnapshot with
            {
                DraftBoard = scenario.AlphaSnapshot.DraftBoard with { Entries = entries }
            },
            DraftWarRoom = DraftWarRoomState.Empty,
            PlayerRatings = Array.Empty<PlayerRatingSnapshot>(),
            ScoutedRatings = Array.Empty<PlayerScoutedRatings>(),
            TrueRatings = Array.Empty<PlayerTrueRatings>()
        };
    }

    private static DraftProspectBio BioWithPosition(DraftBoardEntry entry, RosterPosition position)
    {
        var bio = entry.Bio ?? new DraftProspectBio(
            RosterPosition.Center,
            "Shoots L",
            72,
            185,
            2008,
            "Test City",
            "ON",
            "Canada",
            "Test Club",
            "Test League",
            "Competitive prospect.",
            "depth prospect");
        var projection = position switch
        {
            RosterPosition.Goalie => "starter upside",
            RosterPosition.Defense => "two-way defense projection",
            RosterPosition.Center => "competitive center projection",
            RosterPosition.LeftWing => "scoring winger projection",
            RosterPosition.RightWing => "play-driving winger projection",
            _ => "draft prospect"
        };
        return bio with { Position = position, PotentialLineupProjection = projection };
    }

    private static RosterPosition[] BalancedNhlPositions()
    {
        var pattern = new[]
        {
            RosterPosition.Center,
            RosterPosition.Defense,
            RosterPosition.LeftWing,
            RosterPosition.RightWing,
            RosterPosition.Center,
            RosterPosition.Defense,
            RosterPosition.LeftWing,
            RosterPosition.RightWing,
            RosterPosition.Center,
            RosterPosition.Goalie
        };
        return Enumerable.Range(0, 70)
            .Select(index => pattern[index % pattern.Length])
            .ToArray();
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
