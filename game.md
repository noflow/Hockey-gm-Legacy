# Core Architecture v1

The engine comes before the interface. Godot displays the simulation; it does not contain it.

## Core Engine Objects

- World
- Person
- Role
- Organization
- League
- Rulebook
- Event
- Relationship
- History

## UI Rule

The UI requests an action. The engine validates it. The engine creates an event. The event updates the world.
