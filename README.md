# Hockey GM Legacy – Codex v1.0

**Tagline:** You are not building a hockey team. You are building hockey lives.

Hockey GM Legacy is a living hockey universe simulator where the player builds a lifelong executive career, beginning in junior hockey and potentially rising through the professional ranks.

## Start Here

1. [Project Charter](docs/00-charter/PROJECT_CHARTER.md)
2. [Manifesto](docs/00-charter/MANIFESTO.md)
3. [Documentation Index](docs/INDEX.md)
4. [MVP Scope](docs/00-charter/MVP_V1_SCOPE.md)
5. [Rule Engine Implementation Spec](docs/09-implementation-specs/RULE_ENGINE_IMPLEMENTATION_SPEC.md)

## Current Implementation Anchor

The current implementation is focused on standalone LegacyEngine modules:

- Rule Engine
- Owners
- Scouting
- People
- Relationships
- Events
- World
- Recruiting
- Contracts
- Draft
- Rosters
- Player Development
- Injuries
- Alpha Integration
- Alpha Console
- Human Intelligence
- Alpha Daily Simulation Pipeline
- Alpha Desktop
- Training Camp
- Season Readiness
- Executive Reports
- Staff & Scouting Operations

Alpha 1.3 - GM Character Creation + First GM Actions starts AlphaDesktop on a GM creation screen, then drops the created GM into the Prairie Falcons scenario two weeks before the draft. The player can review the club, re-rank the draft board, assign a scout focus, make a recruiting offer, and advance days to process responses.

Alpha 1.4 - Complete Draft Experience adds a playable draft loop. The player can prepare the draft board, star prospects, add GM notes, assign scouting focuses, reach draft day, let AI teams draft, make selections, receive reactions, complete the draft, and review a draft recap. Draft length comes from the active RuleEngine rulebook.

Alpha 1.6 - Training Camp + Roster Cutdown v1 adds the first post-draft camp loop. After draft/offseason setup, the player can open camp, review returning players, drafted prospects, recruits, and AHL-style invite sources, generate staff evaluations, make keep/cut/assign/return decisions, and complete camp with RuleEngine roster validation.

Alpha 1.8 - Opening Roster & Season Readiness adds the Opening Night gate. The desktop now shows roster compliance, unresolved GM decisions, owner/coach/scout reviews, staff recommendations, and a checklist. Begin Season stays blocked until the roster, camp, prospect, and pending-action requirements are resolved by the player.

Alpha 1.8.1 - Executive Reports adds permanent in-memory career reports for each season. Front Office Readiness is archived when Opening Night requirements are complete, and End of Season Executive Review is archived after the SeasonEngine marks a season completed. Reports include owner, coach, scout, development, medical, roster compliance, organization health, progress, and executive summary sections without adding game simulation, standings, playoffs, or save/load.

Alpha 1.9 - Staff & Scouting Operations v1 deepens daily scouting. The GM can review scout profiles, relationships, workload, strengths, weaknesses, and conflict warnings; assign scouts to regions or specific prospects; advance days to complete reports; and use basic staff controls to reassign, release, or hire a placeholder staff candidate. Report confidence now reflects scout fit, workload, and relationship/communication quality.

Inbox v2 organizes GM messages into category tabs, supports read/unread, archive, delete, and pin state, and keeps Event Engine history intact.

Alpha 1.2 refines the desktop inbox into an email-style GM workspace with a category sidebar, message list, reading pane, row-level actions, and local filters.

## Alpha Console

Run the engine-only playtest harness:

```bash
dotnet run --project tools/AlphaConsole
```

Run the basic desktop UI:

```bash
dotnet run --project client/AlphaDesktop
```

The desktop playtest harness starts with GM creation and draft preparation, supports Staff & Scouting Operations, then unlocks training camp after draft/offseason setup, Season Readiness before Opening Night, and Executive Reports for the career archive. Smoke tests and the console harness keep a Jordan Hayes fallback when no custom GM is supplied.

Available commands:

- `help`
- `status`
- `inbox`
- `owner`
- `staff`
- `roster`
- `recruits`
- `scouting`
- `draftboard`
- `relationships`
- `advance`
- `advance 7`
- `exit`

First rulebook:

`data/rulebooks/junior_v1.json`

## Repository Structure

```text
docs/
  00-charter/
  01-game-bible/
  02-technical-bible/
  03-ai-bible/
  04-communication-systems/
  05-hockey-bible/
  06-ui-ux-bible/
  07-milestones/
  08-rfcs/
  09-implementation-specs/
  10-codex-prompts/

data/
  rulebooks/
  templates/

engine/
  LegacyEngine/

client/
  GodotClient/

tests/
tools/
```

## Architecture Pillars

1. Legacy Engine — the living world.
2. Event Engine — what happens.
3. Human Intelligence System — how people think.
4. Communication Engine — who knows what.
5. Rule Engine — what each league allows.
