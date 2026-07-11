# Alpha 8.4 UX Playtest Findings

## Scope

Alpha 8.4 reviewed the current AlphaDesktop experience as a player-facing GM office. This pass focuses on navigation, context, readability, accessibility, feedback, empty states, and common workflow friction. It does not add gameplay systems.

## Minimum Supported Resolution

- Minimum supported window: 920 x 620.
- Practical playtest targets: 1366 x 768, 1920 x 1080, and larger desktop windows.
- Trade Center and Draft War Room remain best at 1366 x 768 or larger because they intentionally show multiple working columns.

## Highest-Priority Findings Fixed

1. Buttons did not consistently feel interactive.
   - Fixed by making shared buttons focusable and keeping visible enabled/disabled states.

2. Players could lose context after opening another screen or person card.
   - Fixed by adding bounded Back/Forward navigation state and preserving selected person where practical.

3. The player could not always answer "where am I?"
   - Fixed by adding compact workspace breadcrumbs.

4. Common filters were easy to forget and slow to clear.
   - Fixed by adding Reset Filters actions for Roster and Action Center, plus Clear Search for global search.

5. Some empty states were too blank or generic.
   - Fixed Action Center empty state with player-facing next-step language.

6. Keyboard support was too thin for core workflows.
   - Fixed by adding Ctrl+S for save, Ctrl+F for search, Escape for safe popups, and Alt+Left/Alt+Right for navigation history.

7. Popups could trap keyboard users or return focus unpredictably.
   - Improved safe popup Escape handling, initial focus, and focus return.

8. Destructive actions needed clearer consequences.
   - Added confirmation wrappers for release, waivers, buyout confirmation, arbitration walk-away, and prospect rights release.

9. Successful actions were too easy to miss.
   - Added a persistent status feedback line in the header.

10. Save/load details were too raw.
   - Settings now shows GM, team, league, date, season, record, save version, compatibility, path, and last-saved status.

## Remaining UX Issues

- Some large text-report screens still need card/table replacements.
- Full scroll-position preservation is not implemented yet.
- Trade Center and Draft War Room need manual review at smaller desktop widths.
- Tooltip coverage is better but not complete across every advanced hockey term.
- The playtest checklist is local/in-memory text and not a full issue tracker.
- No full accessibility certification has been attempted.

## Confirmation Rules

Confirmation is required for irreversible or destructive actions:

- releasing players or staff
- waivers
- buyout confirmation
- arbitration walk-away
- releasing prospect rights
- major trade approval when routed through pending actions

Routine navigation, selection, filtering, comparison, and reversible proposal edits should not require confirmation.

## Local UX Counters

Alpha 8.4 adds local-only developer counters for button clicks and modal openings. These are not analytics, are not transmitted, and exist only to help alpha playtesting spot friction in the current session.

## No New Gameplay

This pass did not add new simulation, AI, contract, draft, trade, or game logic. AlphaDesktop remains a client around LegacyEngine state.
