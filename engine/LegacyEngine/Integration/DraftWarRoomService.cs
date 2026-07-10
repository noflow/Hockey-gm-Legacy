using LegacyEngine.Draft;
using LegacyEngine.Rosters;
using LegacyEngine.Scouting;

namespace LegacyEngine.Integration;

public sealed class DraftWarRoomService
{
    public NewGmScenarioSnapshot EnsureWarRoom(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var existing = scenario.DraftWarRoom;
        var existingById = existing.BoardEntries.ToDictionary(entry => entry.ProspectPersonId, StringComparer.Ordinal);
        var entries = scenario.AlphaSnapshot.DraftBoard.Entries
            .OrderBy(entry => entry.Rank)
            .Select(entry => BuildEntry(scenario, entry, existingById.TryGetValue(entry.ProspectPersonId, out var current) ? current : null))
            .OrderBy(entry => entry.IsRemoved)
            .ThenBy(entry => entry.PersonalRank)
            .Select((entry, index) => entry with { PersonalRank = index + 1 })
            .ToArray();

        var state = new DraftWarRoomState(
            scenario.Organization.OrganizationId,
            scenario.Season.Year,
            entries,
            BuildNeeds(scenario),
            BuildStorylines(scenario),
            BuildBestPlayerAvailableOpinions(scenario),
            existing.OriginalBoardSnapshot.Count > 0 ? existing.OriginalBoardSnapshot : BuildOriginalBoardSnapshot(scenario),
            existing.PostDraftReview);
        state.Validate();
        var withState = scenario with { DraftWarRoom = state };
        return ShouldEnrichDraftIntelligence(withState)
            ? new DraftIntelligenceService().EnsureDraftIntelligence(withState)
            : withState;
    }

    public NewGmScenarioSnapshot MoveProspect(NewGmScenarioSnapshot scenario, string prospectPersonId, int direction)
    {
        var prepared = EnsureWarRoom(scenario);
        var entries = prepared.DraftWarRoom.BoardEntries.OrderBy(entry => entry.PersonalRank).ToArray();
        var index = Array.FindIndex(entries, entry => entry.ProspectPersonId == prospectPersonId);
        if (index < 0)
        {
            throw new ArgumentException("Prospect is not in the draft war room.", nameof(prospectPersonId));
        }

        var target = Math.Clamp(index + (direction < 0 ? -1 : 1), 0, entries.Length - 1);
        if (target == index)
        {
            return prepared;
        }

        (entries[index], entries[target]) = (entries[target], entries[index]);
        return ApplyEntries(prepared, entries);
    }

