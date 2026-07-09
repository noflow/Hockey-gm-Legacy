# Changelog

## Current - Alpha 7.4

### Added

- Rulebook-driven contract buyout rules, including enable/disable flag, buyout window offsets, configured cost percentage, penalty-years multiplier, cap-penalty flag, minimum remaining-years rule, and age-rule placeholder.
- Buyout models for `ContractBuyout`, `BuyoutEligibility`, `BuyoutCalculation`, `BuyoutPenalty`, `BuyoutWindow`, `BuyoutResult`, and `BuyoutHistory`.
- `BuyoutService` to evaluate eligibility, calculate buyout cost, create pending confirmations, confirm/cancel buyouts, release bought-out players to free agency, record history, and queue events/messages.
- Confirmed buyouts now create future penalty schedules and feed the salary-cap snapshot as buyout/dead-cap penalties.
- Player/agent relationship impact for confirmed buyouts.
- Action Center buyout items for open/closing windows, pending confirmations, and active penalty warnings.
- Player Dossier buyout status/history lines.
- League News transaction/event support for completed contract buyouts.
- AlphaDesktop Contract Buyouts screen under Hockey Operations, plus selected-player actions for Calculate Buyout, Confirm Buyout, Cancel Buyout, and View Dossier.
- Alpha 7.4 tests covering rulebook eligibility, junior-disabled behavior, outside-window blocking, calculation, future penalties, free-agency release, cap snapshot updates, history, relationship impact, Action Center, UI exposure, save/load preservation, and forbidden-system boundaries.

### Changed

- AlphaDesktop version label updated to Alpha 7.4.
- Contract decision count now includes pending buyout confirmations.
- Budget/cap views now expose buyout/dead-cap penalty schedule when present.
- Alpha 5.6 boundary test remains focused on no LTIR, no offer-sheet engine, and no Godot now that buyouts exist in Alpha 7.4.

### Verified

- `dotnet build HockeyGmLegacy.slnx --no-restore`
- `dotnet run --project tests/LegacyEngine.Tests`
- `dotnet run --project client/AlphaDesktop -- --smoke-test`

### Not Added

- No LTIR.
- No retained salary.
- No compliance buyouts.
- No contract termination appeals.
- No offer sheets.
- No full CBA edge-case system.
- No Godot.
- No new game simulation logic.

## Previous - Alpha 7.3

### Added

- Rulebook-driven salary arbitration rules, including enable/disable flag, eligibility age, accrued-season threshold, filing window, hearing date range, walk-away permission, and award bounds.
- Arbitration models for `ArbitrationEligibility`, `ArbitrationCase`, `ArbitrationFiling`, `ArbitrationAward`, `ArbitrationResult`, `ArbitrationDecisionType`, and `ArbitrationHistory`.
- `ArbitrationService` to build eligible qualified RFA cases, file team/player arbitration, schedule hearings, estimate awards, negotiate settlements, accept awards, walk away where allowed, and record arbitration history.
- Award estimates using previous salary, recent/career production, role, budget/cap context, comparable-contract placeholder, and agent commentary.
- Action Center arbitration review items for filing/hearing/award decisions.
- Player Dossier arbitration status, hearing date, award projection, agent comment, recommendation, and arbitration history lines.
- League News transaction types/events for arbitration filing, awards, settlements, and walk-away decisions.
- AlphaDesktop Salary Arbitration screen under Hockey Operations, plus selected-player actions for File Arbitration, Settle Arbitration, Accept Award, Walk Away, and View Dossier.
- Alpha 7.3 tests covering eligibility, ineligible players, rulebook-controlled thresholds, case creation, hearing dates, award estimates, settlement, accepted awards, walk-away release, Action Center, dossier/history, save/load preservation, desktop exposure, and forbidden-system boundaries.

### Changed

- AlphaDesktop version label updated to Alpha 7.3.
- Contract decision count now includes open arbitration cases.
- Rulebook copy helpers now preserve arbitration rules so generated league profiles and tests keep professional arbitration behavior.
- Alpha 7.2 boundary test now remains focused on no offer sheets and no Godot now that arbitration exists in Alpha 7.3.

