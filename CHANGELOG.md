# Changelog

## Current - Alpha 5.9

### Added

- OrganizationAiProfile with explicit team personality, strategy phase, current needs, urgency, suggested asset target, strategy history, and behavior summaries.
- OrganizationAiService for AI draft, trade, free-agency, staff-hiring, and strategy-evolution decision scoring.
- Organization AI profiles are stored on NewGmScenarioSnapshot so save/load preserves AI personality, strategy, needs, and strategy history.
- Limited strategic league-news headlines for team direction changes without adding inbox spam.
- AlphaDesktop organization/team/trade surfaces now show AI personality, strategy phase, needs, draft/trade/free-agency/budget/scouting/staff behavior, and future league AI filter placeholders.
- Tests covering AI profile generation, needs, strategy-driven draft/trade/free-agency/staff behavior, strategy evolution/history, league news, UI exposure, save/load preservation, and hidden-rating safety.

### Changed

- League AI reports now expose both the existing team identity profile and the richer Alpha 5.9 organization AI profile.
- Trade evaluations now include the other team's AI strategy, top needs, personality, and AI decision read in the explanation/reasons.
- New GM scenario bootstrap now seeds organization AI profiles for all league teams.
- AlphaDesktop backfills organization AI profiles for older loaded saves.
- AlphaDesktop version label updated to Alpha 5.9.

### Fixed

- AI team behavior no longer relies only on generic identity text; decision explanations now identify strategic motive and need fit.

### Verified

- `dotnet build HockeyGmLegacy.slnx --no-restore`
- `dotnet run --project tests/LegacyEngine.Tests`
- `dotnet run --project client/AlphaDesktop -- --smoke-test`

### Not Added

- No Godot.
- No media engine.
- No expansion/relocation.
- No owner replacement.
- No full playoff engine.
- No new game simulation.
- No game simulation changes.

## Recently Completed

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

- Alpha 6.0 - TBD.
