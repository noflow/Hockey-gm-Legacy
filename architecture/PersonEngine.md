# Person Engine Architecture

## Purpose

The Person Engine is the shared human foundation for Hockey GM Legacy.

Every human is represented as a Person.

Roles define what the person currently does or has done in the past.

## Core Objects

```text
Person
│
├── Identity
├── Status
├── Reputation
├── Personality
├── Roles
└── Career Timeline
```

## Key Rule

The Person ID never changes.

If a player retires and becomes a scout, the system adds a Scout role. It does not create a new person.

## System Boundaries

The Person Engine does not own:

- Relationships
- Memories
- Contracts
- Player ratings
- Staff ratings
- Recruiting
- Game simulation

Those systems reference Person IDs.
