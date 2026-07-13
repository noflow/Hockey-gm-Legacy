using LegacyEngine.Development;
using LegacyEngine.Draft;
using LegacyEngine.Rosters;
using LegacyEngine.Scouting;

namespace LegacyEngine.Integration;

public sealed class HockeyIntelligenceRatingService
{
    public NewGmScenarioSnapshot EnsureRatings(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var existingTrue = scenario.TrueRatings.ToDictionary(rating => rating.PersonId, StringComparer.Ordinal);
        var trueRatings = PlayerIds(scenario)
            .Select(id => existingTrue.TryGetValue(id, out var rating) ? rating : BuildTrueRatings(scenario, id))
            .ToArray();
        var trueLookup = trueRatings.ToDictionary(rating => rating.PersonId, StringComparer.Ordinal);
        var existingScouted = scenario.ScoutedRatings.ToDictionary(rating => rating.PersonId, StringComparer.Ordinal);
        var scoutedRatings = trueRatings
            .Select(rating => existingScouted.TryGetValue(rating.PersonId, out var scouted) ? scouted : BuildInitialScoutedRatings(scenario, rating))
            .ToArray();
        var updated = scenario with
        {
            TrueRatings = trueRatings,
            ScoutedRatings = scoutedRatings
        };
        updated.Validate();
        return updated;
    }

    public PlayerTrueRatings BuildTrueRatings(NewGmScenarioSnapshot scenario, string personId)
    {
        var name = PersonName(scenario, personId);
        var position = ResolvePosition(scenario, personId);
        var age = PersonAge(scenario, personId);
        var isDraftEligible = scenario.AlphaSnapshot.DraftBoard.Entries.Any(entry => entry.ProspectPersonId == personId);
        var overall = BaseOverall(scenario, personId, position, age);
        var potential = BasePotential(scenario, personId, overall, age);
        var archetype = ArchetypeFor(scenario, personId, position);
        var attributes = BuildAttributes(position, archetype, overall, potential, personId).ToArray();
        overall = CalculateOverall(position, archetype, attributes);
        if (isDraftEligible)
        {
            overall = Math.Min(overall, 82);
        }

        potential = Math.Max(overall, potential);
        var ratings = new PlayerTrueRatings(personId, name, position, overall, potential, archetype, attributes, scenario.CurrentDate);
        ratings.Validate();
        return ratings;
    }

    public PlayerScoutedRatings BuildInitialScoutedRatings(NewGmScenarioSnapshot scenario, PlayerTrueRatings truth)
    {
        var color = InitialColor(scenario, truth.PersonId);
        var source = color == PlayerRatingColor.Unknown
            ? PlayerRatingSource.Unknown
            : scenario.AlphaSnapshot.Roster.FindPlayer(truth.PersonId) is not null
                ? PlayerRatingSource.InternalTeamKnowledge
                : PlayerRatingSource.Scout;
        return BuildScoutedFromTruth(
            truth,
            color,
            source,
            scenario.CurrentDate,
            SourceName(source),
            NoteFor(color, source),
            null,
            PersonAge(scenario, truth.PersonId));
    }

    public NewGmScenarioSnapshot RecordScoutingReport(
        NewGmScenarioSnapshot scenario,
        string personId,
        int scoutQuality,
        PlayerRatingCategory? specialty = null,
        string scoutName = "Scouting staff",
        bool regionFit = false)
    {
        var withRatings = EnsureRatings(scenario);
        var truth = RequireTruth(withRatings, personId);
        var existing = withRatings.ScoutedRatings.FirstOrDefault(rating => rating.PersonId == personId);
        var currentColor = existing?.ConfidenceColor ?? PlayerRatingColor.Unknown;
        var steps = scoutQuality >= 82 ? 2 : 1;
        if (specialty is not null && truth.Attributes.Any(attribute => attribute.Category == specialty))
        {
            steps++;
        }
        if (regionFit)
        {
            steps++;
        }

        var nextColor = AdvanceColor(currentColor, steps);
        var updatedScouted = BuildScoutedFromTruth(
            truth,
            nextColor,
            PlayerRatingSource.Scout,
            withRatings.CurrentDate,
            scoutName,
            $"{scoutName} updated the rating picture; confidence improved to {nextColor}.",
            specialty,
            PersonAge(withRatings, truth.PersonId));
        var ratings = withRatings.ScoutedRatings
            .Where(rating => rating.PersonId != personId)
            .Concat(new[] { updatedScouted })
            .OrderBy(rating => rating.PlayerName, StringComparer.Ordinal)
            .ToArray();
        var updated = withRatings with { ScoutedRatings = ratings };
        updated.Validate();
        return updated;
    }

