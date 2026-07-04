using LegacyEngine.Events;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;

internal sealed class RosterEngineTests
{
    public void RosterCreation()
    {
        var roster = new RosterEngine().CreateRoster("roster-001", "org-001");

        roster.Validate();
        Assert.Equal("roster-001", roster.RosterId);
        Assert.Equal("org-001", roster.OrganizationId);
        Assert.Equal(0, roster.Players.Count);
    }

    public void AddPlayer()
    {
        var engine = new RosterEngine();
        var result = engine.AddPlayer(
            engine.CreateRoster("roster-001", "org-001"),
            AddMove("player-001", RosterPosition.Center));

        Assert.True(result.Success, "Add player should succeed.");
        Assert.Equal(1, result.Roster.Players.Count);
        Assert.Equal(RosterStatus.Active, result.Roster.Players[0].Status);
        Assert.Equal(RosterPosition.Center, result.Roster.Players[0].Position);
    }

    public void RemovePlayer()
    {
        var engine = new RosterEngine();
        var roster = AddValidCorePlayers(engine.CreateRoster("roster-001", "org-001"), engine, playerCount: 19);
        var result = engine.RemovePlayer(roster, new RosterMove(RosterMoveType.Remove, "player-003", new DateOnly(2026, 9, 10)), BuildRosterValidator());

        Assert.True(result.Success, "Remove player should succeed.");
        Assert.False(result.Roster.Players.Any(player => player.PersonId == "player-003"), "Removed player should not remain on roster.");
    }

    public void DuplicateActivePlayerRejected()
    {
        var engine = new RosterEngine();
        var roster = engine.AddPlayer(engine.CreateRoster("roster-001", "org-001"), AddMove("player-001", RosterPosition.Center)).Roster;
        var result = engine.AddPlayer(roster, AddMove("player-001", RosterPosition.Center));

        Assert.False(result.Success, "Duplicate active player should fail.");
        Assert.Equal("DUPLICATE_ACTIVE_PLAYER", result.ValidationResult.RuleCode);
    }

    public void MovePlayerToReserve()
    {
        var engine = new RosterEngine();
        var roster = engine.AddPlayer(engine.CreateRoster("roster-001", "org-001"), AddMove("player-001", RosterPosition.Center)).Roster;
        var result = engine.MovePlayer(roster, new RosterMove(RosterMoveType.MoveToReserve, "player-001", new DateOnly(2026, 9, 2)));

        Assert.True(result.Success, "Move to reserve should succeed.");
        Assert.Equal(RosterStatus.Reserve, result.Roster.FindPlayer("player-001")!.Status);
    }

    public void MovePlayerToInjuredReserve()
    {
        var eventEngine = new EventEngine();
        var engine = new RosterEngine(eventEngine);
        var roster = engine.AddPlayer(engine.CreateRoster("roster-001", "org-001"), AddMove("player-001", RosterPosition.Center)).Roster;
        var result = engine.MovePlayer(roster, new RosterMove(RosterMoveType.MoveToInjuredReserve, "player-001", new DateOnly(2026, 9, 3)));

        Assert.True(result.Success, "Move to injured reserve should succeed.");
        Assert.Equal(RosterStatus.InjuredReserve, result.Roster.FindPlayer("player-001")!.Status);
        Assert.True(eventEngine.Queue.PendingEvents.Any(item => item.EventType == LegacyEventType.PlayerMovedToInjuredReserve), "IR event should be queued.");
    }

    public void ReleasePlayer()
    {
        var engine = new RosterEngine();
        var roster = engine.AddPlayer(engine.CreateRoster("roster-001", "org-001"), AddMove("player-001", RosterPosition.Center)).Roster;
        var result = engine.MovePlayer(roster, new RosterMove(RosterMoveType.Release, "player-001", new DateOnly(2026, 9, 4)));

        Assert.True(result.Success, "Release should succeed.");
        Assert.Equal(RosterStatus.Released, result.Roster.FindPlayer("player-001")!.Status);
    }

