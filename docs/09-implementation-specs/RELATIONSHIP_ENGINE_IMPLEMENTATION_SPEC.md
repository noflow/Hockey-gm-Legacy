# Milestone 006 – Relationship Engine Implementation Spec

## Purpose

The Relationship Engine tracks how people view each other.

It supports the emotional and professional connections that drive Hockey GM Legacy.

A relationship is directional.

```text
Person A → Person B
```

This is not the same as:

```text
Person B → Person A
```

Example:

A GM may trust a scout highly.

The scout may respect the GM but not fully trust them yet.

---

# Version 1 Goal

Build a standalone Relationship Engine that can:

- Create directional relationships.
- Track relationship dimensions.
- Change relationship values.
- Clamp values from 0 to 100.
- Track last interaction date.
- Add relationship history entries.
- Support basic relationship decay.
- Be tested without UI or Godot.

---

# Required Module

```text
engine/LegacyEngine/Relationships
```

---

# Core Classes

## Relationship

Required fields:

```text
RelationshipId
FromPersonId
ToPersonId
RelationshipType
Trust
Respect
Confidence
Loyalty
Influence
Friendship
Rivalry
LastInteractionDate
IsActive
History
```

---

## RelationshipType

Version 1 values:

```text
Professional
OwnerToGM
GMToOwner
GMToScout
ScoutToGM
CoachToPlayer
PlayerToCoach
PlayerToPlayer
ParentToOrganization
AgentToGM
Mentor
Family
Media
League
Unknown
```

---

## RelationshipHistoryEntry

Required fields:

```text
EntryId
RelationshipId
Date
Title
Description
DimensionChanged
AmountChanged
OldValue
NewValue
RelatedEventId
```

---

## RelationshipDimension

Version 1 values:

```text
Trust
Respect
Confidence
Loyalty
Influence
Friendship
Rivalry
```

---

## RelationshipChange

Optional helper object:

```text
Dimension
Amount
Reason
Date
RelatedEventId
```

---

# Required Behaviors

## Create Relationship

Create a directional relationship from one Person ID to another Person ID.

Default values should be configurable, but v1 may use:

```text
Trust: 50
Respect: 50
Confidence: 50
Loyalty: 50
Influence: 50
Friendship: 0
Rivalry: 0
```

---

## Directionality

The system must allow:

```text
A → B
```

and

```text
B → A
```

to have different values.

---

## Change Dimension

The system must support changing one dimension at a time.

Example:

```text
Trust +10
Confidence -5
Respect +3
```

All values must clamp between 0 and 100.

---

## Last Interaction

Whenever a relationship changes, update LastInteractionDate.

---

## History Entry

Every change creates a RelationshipHistoryEntry.

---

## Decay

Version 1 should include basic decay logic.

If no interaction occurs for a configurable number of days, selected dimensions may drift slowly toward neutral.

Suggested v1 behavior:

- Trust drifts toward 50.
- Friendship drifts toward 0.
- Rivalry drifts toward 0.
- Respect may remain stable.
- Confidence may remain stable unless future events affect it.

Decay should be conservative.

The goal is not to erase history quickly.

---

# Unit Tests

Required tests:

- Create relationship.
- Relationship is directional.
- Reverse relationship can have different values.
- Trust increases.
- Trust decreases.
- Respect changes.
- Confidence changes.
- Loyalty changes.
- Influence changes.
- Friendship changes.
- Rivalry changes.
- Values cannot exceed 100.
- Values cannot go below 0.
- LastInteractionDate updates after change.
- History entry is created after change.
- Multiple history entries are stored.
- Decay moves trust toward neutral.
- Decay reduces friendship slowly.
- Decay reduces rivalry slowly.
- Inactive relationship does not change unless explicitly allowed.

---

# Boundaries

Do not build:

- Recruiting
- Contracts
- Trades
- Game simulation
- Full Event Engine
- Full Memory Engine
- Full Communication Engine
- UI
- Godot integration

---

# Completion Criteria

Milestone 006 is complete when:

- Relationship Engine exists as a standalone module.
- Relationships can be created.
- Relationships are directional.
- All required dimensions exist.
- Relationship values can change.
- Values are clamped.
- Last interaction date updates.
- History entries are created.
- Basic decay works.
- Unit tests pass.
- No UI or Godot dependency exists.

---

# Final Design Rule

People are permanent.

Roles define what they do.

Relationships define how they feel about each other.