### Verified

- `dotnet build HockeyGmLegacy.slnx --no-restore`
- `dotnet run --project tests/LegacyEngine.Tests`
- `dotnet run --project client/AlphaDesktop -- --smoke-test`

### Not Added

- No offer sheets.
- No full CBA edge-case system.
- No court/legal simulation.
- No media expansion.
- No Godot.
- No new game simulation logic.

## Previous - Alpha 7.2

### Added

- Rulebook-driven RFA/UFA contract-rights rules, including enable/disable flag, UFA age threshold, UFA accrued-season threshold, qualifying-offer requirement, qualifying-offer deadline, rights expiry rule, qualifying-offer multiplier, minimum qualifying offer, and contract tender window.
- Rights models for `FreeAgentRightsStatus`, `ContractRightsStatus`, `PlayerRightsDecision`, `RightsDecisionResult`, `QualifyingOffer`, `RightsExpiryRule`, and `RightsHistory`.
- `RfaUfaService` to classify expiring NHL-style contracts as pending RFA or pending UFA, issue qualifying offers, decline qualifying offers, release rights, move UFAs/not-qualified players into the free-agent market, and preserve rights history.
- Action Center contract-rights warnings before qualifying-offer deadlines.
- Player Dossier RFA/UFA status, rights holder, qualifying-offer deadline/amount, agent note, recommendation, and rights history lines.
- League News transaction types and events for RFA qualification, not-qualified players, UFA market entry, and rights release.
- AlphaDesktop Contract Rights / Expiring Contracts screen under Hockey Operations, plus player-detail actions for Qualify, Do Not Qualify, Negotiate Contract, and View Dossier.
- Alpha 7.2 tests covering young pending RFA, older pending UFA, rulebook thresholds, qualifying offer retention, rights release, UFA market entry, RFA rights holder, desktop rights exposure, dossier exposure, Action Center warning, League News, save/load preservation, and forbidden-system boundaries.

### Changed

- AlphaDesktop version label updated to Alpha 7.2.
- Season rollover now ensures expiring professional contracts are classified into RFA/UFA rights decisions.
- Rulebook copy helpers now preserve free-agent rights rules so tests and generated league profiles do not accidentally drop professional rights behavior.

### Verified

- `dotnet build HockeyGmLegacy.slnx --no-restore`
- `dotnet run --project tests/LegacyEngine.Tests`
- `dotnet run --project client/AlphaDesktop -- --smoke-test`

### Not Added

- No offer sheets.
- No full holdout system.
- No full CBA edge-case system.
- No LTIR.
- No buyouts.
- No Godot.
- No new game simulation logic.

## Previous - Alpha 7.1

### Added

- Rulebook-driven waiver rules for professional leagues, including enable/disable flag, claim window, waiver order, exemption age, exemption seasons, exemption games, and cancellation placeholder.
- Waiver models for `WaiverStatus`, `WaiverEligibility`, `WaiverTransaction`, `WaiverClaim`, `WaiverWire`, `WaiverPriority`, `WaiverHistory`, and `WaiverResult`.
- `WaiverService` for eligibility checks, waiver placement, claim submission, waiver processing, cleared-player affiliate assignment, player recall, waiver history, transaction history, and dossier lines.
- Professional roster transaction events and League News types for waiver placement, waiver claim, waiver clear, affiliate assignment, and recall.
- AlphaDesktop Waiver Wire screens under League and Hockey Operations, player waiver status in roster/detail views, and player actions for Assign Affiliate, Place On Waivers, and Recall.
- Player Dossier Waivers / Assignments section.
- Alpha 7.1 tests covering professional/junior rulebooks, exemption logic, veteran waiver requirements, affiliate assignment, waiver placement, claims, clears, recalls, dossier exposure, save/load preservation, desktop UI exposure, and forbidden-system boundaries.

### Changed

- AlphaDesktop version label updated to Alpha 7.1.
- Professional player assignment now uses waiver state and history instead of treating affiliate assignment as a silent roster status change.
- League Transaction Wire now categorizes waiver and recall activity as roster moves.

### Verified

