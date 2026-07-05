namespace LegacyEngine.Organizations;

/// <summary>
/// A reference to a budget owned elsewhere (Owners today, a future Finance engine
/// later). The Organization engine stores the reference only; it performs no
/// financial calculation.
/// </summary>
public sealed record OrganizationBudgetReference(
    string BudgetId,
    string OrganizationId,
    string? Label = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BudgetId))
        {
            throw new ArgumentException("Budget id is required.", nameof(BudgetId));
        }

        if (string.IsNullOrWhiteSpace(OrganizationId))
        {
            throw new ArgumentException("Organization id is required.", nameof(OrganizationId));
        }
    }
}
