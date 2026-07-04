# Relationship Engine Architecture

## Purpose

The Relationship Engine connects people.

Every relationship is directional.

```text
Person A → Person B
```

is different from:

```text
Person B → Person A
```

## Core Dimensions

- Trust
- Respect
- Confidence
- Loyalty
- Influence
- Friendship
- Rivalry

## Core Objects

```text
Relationship
│
├── From Person
├── To Person
├── Relationship Type
├── Dimensions
├── Last Interaction
└── History
```

## Boundaries

The Relationship Engine does not own:

- Events
- Memories
- Recruiting
- Contracts
- Trades
- Communication spread
- UI

Those systems may reference or modify relationships through approved services.
