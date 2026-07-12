using LegacyEngine.Contracts;
using LegacyEngine.Events;
using LegacyEngine.People;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;
using LegacyEngine.Scouting;

namespace LegacyEngine.Integration;

public sealed class FreeAgentMarketService
{
    public FreeAgentMarket GenerateMarket(NewGmScenarioSnapshot scenario, IReadOnlyList<Person> freeAgentPeople)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(freeAgentPeople);

        var agents = freeAgentPeople
            .Select((person, index) => CreateFreeAgent(scenario, person, index))
            .ToArray();
        var market = new FreeAgentMarket($"free-agent-market:{scenario.Organization.OrganizationId}:{scenario.CurrentDate:yyyyMMdd}", scenario.CurrentDate, agents);
        market.Validate();
        return market;
    }

    public FreeAgentMarketResult Shortlist(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId)
    {
        var agent = RequireAgent(scenario, personId);
        var updated = agent with { IsShortlisted = true };
        var next = ReplaceAgent(scenario, updated);
        QueueEvent(registry, next, LegacyEventType.FreeAgentShortlisted, $"{agent.Name} shortlisted", $"{agent.Name} was added to the GM free-agent shortlist.", agent.PersonId);
        return Result(true, next, updated, new[] { Inbox(next, LegacyEventType.FreeAgentShortlisted, "Free agent shortlisted", $"{agent.Name} was added to the shortlist.", agent.PersonId) }, Array.Empty<LeagueTransaction>(), $"{agent.Name} shortlisted.");
    }

    public FreeAgentMarketResult RemoveFromShortlist(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId)
    {
        var agent = RequireAgent(scenario, personId);
        var updated = agent with { IsShortlisted = false };
        var next = ReplaceAgent(scenario, updated);
        return Result(true, next, updated, Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), $"{agent.Name} removed from shortlist.");
    }

    public FreeAgentMarketResult OfferContract(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId)
    {
        var agent = RequireAgent(scenario, personId);
        if (agent.Status is not FreeAgentStatus.Available)
        {
            return Result(false, scenario, agent, Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), $"{agent.Name} is not available.");
        }

        if (scenario.PendingActions.Any(action =>
            action.IsOpen
            && action.PersonId == agent.PersonId
            && action.ActionType == PendingGmActionType.SignFreeAgent))
        {
            return Result(false, scenario, agent, Array.Empty<AlphaInboxItem>(), Array.Empty<LeagueTransaction>(), $"{agent.Name} already has a pending GM signing decision.");
        }

        var offered = agent.Interest.PlayerOrganizationInterest >= 35
            ? agent with { Status = FreeAgentStatus.Negotiating }
            : agent with { Status = FreeAgentStatus.Rejected };
        var next = ReplaceAgent(scenario, offered);

        QueueEvent(registry, next, LegacyEventType.FreeAgentOfferSubmitted, $"Free agent offer: {agent.Name}", $"{agent.Name} received a contract offer from {scenario.Organization.Name}.", agent.PersonId);
        var inbox = new List<AlphaInboxItem>
        {
            Inbox(next, LegacyEventType.FreeAgentOfferSubmitted, "Free agent offer submitted", $"{agent.Name} is reviewing a {agent.ContractAsk.TermYears}-year ask around {agent.ContractAsk.AnnualAmount:C0}.", agent.PersonId)
        };

        if (offered.Status == FreeAgentStatus.Rejected)
        {
            QueueEvent(registry, next, LegacyEventType.FreeAgentOfferRejected, $"Free agent rejected: {agent.Name}", $"{agent.Name} rejected the approach due to low interest.", agent.PersonId, LegacyEventSeverity.Warning);
            inbox.Add(Inbox(next, LegacyEventType.FreeAgentOfferRejected, "Free agent offer rejected", $"{agent.Name} rejected the offer. {agent.Interest.MotivationSummary}", agent.PersonId, LegacyEventSeverity.Warning));
            return Result(true, next, offered, inbox, Array.Empty<LeagueTransaction>(), $"{agent.Name} rejected the free-agent offer.");
        }

        QueueEvent(registry, next, LegacyEventType.FreeAgentOfferAccepted, $"Free agent accepted terms: {agent.Name}", $"{agent.Name} is willing to sign if the GM approves the agreement.", agent.PersonId);
        var pending = new PendingGmActionService().CreatePendingAction(
            registry,
            next,
            PendingGmActionType.SignFreeAgent,
            agent.PersonId,
            $"{agent.Name} accepted the free-agent offer. Budget impact: {BudgetImpact(next, agent)}",
            "Approve the free-agent agreement or decline before a contract is signed.",
            agent.Position,
            PlayerAcquisitionSource.FreeAgentSigning,
            agent.ContractAsk.ContractType);
        next = ReplaceAgent(pending.ScenarioSnapshot, offered);
        inbox.AddRange(pending.InboxItems);
        if (WouldBeOverBudget(next, agent))
        {
            inbox.Add(Inbox(next, LegacyEventType.BudgetApproved, "Owner budget warning", $"{agent.Name}'s ask may push hockey operations over budget. {BudgetImpact(next, agent)}", agent.PersonId, LegacyEventSeverity.Warning));
        }

        return Result(true, next, offered, inbox, Array.Empty<LeagueTransaction>(), $"{agent.Name} accepted terms; GM approval is required before signing.");
    }

    public FreeAgentMarketResult InviteToCamp(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId)
    {
        var agent = RequireAgent(scenario, personId);
        NewGmScenarioSnapshot next;
        IReadOnlyList<AlphaInboxItem> inbox;

        if (scenario.TrainingCamp is null)
        {
            var pending = new PendingGmActionService().CreatePendingAction(
                registry,
                scenario,
                PendingGmActionType.InviteToCamp,
                agent.PersonId,
                $"{agent.Name} is available for a tryout invite.",
                "Approve the camp invitation when camp opens.",
                agent.Position,
                PlayerAcquisitionSource.Tryout);
            next = pending.ScenarioSnapshot;
            inbox = pending.InboxItems;
        }
        else
        {
            var camp = new TrainingCampService().InvitePlayer(registry, scenario, agent.PersonId, agent.Position, TrainingCampInviteType.Tryout, PlayerAcquisitionSource.Tryout);
            next = camp.ScenarioSnapshot;
            inbox = camp.InboxItems;
        }

        var updated = agent with { Status = agent.Status == FreeAgentStatus.Available ? FreeAgentStatus.Negotiating : agent.Status };
        next = ReplaceAgent(next, updated);
        QueueEvent(registry, next, LegacyEventType.FreeAgentInvitedToCamp, $"Free agent invited to camp: {agent.Name}", $"{agent.Name} was invited to camp/tryout consideration.", agent.PersonId);
        return Result(true, next, updated, inbox.Append(Inbox(next, LegacyEventType.FreeAgentInvitedToCamp, "Free agent camp invite", $"{agent.Name} is in the camp invite workflow.", agent.PersonId)).ToArray(), Array.Empty<LeagueTransaction>(), $"{agent.Name} invited to camp workflow.");
    }

    public FreeAgentMarketResult WithdrawOffer(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId)
    {
        var agent = RequireAgent(scenario, personId);
        var updated = agent with { Status = FreeAgentStatus.Withdrawn };
        var next = ReplaceAgent(scenario, updated);
        QueueEvent(registry, next, LegacyEventType.FreeAgentOfferWithdrawn, $"Free agent offer withdrawn: {agent.Name}", $"{scenario.Organization.Name} withdrew its offer to {agent.Name}.", agent.PersonId);
        return Result(true, next, updated, new[] { Inbox(next, LegacyEventType.FreeAgentOfferWithdrawn, "Free agent offer withdrawn", $"{agent.Name}'s offer was withdrawn.", agent.PersonId) }, Array.Empty<LeagueTransaction>(), $"{agent.Name} offer withdrawn.");
    }

    public FreeAgentMarketResult RecordOtherTeamSigning(NewGmScenarioSnapshot scenario, string personId, string otherTeamName)
    {
        var agent = RequireAgent(scenario, personId);
        var updated = agent with { Status = FreeAgentStatus.Unavailable };
        var next = ReplaceAgent(scenario, updated);
        var transaction = new LeagueTransaction(
            $"league-free-agent:{Guid.NewGuid():N}",
            new DateTimeOffset(scenario.CurrentDate.Year, scenario.CurrentDate.Month, scenario.CurrentDate.Day, 12, 0, 0, TimeSpan.Zero),
            null,
            otherTeamName,
            agent.PersonId,
            agent.Name,
            LeagueTransactionType.PlayerSigned,
            LeagueNewsCategory.Signings,
            $"{otherTeamName} signed free agent {agent.Name}.");
        return Result(true, next, updated, Array.Empty<AlphaInboxItem>(), new[] { transaction }, $"{agent.Name} signed elsewhere.");
    }

    internal static NewGmScenarioSnapshot MarkSigned(NewGmScenarioSnapshot scenario, string personId)
    {
        return MarkStatus(scenario, personId, FreeAgentStatus.Signed);
    }

    internal static NewGmScenarioSnapshot MarkStatus(NewGmScenarioSnapshot scenario, string personId, FreeAgentStatus status)
    {
        var agent = scenario.FreeAgentMarket?.Find(personId);
        if (agent is null)
        {
            return scenario;
        }

        return ReplaceAgent(scenario, agent with { Status = status });
    }

    private static FreeAgent CreateFreeAgent(NewGmScenarioSnapshot scenario, Person person, int index)
    {
        var nhl = scenario.LeagueProfile.Experience == LeagueExperience.Nhl;
        var position = PositionFor(index);
        var seed = Math.Abs(HashCode.Combine(person.PersonId, index));
        var age = person.CalculateAge(scenario.CurrentDate);
        var previousTeam = PreviousTeam(index, nhl);
        var league = nhl ? "North American Pro Alpha" : index % 4 == 0 ? "Prairie Junior League" : index % 4 == 1 ? "CSSHL U18" : index % 4 == 2 ? "AEHL U18" : "Independent Junior";
        var stats = StatLineFor(person, position, scenario.Season.Year - 1, previousTeam, league, scenario.CurrentDate);
        var career = CareerFor(stats, nhl ? Math.Clamp(age - 18, 1, 18) : Math.Clamp(age - 15, 1, 5), age);
        var retirementRisk = nhl ? ExistingWorkforceGenerator.RetirementRiskFor(age, seed) : RetirementRisk.None;
        var tier = MarketTierFor(nhl, age, position, index, retirementRisk);
        var ask = nhl
            ? NhlAskFor(tier, age, index, retirementRisk)
            : new FreeAgentContractAsk(ContractType.JuniorPlayerAgreement, 1_100m + (index % 9 * 175m), "CAD", index % 7 == 0 ? 2 : 1, AskNotes(index));
        var interest = new FreeAgentInterest(
            Math.Clamp(38 + (index * 7 % 55), 15, 92),
            nhl && retirementRisk >= RetirementRisk.ConsideringRetirement ? "Several contenders have asked about a short-term veteran fit." : index % 5 == 0 ? "Two rival clubs have checked in." : index % 4 == 0 ? "One nearby team has mild interest." : "No strong competing offer yet.",
            nhl && retirementRisk >= RetirementRisk.ConsideringRetirement ? "Wants an NHL role, a competitive team, and clarity on whether this is a final contract." : index % 3 == 0 ? "Wants clear ice-time opportunity." : index % 3 == 1 ? "Values development and coaching fit." : "Looking for a stable role and quick decision.");
        var fit = new FreeAgentFitSummary(
            RosterNeedFor(position),
            $"Estimated impact {ask.AnnualAmount:C0}; {ask.Notes}",
            RecommendationFor(index, position),
            index % 6 == 0 ? "Moderate injury/availability risk." : "No major risk in current file.",
            Math.Clamp(45 + (index * 6 % 45), 30, 90));

        var agent = new FreeAgent(
            person.PersonId,
            person.Identity.DisplayName,
            position,
            position == RosterPosition.Goalie ? (seed % 2 == 0 ? "Catches L" : "Catches R") : (seed % 3 == 0 ? "Shoots R" : "Shoots L"),
            age,
            HeightFor(position, seed),
            WeightFor(position, seed),
            person.Identity.Nationality,
            person.Identity.Birthplace,
            previousTeam,
            stats,
            career,
            fit.RiskSummary,
            age <= 17 ? "Development runway remains useful." : age >= 20 ? "Mature player with limited development runway." : "Steady development profile.",
            PlayerTypeFor(position, index),
            RoleFor(position, index),
            ask,
            interest,
            "Unsigned free agent; eligible for junior agreement or tryout path where rulebook allows.",
            index < 6 ? ScoutingConfidenceLevel.Medium : ScoutingConfidenceLevel.Low,
            fit,
            FreeAgentStatus.Available,
            IsShortlisted: index is 0 or 3)
        {
            MarketTier = tier,
            CareerStage = StageFor(age, position),
            RetirementRisk = retirementRisk,
            FinalContractPreference = retirementRisk >= RetirementRisk.ConsideringRetirement
                ? new FinalContractPreference(true, true, true, "Veteran prefers one NHL season with a defined role and contender opportunity.")
                : null
        };
        agent.Validate();
        return agent;
    }

    private static PriorSeasonStatLine StatLineFor(Person person, RosterPosition position, int seasonYear, string teamName, string leagueName, DateOnly date)
    {
        var seed = Math.Abs(HashCode.Combine(person.PersonId, teamName, seasonYear));
        var games = 28 + seed % 26;
        if (position == RosterPosition.Goalie)
        {
            return new PriorSeasonStatLine(person.PersonId, person.Identity.DisplayName, seasonYear, teamName, leagueName, position, games, Wins: 8 + seed % 18, Losses: 5 + seed % 14, SavePercentage: 0.884m + (seed % 31) / 1000m, GoalsAgainstAverage: 2.65m + (seed % 80) / 100m);
        }

        var goals = Math.Max(1, seed % 19 + (position == RosterPosition.Defense ? -3 : 3));
        var assists = 5 + seed % 24 + (position == RosterPosition.Defense ? 6 : 0);
        return new PriorSeasonStatLine(person.PersonId, person.Identity.DisplayName, seasonYear, teamName, leagueName, position, games, goals, assists, (seed % 21) - 8, 6 + seed % 48);
    }

    private static CareerStatSummary CareerFor(PriorSeasonStatLine stat, int seasons, int age) =>
        new(stat.PersonId, stat.PlayerName, stat.Position, seasons, stat.GamesPlayed * seasons, stat.Goals * seasons, stat.Assists * seasons, stat.PenaltyMinutes * seasons, stat.Wins * seasons, stat.Losses * seasons, stat.IsGoalie ? Math.Max(0, seasons - 1) : 0, stat.LeagueName, stat.IsGoalie ? $"{age}-year-old goalie with {seasons} tracked season(s) and recent {stat.SavePercentage:0.000} save percentage." : $"{age}-year-old {stat.Position} with {seasons} tracked season(s) and recent {stat.Points} point season.");

    private static RosterPosition PositionFor(int index) =>
        index switch
        {
            0 or 7 => RosterPosition.Goalie,
            1 or 4 or 8 or 13 => RosterPosition.Defense,
            _ when index % 3 == 0 => RosterPosition.Center,
            _ when index % 3 == 1 => RosterPosition.LeftWing,
            _ => RosterPosition.RightWing
        };

    private static int HeightFor(RosterPosition position, int seed) =>
        position switch
        {
            RosterPosition.Goalie => 71 + seed % 9,
            RosterPosition.Defense => 70 + seed % 9,
            _ => 68 + seed % 8
        };

    private static int WeightFor(RosterPosition position, int seed) =>
        position switch
        {
            RosterPosition.Goalie => 175 + seed % 70,
            RosterPosition.Defense => 178 + seed % 58,
            _ => 162 + seed % 58
        };

    private static string PreviousTeam(int index, bool nhl) => nhl
        ? new[] { "Seattle Cascades", "Anaheim Orange", "Calgary Summit", "Toronto Monarchs", "New York Harbor", "Denver Peaks", "Boston Foundry", "Vancouver Orcas" }[index % 8]
        : new[] { "Swift Current U18", "Moose Jaw North", "Brandon Academy", "Regina Valley", "Saskatoon East", "Calgary Selects", "Winnipeg South", "Red Deer U18" }[index % 8];

    private static FreeAgentMarketTier MarketTierFor(bool nhl, int age, RosterPosition position, int index, RetirementRisk retirementRisk)
    {
        if (!nhl)
        {
            return index < 2 ? FreeAgentMarketTier.RolePlayer : FreeAgentMarketTier.CampInvite;
        }

        if (retirementRisk >= RetirementRisk.RetirementRisk) return FreeAgentMarketTier.RetirementRisk;
        if (index == 0) return FreeAgentMarketTier.ImpactFreeAgent;
        if (index < 4) return FreeAgentMarketTier.NhlRegular;
        if (age >= 33) return FreeAgentMarketTier.VeteranDepth;
        if (position == RosterPosition.Goalie && age >= 28) return FreeAgentMarketTier.RolePlayer;
        return age <= 24 ? FreeAgentMarketTier.DevelopmentPlayer : index % 4 == 0 ? FreeAgentMarketTier.AhlDepth : FreeAgentMarketTier.RolePlayer;
    }

    private static FreeAgentContractAsk NhlAskFor(FreeAgentMarketTier tier, int age, int index, RetirementRisk retirementRisk)
    {
        var amount = tier switch
        {
            FreeAgentMarketTier.ImpactFreeAgent => 6_750_000m,
            FreeAgentMarketTier.NhlRegular => 3_250_000m + index * 250_000m,
            FreeAgentMarketTier.RolePlayer => 1_400_000m + index % 4 * 225_000m,
            FreeAgentMarketTier.VeteranDepth or FreeAgentMarketTier.RetirementRisk => 900_000m + index % 3 * 175_000m,
            FreeAgentMarketTier.DevelopmentPlayer => 875_000m + index % 3 * 75_000m,
            _ => 775_000m
        };
        var years = retirementRisk >= RetirementRisk.ConsideringRetirement || age >= 33 ? 1 : tier is FreeAgentMarketTier.ImpactFreeAgent or FreeAgentMarketTier.NhlRegular ? 2 : 1;
        var notes = retirementRisk >= RetirementRisk.ConsideringRetirement
            ? "Seeking one NHL season, a defined role, and a contender opportunity."
            : tier == FreeAgentMarketTier.DevelopmentPlayer ? "Wants an NHL opportunity or a credible development path." : "Market ask reflects role, age, and expected opportunity.";
        return new FreeAgentContractAsk(ContractType.JuniorPlayerAgreement, amount, "USD", years, notes);
    }

    private static WorkforceCareerStage StageFor(int age, RosterPosition position) =>
        age <= 21 ? WorkforceCareerStage.Rookie :
        age <= 24 ? WorkforceCareerStage.YoungDeveloping :
        age <= 26 ? WorkforceCareerStage.EmergingNhlPlayer :
        age <= 29 ? WorkforceCareerStage.Prime :
        age <= 33 ? WorkforceCareerStage.EstablishedVeteran :
        age <= 36 ? WorkforceCareerStage.AgingVeteran :
        age <= (position == RosterPosition.Goalie ? 39 : 38) ? WorkforceCareerStage.LateCareerDepth : WorkforceCareerStage.NearRetirement;

    private static string RosterNeedFor(RosterPosition position) =>
        position switch
        {
            RosterPosition.Goalie => "Goalie depth and camp competition.",
            RosterPosition.Defense => "Defense depth and injury insurance.",
            RosterPosition.Center => "Center depth and faceoff coverage.",
            _ => "Wing depth and bottom-six competition."
        };

    private static string PlayerTypeFor(RosterPosition position, int index) =>
        position switch
        {
            RosterPosition.Goalie => "Goalie",
            RosterPosition.Defense => index % 2 == 0 ? "Mobile defense" : "Defensive defense",
            RosterPosition.Center => "Two-way center",
            _ => index % 2 == 0 ? "Energy winger" : "Scoring winger"
        };

    private static string RoleFor(RosterPosition position, int index) =>
        position switch
        {
            RosterPosition.Goalie => index % 2 == 0 ? "Backup goalie competition" : "Development goalie",
            RosterPosition.Defense => index % 2 == 0 ? "Third-pair defense" : "Depth defense",
            RosterPosition.Center => "Bottom-six center",
            _ => "Depth winger"
        };

    private static string RecommendationFor(int index, RosterPosition position) =>
        index switch
        {
            0 => "Staff recommend serious review; useful fit if budget allows.",
            1 => "Staff like the fit as a low-cost roster stabilizer.",
            _ when position == RosterPosition.Goalie => "Scout recommends camp invite before contract.",
            _ when index % 5 == 0 => "Poor fit unless roster injuries create a need.",
            _ => "Staff see this as a depth option, not a must-sign."
        };

    private static string AskNotes(int index) =>
        index % 6 == 0 ? "agent wants quick role clarity" : index % 4 == 0 ? "camp invite may be enough" : "standard junior agreement ask";

    private static string BudgetImpact(NewGmScenarioSnapshot scenario, FreeAgent agent) =>
        new BudgetOverviewService().Build(scenario, RulebookPresets.CreateJuniorMajor()).RemainingBudget - agent.ContractAsk.AnnualAmount < 0
            ? $"would put the club over budget by {Math.Abs(new BudgetOverviewService().Build(scenario, RulebookPresets.CreateJuniorMajor()).RemainingBudget - agent.ContractAsk.AnnualAmount):C0}"
            : $"would leave about {(new BudgetOverviewService().Build(scenario, RulebookPresets.CreateJuniorMajor()).RemainingBudget - agent.ContractAsk.AnnualAmount):C0}";

    private static bool WouldBeOverBudget(NewGmScenarioSnapshot scenario, FreeAgent agent) =>
        new BudgetOverviewService().Build(scenario, RulebookPresets.CreateJuniorMajor()).RemainingBudget - agent.ContractAsk.AnnualAmount < 0;

    private static FreeAgent RequireAgent(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.FreeAgentMarket?.Find(personId)
        ?? throw new ArgumentException("Free agent was not found.", nameof(personId));

    private static NewGmScenarioSnapshot ReplaceAgent(NewGmScenarioSnapshot scenario, FreeAgent freeAgent) =>
        scenario with { FreeAgentMarket = (scenario.FreeAgentMarket ?? throw new InvalidOperationException("Free agent market has not been generated.")).Replace(freeAgent) };

    private static AlphaInboxItem Inbox(NewGmScenarioSnapshot scenario, LegacyEventType eventType, string title, string summary, string personId, LegacyEventSeverity severity = LegacyEventSeverity.Notice) =>
        new($"inbox:free-agent:{Guid.NewGuid():N}", new DateTimeOffset(scenario.CurrentDate.Year, scenario.CurrentDate.Month, scenario.CurrentDate.Day, 15, 0, 0, TimeSpan.Zero), eventType, severity, title, summary, personId);

    private static void QueueEvent(EngineRegistry registry, NewGmScenarioSnapshot scenario, LegacyEventType eventType, string title, string description, string personId, LegacyEventSeverity severity = LegacyEventSeverity.Notice)
    {
        var legacyEvent = registry.EventEngine.CreateEvent(
            new DateTimeOffset(scenario.CurrentDate.Year, scenario.CurrentDate.Month, scenario.CurrentDate.Day, 15, 0, 0, TimeSpan.Zero),
            eventType,
            severity,
            LegacyEventVisibility.Organization,
            title,
            description,
            new LegacyEventContext(PrimaryPersonId: personId, OrganizationId: scenario.Organization.OrganizationId),
            new Dictionary<string, object?>
            {
                ["person_name"] = scenario.FreeAgentMarket?.Find(personId)?.Name,
                ["team_name"] = scenario.Organization.Name
            });
        registry.EventEngine.QueueEvent(legacyEvent);
    }

    private static FreeAgentMarketResult Result(bool success, NewGmScenarioSnapshot scenario, FreeAgent? freeAgent, IReadOnlyList<AlphaInboxItem> inboxItems, IReadOnlyList<LeagueTransaction> leagueTransactions, string message)
    {
        var result = new FreeAgentMarketResult(success, scenario, freeAgent, inboxItems, leagueTransactions, message);
        result.Validate();
        return result;
    }
}
