using LegacyEngine.Events;

namespace LegacyEngine.Injuries;

public sealed class InjuryEngine
{
    private readonly EventEngine _eventEngine;

    public InjuryEngine(EventEngine? eventEngine = null)
    {
        _eventEngine = eventEngine ?? new EventEngine();
    }

    public EventEngine EventEngine => _eventEngine;

    public InjuryResult CreateInjury(
        string personId,
        DateOnly injuryDate,
        InjuryBodyPart bodyPart,
        InjuryType injuryType,
        InjurySeverity severity,
        DateOnly? expectedReturnDate = null,
        int recurrenceRisk = 0,
        int longTermImpact = 0,
        string? injuryId = null)
    {
        var id = injuryId ?? $"injury-{Guid.NewGuid():N}";
        var returnDate = expectedReturnDate ?? injuryDate.AddDays(DefaultRecoveryDays(severity));
        var status = severity == InjurySeverity.CareerThreatening
            ? InjuryStatus.CareerThreatening
            : InjuryStatus.Active;

        var recoveryPlan = new InjuryRecoveryPlan(
            InjuryId: id,
            StartDate: injuryDate,
            ExpectedReturnDate: returnDate,
            Summary: $"Recovery plan for {severity} {injuryType}.");

        var injury = new Injury(
            InjuryId: id,
            PersonId: personId,
            InjuryDate: injuryDate,
            BodyPart: bodyPart,
            InjuryType: injuryType,
            Severity: severity,
            ExpectedReturnDate: returnDate,
            ActualReturnDate: null,
            GamesMissed: 0,
            Status: status,
            LongTermImpact: InjuryRiskProfile.ClampScore(longTermImpact),
            RecurrenceRisk: InjuryRiskProfile.ClampScore(recurrenceRisk),
            RecoveryProgress: 0,
            RecoveryPlan: recoveryPlan);

        injury.Validate();

        var events = new List<LegacyEvent>
        {
            QueueInjuryEvent(
                injury,
                injuryDate,
                LegacyEventType.PlayerInjured,
                LegacyEventSeverity.Warning,
                "Player injured",
                $"Player suffered a {severity} {injuryType}.")
        };

        if (severity == InjurySeverity.CareerThreatening)
        {
            events.Add(QueueInjuryEvent(
                injury,
                injuryDate,
                LegacyEventType.InjuryCareerThreatening,
                LegacyEventSeverity.Critical,
                "Career-threatening injury",
                "Player injury was marked career-threatening."));
        }

        return new InjuryResult(true, injury, BuildSummary(injury), events);
    }

    public InjuryResult ApplyRecoveryUpdate(Injury injury, InjuryRecoveryUpdate update)
    {
        injury.Validate();
        update.Validate();

        var status = update.Status ?? (injury.Status == InjuryStatus.Active ? InjuryStatus.Recovering : injury.Status);
        var updated = injury with
        {
            GamesMissed = injury.GamesMissed + update.GamesMissedIncrease,
            RecoveryProgress = InjuryRiskProfile.ClampScore(injury.RecoveryProgress + update.RecoveryProgressDelta),
            RecurrenceRisk = InjuryRiskProfile.ClampScore(injury.RecurrenceRisk + update.RecurrenceRiskDelta),
            LongTermImpact = InjuryRiskProfile.ClampScore(injury.LongTermImpact + update.LongTermImpactDelta),
            Status = status
        };

        if (updated.RecoveryProgress >= 100 || status == InjuryStatus.Cleared)
        {
            return ClearInjury(updated, update.Date);
        }

        updated.Validate();
        return new InjuryResult(true, updated, BuildSummary(updated, update.Notes), Array.Empty<LegacyEvent>());
    }

    public InjuryResult ClearInjury(Injury injury, DateOnly actualReturnDate)
    {
        injury.Validate();

        var cleared = injury with
        {
            ActualReturnDate = actualReturnDate,
            Status = InjuryStatus.Cleared,
            RecoveryProgress = 100
        };
        cleared.Validate();

        var legacyEvent = QueueInjuryEvent(
            cleared,
            actualReturnDate,
            LegacyEventType.PlayerRecovered,
            LegacyEventSeverity.Notice,
            "Player recovered",
            "Player was cleared from injury.");

        return new InjuryResult(true, cleared, BuildSummary(cleared), new[] { legacyEvent });
    }

