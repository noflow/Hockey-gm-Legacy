using LegacyEngine.Events;
using LegacyEngine.Injuries;
using LegacyEngine.Rosters;
using LegacyEngine.Seasons;

namespace LegacyEngine.Integration;

public sealed class ExecutiveReportService
{
    public ExecutiveReportGenerationResult GenerateFrontOfficeReadinessReport(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var readiness = new SeasonReadinessService().Evaluate(registry, scenario);
        if (!readiness.CanBeginSeason)
        {
            return Result(false, scenario, null, Array.Empty<AlphaInboxItem>(), "Front Office Readiness Report is locked until Opening Night requirements are complete.");
        }

        var roster = scenario.AlphaSnapshot.Roster;
        var current = roster.CurrentPlayers;
        var active = roster.ActivePlayers;
        var imports = current.Count(player => player.IsImport);
        var overage = current.Count(player => player.IsOverage());
        var health = CalculateOrganizationHealth(readiness, scenario);
        var recommendation = health >= 82
            ? "READY"
            : health >= 65
                ? "READY WITH CONCERNS"
                : "NOT READY";
        var topProspect = TopProspectName(scenario);
        var hiddenGem = HiddenGemName(scenario);
        var mostImproved = MostImprovedPlayerName(scenario);
        var breakOut = PlayerReadyToBreakOutName(scenario);
        var needsTime = PlayerNeedingMoreTimeName(scenario);
        var injuries = scenario.AlphaSnapshot.Injuries.Where(injury => injury.IsActive).ToArray();

        var report = new ExecutiveReportRecord(
            ReportId: ReportId(ExecutiveReportKind.FrontOfficeReadiness, scenario),
            Kind: ExecutiveReportKind.FrontOfficeReadiness,
            GeneratedAt: ToDateTimeOffset(scenario.CurrentDate),
            OrganizationId: scenario.Organization.OrganizationId,
            OrganizationName: scenario.Organization.Name,
            LeagueId: scenario.Season.LeagueId,
            SeasonId: scenario.Season.SeasonId,
            SeasonYear: scenario.Season.Year,
            GeneralManagerName: scenario.AlphaSnapshot.GeneralManager.Identity.DisplayName,
            OwnerName: scenario.AlphaSnapshot.Owner.Name,
            Title: "Front Office Readiness Report",
            Recommendation: recommendation,
            OrganizationHealthPercent: health,
            Sections: new[]
            {
                Section("Display", $"Opening report for {scenario.Organization.Name}.", new Dictionary<string, string>
                {
                    ["Organization"] = scenario.Organization.Name,
                    ["League"] = scenario.Season.LeagueId,
                    ["Season"] = scenario.Season.Year.ToString(),
                    ["General Manager"] = scenario.AlphaSnapshot.GeneralManager.Identity.DisplayName,
                    ["Owner"] = scenario.AlphaSnapshot.Owner.Name
                }),
                Section("Owner Review", "Ownership reviewed the offseason plan and opening roster posture.", new Dictionary<string, string>
                {
                    ["Owner Satisfaction"] = readiness.OwnerSatisfaction,
                    ["Top Success"] = scenario.ProspectRights.Count > 0 ? "Draft class and prospect rights are organized." : "Roster compliance work is under control.",
                    ["Biggest Concern"] = readiness.RosterReport.ValidationResult.IsValid ? "Opening expectations will rise quickly if the club starts well." : readiness.RosterReport.ValidationResult.Message,
                    ["Expectations"] = "Build a competitive team while protecting the development pipeline.",
                    ["Recommendation"] = readiness.OwnerReview
                }),
                Section("Head Coach Report", "The coaching staff focused on roster balance, leadership, and opening-night usability.", new Dictionary<string, string>
                {
                    ["Roster readiness"] = readiness.RosterStatus,
                    ["Forward depth"] = active.Count(player => IsForward(player.Position)) >= 12 ? "Playable" : "Thin",
                    ["Defense depth"] = active.Count(player => player.Position == RosterPosition.Defense) >= 6 ? "Playable" : "Needs depth",
                    ["Goaltending"] = active.Count(player => player.Position == RosterPosition.Goalie) >= 2 ? "Covered" : "Short",
                    ["Leadership"] = scenario.AlphaSnapshot.Owner.Trust >= 55 ? "Stable room entering the year." : "Leadership support needs attention.",
                    ["Special Teams"] = "To be established once practices begin.",
                    ["Most Improved Player"] = mostImproved,
                    ["Biggest Concern"] = readiness.HeadCoachSummary
                }),
                Section("Head Scout Report", "Scouting summarized the draft class and future watch priorities without revealing hidden ratings.", new Dictionary<string, string>
                {
                    ["Draft Grade"] = scenario.DraftRights.Count > 0 ? "B" : "Incomplete",
                    ["Prospect Pipeline"] = scenario.ProspectRights.Count >= 3 ? "Healthy" : "Needs volume",
                    ["Top Prospect"] = topProspect,
                    ["Hidden Gem"] = hiddenGem,
                    ["Future Watch List"] = FutureWatchList(scenario)
                }),
                Section("Development Department", "Development staff highlighted storylines that matter before the first puck drop.", new Dictionary<string, string>
                {
                    ["Top Development Story"] = $"{breakOut} is trending toward a larger role.",
                    ["Biggest Surprise"] = hiddenGem,
                    ["Player Ready to Break Out"] = breakOut,
                    ["Player Needing More Time"] = needsTime
                }),
                Section("Medical Department", "Medical staff reviewed current availability and recovery risk.", new Dictionary<string, string>
                {
                    ["Current Injuries"] = injuries.Length.ToString(),
                    ["Risk Players"] = injuries.Length == 0 ? "No active risk flags." : string.Join(", ", injuries.Select(injury => PlayerName(scenario, injury.PersonId))),
                    ["Recovery Outlook"] = injuries.Length == 0 ? "Healthy entering opening night." : RecoveryOutlook(injuries)
                }),
                Section("Roster Compliance", "RuleEngine validation is the official opening-night roster gate.", new Dictionary<string, string>
                {
                    ["Current Size"] = readiness.RosterReport.CurrentRosterSize.ToString(),
                    ["Required Size"] = readiness.RosterReport.RequiredRosterSize.ToString(),
                    ["Goalie Count"] = readiness.RosterReport.Goalies.ToString(),
                    ["Import Count"] = imports.ToString(),
                    ["Overage Count"] = overage.ToString(),
                    ["Status"] = readiness.CanBeginSeason ? "READY" : "NOT READY"
                }),
                Section("Organization Health", "Health blends roster compliance, prospects, staff, owner confidence, recruiting, and development.", new Dictionary<string, string>
                {
                    ["Overall %"] = health.ToString(),
                    ["Roster"] = readiness.RosterReport.ValidationResult.IsValid ? "Up" : "Down",
                    ["Prospects"] = scenario.ProspectRights.Count >= 3 ? "Up" : "Flat",
                    ["Staff"] = scenario.StaffMembers.Count >= 4 ? "Up" : "Flat",
                    ["Owner"] = scenario.AlphaSnapshot.Owner.Confidence >= 55 ? "Up" : "Flat",
                    ["Recruiting"] = scenario.AlphaSnapshot.Recruits.Count > 0 ? "Up" : "Flat",
                    ["Development"] = scenario.AlphaSnapshot.DevelopmentProfiles.Count > 0 ? "Up" : "Flat"
                }),
                Section("Opening Night Recommendation", $"Opening night recommendation: {recommendation}.", new Dictionary<string, string>
                {
                    ["Recommendation"] = recommendation
                })
            },
            ExecutiveSummary: $"{scenario.Organization.Name} enters opening night with a {recommendation.ToLowerInvariant()} recommendation, {readiness.RosterStatus.ToLowerInvariant()} roster status, and an organization health score of {health}%.");
        report.Validate();

        var updated = scenario with { ExecutiveReports = scenario.ExecutiveReports.AddOrReplace(report) };
        QueueReportEvent(registry, updated, LegacyEventType.FrontOfficeReadinessReportCreated, "Front Office Readiness Report created", report.ExecutiveSummary);
        var inbox = new[]
        {
            Inbox(updated, LegacyEventType.FrontOfficeReadinessReportCreated, "Front Office Readiness Report", report.ExecutiveSummary)
        };

        return Result(true, updated, report, inbox, "Front Office Readiness Report archived.");
    }

