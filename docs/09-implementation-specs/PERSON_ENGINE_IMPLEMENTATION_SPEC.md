# Milestone 005 – Person Engine Implementation Spec

## Purpose

The Person Engine defines the shared human foundation used by every role in Hockey GM Legacy.

A Player is a Person with a Player role.

A Scout is a Person with a Scout role.

An Owner is a Person with an Owner role.

The Person ID never changes.

## Core Principle

```text
Person = identity
Role = current or historical function
Career Timeline = what happened
Reputation = how the hockey world views them
Personality = how they tend to behave
```

## Version 1 Goal

Build a standalone Person Engine that can:

- Create people.
- Store identity.
- Calculate age.
- Track status.
- Add roles.
- End roles.
- Support multiple roles.
- Track basic reputation.
- Store basic personality values.
- Add career timeline entries.
- Be tested without Godot or UI.

## Required Module

```text
engine/LegacyEngine/People
```

## Core Classes

### Person

Required fields:

```text
PersonId
FirstName
LastName
PreferredName
Gender
BirthDate
Nationality
Birthplace
Status
Reputation
Personality
Roles
CareerTimeline
```

### PersonStatus

Required values:

```text
Active
Retired
Deceased
Inactive
```

### PersonRole

Required fields:

```text
RoleId
PersonId
RoleType
OrganizationId
StartDate
EndDate
IsActive
Reputation
Notes
```

### PersonRoleType

Version 1 values:

```text
Player
Owner
Scout
HeadScout
Coach
HeadCoach
AssistantCoach
GeneralManager
AssistantGeneralManager
Agent
Doctor
Trainer
Parent
Media
LeagueExecutive
```

### PersonalityProfile

Version 1 fields:

```text
WorkEthic
Ambition
Loyalty
PressureHandling
Leadership
Communication
RiskTolerance
Adaptability
Professionalism
```

All values should be simple 0–100 integers for v1.

### CareerTimelineEntry

Required fields:

```text
EntryId
PersonId
Date
Title
Description
EntryType
RelatedOrganizationId
RelatedEventId
```

Entry type examples:

```text
Created
RoleStarted
RoleEnded
ReputationChanged
StatusChanged
CareerMilestone
```

## Required Behaviors

### Create Person

A person can be created with identity data and default status Active.

### Calculate Age

Age is calculated from BirthDate and a supplied current date.

Do not store age permanently.

### Add Role

A person may receive a new role.

### End Role

A role may be ended with an end date.

Ended roles remain in history.

Do not delete roles.

### Multiple Roles

A person may have multiple active or historical roles.

Examples:

Owner + LeagueExecutive

RetiredPlayer + Scout

Coach + AssistantGeneralManager

### Reputation Change

The system should allow reputation changes with reason text.

Reputation should stay within 0–100.

### Status Change

The system should allow status changes.

Example:

Active → Retired

Retired → Deceased

### Career Timeline

Important Person Engine actions should create timeline entries:

- Person created
- Role started
- Role ended
- Reputation changed
- Status changed

## Unit Tests

Required tests:

- Create person.
- Calculate age correctly.
- Add role.
- End role.
- Person can have multiple roles.
- Ended role remains in role history.
- Reputation increases.
- Reputation decreases.
- Reputation cannot exceed 100.
- Reputation cannot go below 0.
- Status can change.
- Timeline entry is created when role starts.
- Timeline entry is created when role ends.
- Timeline entry is created when reputation changes.
- Timeline entry is created when status changes.

## Boundaries

Do not build:

- Relationship Engine
- Memory Engine
- Recruiting
- Contracts
- Trades
- Game simulation
- UI
- Godot integration

## Completion Criteria

Milestone 005 is complete when:

- Person Engine exists as a standalone module.
- People can be created.
- Age can be calculated.
- Roles can be added and ended.
- Multiple roles are supported.
- Reputation is tracked.
- Personality profile exists.
- Career timeline entries are created.
- Unit tests pass.
- No UI or Godot dependency exists.

## Final Design Rule

People are permanent.

Roles change.

History remains.
