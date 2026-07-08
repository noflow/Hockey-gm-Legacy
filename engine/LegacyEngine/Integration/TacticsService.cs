using LegacyEngine.Rosters;
using LegacyEngine.Staff;

namespace LegacyEngine.Integration;

public sealed class TacticsService
{
    private readonly StaffCoachingService _staffCoaching = new();
    private readonly LineupService _lineups = new();
    private readonly LineChemistryService _chemistry = new();
    private readonly GameUsageService _gameUsage = new();

    public NewGmScenarioSnapshot EnsureTactics(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var ready = EnsureInputs(scenario);
        if (ready.CurrentTactics is not null)
        {
            return ready;
        }

        var tactics = BuildDefaultTactics(ready);
        var updated = ready with { CurrentTactics = tactics };
        updated.Validate();
        return updated;
    }

    public TeamTactics BuildDefaultTactics(NewGmScenarioSnapshot scenario)
    {
        var ready = EnsureInputs(scenario);
        var coach = HeadCoachProfile(ready);
        var style = StyleFor(coach.Philosophy);
        var settings = SettingsFor(style, coach.Philosophy);
        var system = SystemFor(style, settings);
        var fit = BuildFitReport(ready, style, settings, coach);
        var recommendations = BuildRecommendations(ready, style, settings, fit, coach);
        var impacts = BuildPlayerImpacts(ready, style, settings);
        var modifiers = BuildModifierProfile(style, settings);
        var tactics = new TeamTactics(
            $"tactics:{ready.Organization.OrganizationId}:{ready.CurrentDate:yyyyMMdd}",
            ready.Organization.OrganizationId,
            ready.CurrentDate,
            style,
            system,
            settings.Physicality,
            settings.RiskLevel,
            settings,
            coach.PersonId,
            coach.StaffName,
            coach.Philosophy,
            fit,
            recommendations,
            impacts,
            modifiers,
            new[] { $"{ready.CurrentDate:yyyy-MM-dd}: Auto-set from {coach.StaffName}'s {coach.Philosophy} philosophy." },
            $"{ready.Organization.Name} uses a {style} tactical identity under {coach.StaffName}.");
        tactics.Validate();
        return tactics;
    }

    public TacticsManagementResult AutoSetFromCoach(NewGmScenarioSnapshot scenario)
    {
        var ready = EnsureInputs(scenario);
        var tactics = BuildDefaultTactics(ready);
        var updated = ready with { CurrentTactics = tactics };
        return Result(true, updated, $"Tactics auto-set from {tactics.CoachName}'s {tactics.CoachPhilosophy} philosophy.");
    }

    public TacticsManagementResult SetStyle(NewGmScenarioSnapshot scenario, TacticalStyle style) =>
        Update(scenario, tactics =>
        {
            var settings = SettingsFor(style, tactics.CoachPhilosophy);
            return tactics with
            {
                Style = style,
                System = SystemFor(style, settings),
                Settings = settings,
                Intensity = settings.Physicality,
                RiskLevel = settings.RiskLevel
            };
        }, $"Tactical style set to {Display(style)}.");

    public TacticsManagementResult SetForecheck(NewGmScenarioSnapshot scenario, ForecheckSetting setting) =>
        UpdateSettings(scenario, settings => settings with { Forecheck = setting }, $"Forecheck set to {Display(setting)}.");

    public TacticsManagementResult SetNeutralZone(NewGmScenarioSnapshot scenario, NeutralZoneSetting setting) =>
        UpdateSettings(scenario, settings => settings with { NeutralZone = setting }, $"Neutral zone set to {Display(setting)}.");

    public TacticsManagementResult SetDefensiveZone(NewGmScenarioSnapshot scenario, DefensiveZoneSetting setting) =>
        UpdateSettings(scenario, settings => settings with { DefensiveZone = setting }, $"Defensive zone set to {Display(setting)}.");

    public TacticsManagementResult SetBreakout(NewGmScenarioSnapshot scenario, BreakoutSetting setting) =>
        UpdateSettings(scenario, settings => settings with { Breakout = setting }, $"Breakout set to {Display(setting)}.");

