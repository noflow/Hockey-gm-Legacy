# Architecture Revision 1
## Rule Engine Adoption

**Status:** Accepted  
**RFC:** RFC-006  
**Decision Type:** Core Architecture

## Decision

Hockey GM Legacy will use a configurable Rule Engine. No major hockey rule should be hardcoded directly into the simulation engine. Each league will use a Rulebook that defines how that league operates.

## Why This Matters

The game must eventually support junior hockey, minor pro hockey, professional hockey, European hockey, college-style hockey, international tournaments, custom leagues, historical leagues, and modded leagues.

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

Nothing in the simulation should assume one league. Every hockey rule should come from a configurable Rulebook.

## Foundation Engines

1. Legacy Engine
2. Event Engine
3. Human Intelligence System
4. Communication Engine
5. Rule Engine