- `dotnet build HockeyGmLegacy.slnx --no-restore`
- `dotnet run --project tests/LegacyEngine.Tests`
- `dotnet run --project client/AlphaDesktop -- --smoke-test`

### Not Added

- No LTIR.
- No emergency recalls.
- No conditional waivers.
- No buyouts.
- No full CBA edge-case system.
- No Godot.
- No new game simulation engine.

## Previous - Alpha 7.0

### Added

- Hockey Intelligence Rating model with hidden `PlayerTrueRatings`, visible `PlayerScoutedRatings`, 0-100 attribute ranges, confidence colors, scout sources, and rating notes.
- Attribute categories for Offensive, Defensive, Skating, Physical, Skill, Mental, Team, and Goalie ratings.
- Confidence color workflow: Unknown, Red, Green, Blue, and Black, with uncertain players showing `???` or ranges instead of hidden truth.
- `HockeyIntelligenceRatingService` to seed true ratings, create initial scouted estimates, record scouting updates, improve confidence over repeated reports, and apply development changes to hidden truth without automatically updating visible estimates.
- Player dossier Hockey Intelligence section showing category ratings, scout source, last update, confidence, and notes while explicitly keeping true internal ratings private.
- Alpha 7.0 tests covering hidden true ratings, scouted privacy, unscouted unknowns, red uncertainty, repeated scouting, elite scout confidence gains, convergence toward truth, all attribute families, junior/NHL scale differences, elite draft prospects, rare high potential, hidden development updates, dossier exposure, UI rating hooks, save/load preservation, and AlphaDesktop privacy.

### Changed

- AlphaDesktop version label updated to Alpha 7.0.
- Existing visible OVR/POT snapshots now consume Hockey Intelligence scouted ranges directly instead of re-spreading estimates.
- Development and daily scenario preparation now ensure Hockey Intelligence ratings before curve and visible-rating snapshots.
- Save/load now preserves true and scouted rating knowledge through the scenario snapshot.

### Verified

- `dotnet build HockeyGmLegacy.slnx --no-restore`
- `dotnet run --project tests/LegacyEngine.Tests`
- `dotnet run --project client/AlphaDesktop -- --smoke-test`

### Not Added

- No editable rating editor.
- No real player database.
- No Godot.
- No new game simulation engine.
- No full tactical simulation rewrite.

## Previous - Alpha 6.17

### Added

- Public player rating model with `PlayerRating`, `PlayerPotential`, confidence bands, rating snapshots, rating history, and `PlayerRatingService`.
- Development curve model with curve types, pace, potential variance, hidden ceiling context, breakthrough/setback notes, projected outcomes, and `DevelopmentCurveService`.
- Visible 0-100 OVR/POT estimates across roster, recruits, prospects, scouting, draft board, live draft, free agents, trade rows, player details, and player dossier ratings.
- Scouting-confidence-driven rating uncertainty: high confidence shows tighter estimates while low-confidence prospects show ranges instead of hidden truth.
- Rating history and dossier trend text for visible OVR/POT movement, peak visible OVR, potential estimate changes, plateau notes, and injury impact.
- Player dossiers now show Development Curve with curve type, pace, ETA, plateau risk, breakout chance, staff note, and best development path.
- Action Center now surfaces major development-curve risks without adding routine inbox noise.
- Alpha 6.17 tests covering player ratings, junior/NHL scale differences, elite draft prospects, rare 95+ potential, confidence ranges, development gains, plateau, injury impact, UI exposure, hidden-potential privacy, exceed/miss projection outcomes, late bloomers, fast/slow curves, role/coaching/injury modifiers, dossier exposure, and Action Center risk warnings.

### Changed

- AlphaDesktop version label updated to Alpha 6.17.
- Draft prospect rating estimates now support elite low-80s OVR at the top of a class while keeping most draft players lower.
- Roster ratings now respond to development profile changes and active injury penalties.
- Visible potential can now be influenced by curve-aware estimated ceiling while still hiding true ceiling when confidence is low.

### Verified

- `dotnet build HockeyGmLegacy.slnx --no-restore`
- `dotnet run --project tests/LegacyEngine.Tests`
- `dotnet run --project client/AlphaDesktop -- --smoke-test`

