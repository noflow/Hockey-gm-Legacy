namespace LegacyEngine.Development;

public sealed record PlayerDevelopmentProfile(
    string PersonId,
    int CurrentAbility,
    int Potential,
    DevelopmentStage Stage,
    IReadOnlyList<DevelopmentTrait> Traits,
    DateOnly LastUpdated)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId))
        {
            throw new ArgumentException("Person id is required.", nameof(PersonId));
        }

        ValidateHiddenRating(CurrentAbility, nameof(CurrentAbility));
        ValidateHiddenRating(Potential, nameof(Potential));

        if (CurrentAbility > Potential)
        {
            throw new ArgumentException("Current ability cannot exceed potential in Player Development v1.", nameof(CurrentAbility));
        }

        foreach (var trait in Traits)
        {
            trait.Validate();
        }

        if (Traits.Select(trait => trait.Attribute).Distinct().Count() != Traits.Count)
        {
            throw new ArgumentException("Development traits must be unique by attribute.", nameof(Traits));
        }
    }

    public int TraitValue(DevelopmentAttribute attribute) =>
        Traits.SingleOrDefault(trait => trait.Attribute == attribute)?.Value
        ?? throw new ArgumentException($"Development trait '{attribute}' is required.", nameof(attribute));

    private static void ValidateHiddenRating(int value, string name)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(name, "Hidden development ratings must be between 0 and 100.");
        }
    }
}
