namespace LegacyEngine.Integration;

public sealed record FreeAgentMarket(
    string MarketId,
    DateOnly OpenedOn,
    IReadOnlyList<FreeAgent> FreeAgents)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(MarketId))
        {
            throw new ArgumentException("Free agent market id is required.", nameof(MarketId));
        }

        if (FreeAgents.Select(agent => agent.PersonId).Distinct(StringComparer.Ordinal).Count() != FreeAgents.Count)
        {
            throw new ArgumentException("Free agent market cannot contain duplicate person ids.", nameof(FreeAgents));
        }

        foreach (var freeAgent in FreeAgents)
        {
            freeAgent.Validate();
        }
    }

    public FreeAgent? Find(string personId) =>
        FreeAgents.FirstOrDefault(agent => agent.PersonId == personId);

    public FreeAgentMarket Replace(FreeAgent freeAgent) =>
        this with
        {
            FreeAgents = FreeAgents
                .Select(agent => agent.PersonId == freeAgent.PersonId ? freeAgent : agent)
                .ToArray()
        };
}