### Not Added

- No full attribute system.
- No editable ratings.
- No real player database.
- No Godot.
- No new game simulation engine.

## Previous - Alpha 6.16

### Added

- Draft War Room state for custom GM board entries, original board snapshot/history, watch-list tags, team needs, draft storylines, department best-player opinions, scout consensus, prospect comparisons, and post-draft reviews.
- `DraftWarRoomService` to create year-round War Room state, move prospects, move to a rank, pin/favorite/tag/remove players, update GM notes, compare 2-4 prospects, build scout consensus, score AI draft fit, and generate post-draft reviews.
- Watch-list tags for Watching, Priority, Sleeper, Avoid, Medical Concern, Character Concern, Late Round Target, Favorite, and Pinned.
- War Room integration into New GM scenario bootstrap and season rollover so each draft class gets a persistent draft prep room.
- AI draft selection now considers organization AI needs, draft strategy/personality, position fit, and scouting confidence instead of only raw board rank.
- AlphaDesktop Draft War Room screen under Hockey Operations with My Draft Board, Watch List, Needs Analysis, Draft Class Summary, department opinions, scout consensus, comparison, storylines, draft history, draft rights, original board snapshot, and post-draft review.
- Live draft controls for Skip, View Dossier, and Compare, with upcoming picks and War Room consensus/tag context in the selected prospect card.
- Alpha 6.16 tests covering War Room creation, custom rankings, watch-list tags, needs, comparison, consensus, best-player opinions, AI draft fit, post-draft review, original board history, Where Are They Now continuity, UI exposure, and forbidden-system boundaries.

### Changed

- AlphaDesktop version label updated to Alpha 6.16.
- Draft completion now stores a War Room post-draft review and folds head scout/owner review into the final draft recap message.
- Manual War Room ranking now preserves the GM's selected order instead of re-sorting back to the previous rank.

### Verified

- `dotnet build HockeyGmLegacy.slnx --no-restore`
- `dotnet run --project tests/LegacyEngine.Tests`
- `dotnet run --project client/AlphaDesktop -- --smoke-test`

### Not Added

- No mock drafts.
- No draft lottery.
- No real player database.
- No Godot.
- No media expansion.
- No save/database/cloud changes.

## Previous - Alpha 6.15

### Added

- Awards model for `Award`, `AwardType`, `AwardCategory`, `AwardRecipient`, `AwardHistory`, and `AwardService`.
- Record model for `RecordBook`, `RecordEntry`, `RecordType`, `RecordScope`, and `RecordService`.
- Deterministic season-end award selection for MVP, Top Scorer, Best Defenseman, Best Goalie, Rookie of the Year, Team MVP, Most Improved, Coach of the Year, GM of the Year, Playoff MVP, Top Scout placeholder, and Development Staff Award placeholder.
- Public-stat record tracking for goals, assists, points, goalie wins, shutouts, games played, team wins, championships, and playoff points placeholder.
- Career timeline entries for award winners and broken player records.
- Media / News articles for major award winners and broken records.
- Player Dossier Awards & Records section.
- Reports / History screens for Awards, Record Book, Team Records, and League Records.
- Organization Command Center Team Awards & Records card.
- Save/load preservation through `NewGmScenarioSnapshot.AwardHistory` and `NewGmScenarioSnapshot.RecordBook`.
- Alpha 6.15 tests covering awards, record updates, broken-record history/media, dossier/report UI exposure, save/load, and forbidden-system boundaries.

### Changed

- AlphaDesktop version label updated to Alpha 6.15.
- Season completion and completed-schedule daily advancement now ensure awards and records before media feed generation.
- Media / News now supports Award and Record article types while League News remains the raw transaction wire.

### Verified

- `dotnet build HockeyGmLegacy.slnx --no-restore`
- `dotnet run --project tests/LegacyEngine.Tests`
- `dotnet run --project client/AlphaDesktop -- --smoke-test`

### Not Added

- No Hall of Fame.
- No jersey retirement.
- No full voting system.
- No real NHL trophy names.
- No Godot.
- No social media expansion.

