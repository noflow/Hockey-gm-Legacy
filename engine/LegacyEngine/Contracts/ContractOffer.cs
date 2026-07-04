namespace LegacyEngine.Contracts;

public sealed record ContractOffer(
    string OfferId,
    string PersonId,
    string OrganizationId,
    ContractType ContractType,
    ContractTerm Term,
    ContractMoney Money,
    IReadOnlyList<ContractClause> Clauses,
    DateOnly OfferedOn,
    string Notes)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OfferId))
        {
            throw new ArgumentException("Offer id is required.", nameof(OfferId));
        }

        if (string.IsNullOrWhiteSpace(PersonId))
        {
            throw new ArgumentException("Person id is required.", nameof(PersonId));
        }

        if (string.IsNullOrWhiteSpace(OrganizationId))
        {
            throw new ArgumentException("Organization id is required.", nameof(OrganizationId));
        }

        Term.Validate();
        Money.Validate();

        foreach (var clause in Clauses)
        {
            clause.Validate();
        }

        if (Clauses.Select(item => item.ClauseId).Distinct(StringComparer.Ordinal).Count() != Clauses.Count)
        {
            throw new ArgumentException("Contract clause ids must be unique.", nameof(Clauses));
        }
    }
}
