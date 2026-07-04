# Milestone 006 – Relationship Engine

## Purpose

Build the system that connects people.

The Relationship Engine is the glue between the Person Engine, Owner Engine, Scouting Engine, future Recruiting, future Contracts, future Event Engine, and future Communication Engine.

Relationships are not global.

Relationships are directional.

Person A may trust Person B more than Person B trusts Person A.

## Version 1 Scope

- Relationship model
- Directional relationships
- Trust
- Respect
- Confidence
- Loyalty
- Influence
- Friendship
- Rivalry
- Relationship type
- Last interaction date
- Relationship history entries
- Relationship value changes
- Value clamping
- Basic decay over time
- Unit tests

## Not Version 1

- Full memory engine
- Full communication engine integration
- Rumor spread
- Family trees
- Group dynamics
- Locker room simulation
- Agent networks
- Media networks
- Recruiting decisions
- Contract negotiation

## Design Rule

Relationships are not simple happiness values.

They represent how one person views another person based on shared history, trust, confidence, respect, loyalty, influence, and emotional memory.

## Why This Comes Before Recruiting

Recruiting needs:

- Player trust
- Parent trust
- Scout influence
- GM reputation
- Organization relationship history

Those cannot be built cleanly until the Relationship Engine exists.
