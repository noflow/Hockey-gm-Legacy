using LegacyEngine.Rosters;
using LegacyEngine.Staff;

namespace LegacyEngine.Integration;

public sealed class FranchiseIdentityService
{
    public NewGmScenarioSnapshot EnsureIdentities(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var teams = scenario.LeagueProfile.Teams;
        var existing = scenario.FranchiseIdentities;
        if (existing.Count >= teams.Count && teams.All(team => existing.Any(identity => identity.OrganizationId == team.OrganizationId)))
        {
            return scenario;
        }

        var aiScenario = scenario.OrganizationAiProfiles.Count >= teams.Count
            ? scenario
            : new OrganizationAiService().EnsureProfiles(scenario);
        var identities = teams
            .Select(team =>
                existing.FirstOrDefault(identity => identity.OrganizationId == team.OrganizationId)
                ?? BuildIdentity(aiScenario, team))
            .ToArray();

        foreach (var identity in identities)
        {
            identity.Validate();
        }

        return aiScenario with { FranchiseIdentities = identities };
    }

    public FranchiseIdentity BuildIdentity(NewGmScenarioSnapshot scenario, TeamSelectionOption team)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(team);
        team.Validate();

        var leagueProfile = new LeagueAiService().BuildOrganizationProfile(scenario, team.OrganizationId, team.TeamName);
        var ai = scenario.OrganizationAiProfiles.FirstOrDefault(profile => profile.OrganizationId == team.OrganizationId)
            ?? new OrganizationAiService().BuildProfile(scenario, leagueProfile);
        var current = PhilosophyFrom(leagueProfile, ai);
        var culture = CultureFrom(leagueProfile, ai);
        var direction = DirectionFrom(ai);
        var reputation = ReputationFrom(leagueProfile, ai);
        var priorIdentity = PriorIdentity(current, team);
        var history = BuildHistory(scenario, team, current, reputation);
        var currentEra = new FranchiseEra(
            $"franchise-era:{team.OrganizationId}:{scenario.Season.Year - 2}",
            scenario.Season.Year - 2,
            null,
            EraName(current, direction),
            team.OrganizationId == scenario.Organization.OrganizationId ? scenario.GeneralManagerProfile.Person.Identity.DisplayName : Readable(ai.Personality),
            team.OrganizationId == scenario.Organization.OrganizationId ? scenario.AlphaSnapshot.Owner.Name : $"{team.TeamName} Ownership",
            HeadCoachName(scenario, team.OrganizationId),
            current,
            CurrentEraAchievements(scenario, team, current, reputation).ToArray());
        var historicalEra = new FranchiseEra(
            $"franchise-era:{team.OrganizationId}:{scenario.Season.Year - 10}",
            scenario.Season.Year - 10,
            scenario.Season.Year - 3,
            EraName(priorIdentity, FranchiseDirection.Retool),
            "Previous front office",
            $"{team.TeamName} Ownership",
            "Previous coaching staff",
            priorIdentity,
            HistoricalEraAchievements(team, priorIdentity).ToArray());
        var dna = BuildTeamDna(current, culture, ai, team).ToArray();
        var strengths = BuildStrengths(current, culture, ai, team).ToArray();
        var weaknesses = BuildWeaknesses(current, ai, team).ToArray();
        var goals = BuildFutureGoals(direction, current, ai).ToArray();
        var shifts = new[]
        {
            new FranchiseIdentityShift(
                scenario.CurrentDate.AddYears(-2),
                priorIdentity,
                current,
                CultureFrom(priorIdentity),
                culture,
                $"{team.TeamName} slowly moved toward {Readable(current).ToLowerInvariant()} after roster, staff, and owner priorities changed.",
                $"{team.TeamName} is now viewed more as {Readable(current).ToLowerInvariant()} than its previous {Readable(priorIdentity).ToLowerInvariant()} era.")
        };

