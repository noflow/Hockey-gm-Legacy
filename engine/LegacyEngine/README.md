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

## Draft

Milestone 011 adds the standalone Draft v1 system under `LegacyEngine.Draft`.

The first build target includes:

- annual draft creation
- draft status
- draft order from reverse standings
- draft rounds and picks
- draft selections
- draft eligibility inputs
- draft board entries with scouting report references and confidence
- light RuleEngine validation for draft enabled, round validity, and eligibility
- light EventEngine event creation for draft start, selection, and completion

## Training Camp

Alpha 1.6 adds the first Integration-layer training camp loop.

The build target includes:

- training camp creation for an organization
- returning roster, drafted prospect, recruit, tryout, and AHL-style invite sources
- staff/scouting/development flavored camp evaluations
- keep, cut, release, assign to affiliate, return to parent, and mark injured decisions
- RuleEngine roster validation for the opening roster
- light EventEngine and inbox integration for important camp events

Training Camp v1 does not simulate games, lines, practices, waivers, salary cap, save/load, or full NHL/AHL transactions.

Draft v1 does not implement pick trading, draft lottery, live draft clock, roster changes, or War Room UI.

## Alpha Draft Experience

Alpha 1.4 adds a scenario-level draft experience under `LegacyEngine.Integration`.

The first playable draft loop includes:

- rulebook-driven draft length through the Rule Engine
- Junior Major, NHL-style, AHL-style, and Custom draft presets
- draft board movement, starred prospects, GM notes, confidence, projection, and analytics summaries
- scout focus assignment for draft preparation
- draft-day state with current round, total rounds, current pick, selecting team, and countdown placeholder
- AI drafting through the existing DraftEngine
- player selections through the existing DraftEngine
- draft recap with player selections, notable AI picks, owner reaction, and head scout reaction
- EventEngine events for draft start, drafted players, draft completion, draft board changes, scout recommendation updates, and owner draft reactions

Alpha 1.4 does not implement games, standings, playoffs, save/load, trades, draft lottery, conditional picks, multiplayer, or Godot integration.

## Rosters

Milestone 012 adds the standalone Roster Engine v1 system under `LegacyEngine.Rosters`.

The first build target includes:

- roster membership
- roster player status
- roster position
- roster moves and move results
- roster validation results
- light RuleEngine roster validation
- light EventEngine event creation for add, remove, injured reserve, and release moves

Roster v1 tracks who is on an organization. It does not build lines, depth charts, contracts, injuries, trades, waivers, salary cap, or gameplay simulation.

## Communication

Milestone 015 adds the standalone Communication Engine v1 under `LegacyEngine.Communication`.

The first build target includes:

- communication messages with source, recipients, channel, and severity
- communication channels (direct message, email, phone call, meeting, announcement, press release, rumor mill)
- communication visibility (private, organization, league, public)
- sending a message from one source to one or more recipients
- creating messages from Event Engine events
- rumors with reliability scores and derived confidence levels
- organization knowledge items
- conversion of important messages into inbox items (reusing the Integration `AlphaInboxItem`)
- queries by recipient, organization, visibility, and date range

Communication v1 turns events into messages, delivers them, tracks rumors and their confidence, and stores what an organization knows. It does not directly mutate People, Relationships, Owners, Rosters, or Rule Engine state, and it does not build UI, Godot scenes, or a save system.

## Staff

Milestone 017 adds the standalone Coaches & Staff Engine v1 under `LegacyEngine.Staff`.

The first build target includes:

- staff members composed of a profile, attributes, assignment history, and performance reviews
- fourteen v1 staff roles across coaching, scouting, medical, equipment, and management departments
- staff profile tracking person id, organization id, current role, department, years of experience, reputation, contract reference, and employment status
- coaching, scouting, and medical attribute scores (0–100)
- hire, assign role, reassign role, remove, evaluate, and record performance review behaviors
- staff evaluations returning an overall score, strengths, weaknesses, recommendation, and development suggestions
- light EventEngine event creation for StaffHired, StaffAssigned, StaffReassigned, StaffReleased, and StaffEvaluated
- contract references by id only, and Person id references only

Staff v1 is the organization staff system that future systems will build on. It does not implement firing logic, contract negotiation, roster interaction, relationship changes, practices, tactics, bench management, morale, development simulation, game simulation, UI, or Godot integration.

## Organizations

Milestone 018 adds the standalone Organization Engine v1 under `LegacyEngine.Organizations`.

The first build target includes:

- organizations with identity (name, city, region, country), type, and status
- organization types: Team, League, GoverningBody, Agency, School, and MediaCompany
- organization statuses: Active, Inactive, Folded, and Relocated
- optional owner person id, league id, and roster id references
- staff memberships (Person id references, optionally with a Staff role and department)
- organization departments, optionally mapped to a Staff department category
- budget references and facility references by id only
- culture values (development focus, winning pressure, financial discipline, community focus, innovation, loyalty)
- reputation values (local, league, national)
- add/remove staff membership, add/remove department, and change status behaviors
- light EventEngine event creation for OrganizationCreated, OrganizationStatusChanged, OrganizationStaffAdded, OrganizationStaffRemoved, OrganizationDepartmentAdded, and OrganizationDepartmentRemoved

Organization v1 is the shared organization foundation that future systems will build on. It references owners, leagues, rosters, budgets, and facilities by id only, and does not implement a Facilities engine, Finance engine, schedule generation, standings, game simulation, save/load, UI, or Godot integration.

## Seasons

Milestone 019 adds the standalone Season Engine v1 under `LegacyEngine.Seasons`.

The first build target includes:

- seasons with a year, status (Upcoming, Active, Completed), current phase, and current date
- season phases: Preseason, RegularSeason, TradeDeadline, Playoffs, Championship, Offseason, Recruiting, Draft, and FreeAgency
- a season calendar of twelve milestones (training camp, season begins, trade deadline, playoffs, championship, awards, recruiting open/close, draft lottery, draft, and free agency open/close)
- rulebook-driven timing: an optional `season_rules` section supplies the season start and each milestone's day offset, so league dates live in data (a neutral default is used only when no rules are supplied)
- creating a season, advancing its date, detecting milestone dates, and changing phase automatically
- a SeasonResult describing the previous/current date and phase, milestones reached, phase transitions, and created events
- independent calendars per league, so multiple leagues advance on their own schedules
- a WorldEngine query extension that answers "what season phase are we currently in?"
- light EventEngine event creation for SeasonCreated, PhaseChanged, MilestoneReached, RecruitingOpened, RecruitingClosed, DraftOpened, DraftClosed, FreeAgencyOpened, and FreeAgencyClosed

Season v1 controls time, league phases, and scheduled hockey events. It does not simulate games and does not implement schedule generation, standings, playoff brackets, awards voting, statistics, save/load, UI, or Godot integration.