    public ExecutiveReportGenerationResult GenerateEndOfSeasonExecutiveReview(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        if (scenario.Season.Status != SeasonStatus.Completed)
        {
            return Result(false, scenario, null, Array.Empty<AlphaInboxItem>(), "End of Season Executive Review is locked until the season is completed.");
        }

        var previous = scenario.ExecutiveReports
            .PreviousSeasons(scenario.Season.Year)
            .Where(report => report.Kind == ExecutiveReportKind.EndOfSeasonExecutiveReview)
            .OrderByDescending(report => report.SeasonYear)
            .FirstOrDefault();
        var health = CalculateEndSeasonHealth(scenario);
        var topProspect = TopProspectName(scenario);
        var hiddenGem = HiddenGemName(scenario);
        var breakout = PlayerReadyToBreakOutName(scenario);
        var improved = MostImprovedPlayerName(scenario);
        var regression = PlayerNeedingMoreTimeName(scenario);
        var injuries = scenario.AlphaSnapshot.Injuries.ToArray();
        var remainingBudget = scenario.AlphaSnapshot.Owner.Budget.Total - scenario.AlphaSnapshot.Owner.Budget.PlayerPayroll;
        var ownerWouldRehire = scenario.AlphaSnapshot.Owner.Confidence >= 50 ? "Yes" : "Uncertain";

        var report = new ExecutiveReportRecord(
            ReportId: ReportId(ExecutiveReportKind.EndOfSeasonExecutiveReview, scenario),
            Kind: ExecutiveReportKind.EndOfSeasonExecutiveReview,
            GeneratedAt: ToDateTimeOffset(scenario.CurrentDate),
            OrganizationId: scenario.Organization.OrganizationId,
            OrganizationName: scenario.Organization.Name,
            LeagueId: scenario.Season.LeagueId,
            SeasonId: scenario.Season.SeasonId,
            SeasonYear: scenario.Season.Year,
            GeneralManagerName: scenario.AlphaSnapshot.GeneralManager.Identity.DisplayName,
            OwnerName: scenario.AlphaSnapshot.Owner.Name,
            Title: "End of Season Executive Review",
            Recommendation: scenario.AlphaSnapshot.Owner.Confidence >= 65 ? "Retain GM with increased expectations." : "Retain GM with review conditions.",
            OrganizationHealthPercent: health,
            Sections: new[]
            {
                Section("Season Result", "Season results use Alpha-known fields only until standings and game simulation exist.", new Dictionary<string, string>
                {
                    ["Season Record"] = "Not simulated in Alpha 1.8.1",
                    ["Playoff Result"] = "Not simulated in Alpha 1.8.1",
                    ["League Finish"] = "Not tracked in Alpha 1.8.1"
                }),
                Section("Owner Review", "Ownership judged progress against culture, budget, and roster readiness rather than simulated standings.", new Dictionary<string, string>
                {
                    ["Owner Satisfaction"] = scenario.AlphaSnapshot.Owner.Confidence >= 60 ? "Satisfied" : "Cautious",
                    ["Were expectations met?"] = scenario.AlphaSnapshot.Owner.Trust >= 55 ? "Mostly" : "Partially",
                    ["Would owner hire you again?"] = ownerWouldRehire,
                    ["Biggest Success"] = "The organization completed a structured GM-led season cycle.",
                    ["Biggest Failure"] = "Competitive results are not available until game simulation is built.",
                    ["Letter from Owner"] = $"{scenario.AlphaSnapshot.Owner.Name}: We value the structure you brought to the organization. Next season will demand clearer competitive proof."
                }),
                Section("Head Coach Review", "The coaching report identifies hockey needs without creating lines or game tactics.", new Dictionary<string, string>
                {
                    ["Team strengths"] = "Roster continuity and development planning.",
                    ["Weaknesses"] = scenario.AlphaSnapshot.Roster.ActivePlayers.Count < 20 ? "Depth remains thin." : "High-end role clarity is still forming.",
                    ["Roster needs"] = "Finalize roles, leadership, and special teams responsibilities.",
                    ["Offseason priorities"] = "Protect top prospects and add targeted depth."
                }),
                Section("Head Scout Review", "The scouting department reviewed draft and prospect outcomes.", new Dictionary<string, string>
                {
                    ["Draft success"] = scenario.DraftRights.Count > 0 ? "Draft class recorded and rights archived." : "No draft class recorded.",
                    ["Prospect development"] = scenario.ProspectRights.Count >= 3 ? "Pipeline has usable volume." : "Pipeline needs more entries.",
                    ["Top Prospect"] = topProspect,
                    ["Hidden Gem"] = hiddenGem,
                    ["Bust Warning"] = regression
                }),
                Section("Development Report", "Development staff summarized trajectory without exposing hidden ratings.", new Dictionary<string, string>
                {
                    ["Most Improved Player"] = improved,
                    ["Biggest Regression"] = regression,
                    ["Breakout Player"] = breakout,
                    ["Prospect Pipeline"] = scenario.ProspectRights.Count >= 3 ? "Improving" : "Needs attention"
                }),
                Section("Medical Report", "Medical summary tracks games lost, recurring risk, returning players, department grade, and budget support.", BuildMedicalReportItems(scenario, injuries)),
                Section("Team Leaders", "Stat leaders are reserved for the future game simulation layer.", new Dictionary<string, string>
                {
                    ["Goals"] = "Not simulated",
                    ["Assists"] = "Not simulated",
                    ["Points"] = "Not simulated",
                    ["Plus Minus"] = "Not simulated",
                    ["Wins"] = "Not simulated",
                    ["Save %"] = "Not simulated",
                    ["Shutouts"] = "Not simulated"
                }),
                Section("Team Awards", "Awards are provisional until gameplay stats exist.", new Dictionary<string, string>
                {
                    ["MVP"] = breakout,
                    ["Most Improved"] = improved,
                    ["Best Rookie"] = topProspect,
                    ["Best Defensive Player"] = scenario.AlphaSnapshot.Roster.ActivePlayers.FirstOrDefault(player => player.Position == RosterPosition.Defense) is { } defender ? PlayerName(scenario, defender.PersonId) : "Not assigned",
                    ["Unsung Hero"] = hiddenGem
                }),
                Section("League Awards", "League awards are placeholders until league-wide simulation exists.", new Dictionary<string, string>
                {
                    ["GM Awards"] = "Not awarded in Alpha 1.8.1",
                    ["Coach Awards"] = "Not awarded in Alpha 1.8.1",
                    ["Player Awards"] = "Not awarded in Alpha 1.8.1"
                }),
                Section("Financial Summary", "Financial summary uses owner budget references already present in the scenario.", new Dictionary<string, string>
                {
                    ["Budget"] = scenario.AlphaSnapshot.Owner.Budget.Total.ToString("0"),
                    ["Spent"] = scenario.AlphaSnapshot.Owner.Budget.PlayerPayroll.ToString("0"),
                    ["Remaining"] = remainingBudget.ToString("0"),
                    ["Owner Comments"] = "Budget discipline remains part of the owner mandate."
                }),
                Section("Fan Report", "Fan fields are executive placeholders until attendance and reputation systems deepen.", new Dictionary<string, string>
                {
                    ["Attendance"] = "Not tracked in Alpha 1.8.1",
                    ["Fan Happiness"] = "Cautiously optimistic",
                    ["Reputation"] = scenario.Organization.Reputation.Local.ToString(),
                    ["Community Support"] = scenario.Organization.Culture.CommunityFocus.ToString(),
                    ["Biggest Rival"] = "Not assigned"
                }),
                Section("Media Report", "Media report summarizes the story of the Alpha season.", new Dictionary<string, string>
                {
                    ["Headline of the Year"] = $"{scenario.Organization.Name} completes first executive cycle under {scenario.AlphaSnapshot.GeneralManager.Identity.DisplayName}",
                    ["Best Moment"] = "Opening roster and staff reviews were completed.",
                    ["Worst Moment"] = injuries.Any(injury => injury.IsActive) ? "Injury concerns carried into the review." : "No major crisis recorded.",
                    ["Biggest Story"] = "Front office structure became the club's identity."
                }),
                Section("Organization Progress", "Progress compares this report with the previous season where available.", BuildProgressComparison(health, scenario, previous)),
                Section("GM Career", "Career values are scoped to the current Alpha organization session.", new Dictionary<string, string>
                {
                    ["Career Record"] = "Not simulated",
                    ["Playoff Record"] = "Not simulated",
                    ["Years with Organization"] = "1",
                    ["Awards"] = "None yet",
                    ["Job Security"] = scenario.AlphaSnapshot.Owner.Confidence >= 55 ? "Stable" : "Under review"
                })
            },
            ExecutiveSummary: $"{scenario.Organization.Name} completed the {scenario.Season.Year} season cycle with organization health at {health}%. Competitive records remain unavailable until game simulation exists, but owner, staff, scouting, development, medical, financial, fan, and media summaries are now archived.");
        report.Validate();

        var updated = scenario with { ExecutiveReports = scenario.ExecutiveReports.AddOrReplace(report) };
        QueueReportEvent(registry, updated, LegacyEventType.EndOfSeasonExecutiveReviewCreated, "End of Season Executive Review created", report.ExecutiveSummary);
        var inbox = new[]
        {
            Inbox(updated, LegacyEventType.EndOfSeasonExecutiveReviewCreated, "End of Season Executive Review", report.ExecutiveSummary)
        };

        return Result(true, updated, report, inbox, "End of Season Executive Review archived.");
    }

