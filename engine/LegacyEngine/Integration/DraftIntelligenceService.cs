using LegacyEngine.Draft;
using LegacyEngine.Rosters;
using LegacyEngine.Scouting;

namespace LegacyEngine.Integration;

public sealed class DraftIntelligenceService
{
    public NewGmScenarioSnapshot EnsureDraftIntelligence(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var prepared = new HockeyIntelligenceRatingService().EnsureRatings(scenario);
        prepared = new ScoutingIntelligenceService().EnsureKnowledgeProfiles(prepared);
        prepared = new DevelopmentCurveService().EnsureCurves(prepared);
        var alerts = BuildAlerts(prepared);
        var views = BuildBoardViews(prepared, alerts);
        var state = prepared.DraftWarRoom with
        {
            BoardViews = views,
            IntelligenceAlerts = alerts
        };
        state.Validate();
        return prepared with { DraftWarRoom = state };
    }

    public DraftProspectIntelligenceCard BuildProspectCard(NewGmScenarioSnapshot scenario, string prospectPersonId)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        if (string.IsNullOrWhiteSpace(prospectPersonId))
        {
            throw new ArgumentException("Prospect id is required.", nameof(prospectPersonId));
        }

        var prepared = scenario.ScoutedRatings.Any(rating => rating.PersonId == prospectPersonId)
            ? scenario
            : new HockeyIntelligenceRatingService().EnsureRatings(scenario);
        var entry = FindDraftEntry(prepared, prospectPersonId);
        var warEntry = prepared.DraftWarRoom.BoardEntries.FirstOrDefault(item => item.ProspectPersonId == prospectPersonId);
        var snapshot = prepared.DraftWarRoom.OriginalBoardSnapshot.FirstOrDefault(item => item.ProspectPersonId == prospectPersonId);
        var scouted = prepared.ScoutedRatings.FirstOrDefault(rating => rating.PersonId == prospectPersonId);
        var rating = prepared.PlayerRatings.FirstOrDefault(item => item.PersonId == prospectPersonId)
            ?? new PlayerRatingService().BuildSnapshot(prepared, prospectPersonId);
        var knowledge = prepared.ScoutingKnowledgeProfiles.FirstOrDefault(profile =>
                profile.OrganizationId == prepared.Organization.OrganizationId && profile.PlayerId == prospectPersonId)
            ?? new ScoutingIntelligenceService().CreateKnowledgeProfile(prepared, prospectPersonId);
        var curve = prepared.DevelopmentCurves.FirstOrDefault(item => item.PersonId == prospectPersonId)
            ?? new DevelopmentCurveService().BuildCurve(prepared, prospectPersonId);
        var consensus = new DraftWarRoomService().BuildConsensus(prepared, prospectPersonId);
        var attributes = BuildAttributeLines(knowledge).ToArray();
        var myRank = warEntry?.PersonalRank ?? snapshot?.Rank ?? entry?.Rank ?? 0;
        var scoutRank = RankBy(prepared, prospectPersonId, ScoutBoardScore);
        var consensusRank = RankBy(prepared, prospectPersonId, ConsensusScore);
        var allAlerts = prepared.DraftWarRoom.IntelligenceAlerts.Count == 0 ? BuildAlerts(prepared) : prepared.DraftWarRoom.IntelligenceAlerts;
        var alerts = allAlerts.Where(alert => alert.ProspectPersonId == prospectPersonId).ToArray();
        var bio = entry?.Bio;
        var position = bio?.Position ?? snapshot?.Position ?? rating.Position;
        var projection = entry?.ProjectionText ?? snapshot?.Projection ?? rating.RoleLabel;
        var card = new DraftProspectIntelligenceCard(
            prospectPersonId,
            PersonName(prepared, prospectPersonId),
            myRank,
            scoutRank,
            consensusRank,
            position,
            rating.Age ?? PersonAge(prepared, prospectPersonId),
            bio is null ? TeamText(prepared, prospectPersonId) : $"{bio.CurrentTeam} / {bio.League}",
            bio?.ShootsCatches ?? "Unknown",
            bio?.HeightDisplay ?? "unknown",
            bio?.WeightDisplay ?? "unknown",
            scouted?.Overall ?? new PlayerRatingRange(rating.Overall.Low, rating.Overall.High),
            scouted?.Potential ?? new PlayerRatingRange(rating.Potential.Low, rating.Potential.High),
            scouted?.ConfidenceColor ?? ColorFor(rating.Confidence),
            entry?.ScoutingConfidence ?? snapshot?.Confidence,
            consensus.Level,
            consensus.AgreementScore,
            TeamFitScore(prepared, position, entry),
            projection,
            RoleLabelFor(position, projection),
            Readable(curve.CurveType),
            Readable(curve.Pace),
            curve.TimeToImpactDisplay,
            RiskText(entry, curve, alerts),
            ScoutRecommendation(prepared, knowledge, entry),
            !string.IsNullOrWhiteSpace(warEntry?.GmNotes) ? warEntry!.GmNotes : !string.IsNullOrWhiteSpace(entry?.PersonalNotes) ? entry!.PersonalNotes : "No GM note yet.",
            attributes,
            alerts);
        card.Validate();
        return card;
    }

    public IReadOnlyList<DraftWarRoomBoardView> BuildBoardViews(NewGmScenarioSnapshot scenario) =>
        BuildBoardViews(scenario, scenario.DraftWarRoom.IntelligenceAlerts.Count == 0 ? BuildAlerts(scenario) : scenario.DraftWarRoom.IntelligenceAlerts);

    public IReadOnlyList<DraftWarRoomBoardView> BuildBoardViews(NewGmScenarioSnapshot scenario, IReadOnlyList<DraftIntelligenceAlert> alerts)
    {
        var activeEntries = scenario.DraftWarRoom.BoardEntries
            .Where(entry => !entry.IsRemoved)
            .OrderBy(entry => entry.PersonalRank)
            .ToArray();
        var cards = activeEntries
            .Select(entry => BuildProspectCardWithoutViewRanks(scenario, entry.ProspectPersonId, alerts))
            .ToArray();
        var myRows = cards
            .OrderBy(card => card.MyBoardRank)
            .Take(30)
            .Select(card => RowFor(card))
            .ToArray();
        var scoutRows = cards
            .OrderByDescending(ScoutBoardScore)
            .ThenBy(card => card.MyBoardRank)
            .Take(30)
            .Select((card, index) => RowFor(card, index + 1))
            .ToArray();
        var consensusRows = cards
            .OrderByDescending(ConsensusScore)
            .ThenBy(card => card.MyBoardRank)
            .Take(30)
            .Select((card, index) => RowFor(card, index + 1))
            .ToArray();
        var watchedRows = cards
            .Where(card => scenario.DraftWarRoom.BoardEntries.Any(entry => entry.ProspectPersonId == card.ProspectPersonId && entry.Tags.Count > 0))
            .OrderBy(card => card.MyBoardRank)
            .Select(card =>
            {
                var tags = scenario.DraftWarRoom.BoardEntries.First(entry => entry.ProspectPersonId == card.ProspectPersonId).Tags;
                return $"{card.ProspectName} | {string.Join(", ", tags)} | {card.RatingDisplay}";
            })
            .ToArray();
        var scarcity = scenario.PositionScarcity ?? new PositionScarcityService().BuildProfile(scenario);
        var needsRows = scenario.DraftWarRoom.Needs
            .Select(need => $"{need.Priority}: {need.Label} - {need.Reason}")
            .Concat(scarcity.Positions
                .OrderByDescending(position => position.ScarcityScore)
                .Take(6)
                .Select(position => $"Position Market: {PositionScarcityService.Label(position.Position)} {position.ScarcityLevel} - {position.Summary}"))
            .ToArray();
        var pickRows = scenario.DraftExperience?.Draft?.Picks
            .Where(pick => pick.Selection is null)
            .OrderBy(pick => pick.PickNumber)
            .Take(10)
            .Select(pick => $"#{pick.PickNumber} {scenario.DraftExperience.OrganizationNames.GetValueOrDefault(pick.OwningOrganizationId, pick.OwningOrganizationId)}")
            .ToArray() ?? Array.Empty<string>();
        var compareRows = cards
            .OrderByDescending(ConsensusScore)
            .Take(4)
            .Select(card => $"{card.ProspectName}: {card.RatingDisplay} | {card.DevelopmentCurve}, ETA {card.Eta} | fit {card.TeamFitScore}/100")
            .ToArray();
        var classRows = scenario.DraftWarRoom.Storylines
            .Select(line => $"{line.Headline}: {line.Summary}")
            .ToArray();
        var alertRows = alerts
            .OrderByDescending(alert => alert.Priority)
            .ThenBy(alert => alert.ProspectName, StringComparer.Ordinal)
            .Take(20)
            .Select(alert => $"{Readable(alert.AlertType)}: {alert.ProspectName} - {alert.Summary}")
            .ToArray();

        var views = new[]
        {
            View(DraftWarRoomViewType.MyBoard, "My Board", myRows, "GM custom board with personal ranks, tags, and notes."),
            View(DraftWarRoomViewType.ScoutBoard, "Scout Board", scoutRows, "Scout-weighted board from current ratings, attribute knowledge, and staff opinions."),
            View(DraftWarRoomViewType.ConsensusBoard, "Consensus Board", consensusRows, "Combined board using scout views, team needs, risk, development path, and class context."),
            View(DraftWarRoomViewType.WatchList, "Watch List", watchedRows, "Tagged favorites, sleepers, avoid flags, medical notes, character flags, and late-round targets."),
            View(DraftWarRoomViewType.TeamNeeds, "Team Needs", needsRows, "Draft needs from roster balance, pipeline, contracts, age curve, identity, and staff philosophy."),
            View(DraftWarRoomViewType.Picks, "Picks", pickRows, "Upcoming draft picks and draft-rights context."),
            View(DraftWarRoomViewType.CompareProspects, "Compare Prospects", compareRows, "Fast comparison of two to four prospects without showing hidden true ratings."),
            View(DraftWarRoomViewType.DraftClassSummary, "Draft Class Summary", classRows, scenario.CurrentDraftClassProfile?.PreviewText ?? "Draft class context is tracked."),
            View(DraftWarRoomViewType.HiddenGemsAvoidList, "Hidden Gems / Avoid List", alertRows, "Intelligence alerts for hidden gems, bust risk, fit, medical, character, and patience warnings.")
        };
        foreach (var view in views)
        {
            view.Validate();
        }

        return views;
    }

    public IReadOnlyList<DraftIntelligenceAlert> BuildAlerts(NewGmScenarioSnapshot scenario)
    {
        var alerts = new List<DraftIntelligenceAlert>();
        var entries = scenario.DraftWarRoom.BoardEntries.Count == 0
            ? scenario.AlphaSnapshot.DraftBoard.Entries.Select((entry, index) => new DraftWarRoomEntry(entry.ProspectPersonId, PersonName(scenario, entry.ProspectPersonId), index + 1, entry.Rank, Array.Empty<DraftWatchTag>(), "Board", false, false, false, entry.PersonalNotes)).ToArray()
            : scenario.DraftWarRoom.BoardEntries.Where(entry => !entry.IsRemoved).OrderBy(entry => entry.PersonalRank).ToArray();

        foreach (var entry in entries.Take(45))
        {
            var card = BuildProspectCardWithoutAlerts(scenario, entry.ProspectPersonId);
            var range = card.PotentialEstimate.IsUnknown ? 0 : (card.PotentialEstimate.High!.Value - card.PotentialEstimate.Low!.Value);
            if (!card.PotentialEstimate.IsUnknown && card.PotentialEstimate.High!.Value >= 88 && (entry.PersonalRank > 18 || card.RatingConfidenceColor is PlayerRatingColor.Red or PlayerRatingColor.Green))
            {
                alerts.Add(Alert(DraftIntelligenceAlertType.HiddenGemCandidate, card, 78, $"POT {card.PotentialEstimate.Display} with incomplete certainty creates possible surplus value.", "Give another scout viewing or keep high on the board."));
            }

            if (!card.PotentialEstimate.IsUnknown && entry.PersonalRank <= 12 && card.PotentialEstimate.High!.Value <= 82 && card.RatingConfidenceColor >= PlayerRatingColor.Blue)
            {
                alerts.Add(Alert(DraftIntelligenceAlertType.BustRisk, card, 82, $"The board slot is aggressive for a tightened POT {card.PotentialEstimate.Display} read.", "Recheck the ranking against safer or higher-ceiling options."));
            }

            if (range >= 10)
            {
                alerts.Add(Alert(DraftIntelligenceAlertType.HighCeilingLowFloor, card, 60, $"Wide potential range {card.PotentialEstimate.Display} points to volatility.", "Compare with safer players before draft day."));
            }

            if (card.ScoutConsensus == ScoutConsensusLevel.StrongConsensus && card.RatingConfidenceColor >= PlayerRatingColor.Blue)
            {
                alerts.Add(Alert(DraftIntelligenceAlertType.SafePick, card, 44, "Scouts are aligned and the current evidence is stable.", "Treat as a lower-variance option."));
            }

            if (card.DevelopmentCurve.Contains("Slow", StringComparison.OrdinalIgnoreCase)
                || card.DevelopmentCurve.Contains("Late", StringComparison.OrdinalIgnoreCase)
                || card.DevelopmentCurve.Contains("Patience", StringComparison.OrdinalIgnoreCase))
            {
                alerts.Add(Alert(DraftIntelligenceAlertType.NeedsPatience, card, 58, $"{card.DevelopmentCurve} path with ETA {card.Eta}.", "Do not rush the player into a role beyond his readiness."));
            }

            if (card.TeamFitScore <= 30 && entry.PersonalRank <= 20)
            {
                alerts.Add(Alert(DraftIntelligenceAlertType.BadFitForOrganization, card, 55, "Strong enough player, but not aligned with the biggest team needs.", "Confirm whether best-player-available outweighs fit."));
            }

            if (card.RiskSummary.Contains("medical", StringComparison.OrdinalIgnoreCase))
            {
                alerts.Add(Alert(DraftIntelligenceAlertType.MedicalRisk, card, 72, card.RiskSummary, "Ask medical staff for a focused review before selecting."));
            }

            if (card.RiskSummary.Contains("character", StringComparison.OrdinalIgnoreCase)
                || card.RiskSummary.Contains("confidence", StringComparison.OrdinalIgnoreCase))
            {
                alerts.Add(Alert(DraftIntelligenceAlertType.CharacterRisk, card, 64, card.RiskSummary, "Have staff clarify fit and support plan."));
            }
        }

        var deduped = alerts
            .GroupBy(alert => $"{alert.AlertType}:{alert.ProspectPersonId}", StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(alert => alert.Priority).First())
            .OrderByDescending(alert => alert.Priority)
            .ThenBy(alert => alert.ProspectName, StringComparer.Ordinal)
            .ToList();

        if (deduped.All(alert => alert.AlertType != DraftIntelligenceAlertType.HiddenGemCandidate))
        {
            var fallback = entries.Skip(18).FirstOrDefault() ?? entries.LastOrDefault();
            if (fallback is not null)
            {
                deduped.Add(Alert(DraftIntelligenceAlertType.HiddenGemCandidate, BuildProspectCardWithoutAlerts(scenario, fallback.ProspectPersonId), 90, "Late-board profile deserves a second look because draft-room uncertainty remains.", "Keep on the sleeper board."));
            }
        }

        if (deduped.All(alert => alert.AlertType != DraftIntelligenceAlertType.BustRisk))
        {
            var fallback = entries.Take(12).LastOrDefault() ?? entries.FirstOrDefault();
            if (fallback is not null)
            {
                deduped.Add(Alert(DraftIntelligenceAlertType.BustRisk, BuildProspectCardWithoutAlerts(scenario, fallback.ProspectPersonId), 88, "Top-board prospect still carries enough uncertainty to revisit before the pick.", "Compare against the consensus board."));
            }
        }

        deduped = deduped
            .OrderByDescending(alert => alert.Priority)
            .ThenBy(alert => alert.ProspectName, StringComparer.Ordinal)
            .ToList();

        foreach (var alert in deduped)
        {
            alert.Validate();
        }

        return deduped.Take(24).ToArray();
    }

    public DraftProspectComparison CompareProspects(NewGmScenarioSnapshot scenario, IReadOnlyList<string> prospectIds)
    {
        var ids = prospectIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).Take(4).ToArray();
        if (ids.Length < 2)
        {
            throw new ArgumentException("Compare requires two to four prospects.", nameof(prospectIds));
        }

        var items = ids.Select(id =>
        {
            var card = BuildProspectCard(scenario, id);
            return new DraftProspectComparisonItem(
                card.ProspectPersonId,
                card.ProspectName,
                card.Position,
                card.Age,
                card.Height,
                card.Weight,
                card.CurrentTeamLeague,
                card.ScoutingConfidence,
                $"{card.RatingDisplay}; {card.Projection}",
                card.ScoutConsensus == ScoutConsensusLevel.VeryDivided ? "Very divided room; inspect character and evidence." : "Character profile is part of the scouting file.",
                $"{card.DevelopmentCurve}, {card.DevelopmentPace}, ETA {card.Eta}",
                card.RiskSummary,
                $"Team fit {card.TeamFitScore}/100; consensus rank #{card.ConsensusBoardRank}; scout rank #{card.ScoutBoardRank}.");
        }).ToArray();
        var comparison = new DraftProspectComparison(
            items,
            $"Comparing {items.Length} prospects by visible ratings, attributes, confidence, development curve, risk, team fit, and scout disagreement. Hidden true ratings are not shown.");
        comparison.Validate();
        return comparison;
    }

    public string BuildRowText(NewGmScenarioSnapshot scenario, DraftBoardEntry entry)
    {
        var card = BuildProspectCard(scenario, entry.ProspectPersonId);
        return $"{entry.Rank}. {card.ProspectName} | {PositionShort(card.Position)} | {card.Age?.ToString() ?? "age ?"} | {card.CurrentTeamLeague} | {card.RatingDisplay} | {card.Projection} | Risk: {ShortRisk(card)} | {card.ScoutConsensus} | Fit {card.TeamFitScore}/100";
    }

    public IReadOnlyList<string> BuildAttributeSummaryLines(NewGmScenarioSnapshot scenario, string prospectPersonId, int count = 8)
    {
        var card = BuildProspectCard(scenario, prospectPersonId);
        return card.Attributes
            .OrderBy(attribute => attribute.Estimate.IsUnknown)
            .ThenByDescending(attribute => attribute.Estimate.Midpoint)
            .Take(count)
            .Select(attribute => attribute.DisplayText)
            .ToArray();
    }

    public string BuildDraftTimeAttributeSnapshot(NewGmScenarioSnapshot scenario, string prospectPersonId)
    {
        var card = BuildProspectCard(scenario, prospectPersonId);
        var known = card.Attributes
            .OrderBy(attribute => attribute.Estimate.IsUnknown)
            .ThenByDescending(attribute => attribute.Estimate.Midpoint)
            .Take(8)
            .Select(attribute => attribute.DisplayText);
        return string.Join("; ", known.DefaultIfEmpty("Attribute confidence not recorded."));
    }

    private DraftProspectIntelligenceCard BuildProspectCardWithoutViewRanks(NewGmScenarioSnapshot scenario, string prospectPersonId, IReadOnlyList<DraftIntelligenceAlert> alerts)
    {
        var card = BuildProspectCardWithoutAlerts(scenario, prospectPersonId);
        return card with
        {
            ScoutBoardRank = 0,
            ConsensusBoardRank = 0,
            Alerts = alerts.Where(alert => alert.ProspectPersonId == prospectPersonId).ToArray()
        };
    }

    private DraftProspectIntelligenceCard BuildProspectCardWithoutAlerts(NewGmScenarioSnapshot scenario, string prospectPersonId)
    {
        var entry = FindDraftEntry(scenario, prospectPersonId);
        var warEntry = scenario.DraftWarRoom.BoardEntries.FirstOrDefault(item => item.ProspectPersonId == prospectPersonId);
        var snapshot = scenario.DraftWarRoom.OriginalBoardSnapshot.FirstOrDefault(item => item.ProspectPersonId == prospectPersonId);
        var rating = scenario.PlayerRatings.FirstOrDefault(item => item.PersonId == prospectPersonId)
            ?? new PlayerRatingService().BuildSnapshot(scenario, prospectPersonId);
        var scouted = scenario.ScoutedRatings.FirstOrDefault(item => item.PersonId == prospectPersonId);
        var knowledge = scenario.ScoutingKnowledgeProfiles.FirstOrDefault(profile =>
                profile.OrganizationId == scenario.Organization.OrganizationId && profile.PlayerId == prospectPersonId)
            ?? new ScoutingIntelligenceService().CreateKnowledgeProfile(scenario, prospectPersonId);
        var curve = scenario.DevelopmentCurves.FirstOrDefault(item => item.PersonId == prospectPersonId)
            ?? new DevelopmentCurveService().BuildCurve(scenario, prospectPersonId);
        var bio = entry?.Bio;
        var position = bio?.Position ?? snapshot?.Position ?? rating.Position;
        var consensus = BasicConsensusLevel(knowledge);
        var card = new DraftProspectIntelligenceCard(
            prospectPersonId,
            PersonName(scenario, prospectPersonId),
            warEntry?.PersonalRank ?? snapshot?.Rank ?? entry?.Rank ?? 0,
            0,
            0,
            position,
            rating.Age ?? PersonAge(scenario, prospectPersonId),
            bio is null ? TeamText(scenario, prospectPersonId) : $"{bio.CurrentTeam} / {bio.League}",
            bio?.ShootsCatches ?? "Unknown",
            bio?.HeightDisplay ?? "unknown",
            bio?.WeightDisplay ?? "unknown",
            scouted?.Overall ?? new PlayerRatingRange(rating.Overall.Low, rating.Overall.High),
            scouted?.Potential ?? new PlayerRatingRange(rating.Potential.Low, rating.Potential.High),
            scouted?.ConfidenceColor ?? ColorFor(rating.Confidence),
            entry?.ScoutingConfidence ?? snapshot?.Confidence,
            consensus,
            Math.Clamp(100 - knowledge.Consensus.DisagreementLevel, 0, 100),
            TeamFitScore(scenario, position, entry),
            entry?.ProjectionText ?? snapshot?.Projection ?? rating.RoleLabel,
            RoleLabelFor(position, entry?.ProjectionText ?? rating.RoleLabel),
            Readable(curve.CurveType),
            Readable(curve.Pace),
            curve.TimeToImpactDisplay,
            RiskText(entry, curve, Array.Empty<DraftIntelligenceAlert>()),
            ScoutRecommendation(scenario, knowledge, entry),
            !string.IsNullOrWhiteSpace(warEntry?.GmNotes) ? warEntry!.GmNotes : !string.IsNullOrWhiteSpace(entry?.PersonalNotes) ? entry!.PersonalNotes : "No GM note yet.",
            BuildAttributeLines(knowledge).ToArray(),
            Array.Empty<DraftIntelligenceAlert>());
        card.Validate();
        return card;
    }

    private static IEnumerable<DraftAttributeIntelligenceLine> BuildAttributeLines(ScoutingKnowledgeProfile knowledge)
    {
        foreach (var attribute in knowledge.Attributes)
        {
            yield return new DraftAttributeIntelligenceLine(
                attribute.Category,
                attribute.Attribute,
                attribute.Estimate,
                attribute.ConfidenceColor,
                attribute.SourceScoutName,
                attribute.LastViewedDate,
                attribute.Note);
        }
    }

    private static DraftWarRoomBoardView View(DraftWarRoomViewType type, string title, IReadOnlyList<string> rows, string summary) =>
        new(type, title, rows.Count == 0 ? new[] { "No entries yet." } : rows, summary);

    private static DraftIntelligenceAlert Alert(DraftIntelligenceAlertType type, DraftProspectIntelligenceCard card, int priority, string summary, string action) =>
        new(type, card.ProspectPersonId, card.ProspectName, Math.Clamp(priority, 0, 100), summary, action);

    private static int ScoutBoardScore(DraftProspectIntelligenceCard card) =>
        card.PotentialEstimate.Midpoint * 4
        + card.OverallEstimate.Midpoint * 2
        + ColorScore(card.RatingConfidenceColor)
        + card.ScoutAgreementScore
        - RiskPenalty(card);

    private static int ConsensusScore(DraftProspectIntelligenceCard card) =>
        ScoutBoardScore(card)
        + card.TeamFitScore * 2
        + (card.DevelopmentCurve.Contains("High Floor", StringComparison.OrdinalIgnoreCase) ? 15 : 0)
        + (card.DevelopmentCurve.Contains("Boom", StringComparison.OrdinalIgnoreCase) ? 8 : 0)
        - (card.Alerts.Any(alert => alert.AlertType == DraftIntelligenceAlertType.BustRisk) ? 22 : 0);

    private int RankBy(NewGmScenarioSnapshot scenario, string prospectPersonId, Func<DraftProspectIntelligenceCard, int> score)
    {
        var cards = scenario.DraftWarRoom.BoardEntries
            .Where(entry => !entry.IsRemoved)
            .Select(entry => BuildProspectCardWithoutAlerts(scenario, entry.ProspectPersonId))
            .OrderByDescending(score)
            .ThenBy(card => card.MyBoardRank)
            .Select((card, index) => new { card.ProspectPersonId, Rank = index + 1 })
            .ToArray();
        return cards.FirstOrDefault(card => card.ProspectPersonId == prospectPersonId)?.Rank ?? 0;
    }

    private static string RowFor(DraftProspectIntelligenceCard card) =>
        RowFor(card, card.MyBoardRank);

    private static string RowFor(DraftProspectIntelligenceCard card, int rank) =>
        $"#{rank} {card.ProspectName} | {PositionShort(card.Position)} | {card.Age?.ToString() ?? "age ?"} | {card.CurrentTeamLeague} | {card.RatingDisplay} | {card.Projection} | Risk {ShortRisk(card)} | {card.ScoutConsensus} | Fit {card.TeamFitScore}/100";

    private static int TeamFitScore(NewGmScenarioSnapshot scenario, RosterPosition position, DraftBoardEntry? entry)
    {
        var baseScore = 42;
        foreach (var need in scenario.DraftWarRoom.Needs)
        {
            if (need.TargetPosition == position || NeedMatchesPosition(need.NeedType, position))
            {
                baseScore += need.Priority switch
                {
                    TradePriority.Urgent => 38,
                    TradePriority.High => 28,
                    TradePriority.Medium => 18,
                    TradePriority.Low => 8,
                    _ => 4
                };
            }
        }

        if (entry?.Bio?.CharacterSummary.Contains("leader", StringComparison.OrdinalIgnoreCase) == true)
        {
            baseScore += 8;
        }

        if (entry?.RiskSummary.Contains("medical", StringComparison.OrdinalIgnoreCase) == true)
        {
            baseScore -= 10;
        }

        if (entry is not null)
        {
            var market = (scenario.PositionScarcity ?? new PositionScarcityService().BuildProfile(scenario))
                .For(PositionScarcityService.MarketPositionFor(entry));
            baseScore += market.ScarcityLevel switch
            {
                PositionScarcityLevel.Critical => 18,
                PositionScarcityLevel.Scarce => 12,
                PositionScarcityLevel.Thin => 6,
                PositionScarcityLevel.Oversupplied => -8,
                _ => 0
            };
        }

        return Math.Clamp(baseScore, 0, 100);
    }

    private static bool NeedMatchesPosition(TeamNeedType need, RosterPosition position) =>
        need switch
        {
            TeamNeedType.StartingGoalie or TeamNeedType.BackupGoalie => position == RosterPosition.Goalie,
            TeamNeedType.TopPairDefense or TeamNeedType.DefensiveDefenseman => position == RosterPosition.Defense,
            TeamNeedType.TopSixForward or TeamNeedType.Scoring or TeamNeedType.Physicality => position is RosterPosition.Center or RosterPosition.LeftWing or RosterPosition.RightWing,
            TeamNeedType.Prospects or TeamNeedType.DraftPicks => true,
            _ => false
        };

    private static ScoutConsensusLevel BasicConsensusLevel(ScoutingKnowledgeProfile knowledge) =>
        knowledge.Consensus.DisagreementLevel >= 58
            ? ScoutConsensusLevel.VeryDivided
            : knowledge.Consensus.DisagreementLevel >= 28
                ? ScoutConsensusLevel.MixedOpinions
                : ScoutConsensusLevel.StrongConsensus;

    private static string ScoutRecommendation(NewGmScenarioSnapshot scenario, ScoutingKnowledgeProfile knowledge, DraftBoardEntry? entry)
    {
        var report = scenario.CompletedScoutingReports
            .Where(report => report.PlayerId == knowledge.PlayerId)
            .OrderByDescending(report => report.CreatedOn)
            .FirstOrDefault();
        if (report is not null)
        {
            return report.Recommendation.ToString();
        }

        return entry?.ScoutingConfidence >= ScoutingConfidenceLevel.High
            ? $"Staff have enough evidence to keep {knowledge.PlayerName} on the active board."
            : knowledge.RecommendedNextAction;
    }

    private static string RiskText(DraftBoardEntry? entry, PlayerDevelopmentCurve curve, IReadOnlyList<DraftIntelligenceAlert> alerts)
    {
        var pieces = new List<string>();
        if (!string.IsNullOrWhiteSpace(entry?.RiskSummary))
        {
            pieces.Add(entry.RiskSummary);
        }

        if (curve.Variance.PlateauRisk >= 70)
        {
            pieces.Add($"Plateau risk {curve.Variance.PlateauRisk}/100.");
        }

        if (curve.Variance.ProbabilityMissingProjection >= 45)
        {
            pieces.Add($"Miss-projection risk {curve.Variance.ProbabilityMissingProjection}/100.");
        }

        pieces.AddRange(alerts.Where(alert => alert.AlertType is DraftIntelligenceAlertType.BustRisk or DraftIntelligenceAlertType.MedicalRisk or DraftIntelligenceAlertType.CharacterRisk).Select(alert => alert.Summary));
        return string.Join(" ", pieces.DefaultIfEmpty("No major draft-room risk flagged."));
    }

    private static string ShortRisk(DraftProspectIntelligenceCard card)
    {
        if (card.Alerts.Any(alert => alert.AlertType == DraftIntelligenceAlertType.BustRisk))
        {
            return "Bust";
        }

        if (card.Alerts.Any(alert => alert.AlertType == DraftIntelligenceAlertType.MedicalRisk))
        {
            return "Medical";
        }

        if (card.PotentialEstimate.IsUnknown || card.RatingConfidenceColor is PlayerRatingColor.Red)
        {
            return "Unknown";
        }

        return card.PotentialEstimate.High!.Value - card.PotentialEstimate.Low!.Value >= 10 ? "Volatile" : "Normal";
    }

    private static int RiskPenalty(DraftProspectIntelligenceCard card) =>
        ShortRisk(card) switch
        {
            "Bust" => 35,
            "Medical" => 22,
            "Unknown" => 10,
            "Volatile" => 8,
            _ => 0
        };

    private static int ColorScore(PlayerRatingColor color) =>
        color switch
        {
            PlayerRatingColor.Black => 35,
            PlayerRatingColor.Blue => 28,
            PlayerRatingColor.Green => 16,
            PlayerRatingColor.Red => 4,
            _ => 0
        };

    private static PlayerRatingColor ColorFor(PlayerRatingConfidence confidence) =>
        confidence switch
        {
            PlayerRatingConfidence.VeryHigh => PlayerRatingColor.Black,
            PlayerRatingConfidence.High => PlayerRatingColor.Blue,
            PlayerRatingConfidence.Medium => PlayerRatingColor.Green,
            PlayerRatingConfidence.Low => PlayerRatingColor.Red,
            _ => PlayerRatingColor.Unknown
        };

    private static string RoleLabelFor(RosterPosition position, string projection)
    {
        if (!string.IsNullOrWhiteSpace(projection))
        {
            return projection;
        }

        return position switch
        {
            RosterPosition.Goalie => "goalie prospect",
            RosterPosition.Defense => "defense prospect",
            RosterPosition.Center => "center prospect",
            RosterPosition.LeftWing or RosterPosition.RightWing => "wing prospect",
            _ => "draft prospect"
        };
    }

    private static DraftBoardEntry? FindDraftEntry(NewGmScenarioSnapshot scenario, string prospectPersonId) =>
        scenario.AlphaSnapshot.DraftBoard.Entries.FirstOrDefault(entry => entry.ProspectPersonId == prospectPersonId);

    private static string TeamText(NewGmScenarioSnapshot scenario, string personId)
    {
        var prospect = scenario.ProspectRights.FirstOrDefault(item => item.ProspectPersonId == personId);
        if (prospect is not null)
        {
            return string.IsNullOrWhiteSpace(prospect.CurrentLeague)
                ? prospect.CurrentTeam
                : $"{prospect.CurrentTeam} / {prospect.CurrentLeague}";
        }

        return scenario.AlphaSnapshot.Organization?.Name ?? scenario.Organization.Name;
    }

    private static int? PersonAge(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.CalculateAge(scenario.CurrentDate);

    private static string PersonName(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? scenario.ProspectRights.FirstOrDefault(record => record.ProspectPersonId == personId)?.ProspectName
        ?? personId;

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

    private static string Readable(Enum value)
    {
        var text = value.ToString();
        return string.Concat(text.Select((letter, index) => index > 0 && char.IsUpper(letter) ? $" {letter}" : letter.ToString()));
    }
}
