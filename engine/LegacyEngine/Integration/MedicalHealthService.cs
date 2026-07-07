using LegacyEngine.Events;
using LegacyEngine.Injuries;
using LegacyEngine.Rosters;
using LegacyEngine.Staff;

namespace LegacyEngine.Integration;

public sealed class MedicalHealthService
{
    public IReadOnlyList<PlayerHealthProfile> BuildHealthProfiles(NewGmScenarioSnapshot scenario) =>
        scenario.AlphaSnapshot.Roster.Players
            .GroupBy(player => player.PersonId, StringComparer.Ordinal)
            .Select(group => BuildHealthProfile(scenario, group.First().PersonId))
            .OrderByDescending(profile => profile.CurrentHealth is HealthStatus.Injured or HealthStatus.HighRisk)
            .ThenByDescending(profile => profile.InjuryRisk)
            .ThenBy(profile => profile.PlayerName, StringComparer.Ordinal)
            .ToArray();

    public PlayerHealthProfile BuildHealthProfile(NewGmScenarioSnapshot scenario, string personId)
    {
        var injuries = InjuriesFor(scenario, personId);
        var active = injuries.FirstOrDefault(injury => injury.IsActive);
        var medicalScore = MedicalDepartmentScore(scenario);
        var age = PersonAge(scenario, personId);
        var wear = Math.Clamp(injuries.Sum(injury => SeverityWear(injury.Severity)) + Math.Max(0, age - 18) * 3, 0, 100);
        var recurrence = Math.Clamp(injuries.Sum(injury => RecurrenceConcern(injury)) / Math.Max(1, injuries.Length), 0, 100);
        var risk = Math.Clamp(25 + wear / 2 + recurrence / 2 + (active?.DevelopmentPenalty ?? 0) - medicalScore / 5, 0, 100);
        var fatigue = active is null ? Math.Clamp(wear / 2, 0, 100) : Math.Clamp(55 + active.DevelopmentPenalty / 2, 0, 100);
        var recoveryRate = Math.Clamp(45 + medicalScore / 2 - wear / 4, 0, 100);
        var durability = Math.Clamp(100 - risk + medicalScore / 8, 0, 100);
        var health = DetermineHealth(active, risk, wear);
        var conditioning = DetermineConditioning(active, fatigue, recoveryRate);
        var previous = injuries
            .OrderByDescending(injury => injury.InjuryDate)
            .Select(injury => $"{injury.InjuryDate:yyyy-MM-dd}: {injury.Severity} {injury.BodyPart} {injury.InjuryType}, {injury.Status}, games missed {injury.GamesMissed}")
            .ToArray();
        var concerns = injuries
            .Where(injury => RecurrenceConcern(injury) >= 45)
            .Select(injury => $"{injury.BodyPart} {injury.InjuryType}: recurrence risk {injury.RecurrenceRisk}, long-term impact {injury.LongTermImpact}")
            .Distinct(StringComparer.Ordinal)
            .DefaultIfEmpty("No major recurring concern.")
            .ToArray();
        var profile = new PlayerHealthProfile(
            personId,
            PersonName(scenario, personId),
            PositionText(scenario, personId),
            health,
            durability,
            fatigue,
            recoveryRate,
            risk,
            wear,
            injuries.Length,
            recurrence,
            Math.Clamp(45 + medicalScore / 2, 0, 100),
            conditioning,
            BuildHealthSummary(active, health, risk, conditioning),
            previous.Length == 0 ? new[] { "No previous injury history." } : previous,
            concerns);
        profile.Validate();
        return profile;
    }

    public MedicalReport BuildMedicalReport(NewGmScenarioSnapshot scenario, string personId)
    {
        var profile = BuildHealthProfile(scenario, personId);
        var active = InjuriesFor(scenario, personId).FirstOrDefault(injury => injury.IsActive);
        var daysRemaining = active is null ? 0 : Math.Max(0, active.ExpectedReturnDate.DayNumber - scenario.CurrentDate.DayNumber);
        var options = AvailableReturnOptions(profile, active, scenario.CurrentDate).ToArray();
        var expectedReturn = active is null
            ? "No active injury; player is available."
            : daysRemaining == 0 ? "Eligible for medical review today." : $"Expected back in {daysRemaining} day(s), around {active.ExpectedReturnDate:yyyy-MM-dd}.";
        var report = new MedicalReport(
            $"medical-report:{personId}:{scenario.CurrentDate:yyyyMMdd}",
            scenario.CurrentDate,
            personId,
            profile.PlayerName,
            profile.Position,
            profile.CurrentHealth,
            profile.Conditioning,
            expectedReturn,
            active is null
                ? $"{profile.PlayerName} is available, but the staff will monitor durability and wear through the month."
                : $"{profile.PlayerName}'s {active.BodyPart} {active.InjuryType} matters because early return can increase recurrence risk and slow development.",
            MedicalStaffComment(scenario, profile, active),
            ReturnRecommendation(profile, active),
            options);
        report.Validate();
        return report;
    }