    private static IReadOnlyDictionary<string, string> BuildProgressComparison(
        int currentHealth,
        NewGmScenarioSnapshot scenario,
        ExecutiveReportRecord? previous)
    {
        static string Direction(int current, int? prior)
        {
            if (prior is null)
            {
                return "Baseline";
            }

            return current > prior ? "Up" : current < prior ? "Down" : "Flat";
        }

        var previousHealth = previous?.OrganizationHealthPercent;
        return new Dictionary<string, string>
        {
            ["Roster"] = Direction(scenario.AlphaSnapshot.Roster.ActivePlayers.Count, previousHealth is null ? null : 20),
            ["Prospect Pipeline"] = Direction(scenario.ProspectRights.Count, previousHealth is null ? null : 3),
            ["Owner Confidence"] = Direction(scenario.AlphaSnapshot.Owner.Confidence, previousHealth),
            ["Fan Support"] = Direction(scenario.Organization.Culture.CommunityFocus, previousHealth),
            ["Development"] = Direction(scenario.AlphaSnapshot.DevelopmentProfiles.Count, previousHealth is null ? null : 10),
            ["Financial Health"] = Direction(currentHealth, previousHealth)
        };
    }

    private static int CalculateOrganizationHealth(SeasonReadinessReport readiness, NewGmScenarioSnapshot scenario)
    {
        var roster = readiness.RosterReport.ValidationResult.IsValid ? 20 : 8;
        var prospects = Math.Min(15, scenario.ProspectRights.Count * 4);
        var staff = Math.Min(15, scenario.StaffMembers.Count * 3);
        var owner = Math.Clamp((scenario.AlphaSnapshot.Owner.Trust + scenario.AlphaSnapshot.Owner.Confidence) / 12, 0, 15);
        var recruiting = scenario.AlphaSnapshot.Recruits.Count > 0 ? 15 : 5;
        var development = scenario.AlphaSnapshot.DevelopmentProfiles.Count > 0 ? 20 : 5;
        return Math.Clamp(roster + prospects + staff + owner + recruiting + development, 0, 100);
    }

