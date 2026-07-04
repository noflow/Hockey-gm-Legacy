namespace LegacyEngine.Relationships;

public sealed class RelationshipEngine
{
    public Relationship CreateRelationship(
        string relationshipId,
        string fromPersonId,
        string toPersonId,
        RelationshipType relationshipType,
        DateOnly createdOn,
        RelationshipDefaults? defaults = null) =>
        Relationship.Create(relationshipId, fromPersonId, toPersonId, relationshipType, createdOn, defaults);

    public Relationship ChangeRelationship(Relationship relationship, RelationshipChange change, bool allowInactive = false) =>
        relationship.ApplyChange(change, allowInactive);

    public Relationship ApplyDecay(
        Relationship relationship,
        DateOnly decayDate,
        int daysBeforeDecay = 30,
        int amountPerPeriod = 1,
        bool allowInactive = false) =>
        relationship.ApplyDecay(decayDate, daysBeforeDecay, amountPerPeriod, allowInactive);
}