    public NewGmScenarioSnapshot MoveToRank(NewGmScenarioSnapshot scenario, string prospectPersonId, int rank)
    {
        if (rank <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rank), "Rank must be positive.");
        }

        var prepared = EnsureWarRoom(scenario);
        var entries = prepared.DraftWarRoom.BoardEntries.OrderBy(entry => entry.PersonalRank).ToList();
        var current = entries.SingleOrDefault(entry => entry.ProspectPersonId == prospectPersonId)
            ?? throw new ArgumentException("Prospect is not in the draft war room.", nameof(prospectPersonId));
        entries.Remove(current);
        entries.Insert(Math.Clamp(rank - 1, 0, entries.Count), current);
        return ApplyEntries(prepared, entries);
    }

    public NewGmScenarioSnapshot SetWatchTag(NewGmScenarioSnapshot scenario, string prospectPersonId, DraftWatchTag tag, bool enabled)
    {
        var prepared = EnsureWarRoom(scenario);
        var entries = prepared.DraftWarRoom.BoardEntries.Select(entry =>
        {
            if (entry.ProspectPersonId != prospectPersonId)
            {
                return entry;
            }

            var tags = enabled
                ? entry.Tags.Append(tag).Distinct().ToArray()
                : entry.Tags.Where(item => item != tag).ToArray();
            return entry with
            {
                Tags = tags,
                IsPinned = tag == DraftWatchTag.Pinned ? enabled : entry.IsPinned,
                IsFavorite = tag == DraftWatchTag.Favorite ? enabled : entry.IsFavorite
            };
        }).ToArray();
        return ApplyEntries(prepared, entries);
    }

    public NewGmScenarioSnapshot RemoveFromPersonalBoard(NewGmScenarioSnapshot scenario, string prospectPersonId)
    {
        var prepared = EnsureWarRoom(scenario);
        var entries = prepared.DraftWarRoom.BoardEntries
            .Select(entry => entry.ProspectPersonId == prospectPersonId
                ? entry with { IsRemoved = true, Tags = entry.Tags.Append(DraftWatchTag.Avoid).Distinct().ToArray() }
                : entry)
            .ToArray();
        return ApplyEntries(prepared, entries);
    }

    public NewGmScenarioSnapshot UpdateGmNotes(NewGmScenarioSnapshot scenario, string prospectPersonId, string notes)
    {
        var prepared = EnsureWarRoom(scenario);
        var entries = prepared.DraftWarRoom.BoardEntries
            .Select(entry => entry.ProspectPersonId == prospectPersonId ? entry with { GmNotes = notes.Trim() } : entry)
            .ToArray();
        return ApplyEntries(prepared, entries);
    }

    public DraftScoutConsensus BuildConsensus(NewGmScenarioSnapshot scenario, string prospectPersonId)
    {
        var entry = scenario.AlphaSnapshot.DraftBoard.Entries.FirstOrDefault(item => item.ProspectPersonId == prospectPersonId);
        var name = PersonName(scenario, prospectPersonId);
        var intelligence = new ScoutingIntelligenceService();
        var knowledge = intelligence.BuildConsensusFromKnowledge(scenario, prospectPersonId);
        var reports = intelligence.BuildReportCards(scenario, prospectPersonId, scenario.LeagueProfile.Rulebook);
        var confidence = entry?.ScoutingConfidence ?? reports.OrderByDescending(report => report.Confidence).FirstOrDefault()?.Confidence ?? ScoutingConfidenceLevel.Unknown;
        var opinions = new[]
        {
            Opinion("Head Scout", prospectPersonId, name, reports.FirstOrDefault()?.Recommendation ?? $"Consensus OVR {knowledge.OverallEstimate.Display}, POT {knowledge.PotentialEstimate.Display}.", confidence),
            Opinion("Regional Scout", prospectPersonId, name, reports.Skip(1).FirstOrDefault()?.CurrentPicture ?? RegionOpinion(entry), confidence),
            Opinion("Development Scout", prospectPersonId, name, $"{DevelopmentOpinion(entry)} {knowledge.Summary}", confidence),
            Opinion("Medical", prospectPersonId, name, MedicalOpinion(entry), confidence),
            Opinion("Character", prospectPersonId, name, entry?.Bio?.CharacterSummary ?? "Character picture incomplete.", confidence),
            Opinion("Analytics", prospectPersonId, name, entry?.AnalyticsSummary ?? $"Attribute knowledge: {knowledge.ScoutOpinions.Count} scout-specific opinions.", confidence)
        };
        var varied = opinions.Select(opinion => opinion.Opinion).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var score = Math.Clamp((int)confidence * 20 + reports.Count * 6 - Math.Max(0, varied - 3) * 5, 0, 100);
        var level = score >= 72 ? ScoutConsensusLevel.StrongConsensus : score >= 42 ? ScoutConsensusLevel.MixedOpinions : ScoutConsensusLevel.VeryDivided;
        var consensus = new DraftScoutConsensus(
            prospectPersonId,
            name,
            level,
            score,
            opinions,
            $"{level}: agreement {score}/100 across head scout, regional, development, medical, character, and analytics views.");
        consensus.Validate();
        return consensus;
    }

    public DraftProspectComparison CompareProspects(NewGmScenarioSnapshot scenario, IReadOnlyList<string> prospectIds)
    {
        var ids = prospectIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).Take(4).ToArray();
        if (ids.Length < 2)
        {
            throw new ArgumentException("Compare requires at least two prospects.", nameof(prospectIds));
        }

        var items = ids.Select(id => BuildComparisonItem(scenario, id)).ToArray();
        var summary = $"Comparing {items.Length} prospects by visible bio, projection, reports, confidence, character, development, medical, and story context. Hidden ratings are not shown.";
        var comparison = new DraftProspectComparison(items, summary);
        comparison.Validate();
        return comparison;
    }

    public DraftPostDraftReview BuildPostDraftReview(NewGmScenarioSnapshot scenario, IReadOnlyList<DraftPickSummary> selections)
    {
        var yourSelections = selections.Where(selection => selection.IsPlayerSelection).ToArray();
        var first = yourSelections.FirstOrDefault();
        var intelligence = new DraftIntelligenceService();
        var cards = yourSelections
            .Select(selection => intelligence.BuildProspectCard(scenario, selection.ProspectPersonId))
            .ToArray();
        var bestValue = cards.OrderByDescending(card => card.ConsensusBoardRank == 0 ? 0 : card.MyBoardRank - card.ConsensusBoardRank).FirstOrDefault();
        var riskiest = cards
            .OrderByDescending(card => card.Alerts.Any(alert => alert.AlertType == DraftIntelligenceAlertType.BustRisk) ? 2 : card.Alerts.Count)
            .ThenByDescending(card => card.RiskSummary.Length)
            .FirstOrDefault();
        var needsFit = cards.Length == 0 ? "No team-needs fit recorded." : $"Team-needs fit average {cards.Average(card => card.TeamFitScore):0}/100.";
        var review = new DraftPostDraftReview(
            scenario.Season.Year,
            scenario.CurrentDate,
            yourSelections.Length == 0
                ? "Head scout: no player-team selections were made."
                : $"Head scout: the room stayed close to the board and added {yourSelections.Length} prospect(s), led by {first!.ProspectName}. Best value: {bestValue?.ProspectName ?? first.ProspectName}. {needsFit}",
            yourSelections.Length == 0
                ? "Owner: no draft-rights impact yet."
                : $"Owner: expects patience with {first!.ProspectName} and wants development runway protected.",
            "Coach: drafted players are future camp and development decisions, not automatic roster adds.",
            GradeForDraft(yourSelections),
            cards.Length == 0
                ? yourSelections.Select(selection => $"{selection.ProspectName}: projected impact tracked through draft rights, prospect list, and Where Are They Now.").ToArray()
                : cards.Select(card => $"{card.ProspectName}: {card.RatingDisplay}; curve {card.DevelopmentCurve}; ETA {card.Eta}; risk {(riskiest?.ProspectPersonId == card.ProspectPersonId ? "highest class risk among your picks" : ShortRisk(card))}.").ToArray());
        review.Validate();
        return review;
    }

    public string BuildWarRoomSummary(NewGmScenarioSnapshot scenario)
    {
        var prepared = EnsureWarRoom(scenario);
        var board = prepared.DraftWarRoom;
        var intelligence = string.Join(" ", new ScoutingIntelligenceService().BuildWarRoomKnowledgeLines(prepared));
        return $"Draft War Room: {board.BoardEntries.Count(entry => !entry.IsRemoved)} active board entries, {board.BoardEntries.Count(entry => entry.Tags.Count > 0)} flagged, {board.Needs.Count} needs, {board.BoardViews.Count} views, {board.IntelligenceAlerts.Count} alerts. Scouting intelligence: {intelligence}";
    }

    public int ScoreAiDraftFit(NewGmScenarioSnapshot scenario, string organizationId, DraftBoardEntry entry, int fallbackRank)
    {
        var ai = scenario.OrganizationAiProfiles.FirstOrDefault(profile => profile.OrganizationId == organizationId);
        var score = 1000 - (entry.Rank * 10) - fallbackRank;
        var position = entry.Bio?.Position ?? RosterPosition.Unknown;
        var market = (scenario.PositionScarcity ?? new PositionScarcityService().BuildProfile(scenario))
            .For(PositionScarcityService.MarketPositionFor(entry));
        score += market.ScarcityLevel switch
        {
            PositionScarcityLevel.Critical => 42,
            PositionScarcityLevel.Scarce => 28,
            PositionScarcityLevel.Thin => 14,
            PositionScarcityLevel.Oversupplied => -18,
            _ => 0
        };
        if (ai is not null)
        {
            foreach (var need in ai.CurrentNeeds)
            {
                score += NeedPositionMatch(need.NeedType, position) ? PriorityScore(need.Priority) : 0;
            }

            score += ai.Personality switch
            {
                OrganizationAiPersonality.GoalieFocused when position == RosterPosition.Goalie => 45,
                OrganizationAiPersonality.DefenseFirst when position == RosterPosition.Defense => 35,
                OrganizationAiPersonality.SkillFirst when position is RosterPosition.Center or RosterPosition.LeftWing or RosterPosition.RightWing => 25,
                OrganizationAiPersonality.DraftAndDevelop or OrganizationAiPersonality.ProspectHoarder => 12,
                OrganizationAiPersonality.RiskTaker when entry.RiskSummary.Contains("boom", StringComparison.OrdinalIgnoreCase) => 18,
                _ => 0
            };
        }

        score += entry.ScoutingConfidence switch
        {
            ScoutingConfidenceLevel.VeryHigh => 28,
            ScoutingConfidenceLevel.High => 20,
            ScoutingConfidenceLevel.Medium => 10,
            ScoutingConfidenceLevel.Low => -5,
            _ => 0
        };
        foreach (var need in scenario.DraftWarRoom.Needs)
        {
            score += NeedPositionMatch(need.NeedType, position) ? PriorityScore(need.Priority) / 2 : 0;
        }

        if (entry.RiskSummary.Contains("medical", StringComparison.OrdinalIgnoreCase))
        {
            score -= ai?.Personality == OrganizationAiPersonality.RiskTaker ? 4 : 18;
        }

        if (entry.RiskSummary.Contains("boom", StringComparison.OrdinalIgnoreCase))
        {
            score += ai?.Personality == OrganizationAiPersonality.RiskTaker ? 18 : 4;
        }

        if (entry.Bio?.PotentialLineupProjection.Contains("upside", StringComparison.OrdinalIgnoreCase) == true)
        {
            score += ai?.Personality is OrganizationAiPersonality.DraftAndDevelop or OrganizationAiPersonality.ProspectHoarder ? 16 : 6;
        }

        return score;
    }

    private NewGmScenarioSnapshot ApplyEntries(NewGmScenarioSnapshot scenario, IReadOnlyList<DraftWarRoomEntry> entries)
    {
        var normalized = entries
            .Select((entry, index) => entry with { PersonalRank = index + 1 })
            .ToArray();
        var state = scenario.DraftWarRoom with { BoardEntries = normalized };
        state.Validate();
        var withState = scenario with { DraftWarRoom = state };
        return ShouldEnrichDraftIntelligence(withState)
            ? new DraftIntelligenceService().EnsureDraftIntelligence(withState)
            : withState;
    }

    private static bool ShouldEnrichDraftIntelligence(NewGmScenarioSnapshot scenario) =>
        scenario.PlayerRatings.Count > 0
        || scenario.ScoutedRatings.Count > 0
        || scenario.DevelopmentCurves.Count > 0
        || scenario.ScoutingKnowledgeProfiles.Count > 0;

    private static string ShortRisk(DraftProspectIntelligenceCard card)
    {
        if (card.Alerts.Any(alert => alert.AlertType == DraftIntelligenceAlertType.BustRisk))
        {
            return "bust risk";
        }

        if (card.Alerts.Any(alert => alert.AlertType == DraftIntelligenceAlertType.MedicalRisk))
        {
            return "medical risk";
        }

        return card.PotentialEstimate.IsUnknown ? "uncertain" : "manageable";
    }

    private DraftWarRoomEntry BuildEntry(NewGmScenarioSnapshot scenario, DraftBoardEntry boardEntry, DraftWarRoomEntry? existing)
    {
        var tags = existing?.Tags
            ?? (boardEntry.IsStarred ? new[] { DraftWatchTag.Priority, DraftWatchTag.Pinned } : Array.Empty<DraftWatchTag>());
        var entry = new DraftWarRoomEntry(
            boardEntry.ProspectPersonId,
            PersonName(scenario, boardEntry.ProspectPersonId),
            existing?.PersonalRank ?? boardEntry.Rank,
            existing?.OriginalRank ?? boardEntry.Rank,
            tags.Distinct().ToArray(),
            existing?.GroupName ?? GroupFor(boardEntry),
            existing?.IsPinned ?? boardEntry.IsStarred,
            existing?.IsFavorite ?? false,
            existing?.IsRemoved ?? false,
            string.IsNullOrWhiteSpace(existing?.GmNotes) ? boardEntry.PersonalNotes : existing!.GmNotes);
        entry.Validate();
        return entry;
    }

    private IReadOnlyList<DraftNeedAnalysis> BuildNeeds(NewGmScenarioSnapshot scenario)
    {
        var aiNeeds = scenario.OrganizationAiProfiles
            .FirstOrDefault(profile => profile.OrganizationId == scenario.Organization.OrganizationId)
            ?.CurrentNeeds
            .Take(5)
            .Select(need => new DraftNeedAnalysis(need.NeedType, Readable(need.NeedType), need.Reason, need.Priority, TargetPosition(need.NeedType)))
            .ToArray();
        if (aiNeeds is { Length: > 0 })
        {
            return aiNeeds;
        }

        var goalies = scenario.AlphaSnapshot.Roster.Players.Count(player => player.Position == RosterPosition.Goalie);
        var defense = scenario.AlphaSnapshot.Roster.Players.Count(player => player.Position == RosterPosition.Defense);
        var needType = goalies < 3 ? TeamNeedType.StartingGoalie : defense < 8 ? TeamNeedType.TopPairDefense : TeamNeedType.TopSixForward;
        return new[]
        {
            new DraftNeedAnalysis(needType, Readable(needType), "Derived from roster balance, prospect pipeline, contracts, age, and development depth.", TradePriority.High, TargetPosition(needType))
        };
    }

    private IReadOnlyList<DraftClassStoryline> BuildStorylines(NewGmScenarioSnapshot scenario)
    {
        var profile = scenario.CurrentDraftClassProfile;
        var lines = new List<DraftClassStoryline>();
        if (profile is not null)
        {
            lines.Add(profile.TopStoryline);
            lines.Add(new DraftClassStoryline("Class Theme", $"{profile.Year} is {profile.ReadableTheme}; {profile.ScoutQuote}"));
            if (profile.GoalieDepth >= DraftClassQuality.AboveAverage)
            {
                lines.Add(new DraftClassStoryline("Top Goalie", "Goalie depth is a visible draft story for the class."));
            }
            if (profile.Theme == DraftClassTheme.BoomBustClass)
            {
                lines.Add(new DraftClassStoryline("Boom/Bust", "Staff believe the class has higher volatility than usual."));
            }
            if (profile.Theme == DraftClassTheme.HighCharacterClass)
            {
                lines.Add(new DraftClassStoryline("Character Leader", "Multiple prospects carry strong character and leadership notes."));
            }
            if (profile.Theme == DraftClassTheme.InternationalHeavyClass)
            {
                lines.Add(new DraftClassStoryline("International Mystery", "International uncertainty is part of the class identity."));
            }
            if (profile.Theme == DraftClassTheme.LocalTalentClass)
            {
                lines.Add(new DraftClassStoryline("Local Star", "Regional prospects are expected to draw extra attention."));
            }
        }

        foreach (var entry in scenario.AlphaSnapshot.DraftBoard.Entries.Where(entry => entry.RiskSummary.Contains("medical", StringComparison.OrdinalIgnoreCase)).Take(1))
        {
            lines.Add(new DraftClassStoryline("Medical Risk", $"{PersonName(scenario, entry.ProspectPersonId)} carries a medical-risk watch note."));
        }

        if (lines.Count == 0)
        {
            lines.Add(new DraftClassStoryline("Late Round Sleeper", "Staff are still sorting the late-round value pocket."));
        }

        return lines.DistinctBy(line => line.Headline).Take(8).ToArray();
    }

    private IReadOnlyList<DraftDepartmentOpinion> BuildBestPlayerAvailableOpinions(NewGmScenarioSnapshot scenario)
    {
        var board = scenario.AlphaSnapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank).ToArray();
        DraftDepartmentOpinion Build(string department, DraftBoardEntry? entry, string fallback)
        {
            var chosen = entry ?? board.FirstOrDefault();
            return chosen is null
                ? new DraftDepartmentOpinion(department, "none", "No prospect", fallback, ScoutingConfidenceLevel.Unknown)
                : Opinion(department, chosen.ProspectPersonId, PersonName(scenario, chosen.ProspectPersonId), fallback, chosen.ScoutingConfidence ?? ScoutingConfidenceLevel.Unknown);
        }

        return new[]
        {
            Build("Head Scout", board.FirstOrDefault(), "Best player available on the current board."),
            Build("Regional Scout", board.Skip(1).FirstOrDefault(), "Best regional fit among top board options."),
            Build("Analytics", board.OrderByDescending(entry => entry.ScoutingConfidence).ThenBy(entry => entry.Rank).FirstOrDefault(), "Best evidence profile without hidden ratings."),
            Build("Medical", board.FirstOrDefault(entry => !entry.RiskSummary.Contains("medical", StringComparison.OrdinalIgnoreCase)), "Prefer the cleanest medical profile in the top tier."),
            Build("Development", board.FirstOrDefault(entry => entry.Bio?.PotentialLineupProjection.Contains("upside", StringComparison.OrdinalIgnoreCase) == true) ?? board.FirstOrDefault(), "Best development runway.")
        };
    }

    private IReadOnlyList<DraftWarRoomBoardSnapshot> BuildOriginalBoardSnapshot(NewGmScenarioSnapshot scenario) =>
        scenario.AlphaSnapshot.DraftBoard.Entries
            .OrderBy(entry => entry.Rank)
            .Select(entry =>
            {
                var snapshot = new DraftWarRoomBoardSnapshot(
                    entry.Rank,
                    entry.ProspectPersonId,
                    PersonName(scenario, entry.ProspectPersonId),
                    entry.Bio?.Position ?? RosterPosition.Unknown,
                    entry.ProjectionText,
                    entry.ScoutingConfidence,
                    entry.PersonalNotes);
                snapshot.Validate();
                return snapshot;
            })
            .ToArray();

    private DraftProspectComparisonItem BuildComparisonItem(NewGmScenarioSnapshot scenario, string prospectId)
    {
        var entry = scenario.AlphaSnapshot.DraftBoard.Entries.FirstOrDefault(item => item.ProspectPersonId == prospectId)
            ?? throw new ArgumentException("Prospect is not available on the draft board.", nameof(prospectId));
        var bio = entry.Bio;
        var reports = new ScoutingIntelligenceService().BuildReportCards(scenario, prospectId, scenario.LeagueProfile.Rulebook);
        var first = reports.FirstOrDefault();
        var item = new DraftProspectComparisonItem(
            prospectId,
            PersonName(scenario, prospectId),
            bio?.Position ?? RosterPosition.Unknown,
            scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == prospectId)?.CalculateAge(scenario.CurrentDate),
            bio?.HeightDisplay ?? "unknown",
            bio?.WeightDisplay ?? "unknown",
            bio is null ? "unknown team/league" : $"{bio.CurrentTeam} / {bio.League}",
            entry.ScoutingConfidence,
            first?.FutureProjection ?? entry.ProjectionText,
            bio?.CharacterSummary ?? first?.Evidence.FirstOrDefault() ?? "character picture incomplete",
            bio?.PotentialLineupProjection ?? first?.CurrentPicture ?? "development path incomplete",
            entry.RiskSummary.Contains("medical", StringComparison.OrdinalIgnoreCase) ? entry.RiskSummary : "No major medical note in public scouting.",
            entry.ClassContextNote);
        item.Validate();
        return item;
    }

    private static DraftDepartmentOpinion Opinion(string department, string prospectId, string name, string opinion, ScoutingConfidenceLevel confidence)
    {
        var result = new DraftDepartmentOpinion(department, prospectId, name, opinion, confidence);
        result.Validate();
        return result;
    }

    private static string GroupFor(DraftBoardEntry entry) =>
        entry.Rank <= 10 ? "Top Prospects" :
        entry.Rank <= 30 ? "Core Board" :
        entry.ScoutingConfidence <= ScoutingConfidenceLevel.Low ? "Watch List" :
        "Late Board";

    private static string RegionOpinion(DraftBoardEntry? entry) =>
        entry?.Bio is null ? "Regional picture needs more live viewings." : $"Regional scout likes the {entry.Bio.CurrentTeam} / {entry.Bio.League} evidence trail.";

    private static string DevelopmentOpinion(DraftBoardEntry? entry) =>
        entry?.Bio?.PotentialLineupProjection ?? "Development staff need more projection evidence.";

    private static string MedicalOpinion(DraftBoardEntry? entry) =>
        entry?.RiskSummary.Contains("medical", StringComparison.OrdinalIgnoreCase) == true
            ? entry.RiskSummary
            : "No major medical concern in the current draft-room file.";

    private static string GradeForDraft(IReadOnlyList<DraftPickSummary> selections) =>
        selections.Count switch
        {
            >= 4 => "A-",
            3 => "B+",
            2 => "B",
            1 => "C+",
            _ => "Incomplete"
        };

    private static bool NeedPositionMatch(TeamNeedType need, RosterPosition position) =>
        need switch
        {
            TeamNeedType.StartingGoalie or TeamNeedType.BackupGoalie => position == RosterPosition.Goalie,
            TeamNeedType.TopPairDefense or TeamNeedType.DefensiveDefenseman => position == RosterPosition.Defense,
            TeamNeedType.TopSixForward or TeamNeedType.Scoring or TeamNeedType.Physicality or TeamNeedType.VeteranLeadership => position is RosterPosition.Center or RosterPosition.LeftWing or RosterPosition.RightWing,
            TeamNeedType.Prospects or TeamNeedType.DraftPicks => true,
            _ => false
        };

    private static RosterPosition? TargetPosition(TeamNeedType need) =>
        need switch
        {
            TeamNeedType.StartingGoalie or TeamNeedType.BackupGoalie => RosterPosition.Goalie,
            TeamNeedType.TopPairDefense or TeamNeedType.DefensiveDefenseman => RosterPosition.Defense,
            TeamNeedType.TopSixForward or TeamNeedType.Scoring or TeamNeedType.Physicality => RosterPosition.Center,
            _ => null
        };

    private static int PriorityScore(TradePriority priority) =>
        priority switch
        {
            TradePriority.Urgent => 55,
            TradePriority.High => 35,
            TradePriority.Medium => 20,
            TradePriority.Low => 8,
            _ => 0
        };

    private static string PersonName(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? personId;

    private static string Readable(Enum value)
    {
        var text = value.ToString();
        return string.Concat(text.Select((letter, index) => index > 0 && char.IsUpper(letter) ? $" {letter}" : letter.ToString()));
    }
}