    public void ReleasedDateStored()
    {
        var engine = new RosterEngine();
        var roster = engine.AddPlayer(engine.CreateRoster("roster-001", "org-001"), AddMove("player-001", RosterPosition.Center)).Roster;
        var result = engine.MovePlayer(roster, new RosterMove(RosterMoveType.Release, "player-001", new DateOnly(2026, 9, 4)));

        Assert.Equal(new DateOnly(2026, 9, 4), result.Roster.FindPlayer("player-001")!.ReleasedDate);
    }

    public void ValidateMaxRosterSize()
    {
        var engine = new RosterEngine();
        var roster = engine.CreateRoster("roster-001", "org-001");
        for (var index = 1; index <= 26; index++)
        {
            roster = roster with
            {
                Players = roster.Players.Append(new RosterPlayer($"player-{index:000}", index <= 2 ? RosterPosition.Goalie : RosterPosition.Center, RosterStatus.Reserve, new DateOnly(2026, 9, 1), Age: 18)).ToArray()
            };
        }

        var validation = engine.ValidateRoster(roster, BuildRosterValidator());

        Assert.False(validation.IsValid, "Roster over max size should fail.");
        Assert.Equal(RuleErrorCodes.RosterTooLarge, validation.RuleCode);
    }

    public void ValidateActiveRosterSize()
    {
        var engine = new RosterEngine();
        var roster = engine.CreateRoster("roster-001", "org-001");
        for (var index = 1; index <= 21; index++)
        {
            roster = roster with
            {
                Players = roster.Players.Append(new RosterPlayer($"player-{index:000}", index <= 2 ? RosterPosition.Goalie : RosterPosition.Center, RosterStatus.Active, new DateOnly(2026, 9, 1), Age: 18)).ToArray()
            };
        }

        var validation = engine.ValidateRoster(roster, BuildRosterValidator());

        Assert.False(validation.IsValid, "Active roster over max size should fail.");
        Assert.Equal(RuleErrorCodes.ActiveRosterTooLarge, validation.RuleCode);
    }

    public void ValidateGoalieRequirement()
    {
        var engine = new RosterEngine();
        var roster = BuildRosterWithPlayers(goalies: 1, skaters: 17, overage: 0, imports: 0);
        var validation = engine.ValidateRoster(roster, BuildRosterValidator());

        Assert.False(validation.IsValid, "Roster with too few goalies should fail.");
        Assert.Equal(RuleErrorCodes.NotEnoughGoalies, validation.RuleCode);
    }

    public void ValidateOverageSlots()
    {
        var engine = new RosterEngine();
        var roster = BuildRosterWithPlayers(goalies: 2, skaters: 17, overage: 4, imports: 0);
        var validation = engine.ValidateRoster(roster, BuildRosterValidator());

        Assert.False(validation.IsValid, "Roster with too many overage players should fail.");
        Assert.Equal(RuleErrorCodes.TooManyOveragePlayers, validation.RuleCode);
    }

    public void ValidateImportSlots()
    {
        var engine = new RosterEngine();
        var roster = BuildRosterWithPlayers(goalies: 2, skaters: 17, overage: 0, imports: 3);
        var validation = engine.ValidateRoster(roster, BuildRosterValidator());

        Assert.False(validation.IsValid, "Roster with too many imports should fail.");
        Assert.Equal(RuleErrorCodes.TooManyImportPlayers, validation.RuleCode);
    }

