using LegacyEngine.Organizations;

namespace LegacyEngine.Integration;

public sealed class ContractTeamFitService
{
    public ContractTeamPreference BuildPreference(NewGmScenarioSnapshot scenario, ContractAsk ask)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(ask);

        var freeAgent = scenario.FreeAgentMarket?.Find(ask.PersonId);
        var hometown = HometownFor(scenario, ask.PersonId, freeAgent?.Hometown);
        var prefersContender = freeAgent?.FinalContractPreference?.PrefersContender == true
            || ContainsAny(freeAgent?.Interest.MotivationSummary, "contender", "winning", "championship");
        var valuesStaff = ContainsAny(
            freeAgent?.Interest.MotivationSummary,
            "staff",
            "coach",
            "coaching",
            "development")
            || ask.AskType is ContractAskType.Prospect or ContractAskType.Recruit;
        var prefersHometown = hometown >= 75;

        var winningImportance = prefersContender ? 90 : freeAgent is not null ? 55 : 35;
        var staffImportance = valuesStaff ? 80 : 35;
        var hometownImportance = prefersHometown ? 85 : 20;
        var minimumTeamFit = prefersContender ? 62 : valuesStaff ? 48 : 35;
        var discount = prefersHometown && valuesStaff ? 8m : prefersHometown ? 5m : 0m;
        var summary = BuildPreferenceSummary(prefersContender, prefersHometown, valuesStaff, discount);

