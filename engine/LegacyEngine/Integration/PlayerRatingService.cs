using LegacyEngine.Development;
using LegacyEngine.Draft;
using LegacyEngine.Injuries;
using LegacyEngine.Rosters;
using LegacyEngine.Scouting;

namespace LegacyEngine.Integration;

public sealed class PlayerRatingService
{
    public NewGmScenarioSnapshot EnsureRatings(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var ids = PlayerIds(scenario).ToArray();
        var snapshots = ids
            .Select(id => BuildSnapshot(scenario, id))
            .ToArray();
        var history = scenario.PlayerRatingHistory.Merge(snapshots);
        var updated = scenario with
        {
            PlayerRatings = snapshots,
            PlayerRatingHistory = history
        };
        updated.Validate();
        return updated;
    }

    public PlayerRatingSnapshot BuildSnapshot(NewGmScenarioSnapshot scenario, string personId)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        if (string.IsNullOrWhiteSpace(personId))
        {
            throw new ArgumentException("Person id is required.", nameof(personId));
        }

        var name = PersonName(scenario, personId);
        var position = ResolvePosition(scenario, personId);
        var age = PersonAge(scenario, personId);
        var profile = scenario.AlphaSnapshot.DevelopmentProfiles.FirstOrDefault(item => item.PersonId == personId);
        var draft = scenario.AlphaSnapshot.DraftBoard.Entries.FirstOrDefault(item => item.ProspectPersonId == personId);
        var latestReport = scenario.CompletedScoutingReports
            .Where(report => report.PlayerId == personId)
            .OrderByDescending(report => report.CreatedOn)
            .FirstOrDefault();
        var confidence = ResolveConfidence(scenario, personId, draft, latestReport);
        var raw = EstimateRawRatings(scenario, personId, position, age, profile, draft, latestReport);
        var injuryPenalty = ActiveInjuryPenalty(scenario, personId);
        var adjustedOverall = Math.Clamp(raw.Overall - injuryPenalty, 0, 100);
        var adjustedPotential = Math.Clamp(Math.Max(raw.Potential, adjustedOverall), 0, 100);
        var overall = VisibleOverall(adjustedOverall, confidence);
        var potential = VisiblePotential(adjustedPotential, confidence);
        var band = BandFor(overall.Midpoint, potential.Midpoint, scenario.LeagueProfile.Experience, draft is not null);
        var snapshot = new PlayerRatingSnapshot(
            personId,
            name,
            position,
            age,
            overall,
            potential,
            band,
            confidence,
            scenario.CurrentDate,
            raw.Source,
            RoleLabelFor(overall.Midpoint, position),
            DevelopmentNoteFor(profile, raw, injuryPenalty, overall, potential));
        snapshot.Validate();
        return snapshot;
    }

    public IReadOnlyList<string> BuildDossierLines(NewGmScenarioSnapshot scenario, string personId)
    {
        var snapshot = scenario.PlayerRatings.FirstOrDefault(item => item.PersonId == personId)
            ?? BuildSnapshot(scenario, personId);
        var history = scenario.PlayerRatingHistory.ForPerson(personId);
        var peak = history.Count == 0 ? snapshot.Overall.Midpoint : history.Max(item => item.Overall.Midpoint);
        var lines = new List<string>
        {
            $"{snapshot.OverallDisplay} | {snapshot.PotentialDisplay} | Confidence: {snapshot.Confidence}",
            $"Role label: {snapshot.RoleLabel}",
            $"Band: {snapshot.Band}",
            $"Source: {snapshot.RatingSource}",
            $"Last updated: {snapshot.LastUpdated:yyyy-MM-dd}",
            $"Rating trend: {TrendText(history, snapshot)}",
            $"Peak visible overall: {peak}",
            $"Potential history: {PotentialHistoryText(history, snapshot)}",
            $"Development note: {snapshot.DevelopmentNote}"
        };

        if (snapshot.Confidence is PlayerRatingConfidence.Low or PlayerRatingConfidence.Unknown)
        {
            lines.Add("Low-confidence ratings are scout estimates, not perfect truth.");
        }

        return lines;
    }

    private static IEnumerable<string> PlayerIds(NewGmScenarioSnapshot scenario) =>
        scenario.AlphaSnapshot.Roster.Players.Select(player => player.PersonId)
            .Concat(scenario.AlphaSnapshot.DraftBoard.Entries.Select(entry => entry.ProspectPersonId))
            .Concat(scenario.ProspectRights.Select(record => record.ProspectPersonId))
            .Concat(scenario.FreeAgentMarket?.FreeAgents.Select(agent => agent.PersonId) ?? Array.Empty<string>())
            .Concat(scenario.TradeBlock?.Entries.Select(entry => entry.PersonId) ?? Array.Empty<string>())
            .Distinct(StringComparer.Ordinal);

    private static (int Overall, int Potential, string Source) EstimateRawRatings(
        NewGmScenarioSnapshot scenario,
        string personId,
        RosterPosition position,
        int? age,
        PlayerDevelopmentProfile? profile,
        DraftBoardEntry? draft,
        ScoutingReport? latestReport)
    {
        if (draft is not null)
        {
            var rank = draft.Rank;
            var overall = rank switch
            {
                1 => 81,
                2 => 79,
                3 => 78,
                <= 10 => 73 - ((rank - 4) / 2),
                <= 25 => 66 - ((rank - 11) / 5),
                <= 45 => 60 - ((rank - 26) / 7),
                _ => 54
            };
            var potential = rank switch
            {
                1 => 96,
                2 => 93,
                <= 5 => 90,
                <= 12 => 87,
                <= 30 => 83,
                <= 45 => 78,
                _ => 73
            };
            if (draft.Bio?.PotentialLineupProjection.Contains("franchise", StringComparison.OrdinalIgnoreCase) == true)
            {
                potential = Math.Max(potential, 96);
            }

            return (Math.Clamp(overall, 40, 82), Math.Clamp(potential, 65, 98), "Draft board + scouting staff estimate");
        }

        var rosterPlayer = scenario.AlphaSnapshot.Roster.FindPlayer(personId);
        if (rosterPlayer is not null)
        {
            var active = scenario.AlphaSnapshot.Roster.Players.OrderBy(player => player.Position).ThenBy(player => player.PersonId, StringComparer.Ordinal).ToArray();
            var index = Array.FindIndex(active, player => player.PersonId == personId);
            var baseOverall = scenario.LeagueProfile.Experience switch
            {
                LeagueExperience.Nhl => NhlRosterOverall(index, position),
                LeagueExperience.Ahl => AhlRosterOverall(index, position),
                _ => JuniorRosterOverall(index, position)
            };
            var profileOverall = profile is null ? baseOverall : NormalizeProfileOverall(profile, scenario.LeagueProfile.Experience);
            var overall = Math.Max(baseOverall, profileOverall);
            var basePotential = Math.Max(overall + PotentialRunway(age), profile is null ? overall + 6 : NormalizeProfilePotential(profile, scenario.LeagueProfile.Experience));
            return (Math.Clamp(overall, 0, 98), Math.Clamp(basePotential, overall, 98), "Roster, role, league level, and development profile");
        }

        var rights = scenario.ProspectRights.FirstOrDefault(record => record.ProspectPersonId == personId);
        if (rights is not null)
        {
            var overall = rights.Age <= 18 ? 58 : rights.Age == 19 ? 62 : 66;
            var potential = rights.RoundNumber switch
            {
                1 => 90,
                2 => 86,
                3 => 82,
                _ => 77
            };
            return (overall, Math.Max(overall, potential), "Prospect rights and pipeline estimate");
        }

        var freeAgent = scenario.FreeAgentMarket?.Find(personId);
        if (freeAgent is not null)
        {
            var overall = Math.Clamp(60 + freeAgent.FitSummary.FitScore / 4, 58, scenario.LeagueProfile.Experience == LeagueExperience.Nhl ? 86 : 76);
            var potential = Math.Clamp(overall + Math.Max(0, 27 - freeAgent.Age) / 2, overall, 90);
            return (overall, potential, "Free-agent market and staff fit estimate");
        }

        var trade = scenario.TradeBlock?.Find(personId);
        if (trade is not null)
        {
            var overall = Math.Clamp(58 + trade.AssetValue / 3, 58, scenario.LeagueProfile.Experience == LeagueExperience.Nhl ? 90 : 78);
            var potential = Math.Clamp(overall + Math.Max(0, 25 - trade.Age) / 2, overall, 94);
            return (overall, potential, "Trade market and asset-value estimate");
        }

        if (latestReport is not null)
        {
            return (latestReport.CurrentAbilityEstimate.High, latestReport.PotentialEstimate.High, "Scouting report estimate");
        }

        if (profile is not null)
        {
            var overall = NormalizeProfileOverall(profile, scenario.LeagueProfile.Experience);
            var potential = NormalizeProfilePotential(profile, scenario.LeagueProfile.Experience);
            return (overall, Math.Max(overall, potential), "Development profile estimate");
        }

        return (55, 70, "Fallback public estimate");
    }

    private static int NhlRosterOverall(int index, RosterPosition position) =>
        index switch
        {
            < 3 => position == RosterPosition.Goalie ? 88 : 89,
            < 8 => 85,
            < 15 => 81,
            < 22 => 76,
            _ => 73
        };

    private static int AhlRosterOverall(int index, RosterPosition position) =>
        index switch
        {
            < 4 => 75,
            < 12 => 70,
            < 20 => 66,
            _ => 62
        };

    private static int JuniorRosterOverall(int index, RosterPosition position) =>
        index switch
        {
            < 3 => 74,
            < 10 => 68,
            < 20 => 62,
            _ => 56
        };

    private static int NormalizeProfileOverall(PlayerDevelopmentProfile profile, LeagueExperience experience) =>
        experience switch
        {
            LeagueExperience.Nhl => Math.Clamp(profile.CurrentAbility + 38, 72, 92),
            LeagueExperience.Ahl => Math.Clamp(profile.CurrentAbility + 28, 60, 78),
            _ => Math.Clamp(profile.CurrentAbility + 18, 40, 82)
        };

    private static int NormalizeProfilePotential(PlayerDevelopmentProfile profile, LeagueExperience experience) =>
        experience switch
        {
            LeagueExperience.Nhl => Math.Clamp(profile.Potential + 34, 75, 98),
            LeagueExperience.Ahl => Math.Clamp(profile.Potential + 24, 65, 92),
            _ => Math.Clamp(profile.Potential + 22, 65, 95)
        };

    private static int PotentialRunway(int? age) =>
        age switch
        {
            <= 18 => 16,
            <= 21 => 10,
            <= 24 => 6,
            <= 28 => 3,
            _ => 0
        };

    private static PlayerRating VisibleOverall(int value, PlayerRatingConfidence confidence)
    {
        var spread = confidence switch
        {
            PlayerRatingConfidence.VeryHigh => 0,
            PlayerRatingConfidence.High => 1,
            PlayerRatingConfidence.Medium => 3,
            PlayerRatingConfidence.Low => 6,
            _ => 8
        };
        return new PlayerRating(Math.Clamp(value - spread, 0, 100), Math.Clamp(value + spread, 0, 100));
    }

    private static PlayerPotential VisiblePotential(int value, PlayerRatingConfidence confidence)
    {
        var spread = confidence switch
        {
            PlayerRatingConfidence.VeryHigh => 0,
            PlayerRatingConfidence.High => 2,
            PlayerRatingConfidence.Medium => 4,
            PlayerRatingConfidence.Low => 8,
            _ => 10
        };
        return new PlayerPotential(Math.Clamp(value - spread, 0, 100), Math.Clamp(value + spread, 0, 100));
    }

    private static PlayerRatingConfidence ResolveConfidence(NewGmScenarioSnapshot scenario, string personId, DraftBoardEntry? draft, ScoutingReport? latestReport)
    {
        if (draft?.ScoutingConfidence is { } draftConfidence)
        {
            return MapConfidence(draftConfidence);
        }

        if (latestReport is not null)
        {
            return MapConfidence(latestReport.Confidence);
        }

        if (scenario.FreeAgentMarket?.Find(personId) is { } freeAgent)
        {
            return MapConfidence(freeAgent.ScoutingConfidence);
        }

        return scenario.AlphaSnapshot.Roster.FindPlayer(personId) is not null
            ? PlayerRatingConfidence.High
            : PlayerRatingConfidence.Medium;
    }

    private static PlayerRatingConfidence MapConfidence(ScoutingConfidenceLevel confidence) =>
        confidence switch
        {
            ScoutingConfidenceLevel.VeryHigh => PlayerRatingConfidence.VeryHigh,
            ScoutingConfidenceLevel.High => PlayerRatingConfidence.High,
            ScoutingConfidenceLevel.Medium => PlayerRatingConfidence.Medium,
            ScoutingConfidenceLevel.Low => PlayerRatingConfidence.Low,
            _ => PlayerRatingConfidence.Unknown
        };

    private static PlayerRatingBand BandFor(int overall, int potential, LeagueExperience experience, bool isDraftProspect)
    {
        if (potential >= 99)
        {
            return PlayerRatingBand.Generational;
        }

        if (potential >= 95)
        {
            return PlayerRatingBand.Franchise;
        }

        if (overall >= 94)
        {
            return PlayerRatingBand.Elite;
        }

        if (overall >= 89)
        {
            return PlayerRatingBand.Star;
        }

        if (overall >= 84)
        {
            return PlayerRatingBand.TopSix;
        }

        if (overall >= 78)
        {
            return PlayerRatingBand.MiddleSix;
        }

        if (experience == LeagueExperience.Nhl && overall >= 72)
        {
            return PlayerRatingBand.Depth;
        }

        if (experience == LeagueExperience.Ahl && overall >= 75)
        {
            return PlayerRatingBand.NhlReadyProspect;
        }

        if (experience == LeagueExperience.Ahl && overall >= 68)
        {
            return PlayerRatingBand.GoodAhl;
        }

        if (isDraftProspect && overall >= 76)
        {
            return PlayerRatingBand.EliteDraftProspect;
        }

        if (overall >= 65)
        {
            return PlayerRatingBand.StrongJunior;
        }

        return overall >= 55 ? PlayerRatingBand.AverageJunior : PlayerRatingBand.WeakProspect;
    }

    private static string RoleLabelFor(int overall, RosterPosition position)
    {
        if (position == RosterPosition.Goalie)
        {
            return overall switch
            {
                >= 94 => "Franchise Goalie",
                >= 89 => "Starter",
                >= 82 => "Tandem / NHL Ready",
                >= 75 => "Backup / AHL Starter",
                _ => "Development Goalie"
            };
        }

        if (position == RosterPosition.Defense)
        {
            return overall switch
            {
                >= 94 => "Franchise Defenseman",
                >= 89 => "Top Pair",
                >= 84 => "Second Pair",
                >= 78 => "Third Pair",
                >= 72 => "Depth Defense",
                _ => "Development Defense"
            };
        }

        return overall switch
        {
            >= 94 => "Franchise Forward",
            >= 89 => "First Line",
            >= 84 => "Top Six",
            >= 78 => "Middle Six",
            >= 72 => "Depth / Fourth Line",
            _ => "Development Forward"
        };
    }

    private static string DevelopmentNoteFor(PlayerDevelopmentProfile? profile, (int Overall, int Potential, string Source) raw, int injuryPenalty, PlayerRating overall, PlayerPotential potential)
    {
        if (injuryPenalty > 0)
        {
            return "Injury context is lowering the current visible read.";
        }

        if (profile is null)
        {
            return "No tracked development profile yet; rating is based on public roster/scouting context.";
        }

        var gap = raw.Potential - raw.Overall;
        if (profile.Potential - profile.CurrentAbility <= 2)
        {
            return "Capped out / plateaued: staff estimate little remaining rating runway.";
        }

        if (gap <= 2)
        {
            return "Capped out / plateaued: staff estimate little remaining rating runway.";
        }

        if (overall.Midpoint >= potential.Midpoint - 4)
        {
            return "Approaching ceiling; future gains may be smaller.";
        }

        if (profile.LastUpdated.Year < DateTime.UtcNow.Year)
        {
            return "Yearly snapshot may be ready for review.";
        }

        return gap >= 12 ? "Meaningful development runway remains." : "Moderate development runway remains.";
    }

    private static int ActiveInjuryPenalty(NewGmScenarioSnapshot scenario, string personId)
    {
        var injury = scenario.AlphaSnapshot.Injuries
            .Where(injury => injury.PersonId == personId && injury.IsActive)
            .OrderByDescending(injury => injury.Severity)
            .FirstOrDefault();
        return injury?.Severity switch
        {
            InjurySeverity.Minor => 1,
            InjurySeverity.Moderate => 3,
            InjurySeverity.Major => 6,
            InjurySeverity.Severe => 9,
            InjurySeverity.CareerThreatening => 12,
            _ => 0
        };
    }

    private static string TrendText(IReadOnlyList<PlayerRatingSnapshot> history, PlayerRatingSnapshot current)
    {
        var previous = history.LastOrDefault(snapshot => snapshot.PersonId == current.PersonId && snapshot.LastUpdated < current.LastUpdated);
        if (previous is null)
        {
            return "Initial rating snapshot.";
        }

        var delta = current.Overall.Midpoint - previous.Overall.Midpoint;
        return delta switch
        {
            > 0 => $"+{delta} Overall since prior snapshot.",
            < 0 => $"{delta} Overall since prior snapshot.",
            _ => "No visible overall change since prior snapshot."
        };
    }

    private static string PotentialHistoryText(IReadOnlyList<PlayerRatingSnapshot> history, PlayerRatingSnapshot current)
    {
        var previous = history.LastOrDefault(snapshot => snapshot.PersonId == current.PersonId && snapshot.LastUpdated < current.LastUpdated);
        if (previous is null)
        {
            return $"Initial visible POT {current.Potential.Display}.";
        }

        var delta = current.Potential.Midpoint - previous.Potential.Midpoint;
        return delta switch
        {
            > 0 => $"Potential estimate revised upward by {delta}.",
            < 0 => $"Potential estimate revised downward by {Math.Abs(delta)}.",
            _ => "Potential estimate unchanged."
        };
    }

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
        ?? scenario.ProspectRights.FirstOrDefault(record => record.ProspectPersonId == personId)?.Age
        ?? scenario.FreeAgentMarket?.Find(personId)?.Age
        ?? scenario.TradeBlock?.Find(personId)?.Age;

    private static string PersonName(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? scenario.ProspectRights.FirstOrDefault(record => record.ProspectPersonId == personId)?.ProspectName
        ?? scenario.FreeAgentMarket?.Find(personId)?.Name
        ?? scenario.TradeBlock?.Find(personId)?.Name
        ?? personId;
}