    public void EventsCreatedForAddRemoveIrRelease()
    {
        var eventEngine = new EventEngine();
        var engine = new RosterEngine(eventEngine);
        var roster = engine.CreateRoster("roster-001", "org-001");
        roster = engine.AddPlayer(roster, AddMove("player-001", RosterPosition.Center)).Roster;
        roster = engine.MovePlayer(roster, new RosterMove(RosterMoveType.MoveToInjuredReserve, "player-001", new DateOnly(2026, 9, 2))).Roster;
        roster = engine.MovePlayer(roster, new RosterMove(RosterMoveType.Release, "player-001", new DateOnly(2026, 9, 3))).Roster;
        roster = engine.AddPlayer(roster, AddMove("player-002", RosterPosition.Goalie)).Roster;
        engine.RemovePlayer(roster, new RosterMove(RosterMoveType.Remove, "player-002", new DateOnly(2026, 9, 4)));

        Assert.True(eventEngine.Queue.PendingEvents.Any(item => item.EventType == LegacyEventType.PlayerAddedToRoster), "Add event should be queued.");
        Assert.True(eventEngine.Queue.PendingEvents.Any(item => item.EventType == LegacyEventType.PlayerMovedToInjuredReserve), "IR event should be queued.");
        Assert.True(eventEngine.Queue.PendingEvents.Any(item => item.EventType == LegacyEventType.PlayerReleased), "Release event should be queued.");
        Assert.True(eventEngine.Queue.PendingEvents.Any(item => item.EventType == LegacyEventType.PlayerRemovedFromRoster), "Remove event should be queued.");
    }

    private static Roster AddValidCorePlayers(Roster roster, RosterEngine engine, int playerCount = 18)
    {
        for (var index = 1; index <= playerCount; index++)
        {
            var position = index <= 2 ? RosterPosition.Goalie : RosterPosition.Center;
            roster = engine.AddPlayer(roster, AddMove($"player-{index:000}", position, age: 18)).Roster;
        }

        return roster;
    }

    private static Roster BuildRosterWithPlayers(int goalies, int skaters, int overage, int imports)
    {
        var roster = new RosterEngine().CreateRoster("roster-001", "org-001");
        var players = new List<RosterPlayer>();
        var total = goalies + skaters;
        for (var index = 1; index <= total; index++)
        {
            players.Add(new RosterPlayer(
                PersonId: $"player-{index:000}",
                Position: index <= goalies ? RosterPosition.Goalie : RosterPosition.Center,
                Status: RosterStatus.Active,
                JoinedDate: new DateOnly(2026, 9, 1),
                Age: index <= overage ? 20 : 18,
                IsImport: index <= imports));
        }

        return roster with { Players = players };
    }

    private static RosterMove AddMove(
        string personId,
        RosterPosition position,
        int? age = 18,
        bool isImport = false) =>
        new(
            MoveType: RosterMoveType.Add,
            PersonId: personId,
            Date: new DateOnly(2026, 9, 1),
            Position: position,
            TargetStatus: RosterStatus.Active,
            Age: age,
            IsImport: isImport);

    private static RosterRuleValidator BuildRosterValidator() =>
        new(BuildRulebook());

    private static Rulebook BuildRulebook() =>
        new()
        {
            RulebookId = "test_rulebook",
            LeagueType = "junior",
            Version = "1.0",
            RosterRules = new RosterRules
            {
                MinRoster = 18,
                MaxRoster = 25,
                ActiveRoster = 20,
                GoaliesRequired = 2,
                OverageSlots = 3,
                ImportSlots = 2
            },
            EligibilityRules = new EligibilityRules { MinAge = 15, MaxAge = 20 },
            ContractRules = new ContractRules
            {
                AllowedContractTypes = new[] { "junior_player_agreement" },
                JuniorStipendsEnabled = true,
                EducationPackagesEnabled = true,
                HousingSupportEnabled = true
            },
            DraftRules = new DraftRules
            {
                DraftEnabled = true,
                Rounds = 8,
                DraftOrder = "reverse_standings"
            },
            PlayoffRules = new PlayoffRules
            {
                TeamsQualify = 8,
                SeriesFormat = new[] { 7, 7, 7 },
                ReseedEachRound = true
            },
            BudgetRules = new BudgetRules { OwnerBudgetEnabled = true }
        };
}