## Previous - Alpha 6.14

### Added

- Media & News models for `MediaArticle`, `MediaArticleType`, `MediaSource`, `MediaTone`, `MediaImportance`, `MediaFeed`, and `MediaService`.
- Fictional media source pool: League Wire, Hockey Daily, Prospect Central, Front Office Report, Local Beat, and Draft Desk.
- Short readable media articles generated from Living Story arcs, major trades/signings, draft picks, prospect rights, player/staff/owner milestones, and trade-deadline rumors.
- Rumor support with Low, Medium, and High confidence labels.
- Media feed filtering by article type, team, player/person, importance, and source.
- Dashboard top headline card.
- Reports / History Media / News screen with sources, top headlines, and grouped article coverage.
- Player Dossier Media Coverage section.
- Organization Command Center Media Coverage card.
- Save/load preservation through `NewGmScenarioSnapshot.MediaFeed`.
- Alpha 6.14 tests covering major trade articles, draft articles, milestone articles, rumors, article metadata, feed filters, dashboard/dossier UI exposure, separation from League News, save/load, and forbidden-system boundaries.

### Changed

- AlphaDesktop version label updated to Alpha 6.14.
- New GM scenario, daily advancement, loaded desktop state, and save flow now ensure media feed state alongside stories, franchise identity, life-cycle, relationship, lineup, chemistry, usage, and tactics state.
- League News remains the raw transaction wire, while Media / News is the article/story layer.

### Verified

- `dotnet build HockeyGmLegacy.slnx --no-restore`
- `dotnet run --project tests/LegacyEngine.Tests`
- `dotnet run --project client/AlphaDesktop -- --smoke-test`

### Not Added

- No social media system.
- No full media pressure engine.
- No press conferences.
- No interviews.
- No long-form generated articles.
- No real media brands.
- No voice/commentary system.
- No Godot.

## Previous - Alpha 6.13

### Added

- Living Story Engine models for `Story`, `StoryArc`, `StoryEvent`, `StoryType`, `StoryStatus`, `StoryImportance`, `StorySummary`, and `StoryService`.
- Generated story arcs for players, the GM, the organization, scouting staff, and owner context using existing career history, life-cycle, relationship, franchise identity, and prospect data.
- Story progression support that records dated story events, updates arc progress/status, and preserves readable key moments.
- Player Dossier Stories section with current story, status, progress, summary, and major moments.
- Organization Command Center Current Organization Story card.
- Executive report and monthly summary Living Stories / Storylines sections.
- Action Center story items for major or at-risk stories only.
- Limited League News story headlines using `LeagueTransactionType.StoryUpdate`.
- Save/load preservation through `NewGmScenarioSnapshot.Stories`.
- Alpha 6.13 tests covering story generation, progression, readable summaries, League News, dossier/report/action-center visibility, monthly reports, save/load, and forbidden-system boundaries.

### Changed

- AlphaDesktop version label updated to Alpha 6.13.
- New GM scenario, daily advancement, and loaded desktop state now ensure story state alongside life-cycle, relationship, franchise identity, lineup, chemistry, usage, and tactics state.

### Verified

- `dotnet build HockeyGmLegacy.slnx --no-restore`
- `dotnet run --project tests/LegacyEngine.Tests`
- `dotnet run --project client/AlphaDesktop -- --smoke-test`

### Not Added

- No media engine.
- No articles.
- No social media.
- No voice/commentary system.
- No Hall of Fame.
- No Godot.
- No save database/cloud changes.

## Previous - Alpha 6.12

### Added

- Franchise Identity model with current identity, historical identity, current philosophy, culture, future direction, current era, historical eras, franchise reputation, team DNA, strengths, weaknesses, future goals, and identity shift history.
- Franchise Identity service that seeds every league organization from existing League AI/team-profile context and evolves identity conservatively when enough evidence exists.
- Franchise history counters for playoff appearances, championships, finals appearances, rebuilds, dynasties, longest playoff streak, worst season, greatest draft class, and best trade.
- Organization Command Center Franchise Overview card with identity, culture, era, reputation, DNA, strengths, weaknesses, future goals, identity change, and franchise history.
- Player Dossier Organization Fit section explaining why a player fits the club identity/culture without exposing hidden ratings.
- Staff/owner command-center fit context using existing staff and organization data.
- Executive report Franchise Identity section and limited League News identity headlines.
- Save/load preservation through `NewGmScenarioSnapshot.FranchiseIdentities`.
- Alpha 6.12 tests covering identity, culture, eras, evolution, reputation, history, command center UI, reports, league news, player fit, staff fit, save/load, and forbidden-system boundaries.

