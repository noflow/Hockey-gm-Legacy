# Core Architecture v1

## Rule

The engine comes before the interface.

Godot displays the simulation. It does not contain the simulation.

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

The UI must never directly change game state.

The UI requests an action.  
The engine validates it.  
The engine creates an event.  
The event updates the world.
