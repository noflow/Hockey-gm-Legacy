using LegacyEngine.Contracts;

namespace LegacyEngine.Integration;

public enum AgentPersonality
{
    EasyGoing,
    Aggressive,
    MoneyFirst,
    PlayerDevelopment,
    Loyal,
    HardNegotiator,
    RelationshipBuilder,
    WinNow,
    Patient
}

public enum AgentNegotiationStyle
{
    Collaborative,
    Aggressive,
    MoneyFocused,
    DevelopmentFocused,
    HardLine,
    Patient,
    RelationshipFirst
}

public enum PlayerRepresentationType
{
    NoAgent,
    FamilyAdvisor,
    JuniorAdvisor,
    ProfessionalAgent
}

public enum AgentNegotiationDecision
{
    Accepted,
    Rejected,
    NeedsImprovement,
    CounterSuggestion,
    Waiting
}

public sealed record AgentProfile(
    string Name,
    int Age,
    string Nationality,
    string AgencyName,
    int YearsExperience,
    AgentPersonality Personality)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name)
            || string.IsNullOrWhiteSpace(Nationality)
            || string.IsNullOrWhiteSpace(AgencyName))
        {
            throw new ArgumentException("Agent profile requires name, nationality, and agency.");
        }

        if (Age < 24 || YearsExperience < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Age), "Agent age and experience must be valid.");
        }
    }
}

public sealed record AgentReputation(
    int Overall,
    int Fairness,
    int MarketInfluence,
    int PlayerTrust,
    string Summary)
{
    public void Validate()
    {
        if (Overall is < 0 or > 100 || Fairness is < 0 or > 100 || MarketInfluence is < 0 or > 100 || PlayerTrust is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(Overall), "Agent reputation values must be between 0 and 100.");
        }

        if (string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Agent reputation summary is required.", nameof(Summary));
        }
    }
}

public sealed record AgentRelationship(
    string TargetId,
    string TargetName,
    int Trust,
    int Respect,
    int Communication,
    string Summary)
{
    public int Score => Math.Clamp((Trust + Respect + Communication) / 3, 0, 100);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TargetId)
            || string.IsNullOrWhiteSpace(TargetName)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Agent relationship requires target and summary.");
        }

        if (Trust is < 0 or > 100 || Respect is < 0 or > 100 || Communication is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(Trust), "Agent relationship values must be between 0 and 100.");
        }
    }
}

public sealed record AgentClient(
    string PersonId,
    string PersonName,
    PlayerRepresentationType RepresentationType,
    DateOnly RepresentationStart)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId) || string.IsNullOrWhiteSpace(PersonName))
        {
            throw new ArgumentException("Agent client requires person identity.");
        }
    }
}

public sealed record AgentClientList(IReadOnlyList<AgentClient> Clients)
{
    public void Validate()
    {
        if (Clients.Select(client => client.PersonId).Distinct(StringComparer.Ordinal).Count() != Clients.Count)
        {
            throw new ArgumentException("Agent client list cannot contain duplicate players.", nameof(Clients));
        }

        foreach (var client in Clients)
        {
            client.Validate();
        }
    }
}

public sealed record Agent(
    string AgentId,
    AgentProfile Profile,
    AgentClientList ClientList,
    AgentReputation Reputation,
    AgentRelationship GmRelationship,
    AgentRelationship OrganizationRelationship,
    AgentRelationship CoachRelationship,
    AgentNegotiationStyle NegotiationStyle,
    IReadOnlyList<ContractType> PreferredContractTypes)
{
    public string Name => Profile.Name;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(AgentId) || PreferredContractTypes.Count == 0)
        {
            throw new ArgumentException("Agent requires id and preferred contract types.");
        }

        Profile.Validate();
        ClientList.Validate();
        Reputation.Validate();
        GmRelationship.Validate();
        OrganizationRelationship.Validate();
        CoachRelationship.Validate();
    }
}

public sealed record AgentRepresentationRecord(
    string PersonId,
    string PersonName,
    string? AgentId,
    PlayerRepresentationType RepresentationType,
    DateOnly RepresentationStart,
    IReadOnlyList<string> RepresentationHistory)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId) || string.IsNullOrWhiteSpace(PersonName))
        {
            throw new ArgumentException("Representation record requires person identity.");
        }

        if (RepresentationType != PlayerRepresentationType.NoAgent && string.IsNullOrWhiteSpace(AgentId))
        {
            throw new ArgumentException("Represented players require an agent id.", nameof(AgentId));
        }

        foreach (var entry in RepresentationHistory)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                throw new ArgumentException("Representation history entries cannot be blank.", nameof(RepresentationHistory));
            }
        }
    }
}

public sealed record AgentHistoryRecord(
    string HistoryId,
    string AgentId,
    DateOnly Date,
    string Summary,
    string? PersonId,
    string? OrganizationId,
    string Category)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(HistoryId)
            || string.IsNullOrWhiteSpace(AgentId)
            || string.IsNullOrWhiteSpace(Summary)
            || string.IsNullOrWhiteSpace(Category))
        {
            throw new ArgumentException("Agent history requires id, agent, summary, and category.");
        }
    }
}

public sealed record AgentNegotiationReview(
    string? AgentId,
    string AgentName,
    string AgencyName,
    PlayerRepresentationType RepresentationType,
    AgentNegotiationStyle NegotiationStyle,
    AgentNegotiationDecision Decision,
    int ScoreModifier,
    int Likelihood,
    string Opinion,
    string BiggestConcern,
    string RequestedImprovement,
    string Risk,
    string CounterSuggestion,
    int ResponseDelayDays)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(AgentName)
            || string.IsNullOrWhiteSpace(AgencyName)
            || string.IsNullOrWhiteSpace(Opinion)
            || string.IsNullOrWhiteSpace(BiggestConcern)
            || string.IsNullOrWhiteSpace(RequestedImprovement)
            || string.IsNullOrWhiteSpace(Risk)
            || string.IsNullOrWhiteSpace(CounterSuggestion))
        {
            throw new ArgumentException("Agent negotiation review requires readable explanation fields.");
        }

        if (Likelihood is < 0 or > 100 || ScoreModifier is < -30 or > 30 || ResponseDelayDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Likelihood), "Agent review scores must be valid.");
        }
    }
}