    public NewGmScenarioSnapshot RecordDevelopmentReport(
        NewGmScenarioSnapshot scenario,
        string personId,
        PlayerRatingColor minimumColor = PlayerRatingColor.Blue,
        PlayerRatingCategory? focusCategory = null,
        string staffName = "Development staff",
        string note = "Development report updated the visible rating estimate.")
    {
        var withRatings = EnsureRatings(scenario);
        var truth = RequireTruth(withRatings, personId);
        var existing = withRatings.ScoutedRatings.FirstOrDefault(rating => rating.PersonId == personId);
        var currentColor = existing?.ConfidenceColor ?? PlayerRatingColor.Unknown;
        var nextColor = currentColor < minimumColor ? minimumColor : currentColor;
        var updatedScouted = BuildScoutedFromTruth(
            truth,
            nextColor,
            PlayerRatingSource.DevelopmentReport,
            withRatings.CurrentDate,
            string.IsNullOrWhiteSpace(staffName) ? "Development staff" : staffName.Trim(),
            string.IsNullOrWhiteSpace(note) ? "Development report updated the visible rating estimate." : note.Trim(),
            focusCategory,
            PersonAge(withRatings, truth.PersonId));
        var ratings = withRatings.ScoutedRatings
            .Where(rating => rating.PersonId != personId)
            .Concat(new[] { updatedScouted })
            .OrderBy(rating => rating.PlayerName, StringComparer.Ordinal)
            .ToArray();
        var updated = withRatings with { ScoutedRatings = ratings };
        updated.Validate();
        return updated;
    }

    public NewGmScenarioSnapshot ApplyDevelopmentToTrueRatings(
        NewGmScenarioSnapshot scenario,
        string personId,
        IReadOnlyDictionary<PlayerAttributeKey, int> attributeDeltas,
        DateOnly? updateDate = null)
    {
        var withRatings = EnsureRatings(scenario);
        var truth = RequireTruth(withRatings, personId);
        var deltas = attributeDeltas ?? new Dictionary<PlayerAttributeKey, int>();
        var attributes = truth.Attributes
            .Select(attribute => deltas.TryGetValue(attribute.Attribute, out var delta)
                ? attribute with { Value = Math.Clamp(attribute.Value + delta, 0, 100) }
                : attribute)
            .ToArray();
        var overall = CalculateOverall(truth.Position, truth.RoleArchetype, attributes);
        var potential = Math.Clamp(Math.Max(overall, truth.Potential + deltas.Values.Where(delta => delta > 0).DefaultIfEmpty(0).Sum() / 8), 0, 100);
        var updatedTruth = truth with
        {
            Attributes = attributes,
            Overall = overall,
            Potential = potential,
            LastUpdated = updateDate ?? withRatings.CurrentDate
        };
        updatedTruth.Validate();
        var trueRatings = withRatings.TrueRatings
            .Select(rating => rating.PersonId == personId ? updatedTruth : rating)
            .ToArray();
        var updated = withRatings with { TrueRatings = trueRatings };
        updated.Validate();
        return updated;
    }

