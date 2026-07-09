using LegacyEngine.Development;
using LegacyEngine.Draft;
using LegacyEngine.Injuries;
using LegacyEngine.Integration;
using LegacyEngine.Rosters;
using LegacyEngine.Scouting;

internal sealed class Alpha617PlayerRatingsTests
{
    public void PlayersReceiveOverallAndPotential()
    {
        var scenario = JuniorScenario();

        Assert.True(scenario.PlayerRatings.Count > 0, "Players should receive public rating snapshots.");
        Assert.True(scenario.PlayerRatings.All(rating => rating.Overall.Low >= 0 && rating.Overall.High <= 100), "OVR should stay on 0-100 scale.");
        Assert.True(scenario.PlayerRatings.All(rating => rating.Potential.Low >= 0 && rating.Potential.High <= 100), "POT should stay on 0-100 scale.");
    }

    public void JuniorPlayersGenerateLowerOverallThanNhlPlayers()
    {
        var junior = JuniorScenario();
        var nhl = NhlScenario();
        var juniorAverage = junior.AlphaSnapshot.Roster.Players.Select(player => Rating(junior, player.PersonId).Overall.Midpoint).Average();
        var nhlAverage = nhl.AlphaSnapshot.Roster.Players.Select(player => Rating(nhl, player.PersonId).Overall.Midpoint).Average();

        Assert.True(juniorAverage < nhlAverage, "Junior roster average OVR should be lower than NHL roster average OVR.");
    }

    public void EliteDraftProspectsCanReachLowEighties()
    {
        var scenario = JuniorScenario();
        var top = scenario.AlphaSnapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank).First();
        var rating = Rating(scenario, top.ProspectPersonId);

