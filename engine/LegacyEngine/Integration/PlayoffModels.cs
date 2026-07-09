namespace LegacyEngine.Integration;

public enum PlayoffStatus
{
    NotStarted,
    Seeding,
    InProgress,
    RoundComplete,
    Completed
}

public enum PlayoffFormatType
{
    Disabled,
    Top8,
    Top16,
    ConferenceTop8Placeholder
}

public sealed record PlayoffFormat(
    PlayoffFormatType FormatType,
    int TeamsQualify,
    int BestOf,
    bool ReseedEachRound,
    string Source)
{
    public bool IsEnabled => FormatType != PlayoffFormatType.Disabled && TeamsQualify > 1 && BestOf > 0;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Source))
        {
            throw new ArgumentException("Playoff format requires source context.");
        }

        if (TeamsQualify < 0 || BestOf < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(TeamsQualify), "Playoff format values cannot be negative.");
        }
    }
}

public sealed record PlayoffTeamSeed(
    string OrganizationId,
    string TeamName,
    int Seed,
    int RegularSeasonPoints,
    int Wins)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OrganizationId) || string.IsNullOrWhiteSpace(TeamName) || Seed < 1)
        {
            throw new ArgumentException("Playoff seed requires team identity and seed.");
        }
    }
}

public sealed record PlayoffGame(
    string PlayoffGameId,
    string SeriesId,
    int RoundNumber,
    int GameNumber,
    DateOnly Date,
    string HomeOrganizationId,
    string AwayOrganizationId,
    GameStatus Status = GameStatus.Scheduled,
    GameResult? Result = null)
{
    public ScheduledGame ToScheduledGame() =>
        new(PlayoffGameId, Date, HomeOrganizationId, AwayOrganizationId, Status, Result);

    public PlayoffGame Complete(GameResult result)
    {
        result.Validate();
        return this with { Status = GameStatus.Completed, Result = result };
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PlayoffGameId)
            || string.IsNullOrWhiteSpace(SeriesId)
            || string.IsNullOrWhiteSpace(HomeOrganizationId)
            || string.IsNullOrWhiteSpace(AwayOrganizationId))
        {
            throw new ArgumentException("Playoff game requires identity and teams.");
        }

        if (RoundNumber < 1 || GameNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(RoundNumber), "Playoff game round and game numbers must be positive.");
        }

        Result?.Validate();
    }
}

public sealed record PlayoffSeries(
    string SeriesId,
    int RoundNumber,
    int SeriesNumber,
    PlayoffTeamSeed HigherSeed,
    PlayoffTeamSeed LowerSeed,
    int BestOf,
    int HigherSeedWins = 0,
    int LowerSeedWins = 0,
    PlayoffStatus Status = PlayoffStatus.InProgress,
    IReadOnlyList<PlayoffGame> Games = null!,
    string? WinnerOrganizationId = null,
    string? LoserOrganizationId = null)
{
    public IReadOnlyList<PlayoffGame> GamesOrEmpty => Games ?? Array.Empty<PlayoffGame>();

    public bool IsComplete => Status == PlayoffStatus.Completed;

    public int WinsRequired => BestOf / 2 + 1;

    public PlayoffTeamSeed? WinnerSeed =>
        WinnerOrganizationId == HigherSeed.OrganizationId ? HigherSeed :
        WinnerOrganizationId == LowerSeed.OrganizationId ? LowerSeed :
        null;

    public PlayoffTeamSeed? LoserSeed =>
        LoserOrganizationId == HigherSeed.OrganizationId ? HigherSeed :
        LoserOrganizationId == LowerSeed.OrganizationId ? LowerSeed :
        null;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SeriesId) || RoundNumber < 1 || SeriesNumber < 1 || BestOf < 1)
        {
            throw new ArgumentException("Playoff series requires identity, round, and series length.");
        }

        HigherSeed.Validate();
        LowerSeed.Validate();
        foreach (var game in GamesOrEmpty)
        {
            game.Validate();
        }
    }
}

public sealed record PlayoffRound(
    int RoundNumber,
    string Name,
    PlayoffStatus Status,
    IReadOnlyList<PlayoffSeries> Series)
{
    public bool IsComplete => Series.Count > 0 && Series.All(series => series.IsComplete);

    public void Validate()
    {
        if (RoundNumber < 1 || string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Playoff round requires number and name.");
        }

        foreach (var series in Series)
        {
            series.Validate();
        }
    }
}

public sealed record PlayoffSeriesResult(
    string SeriesId,
    int RoundNumber,
    string WinnerOrganizationId,
    string WinnerTeamName,
    string LoserOrganizationId,
    string LoserTeamName,
    int WinnerWins,
    int LoserWins,
    string Summary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SeriesId)
            || string.IsNullOrWhiteSpace(WinnerOrganizationId)
            || string.IsNullOrWhiteSpace(WinnerTeamName)
            || string.IsNullOrWhiteSpace(LoserOrganizationId)
            || string.IsNullOrWhiteSpace(LoserTeamName)
            || string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Playoff series result requires readable winner and loser context.");
        }
    }
}

