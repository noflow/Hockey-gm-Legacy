# Milestone 002 – Rule Engine Implementation Spec

## Purpose

The Rule Engine determines whether an action is legal under a league's rulebook.

It does not decide whether an action is smart.

It only answers:

**Is this allowed in this league?**

## Core Principle

```text
Human Intelligence System = wants to do it
Rule Engine = is it legal
Budget System = can we afford it
Event Engine = records what happened
```

## v1 Goal

Build a standalone Rule Engine that can:

- Load a league rulebook from JSON.
- Validate roster rules.
- Validate player eligibility.
- Validate contract rules.
- Validate draft rules.
- Validate playoff rules.
- Return clear success/failure results.
- Be tested without Godot.

## Required File

`data/rulebooks/junior_v1.json`

## Core Classes

- Rulebook
- RulebookLoader
- RuleValidationResult
- RosterRuleValidator
- EligibilityRuleValidator
- ContractRuleValidator
- DraftRuleValidator
- PlayoffRuleValidator
- BudgetRuleValidator

## RuleValidationResult

Every validation returns:

- is_valid
- rule_code
- message
- severity
- details

## Required Error Codes

- VALID
- MISSING_RULEBOOK_SECTION
- INVALID_RULEBOOK_FIELD
- ROSTER_TOO_SMALL
- ROSTER_TOO_LARGE
- ACTIVE_ROSTER_TOO_LARGE
- NOT_ENOUGH_GOALIES
- TOO_MANY_OVERAGE_PLAYERS
- TOO_MANY_IMPORT_PLAYERS
- PLAYER_TOO_YOUNG
- PLAYER_TOO_OLD
- CONTRACT_TYPE_NOT_ALLOWED
- CONTRACT_CLAUSE_NOT_ALLOWED
- SALARY_CAP_EXCEEDED
- BUDGET_EXCEEDED
- DRAFT_DISABLED
- PLAYER_NOT_DRAFT_ELIGIBLE
- INVALID_DRAFT_ROUND
- PLAYOFF_TEAMS_INVALID
- UNKNOWN_RULE_ERROR

## Completion Criteria

- `junior_v1.json` loads successfully.
- Validators exist.
- Error codes exist.
- Unit tests pass.
- No UI dependency.
- No Godot dependency.