    public TacticsManagementResult SetShotPreference(NewGmScenarioSnapshot scenario, ShotPreference setting) =>
        UpdateSettings(scenario, settings => settings with { ShotPreference = setting }, $"Shot preference set to {Display(setting)}.");

    public TacticsManagementResult SetPhysicality(NewGmScenarioSnapshot scenario, TacticalIntensity setting) =>
        UpdateSettings(scenario, settings => settings with { Physicality = setting }, $"Physicality set to {setting}.");

    public TacticsManagementResult SetRisk(NewGmScenarioSnapshot scenario, TacticalRiskLevel setting) =>
        UpdateSettings(scenario, settings => settings with { RiskLevel = setting }, $"Risk level set to {setting}.");

    public TacticsManagementResult SetPowerPlayStyle(NewGmScenarioSnapshot scenario, PowerPlayTacticalStyle setting) =>
        UpdateSettings(scenario, settings => settings with { PowerPlayStyle = setting }, $"Power play style set to {Display(setting)}.");

    public TacticsManagementResult SetPenaltyKillStyle(NewGmScenarioSnapshot scenario, PenaltyKillTacticalStyle setting) =>
        UpdateSettings(scenario, settings => settings with { PenaltyKillStyle = setting }, $"Penalty kill style set to {Display(setting)}.");

    public TacticalPlayerImpact BuildPlayerImpact(NewGmScenarioSnapshot scenario, string personId)
    {
        var tactics = CurrentTactics(scenario);
        var impact = tactics.PlayerImpacts.FirstOrDefault(impact => impact.PersonId == personId);
        if (impact is not null)
        {
            return impact;
        }

        var fallback = new TacticalPlayerImpact(personId, PersonName(scenario, personId), 0, 0, 0, "No tactical impact is currently tracked for this player.");
        fallback.Validate();
        return fallback;
    }

    public IReadOnlyList<string> BuildDossierTacticsLines(NewGmScenarioSnapshot scenario, string personId)
    {
        var tactics = CurrentTactics(scenario);
        var impact = BuildPlayerImpact(scenario, personId);
        return new[]
        {
            $"Team style: {Display(tactics.Style)}",
            $"System: {Display(tactics.System)}",
            $"Forecheck: {Display(tactics.Settings.Forecheck)}",
            $"Neutral zone: {Display(tactics.Settings.NeutralZone)}",
            $"Defensive zone: {Display(tactics.Settings.DefensiveZone)}",
            $"Breakout: {Display(tactics.Settings.Breakout)}",
            $"Shot preference: {Display(tactics.Settings.ShotPreference)}",
            $"Physicality: {tactics.Settings.Physicality}",
            $"Risk: {tactics.Settings.RiskLevel}",
            $"PP style: {Display(tactics.Settings.PowerPlayStyle)}",
            $"PK style: {Display(tactics.Settings.PenaltyKillStyle)}",
            $"Player impact: {impact.Summary}",
            $"Role satisfaction modifier: {impact.RoleSatisfactionModifier:+#;-#;0}",
            $"Development modifier: {impact.DevelopmentModifier:+#;-#;0}",
            $"Confidence modifier: {impact.ConfidenceModifier:+#;-#;0}"
        };
    }

    public TeamTactics CurrentTactics(NewGmScenarioSnapshot scenario) =>
        scenario.CurrentTactics ?? BuildDefaultTactics(scenario);

    private TacticsManagementResult UpdateSettings(NewGmScenarioSnapshot scenario, Func<TacticalSettings, TacticalSettings> update, string message) =>
        Update(scenario, tactics =>
        {
            var settings = update(tactics.Settings);
            return tactics with
            {
                Settings = settings,
                Intensity = settings.Physicality,
                RiskLevel = settings.RiskLevel,
                System = SystemFor(tactics.Style, settings)
            };
        }, message);

