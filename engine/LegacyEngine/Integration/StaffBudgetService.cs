using LegacyEngine.Contracts;
using LegacyEngine.RuleEngine;
using LegacyEngine.Staff;

namespace LegacyEngine.Integration;

public sealed class StaffBudgetService
{
    public IReadOnlyDictionary<StaffRole, StaffSalaryRange> SalaryRanges(Rulebook rulebook)
    {
        ArgumentNullException.ThrowIfNull(rulebook);
        var leagueType = rulebook.LeagueType.ToLowerInvariant();
        var ranges = leagueType switch
        {
            "ahl_style" => AhlRanges(),
            "nhl_style" => NhlRanges(),
            _ => JuniorRanges()
        };

        foreach (var range in ranges.Values)
        {
            range.Validate();
        }

        return ranges;
    }

    public StaffCompensationProfile CompensationFor(StaffMember member, NewGmScenarioSnapshot scenario, Rulebook rulebook)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(scenario);
        var range = RangeFor(member.CurrentRole, rulebook);
        var contractSalary = SignedContractFor(member.PersonId, scenario)?.Money.SalaryOrStipend;
        var isObligation = member.EmploymentStatus != StaffEmploymentStatus.Employed;
        var rawSalary = contractSalary ?? EstimateSalary(member.CurrentRole, member.Profile.Reputation, rulebook);
        var salary = isObligation
            ? EstimateReleaseObligation(member, scenario, rulebook)
            : Math.Clamp(rawSalary, range.Minimum, range.Maximum);

