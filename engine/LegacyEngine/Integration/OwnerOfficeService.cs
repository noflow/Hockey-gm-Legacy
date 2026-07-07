using LegacyEngine.Events;
using LegacyEngine.Owners;
using LegacyEngine.Relationships;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;
using LegacyEngine.Seasons;

namespace LegacyEngine.Integration;

public sealed class OwnerOfficeService
{
    public OwnerOfficeSummary BuildSummary(NewGmScenarioSnapshot scenario, BudgetSnapshot? budget = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        budget ??= new BudgetOverviewService().Build(scenario, RulebookPresets.CreateJuniorMajor());
        var personality = BuildPersonality(scenario);
        var expectations = BuildSeasonExpectations(scenario, personality, budget);
        var confidence = BuildConfidenceState(scenario, budget);
        var jobSecurity = BuildJobSecurity(scenario, confidence, expectations, budget);
        var meetings = BuildMeetings(scenario, expectations, jobSecurity, budget);
        var letters = BuildLetters(scenario, jobSecurity, expectations, budget);
        var decisions = BuildOwnerDecisions(scenario, personality, confidence, budget);
        var review = BuildPerformanceReview(scenario, confidence, jobSecurity, budget);

        var summary = new OwnerOfficeSummary(personality, expectations, confidence, jobSecurity, meetings, letters, decisions, review);
        summary.Validate();
        return summary;
    }

    public NewGmScenarioSnapshot RecordOwnerMeeting(NewGmScenarioSnapshot scenario, OwnerMeeting meeting)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(meeting);
        meeting.Validate();