    private TacticsManagementResult Update(NewGmScenarioSnapshot scenario, Func<TeamTactics, TeamTactics> update, string message)
    {
        var ready = EnsureInputs(scenario);
        var current = CurrentTactics(ready);
        var changed = update(current);
        var coach = HeadCoachProfile(ready);
        var fit = BuildFitReport(ready, changed.Style, changed.Settings, coach);
        var recommendations = BuildRecommendations(ready, changed.Style, changed.Settings, fit, coach);
        var impacts = BuildPlayerImpacts(ready, changed.Style, changed.Settings);
        var modifier = BuildModifierProfile(changed.Style, changed.Settings);
        var history = changed.ChangeHistory.Append($"{ready.CurrentDate:yyyy-MM-dd}: {message}").TakeLast(12).ToArray();
        var tactics = changed with
        {
            CreatedOn = ready.CurrentDate,
            FitReport = fit,
            Recommendations = recommendations,
            PlayerImpacts = impacts,
            ModifierProfile = modifier,
            ChangeHistory = history,
            Summary = $"{ready.Organization.Name} now uses a {Display(changed.Style)} tactical identity with {Display(changed.Settings.Forecheck)} forecheck and {changed.Settings.RiskLevel} risk."
        };
        tactics.Validate();
        var updated = ready with { CurrentTactics = tactics };
        return Result(true, updated, message);
    }

    private NewGmScenarioSnapshot EnsureInputs(NewGmScenarioSnapshot scenario)
    {
        var lineup = scenario.CurrentLineup is null ? _lineups.EnsureLineup(scenario) : scenario;
        var chemistry = lineup.CurrentLineChemistry is null ? _chemistry.EnsureChemistry(lineup) : lineup;
        var usage = chemistry.CurrentGameUsage is null ? _gameUsage.EnsureGameUsage(chemistry) : chemistry;
        return usage;
    }

    private CoachingStaffProfile HeadCoachProfile(NewGmScenarioSnapshot scenario)
    {
        var profiles = _staffCoaching.BuildCoachProfiles(scenario);
        return profiles.FirstOrDefault(profile => profile.Role == StaffRole.HeadCoach)
            ?? profiles.FirstOrDefault(profile => profile.Department == StaffDepartment.Coaching)
            ?? profiles.First();
    }

    private TacticalFitReport BuildFitReport(NewGmScenarioSnapshot scenario, TacticalStyle style, TacticalSettings settings, CoachingStaffProfile coach)
    {
        var lineup = scenario.CurrentLineup ?? _lineups.BuildDefaultLineup(scenario, scenario.Organization.OrganizationId, scenario.Organization.Name, scenario.AlphaSnapshot.Roster.ActivePlayers);
        var assignments = lineup.Assignments.Where(assignment => assignment.Slot != LineupSlot.HealthyScratch).ToArray();
        var youngCount = assignments.Count(assignment => assignment.Age is <= 21);
        var veteranCount = assignments.Count(assignment => assignment.Age is >= 28);
        var forwardTypes = string.Join(" ", assignments.Select(assignment => assignment.PlayerType));
        var chemistry = scenario.CurrentLineChemistry ?? _chemistry.BuildReport(scenario with { CurrentLineup = lineup });
        var score = 58;
        var strengths = new List<string>();
        var weaknesses = new List<string>();
        var risks = new List<string>();

        if (coach.Philosophy == CoachPhilosophyFor(style) || PhilosophySupportsStyle(coach.Philosophy, style))
        {
            score += 12;
            strengths.Add($"Head coach philosophy supports {Display(style)} hockey.");
        }
        else
        {
            score -= 6;
            weaknesses.Add($"Head coach leans {coach.Philosophy}, which only partly matches {Display(style)}.");
        }

        if (style == TacticalStyle.Speed && (forwardTypes.Contains("Skating", StringComparison.OrdinalIgnoreCase) || forwardTypes.Contains("Speed", StringComparison.OrdinalIgnoreCase)))
        {
            score += 10;
            strengths.Add("Roster has enough pace to support a speed/transition approach.");
        }

        if (style == TacticalStyle.Physical && (forwardTypes.Contains("Power", StringComparison.OrdinalIgnoreCase) || forwardTypes.Contains("Physical", StringComparison.OrdinalIgnoreCase)))
        {
            score += 10;
            strengths.Add("Roster has enough size/edge to support a physical forecheck.");
        }

        if (style == TacticalStyle.Possession && (forwardTypes.Contains("Playmaker", StringComparison.OrdinalIgnoreCase) || forwardTypes.Contains("Two-Way", StringComparison.OrdinalIgnoreCase)))
        {
            score += 8;
            strengths.Add("Puck-movers and playmakers support a possession style.");
        }

        if (style is TacticalStyle.YouthDevelopment && youngCount >= 6)
        {
            score += 12;
            strengths.Add("Young roster fits a patient development identity.");
        }

        if (style is TacticalStyle.VeteranShelter && veteranCount >= 5)
        {
            score += 10;
            strengths.Add("Veteran group can handle a structured sheltering system.");
        }

        if (settings.RiskLevel == TacticalRiskLevel.High && youngCount >= 8)
        {
            score -= 18;
            risks.Add("High-risk tactics may overload a young roster.");
        }

        if (settings.Forecheck == ForecheckSetting.Aggressive && chemistry.Overall.Score.Grade is LineChemistryGrade.Poor or LineChemistryGrade.Problem)
        {
            score -= 14;
            risks.Add("Aggressive forecheck is risky while team chemistry is poor.");
        }

        if (settings.DefensiveZone == DefensiveZoneSetting.Pressure && assignments.Count(assignment => assignment.Position == RosterPosition.Defense && assignment.Age is <= 20) >= 3)
        {
            score -= 8;
            weaknesses.Add("Young defense group may need a calmer defensive-zone structure.");
        }

        if (settings.PowerPlayStyle == PowerPlayTacticalStyle.PointShot && scenario.CurrentGameUsage?.SpecialTeams.PowerPlayUnits.FirstOrDefault()?.QuarterbackDefense is null)
        {
            risks.Add("Point-shot power play needs a clear QB defenseman.");
            score -= 6;
        }

        if (strengths.Count == 0)
        {
            strengths.Add("Balanced roster pieces keep the current system playable.");
        }

        if (weaknesses.Count == 0)
        {
            weaknesses.Add("No major roster-system mismatch is visible yet.");
        }

        var grade = GradeFor(score);
        var report = new TacticalFitReport(
            $"tactical-fit:{scenario.Organization.OrganizationId}:{scenario.CurrentDate:yyyyMMdd}",
            scenario.CurrentDate,
            grade,
            Math.Clamp(score, 0, 100),
            strengths.Take(4).ToArray(),
            weaknesses.Take(4).ToArray(),
            risks.Take(4).ToArray(),
            CoachRecommendationFor(grade, risks, style, settings),
            $"Tactical fit is {grade} ({Math.Clamp(score, 0, 100)}/100) for a {Display(style)} identity.");
        report.Validate();
        return report;
    }

