using LegacyEngine.Contracts;
using LegacyEngine.People;

namespace LegacyEngine.Integration;

public sealed class AgentEngine
{
    private static readonly AgentProfile[] SeedProfiles =
    {
        new("Marc Deslauriers", 48, "Canada", "Prairie Player Counsel", 18, AgentPersonality.RelationshipBuilder),
        new("Andrea Kovac", 42, "Czechia", "Kovac Hockey Group", 13, AgentPersonality.HardNegotiator),
        new("Ryan McKenna", 39, "USA", "Northline Sports", 10, AgentPersonality.MoneyFirst),
        new("Sofia Lindholm", 45, "Sweden", "Lindholm Representation", 16, AgentPersonality.PlayerDevelopment),
        new("Pavel Novak", 51, "Slovakia", "Central Ice Advisors", 22, AgentPersonality.Patient),
        new("Elliot Fraser", 36, "Canada", "Fraser Family Hockey", 8, AgentPersonality.EasyGoing),
        new("Mika Salonen", 44, "Finland", "Nordic Pathway Agency", 15, AgentPersonality.WinNow),
        new("Claire Bennett", 40, "Canada", "Bennett Sports Management", 12, AgentPersonality.Loyal)
    };

    public NewGmScenarioSnapshot EnsureAgents(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var candidates = CollectRepresentablePeople(scenario);
        if (candidates.Count == 0)
        {
            return scenario;
        }

        var existing = scenario.AgentRepresentations.ToDictionary(record => record.PersonId, StringComparer.Ordinal);
        var representations = candidates
            .Select((candidate, index) => existing.TryGetValue(candidate.PersonId, out var found)
                ? found
                : CreateRepresentation(scenario, candidate, index))
            .GroupBy(record => record.PersonId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();

        var agents = SeedProfiles
            .Select((profile, index) => BuildAgent(scenario, profile, index, representations))
            .ToArray();
        var agentIds = agents.Select(agent => agent.AgentId).ToHashSet(StringComparer.Ordinal);
        representations = representations
            .Select(record => record.AgentId is not null && !agentIds.Contains(record.AgentId)
                ? record with { AgentId = AgentIdFor(Math.Abs(StableHash(record.PersonId)) % SeedProfiles.Length) }
                : record)
            .ToArray();
        agents = SeedProfiles
            .Select((profile, index) => BuildAgent(scenario, profile, index, representations))
            .ToArray();

        var history = scenario.AgentHistory.Count == 0
            ? agents.SelectMany(agent => BuildInitialHistory(scenario, agent)).ToArray()
            : scenario.AgentHistory;

        return scenario with
        {
            Agents = agents,
            AgentRepresentations = representations,
            AgentHistory = history
        };
    }

    public AgentNegotiationReview ReviewOffer(
        NewGmScenarioSnapshot scenario,
        ContractAsk ask,
        ContractOfferBuildRequest request,
        int baseScore)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(ask);
        ArgumentNullException.ThrowIfNull(request);

        var representation = FindRepresentation(scenario, ask.PersonId);
        var agent = representation?.AgentId is null ? null : FindAgent(scenario, representation.AgentId);
        var style = agent?.NegotiationStyle ?? StyleForRepresentation(representation?.RepresentationType ?? PlayerRepresentationType.NoAgent);
        var relationship = agent?.GmRelationship.Score ?? 50;
        var salaryGap = ask.RequestedSalary <= 0 ? 0 : (request.AnnualSalary - ask.RequestedSalary) / ask.RequestedSalary;
        var termGap = request.TermYears - ask.RequestedTermYears;
        var modifier = StyleModifier(style, salaryGap, termGap, relationship, ask, request);
        var likelihood = Math.Clamp(baseScore + modifier, 0, 100);
        var decision = likelihood switch
        {
            >= 75 => AgentNegotiationDecision.Accepted,
            >= 58 => AgentNegotiationDecision.CounterSuggestion,
            >= 42 => AgentNegotiationDecision.NeedsImprovement,
            _ => AgentNegotiationDecision.Rejected
        };

        if (style is AgentNegotiationStyle.Patient && decision != AgentNegotiationDecision.Rejected && request.AskType == ContractAskType.FreeAgent)
        {
            decision = AgentNegotiationDecision.Waiting;
        }

        var concern = BiggestConcern(ask, request, salaryGap, termGap, style);
        var improvement = RequestedImprovement(ask, request, salaryGap, termGap, style);
        var agentName = agent?.Name ?? RepresentationLabel(representation?.RepresentationType ?? PlayerRepresentationType.NoAgent);
        var agency = agent?.Profile.AgencyName ?? "No formal agency";
        var opinion = OpinionFor(agentName, ask.PersonName, decision, style, concern);
        var review = new AgentNegotiationReview(
            representation?.AgentId,
            agentName,
            agency,
            representation?.RepresentationType ?? PlayerRepresentationType.NoAgent,
            style,
            decision,
            modifier,
            likelihood,
            opinion,
            concern,
            improvement,
            RiskFor(style, relationship, request.AskType),
            CounterFor(ask, request, improvement),
            ResponseDelayFor(style, decision, request.AskType));
        review.Validate();
        return review;
    }

