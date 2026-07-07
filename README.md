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
- Player Dossiers
- Staff Control
- Selectable People Rows
- Alpha 2.2 UI/UX Structural Pass
- Alpha 2.2.1 Dossier Window, Roster Filters, Budget Overview, and Scouting Cleanup
- Alpha 2.3 Recruiting v2
- Alpha 2.3.1 Name Generation System + Deduping
- Alpha 2.4 Staff Control v2 + Hockey Operations Budget
- Alpha 2.5 Season Framework v1
- Alpha 2.6 Game Recap + Stats Polish
- Alpha 2.7 First Month Playability Pass
- Alpha 2.7.1 Roster & Front Office Realism Pass
- Alpha 2.7.2 Inbox Cleanup + League Transaction Wire
- Alpha 2.7.3 Live Draft Layout + Staff Hiring Layout Fix
- Alpha 2.8 GM Office Navigation Redesign
- Alpha 2.9 Action Center & Daily Workflow UI
- Alpha 3.0 Existing World History v1
- Alpha 3.1 Free Agent Market v1
- Alpha 3.2 Trade Engine v1
- Alpha 3.3 Trade Deadline Event v1
- Alpha 3.4 Career & History Framework v1
- Alpha 3.5 Save/Load v1
- Alpha 4.0 Multi-Season Playability v1
- Alpha 4.1 Contracts v2
- Alpha 4.2 Free Agency v2
- Alpha 4.3 Trade Engine v2 (Negotiation & Team Strategy)
- Alpha 4.4 Scouting v2 (Intelligence & Reports)
- Alpha 4.5 Player Development v2 (Development Plans & Progress)
- Alpha 4.6 Staff & Coaching v3 (Philosophy & Development)
- Alpha 4.7 Injury & Medical v2 (Health & Recovery)
- Alpha 4.8 Owner & Job Security v2
- Alpha 4.9 League AI & Team Identity v2

Alpha 1.3 - GM Character Creation + First GM Actions starts AlphaDesktop on a GM creation screen, then drops the created GM into the Prairie Falcons scenario two weeks before the draft. The player can review the club, re-rank the draft board, assign a scout focus, make a recruiting offer, and advance days to process responses.

Alpha 1.4 - Complete Draft Experience adds a playable draft loop. The player can prepare the draft board, star prospects, add GM notes, assign scouting focuses, reach draft day, let AI teams draft, make selections, receive reactions, complete the draft, and review a draft recap. Draft length comes from the active RuleEngine rulebook.

Alpha 1.6 - Training Camp + Roster Cutdown v1 adds the first post-draft camp loop. After draft/offseason setup, the player can open camp, review returning players, drafted prospects, recruits, and AHL-style invite sources, generate staff evaluations, make keep/cut/assign/return decisions, and complete camp with RuleEngine roster validation.

Alpha 1.8 - Opening Roster & Season Readiness adds the Opening Night gate. The desktop now shows roster compliance, unresolved GM decisions, owner/coach/scout reviews, staff recommendations, and a checklist. Begin Season stays blocked until the roster, camp, prospect, and pending-action requirements are resolved by the player.

Alpha 1.8.1 - Executive Reports adds permanent in-memory career reports for each season. Front Office Readiness is archived when Opening Night requirements are complete, and End of Season Executive Review is archived after the SeasonEngine marks a season completed. Reports include owner, coach, scout, development, medical, roster compliance, organization health, progress, and executive summary sections without adding game simulation, standings, playoffs, or save/load.

Alpha 1.9 - Staff & Scouting Operations v1 deepens daily scouting. The GM can review scout profiles, relationships, workload, strengths, weaknesses, and conflict warnings; assign scouts to regions or specific prospects; advance days to complete reports; and use basic staff controls to reassign, release, or hire a placeholder staff candidate. Report confidence now reflects scout fit, workload, and relationship/communication quality.

Alpha 2.0 - Player Dossier v1 + Name Cleanup adds clean player display names and a GM-facing dossier. The desktop can open player/prospect dossiers from roster, recruits, scouting, draft board, prospect list, and training camp contexts, with facts, scouting language, development summary, medical context, contract/rights status, staff opinions, relationships, and editable GM notes. Dossiers keep internal ability and potential values private.

