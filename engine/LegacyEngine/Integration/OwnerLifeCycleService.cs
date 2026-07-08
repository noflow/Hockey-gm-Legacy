using LegacyEngine.Events;
using LegacyEngine.Owners;

namespace LegacyEngine.Integration;

public sealed class OwnerLifeCycleService
{
    public NewGmScenarioSnapshot EnsureLifeCycle(NewGmScenarioSnapshot scenario, EngineRegistry? registry = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var budget = new BudgetOverviewService().Build(scenario, registry?.Rulebook ?? scenario.LeagueProfile.Rulebook);
        var office = new OwnerOfficeService().BuildSummary(scenario, budget);
        var existingMilestones = scenario.OwnerMilestones
            .GroupBy(item => item.MilestoneId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var milestones = existingMilestones.Values.ToList();
        var timeline = scenario.CareerTimeline;
        var news = scenario.OwnerLifeCycleNews.ToList();
        var state = BuildCareerState(scenario, office, budget);
        var expectationHistory = BuildExpectationHistory(scenario, office).ToArray();
        var confidenceHistory = MergeConfidenceHistory(scenario, office, budget).ToArray();
        var meetingHistory = BuildMeetingHistory(scenario, office).ToArray();
        var letters = MergeLetters(scenario, office).ToArray();
        var jobSecurityHistory = MergeJobSecurityHistory(scenario, office).ToArray();

        foreach (var milestone in BuildMilestones(scenario, office, state, budget, letters))
        {
            if (existingMilestones.ContainsKey(milestone.MilestoneId))
            {
                continue;
            }

            milestones.Add(milestone);
            existingMilestones[milestone.MilestoneId] = milestone;
            timeline = timeline.Add(ToTimelineEntry(scenario, milestone));
            QueueMilestoneEvent(registry, scenario, milestone);
            if (milestone.IsNotable)
            {
                var transaction = ToLeagueNews(scenario, milestone);
                if (news.All(item => item.TransactionId != transaction.TransactionId))
                {
                    news.Add(transaction);
                }
            }
        }

        var legacy = BuildLegacyProfile(scenario, office, state, budget);
        var summary = new OwnerCareerSummary(
            scenario.AlphaSnapshot.Owner.OwnerId,
            scenario.AlphaSnapshot.Owner.Name,
            state.LifeStage,
            state.CurrentPersonality,
            state.ConfidenceTrend,
            $"{scenario.AlphaSnapshot.Owner.Name} is a {state.LifeStage} with {state.CurrentPersonality} tendencies and {state.JobSecurity} GM security.",
            expectationHistory,
            confidenceHistory,
            meetingHistory,
            letters,
            jobSecurityHistory,
            milestones.OrderByDescending(item => item.Date).ToArray(),
            legacy,
            BudgetRelationship(office, budget),
            PersonalityEvolution(scenario, office, state),
            OrganizationHistorySummary(scenario, state, office));
        summary.Validate();

        var updated = scenario with
        {
            OwnerCareerState = state,
            OwnerCareerSummary = summary,
            OwnerLegacyProfile = legacy,
            OwnerExpectationHistory = expectationHistory,
            OwnerConfidenceHistory = confidenceHistory,
            OwnerMeetingHistory = meetingHistory,
            OwnerLetters = letters,
            OwnerJobSecurityHistory = jobSecurityHistory,
            OwnerMilestones = milestones
                .OrderByDescending(item => item.Date)
                .ThenBy(item => item.MilestoneType)
                .ToArray(),
            OwnerLifeCycleNews = news
                .OrderByDescending(item => item.Date)
                .ThenBy(item => item.Description, StringComparer.Ordinal)
                .Take(80)
                .ToArray(),
            CareerTimeline = timeline
        };
        updated.Validate();
        return updated;
    }

    public IReadOnlyList<ActionCenterItem> BuildActionItems(NewGmScenarioSnapshot scenario)
    {
        var summary = scenario.OwnerCareerSummary;
        if (summary is null)
        {
            return Array.Empty<ActionCenterItem>();
        }

        var items = new List<ActionCenterItem>();
        var nextMeeting = summary.MeetingHistory
            .Where(item => item.Date >= scenario.CurrentDate && item.Date <= scenario.CurrentDate.AddDays(14))
            .OrderBy(item => item.Date)
            .FirstOrDefault();
        if (nextMeeting is not null)
        {
            items.Add(Item(
                $"action-center:owner-lifecycle:meeting:{nextMeeting.MeetingHistoryId}",
                $"Owner meeting scheduled: {nextMeeting.MeetingType}",
                ActionCenterPriority.Normal,
                nextMeeting.Date,
                nextMeeting.Topic,
                "Owner meetings shape expectations, budget support, and job security history.",
                "Review Owner workspace before the meeting."));
        }

        var latestConfidence = summary.ConfidenceHistory.OrderByDescending(item => item.Date).FirstOrDefault();
        if (latestConfidence is not null && latestConfidence.Trend is OwnerTrend.Falling or OwnerTrend.Critical)
        {
            items.Add(Item(
                $"action-center:owner-lifecycle:confidence:{latestConfidence.ConfidenceHistoryId}",
                "Owner confidence falling",
                latestConfidence.Trend == OwnerTrend.Critical ? ActionCenterPriority.Urgent : ActionCenterPriority.Important,
                scenario.CurrentDate.AddDays(7),
                latestConfidence.Reason,
                "Falling confidence can restrict budget support and create job-security pressure.",
                "Review expectations, budget status, and owner relationship before advancing too far."));
        }

        var budgetReview = summary.MeetingHistory
            .FirstOrDefault(item => item.MeetingType == OwnerMeetingType.BudgetReview && item.Date >= scenario.CurrentDate);
        if (budgetReview is not null)
        {
            items.Add(Item(
                $"action-center:owner-lifecycle:budget:{budgetReview.MeetingHistoryId}",
                "Budget review required",
                ActionCenterPriority.Normal,
                budgetReview.Date,
                summary.BudgetRelationship,
                "Budget support changes with trust, results, and spending discipline.",
                "Review budget before hiring or signing expensive personnel."));
        }

        var warningLetter = summary.Letters
            .Where(item => item.Date >= scenario.CurrentDate.AddDays(-14))
            .OrderByDescending(item => item.Date)
            .FirstOrDefault(item => item.IsWarning);
        if (warningLetter is not null)
        {
            items.Add(Item(
                $"action-center:owner-lifecycle:letter:{warningLetter.LetterId}",
                $"Owner letter received: {warningLetter.Subject}",
                ActionCenterPriority.Important,
                scenario.CurrentDate.AddDays(7),
                warningLetter.Body,
                "Owner letters are permanent history and may signal pressure or a mandate change.",
                "Open Owner Letters and address the concern."));
        }

        var jobSecurity = summary.JobSecurityHistory.OrderByDescending(item => item.Date).FirstOrDefault();
        if (jobSecurity is not null && jobSecurity.Level is JobSecurityLevel.HotSeat or JobSecurityLevel.Critical or JobSecurityLevel.Questioned)
        {
            items.Add(Item(
                $"action-center:owner-lifecycle:job-security:{jobSecurity.JobSecurityHistoryId}",
                $"Job security warning: {jobSecurity.Level}",
                jobSecurity.Level == JobSecurityLevel.Critical ? ActionCenterPriority.Urgent : ActionCenterPriority.Important,
                scenario.CurrentDate.AddDays(7),
                jobSecurity.Reason,
                "Job-security history tracks warnings, pressure periods, and recovery periods.",
                "Review owner expectations and prioritize the highest-risk mandate."));
        }

        return items
            .GroupBy(item => item.ActionCenterItemId, StringComparer.Ordinal)
            .Select(group => group.First())
            .Take(6)
            .ToArray();

        ActionCenterItem Item(string id, string title, ActionCenterPriority priority, DateOnly? dueDate, string reason, string consequence, string action) =>
            new(
                id,
                title,
                ActionCenterCategory.Owner,
                priority,
                dueDate,
                scenario.AlphaSnapshot.Owner.OwnerId,
                scenario.AlphaSnapshot.Owner.Name,
                scenario.Organization.OrganizationId,
                scenario.Organization.Name,
                reason,
                consequence,
                action,
                null,
                null,
                null);
    }

    public IReadOnlyList<string> BuildReportHighlights(NewGmScenarioSnapshot scenario)
    {
        var summary = scenario.OwnerCareerSummary;
        if (summary is null)
        {
            return new[] { "Owner life-cycle history has not been generated yet." };
        }

        var latestConfidence = summary.ConfidenceHistory.OrderByDescending(item => item.Date).FirstOrDefault();
        var topExpectation = summary.ExpectationHistory.OrderByDescending(item => item.Priority).ThenByDescending(item => item.Difficulty).FirstOrDefault();
        return new[]
        {
            $"Owner life stage: {summary.LifeStage}.",
            $"Owner personality: {summary.CurrentPersonality}. {summary.PersonalityEvolution}",
            $"Confidence trend: {summary.ConfidenceTrend}. {latestConfidence?.Reason ?? "No confidence history yet."}",
            $"Top expectation: {topExpectation?.ExpectationType.ToString() ?? "none"} - {topExpectation?.Result.ToString() ?? "not tracked"}.",
            $"Budget relationship: {summary.BudgetRelationship}",
            $"Legacy: {summary.LegacyProfile.LegacySummary}"
        };
    }

    private static OwnerCareerState BuildCareerState(NewGmScenarioSnapshot scenario, OwnerOfficeSummary office, BudgetSnapshot budget)
    {
        var owner = scenario.AlphaSnapshot.Owner;
        var tenure = OwnerTenureYears(scenario);
        var trend = ConfidenceTrend(office.Confidence, office.JobSecurity, budget);
        var stage = DetermineLifeStage(owner, office, tenure, trend);
        var state = new OwnerCareerState(
            owner.OwnerId,
            owner.Name,
            stage,
            office.Personality.PersonalityType,
            trend,
            tenure,
            office.Confidence.Trust,
            office.Confidence.Confidence,
            office.Confidence.Patience,
            office.Confidence.Pressure,
            office.JobSecurity.Level,
            $"{owner.Name} has owned {scenario.Organization.Name} for {tenure} year(s), with {trend} confidence and {office.JobSecurity.Level} GM security.");
        state.Validate();
        return state;
    }

    private static OwnerLifeStage DetermineLifeStage(Owner owner, OwnerOfficeSummary office, int tenure, OwnerTrend trend)
    {
        if (tenure <= 2)
        {
            return OwnerLifeStage.NewOwner;
        }

        if (trend == OwnerTrend.Critical || office.Confidence.Pressure >= 75)
        {
            return OwnerLifeStage.PressureOwner;
        }

        if (tenure >= 14 && office.Confidence.Support < 45)
        {
            return OwnerLifeStage.TransitionPlanning;
        }

        if (office.Confidence.Patience < 35 && office.Confidence.Support < 45)
        {
            return OwnerLifeStage.DecliningInterest;
        }

        if (office.Personality.PersonalityType is OwnerPersonalityType.ChampionshipOrBust or OwnerPersonalityType.BigSpender && office.Confidence.Confidence >= 70)
        {
            return OwnerLifeStage.ChampionshipOwner;
        }

        if (owner.Archetype == OwnerArchetype.Builder || office.Confidence.Patience >= 70)
        {
            return OwnerLifeStage.PatientBuilder;
        }

        return OwnerLifeStage.EstablishedOwner;
    }

    private static IEnumerable<OwnerExpectationHistoryRecord> BuildExpectationHistory(NewGmScenarioSnapshot scenario, OwnerOfficeSummary office)
    {
        foreach (var expectation in office.Expectations)
        {
            var result = expectation.CurrentProgress switch
            {
                >= 85 => OwnerExpectationResult.Met,
                >= 60 => OwnerExpectationResult.OnTrack,
                >= 35 => OwnerExpectationResult.Mixed,
                <= 5 => OwnerExpectationResult.NotStarted,
                _ => OwnerExpectationResult.Missed
            };
            var reaction = result switch
            {
                OwnerExpectationResult.Met => "Owner is pleased with progress.",
                OwnerExpectationResult.OnTrack => "Owner sees the mandate moving in the right direction.",
                OwnerExpectationResult.Mixed => "Owner wants clearer evidence before increasing support.",
                OwnerExpectationResult.NotStarted => "Owner is waiting for action.",
                _ => "Owner is concerned about the current direction."
            };
            var record = new OwnerExpectationHistoryRecord(
                $"owner-expectation-history:{scenario.Season.Year}:{expectation.ExpectationType}",
                scenario.Season.Year,
                expectation.ExpectationType,
                expectation.Difficulty,
                expectation.Priority,
                result,
                expectation.CurrentProgress,
                reaction,
                $"GM progress is {expectation.CurrentProgress}/100 against '{expectation.Description}'.");
            record.Validate();
            yield return record;
        }
    }

    private static IEnumerable<OwnerConfidenceHistoryRecord> MergeConfidenceHistory(NewGmScenarioSnapshot scenario, OwnerOfficeSummary office, BudgetSnapshot budget)
    {
        foreach (var existing in scenario.OwnerConfidenceHistory)
        {
            yield return existing;
        }

        var budgetSupport = budget.Status switch
        {
            BudgetStatus.UnderBudget => 72,
            BudgetStatus.NearLimit => 48,
            BudgetStatus.OverBudget => 28,
            _ => 58
        };
        var record = new OwnerConfidenceHistoryRecord(
            $"owner-confidence-history:{scenario.CurrentDate:yyyyMMdd}",
            scenario.CurrentDate,
            office.Confidence.Confidence,
            office.Confidence.Trust,
            office.Confidence.Patience,
            office.Confidence.Pressure,
            budgetSupport,
            office.JobSecurity.Level,
            ConfidenceTrend(office.Confidence, office.JobSecurity, budget),
            string.Join(" ", office.Confidence.Drivers.Take(3)));
        record.Validate();
        if (scenario.OwnerConfidenceHistory.All(item => item.ConfidenceHistoryId != record.ConfidenceHistoryId))
        {
            yield return record;
        }
    }

    private static IEnumerable<OwnerMeetingHistoryRecord> BuildMeetingHistory(NewGmScenarioSnapshot scenario, OwnerOfficeSummary office)
    {
        foreach (var existing in scenario.OwnerMeetingHistory)
        {
            yield return existing;
        }

        foreach (var meeting in office.Meetings)
        {
            var record = new OwnerMeetingHistoryRecord(
                $"owner-meeting-history:{meeting.MeetingId}",
                meeting.MeetingType,
                meeting.ScheduledDate,
                meeting.MeetingType.ToString(),
                meeting.OwnerComments,
                meeting.GmResponseOptions.FirstOrDefault() ?? "GM response placeholder.",
                meeting.Summary,
                meeting.MeetingType == OwnerMeetingType.BudgetReview ? -2 : meeting.MeetingType == OwnerMeetingType.EndOfSeason ? 4 : 1);
            record.Validate();
            if (scenario.OwnerMeetingHistory.All(item => item.MeetingHistoryId != record.MeetingHistoryId))
            {
                yield return record;
            }
        }
    }

    private static IEnumerable<OwnerLetter> MergeLetters(NewGmScenarioSnapshot scenario, OwnerOfficeSummary office)
    {
        var seen = new HashSet<string>(scenario.OwnerLetters.Select(item => item.LetterId), StringComparer.Ordinal);
        foreach (var letter in scenario.OwnerLetters)
        {
            yield return letter;
        }

        foreach (var letter in office.Letters)
        {
            if (seen.Add(letter.LetterId))
            {
                yield return letter;
            }
        }
    }

    private static IEnumerable<OwnerJobSecurityHistoryRecord> MergeJobSecurityHistory(NewGmScenarioSnapshot scenario, OwnerOfficeSummary office)
    {
        foreach (var existing in scenario.OwnerJobSecurityHistory)
        {
            yield return existing;
        }

        var record = new OwnerJobSecurityHistoryRecord(
            $"owner-job-security-history:{scenario.CurrentDate:yyyyMMdd}",
            scenario.CurrentDate,
            office.JobSecurity.Level,
            office.JobSecurity.Score,
            office.JobSecurity.Level is JobSecurityLevel.Critical or JobSecurityLevel.HotSeat ? OwnerTrend.Critical : office.JobSecurity.Level == JobSecurityLevel.Questioned ? OwnerTrend.Falling : OwnerTrend.Stable,
            office.JobSecurity.Explanation);
        record.Validate();
        if (scenario.OwnerJobSecurityHistory.All(item => item.JobSecurityHistoryId != record.JobSecurityHistoryId))
        {
            yield return record;
        }
    }

    private static IEnumerable<OwnerMilestone> BuildMilestones(NewGmScenarioSnapshot scenario, OwnerOfficeSummary office, OwnerCareerState state, BudgetSnapshot budget, IReadOnlyList<OwnerLetter> letters)
    {
        var owner = scenario.AlphaSnapshot.Owner;
        var startDate = scenario.CurrentDate.AddYears(-Math.Max(1, state.TenureYears));
        yield return Milestone(scenario, OwnerMilestoneType.OwnershipStarted, startDate, $"{owner.Name} began the current ownership era for {scenario.Organization.Name}.", true);
        yield return Milestone(scenario, OwnerMilestoneType.GmHired, scenario.CurrentDate, $"{owner.Name} hired {scenario.GeneralManagerProfile.Person.Identity.DisplayName} as general manager.", true);
        yield return Milestone(scenario, OwnerMilestoneType.ExpectationsSet, scenario.CurrentDate, $"{owner.Name} set {office.Expectations.Count} expectation(s) for the season.", false);

        if (budget.Status is BudgetStatus.NearLimit or BudgetStatus.OverBudget)
        {
            yield return Milestone(scenario, OwnerMilestoneType.BudgetReviewed, scenario.CurrentDate, $"{owner.Name} flagged budget pressure: {budget.OwnerBudgetConfidence}", true);
        }

        if (state.ConfidenceTrend is OwnerTrend.Falling or OwnerTrend.Critical)
        {
            yield return Milestone(scenario, OwnerMilestoneType.ConfidenceChanged, scenario.CurrentDate, $"{owner.Name}'s confidence trend moved to {state.ConfidenceTrend}.", true);
        }

        if (office.JobSecurity.Level is JobSecurityLevel.Questioned or JobSecurityLevel.HotSeat or JobSecurityLevel.Critical)
        {
            yield return Milestone(scenario, OwnerMilestoneType.JobSecurityChanged, scenario.CurrentDate, $"GM job security moved to {office.JobSecurity.Level}: {office.JobSecurity.Explanation}", true);
        }

        foreach (var letter in letters.Where(item => item.IsWarning).Take(2))
        {
            yield return Milestone(scenario, OwnerMilestoneType.OwnerLetterSent, letter.Date, $"{owner.Name} sent a warning letter: {letter.Subject}.", true);
        }

        if (office.Personality.PersonalityType is OwnerPersonalityType.PatientBuilder or OwnerPersonalityType.ProspectLover)
        {
            yield return Milestone(scenario, OwnerMilestoneType.RebuildApproved, scenario.CurrentDate, $"{owner.Name} continues to support a development-first mandate.", false);
        }
    }

    private static OwnerLegacyProfile BuildLegacyProfile(NewGmScenarioSnapshot scenario, OwnerOfficeSummary office, OwnerCareerState state, BudgetSnapshot budget)
    {
        var legacy = new OwnerLegacyProfile(
            state.OwnerId,
            state.OwnerName,
            state.TenureYears,
            office.Personality.PersonalityType is OwnerPersonalityType.PatientBuilder or OwnerPersonalityType.ProspectLover ? "Development era" : "Results-pressure era",
            budget.Status == BudgetStatus.UnderBudget ? "Disciplined budget era" : budget.Status == BudgetStatus.OverBudget ? "Budget pressure era" : "Balanced spending era",
            office.Confidence.Support >= 65 ? "Supportive GM relationship era" : office.Confidence.Support < 45 ? "Strained GM relationship era" : "Evaluation era",
            scenario.OrganizationAiProfiles.FirstOrDefault(profile => profile.OrganizationId == scenario.Organization.OrganizationId)?.Strategy.Phase.ToString() ?? "Competitive identity forming",
            $"{state.OwnerName}'s legacy is defined by {office.Personality.Vision}");
        legacy.Validate();
        return legacy;
    }

    private static string BudgetRelationship(OwnerOfficeSummary office, BudgetSnapshot budget) =>
        budget.Status switch
        {
            BudgetStatus.UnderBudget when office.Confidence.Trust >= 60 => "Owner is open to selective hockey-operations spending because trust and budget discipline are healthy.",
            BudgetStatus.OverBudget => "Owner is warning about overspending and may restrict hiring until costs are explained.",
            BudgetStatus.NearLimit => "Owner expects careful budget review before new commitments.",
            _ => "Owner budget support is balanced and tied to expectation progress."
        };

    private static string PersonalityEvolution(NewGmScenarioSnapshot scenario, OwnerOfficeSummary office, OwnerCareerState state)
    {
        if (state.ConfidenceTrend == OwnerTrend.Critical)
        {
            return "Pressure is pushing ownership toward shorter patience and tighter oversight.";
        }

        if (office.Confidence.Trust >= 70 && scenario.ProspectRights.Count >= 4)
        {
            return "Trust and prospect progress are reinforcing a patient development personality.";
        }

        if (office.Confidence.Pressure >= 65)
        {
            return "Fan and budget pressure placeholders are making ownership more demanding.";
        }

        return "Owner personality is stable, with slow evolution tied to results, trust, and budget behavior.";
    }

    private static string OrganizationHistorySummary(NewGmScenarioSnapshot scenario, OwnerCareerState state, OwnerOfficeSummary office) =>
        $"{state.OwnerName}'s tenure includes {scenario.OrganizationSeasonHistory.Count} archived season record(s), a {office.Personality.BudgetPhilosophy} budget philosophy, and a {office.Personality.RelationshipStyle} GM relationship style.";

    private static OwnerTrend ConfidenceTrend(OwnerConfidenceState confidence, JobSecurityAssessment jobSecurity, BudgetSnapshot budget)
    {
        if (jobSecurity.Level == JobSecurityLevel.Critical || confidence.Confidence < 30 || confidence.Support < 30)
        {
            return OwnerTrend.Critical;
        }

        if (confidence.Confidence < 45 || confidence.Pressure > 70 || budget.Status == BudgetStatus.OverBudget)
        {
            return OwnerTrend.Falling;
        }

        if (confidence.Confidence >= 65 && confidence.Trust >= 60)
        {
            return OwnerTrend.Rising;
        }

        return OwnerTrend.Stable;
    }

    private static int OwnerTenureYears(NewGmScenarioSnapshot scenario)
    {
        var ownerPerson = scenario.AlphaSnapshot.People.FirstOrDefault(person => person.Identity.DisplayName == scenario.AlphaSnapshot.Owner.Name);
        var start = ownerPerson?.Roles.OrderBy(role => role.StartDate).FirstOrDefault()?.StartDate
            ?? scenario.CurrentDate.AddYears(6);
        return Math.Max(0, scenario.CurrentDate.Year - start.Year - (scenario.CurrentDate < start.AddYears(scenario.CurrentDate.Year - start.Year) ? 1 : 0));
    }

    private static OwnerMilestone Milestone(NewGmScenarioSnapshot scenario, OwnerMilestoneType type, DateOnly date, string summary, bool notable)
    {
        var milestone = new OwnerMilestone(
            $"owner-milestone:{scenario.AlphaSnapshot.Owner.OwnerId}:{type}:{date:yyyyMMdd}",
            scenario.AlphaSnapshot.Owner.OwnerId,
            scenario.AlphaSnapshot.Owner.Name,
            type,
            date,
            scenario.Season.Year,
            summary,
            notable);
        milestone.Validate();
        return milestone;
    }

    private static CareerTimelineEntry ToTimelineEntry(NewGmScenarioSnapshot scenario, OwnerMilestone milestone) =>
        new(
            $"career:owner-lifecycle:{milestone.MilestoneId}",
            TimelineTypeFor(milestone.MilestoneType),
            milestone.Date,
            milestone.SeasonYear,
            scenario.AlphaSnapshot.GeneralManager.PersonId,
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            milestone.MilestoneType.ToString(),
            milestone.Summary,
            null,
            milestone.IsNotable ? HistoryImportance.Important : HistoryImportance.Normal);

    private static CareerTimelineEntryType TimelineTypeFor(OwnerMilestoneType type) =>
        type switch
        {
            OwnerMilestoneType.GmHired => CareerTimelineEntryType.GMHired,
            OwnerMilestoneType.OwnerLetterSent => CareerTimelineEntryType.OwnerLetter,
            OwnerMilestoneType.OwnerMeetingHeld or OwnerMilestoneType.BudgetReviewed => CareerTimelineEntryType.OwnerMeeting,
            OwnerMilestoneType.JobSecurityChanged => CareerTimelineEntryType.OwnerPerformanceReview,
            OwnerMilestoneType.PhilosophyChanged => CareerTimelineEntryType.OrganizationIdentityChanged,
            _ => CareerTimelineEntryType.OwnerMeeting
        };

    private static LeagueTransaction ToLeagueNews(NewGmScenarioSnapshot scenario, OwnerMilestone milestone)
    {
        var transaction = new LeagueTransaction(
            $"transaction:owner-lifecycle:{milestone.MilestoneId}",
            new DateTimeOffset(milestone.Date.Year, milestone.Date.Month, milestone.Date.Day, 12, 45, 0, TimeSpan.Zero),
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            milestone.OwnerId,
            milestone.OwnerName,
            LeagueTransactionType.OwnerMilestone,
            LeagueNewsCategory.League,
            milestone.Summary);
        transaction.Validate();
        return transaction;
    }

    private static void QueueMilestoneEvent(EngineRegistry? registry, NewGmScenarioSnapshot scenario, OwnerMilestone milestone)
    {
        if (registry is null)
        {
            return;
        }

        var legacyEvent = registry.EventEngine.CreateEvent(
            new DateTimeOffset(milestone.Date.Year, milestone.Date.Month, milestone.Date.Day, 13, 0, 0, TimeSpan.Zero),
            LegacyEventType.MilestoneReached,
            milestone.IsNotable ? LegacyEventSeverity.Notice : LegacyEventSeverity.Info,
            LegacyEventVisibility.Organization,
            $"Owner milestone: {milestone.OwnerName}",
            milestone.Summary,
            new LegacyEventContext(PrimaryPersonId: milestone.OwnerId, OrganizationId: scenario.Organization.OrganizationId, SeasonId: scenario.Season.SeasonId),
            new Dictionary<string, object?>
            {
                ["owner_lifecycle"] = true,
                ["milestone_type"] = milestone.MilestoneType.ToString(),
                ["owner_name"] = milestone.OwnerName,
                ["team_name"] = scenario.Organization.Name
            });
        registry.EventEngine.QueueEvent(legacyEvent);
    }
}