### Changed

- AlphaDesktop version label updated to Alpha 6.12.
- New GM scenario and loaded desktop state now ensure franchise identity exists alongside Organization AI, life-cycle, relationship, lineup, chemistry, usage, and tactics state.

### Verified

- `dotnet build HockeyGmLegacy.slnx --no-restore`
- `dotnet run --project tests/LegacyEngine.Tests`
- `dotnet run --project client/AlphaDesktop -- --smoke-test`

### Not Added

- No expansion.
- No relocation.
- No Hall of Fame.
- No media engine.
- No Godot.
- No game simulation changes.

## Previous - Alpha 6.11

### Added

- AlphaDesktop Organization Command Center as the first Organization workspace.
- Department rail for Owner, Front Office, Coaching, Scouting, Development, Medical, Equipment, Finance, and Facilities placeholder.
- Department overview cards for organization needs, department health, organization chart, financial overview, executive report, and Action Center items.
- Department health display with grade, summary, evidence, strengths, weaknesses, budget, staff count, vacancies, and recommendations.
- Selected owner/staff/vacancy card with salary, years remaining, extension recommendation, replacement cost, relationship, performance, history, and context-specific actions.
- Staff movement workflow buttons for promote, demote, move department/focus, performance review, and release using existing Staff Office actions.
- Vacancy workflow routing to Staff Hiring and Vacancies.
- Alpha 6.11 tests covering Organization Command Center exposure, departments, health, chart/budget/report/action cards, selected staff actions, owner/vacancy workflow, and forbidden-system boundaries.

### Changed

- Organization workspace now opens on Command Center while preserving the existing Owner, Staff, Staff Hiring, Vacancies, Budget, Organization Health, and Relationships screens.
- AlphaDesktop version label updated to Alpha 6.11.

### Verified

- `dotnet build HockeyGmLegacy.slnx --no-restore`
- `dotnet run --project tests/LegacyEngine.Tests`
- `dotnet run --project client/AlphaDesktop -- --smoke-test`

### Not Added

- No Godot.
- No media system.
- No facilities simulation.
- No new gameplay system.
- No game simulation changes.

## Previous - Alpha 6.10

### Added

- AlphaDesktop Hockey Operations Command Center as the first Hockey Operations workspace.
- Left source rail for Roster, Prospects, AHL, Junior Rights, Free Agents, and Trade Targets.
- Center work views for Lines, Roster, Development, Contracts, Scouting, Trade, and Free Agency.
- Persistent selected-player command card with photo placeholder, position, age, team/rights, current role, potential role, current line, contract/rights, development, medical, scouting, relationships, and history context.
- Command Center quick actions and context menu for dossier, line assignment, development review, contract view, scout assignment, trade view, and history.
- Source/search filtering with row deduping by person id.
- Alpha 6.10 tests covering Command Center exposure, source rails, work views, selected-player context, quick actions, and forbidden-system boundaries.

### Changed

- Hockey Operations now opens on Command Center while preserving the existing Roster, Lineup, Tactics, Prospects, Recruits, Scouting, Draft Board, Training Camp, and related screens.
- AlphaDesktop version label updated to Alpha 6.10.

### Verified

- `dotnet build HockeyGmLegacy.slnx --no-restore`
- `dotnet run --project tests/LegacyEngine.Tests`
- `dotnet run --project client/AlphaDesktop -- --smoke-test`

### Not Added

- No Godot.
- No media system.
- No new game simulation logic.
- No new tactics engine.
- No save/load changes.

## Previous - Alpha 6.9

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

- Alpha 7.1 - TBD.
