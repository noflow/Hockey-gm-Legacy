# Changelog

## Current - Alpha 5.6

### Added

- Salary Cap & Roster Compliance v1 for professional-style rulebooks.
- Salary cap models and service: profile, status, snapshot, cap space, contract commitment, calculation, and service.
- Rulebook-driven cap settings for enabled/disabled state, cap amount, floor, roster limit, contract limit, retained-salary placeholder, and offseason placeholder.
- Dashboard salary-cap card for professional leagues.
- Budget workspace split between operating budget and salary cap / current contracts.
- Free agency cap preview showing current cap, cap after signing, remaining space, and warnings.
- Trade builder cap preview with before/after cap impact and green/red indicator text.
- Salary cap checks for free-agent offers, contract approvals, trade validation, and opening roster compliance.

### Changed

- NHL-style and AHL-style presets now enable salary cap rules by default.
- Junior-style leagues keep operating-budget behavior and do not enable the salary cap.
- AHL affiliate rulebooks preserve salary-cap settings when parent/affiliate IDs are applied.
- Contract offer evaluation now includes cap hit, remaining cap before/after, and cap warnings.
- Pending contract approval now fails cleanly if approving would break cap rules.
- Older compatibility tests updated so Alpha 5.6 can intentionally add salary cap while still blocking full waiver systems.
- AlphaDesktop version label updated to Alpha 5.6.

### Fixed

- Restored the missing Alpha 5.4 player-pipeline compatibility test class so the full test runner builds again.
- Free agency now blocks impossible cap-breaking offers before they become active offers.

### Verified

- `dotnet build HockeyGmLegacy.slnx --no-restore`
- `dotnet run --project tests/LegacyEngine.Tests`
- `dotnet run --project client/AlphaDesktop -- --smoke-test`

### Not Added

- No Godot.
- No LTIR.
- No buyouts.
- No retained salary implementation.
- No performance bonuses.
- No offer sheets.
- No waiver claim system.
- No full CBA edge cases.
- No database or cloud save system.

## Recently Completed

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

- Alpha 5.7 - TBD.