    public IReadOnlyList<string> BuildDossierLines(NewGmScenarioSnapshot scenario, string personId)
    {
        var withRatings = new InternalPlayerKnowledgeService().EnsureKnowledge(EnsureRatings(scenario));
        var internalKnowledge = withRatings.InternalPlayerKnowledge.FirstOrDefault(knowledge => knowledge.PersonId == personId);
        if (internalKnowledge is not null)
        {
            var internalLines = new List<string>
            {
                $"Overall / Potential: OVR {internalKnowledge.OverallEstimate} | POT {internalKnowledge.PotentialEstimate} | Confidence: {internalKnowledge.Confidence}",
                $"Source: internal organizational evaluation ({string.Join(", ", internalKnowledge.Sources)})",
                $"Last evaluated: {internalKnowledge.LastEvaluated.ToString("yyyy-MM-dd")}",
                $"Staff note: {internalKnowledge.Summary}"
            };
            foreach (var group in internalKnowledge.Attributes.GroupBy(attribute => attribute.Category).OrderBy(group => group.Key))
            {
                internalLines.Add($"{group.Key}:");
                internalLines.AddRange(group.Select(attribute => $"  {Readable(attribute.Attribute)}: {attribute.Estimate} {attribute.Confidence}"));
            }

            internalLines.Add("Internal evaluations are organization assessments, not hidden engine ratings.");
            return internalLines;
        }

        var scouted = withRatings.ScoutedRatings.FirstOrDefault(rating => rating.PersonId == personId);
        if (scouted is null)
        {
            return new[] { "Ratings: no public scouting estimate available yet." };
        }

        var lines = new List<string>
        {
            $"Overall / Potential: OVR {VisibleEstimate(scouted.Overall)} | POT {VisibleEstimate(scouted.Potential)} | Confidence: {scouted.ConfidenceColor}",
            $"Source: {scouted.Source} - {scouted.ScoutSource}",
            $"Last updated: {(scouted.LastScoutedDate is null ? "not scouted" : scouted.LastScoutedDate.Value.ToString("yyyy-MM-dd"))}",
            $"Scout note: {scouted.ScoutNote}"
        };
        foreach (var group in scouted.Attributes.GroupBy(attribute => attribute.Category).OrderBy(group => group.Key))
        {
            lines.Add($"{group.Key}:");
            lines.AddRange(group.Select(attribute => $"  {Readable(attribute.Attribute)}: {attribute.Range.Display} {attribute.ConfidenceColor}"));
        }

        lines.Add("True internal ratings are not displayed; these are scouted estimates.");
        return lines;
    }

    public string CompactRatingText(NewGmScenarioSnapshot scenario, string personId)
    {
        var scouted = scenario.ScoutedRatings.FirstOrDefault(rating => rating.PersonId == personId)
            ?? EnsureRatings(scenario).ScoutedRatings.FirstOrDefault(rating => rating.PersonId == personId);
        return scouted is null
            ? "OVR ??? | POT ??? | Confidence Unknown"
            : $"OVR {VisibleEstimate(scouted.Overall)} | POT {VisibleEstimate(scouted.Potential)} | {scouted.ConfidenceColor}";
    }

    private static string VisibleEstimate(PlayerRatingRange rating) => rating.IsUnknown ? "???" : rating.Midpoint.ToString();

