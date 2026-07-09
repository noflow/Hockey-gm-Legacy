# Changelog

## Current - Alpha 6.9

### Added

- PlayoffFormat, PlayoffBracket, PlayoffRound, PlayoffSeries, PlayoffSeriesResult, PlayoffTeamSeed, PlayoffGame, PlayoffStatus, PlayoffState, PlayoffSimulationResult, and PlayoffService.
- Rulebook-aware playoff format support with safe Top 8 best-of-7 defaults when playoff rules are missing.
- Standings-based playoff seeding, missed-playoff tracking, first-round bracket creation, series score tracking, round advancement, and champion/runner-up recording.
- Playoff games simulated through the existing GameSimulationService and GameRecapService.
- Separate playoff skater, goalie, and team stats so regular-season stats are not overwritten.
- Playoff qualification, series, champion, league-news, inbox, Action Center, career-timeline, and organization-history hooks.
- AlphaDesktop Season > Playoffs screen with seeds, bracket, series status, recent recaps, and playoff stat leaders.
- AlphaDesktop Reports / History > Playoff Archive and Champions screens.
- Dashboard playoff status card and summary line.
- Save/load preservation of playoff bracket, recaps, stats, and history through NewGmScenarioSnapshot.
- Alpha 6.9 tests covering default format, seeding, qualification inbox, game simulation, separate stats, champion recording, player playoff debut, daily pipeline integration, save/load preservation, desktop exposure, and forbidden-system boundaries.

### Changed

- DailySimulationCoordinator now advances playoffs after the regular-season schedule is complete and waits until a champion is crowned before generating the end-of-season executive review.
- Season rollover archives now prefer the playoff champion when a completed bracket exists instead of using the old standings-leader fallback.
- Season archive and organization history language now uses championship/playoff result wording.
- AlphaDesktop version label updated to Alpha 6.9.
- The older Alpha 4.0 guard no longer bans playoff bracket code now that playoffs are intentionally implemented.

### Verified

- `dotnet build HockeyGmLegacy.slnx --no-restore`
- `dotnet run --project tests/LegacyEngine.Tests`
- `dotnet run --project client/AlphaDesktop -- --smoke-test`

### Not Added

- No Godot.
- No play-by-play engine.
- No shift simulation.
- No shot-by-shot simulation.
- No line matching.
- No 2D/3D game view.
- No full awards system.
- No Hall of Fame.
- No full retirement system.

## Previous - Alpha 6.8

### Added

- GameSimulationContext, TeamSimulationProfile, LineSimulationProfile, GoalieSimulationProfile, SpecialTeamsSimulationProfile, TacticalSimulationProfile, GameSimulationResultV2, and GameSimulationService.
- Public team strength bands for offense, defense, goaltending, special teams, coaching, and chemistry.
- Boxscore-based game simulation that considers lineup roles, top-line usage, defense pairs, starter goalie, special teams, tactics, coach fit, chemistry, active injuries, home/away, and team profile estimates.
- V2 stat allocation where top-line players receive more scoring opportunity, power-play players can receive power-play points, defensemen receive realistic supporting point share, and the tracked starter receives saves/goals-against.
- Enhanced game recaps with top-line summary, special-teams note, tactical note, chemistry note, goalie usage note, key concern, injury note, development note, and milestone context.
- First tracked game milestones for goals, points, and shutouts where the game creates a valid first.
- AlphaDesktop schedule recap text for special teams, tactics, chemistry, goalie usage, top-line impact, and key concern.
- Dashboard last-game top performer and last-game concern lines.
- Alpha 6.8 tests covering simulation context, lineup impact, chemistry impact, special teams, tactics, injury exclusion, goalie usage, top-line opportunity, PP point allocation, enhanced recaps, game milestones, standings/stats continuity, UI exposure, and forbidden-system boundaries.

### Changed

- Daily season simulation now uses GameSimulationService instead of the old basic goal-only simulator.
- Season stats now have v2 overloads that apply player and goalie allocations from the simulation result.
- GameRecapService can build recaps from the v2 simulation result while keeping the legacy recap path available.
- AlphaDesktop version label updated to Alpha 6.8.
- The older Alpha 2.4 staff-budget guard now checks staff budget files instead of banning later game-simulation work across the whole engine.

### Verified

- `dotnet build HockeyGmLegacy.slnx --no-restore`
- `dotnet run --project tests/LegacyEngine.Tests`
- `dotnet run --project client/AlphaDesktop -- --smoke-test`

### Not Added

- No Godot.
- No play-by-play engine.
- No shift simulation.
- No shot-by-shot simulation.
- No line matching.
- No 2D/3D game view.
- No advanced fatigue engine.
- No advanced injury model expansion.

## Previous - Alpha 6.7

### Added

- Team tactics model covering tactical style, system, intensity, risk level, even-strength settings, PP/PK tactical style, fit report, recommendations, player impacts, and small future simulator tendency modifiers.
- Tactical styles: Balanced, Offensive, Defensive, Physical, Speed, Possession, Counterattack, Youth Development, and Veteran Shelter.
- Even-strength settings for forecheck, neutral zone, defensive zone, breakout, shot preference, physicality, and risk.
- Special-teams tactical settings for power play style and penalty kill style while reusing Alpha 6.6 personnel units.
- TacticsService with coach-derived defaults from head coach philosophy.
- Tactical fit report that compares roster composition, line chemistry, coach philosophy, player types, age/experience, and special-teams usage.
- Tactical recommendations for major mismatches such as high-risk tactics on a young roster, coach/style mismatch, and PP/PK tactic mismatch.
- Per-player tactical impact notes with modest role satisfaction, development, and confidence modifiers.
- Player dossier Tactics section.
- Hockey Operations Tactics view in AlphaDesktop with selectable rows for Tactical Identity, Even Strength System, Special Teams Tactics, Tactical Fit Report, Future Simulator Modifiers, and recommendations.
- Dashboard Tactical Fit metric and Action Center routing for important tactic issues only.
- Save/load preservation of team tactics through the scenario snapshot.
- Alpha 6.7 tests covering tactics creation, coach defaults, settings changes, PP/PK style changes, fit reports, mismatch warnings, recommendations, player impact notes, dossier section, Action Center filtering, UI exposure, save/load preservation, and forbidden-system boundaries.

