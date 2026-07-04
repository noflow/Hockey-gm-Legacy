# Alpha Integration Layer Implementation Spec

## Scope

Alpha 0.1 creates the first simple playable engine loop without UI, Godot, save/load, schedule generation, or game simulation.

## Responsibilities

- Create a simple alpha world using existing LegacyEngine modules.
- Assemble world state, owner, GM/person, scout/person, players, recruit profiles, roster, and draft board.
- Share one EventEngine through the registered services.
- Advance one day through WorldEngine.
- Process queued events.
- Convert important processed events into inbox items.
- Return an AlphaSimulationResult with date, processed event count, inbox items, summary text, and world snapshot.

## Boundaries

The integration layer coordinates existing modules. It does not own simulation logic that belongs inside domain engines.

## Out of Scope

- UI
- Godot integration
- Game simulation
- Schedule generation
- Save/load
- Database persistence
- Full season flow
- Trades
- Salary cap
- Waivers