    private IReadOnlyList<TacticalRecommendation> BuildRecommendations(NewGmScenarioSnapshot scenario, TacticalStyle style, TacticalSettings settings, TacticalFitReport fit, CoachingStaffProfile coach)
    {
        var recommendations = new List<TacticalRecommendation>();
        if (fit.Grade is TacticalFitGrade.Poor or TacticalFitGrade.Problem)
        {
            recommendations.Add(new(
                $"tactics-rec:fit:{scenario.Organization.OrganizationId}",
                TacticalRecommendationType.MatchCoachPhilosophy,
                "Tactics do not fit roster",
                fit.Summary,
                "Review style, risk, and forecheck before the next stretch of games.",
                true));
        }

        if (fit.RiskWarnings.Any(warning => warning.Contains("young roster", StringComparison.OrdinalIgnoreCase)))
        {
            recommendations.Add(new(
                $"tactics-rec:risk:{scenario.Organization.OrganizationId}",
                TacticalRecommendationType.ReduceRisk,
                "High-risk tactics on young roster",
                "Prospects may lose confidence if asked to play too aggressively too soon.",
                "Lower risk or use a development/shelter style.",
                true));
        }

        if (!PhilosophySupportsStyle(coach.Philosophy, style))
        {
            recommendations.Add(new(
                $"tactics-rec:coach:{coach.PersonId}",
                TacticalRecommendationType.MatchCoachPhilosophy,
                "Coach recommends system change",
                $"{coach.StaffName}'s {coach.Philosophy} philosophy does not fully match {Display(style)}.",
                "Use Auto Set From Coach or adjust one setting at a time.",
                fit.Grade is TacticalFitGrade.Poor or TacticalFitGrade.Problem));
        }

        if (settings.PowerPlayStyle == PowerPlayTacticalStyle.PointShot && scenario.CurrentGameUsage?.SpecialTeams.PowerPlayUnits.Any(unit => unit.QuarterbackDefense is null) == true)
        {
            recommendations.Add(new(
                $"tactics-rec:pp:{scenario.Organization.OrganizationId}",
                TacticalRecommendationType.AdjustPowerPlayStyle,
                "Power play tactic mismatch",
                "Point-shot PP requires a reliable QB defenseman on each unit.",
                "Use balanced/skill movement or assign a QB defenseman.",
                true));
        }

        if (settings.PenaltyKillStyle == PenaltyKillTacticalStyle.Pressure && settings.RiskLevel == TacticalRiskLevel.High)
        {
            recommendations.Add(new(
                $"tactics-rec:pk:{scenario.Organization.OrganizationId}",
                TacticalRecommendationType.AdjustPenaltyKillStyle,
                "PK pressure adds risk",
                "High-risk team tactics paired with pressure PK can create fatigue and penalty-kill breakdowns.",
                "Consider Balanced or Shot Blocking PK until the group stabilizes.",
                fit.Grade is TacticalFitGrade.Poor or TacticalFitGrade.Problem));
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add(new(
                $"tactics-rec:stable:{scenario.Organization.OrganizationId}",
                TacticalRecommendationType.KeepCurrentSystem,
                "Tactical fit stable",
                "Current style, coach philosophy, and roster mix are workable.",
                "Keep monitoring fit after games and roster changes.",
                false));
        }

        foreach (var recommendation in recommendations)
        {
            recommendation.Validate();
        }

        return recommendations.Take(5).ToArray();
    }