Alpha 2.1 - Staff Control v2 turns the Staff tab into a front-office workspace. The GM can review full staff profiles, contract references, strengths, weaknesses, GM relationships, chemistry warnings, current assignments, current focus areas, candidate pool recommendations, and recent evaluations. Staff actions now include candidate generation/hiring, role changes, releases, development/medical/scouting focus changes, and staff evaluation messages while preserving Alpha 1.9 Scouting Operations.

The Alpha UI Interaction Pass replaces major text-dump people screens with selectable rows and detail/action panels. Staff, roster, recruits, scouting, scouting operations, draft board, prospect list, training camp, and dossier entry points now let the GM select the person first, then take valid staff/player/prospect actions from that selected detail panel.

Alpha 2.2 - UI/UX Structural Pass v1 turns AlphaDesktop into a more playable GM workspace. The dashboard now uses cards for current date, draft/training camp countdowns, unread inbox, pending decisions, roster issues, and scouting reports, with quick actions for advancing time and jumping to Inbox, Draft Board, and Pending Actions. Main tabs now show simple notification counts while the Inbox v2 layout remains intact.

Alpha 2.2.1 - Dossier Window, Roster Filters, Budget Overview, and Scouting Cleanup removes the permanent Player Dossier tab in favor of selected-person dossier windows with editable GM notes. The roster now has search/filter controls and richer non-hidden summary fields, Dashboard and Owner views show a simple Integration-level budget overview, and scouting assignments now use duration/return-date dialogs with available scouts only.

Alpha 2.3 - Recruiting v2 makes recruit management a front-office workflow. Recruits now expose richer priorities, family concerns, decision style, scouting context, competitor pressure, offers, promises, and GM notes. The desktop recruit panel supports calls, family calls, visit invites, offers, education-package promises, recruiting promises, scout information requests, offer withdrawals, and dossier access, with inbox messages and explainable recruit decisions.

Alpha 2.3.1 - Name Generation System + Deduping adds a standalone regional name generator for long-running player and staff creation. Generated names come from national/regional pools, never use visible numeric suffixes, keep uniqueness through PersonId, dedupe recruit/draft/scouting rows by person ID, and clarify true same-name players with position, age, and region/team context.

Alpha 2.4 - Staff Control v2 + Hockey Operations Budget adds league-driven staff and GM salary ranges, candidate salary asks, active staff salary impact, released-staff obligations, and a hockey operations budget breakdown. The desktop now shows GM salary, coaching, scouting, medical/training, staff total, player contracts, remaining budget, and owner warnings when hiring pushes the club over budget.

Alpha 2.5 - Season Framework v1 adds the first basic regular-season loop. Beginning the season now generates a league schedule, Advance Day and Advance 7 simulate scheduled games, standings update, team/player/goalie stats accumulate, game recap inbox messages are created, and AlphaDesktop exposes Schedule, Standings, and Stats tabs plus a dashboard next-game card.

Alpha 2.6 - Game Recap + Stats Polish turns simulated results into readable GM information. Completed games now create boxscore-style recaps with final score, shots/power-play placeholders, three stars, notable players, goalie summary, light medical/development notes, and narrative summaries. The desktop dashboard now shows last game, next game, and team record; Schedule separates today/upcoming/recent results and recaps; Standings show rank and goal differential; Stats now show team, league, skater, and goalie leaders without exposing hidden ratings.

Alpha 2.7 - First Month Playability Pass makes time advancement more playable. The desktop now supports Advance Day, Advance Week, Advance to Next Game, and Advance to Month End, with clear stop reasons for player games, injuries, urgent GM decisions, roster problems, scouting reports, and month-end reports. Inbox messages now carry priority, urgent/unread/newest sorting is clearer, routine league-wide games stay out of the GM inbox, and monthly GM summaries collect record, standings, owner/staff, medical, development, roster, budget, scouting, and pending-action context.

Alpha 2.7.1 - Roster & Front Office Realism Pass updates the junior roster target to 26 players, starts the New GM scenario with a legal roster, adds realistic draft prospect bios with physical/team/background context, expands hockey operations staff roles, adds rulebook-driven staff limits and vacancies, and improves staff candidate hiring information with salary, role, employer, experience, strengths, weaknesses, and chemistry risk.

Alpha 2.7.2 - Inbox Cleanup + League Transaction Wire keeps the GM inbox focused on your organization and decisions that need attention. Other-team signings, contract updates, roster moves, injuries, draft picks, and staff transactions now route into a separate League News / Transaction Wire feed with team, player/staff, category, date, and description.

