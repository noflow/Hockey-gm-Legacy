namespace LegacyEngine.Integration;

public sealed record SalaryCapCalculation(
    SalaryCapSnapshot Before,
    SalaryCapSnapshot After,
    bool IsCompliant,
    IReadOnlyList<string> Reasons)
{
    public void Validate()
    {
        Before.Validate();
        After.Validate();
        foreach (var reason in Reasons)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Salary cap calculation reasons cannot be blank.", nameof(Reasons));
            }
        }
    }
}
