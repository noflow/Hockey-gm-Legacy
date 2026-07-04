# Technical Bible
## Rule Engine v1

## Purpose

The Rule Engine allows every league to operate from configurable rulebooks instead of hardcoded hockey rules.

## Responsibilities

- Load rulebooks
- Validate roster moves
- Validate contracts
- Validate trades
- Validate player eligibility
- Determine draft order
- Determine playoff qualification
- Determine valid contract clauses
- Determine budget restrictions
- Determine player rights

## Version 1 Required Rule Checks

- roster_size_valid
- age_eligible
- overage_slots_available
- import_slots_available
- contract_type_allowed
- contract_clause_allowed
- draft_eligible
- playoff_eligible
- budget_limit_valid

## Design Rule

The Rule Engine answers whether something is legal. The Human Intelligence System decides whether a person wants to do it. The Event Engine records what happened.
