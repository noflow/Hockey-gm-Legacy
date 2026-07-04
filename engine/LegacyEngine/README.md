# LegacyEngine

This folder will contain the simulation engine.

First build target:

`RuleEngine`

The engine should be testable without Godot.

## RuleEngine

Milestone 002 adds a standalone C# RuleEngine module under `LegacyEngine.RuleEngine`.

It loads JSON rulebooks and validates:

- roster limits
- player eligibility
- contract types, clauses, and salary caps
- draft availability, eligibility, and rounds
- playoff qualification counts
- owner budgets and hard salary caps

Run the milestone tests from the repository root:

```powershell
$env:APPDATA = (Join-Path (Get-Location) '.appdata')
dotnet run --project tests/LegacyEngine.Tests/LegacyEngine.Tests.csproj --no-restore
```
