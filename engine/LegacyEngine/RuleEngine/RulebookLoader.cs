using System.Text.Json;

namespace LegacyEngine.RuleEngine;

public sealed class RulebookLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public Rulebook LoadFromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Rulebook path is required.", nameof(path));
        }

        return LoadFromJson(File.ReadAllText(path));
    }

    public Rulebook LoadFromJson(string json)
    {
        Rulebook? rulebook;
        try
        {
            rulebook = JsonSerializer.Deserialize<Rulebook>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new RulebookLoadException(
                RuleValidationResult.Failure(
                    RuleErrorCodes.InvalidRulebookField,
                    "Rulebook JSON could not be parsed.",
                    details: new Dictionary<string, object?> { ["json_error"] = ex.Message }),
                ex);
        }

        if (rulebook is null)
        {
            throw new RulebookLoadException(RuleValidationResult.Failure(
                RuleErrorCodes.UnknownRuleError,
                "Rulebook JSON produced no rulebook."));
        }

        var validation = Validate(rulebook);
        if (!validation.IsValid)
        {
            throw new RulebookLoadException(validation);
        }

        return rulebook;
    }

    public RuleValidationResult Validate(Rulebook rulebook)
    {
        if (string.IsNullOrWhiteSpace(rulebook.RulebookId))
        {
            return InvalidField("rulebook_id", "Rulebook id is required.");
        }

        if (string.IsNullOrWhiteSpace(rulebook.LeagueType))
        {
            return InvalidField("league_type", "League type is required.");
        }

        if (string.IsNullOrWhiteSpace(rulebook.Version))
        {
            return InvalidField("version", "Rulebook version is required.");
        }

        if (rulebook.RosterRules is null)
        {
            return MissingSection("roster_rules");
        }

        if (rulebook.EligibilityRules is null)
        {
            return MissingSection("eligibility_rules");
        }

        if (rulebook.ContractRules is null)
        {
            return MissingSection("contract_rules");
        }

        if (rulebook.DraftRules is null)
        {
            return MissingSection("draft_rules");
        }

        if (rulebook.PlayoffRules is null)
        {
            return MissingSection("playoff_rules");
        }

        if (rulebook.BudgetRules is null)
        {
            return MissingSection("budget_rules");
        }

        return ValidateFields(rulebook);
    }

    private static RuleValidationResult ValidateFields(Rulebook rulebook)
    {
        var roster = rulebook.RosterRules!;
        if (roster.MinRoster < 0 || roster.MaxRoster <= 0 || roster.MinRoster > roster.MaxRoster)
        {
            return InvalidField("roster_rules", "Roster minimum and maximum are invalid.");
        }

        if (roster.ActiveRoster <= 0 || roster.ActiveRoster > roster.MaxRoster)
        {
            return InvalidField("roster_rules.active_roster", "Active roster must be positive and no larger than max roster.");
        }

        if (roster.GoaliesRequired < 0 || roster.OverageSlots < 0 || roster.ImportSlots < 0)
        {
            return InvalidField("roster_rules", "Roster slot counts cannot be negative.");
        }

        var eligibility = rulebook.EligibilityRules!;
        if (eligibility.MinAge < 0 || eligibility.MaxAge < eligibility.MinAge)
        {
            return InvalidField("eligibility_rules", "Eligibility age range is invalid.");
        }

        var contracts = rulebook.ContractRules!;
        if (contracts.AllowedContractTypes.Count == 0)
        {
            return InvalidField("contract_rules.allowed_contract_types", "At least one contract type must be allowed.");
        }

        if (contracts.SalaryCapEnabled && contracts.SalaryCapAmount is null or < 0)
        {
            return InvalidField("contract_rules.salary_cap_amount", "Enabled salary caps require a non-negative amount.");
        }

        var draft = rulebook.DraftRules!;
        if (draft.DraftEnabled && draft.Rounds <= 0)
        {
            return InvalidField("draft_rules.rounds", "Enabled drafts require at least one round.");
        }

        if (draft.DraftEnabled
            && rulebook.LeagueType.Equals("junior", StringComparison.OrdinalIgnoreCase)
            && draft.Rounds is < 8 or > 15)
        {
            return InvalidField("draft_rules.rounds", "Junior Major drafts must be between 8 and 15 rounds.");
        }

        var playoff = rulebook.PlayoffRules!;
        if (playoff.TeamsQualify <= 0 || playoff.SeriesFormat.Count == 0 || playoff.SeriesFormat.Any(games => games <= 0))
        {
            return InvalidField("playoff_rules", "Playoff teams and series format must be positive.");
        }

        var budget = rulebook.BudgetRules!;
        if (budget.HardSalaryCapEnabled && budget.HardSalaryCapAmount is null or < 0)
        {
            return InvalidField("budget_rules.hard_salary_cap_amount", "Enabled hard salary caps require a non-negative amount.");
        }

        if (rulebook.StaffRules is { } staffRules)
        {
            foreach (var limit in staffRules.PositionLimits)
            {
                if (string.IsNullOrWhiteSpace(limit.Role)
                    || string.IsNullOrWhiteSpace(limit.Department)
                    || limit.Minimum < 0
                    || limit.Maximum < limit.Minimum)
                {
                    return InvalidField("staff_rules.position_limits", "Staff position limits require role, department, and valid min/max values.");
                }
            }
        }

        if (rulebook.AffiliateRules is { AffiliateEnabled: true } affiliate
            && affiliate.AllowedAcquisitionSources.Count == 0)
        {
            return InvalidField("affiliate_rules.allowed_acquisition_sources", "Enabled affiliate rules require at least one acquisition source.");
        }

        return RuleValidationResult.Valid("Rulebook is valid.");
    }

    private static RuleValidationResult MissingSection(string section) =>
        RuleValidationResult.Failure(
            RuleErrorCodes.MissingRulebookSection,
            $"Rulebook is missing required section '{section}'.",
            details: new Dictionary<string, object?> { ["section"] = section });

    private static RuleValidationResult InvalidField(string field, string message) =>
        RuleValidationResult.Failure(
            RuleErrorCodes.InvalidRulebookField,
            message,
            details: new Dictionary<string, object?> { ["field"] = field });
}

public sealed class RulebookLoadException : Exception
{
    public RulebookLoadException(RuleValidationResult result)
        : base(result.Message)
    {
        Result = result;
    }

    public RulebookLoadException(RuleValidationResult result, Exception innerException)
        : base(result.Message, innerException)
    {
        Result = result;
    }

    public RuleValidationResult Result { get; }
}
