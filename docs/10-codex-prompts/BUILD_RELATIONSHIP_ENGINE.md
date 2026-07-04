# Codex Prompt – Build Relationship Engine

Read:

- PROJECT_STATUS.md
- DEVELOPMENT_CONSTITUTION.md
- docs/00-charter/PROJECT_CHARTER.md
- docs/09-implementation-specs/RELATIONSHIP_ENGINE_IMPLEMENTATION_SPEC.md
- Existing Rule Engine code
- Existing Owners code
- Existing Scouting code
- Existing Person Engine code

Then implement Milestone 006 – Relationship Engine.

Requirements:

- Use C#.
- Do not build UI.
- Do not use Godot-specific code.
- Create a standalone module under `engine/LegacyEngine/Relationships`.
- Keep the engine testable without Godot.

Implement:

- Relationship
- RelationshipType
- RelationshipDimension
- RelationshipHistoryEntry
- RelationshipChange if useful
- Basic RelationshipEngine service or manager if useful.

Required behavior:

- Create a directional relationship.
- Support independent reverse relationships.
- Track trust, respect, confidence, loyalty, influence, friendship, and rivalry.
- Clamp all values between 0 and 100.
- Update LastInteractionDate when values change.
- Add history entries when values change.
- Support basic decay over time.

Add unit tests for:

- Creating relationship.
- Directionality.
- Reverse relationship independence.
- Value changes.
- Value clamping.
- LastInteractionDate updates.
- History entry creation.
- Multiple history entries.
- Decay behavior.

Do not build:

- Recruiting
- Contracts
- Trades
- Game simulation
- Event Engine
- Memory Engine
- Communication Engine
- UI
- Godot integration

After implementation, verify:

```powershell
dotnet build HockeyGmLegacy.slnx --no-restore
dotnet run --project tests/LegacyEngine.Tests
```
