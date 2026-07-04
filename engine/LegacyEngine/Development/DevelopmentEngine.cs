using LegacyEngine.Events;

namespace LegacyEngine.Development;

public sealed class DevelopmentEngine
{
    private readonly EventEngine _eventEngine;

    public DevelopmentEngine(EventEngine? eventEngine = null)
    {
        _eventEngine = eventEngine ?? new EventEngine();
    }

    public EventEngine EventEngine => _eventEngine;

    public PlayerDevelopmentProfile CreateProfile(
        string personId,
        int currentAbility,
        int potential,
        DevelopmentStage stage,
        IReadOnlyList<DevelopmentTrait>? traits = null,
        DateOnly? lastUpdated = null)
    {
        var profile = new PlayerDevelopmentProfile(
            PersonId: personId,
            CurrentAbility: currentAbility,
            Potential: potential,
            Stage: stage,
            Traits: traits ?? CreateDefaultTraits(),
            LastUpdated: lastUpdated ?? DateOnly.FromDateTime(DateTime.UtcNow));

        profile.Validate();
        return profile;
    }

    public DevelopmentResult ApplyMonthlyUpdate(
        PlayerDevelopmentProfile profile,
        DevelopmentFactor factors)
    {
        profile.Validate();
        factors.Validate();

        var monthlyMomentum = CalculateMonthlyMomentum(profile, factors);
        var updates = new List<DevelopmentUpdate>();
        var updatedTraits = new List<DevelopmentTrait>();

        foreach (var trait in profile.Traits)
        {
            var traitDelta = CalculateTraitDelta(trait.Attribute, monthlyMomentum, factors.InjuryPenalty ?? 0);
            var updatedTrait = trait.Change(traitDelta);
            updatedTraits.Add(updatedTrait);

            if (updatedTrait.Value != trait.Value)
            {
                updates.Add(new DevelopmentUpdate(
                    Date: factors.UpdateDate,
                    Attribute: trait.Attribute,
                    PreviousValue: trait.Value,
                    NewValue: updatedTrait.Value));
            }
        }

        var currentAbilityChange = CalculateAbilityChange(profile, factors, monthlyMomentum);
        var uncappedAbility = profile.CurrentAbility + currentAbilityChange;
        var cappedAbility = Math.Clamp(uncappedAbility, 0, profile.Potential);
        currentAbilityChange = cappedAbility - profile.CurrentAbility;

        var updatedProfile = profile with
        {
            CurrentAbility = cappedAbility,
            Traits = updatedTraits,
            LastUpdated = factors.UpdateDate
        };
        updatedProfile.Validate();

        var summary = BuildSummary(currentAbilityChange, profile.Stage, factors);
        var playerFacingSummary = BuildPlayerFacingSummary(currentAbilityChange);
        var events = QueueEvents(updatedProfile, factors.UpdateDate, currentAbilityChange, summary);

        return new DevelopmentResult(
            PersonId: profile.PersonId,
            UpdateDate: factors.UpdateDate,
            UpdatedProfile: updatedProfile,
            Updates: updates,
            CurrentAbilityChange: currentAbilityChange,
            Summary: summary,
            PlayerFacingSummary: playerFacingSummary,
            Events: events);
    }

    public string CreateDossierDevelopmentSummary(DevelopmentResult result) => result.PlayerFacingSummary;

    private static IReadOnlyList<DevelopmentTrait> CreateDefaultTraits() =>
        Enum.GetValues<DevelopmentAttribute>()
            .Select(attribute => new DevelopmentTrait(attribute, 50))
            .ToArray();

    private static double CalculateMonthlyMomentum(PlayerDevelopmentProfile profile, DevelopmentFactor factors)
    {
        var workEthic = profile.TraitValue(DevelopmentAttribute.WorkEthic);
        var coachability = profile.TraitValue(DevelopmentAttribute.Coachability);
        var confidence = profile.TraitValue(DevelopmentAttribute.Confidence);
        var iceTime = factors.IceTimeScore ?? 50;
        var facility = factors.FacilityBonus ?? 0;
        var coaching = factors.CoachingBonus ?? 0;
        var injury = factors.InjuryPenalty ?? 0;

        return AgeCurve(factors.Age)
            + StageCurve(profile.Stage)
            + ((workEthic - 50) / 16.0)
            + ((coachability - 50) / 20.0)
            + ((confidence - 50) / 24.0)
            + ((iceTime - 50) / 25.0)
            + (facility / 22.0)
            + (coaching / 22.0)
            - (injury / 16.0)
            + (factors.RandomModifier / 5.0);
    }

