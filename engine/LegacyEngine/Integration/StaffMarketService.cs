using LegacyEngine.Events;
using LegacyEngine.People;
using LegacyEngine.RuleEngine;
using LegacyEngine.Staff;

namespace LegacyEngine.Integration;

public sealed class StaffMarketService
{
    public NewGmScenarioSnapshot EnsureMarket(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        if (scenario.StaffMarket is not null)
        {
            return scenario;
        }

        var candidateScenario = scenario;
        if (candidateScenario.StaffCandidates.Count == 0)
        {
            candidateScenario = new StaffOfficeService().GenerateCandidatePool(registry, candidateScenario).ScenarioSnapshot;
        }

        var candidates = candidateScenario.StaffCandidates
            .Select((candidate, index) => ToMarketCandidate(candidate, candidateScenario, index))
            .ToArray();
        var market = new StaffMarket(
            $"staff-market:{candidateScenario.Season.SeasonId}",
            candidateScenario.CurrentDate,
            candidates,
            Array.Empty<StaffMovementRecord>());
        market.Validate();
        var updated = candidateScenario with { StaffMarket = market };
        updated.Validate();
        return updated;
    }

    public StaffMarketResult SimulateOtherTeamVacancyHire(EngineRegistry registry, NewGmScenarioSnapshot scenario, string organizationId, string teamName)
    {
        var current = EnsureMarket(registry, scenario);
        var market = current.StaffMarket!;
        var candidate = market.AvailableCandidates
            .Where(candidate => candidate.PersonId != current.GeneralManagerProfile.Person.PersonId)
            .OrderByDescending(candidate => candidate.Reputation + candidate.HiringInterest)
            .FirstOrDefault();
        if (candidate is null)
        {
            return Result(false, current, market, null, null, Array.Empty<LeagueTransaction>(), Array.Empty<AlphaInboxItem>(), "No market candidate is available for another team's vacancy.");
        }

        var moved = candidate with
        {
            Status = StaffMarketStatus.Employed,
            CurrentEmployerOrganizationId = organizationId,
            CurrentEmployer = teamName,
            AvailabilitySummary = $"{candidate.Name} accepted a staff role with {teamName}."
        };
        var movement = Movement(current, moved, null, null, organizationId, teamName, StaffMarketStatus.Employed, $"{teamName} hired {candidate.Name} as {StaffRoles.Title(candidate.DesiredRole)}.");
        var updatedMarket = market.Replace(moved).AddMovement(movement);
        var transaction = Transaction(current, organizationId, teamName, moved.PersonId, moved.Name, LeagueTransactionType.StaffHired, movement.Summary);
        var updated = current with
        {
            StaffMarket = updatedMarket,
            StaffMovementHistory = current.StaffMovementHistory.Append(movement).ToArray()
        };
        updated.Validate();
        return Result(true, updated, updatedMarket, moved, movement, new[] { transaction }, Array.Empty<AlphaInboxItem>(), movement.Summary);
    }

    public StaffMarketCandidate CandidateReturnedToMarket(
        NewGmScenarioSnapshot scenario,
        Person person,
        StaffMember staffMember,
        StaffSalary salary,
        string reason)
    {
        var candidate = new StaffCandidate(
            $"candidate-released:{person.PersonId}",
            person,
            staffMember,
            Math.Clamp(staffMember.Profile.Reputation + 6, 0, 100),
            Math.Clamp(staffMember.Profile.Reputation + 2, 0, 100),
            staffMember.Profile.Reputation,
            salary,
            new[] { "league experience", "known staff habits" },
            new[] { "recent release requires fit review" },
            "Known staff member returning to the market after a release.",
            "Medium risk; recent release may affect trust and role expectations.",
            "Worth reviewing if the role and salary fit.",
            "Available",
            staffMember.Profile.YearsExperience);

        return new StaffMarketCandidate(
            $"market:{candidate.CandidateId}",
            candidate,
            StaffMarketStatus.Available,
            null,
            "Available",
            new[] { staffMember.CurrentRole },
            Math.Clamp(55 + staffMember.Profile.Reputation / 3, 0, 100),
            StaffMarketReasonAvailable.ReleasedByTeam,
            new[] { $"{scenario.CurrentDate:yyyy-MM-dd}: Released by {scenario.Organization.Name}.", reason },
            reason);
    }

    public static StaffMarketCandidate ToMarketCandidate(StaffCandidate candidate, NewGmScenarioSnapshot scenario, int index)
    {
        var employed = !string.Equals(candidate.CurrentEmployer, "Available", StringComparison.OrdinalIgnoreCase)
            && !candidate.CurrentEmployer.Contains("consultant", StringComparison.OrdinalIgnoreCase)
            && !candidate.CurrentEmployer.Contains("clinic", StringComparison.OrdinalIgnoreCase)
            && !candidate.CurrentEmployer.Contains("equipment room", StringComparison.OrdinalIgnoreCase);
        var status = employed
            ? (index % 2 == 0 ? StaffMarketStatus.Interested : StaffMarketStatus.Employed)
            : StaffMarketStatus.Available;
        var reason = status == StaffMarketStatus.Available
            ? StaffMarketReasonAvailable.Unemployed
            : index % 3 == 0 ? StaffMarketReasonAvailable.SeekingPromotion : StaffMarketReasonAvailable.WantsBiggerRole;
        var marketCandidate = new StaffMarketCandidate(
            $"market:{candidate.CandidateId}",
            candidate,
            status,
            status == StaffMarketStatus.Available ? null : $"org-market-employer-{index + 1:000}",
            status == StaffMarketStatus.Available ? "Available" : candidate.CurrentEmployer,
            AcceptableRoles(candidate.StaffMember.CurrentRole),
            Math.Clamp(42 + candidate.RoleFit / 2 + (status == StaffMarketStatus.Interested ? 12 : 0), 0, 100),
            reason,
            CareerHistoryFor(candidate, scenario),
            AvailabilitySummary(reason, status, candidate.CurrentEmployer));
        marketCandidate.Validate();
        return marketCandidate;
    }

