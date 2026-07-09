using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;
using LegacyEngine.Scouting;
using LegacyEngine.Staff;
using LegacyEngine.Draft;

namespace LegacyEngine.Integration;

public sealed partial class ScoutingIntelligenceService
{
    public IReadOnlyList<ScoutIntelligenceProfile> BuildScoutProfiles(NewGmScenarioSnapshot scenario, Rulebook? rulebook = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        return ScoutStaff(scenario)
            .Select(member => BuildScoutProfile(scenario, member, rulebook))
            .ToArray();
    }

    public ScoutIntelligenceProfile BuildScoutProfile(NewGmScenarioSnapshot scenario, StaffMember scout, Rulebook? rulebook = null)
    {
        var traits = BuildTraits(scout).ToArray();
        var regions = BuildKnownRegions(scout).ToArray();
        var specialties = BuildSpecialties(scout).ToArray();
        var workload = scenario.ScoutingOperations.Count(assignment => assignment.ScoutPersonId == scout.PersonId && assignment.IsOpen);
        var completed = scenario.CompletedScoutingReports.Count(report => report.ScoutId == scout.PersonId);
        var budget = BuildBudgetImpact(scenario, rulebook);
        var summary = $"{PersonName(scenario, scout.PersonId)} is a {scout.CurrentRole} with {string.Join(", ", traits.Take(3))} tendencies. Best regions: {string.Join(", ", regions.Take(2))}.";
        var profile = new ScoutIntelligenceProfile(
            scout.PersonId,
            PersonName(scenario, scout.PersonId),
            scout.CurrentRole.ToString(),
            traits,
            regions,
            specialties,
            scout.Profile.Reputation,
            completed * 12 + scout.PerformanceHistory.Count * 8,
            workload,
            budget.TravelCoverage,
            summary);
        profile.Validate();
        return profile;
    }

    public ScoutingIntelligenceReport CreateReport(
        NewGmScenarioSnapshot scenario,
        string playerId,
        string scoutId,
        int viewings,
        ScoutingRegionFocus region,
        ScoutingViewingType viewingType,
        ScoutingTournamentType? tournament = null,
        Rulebook? rulebook = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        if (viewings <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(viewings), "Scouting viewings must be positive.");
        }

