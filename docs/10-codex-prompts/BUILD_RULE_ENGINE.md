# Codex Prompt – Build Rule Engine

Build the first LegacyEngine skeleton for Hockey GM Legacy.

Start with Milestone 002 – Rule Engine.

Requirements:

- Use C#.
- Do not build UI.
- Do not use Godot-specific code yet.
- Create a standalone RuleEngine module.
- Load rulebooks from JSON.
- Implement:
  - Rulebook
  - RulebookLoader
  - RuleValidationResult
  - RosterRuleValidator
  - EligibilityRuleValidator
  - ContractRuleValidator
  - DraftRuleValidator
  - PlayoffRuleValidator
  - BudgetRuleValidator
- Use `data/rulebooks/junior_v1.json` as the first rulebook.
- Add unit tests for valid and invalid rule cases.
- Keep the engine independent so it can later be used by Godot.

Important architecture rule:

The Rule Engine only answers whether an action is legal.

It does not decide whether the AI wants to take the action.

It does not create events.

It does not update UI.
