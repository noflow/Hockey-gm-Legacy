namespace LegacyEngine.Contracts;

public sealed record Contract(
    string ContractId,
    string PersonId,
    string OrganizationId,
    ContractType ContractType,
    ContractStatus Status,
    ContractTerm Term,
    ContractMoney Money,
    IReadOnlyList<ContractClause> Clauses,
    DateOnly OfferedOn,
    DateOnly? SignedOn,
    DateOnly? RejectedOn,
    DateOnly? TerminatedOn,
    DateOnly? ExpiredOn)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ContractId))
        {
            throw new ArgumentException("Contract id is required.", nameof(ContractId));
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
    }

    public Contract Sign(DateOnly signedOn)
    {
        EnsureOffered();
        return this with { Status = ContractStatus.Signed, SignedOn = signedOn };
    }

    public Contract Reject(DateOnly rejectedOn)
    {
        EnsureOffered();
        return this with { Status = ContractStatus.Rejected, RejectedOn = rejectedOn };
    }

    public Contract Terminate(DateOnly terminatedOn)
    {
        if (Status != ContractStatus.Signed)
        {
            throw new InvalidOperationException("Only signed contracts can be terminated.");
        }

        return this with { Status = ContractStatus.Terminated, TerminatedOn = terminatedOn };
    }

    public Contract Expire(DateOnly expiredOn)
    {
        if (expiredOn < Term.EndDate)
        {
            throw new ArgumentOutOfRangeException(nameof(expiredOn), "Contract cannot expire before its end date.");
        }

        return this with { Status = ContractStatus.Expired, ExpiredOn = expiredOn };
    }

    private void EnsureOffered()
    {
        if (Status != ContractStatus.Offered)
        {
            throw new InvalidOperationException("Only offered contracts can be signed or rejected.");
        }
    }
}