    private IReadOnlyList<TacticalPlayerImpact> BuildPlayerImpacts(NewGmScenarioSnapshot scenario, TacticalStyle style, TacticalSettings settings)
    {
        var lineup = scenario.CurrentLineup ?? _lineups.BuildDefaultLineup(scenario, scenario.Organization.OrganizationId, scenario.Organization.Name, scenario.AlphaSnapshot.Roster.ActivePlayers);
        return lineup.Assignments
            .Where(assignment => assignment.Slot != LineupSlot.HealthyScratch)
            .Select(assignment =>
            {
                var roleMod = 0;
                var developmentMod = 0;
                var confidenceMod = 0;
                var notes = new List<string>();
                if (style == TacticalStyle.Offensive && IsOffensiveType(assignment.PlayerType))
                {
                    roleMod += 2;
                    confidenceMod += 1;
                    notes.Add("offensive system supports attacking role");
                }

                if (style == TacticalStyle.Defensive && IsDefensiveType(assignment.PlayerType))
                {
                    roleMod += 2;
                    developmentMod += 1;
                    notes.Add("defensive system fits checking habits");
                }

                if (style == TacticalStyle.Speed && assignment.PlayerType.Contains("Skating", StringComparison.OrdinalIgnoreCase))
                {
                    confidenceMod += 2;
                    notes.Add("speed system rewards skating pace");
                }

                if (style == TacticalStyle.YouthDevelopment && assignment.Age is <= 21)
                {
                    developmentMod += 3;
                    notes.Add("development identity gives young player patience");
                }

                if (settings.RiskLevel == TacticalRiskLevel.High && assignment.Age is <= 20)
                {
                    confidenceMod -= 2;
                    notes.Add("high-risk system may test young-player confidence");
                }

                if (settings.Physicality == TacticalIntensity.High && assignment.PlayerType.Contains("Power", StringComparison.OrdinalIgnoreCase))
                {
                    roleMod += 1;
                    notes.Add("physicality matches player type");
                }

                var summary = notes.Count == 0
                    ? "Tactical fit is neutral for this player right now."
                    : $"Tactical fit: {string.Join("; ", notes)}.";
                var impact = new TacticalPlayerImpact(
                    assignment.PersonId,
                    assignment.PlayerName,
                    Math.Clamp(roleMod, -10, 10),
                    Math.Clamp(developmentMod, -10, 10),
                    Math.Clamp(confidenceMod, -10, 10),
                    summary);
                impact.Validate();
                return impact;
            })
            .ToArray();
    }

