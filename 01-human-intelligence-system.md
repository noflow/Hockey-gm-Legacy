# Architecture Revision 001 – Rule Engine Adoption

**Status:** Accepted  
**Decision Type:** Core Architecture  
**RFC:** RFC-006

## Decision

Hockey GM Legacy will use a configurable **Rule Engine**. No major hockey rule should be hardcoded directly into the simulation engine.

Each league uses a **Rulebook** that defines how that league operates.

## Updated Architecture

```text
World
│
├── Leagues
│   └── Rulebooks
│
├── Organizations
├── People
├── Events
├── Relationships
├── Communication
├── History
└── UI
```

## Rule Engine Responsibilities

The Rule Engine answers:

- Is this player eligible?
- Can this contract be signed?
- Is this trade legal?
- Does this player require waivers?
- Can this team exceed budget?
- How many imports are allowed?
- How is the draft order determined?
- Who qualifies for playoffs?
- What contract clauses are valid in this league?

## Principle 13

**Nothing in the simulation should assume one league.**

Every hockey rule should come from a configurable Rulebook.