    public MedicalSummaryReport BuildMedicalSummary(NewGmScenarioSnapshot scenario)
    {
        var profiles = BuildHealthProfiles(scenario);
        var injuries = scenario.AlphaSnapshot.Injuries;
        var significant = injuries
            .OrderByDescending(injury => injury.LongTermImpact + injury.RecurrenceRisk + SeverityWear(injury.Severity))
            .FirstOrDefault();
        var department = new StaffCoachingService().BuildDepartmentGrades(scenario).FirstOrDefault(grade => grade.DepartmentName == "Medical");
        var budget = new BudgetOverviewService().Build(scenario, LegacyEngine.RuleEngine.RulebookPresets.CreateJuniorMajor());
        var summary = new MedicalSummaryReport(
            scenario.CurrentDate,
            injuries.Sum(injury => injury.GamesMissed),
            significant is null ? "None" : $"{PersonName(scenario, significant.PersonId)} - {significant.Severity} {significant.BodyPart} {significant.InjuryType}",
            injuries.Count(injury => injury.IsActive),
            injuries.Count(injury => injury.IsActive && injury.ExpectedReturnDate <= scenario.CurrentDate.AddDays(14)),
            profiles.Count(profile => profile.CurrentHealth == HealthStatus.HighRisk || profile.InjuryRisk >= 70),
            profiles.Count(profile => profile.Conditioning != ConditioningStatus.GameReady),
            department is null ? "Medical: C (baseline)" : $"{department.Grade} ({department.Score}/100)",
            $"Medical/training salaries {budget.MedicalTrainingSalaries:C0}; operations support {budget.MedicalAndStaffOperationsBudget:C0}; budget status {budget.Status}.",
            profiles
                .Where(profile => profile.CurrentHealth is HealthStatus.Injured or HealthStatus.Recovering or HealthStatus.HighRisk)
                .Take(6)
                .Select(profile => $"{profile.PlayerName} ({profile.Position}): {profile.CurrentHealth}, risk {profile.InjuryRisk}/100, conditioning {profile.Conditioning}.")
                .DefaultIfEmpty("No major medical concern.")
                .ToArray());
        summary.Validate();
        return summary;
    }

    public ReturnToPlayDecisionResult ApplyReturnDecision(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        string personId,
        ReturnToPlayOption option)
    {
        var injury = InjuriesFor(scenario, personId).FirstOrDefault(injury => injury.IsActive);
        if (injury is null)
        {
            return Result(false, scenario, personId, option, $"{PersonName(scenario, personId)} has no active injury decision.", Array.Empty<AlphaInboxItem>());
        }

        var updatedInjury = option switch
        {
            ReturnToPlayOption.ReturnImmediately => registry.InjuryEngine.ClearInjury(injury with
            {
                RecurrenceRisk = InjuryRiskProfile.ClampScore(injury.RecurrenceRisk + 15),
                LongTermImpact = InjuryRiskProfile.ClampScore(injury.LongTermImpact + 5)
            }, scenario.CurrentDate).Injury,
            ReturnToPlayOption.MedicalClearance => registry.InjuryEngine.ClearInjury(injury, scenario.CurrentDate).Injury,
            ReturnToPlayOption.LimitedMinutes => injury with
            {
                RecoveryProgress = Math.Max(injury.RecoveryProgress, 85),
                RecurrenceRisk = InjuryRiskProfile.ClampScore(injury.RecurrenceRisk + 5),
                RecoveryPlan = injury.RecoveryPlan with { Summary = "Limited minutes recommended until conditioning is fully restored." }
            },
            ReturnToPlayOption.AdditionalRecovery => injury with
            {
                ExpectedReturnDate = injury.ExpectedReturnDate.AddDays(7),
                RecoveryPlan = injury.RecoveryPlan with
                {
                    ExpectedReturnDate = injury.ExpectedReturnDate.AddDays(7),
                    Summary = "Additional recovery approved to reduce reinjury risk."
                }
            },
            ReturnToPlayOption.ConditioningAssignment => injury with
            {
                RecoveryProgress = Math.Max(injury.RecoveryProgress, 90),
                RecoveryPlan = injury.RecoveryPlan with { Summary = "Conditioning assignment recommended before full game load." }
            },
            _ => injury
        };
        updatedInjury.Validate();

        var injuries = scenario.AlphaSnapshot.Injuries
            .Select(item => item.InjuryId == injury.InjuryId ? updatedInjury : item)
            .ToArray();
        var updatedScenario = scenario with
        {
            AlphaSnapshot = scenario.AlphaSnapshot with { Injuries = injuries }
        };
        updatedScenario = new CareerHistoryService().RecordInjury(updatedScenario, personId, $"{PersonName(updatedScenario, personId)} medical decision: {option}. {updatedInjury.RecoveryPlan.Summary}");
        var report = BuildMedicalReport(updatedScenario, personId);
        var inbox = new[]
        {
            new AlphaInboxItem(
                $"medical-decision:{personId}:{option}:{scenario.CurrentDate:yyyyMMdd}",
                new DateTimeOffset(scenario.CurrentDate.Year, scenario.CurrentDate.Month, scenario.CurrentDate.Day, 9, 0, 0, TimeSpan.Zero),
                option is ReturnToPlayOption.ReturnImmediately or ReturnToPlayOption.MedicalClearance ? LegacyEventType.PlayerRecovered : LegacyEventType.Generic,
                option == ReturnToPlayOption.ReturnImmediately ? LegacyEventSeverity.Warning : LegacyEventSeverity.Notice,
                $"Medical decision: {report.PlayerName} ({report.Position})",
                $"{report.PlayerName} ({report.Position}) - {report.ReturnRecommendation} {report.WhyItMatters}",
                personId)
        };

        return Result(true, updatedScenario, personId, option, $"{report.PlayerName}: {option} applied. {report.ReturnRecommendation}", inbox);
    }

