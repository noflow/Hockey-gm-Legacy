# Injury Engine Implementation Spec

## Scope

Milestone 014 implements Injury Engine v1 as a standalone LegacyEngine module.

The module has no UI dependency, no Godot dependency, no gameplay simulation dependency, and no automatic roster movement.

## Responsibilities

- Create injuries for people.
- Track injury date.
- Track body part.
- Track injury type.
- Track severity.
- Track expected and actual return dates.
- Track games missed.
- Track status.
- Track recurrence risk.
- Track long-term impact.
- Apply recovery updates.
- Clear injuries.
- Re-aggravate injuries.
- Mark injuries as career-threatening.
- Queue injury events through EventEngine.

## Development Integration

Injury Engine exposes injury penalty values through Injury and InjuryRiskProfile.

Player Development can consume those values later, but Injury Engine v1 does not directly modify Development state.

## Roster Integration

Injury Engine exposes roster availability information through injury status.

Roster Engine can consume that information later, but Injury Engine v1 does not automatically move players to injured reserve.

## Events

Injury Engine v1 may queue:

- PlayerInjured
- PlayerRecovered
- InjuryReAggravated
- InjuryCareerThreatening

## Out of Scope

- Game simulation
- Automatic injury generation from games
- Medical staff system
- Surgery system
- Full rehab system
- Roster automatic injured reserve movement
- Save system
- UI
- Godot integration
