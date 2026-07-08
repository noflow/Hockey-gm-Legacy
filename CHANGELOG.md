# Changelog

## Current - Alpha 6.4

### Added

- Lineup role model covering forwards, defensemen, and goalies with opinion/projection labels such as Franchise Forward, Top Six Forward, Top Pair Defenseman, Starting Goalie, Backup Goalie, and Prospect Goalie.
- Lineup model for four forward lines, three defense pairs, starter/backup goalie depth, lineup slot assignments, coach recommendations, and lineup development impact summaries.
- LineupService default lineup generation for the player organization using position, age, development stage, player type, team strength, and roster balance.
- Manual lineup management for assign, remove, swap, and auto-fill actions with slot eligibility validation.
- Player role expectation tracking for current role, expected role, promised role, coach recommended role, potential role, promise status, role satisfaction, development usage context, and morale note.
- Role promise consequences that can mark promises kept, at risk, or broken and route broken promises through relationship/morale fallout.
- Contract role-promise capacity warning when an offer promises top-six, top-pair, first-line, or starter usage that the lineup cannot easily support.
- Player dossier Role / Usage section with lineup slot, expectation, promise, coach recommendation, satisfaction, role history, and development impact context.
- Hockey Operations Lineup view showing lines, pairs, goalie depth, coach recommendations, and lineup-driven development context.
- Alpha 6.4 tests covering NHL top-line/top-pair role generation, contender vs rebuilding role differences, lineup structure, manual slot assignment, swaps, invalid placement warnings, duplicate warnings, injured-player warnings, role promises, relationship/morale fallout, dossier usage, contract role warnings, save preservation, trade role labels, hidden-rating boundaries, and no tactical-simulation expansion.

### Changed

- New GM scenarios now store a generated current lineup in the scenario snapshot.
- AlphaDesktop version label updated to Alpha 6.4.
- Roster rows now show player type, current lineup role, expected role, promised role, current line/pair, satisfaction, development stage, contract/rights status, prior stats, injury status, and development trend.
- Roster detail panels now include expected role, promised role, promise status, role satisfaction, lineup development impact, and coach lineup notes.
- Roster role filters now include richer hockey role labels, including franchise, first line, top six, checking, top pair, second pair, starter, tandem, backup, depth, prospect, and healthy scratch.
- Trade assets and team-browser roster views now show role, potential role, and target type context such as top-line player, top-pair defenseman, depth player, prospect, and buried player.
- Important coach lineup recommendations now appear in the Action Center as roster items.

### Fixed

- Roster role labels no longer rely on desktop-only hidden current-ability/potential thresholds.
- Trade asset summaries now include lineup role context instead of generic middle-six/depth wording only.

### Verified

- `dotnet build HockeyGmLegacy.slnx --no-restore`
- `dotnet run --project tests/LegacyEngine.Tests`
- `dotnet run --project client/AlphaDesktop -- --smoke-test`

### Not Added

- No Godot.
- No full tactics engine.
- No special teams.
- No line chemistry engine.
- No drag/drop lineup editor.
- No game simulation overhaul.

## Recently Completed

- Alpha 6.3 - Relationship Expansion v1.
- Alpha 6.2.1 - Trades v3: Roster Assets, Draft Picks, and Counter Offers.
- Alpha 6.2 - Owner Life Cycle v1.
- Alpha 6.1 - Staff Life Cycle v1.
- Alpha 6.0 - Player Life Cycle v1.
- Alpha 5.9 - League AI v2.
- Alpha 5.8 - Dynamic Draft Classes v1.
- Alpha 5.7 - Agent Engine v1.
- Alpha 5.6 - Salary Cap & Roster Compliance v1.
- Alpha 5.4 - NHL/AHL/Junior Player Pipeline v1.
- Alpha 5.3.1 - Trade Window Interactions + Living Staff Market.
- Alpha 5.3 - Full League Teams + NHL/AHL Player Pipeline v1.
- Alpha 5.1 - Multi-League Career Framework.
- Alpha 5.0 - Playability & Polish.
- Alpha 4.x - Contracts v2, Free Agency v2, Trade Engine v2, Scouting v2, Player Development v2, Staff & Coaching v3, Injury & Medical v2, Owner & Job Security v2, and League AI & Team Identity v2.
- Alpha 3.x - Existing World History, Free Agent Market, Trade Engine, Trade Deadline, Career & History, and Save/Load.
- Alpha 2.x - Dossiers, Staff Control, Recruiting v2, Season Framework, Game Recaps, GM Office UI, Action Center, and inbox cleanup.
- Alpha 1.x - New GM scenario, draft experience, training camp, opening roster readiness, executive reports, and scouting operations.
- Core LegacyEngine milestones for Rule Engine, Owners, Scouting, People, Relationships, Events, World, Recruiting, Contracts, Draft, Rosters, Development, Injuries, Communication, Human Intelligence, Staff, Organizations, and Seasons.

## Next

- Alpha 6.5 - TBD.
