using LegacyEngine.Contracts;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;

namespace LegacyEngine.Integration;

/// <summary>
/// Builds the player-facing organization roster from contracts, rights, and
/// current assignments. It deliberately keeps active roster, contract count,
/// and prospect rights as separate concepts.
/// </summary>
public sealed class RosterAllocationService
{
    public NewGmScenarioSnapshot EnsureAllocation(NewGmScenarioSnapshot scenario, Rulebook? rulebook = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var allocation = BuildOrganizationRoster(scenario, rulebook ?? scenario.LeagueProfile.Rulebook);
        return scenario with { OrganizationRoster = allocation };
    }

    public OrganizationRoster BuildOrganizationRoster(NewGmScenarioSnapshot scenario, Rulebook? rulebook = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var rules = rulebook?.PlayerAssignmentRules;
        var contracts = SignedPlayerContracts(scenario);
        var roster = new List<OrganizationRosterPlayer>();
        var names = Names(scenario);

        foreach (var player in scenario.AlphaSnapshot.Roster.Players.Where(player => player.Status != RosterStatus.Released))
        {
            var contract = contracts.FirstOrDefault(item => item.PersonId == player.PersonId);
            var group = player.Status == RosterStatus.AssignedToAffiliate
                ? OrganizationRosterGroup.AhlAffiliateRoster
                : player.Status == RosterStatus.InjuredReserve
                    ? OrganizationRosterGroup.InjuredOrUnavailable
                    : scenario.LeagueProfile.Experience == LeagueExperience.Nhl
                        ? OrganizationRosterGroup.NhlActiveRoster
                        : OrganizationRosterGroup.OtherContracted;
            roster.Add(Player(
                scenario,
                player.PersonId,
                names.GetValueOrDefault(player.PersonId, player.PersonId),
                player.Position,
                player.Age ?? Age(scenario, player.PersonId),
                group,
                contract,
                group == OrganizationRosterGroup.AhlAffiliateRoster ? "AHL" : "NHL",
                group == OrganizationRosterGroup.AhlAffiliateRoster ? AffiliateName(scenario) : scenario.Organization.Name,
                false,
                rules));
        }

        foreach (var prospect in scenario.ProspectRights)
        {
            if (roster.Any(item => item.PersonId == prospect.ProspectPersonId))
            {
                continue;
            }

            var contract = contracts.FirstOrDefault(item => item.PersonId == prospect.ProspectPersonId);
            var group = prospect.Status switch
            {
                ProspectStatus.AssignedToAffiliate => OrganizationRosterGroup.AhlAffiliateRoster,
                ProspectStatus.ReturnedToJunior or ProspectStatus.ReturnedToYouthTeam when contract is not null => OrganizationRosterGroup.SignedJuniorReturn,
                ProspectStatus.Signed when contract is not null => OrganizationRosterGroup.OtherContracted,
                _ => OrganizationRosterGroup.UnsignedProspectRights
            };
            roster.Add(Player(
                scenario,
                prospect.ProspectPersonId,
                prospect.ProspectName,
                prospect.Position,
                prospect.Age,
                group,
                contract,
                group == OrganizationRosterGroup.AhlAffiliateRoster ? "AHL" : group == OrganizationRosterGroup.SignedJuniorReturn ? "Junior" : "Prospect Rights",
                group == OrganizationRosterGroup.AhlAffiliateRoster ? AffiliateName(scenario) : prospect.CurrentTeam,
                group == OrganizationRosterGroup.SignedJuniorReturn,
                rules));
        }

        // A signed player can be off the game-day roster and absent from the prospect list.
        foreach (var contract in contracts.Where(contract => roster.All(player => player.PersonId != contract.PersonId)))
        {
            roster.Add(Player(
                scenario,
                contract.PersonId,
                names.GetValueOrDefault(contract.PersonId, contract.PersonId),
                Position(scenario, contract.PersonId),
                Age(scenario, contract.PersonId),
                OrganizationRosterGroup.OtherContracted,
                contract,
                "Other Contracted",
                scenario.Organization.Name,
                false,
                rules));
        }

        var allocation = new OrganizationRoster(scenario.Organization.OrganizationId, roster.OrderBy(player => player.Group).ThenBy(player => player.PlayerName, StringComparer.Ordinal).ToArray());
        allocation.Validate();
        return allocation;
    }

