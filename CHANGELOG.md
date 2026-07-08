# Changelog

## Current - Alpha 5.8

### Added

- Dynamic draft class profiles with theme, quality, strengths, weaknesses, storyline, positional depth, regional distribution, scout quote, and uncertainty.
- DraftClassGenerator for annual fictional class creation using NameGenerator, public DraftProspectBio, realistic position/measurement/handedness/team context, class risk, and class-context notes.
- Rulebook-aware class behavior: NHL-style classes use 17-18 year olds and broader geography, junior-style classes use younger regional prospects, and AHL-style rulebooks generate no amateur draft class when draft is disabled.
- Draft class history preservation so completed drafts keep the original class profile/read.
- Draft Board and Live Draft class summaries, positional depth, best available by position, players to watch, risk/context notes, and future filter placeholders.
- Dossier and scouting intelligence class context without exposing hidden ratings.
- Tests covering class generation, themes, NHL/junior/AHL behavior, inherited scouting, clean generated names, UI exposure, dossiers, and history preservation.

### Changed

- New GM scenario draft board creation now uses the dynamic class generator instead of static prospect position/projection helpers.
- Season rollover now creates the next season's draft board from a new draft class profile.
- Opening scenario draft prospects use birth dates that match their public league-style age on the scenario start date.
- Inherited scouting reports now include class context and risk notes.
- AlphaDesktop version label updated to Alpha 5.8.

### Fixed

- NHL-style generated draft bios now use each generated prospect's actual origin instead of defaulting to local junior geography.
- Draft prospect risk summaries now display from the new risk field instead of reusing analytics text.

### Verified

- `dotnet build HockeyGmLegacy.slnx --no-restore`
- `dotnet run --project tests/LegacyEngine.Tests`
- `dotnet run --project client/AlphaDesktop -- --smoke-test`

### Not Added

- No Godot.
- No real player database.
- No media/mock draft engine.
- No salary-cap or waiver changes.
- No game simulation changes.

## Recently Completed

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

- Alpha 5.9 - TBD.