    private static PlayerScoutedRatings BuildScoutedFromTruth(
        PlayerTrueRatings truth,
        PlayerRatingColor color,
        PlayerRatingSource source,
        DateOnly date,
        string scoutSource,
        string note,
        PlayerRatingCategory? specialty,
        int? age)
    {
        var attributes = truth.Attributes
            .Select(attribute =>
            {
                var attributeColor = specialty is not null && attribute.Category == specialty
                    ? AdvanceColor(color, 1)
                    : AttributeColor(color, attribute.Category);
                return new PlayerScoutedAttributeRating(
                    attribute.Category,
                    attribute.Attribute,
                    VisibleRange(attribute.Value, attributeColor, truth.PersonId, attribute.Attribute),
                    attributeColor,
                    source,
                    attributeColor == PlayerRatingColor.Unknown ? null : date,
                    note);
            })
            .ToArray();
        var confidence = AgeAwareRatingRules.FromColor(color);
        var overallRange = AgeAwareRatingRules.Overall(truth.Overall, age, confidence);
        var potentialRange = AgeAwareRatingRules.Potential(
            truth.Potential,
            (overallRange.Low + overallRange.High) / 2,
            age,
            confidence);
        var ratings = new PlayerScoutedRatings(
            truth.PersonId,
            truth.PlayerName,
            truth.Position,
            new PlayerRatingRange(overallRange.Low, overallRange.High),
            new PlayerRatingRange(potentialRange.Low, potentialRange.High),
            color,
            source,
            color == PlayerRatingColor.Unknown ? null : date,
            scoutSource,
            note,
            attributes);
        ratings.Validate();
        return ratings;
    }

    private static PlayerRatingColor AttributeColor(PlayerRatingColor baseColor, PlayerRatingCategory category)
    {
        if (baseColor == PlayerRatingColor.Unknown && category is PlayerRatingCategory.Skating or PlayerRatingCategory.Physical)
        {
            return PlayerRatingColor.Red;
        }

        return baseColor;
    }

    private static PlayerRatingRange VisibleRange(int trueValue, PlayerRatingColor color, string personId, PlayerAttributeKey key)
    {
        if (color == PlayerRatingColor.Unknown)
        {
            return PlayerRatingRange.Unknown;
        }

        var spread = color switch
        {
            PlayerRatingColor.Red => 8,
            PlayerRatingColor.Green => 4,
            PlayerRatingColor.Blue => 2,
            PlayerRatingColor.Black => 1,
            _ => 10
        };
        var rawOffset = StableOffset(personId, key, 8);
        var offset = (int)Math.Round(rawOffset * (spread / 8.0), MidpointRounding.AwayFromZero);
        var estimate = Math.Clamp(trueValue + offset, 0, 100);
        return new PlayerRatingRange(Math.Clamp(estimate - spread, 0, 100), Math.Clamp(estimate + spread, 0, 100));
    }

    private static int StableOffset(string personId, PlayerAttributeKey key, int spread)
    {
        if (spread <= 0)
        {
            return 0;
        }

        var seed = HashCode.Combine(personId, key) & int.MaxValue;
        return seed % (spread * 2 + 1) - spread;
    }

    private static PlayerRatingColor InitialColor(NewGmScenarioSnapshot scenario, string personId)
    {
        if (scenario.AlphaSnapshot.Roster.FindPlayer(personId) is not null)
        {
            return PlayerRatingColor.Blue;
        }

        var draft = scenario.AlphaSnapshot.DraftBoard.Entries.FirstOrDefault(entry => entry.ProspectPersonId == personId);
        if (draft?.ScoutingConfidence is { } confidence)
        {
            return confidence switch
            {
                ScoutingConfidenceLevel.VeryHigh => PlayerRatingColor.Black,
                ScoutingConfidenceLevel.High => PlayerRatingColor.Blue,
                ScoutingConfidenceLevel.Medium => PlayerRatingColor.Green,
                ScoutingConfidenceLevel.Low => PlayerRatingColor.Red,
                _ => PlayerRatingColor.Unknown
            };
        }

        if (scenario.FreeAgentMarket?.Find(personId) is not null || scenario.TradeBlock?.Find(personId) is not null)
        {
            return PlayerRatingColor.Green;
        }

        return PlayerRatingColor.Unknown;
    }

    private static PlayerRatingColor AdvanceColor(PlayerRatingColor color, int steps)
    {
        var value = (int)color;
        value = Math.Clamp(value + steps, (int)PlayerRatingColor.Unknown, (int)PlayerRatingColor.Black);
        return (PlayerRatingColor)value;
    }

