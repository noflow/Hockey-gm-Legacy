namespace LegacyEngine.Relationships;

public sealed record Relationship(
    string RelationshipId,
    string FromPersonId,
    string ToPersonId,
    RelationshipType RelationshipType,
    int Trust,
    int Respect,
    int Confidence,
    int Loyalty,
    int Influence,
    int Friendship,
    int Rivalry,
    DateOnly LastInteractionDate,
    bool IsActive,
    IReadOnlyList<RelationshipHistoryEntry> History)
{
    public static Relationship Create(
        string relationshipId,
        string fromPersonId,
        string toPersonId,
        RelationshipType relationshipType,
        DateOnly createdOn,
        RelationshipDefaults? defaults = null)
    {
        defaults ??= RelationshipDefaults.Standard;

        var relationship = new Relationship(
            RelationshipId: relationshipId,
            FromPersonId: fromPersonId,
            ToPersonId: toPersonId,
            RelationshipType: relationshipType,
            Trust: defaults.Trust,
            Respect: defaults.Respect,
            Confidence: defaults.Confidence,
            Loyalty: defaults.Loyalty,
            Influence: defaults.Influence,
            Friendship: defaults.Friendship,
            Rivalry: defaults.Rivalry,
            LastInteractionDate: createdOn,
            IsActive: true,
            History: Array.Empty<RelationshipHistoryEntry>());

        relationship.Validate();
        return relationship;
    }

    public Relationship ApplyChange(RelationshipChange change, bool allowInactive = false)
    {
        Validate();
        change.Validate();

        if (!IsActive && !allowInactive)
        {
            return this;
        }

        var oldValue = GetDimensionValue(change.Dimension);
        var newValue = ClampScore(oldValue + change.Amount);
        if (oldValue == newValue)
        {
            return this with { LastInteractionDate = change.Date };
        }

        var changed = SetDimensionValue(change.Dimension, newValue) with { LastInteractionDate = change.Date };
        return changed.AddHistoryEntry(new RelationshipHistoryEntry(
            EntryId: BuildHistoryEntryId(change.Dimension, change.Date, History.Count + 1),
            RelationshipId: RelationshipId,
            Date: change.Date,
            Title: $"{change.Dimension} changed",
            Description: change.Reason,
            DimensionChanged: change.Dimension,
            AmountChanged: newValue - oldValue,
            OldValue: oldValue,
            NewValue: newValue,
            RelatedEventId: change.RelatedEventId));
    }

    public Relationship ApplyDecay(
        DateOnly decayDate,
        int daysBeforeDecay = 30,
        int amountPerPeriod = 1,
        bool allowInactive = false)
    {
        Validate();

        if (!IsActive && !allowInactive)
        {
            return this;
        }

        if (daysBeforeDecay <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(daysBeforeDecay), "Decay interval must be positive.");
        }

        if (amountPerPeriod <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amountPerPeriod), "Decay amount must be positive.");
        }

        var daysSinceInteraction = decayDate.DayNumber - LastInteractionDate.DayNumber;
        if (daysSinceInteraction < daysBeforeDecay)
        {
            return this;
        }

        var decayAmount = Math.Max(1, daysSinceInteraction / daysBeforeDecay) * amountPerPeriod;
        var decayed = this;
        decayed = decayed.DecayDimension(RelationshipDimension.Trust, 50, decayAmount, decayDate);
        decayed = decayed.DecayDimension(RelationshipDimension.Friendship, 0, decayAmount, decayDate);
        decayed = decayed.DecayDimension(RelationshipDimension.Rivalry, 0, decayAmount, decayDate);
        return decayed;
    }

    public Relationship Deactivate(DateOnly date, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Relationship deactivation reason is required.", nameof(reason));
        }

        return this with { IsActive = false, LastInteractionDate = date };
    }

    public int GetDimensionValue(RelationshipDimension dimension) =>
        dimension switch
        {
            RelationshipDimension.Trust => Trust,
            RelationshipDimension.Respect => Respect,
            RelationshipDimension.Confidence => Confidence,
            RelationshipDimension.Loyalty => Loyalty,
            RelationshipDimension.Influence => Influence,
            RelationshipDimension.Friendship => Friendship,
            RelationshipDimension.Rivalry => Rivalry,
            _ => throw new ArgumentOutOfRangeException(nameof(dimension), dimension, "Unsupported relationship dimension.")
        };

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(RelationshipId))
        {
            throw new ArgumentException("Relationship id is required.", nameof(RelationshipId));
        }

        if (string.IsNullOrWhiteSpace(FromPersonId))
        {
            throw new ArgumentException("From person id is required.", nameof(FromPersonId));
        }

        if (string.IsNullOrWhiteSpace(ToPersonId))
        {
            throw new ArgumentException("To person id is required.", nameof(ToPersonId));
        }

        if (FromPersonId == ToPersonId)
        {
            throw new ArgumentException("A relationship must point from one person to another person.");
        }

        ValidateScore(Trust, nameof(Trust));
        ValidateScore(Respect, nameof(Respect));
        ValidateScore(Confidence, nameof(Confidence));
        ValidateScore(Loyalty, nameof(Loyalty));
        ValidateScore(Influence, nameof(Influence));
        ValidateScore(Friendship, nameof(Friendship));
        ValidateScore(Rivalry, nameof(Rivalry));

        foreach (var entry in History)
        {
            entry.Validate();
        }

        if (History.Select(entry => entry.EntryId).Distinct().Count() != History.Count)
        {
            throw new ArgumentException("Relationship history entry ids must be unique.", nameof(History));
        }
    }

    private Relationship DecayDimension(RelationshipDimension dimension, int target, int amount, DateOnly date)
    {
        var oldValue = GetDimensionValue(dimension);
        var newValue = DriftToward(oldValue, target, amount);
        if (oldValue == newValue)
        {
            return this;
        }

        var changed = SetDimensionValue(dimension, newValue) with { LastInteractionDate = date };
        return changed.AddHistoryEntry(new RelationshipHistoryEntry(
            EntryId: BuildHistoryEntryId(dimension, date, History.Count + 1),
            RelationshipId: RelationshipId,
            Date: date,
            Title: $"{dimension} decayed",
            Description: $"{dimension} drifted conservatively toward {target}.",
            DimensionChanged: dimension,
            AmountChanged: newValue - oldValue,
            OldValue: oldValue,
            NewValue: newValue,
            RelatedEventId: null));
    }

    private Relationship SetDimensionValue(RelationshipDimension dimension, int value) =>
        dimension switch
        {
            RelationshipDimension.Trust => this with { Trust = value },
            RelationshipDimension.Respect => this with { Respect = value },
            RelationshipDimension.Confidence => this with { Confidence = value },
            RelationshipDimension.Loyalty => this with { Loyalty = value },
            RelationshipDimension.Influence => this with { Influence = value },
            RelationshipDimension.Friendship => this with { Friendship = value },
            RelationshipDimension.Rivalry => this with { Rivalry = value },
            _ => throw new ArgumentOutOfRangeException(nameof(dimension), dimension, "Unsupported relationship dimension.")
        };

    private Relationship AddHistoryEntry(RelationshipHistoryEntry entry)
    {
        entry.Validate();
        return this with { History = History.Append(entry).OrderBy(item => item.Date).ToArray() };
    }

    private string BuildHistoryEntryId(RelationshipDimension dimension, DateOnly date, int sequence) =>
        $"{RelationshipId}:{dimension}:{date:yyyyMMdd}:{sequence}";

    private static int DriftToward(int value, int target, int amount)
    {
        if (value == target)
        {
            return value;
        }

        if (value > target)
        {
            return Math.Max(target, value - amount);
        }

        return Math.Min(target, value + amount);
    }

    private static int ClampScore(int value) => Math.Clamp(value, 0, 100);

    private static void ValidateScore(int value, string name)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(name, "Relationship values must be between 0 and 100.");
        }
    }
}