Alpha 2.7.3 - Live Draft Layout + Staff Hiring Layout Fix reshapes the live draft modal into selected prospect, draft list, and draft status columns. Draft rows now surface basic visible bio and scouting context, dossier overviews include draft bio facts, and the Staff tab separates Current Staff, Hire Staff / Staff Candidates, and Vacancies so candidate actions no longer look like staff actions.

Alpha 3.4 - Career & History Framework v1 starts the long-save memory layer. The engine now seeds GM, staff, organization, and player career history, tracks player draft picks and draft classes, adds Where Are They Now records, records trade/free-agent/injury history, and expands Reports / History with GM Career, Organization History, Draft History, Drafted Players, Player Career Timelines, Staff History, and Transaction History views without exposing hidden ratings.

Alpha 3.5 - Save/Load v1 adds local JSON career saves. AlphaDesktop can start a new career, load an existing career, save the current career, or save as a new file. Saves include the current scenario snapshot, roster, staff, contracts, prospects, free agents, trades, schedule, standings, stats, inbox statuses, league news, pending GM actions, Action Center statuses, scouting/recruiting/training camp state, season readiness, budget snapshot, executive/monthly reports, and career/draft/organization history. Save files are local `.json` files under `%LOCALAPPDATA%\\HockeyGmLegacy\\Saves` by default.

Alpha 4.0 - Multi-Season Playability v1 proves the Alpha career can roll from one season into the next. Once the regular-season schedule is complete, the GM can finish the season, archive final standings, stats, recaps, and organization history, generate an End of Season Executive Review, enter the offseason, create the next draft class, convert expiring contracts into explicit pending GM decisions, reset current-season stats, preserve prior-season and career history, and save/load the career after rollover.

Alpha 4.1 - Contracts v2 adds explainable contract asks and offer evaluation without adding a full agent system. The GM can review contract asks for roster players, prospects, recruits, free agents, and staff; see salary, term, desired role, budget before/after, common expiry date, likelihood, risk warnings, and reasons. Accepted terms create pending GM approval actions and never sign automatically.

Alpha 4.2 - Free Agency v2 turns unsigned player shopping into a calendar-driven market. Free agency now has opening, active, slow, late, and closed phases; players expose top motivations, competing offers, staff recommendations, budget impact, offer likelihood, response timing, and decision explanations. Accepted offers still become pending GM approvals first, while other-team signings flow to League News instead of cluttering the GM inbox.

Alpha 4.3 - Trade Engine v2 (Negotiation & Team Strategy) makes AI trade logic depend on club direction, roster needs, prospect pipeline, budget pressure, owner/team context, and GM trade personality. Trade evaluations now explain asset fit, need matching, budget impact, simple counter requests, staff/player reactions, and estimated value without exposing hidden ratings. Multi-asset trades continue to require explicit GM approval before any roster or rights change.

Alpha 4.6 - Staff & Coaching v3 (Philosophy & Development) makes staff a living hockey operations layer. Coaches and staff now surface philosophy, specialties, personality, staff chemistry, player fit, monthly staff meetings, department grades, organization chart context, hiring fit, player dossier staff opinions, Action Center coaching items, and staff performance reviews without adding Godot, save/load changes, or game simulation changes.

Alpha 4.7 - Injury & Medical v2 (Health & Recovery) turns injuries into a GM health-management system. Roster players now have health profiles with durability, fatigue, recovery rate, injury risk, wear and tear, recurring concerns, medical confidence, and conditioning. Medical reports explain return timing, risk, staff confidence, and recommended return-to-play choices, while dossiers, Action Center, organization health, and executive reports surface medical context without changing save/load or game simulation.

Alpha 4.8 - Owner & Job Security v2 makes ownership a visible accountability layer. The owner now has a richer personality profile, season expectations, confidence/pressure/support state, scheduled meetings, letters, budget and strategy decisions, GM performance review, job security explanation, Action Center items, executive report context, and career-history hooks. Alpha 4.8 creates warnings and pressure only; it does not add automatic firing, job offers, save/load changes, or game simulation changes.

