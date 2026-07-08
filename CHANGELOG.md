# Changelog

## Current - Alpha 6.2

### Added

- Owner life-cycle models for life stage, career state, expectation history, confidence history, meeting history, job-security history, milestones, and legacy profiles.
- OwnerLifeCycleService to evaluate owner personality evolution, budget relationship, confidence trends, permanent owner letters, ownership-era summaries, and organization-history context.
- Owner milestones now feed career timelines and limited notable Owner-category League News items.
- Owner workspace now shows life stage, confidence trend, current personality, owner career summary, personality evolution, budget relationship, legacy, and organization-era context.
- Reports / History now includes Owner History, Owner Letters, Job Security History, and Expectation Results views.
- Executive reports and Action Center now include owner life-cycle highlights for meetings, budget review, confidence pressure, letters, job-security warnings, and legacy context.
- Tests covering life stage generation, expectations, confidence, meetings, letters, job security, legacy, milestones, Action Center, reports, budget pressure, save/load, UI exposure, and forbidden-system checks.

### Changed

- New GM scenario bootstrap now seeds owner life-cycle records for the existing ownership era.
- AlphaDesktop backfills owner life-cycle records for current and loaded scenario state.
- League News counts and feeds now include notable owner milestones.
- AlphaDesktop version label updated to Alpha 6.2.

### Fixed

- Owner context no longer lives only in current expectation/job-security snapshots; owner screens and reports now show a coherent long-term ownership read.

### Verified

- `dotnet build HockeyGmLegacy.slnx --no-restore`
- `dotnet run --project tests/LegacyEngine.Tests`
- `dotnet run --project client/AlphaDesktop -- --smoke-test`

### Not Added

- No Godot.
- No actual firing system.
- No owner replacement system.
- No job offers.
- No board of directors.
- No media pressure engine.
- No full finance engine.
- No game simulation changes.

## Recently Completed

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

- Alpha 6.3 - TBD.
