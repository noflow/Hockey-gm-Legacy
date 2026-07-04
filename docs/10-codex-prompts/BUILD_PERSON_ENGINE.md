# Codex Prompt – Build Person Engine

Read:

- PROJECT_STATUS.md
- DEVELOPMENT_CONSTITUTION.md
- docs/00-charter/PROJECT_CHARTER.md
- docs/09-implementation-specs/PERSON_ENGINE_IMPLEMENTATION_SPEC.md
- Existing Rule Engine code
- Existing Owners code
- Existing Scouting code

Then implement Milestone 005 – Person Engine.

Requirements:

- Use C#.
- Do not build UI.
- Do not use Godot-specific code.
- Create a standalone module under `engine/LegacyEngine/People`.
- Keep the engine testable without Godot.

Implement:

- Person
- PersonStatus
- PersonRole
- PersonRoleType
- PersonalityProfile
- CareerTimelineEntry
- Basic PersonEngine service or manager if useful.

Required behavior:

- Create a person.
- Calculate age from birth date and current date.
- Add a role.
- End a role.
- Support multiple roles.
- Track reputation.
- Clamp reputation between 0 and 100.
- Change person status.
- Add career timeline entries.

Add unit tests for:

- Person creation.
- Age calculation.
- Adding role.
- Ending role.
- Multiple roles.
- Reputation changes.
- Reputation clamping.
- Status changes.
- Timeline entries.

Do not build:

- Recruiting
- Contracts
- Trades
- Game simulation
- Relationship Engine
- Memory Engine
- UI
- Godot integration

After implementation, verify:

```powershell
dotnet build HockeyGmLegacy.slnx --no-restore
dotnet run --project tests/LegacyEngine.Tests
```
