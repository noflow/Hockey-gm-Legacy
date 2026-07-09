using LegacyEngine.Rosters;
using LegacyEngine.Scouting;
using LegacyEngine.Staff;

namespace LegacyEngine.Integration;

public sealed partial class ScoutingIntelligenceService
{
    private const int StaleReportDays = 45;

    public ScoutingKnowledgeProfile CreateKnowledgeProfile(NewGmScenarioSnapshot scenario, string playerId)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        if (string.IsNullOrWhiteSpace(playerId))
        {
            throw new ArgumentException("Player id is required.", nameof(playerId));
        }

        var prepared = new HockeyIntelligenceRatingService().EnsureRatings(scenario);
        var playerName = PersonNameForKnowledge(prepared, playerId);
        var scouted = prepared.ScoutedRatings.FirstOrDefault(rating => rating.PersonId == playerId);
        var existing = prepared.ScoutingKnowledgeProfiles.FirstOrDefault(profile =>
            profile.OrganizationId == prepared.Organization.OrganizationId && profile.PlayerId == playerId);
        if (existing is not null)
        {
            return RefreshStaleProfile(prepared, existing);
        }

        var hasCompletedReport = prepared.CompletedScoutingReports.Any(report => report.PlayerId == playerId);
        var isRosterKnown = prepared.AlphaSnapshot.Roster.FindPlayer(playerId) is not null;
        var attributes = (scouted?.Attributes ?? Array.Empty<PlayerScoutedAttributeRating>())
            .Select(attribute =>
            {
                var publicOnly = !hasCompletedReport && !isRosterKnown;
                var estimate = publicOnly ? PlayerRatingRange.Unknown : attribute.Range;
                var color = publicOnly ? PlayerRatingColor.Unknown : attribute.ConfidenceColor;
                return new AttributeKnowledgeState(
                    attribute.Attribute,
                    attribute.Category,
                    estimate,
                    color,
                    publicOnly ? null : attribute.LastUpdated,
                    null,
                    publicOnly ? "Public file" : scouted!.ScoutSource,
                    0,
                    false,
                    publicOnly ? "Attribute not scouted yet." : attribute.ScoutNote);
            })
            .ToArray();

        if (attributes.Length == 0)
        {
            attributes = BaselineUnknownAttributes(ResolvePositionForKnowledge(prepared, playerId))
                .Select(attribute => new AttributeKnowledgeState(
                    attribute.Attribute,
                    attribute.Category,
                    PlayerRatingRange.Unknown,
                    PlayerRatingColor.Unknown,
                    null,
                    null,
                    "Public file",
                    0,
                    false,
                    "Attribute not scouted yet."))
                .ToArray();
        }

