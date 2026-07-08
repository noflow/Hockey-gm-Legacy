# Changelog

## Current - Alpha 6.3

### Added

- Relationship Expansion models for expanded relationship types, trends, change triggers, conflicts, chemistry labels, and impact summaries.
- Relationship profiles for GM/player, GM/staff, GM/owner, GM/agent, player/coach, staff/staff, staff/owner, organization/player, organization/staff, organization/agent, and organization/organization links.
- Relationship change history records with reason, amount, date, related event reference, and visible explanation.
- Simple conflict records for tension, trust issues, broken promises, role frustration, personality clashes, and staff disagreement.
- Chemistry summaries for roster, staff, scouting department, coach-player fit, and GM office relationships.
- Tests covering relationship types, change history, signing/rejected-offer/trade/broken-promise effects, contract impact, staff chemistry, dossiers, Action Center conflicts, save/load preservation, and forbidden dependency checks.

### Changed

- Contract offer evaluation now includes a modest relationship modifier and explains that impact.
- Staff chemistry now reads expanded relationship profiles and active conflicts.
- Player dossiers now show expanded relationship score, trend, key moments, recent changes, and conflict context.
- Owner, Organization Health, and Relationships desktop views now surface relationship health and chemistry.
- New GM scenarios and loaded careers backfill relationship expansion state automatically.
- Save/load now preserves relationship profiles, change history, conflicts, and chemistry through the scenario snapshot.
- AlphaDesktop version label updated to Alpha 6.3.

### Fixed

- Staff relationship conflicts now appear in staff chemistry warnings even when the numeric chemistry score has not fully collapsed yet.
- Relationship event helpers infer staff, owner, agent, or player target type instead of treating every GM relationship as GM-player.

### Verified

- `dotnet build HockeyGmLegacy.slnx --no-restore`
- `dotnet run --project tests/LegacyEngine.Tests`
- `dotnet run --project client/AlphaDesktop -- --smoke-test`

### Not Added

- No Godot.
- No full morale/drama engine.
- No social media or media system.
- No firing system.
- No game simulation changes.

## Recently Completed

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

- Alpha 6.4 - TBD.