    private static int CalculateEndSeasonHealth(NewGmScenarioSnapshot scenario)
    {
        var roster = scenario.AlphaSnapshot.Roster.ActivePlayers.Count >= 18 ? 20 : 10;
        var prospects = Math.Min(15, scenario.ProspectRights.Count * 4);
        var staff = Math.Min(15, scenario.StaffMembers.Count * 3);
        var owner = Math.Clamp((scenario.AlphaSnapshot.Owner.Trust + scenario.AlphaSnapshot.Owner.Confidence + scenario.AlphaSnapshot.Owner.Patience) / 18, 0, 15);
        var development = scenario.AlphaSnapshot.DevelopmentProfiles.Count > 0 ? 20 : 5;
        var medical = scenario.AlphaSnapshot.Injuries.Count(injury => injury.IsActive) == 0 ? 15 : 8;
        return Math.Clamp(roster + prospects + staff + owner + development + medical, 0, 100);
    }

    private static ExecutiveReportSection Section(string title, string narrative, IReadOnlyDictionary<string, string> items) =>
        new(title, items, narrative);

    private static IReadOnlyDictionary<string, string> BuildMedicalReportItems(NewGmScenarioSnapshot scenario, IReadOnlyList<Injury> injuries)
    {
        var summary = new MedicalHealthService().BuildMedicalSummary(scenario);
        return new Dictionary<string, string>
        {
            ["Games Lost to Injury"] = summary.GamesLostToInjury.ToString(),
            ["Most Significant Injury"] = summary.MostSignificantInjury,
            ["Medical Department Grade"] = summary.MedicalDepartmentGrade,
            ["Players Returning"] = summary.ReturningSoon.ToString(),
            ["High Risk Players"] = summary.HighRiskPlayers.ToString(),
            ["Conditioning Assignments"] = summary.ConditioningAssignments.ToString(),
            ["Medical Budget"] = summary.MedicalBudgetImpact,
            ["Players Entering Offseason Healthy"] = (scenario.AlphaSnapshot.Roster.CurrentPlayers.Count - injuries.Count(injury => injury.IsActive)).ToString()
        };
    }