    private static TacticalModifierProfile BuildModifierProfile(TacticalStyle style, TacticalSettings settings)
    {
        var offense = style is TacticalStyle.Offensive or TacticalStyle.Speed or TacticalStyle.Possession ? 4 : style == TacticalStyle.Defensive ? -2 : 0;
        var defense = style is TacticalStyle.Defensive or TacticalStyle.VeteranShelter ? 4 : settings.RiskLevel == TacticalRiskLevel.High ? -2 : 0;
        var pace = style is TacticalStyle.Speed or TacticalStyle.Counterattack ? 4 : settings.Breakout == BreakoutSetting.FastTransition ? 2 : 0;
        var physical = settings.Physicality switch { TacticalIntensity.High => 4, TacticalIntensity.Low => -3, _ => 0 };
        var risk = settings.RiskLevel switch { TacticalRiskLevel.High => 5, TacticalRiskLevel.Low => -4, _ => 0 };
        var special = settings.PowerPlayStyle == PowerPlayTacticalStyle.SkillMovement || settings.PenaltyKillStyle == PenaltyKillTacticalStyle.Pressure ? 2 : 0;
        var profile = new TacticalModifierProfile(
            Math.Clamp(offense, -10, 10),
            Math.Clamp(defense, -10, 10),
            Math.Clamp(pace, -10, 10),
            Math.Clamp(physical, -10, 10),
            Math.Clamp(risk, -10, 10),
            Math.Clamp(special, -10, 10),
            "Small tendency modifiers are exposed for a future simulator pass; current v1 does not overhaul game simulation.");
        profile.Validate();
        return profile;
    }

    private static TacticalStyle StyleFor(CoachPhilosophy philosophy) =>
        philosophy switch
        {
            CoachPhilosophy.Offensive or CoachPhilosophy.Creativity => TacticalStyle.Offensive,
            CoachPhilosophy.Defensive or CoachPhilosophy.Discipline => TacticalStyle.Defensive,
            CoachPhilosophy.Physical => TacticalStyle.Physical,
            CoachPhilosophy.Speed => TacticalStyle.Speed,
            CoachPhilosophy.PuckPossession => TacticalStyle.Possession,
            CoachPhilosophy.CounterAttack => TacticalStyle.Counterattack,
            CoachPhilosophy.PlayerDevelopment or CoachPhilosophy.YouthFocus => TacticalStyle.YouthDevelopment,
            CoachPhilosophy.VeteranFocus => TacticalStyle.VeteranShelter,
            _ => TacticalStyle.Balanced
        };

    private static CoachPhilosophy CoachPhilosophyFor(TacticalStyle style) =>
        style switch
        {
            TacticalStyle.Offensive => CoachPhilosophy.Offensive,
            TacticalStyle.Defensive => CoachPhilosophy.Defensive,
            TacticalStyle.Physical => CoachPhilosophy.Physical,
            TacticalStyle.Speed => CoachPhilosophy.Speed,
            TacticalStyle.Possession => CoachPhilosophy.PuckPossession,
            TacticalStyle.Counterattack => CoachPhilosophy.CounterAttack,
            TacticalStyle.YouthDevelopment => CoachPhilosophy.PlayerDevelopment,
            TacticalStyle.VeteranShelter => CoachPhilosophy.VeteranFocus,
            _ => CoachPhilosophy.Balanced
        };

