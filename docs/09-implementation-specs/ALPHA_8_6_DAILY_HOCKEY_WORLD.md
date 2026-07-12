# Alpha 8.6 - Daily Hockey World

## Purpose

Daily Hockey World makes an advance feel like arriving at the GM office. It is a presentation read model over existing engine state, not a new simulation or decision engine.

## Layout

The AlphaDesktop dashboard now includes a dedicated **Daily Hockey World** screen with three columns:

- **Today's Organization:** record, next game, owner mood, cap space, prospect progress, injuries, and expiring contracts.
- **League Pulse:** major story, trade/signing watch, standings, hot/cold player context, leaders, and transaction wire.
- **Today's Actions:** at most three existing Action Center items, with no second decision workflow.

Below the columns are concise Assistant GM, coach, head scout, and medical reports, plus Prospect Watch and Schedule & Calendar sections.

## Navigation

Each card has a destination in the existing GM Office. Player cards open the universal player profile; other cards route to the appropriate workspace. Advance Day, Advance to Next Game, and Advance to Month End all open Daily Hockey World after processing.

Before routing, the current GM Office location is stored in bounded Back navigation. A live draft modal can take foreground priority when the club is on the clock; the briefing remains available from Dashboard afterward.

## Briefing Archive

Every advance produces one compact, in-memory `DailyBriefingRecord` with the previous/current date, days advanced, stop reason, team record, headline, important actions, and selected period events. Records are deduplicated, capped at 180, saved with the scenario, and available from **Reports / History > Daily Briefings**. Reading or dismissing a briefing changes only its local archive state and never creates an inbox message.

## Boundaries

League Pulse is information. Action Center remains actionable decisions. Inbox remains communication. Journal remains history. The feature does not advance simulation, alter AI behavior, expand media, or create duplicate messages.