Alpha 4.9 - League AI & Team Identity v2 makes organizations recognizable by how they build. Every league team now receives an identity, AI GM personality, owner influence, current strategy, monthly needs, budget style, draft style, scouting focus, development grade, trade/free-agency/draft/scouting behavior, occasional League News direction headlines, and organization-history hooks. These profiles are deterministic and in-memory for Alpha, with no media engine, save/load changes, or game simulation changes.

Alpha 4.4 - Scouting v2 (Intelligence & Reports) turns scouting into an information system instead of a rating reveal. Scouts now have tendencies, known regions, specialties, workload effects, confidence stars, viewing samples, tournament context, budget coverage notes, report comparisons, and scout career/development summaries. Player dossiers show multiple report-card style opinions, evidence, disagreements, and confidence while keeping hidden ratings private.

Alpha 4.5 - Player Development v2 (Development Plans & Progress) turns development into a GM-managed pathway. Tracked players now receive development plans with focus areas, ice-time roles, confidence, morale, coach comments, coach recommendations, yearly reviews, dossier development context, Action Center items, and career timeline hooks. Development remains player-facing and explainable while hidden ability and potential values stay private.

Alpha 2.8 - GM Office Navigation Redesign replaces the crowded top-level AlphaDesktop tabs with a cleaner GM Office shell: Dashboard, Inbox, Organization, Hockey Operations, Season, Reports / History, and Settings. Existing features are preserved inside workspace sub-navigation, the dashboard now acts as an action center with owner mood and grouped advance controls, and League News remains near the Inbox without adding save/load, Godot, or new game simulation behavior.

Alpha 2.9 - Action Center & Daily Workflow UI makes the dashboard answer "What should I do today?" The desktop now aggregates pending GM actions, urgent inbox messages, roster warnings, staff vacancies, budget warnings, scouting completions, upcoming games, injury issues, and season readiness into a filterable Action Center with daily agenda, Assistant GM recommendations, priority counts, and resolve/defer/dismiss workflow. It does not auto-complete GM decisions.

Alpha 3.0 - Existing World History v1 gives the New GM scenario a lived-in past. Roster players and prospects now have prior stats, older players have multi-year summaries, staff can carry career timeline entries, the organization has a prior-season record, and dossiers/roster views expose history without revealing hidden ratings.

Alpha 3.1 - Free Agent Market v1 adds unsigned player management to Hockey Operations. The scenario now generates a clean-name free-agent market with veterans, released players, undrafted players, depth skaters, goalies, prior stats, contract asks, interest, fit summaries, and staff recommendations. The GM can view dossiers, shortlist players, invite players to camp, submit offers, withdraw offers, and approve/decline pending signings; contracts are created only after explicit GM approval.

Alpha 3.2 - Trade Engine v1 adds the first player-controlled trade flow. The GM can review a league trade block, inspect available players, build simple offers from roster players, prospect rights, or draft-pick placeholders, propose trades, receive AI accept/reject/counter feedback, and complete accepted trades only through explicit Pending GM Action approval. Completed trades create league news while the GM inbox stays focused on team-relevant trade decisions.

Alpha 3.3 - Trade Deadline Event v1 makes the deadline a calendar-driven league event. The engine tracks approaching, deadline week, deadline day, and closed states from the SeasonEngine calendar, expands the trade block near the deadline, creates limited rumors and owner/coach/assistant pressure messages, adds deadline items to the Action Center and League News, and blocks new trade proposals after the deadline until the playoff/offseason window. Accepted trades created before the deadline remain valid pending GM approval.

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

The desktop playtest harness starts with GM creation and draft preparation, supports Staff & Scouting Operations, Scouting v2 report comparisons, Player Development v2 plans/reviews, Staff Control, Staff Candidate hiring with salary asks, staff vacancies, Hockey Operations Budget, Recruiting v2, Free Agency v2, Contracts v2, Trade Engine v2, Trade Deadline Event v1, Player Dossier windows, roster filters, selectable person-specific action panels, a League News transaction wire, and a card-based dashboard with notification counts, then unlocks training camp after draft/offseason setup, Season Readiness before Opening Night, Executive Reports for the career archive, and a basic Schedule/Standings/Stats season loop with readable game recaps after Begin Season. The first-month flow adds smarter advance controls, priority inbox handling, monthly GM summaries, realistic draft prospect bios, a 26-player junior roster target, and a clearer three-column live draft experience. Smoke tests and the console harness keep a Jordan Hayes fallback when no custom GM is supplied.

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
