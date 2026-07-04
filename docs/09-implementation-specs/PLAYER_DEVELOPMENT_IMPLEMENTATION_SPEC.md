# Player Development Implementation Spec

## Scope

Milestone 013 implements Player Development v1 as a standalone LegacyEngine module.

The module has no UI dependency, no Godot dependency, and no gameplay simulation dependency.

## Responsibilities

- Create a development profile for a person.
- Track hidden current ability.
- Track hidden potential.
- Track development stage.
- Track development attributes.
- Apply deterministic monthly development updates.
- Produce development results with internal and player-facing summaries.
- Queue development events through EventEngine.

## Development Inputs

Monthly updates may consider:

- Age
- Work ethic
- Coachability
- Confidence
- Ice time input
- Facility bonus
- Coaching bonus
- Injury penalty
- Random modifier for deterministic variation

## Hidden Ratings

Current ability and potential are engine values.

Potential is not exposed through dossier-facing output. Player-facing development summaries must remain qualitative.

## Events

Player Development v1 may queue:

- PlayerDevelopmentUpdated
- PlayerBreakout
- PlayerRegression

The module does not directly mutate People, Scouting, Relationships, Rosters, World, or gameplay systems.

## Out of Scope

- Injury Engine
- Game simulation
- Line combinations
- Full coaching system
- Full facility system
- Save system
- UI
- Godot integration
