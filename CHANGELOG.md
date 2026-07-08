# Changelog

## Current - Alpha 6.2.1

### Added

- Trade asset builders for both organizations, including roster players, prospect rights, draft picks, and future-consideration placeholders.
- Draft pick trade assets with year, round, original owner, current owner, protected-placeholder, estimated value, and readable display text.
- Trade counter offers now carry revised proposal packages so the GM can accept the counter into the builder before proposing again.
- AlphaDesktop trade builder now separates assets into Your Assets, You Give, You Receive, and Other Team Assets.
- Tests covering both-team roster assets, prospect assets, draft-pick assets, separated proposal buckets, counter offers, pending approvals, approved/declined trades, League News summaries, and forbidden-system checks.

### Changed

- Other-team trade assets now include active-roster style players instead of only a thin prospect/trade-block view.
- Trade validation accepts correctly owned other-team roster/prospect/pick assets without requiring every other-team asset to exist in the small trade-block list.
- AI trade evaluation rejects truly lopsided offers while returning Countered for close-but-incomplete packages.
- Completed trade League News summaries include players, prospects, and draft picks moved.
- AlphaDesktop version label updated to Alpha 6.2.1.

### Fixed

- Trade proposal area no longer mixes both sides' selected assets in one shared list.
- Draft picks are now selectable on both the You Give and You Receive sides.
- Counter offers no longer only say no; they can request concrete added assets.

### Verified

- `dotnet build HockeyGmLegacy.slnx --no-restore`
- `dotnet run --project tests/LegacyEngine.Tests`
- `dotnet run --project client/AlphaDesktop -- --smoke-test`

### Not Added

- No Godot.
- No retained salary.
- No conditional picks.
- No no-trade clauses.
- No three-team trades.
- No salary cap expansion.
- No game simulation changes.

## Recently Completed

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

- Alpha 6.3 - TBD.