public sealed record PlayoffBracket(
    string BracketId,
    string LeagueId,
    int SeasonYear,
    PlayoffFormat Format,
    PlayoffStatus Status,
    IReadOnlyList<PlayoffTeamSeed> Seeds,
    IReadOnlyList<PlayoffTeamSeed> MissedPlayoffs,
    IReadOnlyList<PlayoffRound> Rounds,
    string? ChampionOrganizationId = null,
    string? ChampionTeamName = null,
    string? RunnerUpOrganizationId = null,
    string? RunnerUpTeamName = null,
    string? PlayoffMvpPlaceholder = null,
    IReadOnlyList<PlayoffSeriesResult>? CompletedSeries = null)
{
    public IReadOnlyList<PlayoffSeriesResult> Results => CompletedSeries ?? Array.Empty<PlayoffSeriesResult>();

    public PlayoffSeries? CurrentSeries =>
        Rounds.OrderBy(round => round.RoundNumber)
            .SelectMany(round => round.Series)
            .FirstOrDefault(series => !series.IsComplete);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BracketId) || string.IsNullOrWhiteSpace(LeagueId) || SeasonYear < 1)
        {
            throw new ArgumentException("Playoff bracket requires identity and season context.");
        }

        Format.Validate();
        foreach (var seed in Seeds)
        {
            seed.Validate();
        }

        foreach (var team in MissedPlayoffs)
        {
            team.Validate();
        }

        foreach (var round in Rounds)
        {
            round.Validate();
        }

        foreach (var result in Results)
        {
            result.Validate();
        }

        if (Status == PlayoffStatus.Completed && string.IsNullOrWhiteSpace(ChampionTeamName))
        {
            throw new ArgumentException("Completed playoff bracket requires a champion.");
        }
    }
}

public sealed record PlayoffState(
    PlayoffBracket? Bracket = null,
    IReadOnlyList<PlayerSeasonStatLine>? SkaterStats = null,
    IReadOnlyList<GoalieSeasonStatLine>? GoalieStats = null,
    IReadOnlyList<TeamSeasonStatLine>? TeamStats = null,
    IReadOnlyList<GameRecap>? GameRecaps = null,
    IReadOnlyList<LeagueTransaction>? LeagueNews = null,
    IReadOnlyList<AlphaInboxItem>? InboxItems = null)
{
    public static PlayoffState Empty { get; } = new();

    public IReadOnlyList<PlayerSeasonStatLine> PlayoffSkaterStats => SkaterStats ?? Array.Empty<PlayerSeasonStatLine>();

    public IReadOnlyList<GoalieSeasonStatLine> PlayoffGoalieStats => GoalieStats ?? Array.Empty<GoalieSeasonStatLine>();

    public IReadOnlyList<TeamSeasonStatLine> PlayoffTeamStats => TeamStats ?? Array.Empty<TeamSeasonStatLine>();

    public IReadOnlyList<GameRecap> PlayoffGameRecaps => GameRecaps ?? Array.Empty<GameRecap>();

    public IReadOnlyList<LeagueTransaction> PlayoffLeagueNews => LeagueNews ?? Array.Empty<LeagueTransaction>();

    public IReadOnlyList<AlphaInboxItem> PlayoffInboxItems => InboxItems ?? Array.Empty<AlphaInboxItem>();

    public void Validate()
    {
        Bracket?.Validate();
        foreach (var stat in PlayoffSkaterStats)
        {
            stat.Validate();
        }

        foreach (var stat in PlayoffGoalieStats)
        {
            stat.Validate();
        }

        foreach (var stat in PlayoffTeamStats)
        {
            stat.Validate();
        }

        foreach (var recap in PlayoffGameRecaps)
        {
            recap.Validate();
        }

        foreach (var news in PlayoffLeagueNews)
        {
            news.Validate();
        }
    }
}

public sealed record PlayoffSimulationResult(
    bool Success,
    NewGmScenarioSnapshot ScenarioSnapshot,
    PlayoffBracket? Bracket,
    IReadOnlyList<GameRecap> GameRecaps,
    IReadOnlyList<AlphaInboxItem> InboxItems,
    IReadOnlyList<LeagueTransaction> LeagueNews,
    string Message)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        Bracket?.Validate();
        foreach (var recap in GameRecaps)
        {
            recap.Validate();
        }

        foreach (var news in LeagueNews)
        {
            news.Validate();
        }

        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Playoff simulation result requires a message.");
        }
    }
}
