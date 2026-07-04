# Database Schema v1

## Core Tables

- universes
- people
- person_roles
- organizations
- organization_members
- leagues
- rulebooks
- seasons
- players
- staff
- owners
- relationships
- events
- games
- standings
- stat_lines
- injuries
- contracts
- scouting_reports
- recruits
- facilities
- drafts
- draft_picks
- history_records
- news_items

## Rulebooks

```sql
CREATE TABLE rulebooks (
    rulebook_id TEXT PRIMARY KEY,
    universe_id TEXT NOT NULL,
    name TEXT NOT NULL,
    league_type TEXT NOT NULL,
    version TEXT NOT NULL,
    rules_json TEXT NOT NULL,
    active INTEGER DEFAULT 1
);
```

## Schema Rule

Never delete historical data.

If an entity becomes inactive, mark it inactive.