    private static string TopProspectName(NewGmScenarioSnapshot scenario) =>
        scenario.ProspectRights.OrderBy(item => item.PickNumber).FirstOrDefault()?.ProspectName
        ?? scenario.DraftRights.OrderBy(item => item.PickNumber).FirstOrDefault()?.ProspectName
        ?? "No prospect identified";

    private static string HiddenGemName(NewGmScenarioSnapshot scenario) =>
        scenario.ProspectRights.OrderByDescending(item => item.PickNumber).FirstOrDefault()?.ProspectName
        ?? (scenario.AlphaSnapshot.Recruits.LastOrDefault() is { } recruit ? PlayerName(scenario, recruit.RecruitPersonId) : "No hidden gem identified");

    private static string FutureWatchList(NewGmScenarioSnapshot scenario)
    {
        var names = scenario.ProspectRights
            .OrderBy(item => item.PickNumber)
            .Skip(1)
            .Take(3)
            .Select(item => item.ProspectName)
            .ToArray();
        return names.Length == 0 ? "No watch list yet." : string.Join(", ", names);
    }

    private static string MostImprovedPlayerName(NewGmScenarioSnapshot scenario) =>
        scenario.AlphaSnapshot.DevelopmentProfiles
            .OrderByDescending(profile => profile.TraitValue(LegacyEngine.Development.DevelopmentAttribute.WorkEthic))
            .Select(profile => PlayerName(scenario, profile.PersonId))
            .FirstOrDefault()
        ?? "No development profile available";