        var entry = new CareerTimelineEntry(
            $"career:owner-meeting:{meeting.MeetingId}",
            CareerTimelineEntryType.OwnerMeeting,
            meeting.ScheduledDate,
            scenario.Season.Year,
            scenario.AlphaSnapshot.GeneralManager.PersonId,
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            $"Owner meeting: {meeting.MeetingType}",
            meeting.Summary,
            null,
            meeting.MeetingType is OwnerMeetingType.EndOfSeason or OwnerMeetingType.Special ? HistoryImportance.Important : HistoryImportance.Normal);
        return scenario with { CareerTimeline = scenario.CareerTimeline.Add(entry) };
    }

    public NewGmScenarioSnapshot RecordOwnerLetter(NewGmScenarioSnapshot scenario, OwnerLetter letter)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(letter);
        letter.Validate();

        var entry = new CareerTimelineEntry(
            $"career:owner-letter:{letter.LetterId}",
            CareerTimelineEntryType.OwnerLetter,
            letter.Date,
            scenario.Season.Year,
            scenario.AlphaSnapshot.GeneralManager.PersonId,
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            letter.Subject,
            letter.Body,
            null,
            letter.IsWarning ? HistoryImportance.Important : HistoryImportance.Normal);
        return scenario with { CareerTimeline = scenario.CareerTimeline.Add(entry) };
    }

    public NewGmScenarioSnapshot RecordPerformanceReview(NewGmScenarioSnapshot scenario, OwnerPerformanceReview review)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(review);
        review.Validate();

        var entry = new CareerTimelineEntry(
            $"career:owner-review:{review.ReviewId}",
            CareerTimelineEntryType.OwnerPerformanceReview,
            scenario.CurrentDate,
            scenario.Season.Year,
            scenario.AlphaSnapshot.GeneralManager.PersonId,
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            $"Owner performance review: {review.OverallGrade}",
            review.Narrative,
            null,
            review.OverallGrade is OwnerPerformanceGrade.Poor or OwnerPerformanceGrade.Critical ? HistoryImportance.Important : HistoryImportance.Normal);
        var history = scenario.GmCareerHistory is null
            ? null
            : scenario.GmCareerHistory with
            {
                OwnerConfidenceHistory = scenario.GmCareerHistory.OwnerConfidenceHistory
                    .Append($"{scenario.CurrentDate:yyyy-MM-dd}: {review.OverallGrade} review. {review.Recommendation}")
                    .ToArray()
            };
        return scenario with { CareerTimeline = scenario.CareerTimeline.Add(entry), GmCareerHistory = history };
    }

    public IReadOnlyList<AlphaInboxItem> BuildOwnerInboxItems(NewGmScenarioSnapshot scenario, OwnerOfficeSummary summary)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(summary);
        summary.Validate();

        var items = new List<AlphaInboxItem>();
        var warning = summary.Letters.FirstOrDefault(letter => letter.IsWarning) ?? summary.Letters.FirstOrDefault();
        if (warning is not null)
        {
            items.Add(Inbox(scenario, LegacyEventType.OwnerOffseasonReview, warning.Subject, warning.Body, warning.IsWarning ? LegacyEventSeverity.Warning : LegacyEventSeverity.Notice));
        }

        var meeting = summary.Meetings.OrderBy(meeting => Math.Abs(meeting.ScheduledDate.DayNumber - scenario.CurrentDate.DayNumber)).FirstOrDefault();
        if (meeting is not null && meeting.ScheduledDate.DayNumber - scenario.CurrentDate.DayNumber <= 7)
        {
            items.Add(Inbox(scenario, LegacyEventType.OwnerGoalSet, $"Owner meeting scheduled: {meeting.MeetingType}", meeting.Summary, LegacyEventSeverity.Notice));
        }

        if (summary.JobSecurity.Level is JobSecurityLevel.HotSeat or JobSecurityLevel.Critical)
        {
            items.Add(Inbox(scenario, LegacyEventType.OwnerOffseasonReview, $"Job security: {summary.JobSecurity.Level}", summary.JobSecurity.Explanation, LegacyEventSeverity.Warning));
        }

        return items.ToArray();
    }

    private static OwnerPersonalityProfile BuildPersonality(NewGmScenarioSnapshot scenario)
    {
        var owner = scenario.AlphaSnapshot.Owner;
        var type = owner.Archetype switch
        {
            OwnerArchetype.Builder => OwnerPersonalityType.PatientBuilder,
            OwnerArchetype.Competitor => OwnerPersonalityType.WinNow,
            OwnerArchetype.CommunityOwner => OwnerPersonalityType.CommunityFocused,
            OwnerArchetype.Investor => OwnerPersonalityType.FinancialConservative,
            OwnerArchetype.Traditionalist => OwnerPersonalityType.VeteranLover,
            OwnerArchetype.Innovator => OwnerPersonalityType.ProspectLover,
            _ => OwnerPersonalityType.PatientBuilder
        };

        var relationshipStyle = owner.AutonomyLevel switch
        {
            var value when value == (OwnerAutonomyLevel)3 => OwnerRelationshipStyle.HandsOff,
            OwnerAutonomyLevel.High => OwnerRelationshipStyle.Collaborative,
            OwnerAutonomyLevel.Normal => OwnerRelationshipStyle.Direct,
            OwnerAutonomyLevel.Low => OwnerRelationshipStyle.Demanding,
            _ => OwnerRelationshipStyle.Micromanaging
        };

        var budgetPhilosophy = type switch
        {
            OwnerPersonalityType.FinancialConservative => OwnerBudgetPhilosophy.Conservative,
            OwnerPersonalityType.BigSpender or OwnerPersonalityType.ChampionshipOrBust => OwnerBudgetPhilosophy.SpendForWinner,
            OwnerPersonalityType.WinNow => OwnerBudgetPhilosophy.Aggressive,
            OwnerPersonalityType.CommunityFocused => OwnerBudgetPhilosophy.ProtectCashFlow,
            _ => OwnerBudgetPhilosophy.Balanced
        };

        var vision = type switch
        {
            OwnerPersonalityType.PatientBuilder => "Build a sustainable junior program that graduates players without chasing shortcuts.",
            OwnerPersonalityType.WinNow => "Push the organization toward immediate contention while avoiding reckless panic moves.",
            OwnerPersonalityType.FinancialConservative => "Protect the club's finances and make every hockey operations dollar explainable.",
            OwnerPersonalityType.ProspectLover => "Win through scouting, development, and a deep prospect pipeline.",
            OwnerPersonalityType.VeteranLover => "Keep the room mature, competitive, and accountable.",
            OwnerPersonalityType.CommunityFocused => "Make the club a trusted part of the local hockey community.",
            _ => "Keep the organization disciplined, competitive, and accountable."
        };

        var profile = new OwnerPersonalityProfile(
            type,
            vision,
            RiskTolerance: type is OwnerPersonalityType.WinNow or OwnerPersonalityType.BigSpender or OwnerPersonalityType.ChampionshipOrBust ? 72 : type == OwnerPersonalityType.FinancialConservative ? 32 : 55,
            Patience: owner.Patience,
            budgetPhilosophy,
            WinningExpectation: type is OwnerPersonalityType.WinNow or OwnerPersonalityType.ChampionshipOrBust ? "Playoff push expected." : "Competitive progress expected.",
            ProspectExpectation: type is OwnerPersonalityType.PatientBuilder or OwnerPersonalityType.ProspectLover ? "Prospect growth is central to the mandate." : "Prospects matter, but results still carry weight.",
            relationshipStyle);
        profile.Validate();
        return profile;
    }

    private static IReadOnlyList<OwnerExpectation> BuildSeasonExpectations(NewGmScenarioSnapshot scenario, OwnerPersonalityProfile personality, BudgetSnapshot budget)
    {
        var deadline = scenario.Season.Calendar.SeasonEnd ?? scenario.DraftDate.AddYears(1).AddDays(-3);
        var expectations = new List<OwnerExpectation>
        {
            Expectation(scenario, OwnerExpectationType.DevelopYoungPlayers, 5, 3, deadline, DevelopmentProgress(scenario), "Show visible progress from young players and recent draft picks."),
            Expectation(scenario, OwnerExpectationType.ReachPlayoffs, personality.PersonalityType is OwnerPersonalityType.WinNow or OwnerPersonalityType.ChampionshipOrBust ? 5 : 3, 4, deadline, WinningProgress(scenario), "Keep the club in the playoff conversation."),
            Expectation(scenario, OwnerExpectationType.ImproveGoaltending, 3, 3, deadline, GoalieProgress(scenario), "Stabilize the crease and reduce nightly risk.")
        };

        if (budget.Status is BudgetStatus.NearLimit or BudgetStatus.OverBudget || personality.BudgetPhilosophy is OwnerBudgetPhilosophy.Conservative or OwnerBudgetPhilosophy.ProtectCashFlow)
        {
            expectations.Add(Expectation(scenario, OwnerExpectationType.ReduceBudget, 4, 3, deadline, budget.Status == BudgetStatus.UnderBudget ? 75 : 35, "Keep hockey operations spending under control."));
        }

        if (personality.PersonalityType is OwnerPersonalityType.VeteranLover or OwnerPersonalityType.WinNow)
        {
            expectations.Add(Expectation(scenario, OwnerExpectationType.AcquireVeteranLeadership, 3, 2, deadline, VeteranLeadershipProgress(scenario), "Make sure the room has credible veteran leadership."));
        }

        foreach (var expectation in expectations)
        {
            expectation.Validate();
        }

        return expectations
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.ExpectationType)
            .ToArray();
    }

    private static OwnerConfidenceState BuildConfidenceState(NewGmScenarioSnapshot scenario, BudgetSnapshot budget)
    {
        var owner = scenario.AlphaSnapshot.Owner;
        var standing = PlayerStanding(scenario);
        var winPct = standing is null || standing.GamesPlayed == 0 ? 0.5m : (decimal)standing.Wins / standing.GamesPlayed;
        var development = scenario.DevelopmentReviews.Count + scenario.DevelopmentRecommendations.Count + scenario.AlphaSnapshot.DevelopmentProfiles.Count / 4;
        var draft = scenario.DraftPickHistory.Count + scenario.ProspectRights.Count / 3;
        var trades = scenario.TradeOffers.Count(offer => offer.Status == TradeOfferStatus.Completed);
        var budgetPenalty = budget.Status == BudgetStatus.OverBudget ? 12 : budget.Status == BudgetStatus.NearLimit ? 5 : 0;
        var relationship = OwnerGmRelationship(scenario);
        var confidence = Clamp(owner.Confidence + (winPct >= 0.55m ? 7 : winPct < 0.40m ? -10 : 0) + Math.Min(8, development) + Math.Min(6, draft) + Math.Min(4, trades) - budgetPenalty + RelationshipModifier(relationship));
        var pressure = Clamp(100 - owner.Patience + budgetPenalty + (winPct < 0.40m ? 12 : 0));
        var support = Clamp((confidence + owner.Trust + relationship) / 3);
        var drivers = new List<string>
        {
            standing is null || standing.GamesPlayed == 0 ? "Competitive record is still forming." : $"Current record is {standing.Wins}-{standing.Losses}-{standing.OvertimeLosses}.",
            budget.Status == BudgetStatus.UnderBudget ? "Budget discipline is helping owner trust." : $"Budget status is {budget.Status}.",
            scenario.ProspectRights.Count > 0 ? "Draft and prospect rights are organized." : "Owner wants more prospect pipeline evidence.",
            relationship >= 60 ? "Owner-GM relationship is supportive." : relationship < 45 ? "Owner-GM relationship is strained." : "Owner-GM relationship is still being established."
        };
        var state = new OwnerConfidenceState(confidence, owner.Trust, owner.Patience, pressure, support, drivers);
        state.Validate();
        return state;
    }

    private static JobSecurityAssessment BuildJobSecurity(NewGmScenarioSnapshot scenario, OwnerConfidenceState confidence, IReadOnlyList<OwnerExpectation> expectations, BudgetSnapshot budget)
    {
        var expectationScore = expectations.Count == 0 ? 50 : (int)Math.Round(expectations.Average(item => item.CurrentProgress));
        var score = Clamp((confidence.Confidence + confidence.Trust + confidence.Patience + expectationScore + confidence.Support) / 5 - (budget.Status == BudgetStatus.OverBudget ? 12 : 0));
        var level = score switch
        {
            >= 82 => JobSecurityLevel.VerySecure,
            >= 68 => JobSecurityLevel.Secure,
            >= 52 => JobSecurityLevel.Stable,
            >= 38 => JobSecurityLevel.Questioned,
            >= 22 => JobSecurityLevel.HotSeat,
            _ => JobSecurityLevel.Critical
        };
        var reasons = new List<string>
        {
            $"Owner confidence is {confidence.Confidence}/100.",
            $"Expectation progress average is {expectationScore}/100.",
            budget.Status == BudgetStatus.OverBudget ? "Budget is over the approved hockey operations number." : $"Budget is {budget.Status}.",
            scenario.TradeOffers.Any(offer => offer.Status == TradeOfferStatus.Completed) ? "Trade activity is visible to ownership." : "Owner still wants proof from roster-building decisions."
        };
        var assessment = new JobSecurityAssessment(level, score, $"{scenario.AlphaSnapshot.Owner.Name} currently views the GM as {Readable(level)} because confidence, expectations, budget discipline, and relationship support are all being weighed.", reasons);
        assessment.Validate();
        return assessment;
    }

    private static IReadOnlyList<OwnerMeeting> BuildMeetings(NewGmScenarioSnapshot scenario, IReadOnlyList<OwnerExpectation> expectations, JobSecurityAssessment jobSecurity, BudgetSnapshot budget)
    {
        var preseason = scenario.Season.Calendar.SeasonStart.Value;
        var deadline = scenario.Season.Calendar.Milestones
            .FirstOrDefault(milestone => milestone.Type == SeasonMilestoneType.TradeDeadline)?.Date.Value
            ?? preseason.AddDays(90);
        var seasonEnd = scenario.Season.Calendar.SeasonEnd.Value;
        var meetings = new[]
        {
            Meeting(scenario, OwnerMeetingType.Preseason, preseason.AddDays(-3), expectations, jobSecurity, "Review opening roster, staff plan, and development mandate."),
            Meeting(scenario, OwnerMeetingType.BudgetReview, scenario.CurrentDate.AddDays(7), expectations, jobSecurity, budget.Status == BudgetStatus.OverBudget ? "Ownership wants immediate budget explanation." : "Review budget posture before the season calendar gets busy."),
            Meeting(scenario, OwnerMeetingType.TradeDeadline, deadline.AddDays(-2), expectations, jobSecurity, "Review buy/sell posture and avoid reactive moves."),
            Meeting(scenario, OwnerMeetingType.EndOfSeason, seasonEnd.AddDays(1), expectations, jobSecurity, "Evaluate season performance, development progress, and next-year expectations.")
        };
        foreach (var meeting in meetings)
        {
            meeting.Validate();
        }

        return meetings;
    }

    private static IReadOnlyList<OwnerLetter> BuildLetters(NewGmScenarioSnapshot scenario, JobSecurityAssessment jobSecurity, IReadOnlyList<OwnerExpectation> expectations, BudgetSnapshot budget)
    {
        var letters = new List<OwnerLetter>
        {
            new(
                $"owner-letter:{scenario.Season.Year}:mandate",
                scenario.CurrentDate,
                "Owner mandate",
                $"{scenario.AlphaSnapshot.Owner.Name}: I want clarity on our direction. {expectations.First().Description} Keep me informed before major budget or roster decisions.",
                false)
        };

        if (budget.Status is BudgetStatus.NearLimit or BudgetStatus.OverBudget)
        {
            letters.Add(new OwnerLetter(
                $"owner-letter:{scenario.Season.Year}:budget",
                scenario.CurrentDate,
                "Budget caution",
                $"{scenario.AlphaSnapshot.Owner.Name}: Please avoid expensive commitments unless the hockey case is clear. Current budget status is {budget.Status}.",
                true));
        }

        if (jobSecurity.Level is JobSecurityLevel.HotSeat or JobSecurityLevel.Critical or JobSecurityLevel.Questioned)
        {
            letters.Add(new OwnerLetter(
                $"owner-letter:{scenario.Season.Year}:job-security",
                scenario.CurrentDate,
                "Job security warning",
                $"{scenario.AlphaSnapshot.Owner.Name}: {jobSecurity.Explanation} I am not firing anyone today, but the next review needs stronger evidence.",
                true));
        }

        foreach (var letter in letters)
        {
            letter.Validate();
        }

        return letters;
    }

    private static IReadOnlyList<OwnerDecision> BuildOwnerDecisions(NewGmScenarioSnapshot scenario, OwnerPersonalityProfile personality, OwnerConfidenceState confidence, BudgetSnapshot budget)
    {
        var decisions = new List<OwnerDecision>();
        if (budget.Status == BudgetStatus.OverBudget)
        {
            decisions.Add(new OwnerDecision(OwnerDecisionType.FreezeHiring, "Hockey operations spending is over budget.", "New staff spending should be justified before approval.", true));
            decisions.Add(new OwnerDecision(OwnerDecisionType.ReduceBudget, "Ownership wants the club back under the approved number.", "GM should review contracts, staff hires, and low-impact spending.", true));
        }
        else if (personality.BudgetPhilosophy is OwnerBudgetPhilosophy.Aggressive or OwnerBudgetPhilosophy.SpendForWinner && confidence.Confidence >= 62)
        {
            decisions.Add(new OwnerDecision(OwnerDecisionType.ApproveLargerBudget, "Owner confidence is strong enough to support targeted spending.", "The GM can justify a larger budget request if the move advances the plan.", false));
        }

        if (scenario.ProspectRights.Count < 3 || personality.PersonalityType == OwnerPersonalityType.ProspectLover)
        {
            decisions.Add(new OwnerDecision(OwnerDecisionType.IncreaseScouting, "The owner wants a deeper prospect pipeline.", "Scouting coverage should stay active and evidence-driven.", true));
        }

        if (PlayerStanding(scenario) is { GamesPlayed: > 10 } standing && standing.Wins < standing.Losses)
        {
            decisions.Add(new OwnerDecision(OwnerDecisionType.DemandRosterChanges, "The current record is below expectations.", "Owner expects the GM to review roster balance and trade/free-agent options.", true));
        }

        if (personality.PersonalityType is OwnerPersonalityType.PatientBuilder or OwnerPersonalityType.ProspectLover && confidence.Pressure < 65)
        {
            decisions.Add(new OwnerDecision(OwnerDecisionType.SupportRebuild, "Owner patience supports a development-first path.", "The GM can prioritize prospects as long as expectations are communicated.", false));
        }

        if (decisions.Count == 0)
        {
            decisions.Add(new OwnerDecision(OwnerDecisionType.ReplaceExpectations, "No urgent owner mandate has changed.", "Maintain current expectations and review again at the next owner meeting.", false));
        }

        foreach (var decision in decisions)
        {
            decision.Validate();
        }

        return decisions;
    }

    private static OwnerPerformanceReview BuildPerformanceReview(NewGmScenarioSnapshot scenario, OwnerConfidenceState confidence, JobSecurityAssessment security, BudgetSnapshot budget)
    {
        var categories = new Dictionary<string, OwnerPerformanceGrade>
        {
            ["Record"] = GradeFromStanding(scenario),
            ["Player Development"] = scenario.DevelopmentReviews.Count > 0 || scenario.DevelopmentRecommendations.Count > 0 ? OwnerPerformanceGrade.Good : OwnerPerformanceGrade.Average,
            ["Drafting"] = scenario.DraftPickHistory.Count > 0 || scenario.ProspectRights.Count > 0 ? OwnerPerformanceGrade.Good : OwnerPerformanceGrade.Average,
            ["Scouting"] = scenario.CompletedScoutingReports.Count > 0 ? OwnerPerformanceGrade.Good : OwnerPerformanceGrade.Average,
            ["Staff Hiring"] = scenario.StaffMembers.Count >= 5 ? OwnerPerformanceGrade.Good : OwnerPerformanceGrade.Average,
            ["Budget"] = budget.Status == BudgetStatus.OverBudget ? OwnerPerformanceGrade.Poor : budget.Status == BudgetStatus.NearLimit ? OwnerPerformanceGrade.Average : OwnerPerformanceGrade.Good,
            ["Contracts"] = scenario.PendingActions.Count(action => action.IsOpen) > 4 ? OwnerPerformanceGrade.Average : OwnerPerformanceGrade.Good,
            ["Trades"] = scenario.TradeOffers.Any(offer => offer.Status == TradeOfferStatus.Completed) ? OwnerPerformanceGrade.Good : OwnerPerformanceGrade.Average,
            ["Organization Health"] = confidence.Support >= 65 ? OwnerPerformanceGrade.Good : confidence.Support < 40 ? OwnerPerformanceGrade.Poor : OwnerPerformanceGrade.Average
        };
        var average = categories.Values.Average(GradeScore);
        var overall = average switch
        {
            >= 4.5 => OwnerPerformanceGrade.Excellent,
            >= 3.5 => OwnerPerformanceGrade.Good,
            >= 2.5 => OwnerPerformanceGrade.Average,
            >= 1.5 => OwnerPerformanceGrade.Poor,
            _ => OwnerPerformanceGrade.Critical
        };
        var review = new OwnerPerformanceReview(
            $"owner-review:{scenario.Season.Year}:{scenario.Organization.OrganizationId}",
            scenario.Season.Year,
            overall,
            categories,
            $"{scenario.AlphaSnapshot.Owner.Name} grades the GM as {overall}. Job security is {security.Level}; confidence is {confidence.Confidence}/100 and support is {confidence.Support}/100.",
            security.Level is JobSecurityLevel.HotSeat or JobSecurityLevel.Critical
                ? "Continue with warning conditions; no automatic firing in Alpha 4.8."
                : "Continue with the current mandate and revisit at the next scheduled owner meeting.");
        review.Validate();
        return review;
    }

    private static OwnerExpectation Expectation(NewGmScenarioSnapshot scenario, OwnerExpectationType type, int priority, int difficulty, DateOnly deadline, int progress, string description)
    {
        var expectation = new OwnerExpectation(
            $"owner-expectation:{scenario.Season.Year}:{type}",
            type,
            priority,
            difficulty,
            deadline,
            Clamp(progress),
            description);
        expectation.Validate();
        return expectation;
    }

    private static OwnerMeeting Meeting(NewGmScenarioSnapshot scenario, OwnerMeetingType type, DateOnly date, IReadOnlyList<OwnerExpectation> expectations, JobSecurityAssessment security, string topic)
    {
        var meeting = new OwnerMeeting(
            $"owner-meeting:{scenario.Season.Year}:{type}",
            type,
            date,
            $"{scenario.AlphaSnapshot.Owner.Name}: {topic} Current job security is {security.Level}.",
            new[] { "Commit to the current plan.", "Ask for more budget support.", "Promise a clearer roster response." },
            expectations.Take(2).Select(expectation => $"Focus: {expectation.Description}").Append("Keep decisions explicit; no automatic roster or contract moves.").ToArray(),
            expectations.Take(2).ToArray(),
            $"{type} meeting scheduled for {date:yyyy-MM-dd}. Owner will review expectations, budget, and GM accountability.");
        meeting.Validate();
        return meeting;
    }

    private static TeamStanding? PlayerStanding(NewGmScenarioSnapshot scenario) =>
        scenario.Standings?.Teams.FirstOrDefault(team => team.OrganizationId == scenario.Organization.OrganizationId);

    private static int WinningProgress(NewGmScenarioSnapshot scenario)
    {
        var standing = PlayerStanding(scenario);
        if (standing is null || standing.GamesPlayed == 0)
        {
            return 45;
        }

        return Clamp((int)Math.Round((decimal)standing.Points / Math.Max(1, standing.GamesPlayed * 2) * 100));
    }

    private static int DevelopmentProgress(NewGmScenarioSnapshot scenario) =>
        Clamp(45 + Math.Min(35, scenario.DevelopmentReviews.Count * 6 + scenario.DevelopmentRecommendations.Count * 3 + scenario.AlphaSnapshot.DevelopmentProfiles.Count));

    private static int GoalieProgress(NewGmScenarioSnapshot scenario)
    {
        var goalies = scenario.AlphaSnapshot.Roster.CurrentPlayers.Count(player => player.Position == RosterPosition.Goalie);
        return goalies >= 2 ? 70 : goalies == 1 ? 45 : 20;
    }

    private static int VeteranLeadershipProgress(NewGmScenarioSnapshot scenario)
    {
        var veterans = scenario.AlphaSnapshot.Roster.CurrentPlayers.Count(player => player.IsOverage());
        return Clamp(35 + veterans * 12);
    }

    private static int OwnerGmRelationship(NewGmScenarioSnapshot scenario) =>
        scenario.AlphaSnapshot.Relationships
            .Where(relationship => relationship.FromPersonId == scenario.AlphaSnapshot.Owner.OwnerId && relationship.ToPersonId == scenario.AlphaSnapshot.GeneralManager.PersonId)
            .Select(relationship => (relationship.Trust + relationship.Respect + relationship.Confidence + relationship.Loyalty) / 4)
            .DefaultIfEmpty((scenario.AlphaSnapshot.Owner.Trust + scenario.AlphaSnapshot.Owner.Confidence) / 2)
            .First();

    private static int RelationshipModifier(int value) =>
        value >= 70 ? 6 : value >= 55 ? 2 : value < 40 ? -8 : -2;

    private static OwnerPerformanceGrade GradeFromStanding(NewGmScenarioSnapshot scenario)
    {
        var standing = PlayerStanding(scenario);
        if (standing is null || standing.GamesPlayed == 0)
        {
            return OwnerPerformanceGrade.Average;
        }

        var pointPct = (decimal)standing.Points / Math.Max(1, standing.GamesPlayed * 2);
        return pointPct switch
        {
            >= 0.70m => OwnerPerformanceGrade.Excellent,
            >= 0.56m => OwnerPerformanceGrade.Good,
            >= 0.45m => OwnerPerformanceGrade.Average,
            >= 0.30m => OwnerPerformanceGrade.Poor,
            _ => OwnerPerformanceGrade.Critical
        };
    }

    private static double GradeScore(OwnerPerformanceGrade grade) =>
        grade switch
        {
            OwnerPerformanceGrade.Excellent => 5,
            OwnerPerformanceGrade.Good => 4,
            OwnerPerformanceGrade.Average => 3,
            OwnerPerformanceGrade.Poor => 2,
            OwnerPerformanceGrade.Critical => 1,
            _ => 3
        };

    private static string Readable(JobSecurityLevel level) =>
        level switch
        {
            JobSecurityLevel.VerySecure => "very secure",
            JobSecurityLevel.HotSeat => "on the hot seat",
            _ => level.ToString().ToLowerInvariant()
        };

    private static int Clamp(int value) => Math.Clamp(value, 0, 100);

    private static AlphaInboxItem Inbox(NewGmScenarioSnapshot scenario, LegacyEventType eventType, string title, string summary, LegacyEventSeverity severity) =>
        new(
            InboxItemId: $"inbox:owner-office:{Guid.NewGuid():N}",
            Date: new DateTimeOffset(scenario.CurrentDate.Year, scenario.CurrentDate.Month, scenario.CurrentDate.Day, 9, 0, 0, TimeSpan.Zero),
            EventType: eventType,
            Severity: severity,
            Title: title,
            Summary: summary,
            PrimaryPersonId: scenario.AlphaSnapshot.Owner.OwnerId);
}