    public InjuryResult ReAggravateInjury(
        Injury injury,
        DateOnly date,
        int recurrenceRiskIncrease = 10,
        int longTermImpactIncrease = 5)
    {
        injury.Validate();

        var reAggravated = injury with
        {
            Status = InjuryStatus.ReAggravated,
            ActualReturnDate = null,
            RecoveryProgress = Math.Min(injury.RecoveryProgress, 50),
            RecurrenceRisk = InjuryRiskProfile.ClampScore(injury.RecurrenceRisk + recurrenceRiskIncrease),
            LongTermImpact = InjuryRiskProfile.ClampScore(injury.LongTermImpact + longTermImpactIncrease),
            ExpectedReturnDate = date > injury.ExpectedReturnDate ? date.AddDays(DefaultRecoveryDays(injury.Severity) / 2) : injury.ExpectedReturnDate
        };
        reAggravated.Validate();

        var legacyEvent = QueueInjuryEvent(
            reAggravated,
            date,
            LegacyEventType.InjuryReAggravated,
            LegacyEventSeverity.Warning,
            "Injury re-aggravated",
            "Player injury was re-aggravated.");

        return new InjuryResult(true, reAggravated, BuildSummary(reAggravated), new[] { legacyEvent });
    }

    public InjuryResult MarkCareerThreatening(Injury injury, DateOnly date, int longTermImpactIncrease = 30)
    {
        injury.Validate();

        var careerThreatening = injury with
        {
            Severity = InjurySeverity.CareerThreatening,
            Status = InjuryStatus.CareerThreatening,
            LongTermImpact = InjuryRiskProfile.ClampScore(injury.LongTermImpact + longTermImpactIncrease),
            RecurrenceRisk = InjuryRiskProfile.ClampScore(Math.Max(injury.RecurrenceRisk, 75))
        };
        careerThreatening.Validate();

        var legacyEvent = QueueInjuryEvent(
            careerThreatening,
            date,
            LegacyEventType.InjuryCareerThreatening,
            LegacyEventSeverity.Critical,
            "Career-threatening injury",
            "Player injury was marked career-threatening.");

        return new InjuryResult(true, careerThreatening, BuildSummary(careerThreatening), new[] { legacyEvent });
    }

    public InjuryRiskProfile CreateRiskProfile(Injury injury)
    {
        injury.Validate();

        var profile = new InjuryRiskProfile(
            PersonId: injury.PersonId,
            BaseRisk: injury.Severity switch
            {
                InjurySeverity.Minor => 10,
                InjurySeverity.Moderate => 25,
                InjurySeverity.Major => 45,
                InjurySeverity.Severe => 65,
                InjurySeverity.CareerThreatening => 90,
                _ => 0
            },
            RecurrenceRisk: injury.RecurrenceRisk,
            LongTermImpact: injury.LongTermImpact);

        profile.Validate();
        return profile;
    }

    private LegacyEvent QueueInjuryEvent(
        Injury injury,
        DateOnly date,
        LegacyEventType eventType,
        LegacyEventSeverity severity,
        string title,
        string description)
    {
        var legacyEvent = _eventEngine.CreateEvent(
            new DateTimeOffset(date.Year, date.Month, date.Day, 12, 0, 0, TimeSpan.Zero),
            eventType,
            severity,
            LegacyEventVisibility.Organization,
            title,
            description,
            new LegacyEventContext(PrimaryPersonId: injury.PersonId),
            new Dictionary<string, object?>
            {
                ["injury_id"] = injury.InjuryId,
                ["injury_type"] = injury.InjuryType.ToString(),
                ["body_part"] = injury.BodyPart.ToString(),
                ["severity"] = injury.Severity.ToString(),
                ["status"] = injury.Status.ToString()
            });

        return _eventEngine.QueueEvent(legacyEvent);
    }

    private static string BuildSummary(Injury injury, string? notes = null)
    {
        var summary = $"Injury update: {injury.Severity} {injury.InjuryType} to {injury.BodyPart}; status {injury.Status}; expected return {injury.ExpectedReturnDate:yyyy-MM-dd}.";
        return string.IsNullOrWhiteSpace(notes) ? summary : $"{summary} {notes}";
    }

    private static int DefaultRecoveryDays(InjurySeverity severity) => severity switch
    {
        InjurySeverity.Minor => 7,
        InjurySeverity.Moderate => 21,
        InjurySeverity.Major => 60,
        InjurySeverity.Severe => 120,
        InjurySeverity.CareerThreatening => 240,
        _ => 14
    };
}