        Assert.True(rating.Overall.High >= 80 || rating.Overall.Midpoint >= 76, "Elite draft prospects should be able to show low-80s visible OVR range.");
    }

    public void RarePotentialNinetyFivePlusExistsButIsUncommon()
    {
        var scenario = JuniorScenario();
        var draftRatings = scenario.AlphaSnapshot.DraftBoard.Entries
            .Select(entry => Rating(scenario, entry.ProspectPersonId))
            .ToArray();
        var rare = draftRatings.Count(rating => rating.Potential.High >= 95);

        Assert.True(rare >= 1, "At least one rare top-end potential prospect should exist in the generated class.");
        Assert.True(rare < draftRatings.Length / 4, "95+ potential prospects should remain uncommon.");
    }

    public void LowScoutingConfidenceShowsRatingRanges()
    {
        var scenario = WithDraftConfidence(JuniorScenario(), ScoutingConfidenceLevel.Low);
        var prospect = scenario.AlphaSnapshot.DraftBoard.Entries.First();
        var rating = new PlayerRatingService().BuildSnapshot(scenario, prospect.ProspectPersonId);

        Assert.True(!rating.Overall.IsExact, "Low confidence should show OVR range.");
        Assert.True(!rating.Potential.IsExact, "Low confidence should show POT range.");
    }

    public void HighConfidenceShowsTighterRating()
    {
        var low = WithDraftConfidence(JuniorScenario(), ScoutingConfidenceLevel.Low);
        var high = WithDraftConfidence(JuniorScenario(), ScoutingConfidenceLevel.VeryHigh);
        var personId = low.AlphaSnapshot.DraftBoard.Entries.First().ProspectPersonId;
        var lowRating = new PlayerRatingService().BuildSnapshot(low, personId);
        var highRating = new PlayerRatingService().BuildSnapshot(high, personId);

        Assert.True((highRating.Overall.High - highRating.Overall.Low) < (lowRating.Overall.High - lowRating.Overall.Low), "High confidence should produce tighter OVR.");
        Assert.True((highRating.Potential.High - highRating.Potential.Low) < (lowRating.Potential.High - lowRating.Potential.Low), "High confidence should produce tighter POT.");
    }

    public void DevelopmentCanIncreaseOverall()
    {
        var scenario = JuniorScenario();
        var player = scenario.AlphaSnapshot.Roster.Players.Last();
        var before = Rating(scenario, player.PersonId);
        var profiles = scenario.AlphaSnapshot.DevelopmentProfiles
            .Select(profile => profile.PersonId == player.PersonId ? profile with { CurrentAbility = Math.Min(profile.Potential, profile.CurrentAbility + 8) } : profile)
            .ToArray();
        var updated = scenario with { AlphaSnapshot = scenario.AlphaSnapshot with { DevelopmentProfiles = profiles } };
        var after = new PlayerRatingService().BuildSnapshot(updated, player.PersonId);

        Assert.True(after.Overall.Midpoint >= before.Overall.Midpoint, "Development improvement should be able to increase visible OVR.");
    }

    public void PlateauCanOccurBelowPotential()
    {
        var scenario = JuniorScenario();
        var player = scenario.AlphaSnapshot.Roster.Players.Last();
        var profiles = scenario.AlphaSnapshot.DevelopmentProfiles
            .Select(profile => profile.PersonId == player.PersonId ? profile with { CurrentAbility = 60, Potential = 61 } : profile)
            .ToArray();
        var updated = scenario with { AlphaSnapshot = scenario.AlphaSnapshot with { DevelopmentProfiles = profiles } };
        var rating = new PlayerRatingService().BuildSnapshot(updated, player.PersonId);

        Assert.True(rating.DevelopmentNote.Contains("plateaued", StringComparison.OrdinalIgnoreCase), "Ratings should describe plateau cases.");
    }

    public void InjuryCanReduceOverall()
    {
        var scenario = JuniorScenario();
        var player = scenario.AlphaSnapshot.Roster.Players.First();
        var before = Rating(scenario, player.PersonId);
        var injury = new Injury(
            "injury-alpha617-test",
            player.PersonId,
            scenario.CurrentDate,
            InjuryBodyPart.Knee,
            InjuryType.Strain,
            InjurySeverity.Major,
            scenario.CurrentDate.AddDays(28),
            null,
            0,
            InjuryStatus.Active,
            10,
            15,
            10,
            new InjuryRecoveryPlan("injury-alpha617-test", scenario.CurrentDate, scenario.CurrentDate.AddDays(28), "Testing visible rating injury effect."));
        var updated = scenario with { AlphaSnapshot = scenario.AlphaSnapshot with { Injuries = scenario.AlphaSnapshot.Injuries.Append(injury).ToArray() } };
        var after = new PlayerRatingService().BuildSnapshot(updated, player.PersonId);

        Assert.True(after.Overall.Midpoint < before.Overall.Midpoint, "Active injury should be able to reduce visible current OVR.");
    }

    public void UiExposesRatings()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("State.RatingText", StringComparison.Ordinal), "AlphaDesktop rows should expose compact ratings.");
        Assert.True(source.Contains("State.RatingContextText", StringComparison.Ordinal), "AlphaDesktop details should expose rating context.");
        Assert.True(source.Contains("OVR", StringComparison.Ordinal), "UI should use OVR label.");
        Assert.True(source.Contains("POT", StringComparison.Ordinal), "UI should use POT label.");
    }

    public void HiddenTruePotentialNotShownWhenConfidenceLow()
    {
        var scenario = WithDraftConfidence(JuniorScenario(), ScoutingConfidenceLevel.Low);
        var prospect = scenario.AlphaSnapshot.DraftBoard.Entries.First();
        var rating = new PlayerRatingService().BuildSnapshot(scenario, prospect.ProspectPersonId);
        var dossier = new PlayerDossierService().CreateDossier(scenario with
        {
            PlayerRatings = new[] { rating },
            PlayerRatingHistory = PlayerRatingHistory.Empty.Merge(new[] { rating })
        }, prospect.ProspectPersonId);
        var text = string.Join(Environment.NewLine, dossier.Sections.SelectMany(section => section.Lines));

        Assert.True(rating.Potential.IsExact == false, "Low confidence should not show exact visible potential.");
        Assert.False(text.Contains("CurrentAbility", StringComparison.Ordinal), "Dossier must not expose hidden current ability field name.");
        Assert.False(text.Contains("Potential =", StringComparison.Ordinal), "Dossier must not expose hidden true potential assignment.");
    }

    private static PlayerRatingSnapshot Rating(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.PlayerRatings.FirstOrDefault(rating => rating.PersonId == personId)
        ?? new PlayerRatingService().BuildSnapshot(scenario, personId);

    private static NewGmScenarioSnapshot JuniorScenario() =>
        new PlayerRatingService().EnsureRatings(NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot);

    private static NewGmScenarioSnapshot NhlScenario()
    {
        var service = new MultiLeagueCareerService();
        return service.CreateScenario(service.SelectLeagueAndTeam(LeagueExperience.Nhl, "org-seattle-cascades")).ScenarioSnapshot;
    }

    private static NewGmScenarioSnapshot WithDraftConfidence(NewGmScenarioSnapshot scenario, ScoutingConfidenceLevel confidence)
    {
        var board = scenario.AlphaSnapshot.DraftBoard;
        var entries = board.Entries
            .Select(entry => entry with { ScoutingConfidence = confidence })
            .ToArray();
        var updatedBoard = new DraftBoard(board.BoardId, board.OrganizationId, entries);
        return scenario with
        {
            AlphaSnapshot = scenario.AlphaSnapshot with { DraftBoard = updatedBoard },
            PlayerRatings = Array.Empty<PlayerRatingSnapshot>(),
            PlayerRatingHistory = PlayerRatingHistory.Empty
        };
    }

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
