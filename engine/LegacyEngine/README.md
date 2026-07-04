# LegacyEngine

This folder will contain the simulation engine.

First build target:

`RuleEngine`

The engine should be testable without Godot.

## RuleEngine

Milestone 002 adds a standalone C# RuleEngine module under `LegacyEngine.RuleEngine`.

It loads JSON rulebooks and validates:

- roster limits
- player eligibility
- contract types, clauses, and salary caps
- draft availability, eligibility, and rounds
- playoff qualification counts
- owner budgets and hard salary caps

Run the milestone tests from the repository root:

```powershell
$env:APPDATA = (Join-Path (Get-Location) '.appdata')
dotnet run --project tests/LegacyEngine.Tests/LegacyEngine.Tests.csproj --no-restore
```

## Owners

Milestone 003 starts the standalone Owners system under `LegacyEngine.Owners`.

The first build target includes:

- owner model
- owner archetypes and archetype profiles
- budgets
- goals
- trust, confidence, and patience
- autonomy levels
- owner evaluation results

Owners set resources and expectations, then evaluate the GM. They do not approve every hockey decision.

## Scouting

Milestone 004 starts the standalone Scouting system under `LegacyEngine.Scouting`.

The first build target includes:

- scout model
- scout specialties
- scouting assignments
- imperfect scouting reports
- confidence levels
- player dossier structure
- GM personal scouting bonus

Scouting reduces uncertainty. It reports facts, observations, opinions, unknowns, and confidence without exposing hidden true ratings by default.

## People

Milestone 005 starts the standalone Person Engine under `LegacyEngine.People`.

The first build target includes:

- person model and identity
- gender, birth date, age calculation, nationality, and birthplace
- active, retired, and deceased status
- role system with multiple roles per person
- role start and end dates
- basic reputation
- basic personality profile
- career timeline entries

Every human in the game starts as a Person. Roles attach to people; they do not replace them.

## Relationships

Milestone 006 adds the standalone Relationship Engine under `LegacyEngine.Relationships`.

The first build target includes:

- directional relationships
- independent reverse relationships
- relationship types
- trust, respect, confidence, loyalty, influence, friendship, and rivalry
- relationship changes with clamped values
- last interaction tracking
- relationship history entries
- conservative decay over time

People are permanent. Roles define what they do. Relationships define how they feel about each other.
