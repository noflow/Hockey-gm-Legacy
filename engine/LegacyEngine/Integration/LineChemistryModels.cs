namespace LegacyEngine.Integration;

public enum LineChemistryGrade
{
    Excellent,
    Good,
    Neutral,
    Poor,
    Problem
}

public enum LineChemistryUnitType
{
    ForwardLine,
    DefensePair,
    GoalieDepth,
    Team
}

public enum LineChemistryFactorType
{
    PlayerTypeFit,
    HandednessBalance,
    PositionFit,
    RoleFit,
    AgeExperienceMix,
    PersonalityFit,
    RelationshipFit,
    CoachPhilosophyFit,
    MoraleConfidence,
    PromiseSatisfaction,
    RecentPerformancePlaceholder
}

public sealed record LineChemistryScore(
    int Value,
    LineChemistryGrade Grade,
    string ScoreBand)
{
    public void Validate()
    {
        if (Value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(Value), "Line chemistry score must be between 0 and 100.");
        }

        if (string.IsNullOrWhiteSpace(ScoreBand))
        {
            throw new ArgumentException("Line chemistry score requires a readable score band.", nameof(ScoreBand));
        }
    }
}

public sealed record LineChemistryFactor(
    LineChemistryFactorType FactorType,
    int Modifier,
    string Summary)
{
    public void Validate()
    {
        if (Modifier is < -25 or > 25)
        {
            throw new ArgumentOutOfRangeException(nameof(Modifier), "Line chemistry factor modifiers must stay modest.");
        }

        if (string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Line chemistry factor requires a readable summary.", nameof(Summary));
        }
    }
}

public sealed record LineChemistry(
    string UnitId,
    LineChemistryUnitType UnitType,
    string Label,
    LineChemistryScore Score,
    IReadOnlyList<string> PlayerIds,
    IReadOnlyList<string> PlayerNames,
    IReadOnlyList<LineChemistryFactor> Factors,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Weaknesses,
    string Recommendation,
    string DevelopmentNote,
    string RelationshipNote,
    string RolePromiseNote)
{
    public bool IsMajorIssue => Score.Grade is LineChemistryGrade.Poor or LineChemistryGrade.Problem;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(UnitId)
            || string.IsNullOrWhiteSpace(Label)
            || string.IsNullOrWhiteSpace(Recommendation)
            || string.IsNullOrWhiteSpace(DevelopmentNote)
            || string.IsNullOrWhiteSpace(RelationshipNote)
            || string.IsNullOrWhiteSpace(RolePromiseNote))
        {
            throw new ArgumentException("Line chemistry requires readable report context.");
        }

        Score.Validate();
        foreach (var factor in Factors)
        {
            factor.Validate();
        }
    }
}

public sealed record LineChemistryReport(
    string ReportId,
    string OrganizationId,
    DateOnly CreatedOn,
    LineChemistry Overall,
    IReadOnlyList<LineChemistry> ForwardLines,
    IReadOnlyList<LineChemistry> DefensePairs,
    LineChemistry GoalieDepth,
    string BestLine,
    string WorstLine,
    IReadOnlyList<string> MajorConcerns,
    IReadOnlyList<string> CoachRecommendations,
    IReadOnlyList<string> HistoryEvents)
{
    public IReadOnlyList<LineChemistry> Units =>
        ForwardLines.Concat(DefensePairs).Append(GoalieDepth).Prepend(Overall).ToArray();

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ReportId)
            || string.IsNullOrWhiteSpace(OrganizationId)
            || string.IsNullOrWhiteSpace(BestLine)
            || string.IsNullOrWhiteSpace(WorstLine))
        {
            throw new ArgumentException("Line chemistry report requires identity and summary context.");
        }

        Overall.Validate();
        GoalieDepth.Validate();
        foreach (var line in ForwardLines)
        {
            line.Validate();
        }

        foreach (var pair in DefensePairs)
        {
            pair.Validate();
        }
    }
}
