namespace LegacyEngine.Owners;

public sealed record OwnerBudget(
    decimal PlayerPayroll,
    decimal Staff,
    decimal Scouting,
    decimal Facilities,
    decimal Operations)
{
    public decimal Total => PlayerPayroll + Staff + Scouting + Facilities + Operations;

    public bool CanFund(decimal amount) => amount >= 0 && amount <= Total;

    public void Validate()
    {
        if (PlayerPayroll < 0 || Staff < 0 || Scouting < 0 || Facilities < 0 || Operations < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(OwnerBudget), "Owner budget categories cannot be negative.");
        }
    }
}