    public IReadOnlyList<string> BuildDossierMedicalLines(NewGmScenarioSnapshot scenario, string personId)
    {
        var profile = BuildHealthProfile(scenario, personId);
        var report = BuildMedicalReport(scenario, personId);
        var lines = new List<string>
        {
            $"Current Health: {profile.CurrentHealth}",
            $"Durability: {profile.Durability}/100; fatigue {profile.Fatigue}/100; recovery rate {profile.RecoveryRate}/100.",
            $"Injury Risk: {profile.InjuryRisk}/100; wear & tear {profile.WearAndTear}/100; recurring risk {profile.RecurringInjuryRisk}/100.",
            $"Conditioning: {profile.Conditioning}; medical confidence {profile.MedicalConfidence}/100.",
            $"Return recommendation: {report.ReturnRecommendation}",
            $"Medical staff comment: {report.StaffComment}",
            $"Why this matters: {report.WhyItMatters}",
            "Previous injuries:"
        };
        lines.AddRange(profile.PreviousInjuries.Select(item => $"  {item}"));
        lines.Add("Recurring concerns:");
        lines.AddRange(profile.RecurringConcerns.Select(item => $"  {item}"));
        return lines;
    }

    private static ReturnToPlayDecisionResult Result(bool success, NewGmScenarioSnapshot scenario, string personId, ReturnToPlayOption option, string message, IReadOnlyList<AlphaInboxItem> inboxItems)
    {
        var result = new ReturnToPlayDecisionResult(success, scenario, personId, option, message, inboxItems);
        result.Validate();
        return result;
    }

    private static IEnumerable<ReturnToPlayOption> AvailableReturnOptions(PlayerHealthProfile profile, Injury? active, DateOnly currentDate)
    {
        if (active is null)
        {
            yield return ReturnToPlayOption.MedicalClearance;
            yield break;
        }

        if (active.RecoveryProgress >= 85 || active.ExpectedReturnDate <= currentDate.AddDays(14))
        {
            yield return ReturnToPlayOption.MedicalClearance;
            yield return ReturnToPlayOption.LimitedMinutes;
            yield return ReturnToPlayOption.ConditioningAssignment;
        }

        yield return ReturnToPlayOption.AdditionalRecovery;
        if (profile.InjuryRisk < 60 && active.RecoveryProgress >= 80)
        {
            yield return ReturnToPlayOption.ReturnImmediately;
        }
    }

    private static HealthStatus DetermineHealth(Injury? active, int risk, int wear) =>
        active is not null
            ? active.Status == InjuryStatus.Recovering ? HealthStatus.Recovering : HealthStatus.Injured
            : risk >= 70 || wear >= 75 ? HealthStatus.HighRisk
            : risk >= 50 ? HealthStatus.Average
            : risk >= 25 ? HealthStatus.Good
            : HealthStatus.Excellent;

    private static ConditioningStatus DetermineConditioning(Injury? active, int fatigue, int recoveryRate) =>
        active is null && fatigue < 45 ? ConditioningStatus.GameReady
        : active is not null && active.RecoveryProgress < 75 ? ConditioningStatus.NotGameReady
        : recoveryRate >= 55 ? ConditioningStatus.LimitedConditioning
        : ConditioningStatus.NotGameReady;

    private static string BuildHealthSummary(Injury? active, HealthStatus health, int risk, ConditioningStatus conditioning) =>
        active is null
            ? $"Current health is {health}; injury risk is {risk}/100 and conditioning is {conditioning}."
            : $"Recovering from {active.Severity} {active.BodyPart} {active.InjuryType}; injury risk is {risk}/100 and conditioning is {conditioning}.";