    public Agent? FindAgentForPerson(NewGmScenarioSnapshot scenario, string personId)
    {
        var representation = FindRepresentation(scenario, personId);
        return representation?.AgentId is null ? null : FindAgent(scenario, representation.AgentId);
    }

    public AgentRepresentationRecord? FindRepresentation(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AgentRepresentations.FirstOrDefault(record => record.PersonId == personId);

    public Agent? FindAgent(NewGmScenarioSnapshot scenario, string agentId) =>
        scenario.Agents.FirstOrDefault(agent => agent.AgentId == agentId);

    private static IReadOnlyList<(string PersonId, string Name, int Age, bool IsProspect, bool IsFreeAgent)> CollectRepresentablePeople(NewGmScenarioSnapshot scenario)
    {
        var people = scenario.AlphaSnapshot.People
            .Concat(scenario.AlphaSnapshot.Players)
            .GroupBy(person => person.PersonId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var ids = scenario.AlphaSnapshot.Roster.Players.Select(player => player.PersonId)
            .Concat(scenario.AlphaSnapshot.Recruits.Select(recruit => recruit.RecruitPersonId))
            .Concat(scenario.AlphaSnapshot.DraftBoard.Entries.Select(entry => entry.ProspectPersonId))
            .Concat(scenario.ProspectRights.Select(prospect => prospect.ProspectPersonId))
            .Concat(scenario.FreeAgentMarket?.FreeAgents.Select(agent => agent.PersonId) ?? Array.Empty<string>())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return ids.Select(id =>
            {
                var name = people.TryGetValue(id, out var person)
                    ? person.Identity.DisplayName
                    : scenario.FreeAgentMarket?.Find(id)?.Name
                        ?? scenario.ProspectRights.FirstOrDefault(prospect => prospect.ProspectPersonId == id)?.ProspectName
                        ?? id;
                var age = people.TryGetValue(id, out person)
                    ? person.CalculateAge(scenario.CurrentDate)
                    : scenario.FreeAgentMarket?.Find(id)?.Age ?? 18;
                var isProspect = scenario.AlphaSnapshot.Recruits.Any(recruit => recruit.RecruitPersonId == id)
                    || scenario.AlphaSnapshot.DraftBoard.Entries.Any(entry => entry.ProspectPersonId == id)
                    || scenario.ProspectRights.Any(prospect => prospect.ProspectPersonId == id);
                var isFreeAgent = scenario.FreeAgentMarket?.Find(id) is not null;
                return (PersonId: id, Name: name, Age: age, IsProspect: isProspect, IsFreeAgent: isFreeAgent);
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .ToArray();
    }

    private static AgentRepresentationRecord CreateRepresentation(
        NewGmScenarioSnapshot scenario,
        (string PersonId, string Name, int Age, bool IsProspect, bool IsFreeAgent) person,
        int index)
    {
        var type = RepresentationTypeFor(person, index);
        var agentId = type == PlayerRepresentationType.NoAgent
            ? null
            : AgentIdFor(Math.Abs(StableHash(person.PersonId)) % SeedProfiles.Length);
        var history = type == PlayerRepresentationType.NoAgent
            ? new[] { $"{scenario.CurrentDate:yyyy-MM-dd}: No formal agent; family handles early guidance." }
            : new[] { $"{scenario.CurrentDate:yyyy-MM-dd}: Representation started as {Readable(type)}." };
        var record = new AgentRepresentationRecord(person.PersonId, person.Name, agentId, type, scenario.CurrentDate.AddDays(-60 - index), history);
        record.Validate();
        return record;
    }

    private static PlayerRepresentationType RepresentationTypeFor((string PersonId, string Name, int Age, bool IsProspect, bool IsFreeAgent) person, int index)
    {
        if (person.IsFreeAgent || person.Age >= 18)
        {
            return PlayerRepresentationType.ProfessionalAgent;
        }

        if (person.IsProspect && index % 7 == 0)
        {
            return PlayerRepresentationType.NoAgent;
        }

        return index % 3 == 0 ? PlayerRepresentationType.FamilyAdvisor : PlayerRepresentationType.JuniorAdvisor;
    }

    private static Agent BuildAgent(NewGmScenarioSnapshot scenario, AgentProfile profile, int index, IReadOnlyList<AgentRepresentationRecord> representations)
    {
        var agentId = AgentIdFor(index);
        var clients = representations
            .Where(record => string.Equals(record.AgentId, agentId, StringComparison.Ordinal))
            .Select(record => new AgentClient(record.PersonId, record.PersonName, record.RepresentationType, record.RepresentationStart))
            .ToArray();
        var agent = new Agent(
            agentId,
            profile,
            new AgentClientList(clients),
            new AgentReputation(58 + index * 4 % 32, 52 + index * 5 % 35, 48 + index * 7 % 38, 60 + index * 3 % 30, ReputationSummary(profile.Personality)),
            new AgentRelationship(scenario.AlphaSnapshot.GeneralManager.PersonId, scenario.AlphaSnapshot.GeneralManager.Identity.DisplayName, 48 + index * 6 % 36, 52 + index * 4 % 34, 50 + index * 5 % 35, "Existing relationship is forming with the new GM."),
            new AgentRelationship(scenario.Organization.OrganizationId, scenario.Organization.Name, 50 + index * 5 % 35, 54 + index * 4 % 30, 52 + index * 3 % 32, "Agency history with the organization is tracked for future talks."),
            new AgentRelationship(scenario.AlphaSnapshot.CoachPerson?.PersonId ?? "coach-placeholder", scenario.AlphaSnapshot.CoachPerson?.Identity.DisplayName ?? "Coach placeholder", 50, 50, 50, "Coach-agent relationship is a placeholder for future depth."),
            StyleFor(profile.Personality),
            PreferredContractsFor(profile.Personality));
        agent.Validate();
        return agent;
    }

    private static IEnumerable<AgentHistoryRecord> BuildInitialHistory(NewGmScenarioSnapshot scenario, Agent agent)
    {
        yield return History(agent.AgentId, scenario.CurrentDate.AddDays(-120), $"{agent.Name} enters the save with {agent.ClientList.Clients.Count} represented client(s).", null, scenario.Organization.OrganizationId, "Players Represented");
        yield return History(agent.AgentId, scenario.CurrentDate.AddDays(-90), $"{agent.Profile.AgencyName} has prior working history with junior and professional clubs. Biggest deals are tracked as placeholders for now.", null, scenario.Organization.OrganizationId, "Organizations Worked With");
    }

    private static AgentHistoryRecord History(string agentId, DateOnly date, string summary, string? personId, string? organizationId, string category)
    {
        var record = new AgentHistoryRecord($"agent-history:{agentId}:{Math.Abs(StableHash($"{date}:{summary}"))}", agentId, date, summary, personId, organizationId, category);
        record.Validate();
        return record;
    }

    private static AgentNegotiationStyle StyleFor(AgentPersonality personality) =>
        personality switch
        {
            AgentPersonality.Aggressive => AgentNegotiationStyle.Aggressive,
            AgentPersonality.MoneyFirst => AgentNegotiationStyle.MoneyFocused,
            AgentPersonality.PlayerDevelopment => AgentNegotiationStyle.DevelopmentFocused,
            AgentPersonality.HardNegotiator => AgentNegotiationStyle.HardLine,
            AgentPersonality.Patient => AgentNegotiationStyle.Patient,
            AgentPersonality.RelationshipBuilder or AgentPersonality.Loyal => AgentNegotiationStyle.RelationshipFirst,
            _ => AgentNegotiationStyle.Collaborative
        };

    private static AgentNegotiationStyle StyleForRepresentation(PlayerRepresentationType type) =>
        type switch
        {
            PlayerRepresentationType.FamilyAdvisor => AgentNegotiationStyle.DevelopmentFocused,
            PlayerRepresentationType.JuniorAdvisor => AgentNegotiationStyle.Patient,
            _ => AgentNegotiationStyle.Collaborative
        };

    private static IReadOnlyList<ContractType> PreferredContractsFor(AgentPersonality personality) =>
        personality is AgentPersonality.PlayerDevelopment or AgentPersonality.Patient
            ? new[] { ContractType.JuniorPlayerAgreement, ContractType.GMContract }
            : new[] { ContractType.JuniorPlayerAgreement, ContractType.StaffContract, ContractType.CoachContract };

    private static int StyleModifier(AgentNegotiationStyle style, decimal salaryGap, int termGap, int relationship, ContractAsk ask, ContractOfferBuildRequest request)
    {
        var modifier = 0;
        if (style is AgentNegotiationStyle.MoneyFocused or AgentNegotiationStyle.HardLine && salaryGap < 0.05m)
        {
            modifier -= 8;
        }

        if (style is AgentNegotiationStyle.DevelopmentFocused && string.IsNullOrWhiteSpace(request.DevelopmentPromise))
        {
            modifier -= 7;
        }

        if (style is AgentNegotiationStyle.Patient && request.AskType == ContractAskType.FreeAgent)
        {
            modifier -= 4;
        }

        if (style is AgentNegotiationStyle.RelationshipFirst && relationship >= 65)
        {
            modifier += 7;
        }

        if (termGap < 0 && style is AgentNegotiationStyle.HardLine or AgentNegotiationStyle.Patient)
        {
            modifier -= 5;
        }

        if (salaryGap >= 0.15m)
        {
            modifier += 5;
        }

        return Math.Clamp(modifier, -20, 15);
    }

    private static string BiggestConcern(ContractAsk ask, ContractOfferBuildRequest request, decimal salaryGap, int termGap, AgentNegotiationStyle style)
    {
        if (salaryGap < 0)
        {
            return "Salary is below comparable players and below the stated ask.";
        }

        if (termGap < 0)
        {
            return "Term is shorter than the client wanted.";
        }

        if (style is AgentNegotiationStyle.DevelopmentFocused && !request.DevelopmentPromise.Contains("development", StringComparison.OrdinalIgnoreCase))
        {
            return "The development plan is not specific enough.";
        }

        return ask.DevelopmentPathwayConcern;
    }

    private static string RequestedImprovement(ContractAsk ask, ContractOfferBuildRequest request, decimal salaryGap, int termGap, AgentNegotiationStyle style)
    {
        if (salaryGap < 0)
        {
            return $"Improve annual salary by at least {(ask.RequestedSalary - request.AnnualSalary):C0}.";
        }

        if (termGap < 0)
        {
            return "Add another year or explain why the shorter term benefits the player.";
        }

        if (style is AgentNegotiationStyle.DevelopmentFocused)
        {
            return "Add a clearer development and role pathway.";
        }

        return "Keep the role, budget, and communication commitments clear.";
    }

    private static string OpinionFor(string agentName, string clientName, AgentNegotiationDecision decision, AgentNegotiationStyle style, string concern) =>
        decision switch
        {
            AgentNegotiationDecision.Accepted => $"{agentName}: My client {clientName} appreciates the offer and is willing to proceed pending GM approval.",
            AgentNegotiationDecision.Rejected => $"{agentName}: My client is not comfortable with this structure. {concern}",
            AgentNegotiationDecision.CounterSuggestion => $"{agentName}: We can keep talking, but this needs one improvement. {concern}",
            AgentNegotiationDecision.Waiting => $"{agentName}: We will take a little time before responding; there may be other organizations to compare.",
            _ => $"{agentName}: The offer needs improvement before my client can commit. {concern}"
        };

    private static string RiskFor(AgentNegotiationStyle style, int relationship, ContractAskType askType)
    {
        if (relationship < 40)
        {
            return "Relationship risk: agent trust is low, so tone and response speed may suffer.";
        }

        if (style is AgentNegotiationStyle.HardLine or AgentNegotiationStyle.MoneyFocused)
        {
            return "Negotiation risk: agent is likely to push for market value and may counter.";
        }

        return askType == ContractAskType.FreeAgent
            ? "Market risk: competing offers can slow or change the response."
            : "Risk is manageable if the development path is credible.";
    }

    private static string CounterFor(ContractAsk ask, ContractOfferBuildRequest request, string improvement) =>
        $"Counter suggestion: {improvement} Target framework is {ask.RequestedTermYears} year(s) at {ask.RequestedSalary:C0} with {ask.DesiredRole}.";

    private static int ResponseDelayFor(AgentNegotiationStyle style, AgentNegotiationDecision decision, ContractAskType askType)
    {
        if (askType != ContractAskType.FreeAgent || decision == AgentNegotiationDecision.Rejected)
        {
            return 0;
        }

        return style switch
        {
            AgentNegotiationStyle.Patient => 4,
            AgentNegotiationStyle.HardLine or AgentNegotiationStyle.MoneyFocused => 3,
            AgentNegotiationStyle.RelationshipFirst => 1,
            _ => 2
        };
    }

    private static string RepresentationLabel(PlayerRepresentationType type) =>
        type switch
        {
            PlayerRepresentationType.NoAgent => "Family contact",
            PlayerRepresentationType.FamilyAdvisor => "Family advisor",
            PlayerRepresentationType.JuniorAdvisor => "Junior advisor",
            _ => "Player representative"
        };

    private static string Readable(PlayerRepresentationType type) =>
        string.Concat(type.ToString().Select((ch, index) => index > 0 && char.IsUpper(ch) ? $" {ch}" : ch.ToString()));

    private static string ReputationSummary(AgentPersonality personality) =>
        personality switch
        {
            AgentPersonality.MoneyFirst => "Known for pushing salary hard and using market pressure.",
            AgentPersonality.PlayerDevelopment => "Known for prioritizing role, coaching, and long-term player growth.",
            AgentPersonality.HardNegotiator => "Known for patient, difficult negotiations.",
            AgentPersonality.RelationshipBuilder => "Known for keeping communication constructive.",
            AgentPersonality.WinNow => "Known for steering clients toward strong organizations.",
            _ => "Known as a credible, recurring hockey representative."
        };

    private static string AgentIdFor(int index) => $"agent:{index + 1:00}";

    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 17;
            foreach (var ch in value)
            {
                hash = hash * 31 + ch;
            }

            return hash;
        }
    }
}
