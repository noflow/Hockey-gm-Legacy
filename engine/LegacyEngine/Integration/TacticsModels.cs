namespace LegacyEngine.Integration;

public enum TacticalStyle
{
    Balanced,
    Offensive,
    Defensive,
    Physical,
    Speed,
    Possession,
    Counterattack,
    YouthDevelopment,
    VeteranShelter
}

public enum TacticalSystem
{
    BalancedStructure,
    StructuredDefense,
    AttackFirst,
    PaceAndPressure,
    HeavyForecheck,
    PuckPossession,
    CounterPunch,
    DevelopmentShelter
}

public enum TacticalIntensity
{
    Low,
    Normal,
    High
}

public enum TacticalRiskLevel
{
    Low,
    Medium,
    High
}

public enum ForecheckSetting
{
    Conservative,
    Balanced,
    Aggressive
}

public enum NeutralZoneSetting
{
    Passive,
    Balanced,
    Pressure
}

public enum DefensiveZoneSetting
{
    Collapse,
    Balanced,
    Pressure
}

public enum BreakoutSetting
{
    Safe,
    Balanced,
    FastTransition
}

public enum ShotPreference
{
    VolumeShooting,
    QualityChances,
    Balanced
}

public enum PowerPlayTacticalStyle
{
    Balanced,
    PointShot,
    NetFront,
    SkillMovement,
    OverloadPlaceholder
}

public enum PenaltyKillTacticalStyle
{
    PassiveBox,
    Pressure,
    ShotBlocking,
    Balanced
}

public enum TacticalFitGrade
{
    Excellent,
    Good,
    Neutral,
    Poor,
    Problem
}

public enum TacticalRecommendationType
{
    KeepCurrentSystem,
    ReduceRisk,
    IncreasePace,
    AddStructure,
    MatchCoachPhilosophy,
    ShelterYoungRoster,
    AdjustPowerPlayStyle,
    AdjustPenaltyKillStyle
}

public sealed record TacticalSettings(
    ForecheckSetting Forecheck,
    NeutralZoneSetting NeutralZone,
    DefensiveZoneSetting DefensiveZone,
    BreakoutSetting Breakout,
    ShotPreference ShotPreference,
    TacticalIntensity Physicality,
    TacticalRiskLevel RiskLevel,
    PowerPlayTacticalStyle PowerPlayStyle,
    PenaltyKillTacticalStyle PenaltyKillStyle)
{
    public void Validate()
    {
        if (!Enum.IsDefined(Forecheck)
            || !Enum.IsDefined(NeutralZone)
            || !Enum.IsDefined(DefensiveZone)
            || !Enum.IsDefined(Breakout)
            || !Enum.IsDefined(ShotPreference)
            || !Enum.IsDefined(Physicality)
            || !Enum.IsDefined(RiskLevel)
            || !Enum.IsDefined(PowerPlayStyle)
            || !Enum.IsDefined(PenaltyKillStyle))
        {
            throw new ArgumentException("Tactical settings contain an unsupported value.");
        }
    }
}

public sealed record TacticalModifierProfile(
    int OffenseTendency,
    int DefenseTendency,
    int PaceTendency,
    int PhysicalityTendency,
    int RiskTendency,
    int SpecialTeamsTendency,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Tactical modifier profile requires a summary.", nameof(Summary));
        }

        foreach (var value in new[] { OffenseTendency, DefenseTendency, PaceTendency, PhysicalityTendency, RiskTendency, SpecialTeamsTendency })
        {
            if (value is < -10 or > 10)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Tactical modifiers must stay small for v1.");
            }
        }
    }
}

public sealed record TacticalFitReport(
    string ReportId,
    DateOnly CreatedOn,
    TacticalFitGrade Grade,
    int Score,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Weaknesses,
    IReadOnlyList<string> RiskWarnings,
    string CoachRecommendation,
    string Summary)
{
    public bool HasMajorIssue => Grade is TacticalFitGrade.Poor or TacticalFitGrade.Problem || RiskWarnings.Count > 0;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ReportId)
            || string.IsNullOrWhiteSpace(CoachRecommendation)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Tactical fit report requires readable context.");
        }

        if (Score is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(Score), "Tactical fit score must be between zero and one hundred.");
        }
    }
}

public sealed record TacticalRecommendation(
    string RecommendationId,
    TacticalRecommendationType RecommendationType,
    string Title,
    string Reason,
    string SuggestedAction,
    bool IsImportant)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(RecommendationId)
            || string.IsNullOrWhiteSpace(Title)
            || string.IsNullOrWhiteSpace(Reason)
            || string.IsNullOrWhiteSpace(SuggestedAction))
        {
            throw new ArgumentException("Tactical recommendation requires readable context.");
        }
    }
}

public sealed record TacticalPlayerImpact(
    string PersonId,
    string PlayerName,
    int RoleSatisfactionModifier,
    int DevelopmentModifier,
    int ConfidenceModifier,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId)
            || string.IsNullOrWhiteSpace(PlayerName)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Tactical player impact requires readable player context.");
        }

        foreach (var value in new[] { RoleSatisfactionModifier, DevelopmentModifier, ConfidenceModifier })
        {
            if (value is < -10 or > 10)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Tactical player modifiers must stay modest.");
            }
        }
    }
}

public sealed record TeamTactics(
    string TacticsId,
    string OrganizationId,
    DateOnly CreatedOn,
    TacticalStyle Style,
    TacticalSystem System,
    TacticalIntensity Intensity,
    TacticalRiskLevel RiskLevel,
    TacticalSettings Settings,
    string CoachPersonId,
    string CoachName,
    CoachPhilosophy CoachPhilosophy,
    TacticalFitReport FitReport,
    IReadOnlyList<TacticalRecommendation> Recommendations,
    IReadOnlyList<TacticalPlayerImpact> PlayerImpacts,
    TacticalModifierProfile ModifierProfile,
    IReadOnlyList<string> ChangeHistory,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TacticsId)
            || string.IsNullOrWhiteSpace(OrganizationId)
            || string.IsNullOrWhiteSpace(CoachPersonId)
            || string.IsNullOrWhiteSpace(CoachName)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Team tactics requires identity and readable context.");
        }

        if (!Enum.IsDefined(Style) || !Enum.IsDefined(System) || !Enum.IsDefined(Intensity) || !Enum.IsDefined(RiskLevel) || !Enum.IsDefined(CoachPhilosophy))
        {
            throw new ArgumentException("Team tactics contains an unsupported tactical value.");
        }

        Settings.Validate();
        FitReport.Validate();
        ModifierProfile.Validate();
        foreach (var recommendation in Recommendations)
        {
            recommendation.Validate();
        }

        foreach (var impact in PlayerImpacts)
        {
            impact.Validate();
        }
    }
}

public sealed record TacticsManagementResult(
    bool Success,
    NewGmScenarioSnapshot ScenarioSnapshot,
    string Message)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Tactics management result requires a message.");
        }
    }
}