### Changed

- AlphaDesktop version label updated to Alpha 6.7.
- Lineup report now includes the team tactical identity and coaching-style summary.
- Legacy Alpha 2.7 architecture guard now blocks actual matchup/full tactical simulator markers instead of the now-valid word "Tactical".

### Verified

- `dotnet build HockeyGmLegacy.slnx --no-restore`
- `dotnet run --project tests/LegacyEngine.Tests`
- `dotnet run --project client/AlphaDesktop -- --smoke-test`

### Not Added

- No Godot.
- No play-by-play engine.
- No line matching.
- No matchup engine.
- No full tactical simulator.
- No video/2D/3D game view.
- No advanced fatigue engine.
- No advanced injury model.
- No game simulation overhaul.

## Previous - Alpha 6.6

### Added

- Game usage model covering power play units, penalty kill units, goalie workload, extra attacker, three-on-three placeholder, shootout order, per-player usage profiles, and coach recommendations.
- Power Play Unit 1 and Unit 2 personnel slots for LW, C, RW, QB Defense, and Net Front / Second Defense.
- Penalty Kill Unit 1 and Unit 2 personnel slots for LW, RW, LD, and RD.
- Goalie usage profiles with starter/backup role, games started, expected starts, workload, and rest recommendation.
- Extra attacker, three-on-three, and shootout-order personnel structures.
- GameUsageService default deployment generation from the current lineup.
- Assignment methods for PP and PK slots plus shootout order movement.
- Per-player Game Usage profiles showing current line, PP, PK, extra attacker, three-on-three, shootout, role, usage summary, coach comment, and modest development modifier.
- Player dossier Game Usage section.
- Lineup workspace rows for Game Usage Summary, Power Play, Penalty Kill, Goalies, Extra Attacker, Three-on-Three, and Shootout Order.
- Dashboard Game Usage metric for important usage recommendations.
- Action Center routing for important game-usage issues such as goalie workload, PP balance, PK review, and prospect PP opportunity.
- Alpha 6.6 tests covering PP assignment, PK assignment, goalie usage, shootout order, usage summaries, coach recommendations, dossier usage, development modifier, dashboard exposure, Action Center exposure, hidden-rating boundaries, and no tactics/game-sim/Godot expansion.

### Changed

- AlphaDesktop version label updated to Alpha 6.6.
- Lineup summary now includes readable game-usage deployment and recommendations.

### Verified

- `dotnet build HockeyGmLegacy.slnx --no-restore`
- `dotnet run --project tests/LegacyEngine.Tests`
- `dotnet run --project client/AlphaDesktop -- --smoke-test`

### Not Added

- No Godot.
- No power play tactics.
- No penalty kill tactics.
- No forecheck.
- No neutral-zone system.
- No faceoff strategy.
- No line matching.
- No matchup engine.
- No game simulation overhaul.

## Previous - Alpha 6.5

### Added

- Line chemistry model for forward lines, defense pairs, goalie room, and team-level chemistry summaries.
- Chemistry scoring grades: Excellent, Good, Neutral, Poor, and Problem.
- LineChemistryService evaluation using player type fit, handedness balance, position fit, role fit, age/experience mix, personality estimate, relationships, coach philosophy, morale/confidence, role promises, and recent-performance placeholders.
- Explainable strengths, weaknesses, coach recommendations, development notes, relationship notes, and role promise notes for each line/pair.
- Team chemistry summary with overall grade, best unit, worst unit, major concerns, coach recommendations, and simple history events.
- Lineup workspace chemistry rows for each forward line, defense pair, and goalie room.
- Selected line/pair detail panel showing chemistry grade, players, strengths, weaknesses, factors, and recommendations.
- Player dossier Role / Usage chemistry notes showing current line chemistry, best known fit placeholder, and chemistry context.
- Action Center routing for major chemistry issues only, limited to poor/problem units to avoid inbox/action spam.
- Alpha 6.5 tests covering forward, defense, goalie, player-type blend, duplicate-type penalty, L/R defense balance, veteran/prospect support, poor relationship penalty, coach recommendations, team summary, UI exposure, dossier exposure, major-only Action Center items, hidden-rating boundaries, and no tactics-engine expansion.

### Changed

- AlphaDesktop version label updated to Alpha 6.5.
- Lineup Summary now includes team chemistry and line/pair/goalie chemistry grades.
- Lineup slot rows now include the relevant chemistry grade for the unit that slot belongs to.

### Verified

- `dotnet build HockeyGmLegacy.slnx --no-restore`
- `dotnet run --project tests/LegacyEngine.Tests`
- `dotnet run --project client/AlphaDesktop -- --smoke-test`

### Not Added

- No Godot.
- No full tactics engine.
- No special teams.
- No power play / penalty kill.
- No line matching.
- No matchup engine.
- No game simulation overhaul.

## Previous - Alpha 6.4

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

- Alpha 7.0 - TBD.