    private static int CalculateOverall(RosterPosition position, string archetype, IReadOnlyList<PlayerAttributeRating> attributes)
    {
        if (position == RosterPosition.Goalie)
        {
            return WeightedAverage(attributes.Where(attribute => attribute.Category == PlayerRatingCategory.Goalie).ToArray(), GoalieWeights());
        }

        var weights = archetype switch
        {
            "Sniper" => new Dictionary<PlayerRatingCategory, int> { [PlayerRatingCategory.Offensive] = 32, [PlayerRatingCategory.Skill] = 25, [PlayerRatingCategory.Skating] = 18, [PlayerRatingCategory.Mental] = 15, [PlayerRatingCategory.Team] = 10 },
            "Playmaker" => new Dictionary<PlayerRatingCategory, int> { [PlayerRatingCategory.Offensive] = 28, [PlayerRatingCategory.Skill] = 28, [PlayerRatingCategory.Mental] = 18, [PlayerRatingCategory.Skating] = 16, [PlayerRatingCategory.Team] = 10 },
            "Defensive Defenseman" => new Dictionary<PlayerRatingCategory, int> { [PlayerRatingCategory.Defensive] = 35, [PlayerRatingCategory.Physical] = 20, [PlayerRatingCategory.Skating] = 20, [PlayerRatingCategory.Mental] = 15, [PlayerRatingCategory.Team] = 10 },
            "Two-Way" => new Dictionary<PlayerRatingCategory, int> { [PlayerRatingCategory.Offensive] = 20, [PlayerRatingCategory.Defensive] = 22, [PlayerRatingCategory.Skating] = 18, [PlayerRatingCategory.Skill] = 15, [PlayerRatingCategory.Mental] = 15, [PlayerRatingCategory.Team] = 10 },
            _ => new Dictionary<PlayerRatingCategory, int> { [PlayerRatingCategory.Offensive] = 22, [PlayerRatingCategory.Defensive] = 18, [PlayerRatingCategory.Skating] = 20, [PlayerRatingCategory.Skill] = 18, [PlayerRatingCategory.Mental] = 14, [PlayerRatingCategory.Team] = 8 }
        };
        return WeightedAverage(attributes, weights);
    }

    private static int WeightedAverage(IReadOnlyList<PlayerAttributeRating> attributes, IReadOnlyDictionary<PlayerRatingCategory, int> weights)
    {
        var total = 0;
        var weightTotal = 0;
        foreach (var group in attributes.GroupBy(attribute => attribute.Category))
        {
            if (!weights.TryGetValue(group.Key, out var weight))
            {
                continue;
            }

            total += (int)Math.Round(group.Average(attribute => attribute.Value)) * weight;
            weightTotal += weight;
        }

        return weightTotal == 0 ? 50 : Math.Clamp(total / weightTotal, 0, 100);
    }

    private static IReadOnlyDictionary<PlayerRatingCategory, int> GoalieWeights() =>
        new Dictionary<PlayerRatingCategory, int> { [PlayerRatingCategory.Goalie] = 100 };

    private static IEnumerable<PlayerAttributeRating> BuildAttributes(RosterPosition position, string archetype, int overall, int potential, string personId)
    {
        var categories = position == RosterPosition.Goalie
            ? new[] { PlayerRatingCategory.Goalie, PlayerRatingCategory.Mental, PlayerRatingCategory.Physical, PlayerRatingCategory.Team }
            : new[] { PlayerRatingCategory.Offensive, PlayerRatingCategory.Defensive, PlayerRatingCategory.Skating, PlayerRatingCategory.Physical, PlayerRatingCategory.Skill, PlayerRatingCategory.Mental, PlayerRatingCategory.Team };
        foreach (var category in categories)
        {
            foreach (var attribute in AttributesFor(category))
            {
                var bias = CategoryBias(archetype, category, attribute);
                var noise = StableOffset(personId, attribute, 5);
                var value = Math.Clamp(overall + bias + noise + Math.Max(0, potential - overall) / 8, 25, 99);
                yield return new PlayerAttributeRating(category, attribute, value);
            }
        }
    }