    private static string PlayerReadyToBreakOutName(NewGmScenarioSnapshot scenario) =>
        scenario.AlphaSnapshot.DevelopmentProfiles
            .OrderByDescending(profile => profile.TraitValue(LegacyEngine.Development.DevelopmentAttribute.Confidence))
            .Select(profile => PlayerName(scenario, profile.PersonId))
            .FirstOrDefault()
        ?? "No breakout candidate available";

    private static string PlayerNeedingMoreTimeName(NewGmScenarioSnapshot scenario) =>
        scenario.AlphaSnapshot.DevelopmentProfiles
            .OrderBy(profile => profile.TraitValue(LegacyEngine.Development.DevelopmentAttribute.Confidence))
            .Select(profile => PlayerName(scenario, profile.PersonId))
            .FirstOrDefault()
        ?? "No development concern available";

    private static string PlayerName(NewGmScenarioSnapshot scenario, string personId)
    {
        if (string.Equals(personId, scenario.AlphaSnapshot.Owner.OwnerId, StringComparison.Ordinal))
        {
            return scenario.AlphaSnapshot.Owner.Name;
        }

        return scenario.AlphaSnapshot.People.SingleOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
            ?? personId;
    }

    private static string RecoveryOutlook(IReadOnlyList<Injury> injuries)
    {
        var latest = injuries.OrderByDescending(injury => injury.ExpectedReturnDate).First();
        return $"Most cautious return date is {latest.ExpectedReturnDate:yyyy-MM-dd}.";
    }

