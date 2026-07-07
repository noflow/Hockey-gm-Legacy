using LegacyEngine.Contracts;
using LegacyEngine.Organizations;
using LegacyEngine.Scouting;
using LegacyEngine.Seasons;
using LegacyEngine.Staff;

namespace LegacyEngine.Integration;

public sealed record NewGmScenarioSnapshot(
    AlphaWorldSnapshot AlphaSnapshot,
    Organization Organization,
    Season Season,
    IReadOnlyList<StaffMember> StaffMembers,
    IReadOnlyList<Contract> Contracts,
    GmCreationResult GeneralManagerProfile,
    IReadOnlyList<ScoutingAssignment> ScoutingAssignments,
    DateOnly DraftDate,
    IReadOnlyList<AlphaInboxItem> FirstDayInbox,
    string ScenarioSummary)
{
    public DateOnly CurrentDate => AlphaSnapshot.CurrentDate;

    public int DaysUntilDraft => DraftDate.DayNumber - CurrentDate.DayNumber;

    public DraftExperienceState? DraftExperience { get; init; }

    public IReadOnlyList<DraftPickSummary> DraftRights { get; init; } = Array.Empty<DraftPickSummary>();

    public IReadOnlyList<DraftRightsRecord> ProspectRights { get; init; } = Array.Empty<DraftRightsRecord>();

    public TrainingCamp? TrainingCamp { get; init; }

    public IReadOnlyList<PendingGmAction> PendingActions { get; init; } = Array.Empty<PendingGmAction>();

    public IReadOnlyList<ScoutingOperationAssignment> ScoutingOperations { get; init; } = Array.Empty<ScoutingOperationAssignment>();

    public IReadOnlyList<ScoutingReport> CompletedScoutingReports { get; init; } = Array.Empty<ScoutingReport>();

    public IReadOnlyDictionary<string, string> PlayerDossierNotes { get; init; } = new Dictionary<string, string>();

    public IReadOnlyList<StaffCandidate> StaffCandidates { get; init; } = Array.Empty<StaffCandidate>();

    public IReadOnlyList<StaffFocusAssignment> StaffFocusAssignments { get; init; } = Array.Empty<StaffFocusAssignment>();

    public IReadOnlyList<StaffEvaluation> StaffEvaluations { get; init; } = Array.Empty<StaffEvaluation>();

    public SeasonReadinessState SeasonReadiness { get; init; } = new();

    public ExecutiveReportArchive ExecutiveReports { get; init; } = ExecutiveReportArchive.Empty;

    public GameSchedule? Schedule { get; init; }

    public StandingsTable? Standings { get; init; }

    public IReadOnlyList<TeamSeasonStatLine> TeamStats { get; init; } = Array.Empty<TeamSeasonStatLine>();

    public IReadOnlyList<PlayerSeasonStatLine> PlayerStats { get; init; } = Array.Empty<PlayerSeasonStatLine>();

    public IReadOnlyList<GoalieSeasonStatLine> GoalieStats { get; init; } = Array.Empty<GoalieSeasonStatLine>();

    public IReadOnlyList<GameRecap> GameRecaps { get; init; } = Array.Empty<GameRecap>();

    public IReadOnlyList<MonthlyGmSummary> MonthlySummaries { get; init; } = Array.Empty<MonthlyGmSummary>();

    public IReadOnlyList<PriorSeasonStatLine> PriorSeasonStats { get; init; } = Array.Empty<PriorSeasonStatLine>();

    public IReadOnlyList<CareerStatSummary> CareerStatSummaries { get; init; } = Array.Empty<CareerStatSummary>();

    public IReadOnlyList<PlayerTeamHistory> PlayerTeamHistories { get; init; } = Array.Empty<PlayerTeamHistory>();

    public IReadOnlyList<PlayerCareerTimeline> PlayerCareerTimelines { get; init; } = Array.Empty<PlayerCareerTimeline>();

    public OrganizationHistorySnapshot? OrganizationHistory { get; init; }

    public IReadOnlyList<DraftHistoryRecord> DraftHistory { get; init; } = Array.Empty<DraftHistoryRecord>();

    public FreeAgentMarket? FreeAgentMarket { get; init; }

    public TradeBlock? TradeBlock { get; init; }

    public IReadOnlyList<TradeOffer> TradeOffers { get; init; } = Array.Empty<TradeOffer>();

    public void Validate()
    {
        AlphaSnapshot.Validate();
        Organization.Validate();
        Season.Validate();
        GeneralManagerProfile.Validate();

        if (FirstDayInbox.Count == 0)
        {
            throw new ArgumentException("New GM scenario must include first-day inbox items.", nameof(FirstDayInbox));
        }

        if (string.IsNullOrWhiteSpace(ScenarioSummary))
        {
            throw new ArgumentException("Scenario summary is required.", nameof(ScenarioSummary));
        }

        foreach (var staffMember in StaffMembers)
        {
            staffMember.Validate();
        }

        foreach (var contract in Contracts)
        {
            contract.Validate();
        }

        foreach (var assignment in ScoutingAssignments)
        {
            assignment.Validate();
        }

        foreach (var assignment in ScoutingOperations)
        {
            assignment.Validate();
        }

        foreach (var report in CompletedScoutingReports)
        {
            report.Validate();
        }

        foreach (var note in PlayerDossierNotes)
        {
            if (string.IsNullOrWhiteSpace(note.Key))
            {
                throw new ArgumentException("Player dossier note person id is required.", nameof(PlayerDossierNotes));
            }

            if (note.Value is null)
            {
                throw new ArgumentException("Player dossier note cannot be null.", nameof(PlayerDossierNotes));
            }
        }

        foreach (var candidate in StaffCandidates)
        {
            candidate.Validate();
        }

        foreach (var focus in StaffFocusAssignments)
        {
            focus.Validate();
        }

        foreach (var evaluation in StaffEvaluations)
        {
            evaluation.Validate();
        }

        DraftExperience?.Validate();
        foreach (var prospect in ProspectRights)
        {
            prospect.Validate();
        }

        TrainingCamp?.Validate();
        foreach (var pendingAction in PendingActions)
        {
            pendingAction.Validate();
        }

        SeasonReadiness.Validate();
        ExecutiveReports.Validate();
        Schedule?.Validate();
        Standings?.Validate();

        foreach (var stat in TeamStats)
        {
            stat.Validate();
        }

        foreach (var stat in PlayerStats)
        {
            stat.Validate();
        }

        foreach (var stat in GoalieStats)
        {
            stat.Validate();
        }

        foreach (var recap in GameRecaps)
        {
            recap.Validate();
        }

        foreach (var summary in MonthlySummaries)
        {
            summary.Validate();
        }

        foreach (var stat in PriorSeasonStats)
        {
            stat.Validate();
        }

        foreach (var summary in CareerStatSummaries)
        {
            summary.Validate();
        }

        foreach (var history in PlayerTeamHistories)
        {
            history.Validate();
        }

        foreach (var timeline in PlayerCareerTimelines)
        {
            timeline.Validate();
        }

        OrganizationHistory?.Validate();

        foreach (var record in DraftHistory)
        {
            record.Validate();
        }

        FreeAgentMarket?.Validate();
        TradeBlock?.Validate();
        foreach (var offer in TradeOffers)
        {
            offer.Validate();
        }
    }
}
