# Changelog

## Current - Alpha 6.1

### Added

- Staff life-cycle models for life stage, career phase, reputation category, milestones, career story, organizations, roles, salary history, personal legacy, promotion readiness, and concern summaries.
- StaffLifeCycleService to evaluate current staff and staff-market candidates across coaching, scouting, medical, and front-office departments.
- Staff milestones now feed career timelines and limited notable Staff-category League News items.
- Staff profiles now show life stage, career phase, reputation, legacy score, organizations, roles, players developed, prospects discovered, coaching tree links, promotion readiness, and career concerns.
- Reports / History now includes Staff Careers, Coaching Trees, Scout History, and Development Staff History views.
- Executive reports and Action Center now include staff life-cycle highlights for promotion review, succession planning, staff performance review, top scout, development staff, and staff concerns.
- Tests covering life stages, career history, reputation, scouting careers, coaching careers, coaching trees, promotion, staff movement, relationships, mentorship, timeline, history, reports, Action Center, save/load, and UI exposure.

### Changed

- New GM scenario bootstrap now seeds staff life-cycle records for the existing staff room.
- AlphaDesktop backfills staff life-cycle records for current and loaded scenario state.
- League News counts and feeds now include notable staff milestones.
- AlphaDesktop version label updated to Alpha 6.1.

### Fixed

- Staff career context no longer lives only in scattered staff history and market records; staff profiles and reports now show one coherent career read.

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

- Alpha 6.2 - TBD.