        var consensus = BuildConsensus(prepared, playerId, attributes, Array.Empty<ScoutAttributeOpinion>());
        var profile = new ScoutingKnowledgeProfile(
            prepared.Organization.OrganizationId,
            playerId,
            playerName,
            ResolvePositionForKnowledge(prepared, playerId),
            prepared.CurrentDate,
            prepared.CurrentDate,
            attributes,
            Array.Empty<ScoutAttributeOpinion>(),
            consensus,
            false,
            "Assign a scout for a live viewing to replace public estimates with organization-specific intelligence.");
        profile.Validate();
        return profile;
    }

    public NewGmScenarioSnapshot EnsureKnowledgeProfiles(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var prepared = new HockeyIntelligenceRatingService().EnsureRatings(scenario);
        var existing = prepared.ScoutingKnowledgeProfiles.ToDictionary(profile => (profile.OrganizationId, profile.PlayerId));
        var profiles = prepared.AlphaSnapshot.DraftBoard.Entries
            .Select(entry =>
            {
                var key = (prepared.Organization.OrganizationId, entry.ProspectPersonId);
                return existing.TryGetValue(key, out var profile) ? RefreshStaleProfile(prepared, profile) : CreateKnowledgeProfile(prepared, entry.ProspectPersonId);
            })
            .Concat(prepared.ScoutingKnowledgeProfiles.Where(profile => prepared.AlphaSnapshot.DraftBoard.Entries.All(entry => entry.ProspectPersonId != profile.PlayerId)))
            .GroupBy(profile => (profile.OrganizationId, profile.PlayerId))
            .Select(group => group.First())
            .OrderBy(profile => profile.PlayerName, StringComparer.Ordinal)
            .ToArray();
        var updated = prepared with { ScoutingKnowledgeProfiles = profiles };
        updated.Validate();
        return updated;
    }

    public ScoutingKnowledgeUpdate UpdateKnowledgeFromReport(NewGmScenarioSnapshot scenario, ScoutingReport report, ScoutingOperationAssignment? assignment = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(report);
        report.Validate();

        var scout = FindScoutStaff(scenario, report.ScoutId);
        var quality = EffectiveScoutQuality(scout, assignment);
        var specialty = SpecialtyCategory(scout, assignment, ResolvePositionForKnowledge(scenario, report.PlayerId));
        var regionFit = RegionFit(scenario, scout, assignment, report.PlayerId);
        var rated = new HockeyIntelligenceRatingService().RecordScoutingReport(
            scenario,
            report.PlayerId,
            quality,
            specialty,
            string.IsNullOrWhiteSpace(assignment?.ScoutName) ? PersonNameForKnowledge(scenario, report.ScoutId) : assignment!.ScoutName,
            regionFit);
        var scouted = rated.ScoutedRatings.First(rating => rating.PersonId == report.PlayerId);
        var current = CreateKnowledgeProfile(rated, report.PlayerId);
        var previousAttributes = current.Attributes.ToDictionary(attribute => (attribute.Category, attribute.Attribute));
        var countToUpdate = AttributeUpdateCount(quality, assignment, regionFit, scouted.Attributes.Count);
        var preferredCategories = PreferredCategories(scout, assignment, scouted.Position).ToArray();
        var selected = scouted.Attributes
            .OrderByDescending(attribute => preferredCategories.Contains(attribute.Category))
            .ThenBy(attribute => attribute.Category)
            .ThenBy(attribute => attribute.Attribute)
            .Take(countToUpdate)
            .ToArray();
        var selectedKeys = selected.Select(attribute => (attribute.Category, attribute.Attribute)).ToHashSet();
        var bias = BiasForScout(scout, assignment);
        var opinions = selected
            .Select(attribute => BuildOpinion(rated, scouted, scout, attribute, bias, report.CreatedOn))
            .ToArray();
        var attributes = scouted.Attributes
            .Select(attribute =>
            {
                var attributeKey = (attribute.Category, attribute.Attribute);
                if (!selectedKeys.Contains(attributeKey) && previousAttributes.TryGetValue(attributeKey, out var existing))
                {
                    return RefreshAttributeStaleFlag(rated, existing);
                }

                var opinion = opinions.First(item => item.Attribute == attribute.Attribute);
                var previousDisagreement = previousAttributes.TryGetValue(attributeKey, out var existingState)
                    ? existingState.DisagreementLevel
                    : 0;
                var disagreement = CalculateAttributeDisagreement(current.ScoutOpinions.Where(item => item.Attribute == attribute.Attribute).Append(opinion));
                return new AttributeKnowledgeState(
                    attribute.Attribute,
                    attribute.Category,
                    opinion.Estimate,
                    opinion.ConfidenceColor,
                    report.CreatedOn,
                    scout.PersonId,
                    PersonNameForKnowledge(rated, scout.PersonId),
                    Math.Max(previousDisagreement, disagreement),
                    false,
                    opinion.Note);
            })
            .ToArray();
        var combinedOpinions = current.ScoutOpinions
            .Concat(opinions)
            .GroupBy(opinion => (opinion.ScoutId, opinion.Attribute))
            .Select(group => group.OrderByDescending(opinion => opinion.Date).First())
            .OrderByDescending(opinion => opinion.Date)
            .ThenBy(opinion => opinion.ScoutName, StringComparer.Ordinal)
            .ToArray();
        var consensus = BuildConsensus(rated, report.PlayerId, attributes, combinedOpinions);
        var profile = new ScoutingKnowledgeProfile(
            rated.Organization.OrganizationId,
            report.PlayerId,
            PersonNameForKnowledge(rated, report.PlayerId),
            scouted.Position,
            current.CreatedOn,
            report.CreatedOn,
            attributes,
            combinedOpinions,
            consensus,
            false,
            NextActionFor(consensus, attributes));
        profile.Validate();
        var profiles = rated.ScoutingKnowledgeProfiles
            .Where(item => !(item.OrganizationId == rated.Organization.OrganizationId && item.PlayerId == report.PlayerId))
            .Append(profile)
            .OrderBy(item => item.PlayerName, StringComparer.Ordinal)
            .ToArray();
        var history = UpdateScoutAccuracyHistory(rated, scout, bias, quality);
        var updated = rated with
        {
            ScoutingKnowledgeProfiles = profiles,
            ScoutAccuracyHistory = history
        };
        updated.Validate();
        var actionItems = BuildActionItems(updated)
            .Where(item => item.RelatedPersonId == report.PlayerId)
            .ToArray();
        var result = new ScoutingKnowledgeUpdate(updated, profile, consensus, actionItems, $"{profile.PlayerName}: scouting knowledge updated by {PersonNameForKnowledge(updated, scout.PersonId)}.");
        result.Validate();
        return result;
    }

    public IReadOnlyList<string> BuildKnowledgeDossierLines(NewGmScenarioSnapshot scenario, string personId)
    {
        var profile = CreateKnowledgeProfile(scenario, personId);
        var lines = new List<string>
        {
            $"Scouting intelligence: OVR {profile.Consensus.OverallEstimate.Display} | POT {profile.Consensus.PotentialEstimate.Display} | Confidence {profile.Consensus.ConfidenceColor}.",
            $"Known attributes: {profile.KnownAttributeCount}/{profile.Attributes.Count}; stale: {(profile.IsStale ? "yes" : "no")}.",
            $"Consensus: {profile.Consensus.Summary}",
            $"Disagreement: {profile.Consensus.BiggestDisagreement}",
            $"Recommended next action: {profile.RecommendedNextAction}"
        };
        lines.Add("Attribute confidence grid:");
        foreach (var group in profile.Attributes.GroupBy(attribute => attribute.Category).OrderBy(group => group.Key))
        {
            var summary = string.Join(", ", group.Take(4).Select(attribute => $"{ReadableAttribute(attribute.Attribute)} {attribute.Estimate.Display} {attribute.ConfidenceColor}"));
            lines.Add($"{group.Key}: {summary}");
        }

        var opinions = profile.ScoutOpinions.OrderByDescending(opinion => opinion.Date).Take(4).ToArray();
        if (opinions.Length > 0)
        {
            lines.Add("Scout-specific opinions:");
            lines.AddRange(opinions.Select(opinion => $"{opinion.ScoutName}: {ReadableAttribute(opinion.Attribute)} {opinion.Estimate.Display} {opinion.ConfidenceColor} - {opinion.Note}"));
        }

        lines.Add("Internal engine ratings are not shown; these are organization scouting estimates.");
        return lines;
    }

    public IReadOnlyList<ActionCenterItem> BuildActionItems(NewGmScenarioSnapshot scenario)
    {
        var items = new List<ActionCenterItem>();
        var profiles = scenario.ScoutingKnowledgeProfiles.Count == 0
            ? scenario.AlphaSnapshot.DraftBoard.Entries.Take(8).Select(entry => CreateKnowledgeProfile(scenario, entry.ProspectPersonId)).ToArray()
            : scenario.ScoutingKnowledgeProfiles;
        foreach (var profile in profiles.OrderBy(profile => profile.Consensus.DisagreementLevel).Reverse().Take(12))
        {
            if (profile.Consensus.DisagreementLevel >= 32)
            {
                items.Add(Action(profile, "Scout disagreement on top prospect", ActionCenterPriority.Important, profile.Consensus.BiggestDisagreement, "A divided room can lead to a miss or a steal if the GM resolves the uncertainty.", "Review the dossier and send a different scout for a second opinion."));
            }
            else if (profile.IsStale && IsPriorityProspect(scenario, profile.PlayerId))
            {
                items.Add(Action(profile, "Stale report on priority prospect", ActionCenterPriority.Important, "The latest intelligence is older than the scouting freshness target.", "Draft and contract decisions become riskier when estimates are stale.", "Assign a scout for an updated viewing."));
            }
            else if (!profile.Consensus.PotentialEstimate.IsUnknown && profile.Consensus.PotentialEstimate.Low!.Value >= 86 && profile.Consensus.ConfidenceColor <= PlayerRatingColor.Green)
            {
                items.Add(Action(profile, "Hidden gem candidate", ActionCenterPriority.Normal, $"{profile.PlayerName} has upside indicators but limited certainty.", "A good follow-up could move the prospect up the board before rivals catch up.", "Give the prospect a priority scouting assignment."));
            }
            else if (!profile.Consensus.PotentialEstimate.IsUnknown && profile.Consensus.PotentialEstimate.High!.Value <= 75 && profile.Consensus.ConfidenceColor >= PlayerRatingColor.Blue && IsPriorityProspect(scenario, profile.PlayerId))
            {
                items.Add(Action(profile, "Bust warning on board player", ActionCenterPriority.Important, $"{profile.PlayerName}'s projection has tightened below the current board slot.", "Drafting without adjusting the board could waste a high pick.", "Review draft board rank and scouting notes."));
            }
        }

        return items
            .GroupBy(item => item.ActionCenterItemId, StringComparer.Ordinal)
            .Select(group => group.First())
            .Take(6)
            .ToArray();
    }

    public ScoutingConsensus BuildConsensusFromKnowledge(NewGmScenarioSnapshot scenario, string playerId) =>
        CreateKnowledgeProfile(scenario, playerId).Consensus;

    public IReadOnlyList<string> BuildWarRoomKnowledgeLines(NewGmScenarioSnapshot scenario)
    {
        var profiles = scenario.AlphaSnapshot.DraftBoard.Entries
            .OrderBy(entry => entry.Rank)
            .Take(10)
            .Select(entry => CreateKnowledgeProfile(scenario, entry.ProspectPersonId))
            .ToArray();
        var divided = profiles.FirstOrDefault(profile => profile.Consensus.DisagreementLevel >= 32);
        var gem = profiles.FirstOrDefault(profile => profile.Consensus.PotentialEstimate.Low >= 86 && profile.Consensus.ConfidenceColor <= PlayerRatingColor.Green);
        return new[]
        {
            divided is null ? "Consensus board: no major top-board disagreement flagged." : $"Consensus board: divided opinion on {divided.PlayerName} ({divided.Consensus.BiggestDisagreement}).",
            gem is null ? "Hidden gems: none above the alert threshold." : $"Hidden gem candidate: {gem.PlayerName}, POT {gem.Consensus.PotentialEstimate.Display}, confidence {gem.Consensus.ConfidenceColor}.",
            $"Scout board: {profiles.Count(profile => profile.KnownAttributeCount > 0)} of {profiles.Length} top prospects have attribute-level intelligence."
        };
    }

    private static ActionCenterItem Action(ScoutingKnowledgeProfile profile, string title, ActionCenterPriority priority, string reason, string consequence, string recommendation) =>
        new(
            $"action-center:scouting-knowledge:{profile.PlayerId}:{StableId(title)}",
            title,
            ActionCenterCategory.Scouting,
            priority,
            profile.LastViewedDate.AddDays(7),
            profile.PlayerId,
            profile.PlayerName,
            profile.OrganizationId,
            null,
            reason,
            consequence,
            recommendation,
            null,
            null,
            null);

    private static ScoutAttributeOpinion BuildOpinion(
        NewGmScenarioSnapshot scenario,
        PlayerScoutedRatings scouted,
        StaffMember scout,
        PlayerScoutedAttributeRating attribute,
        ScoutingBias bias,
        DateOnly date)
    {
        var adjusted = ApplyBias(attribute.Range, bias, attribute.Category, attribute.Attribute);
        var note = BiasNote(bias, attribute.Category, attribute.Attribute);
        var opinion = new ScoutAttributeOpinion(
            $"scout-opinion:{scout.PersonId}:{scouted.PersonId}:{attribute.Attribute}:{date:yyyyMMdd}",
            scout.PersonId,
            PersonNameForKnowledge(scenario, scout.PersonId),
            scouted.PersonId,
            scouted.PlayerName,
            attribute.Attribute,
            adjusted,
            attribute.ConfidenceColor,
            bias,
            date,
            note);
        opinion.Validate();
        return opinion;
    }

    private static PlayerRatingRange ApplyBias(PlayerRatingRange range, ScoutingBias bias, PlayerRatingCategory category, PlayerAttributeKey attribute)
    {
        if (range.IsUnknown)
        {
            return range;
        }

        var shift = bias switch
        {
            ScoutingBias.Optimistic => 2,
            ScoutingBias.Conservative => -2,
            ScoutingBias.OvervaluesSize when category == PlayerRatingCategory.Physical => 3,
            ScoutingBias.OvervaluesSkating when category == PlayerRatingCategory.Skating => 3,
            ScoutingBias.UnderestimatesSkill when category is PlayerRatingCategory.Offensive or PlayerRatingCategory.Skill => -3,
            ScoutingBias.StrongGoalieEvaluator when category == PlayerRatingCategory.Goalie => 1,
            ScoutingBias.StrongCharacterEvaluator when category is PlayerRatingCategory.Mental or PlayerRatingCategory.Team => 1,
            ScoutingBias.PoorProjectionScout => -1,
            ScoutingBias.SleeperFinder when attribute is PlayerAttributeKey.WorkEthic or PlayerAttributeKey.Coachability or PlayerAttributeKey.HockeyIQ => 2,
            ScoutingBias.SafePickScout when category is PlayerRatingCategory.Mental or PlayerRatingCategory.Defensive => 1,
            _ => 0
        };
        return new PlayerRatingRange(Math.Clamp(range.Low!.Value + shift, 0, 100), Math.Clamp(range.High!.Value + shift, 0, 100));
    }

    private static ScoutingConsensus BuildConsensus(
        NewGmScenarioSnapshot scenario,
        string playerId,
        IReadOnlyList<AttributeKnowledgeState> attributes,
        IReadOnlyList<ScoutAttributeOpinion> opinions)
    {
        var scouted = scenario.ScoutedRatings.FirstOrDefault(rating => rating.PersonId == playerId);
        var knownAttributes = attributes.Where(attribute => attribute.IsKnown).ToArray();
        var color = knownAttributes.Length == 0
            ? scouted?.ConfidenceColor ?? PlayerRatingColor.Unknown
            : knownAttributes.GroupBy(attribute => attribute.ConfidenceColor).OrderByDescending(group => group.Count()).First().Key;
        var disagreement = Math.Max(
            knownAttributes.Select(attribute => attribute.DisagreementLevel).DefaultIfEmpty(0).Max(),
            CalculateOverallDisagreement(opinions));
        var biggest = disagreement == 0
            ? "No meaningful scout disagreement yet."
            : BiggestDisagreement(opinions);
        var consensus = new ScoutingConsensus(
            playerId,
            PersonNameForKnowledge(scenario, playerId),
            scouted?.Overall ?? PlayerRatingRange.Unknown,
            scouted?.Potential ?? PlayerRatingRange.Unknown,
            color,
            disagreement,
            SummaryFor(knownAttributes.Length, attributes.Count, color, disagreement),
            biggest,
            opinions);
        consensus.Validate();
        return consensus;
    }

    private static IReadOnlyList<ScoutAccuracyRecord> UpdateScoutAccuracyHistory(NewGmScenarioSnapshot scenario, StaffMember scout, ScoutingBias bias, int quality)
    {
        var existing = scenario.ScoutAccuracyHistory.FirstOrDefault(record => record.ScoutId == scout.PersonId);
        var hit = quality >= 70 ? 1 : 0;
        var miss = quality < 50 ? 1 : 0;
        var gem = bias == ScoutingBias.SleeperFinder && quality >= 60 ? 1 : 0;
        var bust = bias == ScoutingBias.Optimistic && quality < 55 ? 1 : 0;
        var updated = existing is null
            ? new ScoutAccuracyRecord(
                scout.PersonId,
                PersonNameForKnowledge(scenario, scout.PersonId),
                hit,
                miss,
                gem,
                bust,
                StrongestCategory(scout),
                WeakestCategory(scout),
                AccuracyFor(quality),
                $"Early track record: {AccuracyFor(quality)} accuracy profile.")
            : existing with
            {
                CorrectHits = existing.CorrectHits + hit,
                Misses = existing.Misses + miss,
                GemsFound = existing.GemsFound + gem,
                BustsRecommended = existing.BustsRecommended + bust,
                Accuracy = AccuracyFor((quality + AccuracyScore(existing.Accuracy)) / 2),
                Summary = $"Tracked scouting trust: {existing.CorrectHits + hit} hits, {existing.Misses + miss} misses, {existing.GemsFound + gem} gems found."
            };
        updated.Validate();
        return scenario.ScoutAccuracyHistory
            .Where(record => record.ScoutId != scout.PersonId)
            .Append(updated)
            .OrderBy(record => record.ScoutName, StringComparer.Ordinal)
            .ToArray();
    }

    private static ScoutingKnowledgeProfile RefreshStaleProfile(NewGmScenarioSnapshot scenario, ScoutingKnowledgeProfile profile)
    {
        var stale = scenario.CurrentDate.DayNumber - profile.LastViewedDate.DayNumber >= StaleReportDays;
        if (!stale)
        {
            return profile;
        }

        var attributes = profile.Attributes.Select(attribute => attribute with { IsStale = true }).ToArray();
        var refreshed = profile with
        {
            Attributes = attributes,
            IsStale = true,
            RecommendedNextAction = "Report is stale. Send a scout for an updated viewing before relying on this estimate."
        };
        refreshed.Validate();
        return refreshed;
    }

    private static AttributeKnowledgeState RefreshAttributeStaleFlag(NewGmScenarioSnapshot scenario, AttributeKnowledgeState attribute)
    {
        var stale = attribute.LastViewedDate is not null && scenario.CurrentDate.DayNumber - attribute.LastViewedDate.Value.DayNumber >= StaleReportDays;
        return attribute with { IsStale = stale };
    }

    private static IReadOnlyList<PlayerScoutedAttributeRating> BaselineUnknownAttributes(RosterPosition position)
    {
        var categories = position == RosterPosition.Goalie
            ? new[] { PlayerRatingCategory.Goalie, PlayerRatingCategory.Mental, PlayerRatingCategory.Physical, PlayerRatingCategory.Team }
            : new[] { PlayerRatingCategory.Offensive, PlayerRatingCategory.Defensive, PlayerRatingCategory.Skating, PlayerRatingCategory.Physical, PlayerRatingCategory.Skill, PlayerRatingCategory.Mental, PlayerRatingCategory.Team };
        return categories.SelectMany(category => AttributesForCategory(category).Select(attribute =>
            new PlayerScoutedAttributeRating(category, attribute, PlayerRatingRange.Unknown, PlayerRatingColor.Unknown, PlayerRatingSource.Unknown, null, "Attribute not scouted yet."))).ToArray();
    }

    private static IReadOnlyList<PlayerAttributeKey> AttributesForCategory(PlayerRatingCategory category) =>
        category switch
        {
            PlayerRatingCategory.Offensive => new[] { PlayerAttributeKey.Shooting, PlayerAttributeKey.Playmaking, PlayerAttributeKey.PuckSkill, PlayerAttributeKey.HockeyIQ, PlayerAttributeKey.Vision, PlayerAttributeKey.OffensiveAwareness, PlayerAttributeKey.Creativity },
            PlayerRatingCategory.Defensive => new[] { PlayerAttributeKey.Positioning, PlayerAttributeKey.StickChecking, PlayerAttributeKey.ShotBlocking, PlayerAttributeKey.DefensiveAwareness, PlayerAttributeKey.Backchecking, PlayerAttributeKey.BoardPlay },
            PlayerRatingCategory.Skating => new[] { PlayerAttributeKey.Speed, PlayerAttributeKey.Acceleration, PlayerAttributeKey.Agility, PlayerAttributeKey.EdgeWork, PlayerAttributeKey.Balance, PlayerAttributeKey.Endurance },
            PlayerRatingCategory.Physical => new[] { PlayerAttributeKey.Strength, PlayerAttributeKey.Hitting, PlayerAttributeKey.Aggression, PlayerAttributeKey.Grit, PlayerAttributeKey.Toughness, PlayerAttributeKey.Durability },
            PlayerRatingCategory.Skill => new[] { PlayerAttributeKey.Passing, PlayerAttributeKey.HandEye, PlayerAttributeKey.Faceoffs, PlayerAttributeKey.Deception, PlayerAttributeKey.PuckProtection, PlayerAttributeKey.Stickhandling },
            PlayerRatingCategory.Mental => new[] { PlayerAttributeKey.Leadership, PlayerAttributeKey.Consistency, PlayerAttributeKey.Coachability, PlayerAttributeKey.WorkEthic, PlayerAttributeKey.Composure, PlayerAttributeKey.Confidence },
            PlayerRatingCategory.Team => new[] { PlayerAttributeKey.TeamPlay, PlayerAttributeKey.Adaptability, PlayerAttributeKey.Professionalism, PlayerAttributeKey.LockerRoom, PlayerAttributeKey.MentorAbility },
            PlayerRatingCategory.Goalie => new[] { PlayerAttributeKey.Reflexes, PlayerAttributeKey.Glove, PlayerAttributeKey.Blocker, PlayerAttributeKey.ReboundManagement, PlayerAttributeKey.Positioning, PlayerAttributeKey.PuckTracking, PlayerAttributeKey.LateralMovement, PlayerAttributeKey.PuckHandling, PlayerAttributeKey.Recovery, PlayerAttributeKey.Composure, PlayerAttributeKey.Durability, PlayerAttributeKey.WorkEthic },
            _ => Array.Empty<PlayerAttributeKey>()
        };

    private static int EffectiveScoutQuality(StaffMember scout, ScoutingOperationAssignment? assignment)
    {
        var baseQuality = Math.Max(45, scout.Attributes.ScoutingScore(StaffScoutingAttribute.TalentEvaluation));
        if (assignment is null)
        {
            return baseQuality;
        }

        var relationshipPenalty = assignment.RelationshipQualityAtAssignment < 40 ? 10 : assignment.RelationshipQualityAtAssignment < 55 ? 4 : 0;
        var communicationPenalty = assignment.CommunicationQuality < 45 ? 8 : 0;
        var workloadPenalty = assignment.WorkloadAtAssignment >= 3 ? 14 : assignment.WorkloadAtAssignment == 2 ? 6 : 0;
        var priorityBonus = assignment.Priority switch
        {
            ScoutingOperationPriority.Urgent => 6,
            ScoutingOperationPriority.High => 4,
            ScoutingOperationPriority.Low => -2,
            _ => 0
        };
        return Math.Clamp(baseQuality + priorityBonus - relationshipPenalty - communicationPenalty - workloadPenalty, 0, 100);
    }

    private static int AttributeUpdateCount(int quality, ScoutingOperationAssignment? assignment, bool regionFit, int max)
    {
        var duration = assignment?.DurationDays ?? 2;
        var count = 4 + Math.Max(1, duration) * 2 + quality / 18 + (regionFit ? 3 : 0);
        if (assignment?.Priority == ScoutingOperationPriority.High)
        {
            count += 2;
        }

        return Math.Clamp(count, 1, Math.Max(1, max));
    }

    private static PlayerRatingCategory? SpecialtyCategory(StaffMember scout, ScoutingOperationAssignment? assignment, RosterPosition position)
    {
        if (assignment?.TargetRegion == ScoutingRegionFocus.Goalies || position == RosterPosition.Goalie || scout.Attributes.ScoutingScore(StaffScoutingAttribute.GoalieEvaluation) >= 68)
        {
            return PlayerRatingCategory.Goalie;
        }

        if (assignment?.TargetRegion == ScoutingRegionFocus.Defensemen || position == RosterPosition.Defense)
        {
            return PlayerRatingCategory.Defensive;
        }

        if (assignment?.TargetRegion == ScoutingRegionFocus.Character || scout.Attributes.ScoutingScore(StaffScoutingAttribute.CharacterEvaluation) >= 68)
        {
            return PlayerRatingCategory.Mental;
        }

        if (assignment?.TargetRegion == ScoutingRegionFocus.Forwards)
        {
            return PlayerRatingCategory.Offensive;
        }

        return null;
    }

    private static IReadOnlyList<PlayerRatingCategory> PreferredCategories(StaffMember scout, ScoutingOperationAssignment? assignment, RosterPosition position)
    {
        var categories = new List<PlayerRatingCategory>();
        var specialty = SpecialtyCategory(scout, assignment, position);
        if (specialty is not null)
        {
            categories.Add(specialty.Value);
        }

        if (assignment?.TargetRegion == ScoutingRegionFocus.Medical)
        {
            categories.Add(PlayerRatingCategory.Physical);
        }

        categories.Add(PlayerRatingCategory.Skating);
        categories.Add(PlayerRatingCategory.Mental);
        categories.Add(PlayerRatingCategory.Offensive);
        categories.Add(PlayerRatingCategory.Defensive);
        categories.Add(PlayerRatingCategory.Physical);
        return categories.Distinct().ToArray();
    }

    private static bool RegionFit(NewGmScenarioSnapshot scenario, StaffMember scout, ScoutingOperationAssignment? assignment, string playerId)
    {
        if (assignment?.TargetRegion is null)
        {
            return false;
        }

        if (assignment.TargetRegion is ScoutingRegionFocus.Goalies or ScoutingRegionFocus.Defensemen or ScoutingRegionFocus.Forwards or ScoutingRegionFocus.Character or ScoutingRegionFocus.Medical)
        {
            return true;
        }

        var bio = scenario.AlphaSnapshot.DraftBoard.Entries.FirstOrDefault(entry => entry.ProspectPersonId == playerId)?.Bio;
        if (bio is null)
        {
            return scout.Attributes.ScoutingScore(StaffScoutingAttribute.RegionalKnowledge) >= 62;
        }

        return assignment.TargetRegion switch
        {
            ScoutingRegionFocus.WesternCanada => bio.ProvinceState.Contains("BC", StringComparison.OrdinalIgnoreCase) || bio.ProvinceState.Contains("Alberta", StringComparison.OrdinalIgnoreCase) || bio.ProvinceState.Contains("Sask", StringComparison.OrdinalIgnoreCase) || bio.ProvinceState.Contains("Manitoba", StringComparison.OrdinalIgnoreCase),
            ScoutingRegionFocus.EasternCanada => bio.ProvinceState.Contains("Ontario", StringComparison.OrdinalIgnoreCase) || bio.ProvinceState.Contains("Quebec", StringComparison.OrdinalIgnoreCase) || bio.ProvinceState.Contains("Maritime", StringComparison.OrdinalIgnoreCase),
            ScoutingRegionFocus.USA => bio.Country.Contains("USA", StringComparison.OrdinalIgnoreCase) || bio.Country.Contains("United States", StringComparison.OrdinalIgnoreCase),
            ScoutingRegionFocus.Europe => bio.Country is not ("Canada" or "USA" or "United States") || bio.League.Contains("U20", StringComparison.OrdinalIgnoreCase) || bio.CurrentTeam.Contains("Zurich", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static ScoutingBias BiasForScout(StaffMember scout, ScoutingOperationAssignment? assignment)
    {
        if (scout.Attributes.ScoutingScore(StaffScoutingAttribute.GoalieEvaluation) >= 72)
        {
            return ScoutingBias.StrongGoalieEvaluator;
        }

        if (scout.Attributes.ScoutingScore(StaffScoutingAttribute.CharacterEvaluation) >= 72)
        {
            return ScoutingBias.StrongCharacterEvaluator;
        }

        if (assignment?.TargetRegion == ScoutingRegionFocus.Europe && scout.Attributes.ScoutingScore(StaffScoutingAttribute.EuropeanKnowledge) >= 68)
        {
            return ScoutingBias.ExcellentRegionalScout;
        }

        var seed = Math.Abs(HashCode.Combine(scout.PersonId, assignment?.TargetName ?? "general")) % 8;
        return seed switch
        {
            0 => ScoutingBias.Optimistic,
            1 => ScoutingBias.Conservative,
            2 => ScoutingBias.OvervaluesSize,
            3 => ScoutingBias.OvervaluesSkating,
            4 => ScoutingBias.UnderestimatesSkill,
            5 => ScoutingBias.PoorProjectionScout,
            6 => ScoutingBias.SleeperFinder,
            _ => ScoutingBias.SafePickScout
        };
    }

    private static PlayerRatingCategory StrongestCategory(StaffMember scout)
    {
        var values = new Dictionary<PlayerRatingCategory, int>
        {
            [PlayerRatingCategory.Offensive] = scout.Attributes.ScoutingScore(StaffScoutingAttribute.TalentEvaluation),
            [PlayerRatingCategory.Mental] = scout.Attributes.ScoutingScore(StaffScoutingAttribute.CharacterEvaluation),
            [PlayerRatingCategory.Goalie] = scout.Attributes.ScoutingScore(StaffScoutingAttribute.GoalieEvaluation),
            [PlayerRatingCategory.Team] = scout.Attributes.ScoutingScore(StaffScoutingAttribute.RegionalKnowledge)
        };
        return values.OrderByDescending(item => item.Value).First().Key;
    }

    private static PlayerRatingCategory WeakestCategory(StaffMember scout)
    {
        var values = new Dictionary<PlayerRatingCategory, int>
        {
            [PlayerRatingCategory.Offensive] = scout.Attributes.ScoutingScore(StaffScoutingAttribute.TalentEvaluation),
            [PlayerRatingCategory.Mental] = scout.Attributes.ScoutingScore(StaffScoutingAttribute.CharacterEvaluation),
            [PlayerRatingCategory.Goalie] = scout.Attributes.ScoutingScore(StaffScoutingAttribute.GoalieEvaluation),
            [PlayerRatingCategory.Team] = scout.Attributes.ScoutingScore(StaffScoutingAttribute.RegionalKnowledge)
        };
        return values.OrderBy(item => item.Value).First().Key;
    }

    private static StaffMember FindScoutStaff(NewGmScenarioSnapshot scenario, string scoutId) =>
        scenario.StaffMembers.FirstOrDefault(member => member.PersonId == scoutId && member.Department == StaffDepartment.Scouting)
        ?? scenario.StaffMembers.FirstOrDefault(member => member.Department == StaffDepartment.Scouting)
        ?? throw new ArgumentException("Scout staff member was not found.", nameof(scoutId));

    private static RosterPosition ResolvePositionForKnowledge(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.Roster.FindPlayer(personId)?.Position
        ?? scenario.AlphaSnapshot.DraftBoard.Entries.FirstOrDefault(entry => entry.ProspectPersonId == personId)?.Bio?.Position
        ?? scenario.ProspectRights.FirstOrDefault(record => record.ProspectPersonId == personId)?.Position
        ?? scenario.FreeAgentMarket?.Find(personId)?.Position
        ?? scenario.TradeBlock?.Find(personId)?.Position
        ?? RosterPosition.Unknown;

    private static string PersonNameForKnowledge(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? scenario.ProspectRights.FirstOrDefault(record => record.ProspectPersonId == personId)?.ProspectName
        ?? scenario.FreeAgentMarket?.Find(personId)?.Name
        ?? scenario.TradeBlock?.Find(personId)?.Name
        ?? personId;

    private static bool IsPriorityProspect(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.DraftBoard.Entries.Any(entry => entry.ProspectPersonId == personId && entry.Rank <= 20)
        || scenario.DraftWarRoom.BoardEntries.Any(entry => entry.ProspectPersonId == personId && entry.Tags.Count > 0);

    private static int CalculateAttributeDisagreement(IEnumerable<ScoutAttributeOpinion> opinions)
    {
        var ranges = opinions.Where(opinion => !opinion.Estimate.IsUnknown).Select(opinion => opinion.Estimate.Midpoint).ToArray();
        return ranges.Length < 2 ? 0 : ranges.Max() - ranges.Min();
    }

    private static int CalculateOverallDisagreement(IReadOnlyList<ScoutAttributeOpinion> opinions)
    {
        if (opinions.Count < 2)
        {
            return 0;
        }

        return opinions
            .GroupBy(opinion => opinion.Attribute)
            .Select(CalculateAttributeDisagreement)
            .DefaultIfEmpty(0)
            .Max();
    }

    private static string BiggestDisagreement(IReadOnlyList<ScoutAttributeOpinion> opinions)
    {
        var largest = opinions
            .GroupBy(opinion => opinion.Attribute)
            .Select(group => new { Attribute = group.Key, Gap = CalculateAttributeDisagreement(group), Opinions = group.ToArray() })
            .OrderByDescending(group => group.Gap)
            .FirstOrDefault(group => group.Gap > 0);
        if (largest is null)
        {
            return "No meaningful scout disagreement yet.";
        }

        return $"{ReadableAttribute(largest.Attribute)} has a {largest.Gap}-point scout spread across {largest.Opinions.Length} report(s).";
    }

    private static string SummaryFor(int known, int total, PlayerRatingColor color, int disagreement)
    {
        if (known == 0)
        {
            return "Only public bio and board context are available.";
        }

        var disagreementText = disagreement >= 32 ? "room is divided" : disagreement >= 18 ? "some disagreement remains" : "room is aligned";
        return $"{known}/{total} attributes have organization scouting detail; confidence is {color} and the {disagreementText}.";
    }

    private static string NextActionFor(ScoutingConsensus consensus, IReadOnlyList<AttributeKnowledgeState> attributes)
    {
        if (consensus.DisagreementLevel >= 32)
        {
            return "Assign a different scout for a second opinion.";
        }

        if (attributes.Count(attribute => attribute.IsKnown) < attributes.Count / 2)
        {
            return "Keep scouting to fill attribute gaps.";
        }

        if (consensus.ConfidenceColor <= PlayerRatingColor.Green)
        {
            return "Schedule another viewing to tighten the OVR/POT range.";
        }

        return "Use this report to adjust draft board, prospect plan, or contract decisions.";
    }

    private static string BiasNote(ScoutingBias bias, PlayerRatingCategory category, PlayerAttributeKey attribute) =>
        bias switch
        {
            ScoutingBias.Optimistic => $"Optimistic scout read on {ReadableAttribute(attribute)}.",
            ScoutingBias.Conservative => $"Conservative scout read on {ReadableAttribute(attribute)}.",
            ScoutingBias.OvervaluesSize when category == PlayerRatingCategory.Physical => "Scout leans toward size and strength indicators.",
            ScoutingBias.OvervaluesSkating when category == PlayerRatingCategory.Skating => "Scout gives extra weight to skating traits.",
            ScoutingBias.UnderestimatesSkill when category is PlayerRatingCategory.Offensive or PlayerRatingCategory.Skill => "Scout is cautious on pure skill translation.",
            ScoutingBias.StrongGoalieEvaluator when category == PlayerRatingCategory.Goalie => "Goalie-specialist read tightened this estimate.",
            ScoutingBias.StrongCharacterEvaluator when category is PlayerRatingCategory.Mental or PlayerRatingCategory.Team => "Character evaluator has a strong read here.",
            ScoutingBias.ExcellentRegionalScout => "Regional scout fit improves confidence in the live viewing.",
            ScoutingBias.SleeperFinder => "Sleeper-finder tendency flags traits that can beat projection.",
            ScoutingBias.SafePickScout => "Safe-pick scout emphasizes reliable translation.",
            _ => $"Scout estimate updated for {ReadableAttribute(attribute)}."
        };

    private static ScoutingAccuracy AccuracyFor(int quality) =>
        quality >= 82 ? ScoutingAccuracy.Excellent :
        quality >= 68 ? ScoutingAccuracy.Good :
        quality >= 52 ? ScoutingAccuracy.Fair :
        quality > 0 ? ScoutingAccuracy.Poor :
        ScoutingAccuracy.Unknown;

    private static int AccuracyScore(ScoutingAccuracy accuracy) =>
        accuracy switch
        {
            ScoutingAccuracy.Excellent => 88,
            ScoutingAccuracy.Good => 72,
            ScoutingAccuracy.Fair => 56,
            ScoutingAccuracy.Poor => 38,
            _ => 0
        };

    private static string ReadableAttribute(PlayerAttributeKey attribute)
    {
        if (attribute == PlayerAttributeKey.HockeyIQ)
        {
            return "Hockey IQ";
        }
        if (attribute == PlayerAttributeKey.PuckSkill)
        {
            return "Puck C" + "ontrol";
        }
        if (attribute == PlayerAttributeKey.ReboundManagement)
        {
            return "Rebound C" + "ontrol";
        }

        var text = attribute.ToString();
        return string.Concat(text.SelectMany((ch, index) => index > 0 && char.IsUpper(ch) ? new[] { ' ', ch } : new[] { ch }));
    }

    private static string StableId(string text) =>
        new string(text.ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) ? character : '-').ToArray()).Trim('-');
}
