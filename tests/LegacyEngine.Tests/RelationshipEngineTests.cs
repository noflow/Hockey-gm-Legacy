using LegacyEngine.Relationships;

internal sealed class RelationshipEngineTests
{
    public void CreateRelationship()
    {
        var relationship = BuildRelationship();

        relationship.Validate();
        Assert.Equal("relationship-a-b", relationship.RelationshipId);
        Assert.Equal("person-a", relationship.FromPersonId);
        Assert.Equal("person-b", relationship.ToPersonId);
        Assert.Equal(RelationshipType.GMToScout, relationship.RelationshipType);
        Assert.Equal(50, relationship.Trust);
        Assert.Equal(50, relationship.Respect);
        Assert.Equal(50, relationship.Confidence);
        Assert.Equal(50, relationship.Loyalty);
        Assert.Equal(50, relationship.Influence);
        Assert.Equal(0, relationship.Friendship);
        Assert.Equal(0, relationship.Rivalry);
        Assert.Equal(new DateOnly(2026, 1, 1), relationship.LastInteractionDate);
        Assert.True(relationship.IsActive, "New relationships should be active.");
        Assert.Equal(0, relationship.History.Count);
    }

    public void DirectionalityAndReverseIndependence()
    {
        var engine = new RelationshipEngine();
        var aToB = engine
            .CreateRelationship("relationship-a-b", "person-a", "person-b", RelationshipType.GMToScout, new DateOnly(2026, 1, 1))
            .ApplyChange(new RelationshipChange(RelationshipDimension.Trust, 20, "GM trusts the scout's reports.", new DateOnly(2026, 1, 2)));
        var bToA = engine
            .CreateRelationship("relationship-b-a", "person-b", "person-a", RelationshipType.ScoutToGM, new DateOnly(2026, 1, 1))
            .ApplyChange(new RelationshipChange(RelationshipDimension.Respect, 5, "Scout respects the GM's process.", new DateOnly(2026, 1, 2)));

        Assert.Equal("person-a", aToB.FromPersonId);
        Assert.Equal("person-b", bToA.FromPersonId);
        Assert.Equal(70, aToB.Trust);
        Assert.Equal(50, bToA.Trust);
        Assert.Equal(55, bToA.Respect);
        Assert.Equal(50, aToB.Respect);
    }

    public void TrustChanges()
    {
        var relationship = BuildRelationship()
            .ApplyChange(new RelationshipChange(RelationshipDimension.Trust, 10, "Trust improved after accurate advice.", new DateOnly(2026, 1, 3)))
            .ApplyChange(new RelationshipChange(RelationshipDimension.Trust, -15, "Trust dropped after missed preparation.", new DateOnly(2026, 1, 4)));

        Assert.Equal(45, relationship.Trust);
        Assert.Equal(2, relationship.History.Count);
        Assert.Equal(10, relationship.History[0].AmountChanged);
        Assert.Equal(-15, relationship.History[1].AmountChanged);
    }

    public void AllDimensionsChange()
    {
        var relationship = BuildRelationship()
            .ApplyChange(new RelationshipChange(RelationshipDimension.Respect, 3, "Professional respect increased.", new DateOnly(2026, 1, 2)))
            .ApplyChange(new RelationshipChange(RelationshipDimension.Confidence, -5, "Confidence dipped.", new DateOnly(2026, 1, 3)))
            .ApplyChange(new RelationshipChange(RelationshipDimension.Loyalty, 7, "Loyalty improved.", new DateOnly(2026, 1, 4)))
            .ApplyChange(new RelationshipChange(RelationshipDimension.Influence, 8, "Influence grew.", new DateOnly(2026, 1, 5)))
            .ApplyChange(new RelationshipChange(RelationshipDimension.Friendship, 12, "Friendship formed.", new DateOnly(2026, 1, 6)))
            .ApplyChange(new RelationshipChange(RelationshipDimension.Rivalry, 4, "Competitive tension emerged.", new DateOnly(2026, 1, 7)));

        Assert.Equal(53, relationship.Respect);
        Assert.Equal(45, relationship.Confidence);
        Assert.Equal(57, relationship.Loyalty);
        Assert.Equal(58, relationship.Influence);
        Assert.Equal(12, relationship.Friendship);
        Assert.Equal(4, relationship.Rivalry);
        Assert.Equal(6, relationship.History.Count);
    }

    public void ValueClamping()
    {
        var relationship = BuildRelationship()
            .ApplyChange(new RelationshipChange(RelationshipDimension.Trust, 90, "Major trust boost.", new DateOnly(2026, 1, 2)))
            .ApplyChange(new RelationshipChange(RelationshipDimension.Respect, -90, "Major respect loss.", new DateOnly(2026, 1, 3)))
            .ApplyChange(new RelationshipChange(RelationshipDimension.Friendship, 150, "Friendship cannot exceed bounds.", new DateOnly(2026, 1, 4)))
            .ApplyChange(new RelationshipChange(RelationshipDimension.Rivalry, -10, "Rivalry cannot go below zero.", new DateOnly(2026, 1, 5)));

        Assert.Equal(100, relationship.Trust);
        Assert.Equal(0, relationship.Respect);
        Assert.Equal(100, relationship.Friendship);
        Assert.Equal(0, relationship.Rivalry);
    }

