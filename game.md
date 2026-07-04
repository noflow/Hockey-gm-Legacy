# Core Architecture v1

## Architecture Rule

The engine comes before the interface. Godot displays the simulation. It does not contain the simulation.

## Project Structure

```text
HockeyGMLegacy/
├── LegacyEngine/
│   ├── World/
│   ├── People/
│   ├── Organizations/
│   ├── Events/
│   ├── Relationships/
│   ├── Rules/
│   ├── Hockey/
│   ├── History/
│   ├── AI/
│   ├── Data/
│   └── Tests/
├── GameClient/
├── Data/
└── Docs/
```

## Core Engine Objects

World, Person, Role, Organization, Rulebook, Event, Relationship, History.

## Core Technical Rule

The UI must never directly change game state. The UI requests an action. The engine validates it. The engine creates an event. The event updates the world.