        var scout = ScoutStaff(scenario).FirstOrDefault(member => member.PersonId == scoutId)
            ?? throw new ArgumentException("Scout was not found.", nameof(scoutId));
        var profile = BuildScoutProfile(scenario, scout, rulebook);
        var playerName = PersonName(scenario, playerId);
        var boardEntry = scenario.AlphaSnapshot.DraftBoard.Entries.FirstOrDefault(entry => entry.ProspectPersonId == playerId);
        var position = boardEntry?.Bio?.Position ?? scenario.AlphaSnapshot.Roster.FindPlayer(playerId)?.Position ?? RosterPosition.Unknown;
        var source = ReportSourceFor(scout.CurrentRole, region);
        var budget = BuildBudgetImpact(scenario, rulebook);
        var confidence = ToConfidence(ConfidenceScore(profile, scout, viewings, region, viewingType, budget));
        var report = new ScoutingIntelligenceReport(
            ReportId: $"intel-report:{scoutId}:{playerId}:{scenario.CurrentDate:yyyyMMdd}:{viewings}:{viewingType}",
            PlayerId: playerId,
            PlayerName: playerName,
            ScoutId: scoutId,
            ScoutName: profile.Name,
            ScoutRole: profile.Role,
            CreatedOn: scenario.CurrentDate,
            Source: source,
            ViewingType: viewingType,
            Tournament: tournament,
            Region: region,
            Viewings: viewings,
            Confidence: confidence,
            ConfidenceStars: ConfidenceStars(confidence),
            CurrentPicture: BuildCurrentPicture(playerName, position, boardEntry, profile, viewingType),
            FutureProjection: BuildFutureProjection(position, boardEntry, profile, confidence),
            Recommendation: BuildRecommendation(boardEntry, profile, confidence, viewingType),
            Evidence: BuildEvidence(position, boardEntry, profile, viewings, viewingType, tournament),
            Concerns: BuildConcerns(position, boardEntry, profile, confidence, region),
            Unknowns: BuildUnknowns(confidence, viewingType),
            ScoutTraits: profile.Traits,
            WorkloadNote: profile.Workload >= 3 ? "Workload is heavy; detail and timing may suffer." : "Workload is manageable for a focused report.",
            BudgetNote: BudgetNoteFor(region, viewingType, budget));
        report.Validate();
        return report;
    }

    public IReadOnlyList<ScoutingIntelligenceReport> BuildReportCards(NewGmScenarioSnapshot scenario, string playerId, Rulebook? rulebook = null)
    {
        var converted = scenario.CompletedScoutingReports
            .Where(report => report.PlayerId == playerId)
            .OrderByDescending(report => report.CreatedOn)
            .Select(report => ConvertCompletedReport(scenario, report, rulebook))
            .ToList();

        var scouts = ScoutStaff(scenario).Take(2).ToArray();
        if (converted.Count == 0 && scouts.Length > 0)
        {
            converted.Add(CreateReport(scenario, playerId, scouts[0].PersonId, 3, RegionForPlayer(scenario, playerId), ScoutingViewingType.FiveGameSample, rulebook: rulebook));
        }

        return converted;
    }

    public ScoutingReportComparison CompareReports(NewGmScenarioSnapshot scenario, string playerId, Rulebook? rulebook = null)
    {
        var reports = BuildReportCards(scenario, playerId, rulebook).ToArray();
        if (reports.Length < 2)
        {
            var scout = ScoutStaff(scenario).Skip(1).FirstOrDefault() ?? ScoutStaff(scenario).FirstOrDefault();
            if (scout is not null)
            {
                reports = reports.Append(CreateReport(scenario, playerId, scout.PersonId, 7, RegionForPlayer(scenario, playerId), ScoutingViewingType.FiveGameSample, rulebook: rulebook)).ToArray();
            }
        }

        var agreements = new List<string>
        {
            reports.All(report => report.Evidence.Any(evidence => evidence.Contains("position", StringComparison.OrdinalIgnoreCase)))
                ? "Reports agree on the basic position and role context."
                : "Reports agree that basic identity is known."
        };
        var recommendationGroups = reports.Select(report => report.Recommendation).Distinct(StringComparer.Ordinal).ToArray();
        var disagreements = recommendationGroups.Length > 1
            ? new[] { $"Recommendation split: {string.Join(" / ", recommendationGroups)}." }
            : new[] { "No major recommendation split yet; disagreement is mostly in emphasis and confidence." };
        var confidenceSummary = $"Confidence range: {string.Join(", ", reports.Select(report => $"{report.ScoutName} {report.ConfidenceStars}"))}.";
        var recommendationSummary = recommendationGroups.Length == 1
            ? $"Staff broadly lean {recommendationGroups[0]}."
            : "Staff opinions are split; another viewing or specialist report would help.";
        var comparison = new ScoutingReportComparison(playerId, PersonName(scenario, playerId), reports, agreements, disagreements, confidenceSummary, recommendationSummary);
        comparison.Validate();
        return comparison;
    }

    public ScoutCareerSnapshot BuildScoutCareer(NewGmScenarioSnapshot scenario, string scoutId)
    {
        var scout = ScoutStaff(scenario).FirstOrDefault(member => member.PersonId == scoutId)
            ?? throw new ArgumentException("Scout was not found.", nameof(scoutId));
        var reports = scenario.CompletedScoutingReports.Where(report => report.ScoutId == scoutId).ToArray();
        var discoveries = reports
            .GroupBy(report => report.PlayerId, StringComparer.Ordinal)
            .Select(group =>
            {
                var playerId = group.Key;
                var draft = scenario.DraftPickHistory.FirstOrDefault(pick => pick.PlayerPersonId == playerId);
                return new ScoutDiscovery(
                    playerId,
                    PersonName(scenario, playerId),
                    draft?.Year ?? scenario.Season.Year,
                    draft is null ? "Tracked prospect" : $"Drafted round {draft.Round}, pick {draft.OverallPick}",
                    draft?.OutcomeSummary ?? "Scouting staff began tracking this player.");
            })
            .Take(8)
            .ToArray();
        var profile = BuildScoutProfile(scenario, scout);
        var snapshot = new ScoutCareerSnapshot(
            scoutId,
            profile.Name,
            discoveries,
            profile.ExperiencePoints + reports.Length * 10,
            reports.Length >= 6 ? "Reputation trending upward through completed reports." : "Reputation still forming.",
            profile.Specialties.Select(specialty => specialty.ToString()).Concat(profile.KnownRegions.Select(region => region.ToString())).Distinct().Take(5).ToArray(),
            discoveries.Length == 0
                ? $"{profile.Name} has not been credited with a notable discovery yet."
                : $"{profile.Name} has {discoveries.Length} tracked discovery/discoveries.");
        snapshot.Validate();
        return snapshot;
    }

    public ScoutDevelopmentUpdate BuildScoutDevelopment(NewGmScenarioSnapshot scenario, string scoutId)
    {
        var career = BuildScoutCareer(scenario, scoutId);
        var specialization = career.Specializations.FirstOrDefault() ?? "General scouting";
        var update = new ScoutDevelopmentUpdate(
            scoutId,
            career.ScoutName,
            Math.Max(4, career.DiscoveredPlayers.Count * 6 + career.ExperiencePoints / 10),
            career.ExperiencePoints >= 80 ? "Reputation improving with a stronger evidence trail." : "Reputation steady; needs more completed viewings.",
            specialization,
            $"{career.ScoutName} is building experience through completed reports and may become stronger in {specialization}.");
        update.Validate();
        return update;
    }

    public ScoutingBudgetImpact BuildBudgetImpact(NewGmScenarioSnapshot scenario, Rulebook? rulebook = null)
    {
        var budget = new BudgetOverviewService().Build(scenario, rulebook ?? RulebookPresets.CreateJuniorMajor());
        var scouting = budget.ScoutingBudget;
        var impact = new ScoutingBudgetImpact(
            scouting,
            scouting >= 70_000m ? "Travel coverage supports longer trips and more live viewings." : "Travel coverage is selective; long assignments need prioritization.",
            scouting >= 90_000m ? "Tournament coverage can include major showcase events." : "Tournament coverage should be reserved for priority targets.",
            scouting >= 110_000m ? "International scouting is viable for Europe and cross-border tournaments." : "International coverage is limited and may lower confidence outside core regions.",
            budget.Status == BudgetStatus.OverBudget ? "Owner will want scouting spend justified by actionable reports." : "Owner is comfortable when scouting spend supports draft and roster decisions.");
        impact.Validate();
        return impact;
    }

    private ScoutingIntelligenceReport ConvertCompletedReport(NewGmScenarioSnapshot scenario, ScoutingReport report, Rulebook? rulebook)
    {
        var assignment = scenario.ScoutingOperations.FirstOrDefault(assignment => assignment.AssignmentId == report.AssignmentId);
        var region = assignment?.TargetRegion ?? RegionForPlayer(scenario, report.PlayerId);
        var scout = ScoutStaff(scenario).FirstOrDefault(member => member.PersonId == report.ScoutId);
        if (scout is null)
        {
            return CreateFallbackReport(scenario, report, region, rulebook);
        }

        var viewings = Math.Max(1, assignment?.DurationDays ?? 3);
        return CreateReport(
            scenario,
            report.PlayerId,
            report.ScoutId,
            viewings,
            region,
            viewings >= 12 ? ScoutingViewingType.FifteenGameSample : viewings >= 5 ? ScoutingViewingType.FiveGameSample : ScoutingViewingType.SingleGame,
            rulebook: rulebook) with
        {
            ReportId = report.ReportId,
            CreatedOn = report.CreatedOn,
            Confidence = report.Confidence,
            ConfidenceStars = ConfidenceStars(report.Confidence),
            Evidence = report.Observations.Take(3).Concat(report.Facts.Take(2)).ToArray(),
            Concerns = report.Unknowns.Take(3).ToArray()
        };
    }

    private ScoutingIntelligenceReport CreateFallbackReport(NewGmScenarioSnapshot scenario, ScoutingReport report, ScoutingRegionFocus region, Rulebook? rulebook)
    {
        var scout = ScoutStaff(scenario).First();
        return CreateReport(scenario, report.PlayerId, scout.PersonId, 3, region, ScoutingViewingType.FiveGameSample, rulebook: rulebook) with
        {
            ReportId = report.ReportId,
            ScoutId = report.ScoutId,
            ScoutName = report.ScoutId,
            CreatedOn = report.CreatedOn,
            Confidence = report.Confidence,
            ConfidenceStars = ConfidenceStars(report.Confidence)
        };
    }

    private static int ConfidenceScore(ScoutIntelligenceProfile profile, StaffMember scout, int viewings, ScoutingRegionFocus region, ScoutingViewingType type, ScoutingBudgetImpact budget)
    {
        var viewingScore = viewings switch
        {
            >= 15 => 38,
            >= 5 => 24,
            >= 2 => 13,
            _ => 5
        };
        var regionScore = profile.KnownRegions.Contains(region) ? 18 : -8;
        var specialtyScore = SpecialtyFits(profile, region) ? 14 : 0;
        var typeScore = type is ScoutingViewingType.Tournament or ScoutingViewingType.Playoffs ? 8 : 0;
        var workloadPenalty = profile.Workload >= 3 ? 20 : profile.Workload == 2 ? 9 : 0;
        var budgetScore = budget.InternationalCoverage.StartsWith("International scouting is viable", StringComparison.Ordinal) || region is not ScoutingRegionFocus.Europe ? 7 : -6;
        var reputationScore = scout.Profile.Reputation / 10;
        return Math.Clamp(10 + viewingScore + regionScore + specialtyScore + typeScore + budgetScore + reputationScore - workloadPenalty, 0, 100);
    }

    private static ScoutingConfidenceLevel ToConfidence(int score) =>
        score switch
        {
            >= 84 => ScoutingConfidenceLevel.VeryHigh,
            >= 66 => ScoutingConfidenceLevel.High,
            >= 42 => ScoutingConfidenceLevel.Medium,
            >= 18 => ScoutingConfidenceLevel.Low,
            _ => ScoutingConfidenceLevel.Unknown
        };

    public static string ConfidenceStars(ScoutingConfidenceLevel confidence) =>
        confidence switch
        {
            ScoutingConfidenceLevel.VeryHigh => "***** Very High",
            ScoutingConfidenceLevel.High => "**** High",
            ScoutingConfidenceLevel.Medium => "*** Medium",
            ScoutingConfidenceLevel.Low => "** Low",
            _ => "* Very Low"
        };

    private static string BuildCurrentPicture(string playerName, RosterPosition position, DraftBoardEntry? entry, ScoutIntelligenceProfile profile, ScoutingViewingType type)
    {
        var baseText = entry is null
            ? $"{profile.Name}'s view: {playerName} has enough basic information to describe him as a {PositionText(position)}."
            : $"{profile.Name}'s view: {playerName} is currently viewed as a {PositionText(position)} from {entry.Bio?.CurrentTeam ?? "his current club"}.";
        if (profile.Traits.Contains(ScoutPersonalityTrait.Conservative))
        {
            return $"{baseText} Scout is cautious about declaring readiness until the viewing sample grows.";
        }

        if (profile.Traits.Contains(ScoutPersonalityTrait.Optimistic))
        {
            return $"{baseText} Scout sees encouraging present tools and wants the club to keep tracking momentum.";
        }

        return $"{baseText} {type} evidence gives staff a clearer hockey read without revealing hidden ratings.";
    }

    private static string BuildFutureProjection(RosterPosition position, DraftBoardEntry? entry, ScoutIntelligenceProfile profile, ScoutingConfidenceLevel confidence)
    {
        var role = entry?.Bio?.PotentialLineupProjection ?? $"future {PositionText(position)} role";
        if (profile.Traits.Contains(ScoutPersonalityTrait.PoorAtProjectingCeiling))
        {
            return $"Future projection is intentionally conservative: {role}, but ceiling needs another opinion.";
        }

        if (profile.Traits.Contains(ScoutPersonalityTrait.FindsSleepers))
        {
            return $"Future projection: possible sleeper path toward {role} if development and opportunity align.";
        }

        return confidence >= ScoutingConfidenceLevel.High
            ? $"Future projection: {role} with enough viewings to trust the broad direction."
            : $"Future projection: {role}, but confidence is still building.";
    }

    private static string BuildRecommendation(DraftBoardEntry? entry, ScoutIntelligenceProfile profile, ScoutingConfidenceLevel confidence, ScoutingViewingType viewingType)
    {
        if (viewingType is ScoutingViewingType.Tournament or ScoutingViewingType.Playoffs)
        {
            return "Use as a pressure-performance data point, not a final answer.";
        }

        if (entry?.Rank <= 8 && confidence >= ScoutingConfidenceLevel.Medium)
        {
            return profile.Traits.Contains(ScoutPersonalityTrait.Conservative) ? "Target, but confirm with one more viewing." : "Priority target.";
        }

        return confidence >= ScoutingConfidenceLevel.High ? "Target if roster/pathway fit is clean." : "Watch and scout again.";
    }

    private static IReadOnlyList<string> BuildEvidence(RosterPosition position, DraftBoardEntry? entry, ScoutIntelligenceProfile profile, int viewings, ScoutingViewingType type, ScoutingTournamentType? tournament)
    {
        var evidence = new List<string>
        {
            $"{viewings} viewing(s) create a {type} evidence base.",
            $"Basic position evidence: {PositionText(position)}.",
            entry?.AnalyticsSummary ?? "Analytics evidence is not available yet."
        };
        if (tournament is not null)
        {
            evidence.Add($"{tournament} viewing adds pressure, leadership, consistency, and big-game performance context.");
        }

        if (!string.IsNullOrWhiteSpace(entry?.ClassContextNote))
        {
            evidence.Add($"Draft class context: {entry.ClassContextNote}");
        }

        if (profile.Traits.Contains(ScoutPersonalityTrait.ExcellentWithGoalies) && position == RosterPosition.Goalie)
        {
            evidence.Add("Goalie specialist note: reads tracking, recovery, and composure better than a general report.");
        }

        if (profile.Traits.Contains(ScoutPersonalityTrait.ExcellentWithDefensemen) && position == RosterPosition.Defense)
        {
            evidence.Add("Defense specialist note: stronger read on gap control, retrievals, and decision pace.");
        }

        return evidence;
    }

    private static IReadOnlyList<string> BuildConcerns(RosterPosition position, DraftBoardEntry? entry, ScoutIntelligenceProfile profile, ScoutingConfidenceLevel confidence, ScoutingRegionFocus region)
    {
        var concerns = new List<string>();
        if (confidence <= ScoutingConfidenceLevel.Low)
        {
            concerns.Add("Low confidence: staff need more live viewings before changing the board.");
        }

        if (profile.Traits.Contains(ScoutPersonalityTrait.UnderestimatesEuropeans) && region == ScoutingRegionFocus.Europe)
        {
            concerns.Add("Scout tendency warning: this scout may underweight European context.");
        }

        if (profile.Traits.Contains(ScoutPersonalityTrait.LovesSize))
        {
            concerns.Add("Scout tendency warning: size may be carrying extra weight in the opinion.");
        }

        if (entry?.Bio?.CharacterSummary is { Length: > 0 } character)
        {
            concerns.Add($"Character context: {character}");
        }

        if (!string.IsNullOrWhiteSpace(entry?.RiskSummary))
        {
            concerns.Add($"Class risk: {entry.RiskSummary}");
        }

        if (concerns.Count == 0)
        {
            concerns.Add($"No major concern beyond normal projection risk for a {PositionText(position)}.");
        }

        return concerns;
    }

    private static IReadOnlyList<string> BuildUnknowns(ScoutingConfidenceLevel confidence, ScoutingViewingType type)
    {
        var unknowns = new List<string>
        {
            "Long-term development path still depends on coaching, health, confidence, and opportunity."
        };
        if (confidence < ScoutingConfidenceLevel.High)
        {
            unknowns.Add("Current and future role need a larger viewing sample.");
        }

        if (type == ScoutingViewingType.SingleGame)
        {
            unknowns.Add("Single-game viewing cannot settle consistency.");
        }

        return unknowns;
    }

    private static string BudgetNoteFor(ScoutingRegionFocus region, ScoutingViewingType type, ScoutingBudgetImpact budget)
    {
        if (region == ScoutingRegionFocus.Europe)
        {
            return budget.InternationalCoverage;
        }

        if (type == ScoutingViewingType.Tournament)
        {
            return budget.TournamentCoverage;
        }

        return budget.TravelCoverage;
    }

    private static IEnumerable<ScoutPersonalityTrait> BuildTraits(StaffMember scout)
    {
        var traits = new List<ScoutPersonalityTrait>();
        var talent = scout.Attributes.ScoutingScore(StaffScoutingAttribute.TalentEvaluation);
        var character = scout.Attributes.ScoutingScore(StaffScoutingAttribute.CharacterEvaluation);
        var regional = scout.Attributes.ScoutingScore(StaffScoutingAttribute.RegionalKnowledge);
        var goalies = scout.Attributes.ScoutingScore(StaffScoutingAttribute.GoalieEvaluation);
        var europe = scout.Attributes.ScoutingScore(StaffScoutingAttribute.EuropeanKnowledge);
        if (scout.Profile.Reputation >= 65)
        {
            traits.Add(ScoutPersonalityTrait.Optimistic);
        }
        else
        {
            traits.Add(ScoutPersonalityTrait.Conservative);
        }

        if (talent >= 70)
        {
            traits.Add(ScoutPersonalityTrait.LovesSkill);
        }

        if (regional >= 70)
        {
            traits.Add(ScoutPersonalityTrait.FindsSleepers);
        }

        if (goalies >= 60)
        {
            traits.Add(ScoutPersonalityTrait.ExcellentWithGoalies);
        }

        if (talent >= 60 && character >= 60)
        {
            traits.Add(ScoutPersonalityTrait.ExcellentWithDefensemen);
        }

        if (europe < 45)
        {
            traits.Add(ScoutPersonalityTrait.UnderestimatesEuropeans);
        }

        if (scout.Profile.Reputation < 45)
        {
            traits.Add(ScoutPersonalityTrait.PoorAtProjectingCeiling);
        }

        if (traits.Count < 2)
        {
            traits.Add(ScoutPersonalityTrait.OvervaluesSkating);
        }

        return traits.Distinct();
    }

    private static IEnumerable<ScoutingRegionFocus> BuildKnownRegions(StaffMember scout)
    {
        if (scout.Attributes.ScoutingScore(StaffScoutingAttribute.NorthAmericanKnowledge) >= 55)
        {
            yield return ScoutingRegionFocus.WesternCanada;
            yield return ScoutingRegionFocus.USA;
        }

        if (scout.Attributes.ScoutingScore(StaffScoutingAttribute.EuropeanKnowledge) >= 55)
        {
            yield return ScoutingRegionFocus.Europe;
        }

        if (scout.Attributes.ScoutingScore(StaffScoutingAttribute.GoalieEvaluation) >= 60)
        {
            yield return ScoutingRegionFocus.Goalies;
        }

        yield return ScoutingRegionFocus.EasternCanada;
    }

    private static IEnumerable<ScoutSpecialty> BuildSpecialties(StaffMember scout)
    {
        yield return ScoutSpecialty.Amateur;
        if (scout.Attributes.ScoutingScore(StaffScoutingAttribute.RegionalKnowledge) >= 55)
        {
            yield return ScoutSpecialty.Regional;
        }

        if (scout.Attributes.ScoutingScore(StaffScoutingAttribute.GoalieEvaluation) >= 55)
        {
            yield return ScoutSpecialty.Goalie;
        }

        if (scout.Attributes.ScoutingScore(StaffScoutingAttribute.CharacterEvaluation) >= 55)
        {
            yield return ScoutSpecialty.Character;
        }
    }

    private static ScoutingReportSource ReportSourceFor(StaffRole role, ScoutingRegionFocus region) =>
        role switch
        {
            StaffRole.HeadScout or StaffRole.DirectorOfScouting => ScoutingReportSource.HeadScout,
            StaffRole.EuropeanScout => ScoutingReportSource.EuropeanScout,
            StaffRole.GoaltendingScout => ScoutingReportSource.GoaltendingScout,
            _ when region == ScoutingRegionFocus.Character => ScoutingReportSource.CharacterScout,
            _ when region == ScoutingRegionFocus.Medical => ScoutingReportSource.MedicalScout,
            _ when region == ScoutingRegionFocus.Goalies => ScoutingReportSource.GoaltendingScout,
            _ => ScoutingReportSource.RegionalScout
        };

    private static bool SpecialtyFits(ScoutIntelligenceProfile profile, ScoutingRegionFocus region) =>
        region switch
        {
            ScoutingRegionFocus.Goalies => profile.Specialties.Contains(ScoutSpecialty.Goalie),
            ScoutingRegionFocus.Character => profile.Specialties.Contains(ScoutSpecialty.Character),
            _ => profile.Specialties.Contains(ScoutSpecialty.Regional) || profile.Specialties.Contains(ScoutSpecialty.Amateur)
        };

    private static ScoutingRegionFocus RegionForPlayer(NewGmScenarioSnapshot scenario, string playerId)
    {
        var bio = scenario.AlphaSnapshot.DraftBoard.Entries.FirstOrDefault(entry => entry.ProspectPersonId == playerId)?.Bio;
        if (bio is null)
        {
            return ScoutingRegionFocus.WesternCanada;
        }

        if (bio.Country is "Sweden" or "Finland" or "Czechia" or "Slovakia" or "Germany" or "Switzerland" or "Latvia" or "Russia")
        {
            return ScoutingRegionFocus.Europe;
        }

        if (bio.Country == "USA")
        {
            return ScoutingRegionFocus.USA;
        }

        return bio.ProvinceState is "QC" or "ON" ? ScoutingRegionFocus.EasternCanada : ScoutingRegionFocus.WesternCanada;
    }

    private static IEnumerable<StaffMember> ScoutStaff(NewGmScenarioSnapshot scenario) =>
        scenario.StaffMembers.Where(member => member.Department == StaffDepartment.Scouting && member.EmploymentStatus == StaffEmploymentStatus.Employed);

    private static string PersonName(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? scenario.AlphaSnapshot.DraftBoard.Entries.FirstOrDefault(entry => entry.ProspectPersonId == personId)?.ProspectPersonId
        ?? personId;

    private static string PositionText(RosterPosition position) =>
        position switch
        {
            RosterPosition.Center => "center",
            RosterPosition.LeftWing => "left wing",
            RosterPosition.RightWing => "right wing",
            RosterPosition.Defense => "defenseman",
            RosterPosition.Goalie => "goalie",
            _ => "player"
        };
}