    public void LastInteractionAndHistory()
    {
        var relationship = BuildRelationship();
        var changed = relationship.ApplyChange(new RelationshipChange(
            RelationshipDimension.Confidence,
            9,
            "Confidence rose after a useful meeting.",
            new DateOnly(2026, 2, 10),
            RelatedEventId: "event-001"));

        Assert.Equal(new DateOnly(2026, 2, 10), changed.LastInteractionDate);
        Assert.Equal(1, changed.History.Count);
        Assert.Equal(RelationshipDimension.Confidence, changed.History[0].DimensionChanged);
        Assert.Equal(50, changed.History[0].OldValue);
        Assert.Equal(59, changed.History[0].NewValue);
        Assert.Equal("event-001", changed.History[0].RelatedEventId);
    }

    public void MultipleHistoryEntries()
    {
        var relationship = BuildRelationship()
            .ApplyChange(new RelationshipChange(RelationshipDimension.Trust, 5, "Trust increased.", new DateOnly(2026, 2, 1)))
            .ApplyChange(new RelationshipChange(RelationshipDimension.Respect, 6, "Respect increased.", new DateOnly(2026, 2, 2)))
            .ApplyChange(new RelationshipChange(RelationshipDimension.Loyalty, -3, "Loyalty slipped.", new DateOnly(2026, 2, 3)));

        Assert.Equal(3, relationship.History.Count);
        Assert.Equal(RelationshipDimension.Trust, relationship.History[0].DimensionChanged);
        Assert.Equal(RelationshipDimension.Respect, relationship.History[1].DimensionChanged);
        Assert.Equal(RelationshipDimension.Loyalty, relationship.History[2].DimensionChanged);
    }

    public void DecayMovesTrustTowardNeutral()
    {
        var highTrust = BuildRelationship(new RelationshipDefaults(Trust: 80, Respect: 65, Confidence: 60, Loyalty: 55, Influence: 50, Friendship: 20, Rivalry: 10));
        var lowTrust = BuildRelationship(new RelationshipDefaults(Trust: 30, Respect: 65, Confidence: 60, Loyalty: 55, Influence: 50, Friendship: 20, Rivalry: 10));

        var decayedHigh = highTrust.ApplyDecay(new DateOnly(2026, 4, 1), daysBeforeDecay: 30, amountPerPeriod: 2);
        var decayedLow = lowTrust.ApplyDecay(new DateOnly(2026, 4, 1), daysBeforeDecay: 30, amountPerPeriod: 2);

        Assert.True(decayedHigh.Trust < highTrust.Trust, "High trust should drift downward toward neutral.");
        Assert.True(decayedLow.Trust > lowTrust.Trust, "Low trust should drift upward toward neutral.");
        Assert.Equal(highTrust.Respect, decayedHigh.Respect);
        Assert.Equal(highTrust.Confidence, decayedHigh.Confidence);
    }

    public void DecayReducesFriendshipAndRivalry()
    {
        var relationship = BuildRelationship(new RelationshipDefaults(Trust: 50, Respect: 50, Confidence: 50, Loyalty: 50, Influence: 50, Friendship: 20, Rivalry: 12));
        var decayed = relationship.ApplyDecay(new DateOnly(2026, 3, 2), daysBeforeDecay: 30, amountPerPeriod: 1);

        Assert.Equal(18, decayed.Friendship);
        Assert.Equal(10, decayed.Rivalry);
        Assert.Equal(50, decayed.Trust);
        Assert.True(decayed.History.Count >= 2, "Decay should create history entries for changed dimensions.");
    }

    public void InactiveRelationshipDoesNotChangeByDefault()
    {
        var relationship = BuildRelationship()
            .Deactivate(new DateOnly(2026, 1, 5), "Relationship is no longer active.");

        var changed = relationship.ApplyChange(new RelationshipChange(RelationshipDimension.Trust, 20, "Ignored change.", new DateOnly(2026, 1, 6)));
        var decayed = relationship.ApplyDecay(new DateOnly(2026, 5, 1));
        var explicitlyChanged = relationship.ApplyChange(
            new RelationshipChange(RelationshipDimension.Trust, 20, "Explicit inactive change.", new DateOnly(2026, 1, 6)),
            allowInactive: true);

        Assert.Equal(relationship.Trust, changed.Trust);
        Assert.Equal(relationship.History.Count, changed.History.Count);
        Assert.Equal(relationship.Trust, decayed.Trust);
        Assert.Equal(70, explicitlyChanged.Trust);
    }

    private static Relationship BuildRelationship(RelationshipDefaults? defaults = null) =>
        new RelationshipEngine().CreateRelationship(
            relationshipId: "relationship-a-b",
            fromPersonId: "person-a",
            toPersonId: "person-b",
            relationshipType: RelationshipType.GMToScout,
            createdOn: new DateOnly(2026, 1, 1),
            defaults: defaults);
}