    private static string MedicalStaffComment(NewGmScenarioSnapshot scenario, PlayerHealthProfile profile, Injury? active)
    {
        var score = MedicalDepartmentScore(scenario);
        var confidence = score >= 70 ? "high" : score >= 55 ? "moderate" : "limited";
        if (active is null)
        {
            return $"Medical staff confidence is {confidence}. Focus is prevention and workload monitoring.";
        }

        var recurrence = active.RecurrenceRisk >= 60 || active.BodyPart is InjuryBodyPart.Knee or InjuryBodyPart.Back or InjuryBodyPart.Head || active.InjuryType == InjuryType.Concussion
            ? "Recurring-injury concern remains elevated."
            : "No major recurring-injury flag beyond normal recovery risk.";
        return $"Medical staff confidence is {confidence}. {recurrence} Recovery progress is {active.RecoveryProgress}/100.";
    }

    private static string ReturnRecommendation(PlayerHealthProfile profile, Injury? active)
    {
        if (active is null)
        {
            return profile.InjuryRisk >= 70 ? "Available, but monitor workload and recovery days." : "Available for normal usage.";
        }

        if (active.RecoveryProgress >= 100 || active.Status == InjuryStatus.Cleared)
        {
            return "Medical clearance is reasonable.";
        }

        if (active.RecoveryProgress >= 85)
        {
            return profile.InjuryRisk >= 65 ? "Use limited minutes or conditioning before full return." : "Limited minutes are reasonable if the GM accepts reinjury risk.";
        }

        return "Additional recovery is recommended before returning to play.";
    }

    private static int MedicalDepartmentScore(NewGmScenarioSnapshot scenario)
    {
        var medicalStaff = scenario.StaffMembers
            .Where(member => member.EmploymentStatus == StaffEmploymentStatus.Employed && member.Department == StaffDepartment.Medical)
            .ToArray();
        if (medicalStaff.Length == 0)
        {
            return 45;
        }

        return Math.Clamp((int)Math.Round(medicalStaff.Average(member =>
            (member.Attributes.MedicalScore(StaffMedicalAttribute.Diagnosis)
                + member.Attributes.MedicalScore(StaffMedicalAttribute.Rehabilitation)
                + member.Attributes.MedicalScore(StaffMedicalAttribute.InjuryPrevention)
                + member.Profile.Reputation) / 4.0)), 0, 100);
    }

    private static Injury[] InjuriesFor(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.Injuries
            .Where(injury => injury.PersonId == personId)
            .OrderByDescending(injury => injury.IsActive)
            .ThenByDescending(injury => injury.InjuryDate)
            .ToArray();

    private static int RecurrenceConcern(Injury injury)
    {
        var bodyPartRisk = injury.BodyPart switch
        {
            InjuryBodyPart.Knee or InjuryBodyPart.Back or InjuryBodyPart.Head or InjuryBodyPart.Neck => 20,
            InjuryBodyPart.Shoulder or InjuryBodyPart.Groin or InjuryBodyPart.Hip => 12,
            _ => 5
        };
        var concussionRisk = injury.InjuryType == InjuryType.Concussion ? 25 : 0;
        return Math.Clamp(injury.RecurrenceRisk + injury.LongTermImpact + bodyPartRisk + concussionRisk, 0, 100);
    }

    private static int SeverityWear(InjurySeverity severity) =>
        severity switch
        {
            InjurySeverity.Minor => 5,
            InjurySeverity.Moderate => 12,
            InjurySeverity.Major => 24,
            InjurySeverity.Severe => 38,
            InjurySeverity.CareerThreatening => 60,
            _ => 0
        };

    private static string PersonName(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? scenario.AlphaSnapshot.Roster.Players.FirstOrDefault(player => player.PersonId == personId)?.PersonId
        ?? personId;

    private static int PersonAge(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.CalculateAge(scenario.CurrentDate)
        ?? scenario.AlphaSnapshot.Roster.Players.FirstOrDefault(player => player.PersonId == personId)?.Age
        ?? 18;

    private static string PositionText(NewGmScenarioSnapshot scenario, string personId)
    {
        var player = scenario.AlphaSnapshot.Roster.Players.FirstOrDefault(player => player.PersonId == personId);
        return player is null ? "Unknown" : PositionShort(player.Position);
    }

    private static string PositionShort(RosterPosition position) =>
        position switch
        {
            RosterPosition.Center => "C",
            RosterPosition.LeftWing => "LW",
            RosterPosition.RightWing => "RW",
            RosterPosition.Defense => "D",
            RosterPosition.Goalie => "G",
            _ => "Unknown"
        };
}
