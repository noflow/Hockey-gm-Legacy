# Alpha 8.5.3 - Existing NHL Workforce, Free Agents, and Expiring Contracts

## Purpose

New NHL careers begin in an established pro hockey world rather than a league full of identical young players on fresh contracts.

## Implemented Scope

- Deterministic league workforce profiles for every NHL organization.
- Team strategy-aware age and career-stage distributions.
- Selected-team roster, affiliate, prospects, contracts, ratings, and history remain the authoritative playable data.
- League-facing records describe other organizations without creating a second heavyweight simulation roster for every club.
- Starting free-agent markets include young players, prime replacement options, veterans, late-career depth, and retirement-watch candidates.
- Veteran market asks can prefer one-year NHL opportunities and contenders.
- Contract Management surfaces expiring contracts, RFA/UFA decisions, retirement watch, and a public league market watch.
- Existing world history uses pro league context and multi-year career summaries for older NHL players.

## Guardrails

- Workforce records are not a real-player database.
- Retirement watch identifies context only; it is not a full retirement-decision engine.
- Other-team contract information remains league-facing and does not expose private negotiations.
- No Godot or game-simulation system changes are introduced.

## Validation

`WorkforceRealismValidator` checks NHL age diversity, veterans, late-career players, mixed team stages, starting market composition, retirement-watch candidates, and upcoming contracts. It does not endlessly regenerate the world.