    private static TacticalSettings SettingsFor(TacticalStyle style, CoachPhilosophy philosophy) =>
        style switch
        {
            TacticalStyle.Offensive => new(ForecheckSetting.Aggressive, NeutralZoneSetting.Pressure, DefensiveZoneSetting.Balanced, BreakoutSetting.FastTransition, ShotPreference.VolumeShooting, TacticalIntensity.Normal, TacticalRiskLevel.High, PowerPlayTacticalStyle.SkillMovement, PenaltyKillTacticalStyle.Balanced),
            TacticalStyle.Defensive => new(ForecheckSetting.Conservative, NeutralZoneSetting.Passive, DefensiveZoneSetting.Collapse, BreakoutSetting.Safe, ShotPreference.QualityChances, TacticalIntensity.Normal, TacticalRiskLevel.Low, PowerPlayTacticalStyle.Balanced, PenaltyKillTacticalStyle.PassiveBox),
            TacticalStyle.Physical => new(ForecheckSetting.Aggressive, NeutralZoneSetting.Balanced, DefensiveZoneSetting.Pressure, BreakoutSetting.Balanced, ShotPreference.VolumeShooting, TacticalIntensity.High, TacticalRiskLevel.Medium, PowerPlayTacticalStyle.NetFront, PenaltyKillTacticalStyle.ShotBlocking),
            TacticalStyle.Speed => new(ForecheckSetting.Aggressive, NeutralZoneSetting.Pressure, DefensiveZoneSetting.Balanced, BreakoutSetting.FastTransition, ShotPreference.Balanced, TacticalIntensity.Normal, TacticalRiskLevel.Medium, PowerPlayTacticalStyle.SkillMovement, PenaltyKillTacticalStyle.Pressure),
            TacticalStyle.Possession => new(ForecheckSetting.Balanced, NeutralZoneSetting.Balanced, DefensiveZoneSetting.Balanced, BreakoutSetting.Balanced, ShotPreference.QualityChances, TacticalIntensity.Normal, TacticalRiskLevel.Medium, PowerPlayTacticalStyle.SkillMovement, PenaltyKillTacticalStyle.Balanced),
            TacticalStyle.Counterattack => new(ForecheckSetting.Conservative, NeutralZoneSetting.Pressure, DefensiveZoneSetting.Collapse, BreakoutSetting.FastTransition, ShotPreference.QualityChances, TacticalIntensity.Normal, TacticalRiskLevel.Medium, PowerPlayTacticalStyle.Balanced, PenaltyKillTacticalStyle.Pressure),
            TacticalStyle.YouthDevelopment => new(ForecheckSetting.Balanced, NeutralZoneSetting.Balanced, DefensiveZoneSetting.Balanced, BreakoutSetting.Safe, ShotPreference.Balanced, TacticalIntensity.Low, TacticalRiskLevel.Low, PowerPlayTacticalStyle.Balanced, PenaltyKillTacticalStyle.Balanced),
            TacticalStyle.VeteranShelter => new(ForecheckSetting.Conservative, NeutralZoneSetting.Passive, DefensiveZoneSetting.Collapse, BreakoutSetting.Safe, ShotPreference.QualityChances, TacticalIntensity.Low, TacticalRiskLevel.Low, PowerPlayTacticalStyle.PointShot, PenaltyKillTacticalStyle.PassiveBox),
            _ => philosophy == CoachPhilosophy.Discipline
                ? new(ForecheckSetting.Balanced, NeutralZoneSetting.Balanced, DefensiveZoneSetting.Collapse, BreakoutSetting.Safe, ShotPreference.Balanced, TacticalIntensity.Normal, TacticalRiskLevel.Low, PowerPlayTacticalStyle.Balanced, PenaltyKillTacticalStyle.Balanced)
                : new(ForecheckSetting.Balanced, NeutralZoneSetting.Balanced, DefensiveZoneSetting.Balanced, BreakoutSetting.Balanced, ShotPreference.Balanced, TacticalIntensity.Normal, TacticalRiskLevel.Medium, PowerPlayTacticalStyle.Balanced, PenaltyKillTacticalStyle.Balanced)
        };

    private static TacticalSystem SystemFor(TacticalStyle style, TacticalSettings settings) =>
        style switch
        {
            TacticalStyle.Offensive => TacticalSystem.AttackFirst,
            TacticalStyle.Defensive => TacticalSystem.StructuredDefense,
            TacticalStyle.Physical => TacticalSystem.HeavyForecheck,
            TacticalStyle.Speed => TacticalSystem.PaceAndPressure,
            TacticalStyle.Possession => TacticalSystem.PuckPossession,
            TacticalStyle.Counterattack => TacticalSystem.CounterPunch,
            TacticalStyle.YouthDevelopment or TacticalStyle.VeteranShelter => TacticalSystem.DevelopmentShelter,
            _ => settings.RiskLevel == TacticalRiskLevel.Low ? TacticalSystem.StructuredDefense : TacticalSystem.BalancedStructure
        };

    private static TacticalFitGrade GradeFor(int score) =>
        score >= 84 ? TacticalFitGrade.Excellent :
        score >= 68 ? TacticalFitGrade.Good :
        score >= 50 ? TacticalFitGrade.Neutral :
        score >= 34 ? TacticalFitGrade.Poor :
        TacticalFitGrade.Problem;