    private static int CalculateTraitDelta(DevelopmentAttribute attribute, double monthlyMomentum, int injuryPenalty)
    {
        var adjustment = attribute switch
        {
            DevelopmentAttribute.WorkEthic => -0.35,
            DevelopmentAttribute.Coachability => -0.35,
            DevelopmentAttribute.Confidence => injuryPenalty > 0 ? -0.15 : 0.15,
            DevelopmentAttribute.Physicality => -0.05,
            _ => 0
        };

        return Math.Clamp((int)Math.Round((monthlyMomentum / 3.0) + adjustment, MidpointRounding.AwayFromZero), -3, 3);
    }

    private static int CalculateAbilityChange(PlayerDevelopmentProfile profile, DevelopmentFactor factors, double monthlyMomentum)
    {
        var rawChange = (monthlyMomentum / 2.0) + AbilityStageAdjustment(profile.Stage);
        if (profile.Stage is DevelopmentStage.Veteran or DevelopmentStage.Declining && factors.InjuryPenalty.GetValueOrDefault() >= 40)
        {
            rawChange -= 1.5;
        }

        return Math.Clamp((int)Math.Round(rawChange, MidpointRounding.AwayFromZero), -5, 6);
    }

    private IReadOnlyList<LegacyEvent> QueueEvents(
        PlayerDevelopmentProfile profile,
        DateOnly updateDate,
        int currentAbilityChange,
        string summary)
    {
        var events = new List<LegacyEvent>
        {
            QueueDevelopmentEvent(
                profile,
                updateDate,
                LegacyEventType.PlayerDevelopmentUpdated,
                LegacyEventSeverity.Notice,
                "Player development updated",
                summary,
                currentAbilityChange)
        };

        if (currentAbilityChange >= 4)
        {
            events.Add(QueueDevelopmentEvent(
                profile,
                updateDate,
                LegacyEventType.PlayerBreakout,
                LegacyEventSeverity.Warning,
                "Player breakout",
                "A player made a meaningful development jump.",
                currentAbilityChange));
        }
        else if (currentAbilityChange <= -3)
        {
            events.Add(QueueDevelopmentEvent(
                profile,
                updateDate,
                LegacyEventType.PlayerRegression,
                LegacyEventSeverity.Warning,
                "Player regression",
                "A player showed significant regression.",
                currentAbilityChange));
        }

        return events;
    }

    private LegacyEvent QueueDevelopmentEvent(
        PlayerDevelopmentProfile profile,
        DateOnly updateDate,
        LegacyEventType eventType,
        LegacyEventSeverity severity,
        string title,
        string description,
        int currentAbilityChange)
    {
        var legacyEvent = _eventEngine.CreateEvent(
            new DateTimeOffset(updateDate.Year, updateDate.Month, updateDate.Day, 12, 0, 0, TimeSpan.Zero),
            eventType,
            severity,
            LegacyEventVisibility.Internal,
            title,
            description,
            new LegacyEventContext(PrimaryPersonId: profile.PersonId),
            new Dictionary<string, object?>
            {
                ["stage"] = profile.Stage.ToString(),
                ["current_ability_change"] = currentAbilityChange
            });

        return _eventEngine.QueueEvent(legacyEvent);
    }

    private static string BuildSummary(int currentAbilityChange, DevelopmentStage stage, DevelopmentFactor factors)
    {
        var direction = currentAbilityChange switch
        {
            >= 4 => "breakout month",
            > 0 => "positive month",
            0 => "steady month",
            <= -3 => "significant regression",
            _ => "minor regression"
        };

        return $"Development update: {direction} for a {stage} player at age {factors.Age}.";
    }

    private static string BuildPlayerFacingSummary(int currentAbilityChange) =>
        currentAbilityChange switch
        {
            >= 4 => "Reports suggest a noticeable step forward this month.",
            > 0 => "Reports suggest steady progress this month.",
            0 => "Reports suggest the player held steady this month.",
            <= -3 => "Reports suggest a concerning step back this month.",
            _ => "Reports suggest a slight dip this month."
        };

    private static double AgeCurve(int age) => age switch
    {
        <= 17 => 1.5,
        <= 20 => 1.25,
        <= 24 => 0.75,
        <= 28 => 0,
        <= 32 => -0.75,
        _ => -1.5
    };

    private static double StageCurve(DevelopmentStage stage) => stage switch
    {
        DevelopmentStage.Prospect => 1.25,
        DevelopmentStage.Junior => 1,
        DevelopmentStage.YoungPro => 0.5,
        DevelopmentStage.Prime => 0,
        DevelopmentStage.Veteran => -1,
        DevelopmentStage.Declining => -2,
        _ => 0
    };

    private static double AbilityStageAdjustment(DevelopmentStage stage) => stage switch
    {
        DevelopmentStage.Prospect => 0.25,
        DevelopmentStage.Junior => 0.25,
        DevelopmentStage.YoungPro => 0,
        DevelopmentStage.Prime => -0.25,
        DevelopmentStage.Veteran => -0.75,
        DevelopmentStage.Declining => -1.25,
        _ => 0
    };
}