        var preference = new ContractTeamPreference(
            winningImportance,
            staffImportance,
            hometownImportance,
            minimumTeamFit,
            discount,
            prefersContender,
            summary);
        preference.Validate();
        return preference;
    }

    public ContractTeamFitEvaluation Evaluate(NewGmScenarioSnapshot scenario, ContractAsk ask)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(ask);

        var preference = ask.TeamPreference;
        var winning = WinningScore(scenario);
        var staff = StaffScore(scenario, ask.PersonId);
        var relationship = RelationshipScore(scenario, ask.PersonId);
        var hometown = HometownFor(scenario, ask.PersonId, scenario.FreeAgentMarket?.Find(ask.PersonId)?.Hometown);
        var organization = Math.Clamp(scenario.Organization?.Reputation.League ?? 50, 0, 100);
        var denominator = preference.WinningImportance
            + preference.StaffFitImportance
            + preference.HometownImportance
            + 35;
        var score = denominator == 0
            ? 50
            : (winning * preference.WinningImportance
                + staff * preference.StaffFitImportance
                + hometown * preference.HometownImportance
                + relationship * 20
                + organization * 15) / Math.Max(1, denominator);
        var teamFit = Math.Clamp(score, 0, 100);
        var discountApplied = preference.MaximumHometownDiscountPercent > 0m
            && hometown >= 75
            && winning >= 55
            && staff >= 55;
        var discount = discountApplied ? preference.MaximumHometownDiscountPercent : 0m;
        var label = LabelFor(teamFit);
        var summary = BuildSummary(label, winning, staff, hometown, relationship, discountApplied, discount, preference);
        var risk = teamFit < preference.MinimumTeamFit
            ? $"Team-fit risk: {ask.PersonName} may reject this organization below a {preference.MinimumTeamFit}/100 fit unless the money or role clearly compensates."
            : preference.PrefersContender && winning < 60
                ? "Competitive risk: the player prefers a contender and may compare this offer against stronger teams."
                : "No major team-fit rejection risk is visible.";

        var evaluation = new ContractTeamFitEvaluation(
            teamFit,
            winning,
            staff,
            hometown,
            relationship,
            discountApplied,
            discount,
            label,
            summary,
            risk);
        evaluation.Validate();
        return evaluation;
    }

    public decimal EffectiveSalaryTarget(ContractAsk ask, ContractTeamFitEvaluation fit) =>
        fit.HometownDiscountApplied
            ? Math.Round(ask.RequestedSalary * (1m - fit.AcceptedSalaryDiscountPercent / 100m), 0)
            : ask.RequestedSalary;

    private static int WinningScore(NewGmScenarioSnapshot scenario)
    {
        var standing = scenario.Standings?.Teams.FirstOrDefault(team => team.OrganizationId == scenario.Organization.OrganizationId);
        if (standing is not null && standing.GamesPlayed > 0)
        {
            return Math.Clamp(35 + (int)Math.Round((decimal)standing.Points / Math.Max(1, standing.GamesPlayed * 2) * 65), 0, 100);
        }

        return scenario.CurrentOrganizationPlan?.Window switch
        {
            CompetitiveWindow.Rebuild => 30,
            CompetitiveWindow.Developing => 45,
            CompetitiveWindow.Competing => 62,
            CompetitiveWindow.Contending => 78,
            CompetitiveWindow.AllIn => 90,
            CompetitiveWindow.Declining => 42,
            _ => 50
        };
    }

    private static int StaffScore(NewGmScenarioSnapshot scenario, string personId)
    {
        try
        {
            var fit = new StaffCoachingService().EvaluatePlayerFit(scenario, personId);
            return fit.FitGrade switch
            {
                CoachPlayerFitGrade.Excellent => 90,
                CoachPlayerFitGrade.Good => 75,
                CoachPlayerFitGrade.Average => 55,
                CoachPlayerFitGrade.Poor => 35,
                CoachPlayerFitGrade.Terrible => 20,
                _ => 50
            };
        }
        catch (InvalidOperationException)
        {
            return 50;
        }
    }

    private static int RelationshipScore(NewGmScenarioSnapshot scenario, string personId)
    {
        var gmId = scenario.AlphaSnapshot.GeneralManager.PersonId;
        var coachId = scenario.AlphaSnapshot.CoachPerson?.PersonId;
        var scores = scenario.AlphaSnapshot.Relationships
            .Where(relationship => relationship.ToPersonId == personId
                && (relationship.FromPersonId == gmId || relationship.FromPersonId == coachId))
            .Select(relationship => (relationship.Trust + relationship.Respect + relationship.Confidence + relationship.Loyalty) / 4)
            .ToArray();
        return scores.Length == 0 ? 50 : Math.Clamp((int)Math.Round(scores.Average()), 0, 100);
    }

    private static int HometownFor(NewGmScenarioSnapshot scenario, string personId, string? freeAgentHometown)
    {
        var hometown = freeAgentHometown;
        if (string.IsNullOrWhiteSpace(hometown))
        {
            hometown = scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.Identity.Birthplace;
        }

        var organization = scenario.Organization?.Identity;
        if (string.IsNullOrWhiteSpace(hometown) || organization is null)
        {
            return 0;
        }

        if (ContainsAny(hometown, organization.City, organization.Region, organization.Country))
        {
            return 100;
        }

        return scenario.FreeAgentMarket?.Find(personId)?.PreviousTeam.Contains(organization.Name, StringComparison.OrdinalIgnoreCase) == true ? 90 : 25;
    }

    private static string BuildPreferenceSummary(bool contender, bool hometown, bool staff, decimal discount) =>
        contender
            ? "Player wants a credible contender and may reject a weak competitive fit."
            : hometown && staff
                ? $"Player values home, staff trust, and a clear role; may accept up to {discount:0.#}% less to stay in the right environment."
                : hometown
                    ? $"Player has a hometown connection and may accept up to {discount:0.#}% less for the right long-term fit."
                    : staff
                        ? "Player places meaningful weight on coaching, development, and staff trust."
                        : "Money and role remain the primary contract priorities.";

    private static string BuildSummary(string label, int winning, int staff, int hometown, int relationship, bool discountApplied, decimal discount, ContractTeamPreference preference) =>
        $"{label} team fit: winning {winning}/100, staff {staff}/100, hometown {hometown}/100, relationship {relationship}/100. "
        + (discountApplied
            ? $"A hometown/staff discount of up to {discount:0.#}% may be available."
            : preference.PrefersContender
                ? "The player is likely to compare this offer with stronger teams."
                : "No hometown discount is currently applied.");

    private static string LabelFor(int score) => score switch
    {
        >= 80 => "Excellent",
        >= 65 => "Good",
        >= 50 => "Neutral",
        >= 35 => "Poor",
        _ => "Problem"
    };

    private static bool ContainsAny(string? value, params string[] needles) =>
        !string.IsNullOrWhiteSpace(value)
        && needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));
}