    public OrganizationContractInventory BuildContractInventory(NewGmScenarioSnapshot scenario, Rulebook? rulebook = null)
    {
        // Rebuild from source state so waiver/transaction services cannot leave a stale
        // presentation allocation behind when called outside the desktop wrapper.
        var allocation = BuildOrganizationRoster(scenario, rulebook);
        var max = rulebook?.SalaryCapRules?.MaximumContracts ?? scenario.LeagueProfile.Rulebook?.SalaryCapRules?.MaximumContracts ?? 0;
        var contracted = allocation.Players.Where(player => player.CountsTowardContractLimit).ToArray();
        var inventory = new OrganizationContractInventory(
            max,
            contracted.Length,
            contracted.Count(player => player.Group == OrganizationRosterGroup.NhlActiveRoster),
            contracted.Count(player => player.Group == OrganizationRosterGroup.AhlAffiliateRoster),
            contracted.Count(player => player.Group is OrganizationRosterGroup.OtherContracted or OrganizationRosterGroup.InjuredOrUnavailable),
            contracted.Count(player => player.Group == OrganizationRosterGroup.SignedJuniorReturn),
            allocation.Players.Count(player => player.Group == OrganizationRosterGroup.SignedJuniorReturn && player.IsContractCountExempt),
            contracted);
        inventory.Validate();
        return inventory;
    }

    public RosterAllocationSummary BuildSummary(NewGmScenarioSnapshot scenario, Rulebook? rulebook = null)
    {
        var allocation = BuildOrganizationRoster(scenario, rulebook);
        var inventory = BuildContractInventory(scenario with { OrganizationRoster = allocation }, rulebook);
        var maxActive = rulebook?.RosterRules?.ActiveRoster ?? scenario.LeagueProfile.Rulebook?.RosterRules?.ActiveRoster ?? 0;
        var summary = new RosterAllocationSummary(
            allocation.In(OrganizationRosterGroup.NhlActiveRoster).Count,
            maxActive,
            allocation.In(OrganizationRosterGroup.AhlAffiliateRoster).Count,
            allocation.In(OrganizationRosterGroup.UnsignedProspectRights).Count,
            allocation.In(OrganizationRosterGroup.SignedJuniorReturn).Count,
            inventory,
            $"NHL {allocation.In(OrganizationRosterGroup.NhlActiveRoster).Count}/{maxActive}; AHL {allocation.In(OrganizationRosterGroup.AhlAffiliateRoster).Count}; contracts {inventory.ContractsUsed}/{inventory.MaximumContracts}; unsigned rights {allocation.In(OrganizationRosterGroup.UnsignedProspectRights).Count}; junior returns {allocation.In(OrganizationRosterGroup.SignedJuniorReturn).Count}.");
        summary.Validate();
        return summary;
    }

