namespace LegacyEngine.Contracts;

public sealed record ContractClause(
    string ClauseId,
    ContractClauseType ClauseType,
    string Description)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ClauseId))
        {
            throw new ArgumentException("Clause id is required.", nameof(ClauseId));
        }

        if (string.IsNullOrWhiteSpace(Description))
        {
            throw new ArgumentException("Clause description is required.", nameof(Description));
        }
    }
}