    private static IReadOnlyList<PlayerAttributeKey> AttributesFor(PlayerRatingCategory category) =>
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

    private static int CategoryBias(string archetype, PlayerRatingCategory category, PlayerAttributeKey attribute) =>
        archetype switch
        {
            "Sniper" when category == PlayerRatingCategory.Offensive || attribute is PlayerAttributeKey.Shooting or PlayerAttributeKey.HandEye => 6,
            "Playmaker" when attribute is PlayerAttributeKey.Passing or PlayerAttributeKey.Vision or PlayerAttributeKey.Creativity or PlayerAttributeKey.Playmaking => 7,
            "Defensive Defenseman" when category is PlayerRatingCategory.Defensive or PlayerRatingCategory.Physical => 5,
            "Two-Way" when category is PlayerRatingCategory.Defensive or PlayerRatingCategory.Team => 4,
            "Goalie" when category == PlayerRatingCategory.Goalie => 5,
            _ => 0
        };

    private static string ArchetypeFor(NewGmScenarioSnapshot scenario, string personId, RosterPosition position)
    {
        if (position == RosterPosition.Goalie)
        {
            return "Goalie";
        }

        var text = scenario.AlphaSnapshot.DraftBoard.Entries.FirstOrDefault(entry => entry.ProspectPersonId == personId)?.ProjectionText
            ?? scenario.ProspectRights.FirstOrDefault(record => record.ProspectPersonId == personId)?.ProjectionText
            ?? string.Empty;
        if (position == RosterPosition.Defense)
        {
            return text.Contains("mobile", StringComparison.OrdinalIgnoreCase) ? "Two-Way" : "Defensive Defenseman";
        }

        if (text.Contains("scor", StringComparison.OrdinalIgnoreCase))
        {
            return "Sniper";
        }

        if (text.Contains("play", StringComparison.OrdinalIgnoreCase) || text.Contains("vision", StringComparison.OrdinalIgnoreCase))
        {
            return "Playmaker";
        }

        return "Two-Way";
    }

    private static int BaseOverall(NewGmScenarioSnapshot scenario, string personId, RosterPosition position, int? age)
    {
        var draft = scenario.AlphaSnapshot.DraftBoard.Entries.FirstOrDefault(entry => entry.ProspectPersonId == personId);
        if (draft is not null)
        {
            return draft.Rank switch
            {
                1 => 78,
                2 => 76,
                3 => 75,
                <= 10 => 70 - ((draft.Rank - 4) / 2),
                <= 25 => 63 - ((draft.Rank - 11) / 5),
                <= 45 => 57 - ((draft.Rank - 26) / 7),
                _ => 54
            };
        }

        var roster = scenario.AlphaSnapshot.Roster.FindPlayer(personId);
        if (roster is not null)
        {
            var index = scenario.AlphaSnapshot.Roster.Players.OrderBy(player => player.Position).ThenBy(player => player.PersonId, StringComparer.Ordinal).ToList().FindIndex(player => player.PersonId == personId);
            return scenario.LeagueProfile.Experience switch
            {
                LeagueExperience.Nhl => index < 3 ? 89 : index < 8 ? 85 : index < 15 ? 81 : index < 22 ? 76 : 73,
                LeagueExperience.Ahl => index < 4 ? 75 : index < 12 ? 70 : index < 20 ? 66 : 62,
                _ => index < 3 ? 74 : index < 10 ? 68 : index < 20 ? 62 : 56
            };
        }

        return scenario.LeagueProfile.Experience == LeagueExperience.Nhl ? 74 : 58;
    }

