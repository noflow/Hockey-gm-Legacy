# Changelog

## Current - Alpha 6.0

### Added

- Player life-cycle models for life stage, career phase, reputation category, milestones, achievements, career story, staff influence, and legacy score.
- PlayerLifeCycleService to evaluate tracked roster players, prospects, draft board players, recruits, free agents, and trade-block players.
- Player milestones now feed career timelines and limited notable League News items.
- Player dossiers now show life stage, career phase, reputation, legacy score, story lines, milestones, achievements, influential staff, and coach/scout/medical career notes.
- Reports / History now includes Career Milestones and Player Stories views.
- Executive reports and Action Center now include meaningful player career highlights.
- Tests covering life-stage generation, career progression, milestones, reputation, legacy score, career stories, timeline history, achievements, staff influence, reports, League News, dossiers, and Action Center exposure.

### Changed

- New GM scenario bootstrap now seeds player life-cycle records for the existing world.
- AlphaDesktop backfills player life-cycle records for current scenario state.
- League News counts and feeds now include notable player milestones.
- AlphaDesktop version label updated to Alpha 6.0.

### Fixed

- Player career context no longer lives only in scattered stat/history records; dossiers and reports now show one coherent career read.

### Verified

- `dotnet build HockeyGmLegacy.slnx --no-restore`
- `dotnet run --project tests/LegacyEngine.Tests`
- `dotnet run --project client/AlphaDesktop -- --smoke-test`

### Not Added

- No Godot.
- No Hall of Fame logic.
- No retirement decision system.
- No awards voting.
- No jersey retirement.
- No media engine.
- No game simulation changes.

## Recently Completed

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

- Alpha 6.1 - TBD.
