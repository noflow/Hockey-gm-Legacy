# Changelog

## Current - Alpha 5.7

### Added

- Agent Engine v1 with agents, agent profiles, personalities, negotiation styles, reputation, relationships, client lists, and history.
- Player representation records with representation type, start date, agent id where applicable, and representation history.
- Agent negotiation reviews for contract and free-agency offers, including opinion, likelihood, biggest concern, requested improvement, risk, and counter suggestion.
- Agent / Representation section in player dossiers.
- AlphaDesktop agent readouts for Agent Card, client, relationship, negotiation style, agent comments, history, Improve Offer, Compare, and View Agent.
- Tests covering generated agents, player assignments, relationships, negotiation style effects, free agency, dossiers, history, inbox wording, and UI exposure.

### Changed

- Contract offer responses now come from agents or advisors instead of generic player-facing messages.
- Free agency offer messages now identify the agent/advisor and preserve the agent review in offer state.
- Scenario bootstrap now seeds agents and representation records for roster players, prospects, recruits, and free agents.
- Older compatibility tests now block conversation trees, arbitration, offer sheets, and Godot instead of blocking intentionally added agent/cap systems.
- AlphaDesktop version label updated to Alpha 5.7.

### Fixed

- Loaded or newly created desktop scenarios defensively ensure agent data exists before showing contract/free-agent panels.
- Contract inbox messages no longer use vague generic offer wording when an agent review exists.

### Verified

- `dotnet build HockeyGmLegacy.slnx --no-restore`
- `dotnet run --project tests/LegacyEngine.Tests`
- `dotnet run --project client/AlphaDesktop -- --smoke-test`

### Not Added

- No Godot.
- No conversation trees.
- No voice dialogue.
- No arbitration.
- No offer sheets.
- No no-trade clauses.
- No LTIR.

## Recently Completed

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

- Alpha 5.8 - TBD.
