namespace LegacyEngine.Integration;

public enum TeamStrengthBand
{
    Weak,
    BelowAverage,
    Average,
    Strong,
    Elite
}

public sealed record GameSimulationContext(
    ScheduledGame Game,
    TeamSimulationProfile HomeTeam,
    TeamSimulationProfile AwayTeam)
{
    public void Validate()
    {
        Game.Validate();
        HomeTeam.Validate();
        AwayTeam.Validate();
        if (HomeTeam.OrganizationId != Game.HomeOrganizationId || AwayTeam.OrganizationId != Game.AwayOrganizationId)
        {
            throw new ArgumentException("Game simulation context teams must match the scheduled game.");
        }
    }
}

public sealed record TeamSimulationProfile(
    string OrganizationId,
    string TeamName,
    bool IsHome,
    TeamStrengthBand Offense,
    TeamStrengthBand Defense,
    TeamStrengthBand Goaltending,
    TeamStrengthBand SpecialTeams,
    TeamStrengthBand Coaching,
    TeamStrengthBand Chemistry,
    int OffenseScore,
    int DefenseScore,
    int GoaltendingScore,
    int SpecialTeamsScore,
    int CoachingScore,
    int ChemistryScore,
    IReadOnlyList<string> AvailablePlayerIds,
    IReadOnlyList<string> UnavailablePlayerIds,
    IReadOnlyList<LineSimulationProfile> Lines,
    GoalieSimulationProfile Goalie,
    SpecialTeamsSimulationProfile SpecialTeamsProfile,
    TacticalSimulationProfile TacticalProfile,
    IReadOnlyList<string> Notes)
{
    public int OverallScore => (OffenseScore + DefenseScore + GoaltendingScore + SpecialTeamsScore + CoachingScore + ChemistryScore) / 6;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OrganizationId) || string.IsNullOrWhiteSpace(TeamName))
        {
            throw new ArgumentException("Team simulation profile requires team identity.");
        }

        foreach (var score in new[] { OffenseScore, DefenseScore, GoaltendingScore, SpecialTeamsScore, CoachingScore, ChemistryScore })
        {
            if (score is < 0 or > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(score), "Team simulation profile scores must stay between zero and one hundred.");
            }
        }

        foreach (var line in Lines)
        {
            line.Validate();
        }

        Goalie.Validate();
        SpecialTeamsProfile.Validate();
        TacticalProfile.Validate();
    }
}

public sealed record LineSimulationProfile(
    string UnitName,
    IReadOnlyList<string> PlayerIds,
    int UsageWeight,
    TeamStrengthBand Strength,
    LineChemistryGrade Chemistry,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(UnitName) || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Line simulation profile requires readable context.");
        }

        if (UsageWeight is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(UsageWeight), "Line usage weight must stay between zero and one hundred.");
        }
    }
}

public sealed record GoalieSimulationProfile(
    string? StarterPersonId,
    string StarterName,
    TeamStrengthBand Strength,
    int Score,
    int Fatigue,
    bool IsOverworked,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(StarterName) || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Goalie simulation profile requires readable context.");
        }

        if (Score is < 0 or > 100 || Fatigue is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(Score), "Goalie score and fatigue must stay between zero and one hundred.");
        }
    }
}

public sealed record SpecialTeamsSimulationProfile(
    TeamStrengthBand PowerPlay,
    TeamStrengthBand PenaltyKill,
    int PowerPlayScore,
    int PenaltyKillScore,
    int ExpectedPowerPlayChances,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Special teams simulation profile requires a summary.");
        }

        if (PowerPlayScore is < 0 or > 100 || PenaltyKillScore is < 0 or > 100 || ExpectedPowerPlayChances is < 0 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(PowerPlayScore), "Special teams profile values are outside the expected range.");
        }
    }
}

public sealed record TacticalSimulationProfile(
    string Style,
    int GoalsForTendency,
    int GoalsAgainstTendency,
    int ShotsTendency,
    int PenaltyTendency,
    int PaceTendency,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Style) || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Tactical simulation profile requires readable context.");
        }

        foreach (var value in new[] { GoalsForTendency, GoalsAgainstTendency, ShotsTendency, PenaltyTendency, PaceTendency })
        {
            if (value is < -20 or > 20)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Tactical tendencies must stay modest.");
            }
        }
    }
}

public sealed record PlayerGameStatAllocation(
    string PersonId,
    string PlayerName,
    int Goals,
    int Assists,
    int PlusMinus,
    int PenaltyMinutes,
    bool IncludedPowerPlayPoint,
    int OpportunityWeight,
    string UsageNote)
{
    public int Points => Goals + Assists;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId) || string.IsNullOrWhiteSpace(PlayerName) || string.IsNullOrWhiteSpace(UsageNote))
        {
            throw new ArgumentException("Player game stat allocation requires readable player context.");
        }
    }
}

public sealed record GoalieGameStatAllocation(
    string PersonId,
    string PlayerName,
    bool Won,
    int Saves,
    int GoalsAgainst,
    string UsageNote)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId) || string.IsNullOrWhiteSpace(PlayerName) || string.IsNullOrWhiteSpace(UsageNote))
        {
            throw new ArgumentException("Goalie game stat allocation requires readable goalie context.");
        }

        if (Saves < 0 || GoalsAgainst < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Saves), "Goalie game stats cannot be negative.");
        }
    }
}

public sealed record GameSimulationResultV2(
    GameResult Result,
    GameSimulationContext Context,
    int HomeShots,
    int AwayShots,
    int HomePowerPlayGoals,
    int HomePowerPlayChances,
    int AwayPowerPlayGoals,
    int AwayPowerPlayChances,
    IReadOnlyList<PlayerGameStatAllocation> PlayerTeamSkaterStats,
    GoalieGameStatAllocation? PlayerTeamGoalieStats,
    IReadOnlyList<PlayerMilestone> NewMilestones,
    string TopLineSummary,
    string SpecialTeamsNote,
    string TacticalNote,
    string ChemistryNote,
    string GoalieUsageNote,
    string InjuryNote,
    string DevelopmentNote,
    string KeyConcern,
    string NarrativeSummary)
{
    public void Validate()
    {
        Result.Validate();
        Context.Validate();
        if (HomeShots < 0 || AwayShots < 0 || HomePowerPlayGoals < 0 || AwayPowerPlayGoals < 0 || HomePowerPlayChances < 0 || AwayPowerPlayChances < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(HomeShots), "Game simulation counting stats cannot be negative.");
        }

        foreach (var stat in PlayerTeamSkaterStats)
        {
            stat.Validate();
        }

        PlayerTeamGoalieStats?.Validate();
        foreach (var milestone in NewMilestones)
        {
            milestone.Validate();
        }

        foreach (var text in new[] { TopLineSummary, SpecialTeamsNote, TacticalNote, ChemistryNote, GoalieUsageNote, InjuryNote, DevelopmentNote, KeyConcern, NarrativeSummary })
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Game simulation result requires readable summary text.");
            }
        }
    }
}
