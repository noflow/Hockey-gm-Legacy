namespace LegacyEngine.Integration;

public sealed record GameRecap(
    string GameId,
    DateOnly Date,
    GameBoxScore BoxScore,
    string WinnerOrganizationId,
    string WinnerTeam,
    IReadOnlyList<string> PeriodScoringSummary,
    IReadOnlyList<string> ThreeStars,
    IReadOnlyList<GamePlayerSummary> NotablePlayers,
    IReadOnlyList<GameGoalieSummary> GoalieSummaries,
    IReadOnlyList<GameKeyMoment> KeyMoments,
    IReadOnlyList<string> InjuryNotes,
    IReadOnlyList<string> DevelopmentNotes,
    string NarrativeSummary)
{
    public string TopLineSummary { get; init; } = "Top line summary not available.";

    public string SpecialTeamsNote { get; init; } = "Special teams note not available.";

    public string TacticalNote { get; init; } = "Tactical note not available.";

    public string ChemistryNote { get; init; } = "Chemistry note not available.";

    public string GoalieUsageNote { get; init; } = "Goalie usage note not available.";

    public string KeyConcern { get; init; } = "No key concern recorded.";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(GameId) || string.IsNullOrWhiteSpace(WinnerOrganizationId) || string.IsNullOrWhiteSpace(WinnerTeam))
        {
            throw new ArgumentException("Game recap requires game and winner identity.");
        }

        BoxScore.Validate();
        if (ThreeStars.Count == 0 || string.IsNullOrWhiteSpace(NarrativeSummary))
        {
            throw new ArgumentException("Game recap requires three stars and narrative summary.", nameof(ThreeStars));
        }

        foreach (var text in new[] { TopLineSummary, SpecialTeamsNote, TacticalNote, ChemistryNote, GoalieUsageNote, KeyConcern })
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Game recap v2 notes must be readable.");
            }
        }

        foreach (var player in NotablePlayers)
        {
            player.Validate();
        }

        foreach (var goalie in GoalieSummaries)
        {
            goalie.Validate();
        }

        foreach (var moment in KeyMoments)
        {
            moment.Validate();
        }
    }
}

public sealed record GameBoxScore(
    GameTeamSummary Home,
    GameTeamSummary Away,
    string FinalScore,
    string ShotsSummary,
    string PowerPlaySummary)
{
    public void Validate()
    {
        Home.Validate();
        Away.Validate();

        if (string.IsNullOrWhiteSpace(FinalScore) || string.IsNullOrWhiteSpace(ShotsSummary) || string.IsNullOrWhiteSpace(PowerPlaySummary))
        {
            throw new ArgumentException("Box score requires score, shots, and power play summaries.");
        }
    }
}

public sealed record GameTeamSummary(
    string OrganizationId,
    string TeamName,
    int Goals,
    int Shots,
    string PowerPlay,
    bool IsWinner)
{
    public int GoalDifferentialAgainst(GameTeamSummary opponent) => Goals - opponent.Goals;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OrganizationId) || string.IsNullOrWhiteSpace(TeamName) || string.IsNullOrWhiteSpace(PowerPlay))
        {
            throw new ArgumentException("Game team summary requires team identity and power play summary.");
        }

        if (Goals < 0 || Shots < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Goals), "Goals and shots cannot be negative.");
        }
    }
}

public sealed record GamePlayerSummary(
    string PersonId,
    string PlayerName,
    string TeamName,
    int Goals,
    int Assists,
    int Points,
    int PlusMinus,
    string Note)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId) || string.IsNullOrWhiteSpace(PlayerName) || string.IsNullOrWhiteSpace(TeamName) || string.IsNullOrWhiteSpace(Note))
        {
            throw new ArgumentException("Game player summary requires identity and note.");
        }
    }
}

public sealed record GameGoalieSummary(
    string PersonId,
    string PlayerName,
    string TeamName,
    int Saves,
    int GoalsAgainst,
    decimal SavePercentage,
    bool Won,
    string Note)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PersonId) || string.IsNullOrWhiteSpace(PlayerName) || string.IsNullOrWhiteSpace(TeamName) || string.IsNullOrWhiteSpace(Note))
        {
            throw new ArgumentException("Game goalie summary requires identity and note.");
        }

        if (Saves < 0 || GoalsAgainst < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Saves), "Goalie stats cannot be negative.");
        }
    }
}

public sealed record GameKeyMoment(
    string Period,
    string Time,
    string Description)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Period) || string.IsNullOrWhiteSpace(Time) || string.IsNullOrWhiteSpace(Description))
        {
            throw new ArgumentException("Game key moment requires period, time, and description.");
        }
    }
}