    private static string CoachRecommendationFor(TacticalFitGrade grade, IReadOnlyList<string> risks, TacticalStyle style, TacticalSettings settings)
    {
        if (risks.Count > 0)
        {
            return $"Coach recommends lowering risk or adjusting {Display(style)} details: {risks[0]}";
        }

        return grade switch
        {
            TacticalFitGrade.Excellent or TacticalFitGrade.Good => "Coach is comfortable with the current tactical identity.",
            TacticalFitGrade.Neutral => "Coach says the system is workable, but one or two settings could fit better.",
            _ => $"Coach recommends revisiting {Display(style)} and {Display(settings.Forecheck)} forecheck."
        };
    }

    private static bool PhilosophySupportsStyle(CoachPhilosophy philosophy, TacticalStyle style) =>
        StyleFor(philosophy) == style
        || (philosophy == CoachPhilosophy.YouthFocus && style == TacticalStyle.YouthDevelopment)
        || (philosophy == CoachPhilosophy.Discipline && style is TacticalStyle.Defensive or TacticalStyle.VeteranShelter)
        || (philosophy == CoachPhilosophy.Creativity && style is TacticalStyle.Offensive or TacticalStyle.Possession);

    private static bool IsOffensiveType(string playerType) =>
        playerType.Contains("Scoring", StringComparison.OrdinalIgnoreCase)
        || playerType.Contains("Shooter", StringComparison.OrdinalIgnoreCase)
        || playerType.Contains("Playmaker", StringComparison.OrdinalIgnoreCase)
        || playerType.Contains("Offensive", StringComparison.OrdinalIgnoreCase);

    private static bool IsDefensiveType(string playerType) =>
        playerType.Contains("Two-Way", StringComparison.OrdinalIgnoreCase)
        || playerType.Contains("Defensive", StringComparison.OrdinalIgnoreCase)
        || playerType.Contains("Checking", StringComparison.OrdinalIgnoreCase)
        || playerType.Contains("Shutdown", StringComparison.OrdinalIgnoreCase);

    public static string Display(object value) =>
        value.ToString()!
            .Replace("FastTransition", "Fast Transition", StringComparison.Ordinal)
            .Replace("VolumeShooting", "Volume Shooting", StringComparison.Ordinal)
            .Replace("QualityChances", "Quality Chances", StringComparison.Ordinal)
            .Replace("PointShot", "Point Shot", StringComparison.Ordinal)
            .Replace("NetFront", "Net Front", StringComparison.Ordinal)
            .Replace("SkillMovement", "Skill / Movement", StringComparison.Ordinal)
            .Replace("OverloadPlaceholder", "Overload placeholder", StringComparison.Ordinal)
            .Replace("PassiveBox", "Passive Box", StringComparison.Ordinal)
            .Replace("ShotBlocking", "Shot Blocking", StringComparison.Ordinal)
            .Replace("YouthDevelopment", "Youth Development", StringComparison.Ordinal)
            .Replace("VeteranShelter", "Veteran Shelter", StringComparison.Ordinal)
            .Replace("BalancedStructure", "Balanced Structure", StringComparison.Ordinal)
            .Replace("StructuredDefense", "Structured Defense", StringComparison.Ordinal)
            .Replace("AttackFirst", "Attack First", StringComparison.Ordinal)
            .Replace("PaceAndPressure", "Pace and Pressure", StringComparison.Ordinal)
            .Replace("HeavyForecheck", "Heavy Forecheck", StringComparison.Ordinal)
            .Replace("PuckPossession", "Puck Possession", StringComparison.Ordinal)
            .Replace("CounterPunch", "Counter Punch", StringComparison.Ordinal)
            .Replace("DevelopmentShelter", "Development Shelter", StringComparison.Ordinal);

    private static string PersonName(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? scenario.ProspectRights.FirstOrDefault(prospect => prospect.ProspectPersonId == personId)?.ProspectName
        ?? personId;

    private static TacticsManagementResult Result(bool success, NewGmScenarioSnapshot scenario, string message)
    {
        var result = new TacticsManagementResult(success, scenario, message);
        result.Validate();
        return result;
    }
}
