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

## Events

Milestone 007 adds the standalone Event Engine under `LegacyEngine.Events`.

The first build target includes:

- legacy events
- event type, severity, visibility, and status
- optional event context for related people, organizations, leagues, seasons, games, relationships, and rulebooks
- event metadata
- event queue
- date-ordered processing
- processed event archival
- event history queries

The v1 Event Engine records, queues, processes, and archives events. It does not directly mutate People, Relationships, Owners, Scouting, or Rule Engine state.

## World

Milestone 008 adds the standalone World Engine under `LegacyEngine.World`.

The first build target includes:

- world identity
- world clock and date
- world phase
- world settings
- world state
- daily simulation results
- simple EventEngine coordination

The v1 World Engine coordinates time and queued event processing only. It does not directly mutate People, Owners, Scouting, Relationships, or Rule Engine state.

## Recruiting

Milestone 009 adds the standalone Recruiting v1 system under `LegacyEngine.Recruiting`.

The first build target includes:

- recruit profiles
- recruit status
- recruit priorities
- organization interest tracking
- recruiting pitches
- recruiting promises
- recruiting visits
- recruiting decision results
- light EventEngine event creation for offers, commitments, and rejections

Recruiting v1 considers opportunity, development, education, family comfort, promises, trust, facilities, and pathway. It does not create contracts or modify rosters.

## Contracts

Milestone 010 adds the standalone Contracts v1 system under `LegacyEngine.Contracts`.

The first build target includes:

- contract offers
- contract type and status
- contract term and money
- contract clauses
- contract decisions and results
- junior player agreement clauses
- light EventEngine event creation for offers, signatures, rejections, and terminations
- optional RuleEngine validation for league-disallowed clauses

Contracts v1 supports junior agreements and staff/GM/scout/coach contracts. It does not implement a full CBA, salary cap machinery, LTIR, arbitration, offer sheets, buyouts, trades, or roster modification.