    private static int BasePotential(NewGmScenarioSnapshot scenario, string personId, int overall, int? age)
    {
        var draft = scenario.AlphaSnapshot.DraftBoard.Entries.FirstOrDefault(entry => entry.ProspectPersonId == personId);
        if (draft is not null)
        {
            return draft.Rank switch
            {
                1 => 96,
                2 => 93,
                <= 5 => 90,
                <= 12 => 87,
                <= 30 => 83,
                <= 45 => 78,
                _ => 73
            };
        }

        var runway = age switch
        {
            <= 18 => 16,
            <= 21 => 10,
            <= 24 => 6,
            <= 28 => 3,
            _ => 0
        };
        return Math.Clamp(overall + runway, overall, 98);
    }

    private static IEnumerable<string> PlayerIds(NewGmScenarioSnapshot scenario) =>
        scenario.AlphaSnapshot.Roster.Players.Select(player => player.PersonId)
            .Concat(scenario.AlphaSnapshot.DraftBoard.Entries.Select(entry => entry.ProspectPersonId))
            .Concat(scenario.ProspectRights.Select(record => record.ProspectPersonId))
            .Concat(scenario.FreeAgentMarket?.FreeAgents.Select(agent => agent.PersonId) ?? Array.Empty<string>())
            .Concat(scenario.TradeBlock?.Entries.Select(entry => entry.PersonId) ?? Array.Empty<string>())
            .Distinct(StringComparer.Ordinal);

    private static PlayerTrueRatings RequireTruth(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.TrueRatings.FirstOrDefault(rating => rating.PersonId == personId)
        ?? throw new ArgumentException("True ratings were not found.", nameof(personId));

    private static RosterPosition ResolvePosition(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.Roster.FindPlayer(personId)?.Position
        ?? scenario.AlphaSnapshot.DraftBoard.Entries.FirstOrDefault(entry => entry.ProspectPersonId == personId)?.Bio?.Position
        ?? scenario.ProspectRights.FirstOrDefault(record => record.ProspectPersonId == personId)?.Position
        ?? scenario.FreeAgentMarket?.Find(personId)?.Position
        ?? scenario.TradeBlock?.Find(personId)?.Position
        ?? RosterPosition.Unknown;

    private static int? PersonAge(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.CalculateAge(scenario.CurrentDate)
        ?? scenario.AlphaSnapshot.Roster.FindPlayer(personId)?.Age
        ?? (scenario.AlphaSnapshot.DraftBoard.Entries.FirstOrDefault(entry => entry.ProspectPersonId == personId)?.Bio?.BirthYear is int birthYear
            ? (int?)(scenario.CurrentDate.Year - birthYear)
            : null)
        ?? scenario.ProspectRights.FirstOrDefault(record => record.ProspectPersonId == personId)?.Age
        ?? scenario.FreeAgentMarket?.Find(personId)?.Age
        ?? scenario.TradeBlock?.Find(personId)?.Age;

    private static string PersonName(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? scenario.ProspectRights.FirstOrDefault(record => record.ProspectPersonId == personId)?.ProspectName
        ?? scenario.FreeAgentMarket?.Find(personId)?.Name
        ?? scenario.TradeBlock?.Find(personId)?.Name
        ?? personId;

    private static string SourceName(PlayerRatingSource source) =>
        source switch
        {
            PlayerRatingSource.InternalTeamKnowledge => "Team staff",
            PlayerRatingSource.Scout => "Scouting staff",
            PlayerRatingSource.DevelopmentReport => "Development staff",
            PlayerRatingSource.SeasonReview => "Season review",
            _ => "Unknown"
        };

    private static string NoteFor(PlayerRatingColor color, PlayerRatingSource source) =>
        color == PlayerRatingColor.Unknown
            ? "Unscouted; only basic public information is available."
            : source == PlayerRatingSource.InternalTeamKnowledge
                ? "Internal staff have a strong working read, but it is still an estimate."
                : $"Visible ratings are a {color} confidence scouting estimate.";

    private static string Readable(PlayerAttributeKey attribute)
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
}