    private static OrganizationRosterPlayer Player(
        NewGmScenarioSnapshot scenario,
        string personId,
        string name,
        RosterPosition position,
        int age,
        OrganizationRosterGroup group,
        Contract? contract,
        string level,
        string? team,
        bool juniorReturn,
        PlayerAssignmentRules? rules)
    {
        var games = NhlGames(scenario, personId);
        var isElc = contract is not null && contract.ContractType == ContractType.JuniorPlayerAgreement;
        var slideEligible = isElc && rules?.ElcSlideEnabled == true && age <= rules.ElcSlideAgeCutoff;
        var belowThreshold = games < (rules?.ElcSlideNhlGameThreshold ?? 0);
        var exempt = juniorReturn && rules?.JuniorReturnContractCountExempt == true
            && games < rules.JuniorReturnContractCountGamesThreshold;
        var slidesUsed = scenario.ContractSlideHistory.Count(history => history.PersonId == personId && history.Status == EntryLevelSlideStatus.SlidePreserved);
        var canSlide = slideEligible && juniorReturn && belowThreshold && slidesUsed < (rules?.ElcSlideMaximumSeasons ?? 0);
        var slide = new EntryLevelSlideEligibility(
            personId,
            canSlide,
            juniorReturn,
            age,
            games,
            rules?.ElcSlideNhlGameThreshold ?? 0,
            slidesUsed,
            rules?.ElcSlideMaximumSeasons ?? 0,
            exempt,
            !isElc ? "Not an entry-level contract."
                : canSlide ? $"Eligible: {games}/{rules?.ElcSlideNhlGameThreshold} NHL games while returned to junior."
                : juniorReturn && slideEligible && !belowThreshold ? $"Contract year activated: {games}/{rules?.ElcSlideNhlGameThreshold} NHL games."
                : juniorReturn && slideEligible ? "Slide limit reached or rulebook does not permit another slide."
                : "Not eligible under current rulebook.");
        var activation = new ContractYearActivationStatus(
            slidesUsed,
            contract is null ? 0 : Math.Max(0, contract.Term.EndDate.Year - scenario.CurrentDate.Year),
            !canSlide,
            canSlide ? "Current entry-level year is tracking for a possible slide." : "Current contract year is active or not eligible to slide.");
        var countReason = contract is null
            ? "Unsigned prospect rights do not count toward the signed contract limit."
            : exempt
                ? "Signed junior return is exempt from the contract limit under the rulebook."
                : "Signed player counts toward the organization contract limit.";
        return new OrganizationRosterPlayer(
            personId,
            name,
            position,
            age,
            group,
            contract?.ContractId,
            level,
            team,
            contract is not null && !exempt,
            exempt,
            countReason,
            group == OrganizationRosterGroup.AhlAffiliateRoster && !string.IsNullOrWhiteSpace(scenario.Organization.AffiliateOrganizationId)
                ? new AffiliateAssignment(personId, scenario.Organization.AffiliateOrganizationId!, AffiliateName(scenario), scenario.CurrentDate, false, "Assigned to the configured AHL affiliate.")
                : null,
            slide,
            activation,
            new[] { $"{scenario.CurrentDate:yyyy-MM-dd}: {group}." });
    }

    private static IReadOnlyList<Contract> SignedPlayerContracts(NewGmScenarioSnapshot scenario) =>
        scenario.Contracts.Concat(scenario.AlphaSnapshot.Contracts)
            .GroupBy(contract => contract.ContractId, StringComparer.Ordinal)
            .Select(group => group.Last())
            .Where(contract => contract.OrganizationId == scenario.Organization.OrganizationId
                && contract.Status == ContractStatus.Signed
                && contract.ContractType == ContractType.JuniorPlayerAgreement)
            .ToArray();

    private static IReadOnlyDictionary<string, string> Names(NewGmScenarioSnapshot scenario) =>
        scenario.AlphaSnapshot.People.Concat(scenario.AlphaSnapshot.Players)
            .GroupBy(person => person.PersonId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Identity.DisplayName, StringComparer.Ordinal);

    private static int Age(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.Concat(scenario.AlphaSnapshot.Players)
            .FirstOrDefault(person => person.PersonId == personId)?.CalculateAge(scenario.CurrentDate)
        ?? scenario.ProspectRights.FirstOrDefault(prospect => prospect.ProspectPersonId == personId)?.Age
        ?? 0;

    private static RosterPosition Position(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.Roster.FindPlayer(personId)?.Position
        ?? scenario.ProspectRights.FirstOrDefault(prospect => prospect.ProspectPersonId == personId)?.Position
        ?? RosterPosition.Unknown;

    private static int NhlGames(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.PlayerStats.FirstOrDefault(stat => stat.PersonId == personId)?.GamesPlayed
        ?? scenario.GoalieStats.FirstOrDefault(stat => stat.PersonId == personId)?.GamesPlayed
        ?? 0;

    private static string AffiliateName(NewGmScenarioSnapshot scenario) =>
        scenario.LeagueProfile.Teams.FirstOrDefault(team => team.OrganizationId == scenario.Organization.AffiliateOrganizationId)?.TeamName
        ?? "AHL affiliate";
}