    private static bool IsForward(RosterPosition position) =>
        position is RosterPosition.Center or RosterPosition.LeftWing or RosterPosition.RightWing;

    private static void QueueReportEvent(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        LegacyEventType eventType,
        string title,
        string description)
    {
        var legacyEvent = registry.EventEngine.CreateEvent(
            ToDateTimeOffset(scenario.CurrentDate),
            eventType,
            LegacyEventSeverity.Notice,
            LegacyEventVisibility.Organization,
            title,
            description,
            new LegacyEventContext(OrganizationId: scenario.Organization.OrganizationId, SeasonId: scenario.Season.SeasonId),
            new Dictionary<string, object?> { ["scenario"] = "alpha_1_8_1_executive_reports" });
        registry.EventEngine.QueueEvent(legacyEvent);
    }

    private static AlphaInboxItem Inbox(
        NewGmScenarioSnapshot scenario,
        LegacyEventType eventType,
        string title,
        string summary) =>
        new(
            InboxItemId: $"inbox:executive-report:{Guid.NewGuid():N}",
            Date: ToDateTimeOffset(scenario.CurrentDate),
            EventType: eventType,
            Severity: LegacyEventSeverity.Notice,
            Title: title,
            Summary: summary,
            PrimaryPersonId: null);

    private static ExecutiveReportGenerationResult Result(
        bool success,
        NewGmScenarioSnapshot scenario,
        ExecutiveReportRecord? report,
        IReadOnlyList<AlphaInboxItem> inboxItems,
        string message)
    {
        var result = new ExecutiveReportGenerationResult(success, scenario, report, inboxItems, message);
        result.Validate();
        return result;
    }

    private static string ReportId(ExecutiveReportKind kind, NewGmScenarioSnapshot scenario) =>
        $"executive-report:{scenario.Season.SeasonId}:{kind}";

    private static DateTimeOffset ToDateTimeOffset(DateOnly date) =>
        new(date.Year, date.Month, date.Day, 16, 0, 0, TimeSpan.Zero);
}
