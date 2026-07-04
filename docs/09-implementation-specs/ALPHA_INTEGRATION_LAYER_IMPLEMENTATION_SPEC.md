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

## Alpha 0.3 Daily Pipeline

The daily simulation pipeline runs these steps in stable order:

1. Advance world clock.
2. Process queued events.
3. Apply relationship decay.
4. Apply player development updates.
5. Apply injury recovery updates.
6. Check contract statuses when supported.
7. Progress recruiting interest lightly.
8. Generate alpha communication messages from important processed events.
9. Convert important messages into inbox items.
10. Return AlphaSimulationResult with snapshot, inbox, communication messages, and log entries.

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