    public static StaffMarketCandidate MarkHired(StaffMarketCandidate candidate, NewGmScenarioSnapshot scenario) =>
        candidate with
        {
            Status = StaffMarketStatus.Hired,
            CurrentEmployerOrganizationId = scenario.Organization.OrganizationId,
            CurrentEmployer = scenario.Organization.Name,
            AvailabilitySummary = $"{candidate.Name} was hired by {scenario.Organization.Name}."
        };

    public static StaffMovementRecord Movement(
        NewGmScenarioSnapshot scenario,
        StaffMarketCandidate candidate,
        string? fromOrganizationId,
        string? fromTeamName,
        string? toOrganizationId,
        string? toTeamName,
        StaffMarketStatus status,
        string summary) =>
        new(
            $"staff-move:{Guid.NewGuid():N}",
            scenario.CurrentDate,
            candidate.PersonId,
            candidate.Name,
            candidate.DesiredRole,
            fromOrganizationId,
            fromTeamName,
            toOrganizationId,
            toTeamName,
            status,
            summary);

    public static LeagueTransaction Transaction(
        NewGmScenarioSnapshot scenario,
        string? organizationId,
        string teamName,
        string personId,
        string personName,
        LeagueTransactionType type,
        string description) =>
        new(
            $"transaction:staff:{Guid.NewGuid():N}",
            new DateTimeOffset(scenario.CurrentDate.Year, scenario.CurrentDate.Month, scenario.CurrentDate.Day, 11, 0, 0, TimeSpan.Zero),
            organizationId,
            teamName,
            personId,
            personName,
            type,
            LeagueNewsCategory.Staff,
            description);

    private static IReadOnlyList<StaffRole> AcceptableRoles(StaffRole desired) =>
        desired switch
        {
            StaffRole.HeadCoach => new[] { StaffRole.HeadCoach, StaffRole.AssistantCoach, StaffRole.DevelopmentCoach },
            StaffRole.HeadScout => new[] { StaffRole.HeadScout, StaffRole.Scout },
            StaffRole.TeamDoctor or StaffRole.HeadAthleticTherapist => new[] { desired, StaffRole.Physiotherapist },
            StaffRole.AssistantGM => new[] { StaffRole.AssistantGM, StaffRole.DirectorOfHockeyOperations },
            _ => new[] { desired }
        };

    private static IReadOnlyList<string> CareerHistoryFor(StaffCandidate candidate, NewGmScenarioSnapshot scenario) =>
        new[]
        {
            $"{Math.Max(1, candidate.YearsExperience)} year(s) staff experience.",
            $"{scenario.CurrentDate.Year - 1}: Worked as {StaffRoles.Title(candidate.StaffMember.CurrentRole)} candidate/emerging staff.",
            $"Current market note: {candidate.HiringRecommendation}"
        };

    private static string AvailabilitySummary(StaffMarketReasonAvailable reason, StaffMarketStatus status, string employer) =>
        status == StaffMarketStatus.Available
            ? $"Available: {Readable(reason)}."
            : $"{employer}: {Readable(reason)}; interest should be approached carefully.";

    private static string Readable(StaffMarketReasonAvailable reason) =>
        reason switch
        {
            StaffMarketReasonAvailable.ContractExpired => "contract expired",
            StaffMarketReasonAvailable.ReleasedByTeam => "released by team",
            StaffMarketReasonAvailable.SeekingPromotion => "seeking promotion",
            StaffMarketReasonAvailable.PoorFitWithCurrentTeam => "poor fit with current team",
            StaffMarketReasonAvailable.WantsHigherSalary => "wants higher salary",
            StaffMarketReasonAvailable.WantsBiggerRole => "wants bigger role",
            StaffMarketReasonAvailable.RetiredFormerPlayer => "retired/former player placeholder",
            StaffMarketReasonAvailable.PromotedInternalCandidate => "promoted internal candidate placeholder",
            _ => "unemployed"
        };

    private static StaffMarketResult Result(
        bool success,
        NewGmScenarioSnapshot scenario,
        StaffMarket? market,
        StaffMarketCandidate? candidate,
        StaffMovementRecord? movement,
        IReadOnlyList<LeagueTransaction> transactions,
        IReadOnlyList<AlphaInboxItem> inbox,
        string message)
    {
        var result = new StaffMarketResult(success, scenario, market, candidate, movement, transactions, inbox, message);
        result.Validate();
        return result;
    }
}