        var profile = new StaffCompensationProfile(
            member.PersonId,
            member.CurrentRole,
            CategoryFor(member.CurrentRole),
            new StaffSalary(salary),
            range,
            IsObligation: isObligation,
            Notes: isObligation
                ? "Remaining salary obligation after release."
                : contractSalary is null
                    ? "Estimated from league salary range and reputation."
                    : rawSalary == salary ? "Signed contract salary." : "Signed contract salary normalized to active league range.");
        profile.Validate();
        return profile;
    }

    public StaffSalary EstimateCandidateSalary(StaffCandidate candidate, Rulebook rulebook)
    {
        var amount = EstimateSalary(candidate.StaffMember.CurrentRole, candidate.Reputation, rulebook);
        var salary = new StaffSalary(amount);
        salary.Validate();
        return salary;
    }

    public StaffSalary EstimateSalaryForRole(StaffRole role, int reputation, Rulebook rulebook)
    {
        var salary = new StaffSalary(EstimateSalary(role, reputation, rulebook));
        salary.Validate();
        return salary;
    }

    public HockeyOperationsBudget Build(NewGmScenarioSnapshot scenario, Rulebook rulebook)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var staffProfiles = scenario.StaffMembers.Select(member => CompensationFor(member, scenario, rulebook)).ToArray();
        var gmSalary = SignedContractFor(scenario.AlphaSnapshot.GeneralManager.PersonId, scenario)?.Money.SalaryOrStipend
            ?? GmSalaryRange(rulebook).Midpoint;
        var coaching = staffProfiles.Where(profile => profile.Category == StaffBudgetCategory.Coaching && !profile.IsObligation).Sum(profile => profile.Salary.AnnualAmount);
        var scouting = staffProfiles.Where(profile => profile.Category == StaffBudgetCategory.Scouting && !profile.IsObligation).Sum(profile => profile.Salary.AnnualAmount);
        var medical = staffProfiles.Where(profile => profile.Category == StaffBudgetCategory.MedicalTraining && !profile.IsObligation).Sum(profile => profile.Salary.AnnualAmount);
        var operations = staffProfiles.Where(profile => profile.Category == StaffBudgetCategory.Operations && !profile.IsObligation).Sum(profile => profile.Salary.AnnualAmount);
        var executive = staffProfiles.Where(profile => profile.Category == StaffBudgetCategory.GeneralManager && !profile.IsObligation).Sum(profile => profile.Salary.AnnualAmount);
        var obligations = staffProfiles.Where(profile => profile.IsObligation).Sum(profile => profile.Salary.AnnualAmount);
        var playerContracts = SignedContracts(scenario)
            .Where(contract => contract.ContractType == ContractType.JuniorPlayerAgreement)
            .Sum(contract => contract.Money.SalaryOrStipend + contract.Money.SigningBonus);
        var staffTotal = gmSalary + executive + coaching + scouting + medical + operations;
        var used = staffTotal + obligations + playerContracts;
        var remaining = scenario.AlphaSnapshot.Owner.Budget.Total - used;
        var ratio = scenario.AlphaSnapshot.Owner.Budget.Total == 0 ? 1 : used / scenario.AlphaSnapshot.Owner.Budget.Total;
        var status = remaining < 0
            ? BudgetStatus.OverBudget
            : ratio >= 0.9m
                ? BudgetStatus.NearLimit
                : BudgetStatus.UnderBudget;
        var warnings = new List<string>();
        if (status == BudgetStatus.OverBudget)
        {
            warnings.Add("Owner warning: hockey operations spending is over budget.");
        }
        else if (status == BudgetStatus.NearLimit)
        {
            warnings.Add("Owner caution: hockey operations spending is nearing the approved budget.");
        }

        if (obligations > 0)
        {
            warnings.Add($"Released staff obligations remain on the budget: {obligations:C0}.");
        }

        var budget = new HockeyOperationsBudget(
            scenario.AlphaSnapshot.Owner.Budget.Total,
            gmSalary,
            coaching,
            scouting,
            medical,
            staffTotal,
            obligations,
            playerContracts,
            used,
            remaining,
            status,
            warnings);
        budget.Validate();
        return budget;
    }

    public decimal EstimateReleaseObligation(StaffMember member, NewGmScenarioSnapshot scenario, Rulebook rulebook)
    {
        var signed = SignedContractFor(member.PersonId, scenario);
        if (signed is null || signed.Status != ContractStatus.Signed || signed.Term.EndDate <= scenario.CurrentDate)
        {
            return 0m;
        }

        var totalDays = Math.Max(1, signed.Term.EndDate.DayNumber - signed.Term.StartDate.DayNumber);
        var remainingDays = Math.Max(0, signed.Term.EndDate.DayNumber - scenario.CurrentDate.DayNumber);
        return Math.Round(signed.Money.SalaryOrStipend * remainingDays / totalDays, 2);
    }

    public StaffSalaryRange RangeFor(StaffRole role, Rulebook rulebook)
    {
        var ranges = SalaryRanges(rulebook);
        return ranges.TryGetValue(role, out var range)
            ? range
            : ranges[StaffRole.AssistantCoach];
    }

    public StaffSalaryRange GmSalaryRange(Rulebook rulebook) =>
        rulebook.LeagueType.ToLowerInvariant() switch
        {
            "ahl_style" => new StaffSalaryRange(null, StaffBudgetCategory.GeneralManager, 100_000m, 300_000m),
            "nhl_style" => new StaffSalaryRange(null, StaffBudgetCategory.GeneralManager, 1_000_000m, 5_000_000m),
            _ => new StaffSalaryRange(null, StaffBudgetCategory.GeneralManager, 60_000m, 140_000m)
        };

    private static Contract? SignedContractFor(string personId, NewGmScenarioSnapshot scenario) =>
        SignedContracts(scenario).FirstOrDefault(contract => contract.PersonId == personId);

    private static IReadOnlyList<Contract> SignedContracts(NewGmScenarioSnapshot scenario) =>
        scenario.Contracts
            .Concat(scenario.AlphaSnapshot.Contracts)
            .Where(contract => contract.Status == ContractStatus.Signed)
            .GroupBy(contract => contract.ContractId, StringComparer.Ordinal)
            .Select(group => group.Last())
            .ToArray();

    private decimal EstimateSalary(StaffRole role, int reputation, Rulebook rulebook)
    {
        var range = RangeFor(role, rulebook);
        var reputationRatio = Math.Clamp(reputation, 0, 100) / 100m;
        return Math.Round(range.Minimum + ((range.Maximum - range.Minimum) * reputationRatio), 0);
    }

    private static StaffBudgetCategory CategoryFor(StaffRole role) =>
        role switch
        {
            StaffRole.GeneralManager or StaffRole.AssistantGM or StaffRole.DirectorOfHockeyOperations => StaffBudgetCategory.GeneralManager,
            StaffRole.HeadCoach or StaffRole.AssistantCoach or StaffRole.GoalieCoach or StaffRole.GoaltendingCoach or StaffRole.DevelopmentCoach or StaffRole.VideoCoach or StaffRole.SkillsCoach or StaffRole.StrengthCoach or StaffRole.StrengthConditioningCoach => StaffBudgetCategory.Coaching,
            StaffRole.HeadScout or StaffRole.DirectorOfScouting or StaffRole.Scout or StaffRole.RegionalScout or StaffRole.AmateurScout or StaffRole.ProfessionalScout or StaffRole.EuropeanScout or StaffRole.GoaltendingScout => StaffBudgetCategory.Scouting,
            StaffRole.HeadAthleticTherapist or StaffRole.AthleticTherapist or StaffRole.AssistantTrainer or StaffRole.TeamDoctor or StaffRole.Physiotherapist or StaffRole.MassageTherapist => StaffBudgetCategory.MedicalTraining,
            _ => StaffBudgetCategory.Operations
        };

    private static IReadOnlyDictionary<StaffRole, StaffSalaryRange> JuniorRanges() =>
        new Dictionary<StaffRole, StaffSalaryRange>
        {
            [StaffRole.GeneralManager] = Range(StaffRole.GeneralManager, StaffBudgetCategory.GeneralManager, 60_000m, 140_000m),
            [StaffRole.AssistantGM] = Range(StaffRole.AssistantGM, StaffBudgetCategory.GeneralManager, 60_000m, 140_000m),
            [StaffRole.DirectorOfHockeyOperations] = Range(StaffRole.DirectorOfHockeyOperations, StaffBudgetCategory.GeneralManager, 45_000m, 95_000m),
            [StaffRole.HeadCoach] = Range(StaffRole.HeadCoach, StaffBudgetCategory.Coaching, 50_000m, 130_000m),
            [StaffRole.AssistantCoach] = Range(StaffRole.AssistantCoach, StaffBudgetCategory.Coaching, 20_000m, 70_000m),
            [StaffRole.DevelopmentCoach] = Range(StaffRole.DevelopmentCoach, StaffBudgetCategory.Coaching, 25_000m, 80_000m),
            [StaffRole.GoalieCoach] = Range(StaffRole.GoalieCoach, StaffBudgetCategory.Coaching, 20_000m, 70_000m),
            [StaffRole.GoaltendingCoach] = Range(StaffRole.GoaltendingCoach, StaffBudgetCategory.Coaching, 20_000m, 70_000m),
            [StaffRole.SkillsCoach] = Range(StaffRole.SkillsCoach, StaffBudgetCategory.Coaching, 20_000m, 70_000m),
            [StaffRole.VideoCoach] = Range(StaffRole.VideoCoach, StaffBudgetCategory.Coaching, 20_000m, 60_000m),
            [StaffRole.StrengthCoach] = Range(StaffRole.StrengthCoach, StaffBudgetCategory.Coaching, 25_000m, 80_000m),
            [StaffRole.StrengthConditioningCoach] = Range(StaffRole.StrengthConditioningCoach, StaffBudgetCategory.Coaching, 25_000m, 80_000m),
            [StaffRole.HeadScout] = Range(StaffRole.HeadScout, StaffBudgetCategory.Scouting, 35_000m, 90_000m),
            [StaffRole.DirectorOfScouting] = Range(StaffRole.DirectorOfScouting, StaffBudgetCategory.Scouting, 35_000m, 90_000m),
            [StaffRole.Scout] = Range(StaffRole.Scout, StaffBudgetCategory.Scouting, 10_000m, 45_000m),
            [StaffRole.RegionalScout] = Range(StaffRole.RegionalScout, StaffBudgetCategory.Scouting, 10_000m, 45_000m),
            [StaffRole.AmateurScout] = Range(StaffRole.AmateurScout, StaffBudgetCategory.Scouting, 10_000m, 45_000m),
            [StaffRole.ProfessionalScout] = Range(StaffRole.ProfessionalScout, StaffBudgetCategory.Scouting, 15_000m, 50_000m),
            [StaffRole.EuropeanScout] = Range(StaffRole.EuropeanScout, StaffBudgetCategory.Scouting, 15_000m, 55_000m),
            [StaffRole.GoaltendingScout] = Range(StaffRole.GoaltendingScout, StaffBudgetCategory.Scouting, 12_000m, 48_000m),
            [StaffRole.HeadAthleticTherapist] = Range(StaffRole.HeadAthleticTherapist, StaffBudgetCategory.MedicalTraining, 30_000m, 90_000m),
            [StaffRole.AthleticTherapist] = Range(StaffRole.AthleticTherapist, StaffBudgetCategory.MedicalTraining, 30_000m, 90_000m),
            [StaffRole.AssistantTrainer] = Range(StaffRole.AssistantTrainer, StaffBudgetCategory.MedicalTraining, 25_000m, 70_000m),
            [StaffRole.TeamDoctor] = Range(StaffRole.TeamDoctor, StaffBudgetCategory.MedicalTraining, 30_000m, 90_000m),
            [StaffRole.Physiotherapist] = Range(StaffRole.Physiotherapist, StaffBudgetCategory.MedicalTraining, 30_000m, 90_000m),
            [StaffRole.MassageTherapist] = Range(StaffRole.MassageTherapist, StaffBudgetCategory.MedicalTraining, 20_000m, 60_000m),
            [StaffRole.HeadEquipmentManager] = Range(StaffRole.HeadEquipmentManager, StaffBudgetCategory.Operations, 25_000m, 70_000m),
            [StaffRole.EquipmentManager] = Range(StaffRole.EquipmentManager, StaffBudgetCategory.Operations, 25_000m, 70_000m),
            [StaffRole.AssistantEquipmentManager] = Range(StaffRole.AssistantEquipmentManager, StaffBudgetCategory.Operations, 18_000m, 45_000m)
        };

    private static IReadOnlyDictionary<StaffRole, StaffSalaryRange> AhlRanges() =>
        new Dictionary<StaffRole, StaffSalaryRange>
        {
            [StaffRole.GeneralManager] = Range(StaffRole.GeneralManager, StaffBudgetCategory.GeneralManager, 100_000m, 300_000m),
            [StaffRole.AssistantGM] = Range(StaffRole.AssistantGM, StaffBudgetCategory.GeneralManager, 100_000m, 300_000m),
            [StaffRole.DirectorOfHockeyOperations] = Range(StaffRole.DirectorOfHockeyOperations, StaffBudgetCategory.GeneralManager, 90_000m, 220_000m),
            [StaffRole.HeadCoach] = Range(StaffRole.HeadCoach, StaffBudgetCategory.Coaching, 125_000m, 400_000m),
            [StaffRole.AssistantCoach] = Range(StaffRole.AssistantCoach, StaffBudgetCategory.Coaching, 60_000m, 180_000m),
            [StaffRole.DevelopmentCoach] = Range(StaffRole.DevelopmentCoach, StaffBudgetCategory.Coaching, 60_000m, 180_000m),
            [StaffRole.GoalieCoach] = Range(StaffRole.GoalieCoach, StaffBudgetCategory.Coaching, 60_000m, 180_000m),
            [StaffRole.GoaltendingCoach] = Range(StaffRole.GoaltendingCoach, StaffBudgetCategory.Coaching, 60_000m, 180_000m),
            [StaffRole.SkillsCoach] = Range(StaffRole.SkillsCoach, StaffBudgetCategory.Coaching, 60_000m, 180_000m),
            [StaffRole.VideoCoach] = Range(StaffRole.VideoCoach, StaffBudgetCategory.Coaching, 45_000m, 140_000m),
            [StaffRole.StrengthCoach] = Range(StaffRole.StrengthCoach, StaffBudgetCategory.Coaching, 50_000m, 150_000m),
            [StaffRole.StrengthConditioningCoach] = Range(StaffRole.StrengthConditioningCoach, StaffBudgetCategory.Coaching, 50_000m, 150_000m),
            [StaffRole.HeadScout] = Range(StaffRole.HeadScout, StaffBudgetCategory.Scouting, 40_000m, 120_000m),
            [StaffRole.DirectorOfScouting] = Range(StaffRole.DirectorOfScouting, StaffBudgetCategory.Scouting, 40_000m, 120_000m),
            [StaffRole.Scout] = Range(StaffRole.Scout, StaffBudgetCategory.Scouting, 40_000m, 120_000m),
            [StaffRole.RegionalScout] = Range(StaffRole.RegionalScout, StaffBudgetCategory.Scouting, 40_000m, 120_000m),
            [StaffRole.AmateurScout] = Range(StaffRole.AmateurScout, StaffBudgetCategory.Scouting, 40_000m, 120_000m),
            [StaffRole.ProfessionalScout] = Range(StaffRole.ProfessionalScout, StaffBudgetCategory.Scouting, 45_000m, 130_000m),
            [StaffRole.EuropeanScout] = Range(StaffRole.EuropeanScout, StaffBudgetCategory.Scouting, 50_000m, 140_000m),
            [StaffRole.GoaltendingScout] = Range(StaffRole.GoaltendingScout, StaffBudgetCategory.Scouting, 40_000m, 120_000m),
            [StaffRole.HeadAthleticTherapist] = Range(StaffRole.HeadAthleticTherapist, StaffBudgetCategory.MedicalTraining, 50_000m, 150_000m),
            [StaffRole.AthleticTherapist] = Range(StaffRole.AthleticTherapist, StaffBudgetCategory.MedicalTraining, 50_000m, 150_000m),
            [StaffRole.AssistantTrainer] = Range(StaffRole.AssistantTrainer, StaffBudgetCategory.MedicalTraining, 45_000m, 125_000m),
            [StaffRole.TeamDoctor] = Range(StaffRole.TeamDoctor, StaffBudgetCategory.MedicalTraining, 50_000m, 150_000m),
            [StaffRole.Physiotherapist] = Range(StaffRole.Physiotherapist, StaffBudgetCategory.MedicalTraining, 50_000m, 150_000m),
            [StaffRole.MassageTherapist] = Range(StaffRole.MassageTherapist, StaffBudgetCategory.MedicalTraining, 45_000m, 120_000m),
            [StaffRole.HeadEquipmentManager] = Range(StaffRole.HeadEquipmentManager, StaffBudgetCategory.Operations, 45_000m, 110_000m),
            [StaffRole.EquipmentManager] = Range(StaffRole.EquipmentManager, StaffBudgetCategory.Operations, 45_000m, 110_000m),
            [StaffRole.AssistantEquipmentManager] = Range(StaffRole.AssistantEquipmentManager, StaffBudgetCategory.Operations, 35_000m, 80_000m)
        };

    private static IReadOnlyDictionary<StaffRole, StaffSalaryRange> NhlRanges() =>
        new Dictionary<StaffRole, StaffSalaryRange>
        {
            [StaffRole.GeneralManager] = Range(StaffRole.GeneralManager, StaffBudgetCategory.GeneralManager, 1_000_000m, 5_000_000m),
            [StaffRole.AssistantGM] = Range(StaffRole.AssistantGM, StaffBudgetCategory.GeneralManager, 250_000m, 900_000m),
            [StaffRole.DirectorOfHockeyOperations] = Range(StaffRole.DirectorOfHockeyOperations, StaffBudgetCategory.GeneralManager, 200_000m, 750_000m),
            [StaffRole.HeadCoach] = Range(StaffRole.HeadCoach, StaffBudgetCategory.Coaching, 1_000_000m, 5_000_000m),
            [StaffRole.AssistantCoach] = Range(StaffRole.AssistantCoach, StaffBudgetCategory.Coaching, 250_000m, 900_000m),
            [StaffRole.DevelopmentCoach] = Range(StaffRole.DevelopmentCoach, StaffBudgetCategory.Coaching, 250_000m, 900_000m),
            [StaffRole.GoalieCoach] = Range(StaffRole.GoalieCoach, StaffBudgetCategory.Coaching, 250_000m, 900_000m),
            [StaffRole.GoaltendingCoach] = Range(StaffRole.GoaltendingCoach, StaffBudgetCategory.Coaching, 250_000m, 900_000m),
            [StaffRole.SkillsCoach] = Range(StaffRole.SkillsCoach, StaffBudgetCategory.Coaching, 250_000m, 900_000m),
            [StaffRole.VideoCoach] = Range(StaffRole.VideoCoach, StaffBudgetCategory.Coaching, 150_000m, 500_000m),
            [StaffRole.StrengthCoach] = Range(StaffRole.StrengthCoach, StaffBudgetCategory.Coaching, 150_000m, 500_000m),
            [StaffRole.StrengthConditioningCoach] = Range(StaffRole.StrengthConditioningCoach, StaffBudgetCategory.Coaching, 150_000m, 500_000m),
            [StaffRole.HeadScout] = Range(StaffRole.HeadScout, StaffBudgetCategory.Scouting, 150_000m, 600_000m),
            [StaffRole.DirectorOfScouting] = Range(StaffRole.DirectorOfScouting, StaffBudgetCategory.Scouting, 150_000m, 600_000m),
            [StaffRole.Scout] = Range(StaffRole.Scout, StaffBudgetCategory.Scouting, 60_000m, 200_000m),
            [StaffRole.RegionalScout] = Range(StaffRole.RegionalScout, StaffBudgetCategory.Scouting, 60_000m, 200_000m),
            [StaffRole.AmateurScout] = Range(StaffRole.AmateurScout, StaffBudgetCategory.Scouting, 60_000m, 200_000m),
            [StaffRole.ProfessionalScout] = Range(StaffRole.ProfessionalScout, StaffBudgetCategory.Scouting, 75_000m, 240_000m),
            [StaffRole.EuropeanScout] = Range(StaffRole.EuropeanScout, StaffBudgetCategory.Scouting, 80_000m, 260_000m),
            [StaffRole.GoaltendingScout] = Range(StaffRole.GoaltendingScout, StaffBudgetCategory.Scouting, 60_000m, 200_000m),
            [StaffRole.HeadAthleticTherapist] = Range(StaffRole.HeadAthleticTherapist, StaffBudgetCategory.MedicalTraining, 80_000m, 300_000m),
            [StaffRole.AthleticTherapist] = Range(StaffRole.AthleticTherapist, StaffBudgetCategory.MedicalTraining, 80_000m, 300_000m),
            [StaffRole.AssistantTrainer] = Range(StaffRole.AssistantTrainer, StaffBudgetCategory.MedicalTraining, 70_000m, 220_000m),
            [StaffRole.TeamDoctor] = Range(StaffRole.TeamDoctor, StaffBudgetCategory.MedicalTraining, 80_000m, 300_000m),
            [StaffRole.Physiotherapist] = Range(StaffRole.Physiotherapist, StaffBudgetCategory.MedicalTraining, 80_000m, 300_000m),
            [StaffRole.MassageTherapist] = Range(StaffRole.MassageTherapist, StaffBudgetCategory.MedicalTraining, 60_000m, 180_000m),
            [StaffRole.HeadEquipmentManager] = Range(StaffRole.HeadEquipmentManager, StaffBudgetCategory.Operations, 80_000m, 200_000m),
            [StaffRole.EquipmentManager] = Range(StaffRole.EquipmentManager, StaffBudgetCategory.Operations, 80_000m, 200_000m),
            [StaffRole.AssistantEquipmentManager] = Range(StaffRole.AssistantEquipmentManager, StaffBudgetCategory.Operations, 50_000m, 140_000m)
        };

    private static StaffSalaryRange Range(StaffRole role, StaffBudgetCategory category, decimal minimum, decimal maximum) =>
        new(role, category, minimum, maximum);
}