        var identity = new FranchiseIdentity(
            team.OrganizationId,
            team.TeamName,
            current,
            new[] { priorIdentity, current }.Distinct().ToArray(),
            current,
            culture,
            direction,
            currentEra,
            new[] { historicalEra },
            history,
            reputation,
            dna,
            strengths,
            weaknesses,
            goals,
            shifts,
            scenario.CurrentDate,
            $"{team.TeamName} is known for {Readable(current).ToLowerInvariant()} with a {Readable(culture).ToLowerInvariant()} culture and {Readable(reputation).ToLowerInvariant()} league reputation.");
        identity.Validate();
        return identity;
    }

    public FranchiseIdentity EvolveIdentity(
        FranchiseIdentity identity,
        DateOnly date,
        FranchisePhilosophy suggestedIdentity,
        FranchiseCulture suggestedCulture,
        string reason,
        int evidenceScore)
    {
        ArgumentNullException.ThrowIfNull(identity);
        identity.Validate();

        if (evidenceScore < 70
            || (identity.CurrentIdentity == suggestedIdentity && identity.Culture == suggestedCulture)
            || identity.IdentityShifts.Any(shift => date.Year - shift.Date.Year < 2))
        {
            return identity;
        }

        var shift = new FranchiseIdentityShift(
            date,
            identity.CurrentIdentity,
            suggestedIdentity,
            identity.Culture,
            suggestedCulture,
            reason,
            $"{identity.TeamName} is gradually shifting from {Readable(identity.CurrentIdentity).ToLowerInvariant()} to {Readable(suggestedIdentity).ToLowerInvariant()}.");
        var nextEra = new FranchiseEra(
            $"franchise-era:{identity.OrganizationId}:{date.Year}",
            date.Year,
            null,
            EraName(suggestedIdentity, identity.FutureDirection),
            identity.CurrentEra.GeneralManagerName,
            identity.CurrentEra.OwnerName,
            identity.CurrentEra.CoachName,
            suggestedIdentity,
            new[] { $"Identity shift: {shift.VisibleExplanation}" });
        var priorEra = identity.CurrentEra with { EndYear = date.Year - 1 };
        var next = identity with
        {
            CurrentIdentity = suggestedIdentity,
            CurrentPhilosophy = suggestedIdentity,
            Culture = suggestedCulture,
            CurrentEra = nextEra,
            HistoricalEras = identity.HistoricalEras.Append(priorEra).ToArray(),
            HistoricalIdentity = identity.HistoricalIdentity.Append(suggestedIdentity).Distinct().ToArray(),
            IdentityShifts = identity.IdentityShifts.Append(shift).ToArray(),
            LastUpdated = date,
            Summary = $"{identity.TeamName} is slowly becoming {Readable(suggestedIdentity).ToLowerInvariant()} because {reason}"
        };
        next.Validate();
        return next;
    }

    public IReadOnlyList<string> BuildCommandCenterLines(NewGmScenarioSnapshot scenario, string organizationId)
    {
        var identity = FindIdentity(EnsureIdentities(scenario), organizationId);
        return new[]
        {
            $"Identity: {Readable(identity.CurrentIdentity)}",
            $"Culture: {Readable(identity.Culture)}",
            $"Current era: {identity.CurrentEra.Name} ({identity.CurrentEra.StartYear}-present)",
            $"Future direction: {Readable(identity.FutureDirection)}",
            $"Reputation: {Readable(identity.Reputation)}",
            $"Team DNA: {string.Join(", ", identity.TeamDna.Take(4))}",
            $"Strengths: {string.Join("; ", identity.Strengths.Take(3))}",
            $"Weaknesses: {string.Join("; ", identity.Weaknesses.Take(3))}",
            $"Future goals: {string.Join("; ", identity.FutureGoals.Take(3))}",
            $"Historical eras: {string.Join("; ", identity.HistoricalEras.TakeLast(2).Select(era => $"{era.StartYear}-{era.EndYear}: {era.Name}"))}"
        };
    }

    public IReadOnlyDictionary<string, string> BuildExecutiveReportItems(NewGmScenarioSnapshot scenario, string organizationId)
    {
        var identity = FindIdentity(EnsureIdentities(scenario), organizationId);
        var latestShift = identity.IdentityShifts.OrderByDescending(shift => shift.Date).FirstOrDefault();
        return new Dictionary<string, string>
        {
            ["Current Identity"] = Readable(identity.CurrentIdentity),
            ["Culture"] = Readable(identity.Culture),
            ["Current Era"] = $"{identity.CurrentEra.Name} ({identity.CurrentEra.StartYear}-present)",
            ["Reputation"] = Readable(identity.Reputation),
            ["Franchise Direction"] = Readable(identity.FutureDirection),
            ["Identity Change"] = latestShift?.VisibleExplanation ?? "No identity shift recorded.",
            ["Culture Change"] = latestShift is null ? "Culture is stable." : $"{Readable(latestShift.FromCulture)} to {Readable(latestShift.ToCulture)}",
            ["Era Started"] = identity.CurrentEra.StartYear.ToString(),
            ["Team DNA"] = string.Join(", ", identity.TeamDna.Take(4))
        };
    }

    public IReadOnlyList<LeagueTransaction> BuildLeagueNews(NewGmScenarioSnapshot scenario, int maxItems = 3)
    {
        var withIdentity = EnsureIdentities(scenario);
        var monthKey = scenario.CurrentDate.Year * 100 + scenario.CurrentDate.Month;
        return withIdentity.FranchiseIdentities
            .Where(identity => identity.OrganizationId != scenario.Organization.OrganizationId)
            .Where(identity => StableHash($"{identity.OrganizationId}:{identity.CurrentIdentity}:{monthKey}:franchise-culture") % 4 == 0)
            .OrderBy(identity => identity.TeamName, StringComparer.Ordinal)
            .Take(maxItems)
            .Select(identity => new LeagueTransaction(
                $"franchise-identity:{identity.OrganizationId}:{monthKey}",
                new DateTimeOffset(scenario.CurrentDate.Year, scenario.CurrentDate.Month, scenario.CurrentDate.Day, 12, 15, 0, TimeSpan.Zero),
                identity.OrganizationId,
                identity.TeamName,
                null,
                "Organization",
                LeagueTransactionType.TeamIdentityUpdate,
                LeagueNewsCategory.League,
                NewsHeadline(identity)))
            .ToArray();
    }

    public FranchiseFitResult EvaluatePlayerFit(NewGmScenarioSnapshot scenario, string personId)
    {
        var identity = PlayerIdentity(scenario);
        var person = scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)
            ?? throw new ArgumentException("Player was not found for franchise fit.", nameof(personId));
        var position = ResolvePosition(scenario, personId);
        var age = person.CalculateAge(scenario.CurrentDate);
        var score = 55;
        var reasons = new List<string>();

        if (identity.CurrentIdentity is FranchisePhilosophy.DraftAndDevelop or FranchisePhilosophy.ProspectOrganization && age <= 20)
        {
            score += 18;
            reasons.Add("Young player fits an organization that values draft-and-develop patience.");
        }

        if (identity.CurrentIdentity == FranchisePhilosophy.GoaltendingFactory && position == RosterPosition.Goalie)
        {
            score += 20;
            reasons.Add("Goalie fits a club known for goalie development.");
        }

        if (identity.CurrentIdentity == FranchisePhilosophy.DefenseFirst && position == RosterPosition.Defense)
        {
            score += 16;
            reasons.Add("Defenseman fits the organization's defense-first identity.");
        }

        if (identity.CurrentIdentity == FranchisePhilosophy.OffensiveHockey && position is RosterPosition.Center or RosterPosition.LeftWing or RosterPosition.RightWing)
        {
            score += 12;
            reasons.Add("Forward fits an offense-forward development environment.");
        }

        if (identity.Culture is FranchiseCulture.DevelopmentCulture or FranchiseCulture.PlayerFriendly)
        {
            score += 8;
            reasons.Add($"Culture fit is helped by the club's {Readable(identity.Culture).ToLowerInvariant()} reputation.");
        }

        if (reasons.Count == 0)
        {
            reasons.Add($"Fit is neutral because {identity.TeamName}'s identity does not strongly favor this profile yet.");
        }

        score = Math.Clamp(score, 0, 100);
        var label = score >= 80 ? "Excellent Fit" : score >= 65 ? "Good Fit" : score >= 45 ? "Neutral Fit" : "Poor Fit";
        var result = new FranchiseFitResult(
            personId,
            person.Identity.DisplayName,
            "Player",
            label,
            score,
            reasons,
            $"{label}: {person.Identity.DisplayName} fits {identity.TeamName}'s {Readable(identity.CurrentIdentity).ToLowerInvariant()} identity at {score}/100.");
        result.Validate();
        return result;
    }

    public FranchiseFitResult EvaluateStaffFit(NewGmScenarioSnapshot scenario, string personId)
    {
        var identity = PlayerIdentity(scenario);
        var person = scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)
            ?? scenario.StaffCandidates.Select(candidate => candidate.Person).FirstOrDefault(person => person.PersonId == personId)
            ?? throw new ArgumentException("Staff member was not found for franchise fit.", nameof(personId));
        var staff = scenario.StaffMembers.FirstOrDefault(member => member.PersonId == personId)
            ?? scenario.StaffCandidates.FirstOrDefault(candidate => candidate.Person.PersonId == personId)?.StaffMember;
        var role = staff?.CurrentRole ?? StaffRole.AssistantCoach;
        var score = 55;
        var reasons = new List<string>();

        if (identity.CurrentIdentity is FranchisePhilosophy.DraftAndDevelop or FranchisePhilosophy.ProspectOrganization
            && role is StaffRole.DevelopmentCoach or StaffRole.SkillsCoach or StaffRole.DirectorOfScouting or StaffRole.HeadScout or StaffRole.Scout or StaffRole.RegionalScout or StaffRole.AmateurScout)
        {
            score += 18;
            reasons.Add("Staff role supports a draft-and-develop franchise identity.");
        }

        if (identity.CurrentIdentity == FranchisePhilosophy.DefenseFirst && role is StaffRole.HeadCoach or StaffRole.AssistantCoach)
        {
            score += 10;
            reasons.Add("Coach can reinforce the club's defense-first expectations.");
        }

        if (identity.CurrentIdentity == FranchisePhilosophy.GoaltendingFactory && role is StaffRole.GoalieCoach or StaffRole.GoaltendingCoach or StaffRole.GoaltendingScout)
        {
            score += 20;
            reasons.Add("Goalie staff directly matches the organization's goalie-development DNA.");
        }

        if (identity.Culture is FranchiseCulture.Professional or FranchiseCulture.Disciplined or FranchiseCulture.HardWorking)
        {
            score += 7;
            reasons.Add($"Culture fit is helped by a {Readable(identity.Culture).ToLowerInvariant()} organization standard.");
        }

        if (reasons.Count == 0)
        {
            reasons.Add("Staff fit is neutral until the role and department prove a clearer culture match.");
        }

        score = Math.Clamp(score, 0, 100);
        var label = score >= 80 ? "Excellent Fit" : score >= 65 ? "Good Fit" : score >= 45 ? "Neutral Fit" : "Poor Fit";
        var result = new FranchiseFitResult(
            personId,
            person.Identity.DisplayName,
            "Staff",
            label,
            score,
            reasons,
            $"{label}: {person.Identity.DisplayName} fits {identity.TeamName}'s {Readable(identity.Culture).ToLowerInvariant()} culture at {score}/100.");
        result.Validate();
        return result;
    }

    public FranchiseIdentity PlayerIdentity(NewGmScenarioSnapshot scenario) =>
        FindIdentity(EnsureIdentities(scenario), scenario.Organization.OrganizationId);

    private static FranchiseIdentity FindIdentity(NewGmScenarioSnapshot scenario, string organizationId) =>
        scenario.FranchiseIdentities.First(identity => identity.OrganizationId == organizationId);

    private static FranchisePhilosophy PhilosophyFrom(OrganizationLeagueProfile profile, OrganizationAiProfile ai) =>
        profile.Identity switch
        {
            LeagueTeamIdentity.DevelopmentOrganization => FranchisePhilosophy.DraftAndDevelop,
            LeagueTeamIdentity.DefensiveOrganization => FranchisePhilosophy.DefenseFirst,
            LeagueTeamIdentity.HighSkillOrganization => FranchisePhilosophy.OffensiveHockey,
            LeagueTeamIdentity.GoaltendingOrganization => FranchisePhilosophy.GoaltendingFactory,
            LeagueTeamIdentity.PhysicalOrganization => FranchisePhilosophy.PhysicalTeam,
            LeagueTeamIdentity.ChampionshipOrganization => FranchisePhilosophy.ChampionshipCulture,
            LeagueTeamIdentity.BudgetOrganization => FranchisePhilosophy.BudgetBuilder,
            LeagueTeamIdentity.RebuildingOrganization => FranchisePhilosophy.ProspectOrganization,
            _ => ai.Personality switch
            {
                OrganizationAiPersonality.DraftAndDevelop or OrganizationAiPersonality.PatientRebuilder or OrganizationAiPersonality.ProspectHoarder => FranchisePhilosophy.DraftAndDevelop,
                OrganizationAiPersonality.DefenseFirst => FranchisePhilosophy.DefenseFirst,
                OrganizationAiPersonality.SkillFirst => FranchisePhilosophy.OffensiveHockey,
                OrganizationAiPersonality.GoalieFocused => FranchisePhilosophy.GoaltendingFactory,
                OrganizationAiPersonality.BigSpender or OrganizationAiPersonality.WinNow => FranchisePhilosophy.ChampionshipCulture,
                OrganizationAiPersonality.BudgetConscious => FranchisePhilosophy.BudgetBuilder,
                OrganizationAiPersonality.AggressiveTrader => FranchisePhilosophy.AggressiveMarketBuilder,
                _ => FranchisePhilosophy.ProfessionalOrganization
            }
        };

    private static FranchiseCulture CultureFrom(OrganizationLeagueProfile profile, OrganizationAiProfile ai) =>
        profile.Identity switch
        {
            LeagueTeamIdentity.ChampionshipOrganization => FranchiseCulture.WinningCulture,
            LeagueTeamIdentity.DevelopmentOrganization or LeagueTeamIdentity.RebuildingOrganization => FranchiseCulture.DevelopmentCulture,
            LeagueTeamIdentity.PhysicalOrganization => FranchiseCulture.HardWorking,
            LeagueTeamIdentity.BudgetOrganization => FranchiseCulture.Disciplined,
            LeagueTeamIdentity.VeteranOrganization => FranchiseCulture.Professional,
            _ => CultureFrom(PhilosophyFrom(profile, ai))
        };

    private static FranchiseCulture CultureFrom(FranchisePhilosophy philosophy) =>
        philosophy switch
        {
            FranchisePhilosophy.DraftAndDevelop or FranchisePhilosophy.ProspectOrganization => FranchiseCulture.DevelopmentCulture,
            FranchisePhilosophy.ChampionshipCulture => FranchiseCulture.WinningCulture,
            FranchisePhilosophy.PhysicalTeam => FranchiseCulture.HardWorking,
            FranchisePhilosophy.DefenseFirst => FranchiseCulture.Disciplined,
            FranchisePhilosophy.BudgetBuilder => FranchiseCulture.Demanding,
            FranchisePhilosophy.FastTeam or FranchisePhilosophy.OffensiveHockey => FranchiseCulture.PlayerFriendly,
            _ => FranchiseCulture.Professional
        };

    private static FranchiseDirection DirectionFrom(OrganizationAiProfile ai) =>
        ai.Strategy.Phase switch
        {
            OrganizationStrategyPhase.Rebuilding => FranchiseDirection.Rebuild,
            OrganizationStrategyPhase.Developing => FranchiseDirection.DevelopCore,
            OrganizationStrategyPhase.Retooling => FranchiseDirection.Retool,
            OrganizationStrategyPhase.Competing => FranchiseDirection.Compete,
            OrganizationStrategyPhase.Contending or OrganizationStrategyPhase.AllIn => FranchiseDirection.Contend,
            OrganizationStrategyPhase.BudgetReset => FranchiseDirection.BudgetReset,
            _ => FranchiseDirection.Sustain
        };

    private static FranchiseReputation ReputationFrom(OrganizationLeagueProfile profile, OrganizationAiProfile ai)
    {
        if (profile.Identity == LeagueTeamIdentity.ChampionshipOrganization)
        {
            return FranchiseReputation.EliteOrganization;
        }

        if (profile.Identity == LeagueTeamIdentity.DevelopmentOrganization)
        {
            return FranchiseReputation.ModelOrganization;
        }

        return ai.Strategy.Phase switch
        {
            OrganizationStrategyPhase.Rebuilding => FranchiseReputation.Rebuilding,
            OrganizationStrategyPhase.BudgetReset => FranchiseReputation.BudgetTeam,
            OrganizationStrategyPhase.Contending or OrganizationStrategyPhase.AllIn => FranchiseReputation.Respected,
            _ => FranchiseReputation.Average
        };
    }

    private static FranchisePhilosophy PriorIdentity(FranchisePhilosophy current, TeamSelectionOption team) =>
        current switch
        {
            FranchisePhilosophy.DraftAndDevelop => FranchisePhilosophy.ProspectOrganization,
            FranchisePhilosophy.DefenseFirst => FranchisePhilosophy.PhysicalTeam,
            FranchisePhilosophy.OffensiveHockey => FranchisePhilosophy.FastTeam,
            FranchisePhilosophy.GoaltendingFactory => FranchisePhilosophy.DefenseFirst,
            FranchisePhilosophy.ChampionshipCulture => FranchisePhilosophy.DraftAndDevelop,
            _ => team.ProspectStrength.Contains("High", StringComparison.OrdinalIgnoreCase) ? FranchisePhilosophy.DraftAndDevelop : FranchisePhilosophy.ProfessionalOrganization
        };

    private static FranchiseHistory BuildHistory(NewGmScenarioSnapshot scenario, TeamSelectionOption team, FranchisePhilosophy current, FranchiseReputation reputation)
    {
        var championships = reputation is FranchiseReputation.EliteOrganization or FranchiseReputation.ModelOrganization ? 1 : 0;
        if (scenario.OrganizationHistory is not null && team.OrganizationId == scenario.Organization.OrganizationId && scenario.OrganizationHistory.PreviousLeagueChampion == team.TeamName)
        {
            championships++;
        }

        var playoffAppearances = reputation is FranchiseReputation.PoorlyRun or FranchiseReputation.Rebuilding ? 1 : 4 + championships;
        var history = new FranchiseHistory(
            PlayoffAppearances: playoffAppearances,
            Championships: championships,
            FinalsAppearances: championships + (reputation == FranchiseReputation.Respected ? 1 : 0),
            Rebuilds: current is FranchisePhilosophy.ProspectOrganization or FranchisePhilosophy.BudgetBuilder ? 1 : 0,
            Dynasties: reputation == FranchiseReputation.EliteOrganization ? 1 : 0,
            LongestPlayoffStreak: Math.Max(1, playoffAppearances / 2),
            WorstSeason: $"{scenario.Season.Year - 5}: {team.PreviousRecord}",
            GreatestDraftClass: $"{scenario.Season.Year - 2}: class remembered for depth and development stories.",
            BestTrade: $"{scenario.Season.Year - 3}: acquired core help without sacrificing the whole pipeline.");
        history.Validate();
        return history;
    }

    private static IEnumerable<string> CurrentEraAchievements(NewGmScenarioSnapshot scenario, TeamSelectionOption team, FranchisePhilosophy identity, FranchiseReputation reputation)
    {
        yield return $"{team.PreviousRecord} previous-season baseline.";
        yield return $"{Readable(identity)} became the active front-office identity.";
        if (reputation is FranchiseReputation.EliteOrganization or FranchiseReputation.ModelOrganization)
        {
            yield return $"{Readable(reputation)} reputation remains visible around the league.";
        }

        if (team.OrganizationId == scenario.Organization.OrganizationId && scenario.ProspectRights.Count > 0)
        {
            yield return "Draft rights and prospect pipeline are tracked by the new GM office.";
        }
    }

    private static IEnumerable<string> HistoricalEraAchievements(TeamSelectionOption team, FranchisePhilosophy prior)
    {
        yield return $"{team.TeamName} established a {Readable(prior).ToLowerInvariant()} foundation.";
        yield return $"Prior record context: {team.PreviousRecord}.";
    }

    private static IEnumerable<string> BuildTeamDna(FranchisePhilosophy identity, FranchiseCulture culture, OrganizationAiProfile ai, TeamSelectionOption team)
    {
        yield return DnaFrom(identity);
        yield return $"{Readable(culture)} culture";
        yield return ai.Personality switch
        {
            OrganizationAiPersonality.AggressiveTrader => "Aggressive traders",
            OrganizationAiPersonality.GoalieFocused => "Goalie investment",
            OrganizationAiPersonality.BudgetConscious => "Budget discipline",
            OrganizationAiPersonality.DraftAndDevelop or OrganizationAiPersonality.PatientRebuilder => "Patient prospect runway",
            _ => $"{Readable(ai.Personality)} front office"
        };
        yield return $"{team.ProspectStrength} prospect base";
    }

    private static string DnaFrom(FranchisePhilosophy identity) =>
        identity switch
        {
            FranchisePhilosophy.DraftAndDevelop => "Finds and develops own players",
            FranchisePhilosophy.DefenseFirst => "Develops defensemen",
            FranchisePhilosophy.OffensiveHockey => "Prioritizes skill and scoring",
            FranchisePhilosophy.GoaltendingFactory => "Elite goaltending pathway",
            FranchisePhilosophy.EuropeanPipeline => "European pipeline",
            FranchisePhilosophy.PhysicalTeam => "Heavy, hard-working roster identity",
            FranchisePhilosophy.FastTeam => "Fast team identity",
            FranchisePhilosophy.ProspectOrganization => "Prospect-first organization",
            FranchisePhilosophy.ChampionshipCulture => "Championship expectations",
            FranchisePhilosophy.BudgetBuilder => "Budget team",
            FranchisePhilosophy.AggressiveMarketBuilder => "Aggressive market movers",
            _ => "Professional operating standards"
        };

    private static IEnumerable<string> BuildStrengths(FranchisePhilosophy identity, FranchiseCulture culture, OrganizationAiProfile ai, TeamSelectionOption team)
    {
        yield return $"{Readable(identity)} clarity helps staff and roster decisions.";
        yield return $"{Readable(culture)} gives the club a consistent workplace standard.";
        yield return ai.CurrentNeeds.Count > 0 ? $"Front office understands its top need: {Readable(ai.CurrentNeeds.First().NeedType)}." : $"{team.TeamName} has a stable planning base.";
    }

    private static IEnumerable<string> BuildWeaknesses(FranchisePhilosophy identity, OrganizationAiProfile ai, TeamSelectionOption team)
    {
        if (ai.Strategy.Phase == OrganizationStrategyPhase.BudgetReset)
        {
            yield return "Budget pressure can make the identity harder to sustain.";
        }
        else if (ai.Strategy.Phase == OrganizationStrategyPhase.Rebuilding)
        {
            yield return "Rebuild patience will be tested by owner and fan expectations.";
        }
        else
        {
            yield return $"{Readable(identity)} can become rigid if the staff stops challenging assumptions.";
        }

        yield return $"{team.RosterQuality} roster quality creates pressure to prove the identity on the ice.";
    }

    private static IEnumerable<string> BuildFutureGoals(FranchiseDirection direction, FranchisePhilosophy identity, OrganizationAiProfile ai)
    {
        yield return direction switch
        {
            FranchiseDirection.Rebuild => "Accumulate picks, prospects, and patience.",
            FranchiseDirection.DevelopCore => "Turn the young core into reliable NHL/AHL/junior contributors.",
            FranchiseDirection.Retool => "Refresh the roster without losing the identity.",
            FranchiseDirection.Compete => "Convert identity into a playoff-level standard.",
            FranchiseDirection.Contend => "Protect the window while replenishing the pipeline.",
            FranchiseDirection.BudgetReset => "Lower commitments without gutting the culture.",
            _ => "Sustain current strengths and avoid drift."
        };
        yield return $"Keep the {Readable(identity).ToLowerInvariant()} philosophy visible in draft, development, staff, and trades.";
        yield return $"Address top AI need: {Readable(ai.CurrentNeeds.First().NeedType)}.";
    }

    private static string EraName(FranchisePhilosophy identity, FranchiseDirection direction) =>
        $"{Readable(identity)} {Readable(direction)} Era";

    private static string HeadCoachName(NewGmScenarioSnapshot scenario, string organizationId)
    {
        if (organizationId != scenario.Organization.OrganizationId)
        {
            return "Current coaching staff";
        }

        var coach = scenario.StaffMembers.FirstOrDefault(staff => staff.CurrentRole == StaffRole.HeadCoach);
        return coach is null
            ? "Head coach not assigned"
            : scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == coach.PersonId)?.Identity.DisplayName ?? "Head coach";
    }

    private static string NewsHeadline(FranchiseIdentity identity) =>
        identity.FutureDirection switch
        {
            FranchiseDirection.Rebuild => $"{identity.TeamName} franchise identity appears committed to a long-term rebuild built around {Readable(identity.CurrentIdentity).ToLowerInvariant()}.",
            FranchiseDirection.DevelopCore => $"{identity.TeamName} is becoming known as a {Readable(identity.CurrentIdentity).ToLowerInvariant()} organization with a clear culture.",
            FranchiseDirection.Contend => $"{identity.TeamName} continues to lean on its {Readable(identity.Culture).ToLowerInvariant()} culture during its contention window.",
            FranchiseDirection.BudgetReset => $"{identity.TeamName} is trying to protect its identity while resetting budget commitments.",
            _ => $"{identity.TeamName} continues shaping its franchise identity around {Readable(identity.CurrentIdentity).ToLowerInvariant()}."
        };

    private static RosterPosition ResolvePosition(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.Roster.Players.FirstOrDefault(player => player.PersonId == personId)?.Position
        ?? scenario.ProspectRights.FirstOrDefault(prospect => prospect.ProspectPersonId == personId)?.Position
        ?? scenario.AlphaSnapshot.DraftBoard.Entries.FirstOrDefault(entry => entry.ProspectPersonId == personId)?.Bio?.Position
        ?? scenario.FreeAgentMarket?.Find(personId)?.Position
        ?? RosterPosition.Unknown;

    private static string Readable(Enum value)
    {
        var text = value.ToString();
        return string.Concat(text.Select((letter, index) => index > 0 && char.IsUpper(letter) ? $" {letter}" : letter.ToString()));
    }

    private static int StableHash(string text)
    {
        unchecked
        {
            var hash = 23;
            foreach (var character in text)
            {
                hash = hash * 31 + character;
            }

            return Math.Abs(hash);
        }
    }
}
