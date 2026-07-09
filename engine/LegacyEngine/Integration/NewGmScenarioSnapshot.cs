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
    LeagueProfile LeagueProfile,
    TeamSelectionOption TeamSelection,
    DateOnly DraftDate,
    IReadOnlyList<AlphaInboxItem> FirstDayInbox,
    string ScenarioSummary)
{
    public DateOnly CurrentDate => AlphaSnapshot.CurrentDate;

    public int DaysUntilDraft => DraftDate.DayNumber - CurrentDate.DayNumber;

    public DraftExperienceState? DraftExperience { get; init; }

    public DraftClassProfile? CurrentDraftClassProfile { get; init; }

    public DraftWarRoomState DraftWarRoom { get; init; } = DraftWarRoomState.Empty;

    public IReadOnlyList<DraftPickSummary> DraftRights { get; init; } = Array.Empty<DraftPickSummary>();

    public IReadOnlyList<DraftRightsRecord> ProspectRights { get; init; } = Array.Empty<DraftRightsRecord>();

    public IReadOnlyList<AffiliateLink> AffiliateLinks { get; init; } = Array.Empty<AffiliateLink>();

    public IReadOnlyList<PlayerPipelineRecord> PlayerPipeline { get; init; } = Array.Empty<PlayerPipelineRecord>();

    public TrainingCamp? TrainingCamp { get; init; }

    public IReadOnlyList<PendingGmAction> PendingActions { get; init; } = Array.Empty<PendingGmAction>();

    public IReadOnlyList<ScoutingOperationAssignment> ScoutingOperations { get; init; } = Array.Empty<ScoutingOperationAssignment>();

    public IReadOnlyList<ScoutingReport> CompletedScoutingReports { get; init; } = Array.Empty<ScoutingReport>();

    public IReadOnlyDictionary<string, string> PlayerDossierNotes { get; init; } = new Dictionary<string, string>();

    public IReadOnlyList<StaffCandidate> StaffCandidates { get; init; } = Array.Empty<StaffCandidate>();

    public StaffMarket? StaffMarket { get; init; }

    public IReadOnlyList<StaffMovementRecord> StaffMovementHistory { get; init; } = Array.Empty<StaffMovementRecord>();

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

    public FreeAgencyMarketState? FreeAgencyMarketState { get; init; }

    public TradeBlock? TradeBlock { get; init; }

    public IReadOnlyList<TradeOffer> TradeOffers { get; init; } = Array.Empty<TradeOffer>();

    public TradeDeadlineState? TradeDeadlineState { get; init; }

    public CareerTimeline CareerTimeline { get; init; } = CareerTimeline.Empty;

    public IReadOnlyList<DraftPickHistory> DraftPickHistory { get; init; } = Array.Empty<DraftPickHistory>();

    public IReadOnlyList<DraftClassHistory> DraftClassHistory { get; init; } = Array.Empty<DraftClassHistory>();

    public IReadOnlyList<StaffCareerHistory> StaffCareerHistory { get; init; } = Array.Empty<StaffCareerHistory>();

    public GmCareerHistory? GmCareerHistory { get; init; }

    public IReadOnlyList<OrganizationSeasonHistory> OrganizationSeasonHistory { get; init; } = Array.Empty<OrganizationSeasonHistory>();

    public IReadOnlyList<TransactionHistoryRecord> TransactionHistory { get; init; } = Array.Empty<TransactionHistoryRecord>();

    public WaiverWire WaiverWire { get; init; } = WaiverWire.Empty;

    public WaiverHistory WaiverHistory { get; init; } = WaiverHistory.Empty;

    public IReadOnlyList<PlayerRightsDecision> PlayerRightsDecisions { get; init; } = Array.Empty<PlayerRightsDecision>();

    public RightsHistory RightsHistory { get; init; } = RightsHistory.Empty;

    public IReadOnlyList<ArbitrationCase> ArbitrationCases { get; init; } = Array.Empty<ArbitrationCase>();

    public ArbitrationHistory ArbitrationHistory { get; init; } = ArbitrationHistory.Empty;

    public IReadOnlyList<ContractBuyout> ContractBuyouts { get; init; } = Array.Empty<ContractBuyout>();

    public BuyoutHistory BuyoutHistory { get; init; } = BuyoutHistory.Empty;

    public IReadOnlyList<OfferSheet> OfferSheets { get; init; } = Array.Empty<OfferSheet>();

    public OfferSheetHistory OfferSheetHistory { get; init; } = OfferSheetHistory.Empty;

    public SeasonRolloverState SeasonRollover { get; init; } = new();

    public PlayoffState Playoffs { get; init; } = PlayoffState.Empty;

    public IReadOnlyList<PlayerDevelopmentPlan> DevelopmentPlans { get; init; } = Array.Empty<PlayerDevelopmentPlan>();

    public IReadOnlyList<DevelopmentReview> DevelopmentReviews { get; init; } = Array.Empty<DevelopmentReview>();

    public IReadOnlyList<DevelopmentRecommendation> DevelopmentRecommendations { get; init; } = Array.Empty<DevelopmentRecommendation>();

    public IReadOnlyList<Agent> Agents { get; init; } = Array.Empty<Agent>();

    public IReadOnlyList<AgentRepresentationRecord> AgentRepresentations { get; init; } = Array.Empty<AgentRepresentationRecord>();

    public IReadOnlyList<AgentHistoryRecord> AgentHistory { get; init; } = Array.Empty<AgentHistoryRecord>();

    public IReadOnlyList<OrganizationAiProfile> OrganizationAiProfiles { get; init; } = Array.Empty<OrganizationAiProfile>();

    public IReadOnlyList<FranchiseIdentity> FranchiseIdentities { get; init; } = Array.Empty<FranchiseIdentity>();

    public IReadOnlyList<Story> Stories { get; init; } = Array.Empty<Story>();

    public MediaFeed MediaFeed { get; init; } = MediaFeed.Empty;

    public AwardHistory AwardHistory { get; init; } = AwardHistory.Empty;

    public RecordBook RecordBook { get; init; } = RecordBook.Empty;

    public IReadOnlyList<PlayerTrueRatings> TrueRatings { get; init; } = Array.Empty<PlayerTrueRatings>();

    public IReadOnlyList<PlayerScoutedRatings> ScoutedRatings { get; init; } = Array.Empty<PlayerScoutedRatings>();

    public IReadOnlyList<PlayerRatingSnapshot> PlayerRatings { get; init; } = Array.Empty<PlayerRatingSnapshot>();

    public PlayerRatingHistory PlayerRatingHistory { get; init; } = PlayerRatingHistory.Empty;

    public IReadOnlyList<PlayerDevelopmentCurve> DevelopmentCurves { get; init; } = Array.Empty<PlayerDevelopmentCurve>();

    public IReadOnlyList<AttributeDevelopmentSnapshot> AttributeDevelopmentSnapshots { get; init; } = Array.Empty<AttributeDevelopmentSnapshot>();

    public IReadOnlyList<PlayerCareerState> PlayerCareerStates { get; init; } = Array.Empty<PlayerCareerState>();

    public IReadOnlyList<PlayerCareerSummary> PlayerCareerSummaries { get; init; } = Array.Empty<PlayerCareerSummary>();

    public IReadOnlyList<PlayerMilestone> PlayerMilestones { get; init; } = Array.Empty<PlayerMilestone>();

    public IReadOnlyList<PlayerAchievement> PlayerAchievements { get; init; } = Array.Empty<PlayerAchievement>();

    public IReadOnlyList<LeagueTransaction> PlayerLifeCycleNews { get; init; } = Array.Empty<LeagueTransaction>();

    public IReadOnlyList<StaffCareerState> StaffCareerStates { get; init; } = Array.Empty<StaffCareerState>();

    public IReadOnlyList<StaffCareerSummary> StaffCareerSummaries { get; init; } = Array.Empty<StaffCareerSummary>();

    public IReadOnlyList<StaffMilestone> StaffMilestones { get; init; } = Array.Empty<StaffMilestone>();

    public IReadOnlyList<LeagueTransaction> StaffLifeCycleNews { get; init; } = Array.Empty<LeagueTransaction>();

    public OwnerCareerState? OwnerCareerState { get; init; }

    public OwnerCareerSummary? OwnerCareerSummary { get; init; }

    public OwnerLegacyProfile? OwnerLegacyProfile { get; init; }

    public IReadOnlyList<OwnerExpectationHistoryRecord> OwnerExpectationHistory { get; init; } = Array.Empty<OwnerExpectationHistoryRecord>();

    public IReadOnlyList<OwnerConfidenceHistoryRecord> OwnerConfidenceHistory { get; init; } = Array.Empty<OwnerConfidenceHistoryRecord>();

    public IReadOnlyList<OwnerMeetingHistoryRecord> OwnerMeetingHistory { get; init; } = Array.Empty<OwnerMeetingHistoryRecord>();

    public IReadOnlyList<OwnerLetter> OwnerLetters { get; init; } = Array.Empty<OwnerLetter>();

    public IReadOnlyList<OwnerJobSecurityHistoryRecord> OwnerJobSecurityHistory { get; init; } = Array.Empty<OwnerJobSecurityHistoryRecord>();

    public IReadOnlyList<OwnerMilestone> OwnerMilestones { get; init; } = Array.Empty<OwnerMilestone>();

    public IReadOnlyList<LeagueTransaction> OwnerLifeCycleNews { get; init; } = Array.Empty<LeagueTransaction>();

    public IReadOnlyList<ExpandedRelationshipProfile> RelationshipProfiles { get; init; } = Array.Empty<ExpandedRelationshipProfile>();

    public IReadOnlyList<RelationshipChangeRecord> RelationshipChangeHistory { get; init; } = Array.Empty<RelationshipChangeRecord>();

    public IReadOnlyList<RelationshipConflict> RelationshipConflicts { get; init; } = Array.Empty<RelationshipConflict>();

    public RelationshipChemistrySummary? RelationshipChemistry { get; init; }

    public Lineup? CurrentLineup { get; init; }

    public LineChemistryReport? CurrentLineChemistry { get; init; }

    public GameUsage? CurrentGameUsage { get; init; }

    public TeamTactics? CurrentTactics { get; init; }

    public void Validate()
    {
        AlphaSnapshot.Validate();
        Organization.Validate();
        Season.Validate();
        GeneralManagerProfile.Validate();
        LeagueProfile.Validate();
        TeamSelection.Validate();

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

        StaffMarket?.Validate();
        foreach (var movement in StaffMovementHistory)
        {
            movement.Validate();
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
        CurrentDraftClassProfile?.Validate();
        DraftWarRoom.Validate();
        foreach (var prospect in ProspectRights)
        {
            prospect.Validate();
        }

        foreach (var link in AffiliateLinks)
        {
            link.Validate();
        }

        foreach (var pipelineRecord in PlayerPipeline)
        {
            pipelineRecord.Validate();
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
        FreeAgencyMarketState?.Validate();
        TradeBlock?.Validate();
        foreach (var offer in TradeOffers)
        {
            offer.Validate();
        }

        TradeDeadlineState?.Validate();
        CareerTimeline.Validate();
        foreach (var pick in DraftPickHistory)
        {
            pick.Validate();
        }

        foreach (var draftClass in DraftClassHistory)
        {
            draftClass.Validate();
        }

        foreach (var staff in StaffCareerHistory)
        {
            staff.Validate();
        }

        foreach (var arbitrationCase in ArbitrationCases)
        {
            arbitrationCase.Validate();
        }

        ArbitrationHistory.Validate();
        foreach (var buyout in ContractBuyouts)
        {
            buyout.Validate();
        }

        BuyoutHistory.Validate();
        foreach (var offerSheet in OfferSheets)
        {
            offerSheet.Validate();
        }

        OfferSheetHistory.Validate();

        GmCareerHistory?.Validate();
        foreach (var season in OrganizationSeasonHistory)
        {
            season.Validate();
        }

        foreach (var transaction in TransactionHistory)
        {
            transaction.Validate();
        }

        WaiverWire.Validate();
        WaiverHistory.Validate();
        foreach (var decision in PlayerRightsDecisions)
        {
            decision.Validate();
        }

        RightsHistory.Validate();

        SeasonRollover.Validate();
        Playoffs.Validate();
        foreach (var plan in DevelopmentPlans)
        {
            plan.Validate();
        }

        foreach (var review in DevelopmentReviews)
        {
            review.Validate();
        }

        foreach (var recommendation in DevelopmentRecommendations)
        {
            recommendation.Validate();
        }

        foreach (var agent in Agents)
        {
            agent.Validate();
        }

        foreach (var representation in AgentRepresentations)
        {
            representation.Validate();
        }

        foreach (var history in AgentHistory)
        {
            history.Validate();
        }

        foreach (var profile in OrganizationAiProfiles)
        {
            profile.Validate();
        }

        foreach (var identity in FranchiseIdentities)
        {
            identity.Validate();
        }

        foreach (var story in Stories)
        {
            story.Validate();
        }

        if (MediaFeed.Sources.Count > 0 || MediaFeed.Articles.Count > 0)
        {
            MediaFeed.Validate();
        }

        AwardHistory.Validate();
        RecordBook.Validate();
        foreach (var rating in TrueRatings)
        {
            rating.Validate();
        }

        foreach (var rating in ScoutedRatings)
        {
            rating.Validate();
        }

        foreach (var rating in PlayerRatings)
        {
            rating.Validate();
        }

        PlayerRatingHistory.Validate();

        foreach (var curve in DevelopmentCurves)
        {
            curve.Validate();
        }

        foreach (var snapshot in AttributeDevelopmentSnapshots)
        {
            snapshot.Validate();
        }

        foreach (var state in PlayerCareerStates)
        {
            state.Validate();
        }

        foreach (var summary in PlayerCareerSummaries)
        {
            summary.Validate();
        }

        foreach (var milestone in PlayerMilestones)
        {
            milestone.Validate();
        }

        foreach (var achievement in PlayerAchievements)
        {
            achievement.Validate();
        }

        foreach (var transaction in PlayerLifeCycleNews)
        {
            transaction.Validate();
        }

        foreach (var state in StaffCareerStates)
        {
            state.Validate();
        }

        foreach (var summary in StaffCareerSummaries)
        {
            summary.Validate();
        }

        foreach (var milestone in StaffMilestones)
        {
            milestone.Validate();
        }

        foreach (var transaction in StaffLifeCycleNews)
        {
            transaction.Validate();
        }

        OwnerCareerState?.Validate();
        OwnerCareerSummary?.Validate();
        OwnerLegacyProfile?.Validate();

        foreach (var item in OwnerExpectationHistory)
        {
            item.Validate();
        }

        foreach (var item in OwnerConfidenceHistory)
        {
            item.Validate();
        }

        foreach (var item in OwnerMeetingHistory)
        {
            item.Validate();
        }

        foreach (var letter in OwnerLetters)
        {
            letter.Validate();
        }

        foreach (var item in OwnerJobSecurityHistory)
        {
            item.Validate();
        }

        foreach (var milestone in OwnerMilestones)
        {
            milestone.Validate();
        }

        foreach (var transaction in OwnerLifeCycleNews)
        {
            transaction.Validate();
        }

        foreach (var profile in RelationshipProfiles)
        {
            profile.Validate();
        }

        foreach (var change in RelationshipChangeHistory)
        {
            change.Validate();
        }

        foreach (var conflict in RelationshipConflicts)
        {
            conflict.Validate();
        }

        RelationshipChemistry?.Validate();
        CurrentLineup?.Validate();
        CurrentLineChemistry?.Validate();
        CurrentGameUsage?.Validate();
        CurrentTactics?.Validate();
    }
}
