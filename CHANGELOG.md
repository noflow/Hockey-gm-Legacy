# Changelog

## Current - Alpha 5.4

### Added

- NHL/AHL/Junior Player Pipeline v1.
- Player assignment rule support for junior cutoff age, AHL eligibility age, CHL-to-AHL restrictions, optional 19-year-old exception, and ELC slide threshold.
- Pipeline models for development level, rights status, assignment eligibility, assignment rules, assignment decisions, and assignment results.
- Prospect pipeline display in dossiers and AlphaDesktop prospect views.
- ELC slide visibility for signed 18/19-year-old prospects, including games toward the slide threshold.
- AHL affiliate and parent-prospect visibility for NHL/AHL team contexts.

### Changed

- NHL-drafted prospects now remain in draft rights/prospect lists until the GM makes an explicit decision.
- Drafted prospects no longer auto-join NHL or AHL rosters.
- AHL assignment now requires the player to be signed and rulebook-eligible.
- CHL/junior-age players can be signed and returned to junior while rights remain with the NHL club.
- Prospect decisions now disable or reject invalid assignment paths with clear reasons.
- Player dossiers now explain pipeline status, rights holder, signed/unsigned state, AHL assignment eligibility, junior return eligibility, contract slide status, and staff recommendation.
- AlphaDesktop version label updated to Alpha 5.4.

### Fixed

- European/college placeholder prospects no longer inherit CHL protection from draft-board seed data when their explicit league path is non-CHL.
- Older prospect-decision tests now reflect the current rule that AHL assignment requires a signed eligible player.
- Dossier hidden-rating test narrowed to the development section so normal career/history numbers do not cause false failures.

### Verified

- `dotnet build HockeyGmLegacy.slnx --no-restore`
- `dotnet run --project tests/LegacyEngine.Tests`
- `dotnet run --project client/AlphaDesktop -- --smoke-test`

### Not Added

- No Godot.
- No salary cap.
- No waivers.
- No full CBA system.
- No database or cloud save system.

## Recently Completed

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

- Alpha 5.5 - TBD.
