using LegacyEngine.Rosters;
using LegacyEngine.Staff;

namespace LegacyEngine.Integration;

public sealed class OrganizationAiService
{
    public NewGmScenarioSnapshot EnsureProfiles(NewGmScenarioSnapshot scenario, BudgetSnapshot? budget = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        budget ??= new BudgetOverviewService().Build(scenario, scenario.LeagueProfile.Rulebook);
        var teams = SeasonFrameworkService.LeagueTeams(scenario).ToArray();
        var existing = scenario.OrganizationAiProfiles;
        if (existing.Count >= teams.Length && teams.All(team => existing.Any(profile => profile.OrganizationId == team.OrganizationId)))
        {
            return scenario;
        }

        var leagueProfiles = teams
            .Select(team => new LeagueAiService().BuildOrganizationProfile(scenario, team.OrganizationId, team.TeamName, budget))
            .ToArray();
        var aiProfiles = BuildLeagueProfilesFromLeagueProfiles(scenario, leagueProfiles, budget);
        return scenario with { OrganizationAiProfiles = aiProfiles };
    }

    public IReadOnlyList<OrganizationAiProfile> BuildLeagueProfilesFromLeagueProfiles(
        NewGmScenarioSnapshot scenario,
        IReadOnlyList<OrganizationLeagueProfile> leagueProfiles,
        BudgetSnapshot? budget = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(leagueProfiles);
        budget ??= new BudgetOverviewService().Build(scenario, scenario.LeagueProfile.Rulebook);

        var profiles = leagueProfiles
            .Select(profile => BuildProfile(scenario, profile, budget))
            .ToArray();
        foreach (var profile in profiles)
        {
            profile.Validate();
        }

        return profiles;
    }

    public OrganizationAiProfile BuildProfile(NewGmScenarioSnapshot scenario, OrganizationLeagueProfile profile, BudgetSnapshot? budget = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();

        budget ??= new BudgetOverviewService().Build(scenario, scenario.LeagueProfile.Rulebook);
        var saved = scenario.OrganizationAiProfiles.FirstOrDefault(item => item.OrganizationId == profile.OrganizationId);
        var personality = saved?.Personality ?? PersonalityFor(profile);
        var phase = saved?.Strategy.Phase ?? PhaseFor(profile.CurrentStrategy);
        var risk = saved?.Strategy.RiskTolerance ?? RiskToleranceFor(personality);
        var needs = BuildNeeds(profile, budget, profile.OrganizationId == scenario.Organization.OrganizationId);
        var strategy = BuildStrategy(profile, personality, phase, risk, needs, budget);
        var history = saved?.StrategyHistory.Count > 0
            ? saved.StrategyHistory
            : new[]
            {
                new OrganizationStrategyChange(
                    scenario.CurrentDate,
                    OrganizationStrategyPhase.Unknown,
                    phase,
                    $"{profile.TeamName} enters {Readable(phase).ToLowerInvariant()} mode based on roster direction, owner context, budget, and prospect pipeline.")
            };
        var summary = $"{profile.TeamName}: {Readable(personality)} profile in {Readable(phase).ToLowerInvariant()} phase. Top needs: {string.Join(", ", needs.Take(3).Select(need => Readable(need.NeedType)))}.";
        var ai = new OrganizationAiProfile(
            profile.OrganizationId,
            profile.TeamName,
            personality,
            strategy,
            needs,
            history,
            scenario.CurrentDate,
            summary);
        ai.Validate();
        return ai;
    }

    public AiDecisionResult EvaluateDecision(OrganizationAiProfile profile, AiDecisionContext context)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(context);
        profile.Validate();
        context.Validate();

        var score = 50;
        var reasons = new List<string>
        {
            $"{profile.TeamName} is operating as {Readable(profile.Personality)} in a {Readable(profile.Strategy.Phase).ToLowerInvariant()} phase."
        };

        ApplyStrategyScore(profile, context, ref score, reasons);
        ApplyPersonalityScore(profile, context, ref score, reasons);
        ApplyNeedScore(profile, context, ref score, reasons);
        ApplyCategoryScore(profile, context, ref score, reasons);

        score = Math.Clamp(score, 0, 100);
        var outcome = score switch
        {
            >= 82 => AiDecisionOutcome.VeryInterested,
            >= 65 => AiDecisionOutcome.Accept,
            >= 52 => AiDecisionOutcome.Counter,
            >= 40 => AiDecisionOutcome.Wait,
            >= 25 => AiDecisionOutcome.Reject,
            _ => AiDecisionOutcome.NotInterested
        };
        var result = new AiDecisionResult(
            outcome,
            score,
            reasons,
            PreferredAssetsFor(profile),
            $"{profile.TeamName} decision: {Readable(outcome)} because the option scores {score}/100 against strategy, needs, budget, and risk.");
        result.Validate();
        return result;
    }

    public AiDecisionResult EvaluateDraftDecision(OrganizationAiProfile profile, Draft.DraftBoardEntry entry, DraftClassProfile? draftClass = null)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var assets = new List<AiAssetType> { AiAssetType.ProspectRights, AiAssetType.YoungUpside };
        if (entry.Bio?.Position == RosterPosition.Goalie)
        {
            assets.Add(AiAssetType.Goalie);
        }
        else if (entry.Bio?.Position == RosterPosition.Defense)
        {
            assets.Add(AiAssetType.Defenseman);
        }
        else if (entry.Bio?.Position is RosterPosition.Center or RosterPosition.LeftWing or RosterPosition.RightWing)
        {
            assets.Add(AiAssetType.ScoringForward);
        }

        var context = new AiDecisionContext(
            AiDecisionCategory.Draft,
            assets,
            Position: entry.Bio?.Position,
            Age: 17,
            DraftClassTheme: draftClass?.Theme,
            IsHighRisk: entry.RiskSummary.Contains("risk", StringComparison.OrdinalIgnoreCase) || entry.ProjectionText.Contains("boom", StringComparison.OrdinalIgnoreCase),
            Notes: entry.ProjectionText);
        return EvaluateDecision(profile, context);
    }

    public AiDecisionResult EvaluateTradeDecision(OrganizationAiProfile profile, IReadOnlyList<TradeAsset> incomingAssets, decimal budgetImpact)
    {
        var assets = incomingAssets
            .SelectMany(AssetTypesFor)
            .DefaultIfEmpty(AiAssetType.FutureConsideration)
            .Distinct()
            .ToArray();
        var age = incomingAssets.Count == 0 ? 18 : (int)Math.Round(incomingAssets.Average(asset => asset.Age ?? 18));
        var position = incomingAssets.FirstOrDefault(asset => asset.Position.HasValue)?.Position;
        var context = new AiDecisionContext(AiDecisionCategory.Trade, assets, budgetImpact, position, age);
        return EvaluateDecision(profile, context);
    }

    public AiDecisionResult EvaluateFreeAgencyDecision(OrganizationAiProfile profile, decimal salaryAsk, int age, RosterPosition position, bool immediateHelp)
    {
        var assets = new List<AiAssetType> { AiAssetType.FreeAgentContract };
        if (immediateHelp)
        {
            assets.Add(AiAssetType.VeteranHelpNow);
        }

        if (age <= 20)
        {
            assets.Add(AiAssetType.YoungUpside);
        }

        if (position == RosterPosition.Goalie)
        {
            assets.Add(AiAssetType.Goalie);
        }
        else if (position == RosterPosition.Defense)
        {
            assets.Add(AiAssetType.Defenseman);
        }
        else
        {
            assets.Add(AiAssetType.ScoringForward);
        }

        return EvaluateDecision(profile, new AiDecisionContext(AiDecisionCategory.FreeAgency, assets, salaryAsk, position, age));
    }

    public AiDecisionResult EvaluateStaffDecision(OrganizationAiProfile profile, StaffRole role, decimal salaryAsk)
    {
        return EvaluateDecision(profile, new AiDecisionContext(
            AiDecisionCategory.StaffHiring,
            new[] { AiAssetType.StaffCandidate },
            salaryAsk,
            StaffRole: role));
    }

    public OrganizationAiProfile EvolveStrategy(
        OrganizationAiProfile profile,
        DateOnly date,
        double pointsPercentage,
        int averageRosterAge,
        int consecutiveLosingSeasons,
        int prospectPoolStrength,
        decimal budgetPressure)
    {
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();

        var nextPhase = profile.Strategy.Phase;
        var reason = string.Empty;
        if (budgetPressure > 0m)
        {
            nextPhase = OrganizationStrategyPhase.BudgetReset;
            reason = "budget pressure forced a reset in roster-building posture.";
        }
        else if (pointsPercentage <= 0.38 && consecutiveLosingSeasons >= 2)
        {
            nextPhase = OrganizationStrategyPhase.Rebuilding;
            reason = "consecutive losing seasons pushed the club toward picks and prospects.";
        }
        else if (averageRosterAge >= 23 && profile.Strategy.Phase is OrganizationStrategyPhase.Contending or OrganizationStrategyPhase.AllIn)
        {
            nextPhase = OrganizationStrategyPhase.Retooling;
            reason = "an aging roster moved the club out of its peak contention window.";
        }
        else if (prospectPoolStrength >= 70 && pointsPercentage >= 0.54 && profile.Strategy.Phase == OrganizationStrategyPhase.Developing)
        {
            nextPhase = OrganizationStrategyPhase.Competing;
            reason = "young internal growth pushed the club closer to competing.";
        }
        else if (pointsPercentage >= 0.66 && profile.Strategy.Phase == OrganizationStrategyPhase.Competing)
        {
            nextPhase = OrganizationStrategyPhase.Contending;
            reason = "strong results made the club more willing to spend assets for help now.";
        }

        if (nextPhase == profile.Strategy.Phase)
        {
            return profile;
        }

        var change = new OrganizationStrategyChange(date, profile.Strategy.Phase, nextPhase, reason);
        var nextStrategy = profile.Strategy with
        {
            Phase = nextPhase,
            Summary = $"{profile.TeamName} shifted from {Readable(profile.Strategy.Phase).ToLowerInvariant()} to {Readable(nextPhase).ToLowerInvariant()} because {reason}"
        };
        var next = profile with
        {
            Strategy = nextStrategy,
            StrategyHistory = profile.StrategyHistory.Append(change).ToArray(),
            LastUpdated = date,
            Summary = $"{profile.TeamName}: {Readable(profile.Personality)} profile now in {Readable(nextPhase).ToLowerInvariant()} phase."
        };
        next.Validate();
        return next;
    }

    public NewGmScenarioSnapshot RecordStrategyHistory(NewGmScenarioSnapshot scenario, OrganizationAiProfile profile)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();

        var latest = profile.StrategyHistory.LastOrDefault();
        var entry = new CareerTimelineEntry(
            $"career:organization-ai-strategy:{profile.OrganizationId}:{profile.Strategy.Phase}:{profile.LastUpdated:yyyyMMdd}",
            CareerTimelineEntryType.OrganizationStrategyChanged,
            latest?.Date ?? profile.LastUpdated,
            scenario.Season.Year,
            null,
            profile.OrganizationId,
            profile.TeamName,
            $"{profile.TeamName} strategy: {Readable(profile.Strategy.Phase)}",
            latest?.Reason ?? profile.Strategy.Summary,
            null,
            profile.OrganizationId == scenario.Organization.OrganizationId ? HistoryImportance.Important : HistoryImportance.Normal);
        return scenario with { CareerTimeline = scenario.CareerTimeline.Add(entry) };
    }

    public IReadOnlyList<LeagueTransaction> BuildStrategyLeagueNews(NewGmScenarioSnapshot scenario, IReadOnlyList<OrganizationAiProfile> profiles, int maxItems = 3)
    {
        var monthKey = scenario.CurrentDate.Year * 100 + scenario.CurrentDate.Month;
        return profiles
            .Where(profile => profile.OrganizationId != scenario.Organization.OrganizationId)
            .Where(profile => StableHash($"{profile.OrganizationId}:{profile.Personality}:{monthKey}:ai-v2") % 3 == 0)
            .OrderBy(profile => profile.TeamName, StringComparer.Ordinal)
            .Take(maxItems)
            .Select(profile => new LeagueTransaction(
                $"league-ai-v2:{profile.OrganizationId}:{monthKey}",
                new DateTimeOffset(scenario.CurrentDate.Year, scenario.CurrentDate.Month, scenario.CurrentDate.Day, 11, 20, 0, TimeSpan.Zero),
                profile.OrganizationId,
                profile.TeamName,
                null,
                Readable(profile.Strategy.Phase),
                LeagueTransactionType.TeamIdentityUpdate,
                LeagueNewsCategory.League,
                StrategyNewsHeadline(profile)))
            .ToArray();
    }

    public static string StrategyNewsHeadline(OrganizationAiProfile profile) =>
        profile.Strategy.Phase switch
        {
            OrganizationStrategyPhase.Rebuilding => $"{profile.TeamName} appears to be entering a rebuild and is prioritizing draft picks.",
            OrganizationStrategyPhase.BudgetReset => $"{profile.TeamName} is expected to seek budget relief before adding salary.",
            OrganizationStrategyPhase.Contending or OrganizationStrategyPhase.AllIn => $"{profile.TeamName} is aggressively pursuing veteran help for the current window.",
            OrganizationStrategyPhase.Developing => $"{profile.TeamName} is leaning into internal development and young-player runway.",
            _ => $"{profile.TeamName} is expected to stay selective while targeting {Readable(profile.CurrentNeeds.First().NeedType).ToLowerInvariant()}."
        };

    private static OrganizationAiPersonality PersonalityFor(OrganizationLeagueProfile profile) =>
        profile.Identity switch
        {
            LeagueTeamIdentity.DevelopmentOrganization => OrganizationAiPersonality.DraftAndDevelop,
            LeagueTeamIdentity.RebuildingOrganization => OrganizationAiPersonality.PatientRebuilder,
            LeagueTeamIdentity.BudgetOrganization => OrganizationAiPersonality.BudgetConscious,
            LeagueTeamIdentity.ChampionshipOrganization => OrganizationAiPersonality.BigSpender,
            LeagueTeamIdentity.DefensiveOrganization => OrganizationAiPersonality.DefenseFirst,
            LeagueTeamIdentity.HighSkillOrganization => OrganizationAiPersonality.SkillFirst,
            LeagueTeamIdentity.GoaltendingOrganization => OrganizationAiPersonality.GoalieFocused,
            LeagueTeamIdentity.VeteranOrganization => OrganizationAiPersonality.VeteranBuilder,
            _ => profile.GmPersonality switch
            {
                LeagueGmPersonality.AggressiveTrader => OrganizationAiPersonality.AggressiveTrader,
                LeagueGmPersonality.ProspectHoarder => OrganizationAiPersonality.ProspectHoarder,
                LeagueGmPersonality.DraftPickCollector => OrganizationAiPersonality.DraftAndDevelop,
                LeagueGmPersonality.VeteranBuilder => OrganizationAiPersonality.VeteranBuilder,
                LeagueGmPersonality.BudgetFocused => OrganizationAiPersonality.BudgetConscious,
                LeagueGmPersonality.RiskTaker => OrganizationAiPersonality.RiskTaker,
                LeagueGmPersonality.WinNow => OrganizationAiPersonality.WinNow,
                LeagueGmPersonality.PatientBuilder => OrganizationAiPersonality.PatientRebuilder,
                _ => OrganizationAiPersonality.Conservative
            }
        };

    private static OrganizationStrategyPhase PhaseFor(OrganizationStrategyStage stage) =>
        stage switch
        {
            OrganizationStrategyStage.FiveYearRebuild => OrganizationStrategyPhase.Rebuilding,
            OrganizationStrategyStage.ProspectAccumulation => OrganizationStrategyPhase.Developing,
            OrganizationStrategyStage.BudgetRecovery => OrganizationStrategyPhase.BudgetReset,
            OrganizationStrategyStage.ChampionshipWindow => OrganizationStrategyPhase.Contending,
            OrganizationStrategyStage.StableDevelopment => OrganizationStrategyPhase.Developing,
            OrganizationStrategyStage.CompetitiveRetool => OrganizationStrategyPhase.Retooling,
            _ => OrganizationStrategyPhase.Unknown
        };

    private static IReadOnlyList<TeamNeedProfile> BuildNeeds(OrganizationLeagueProfile profile, BudgetSnapshot budget, bool playerTeam)
    {
        var needs = profile.CurrentNeeds.Select(MapNeed).ToList();
        if (profile.Identity == LeagueTeamIdentity.PhysicalOrganization && needs.All(need => need.NeedType != TeamNeedType.Physicality))
        {
            needs.Add(new TeamNeedProfile(TeamNeedType.Physicality, TradePriority.Medium, "Team identity values harder matchups and heavier minutes.", "Before next roster review", AiAssetType.RosterPlayer));
        }

        if (profile.Identity == LeagueTeamIdentity.DefensiveOrganization && needs.All(need => need.NeedType != TeamNeedType.TopPairDefense))
        {
            needs.Add(new TeamNeedProfile(TeamNeedType.TopPairDefense, TradePriority.High, "Defensive identity needs another trusted matchup defender.", "Before deadline", AiAssetType.Defenseman));
        }

        if (playerTeam && budget.Status == BudgetStatus.OverBudget && needs.All(need => need.NeedType != TeamNeedType.BudgetRelief))
        {
            needs.Add(new TeamNeedProfile(TeamNeedType.BudgetRelief, TradePriority.Urgent, "Owner budget pressure is already visible.", "Immediate", AiAssetType.BudgetRelief));
        }

        return needs
            .OrderByDescending(need => need.Priority)
            .ThenBy(need => need.NeedType)
            .Take(7)
            .ToArray();
    }

    private static TeamNeedProfile MapNeed(TeamNeed need) =>
        need.Need switch
        {
            PositionNeed.StartingGoalie => new TeamNeedProfile(TeamNeedType.StartingGoalie, need.Priority, need.Reason, "Before next competitive window", AiAssetType.Goalie),
            PositionNeed.DefensiveDefenseman => new TeamNeedProfile(TeamNeedType.DefensiveDefenseman, need.Priority, need.Reason, "Before roster deadline", AiAssetType.Defenseman),
            PositionNeed.TopSixForward => new TeamNeedProfile(TeamNeedType.TopSixForward, need.Priority, need.Reason, "Before next major lineup review", AiAssetType.ScoringForward),
            PositionNeed.ScoringDepth => new TeamNeedProfile(TeamNeedType.Scoring, need.Priority, need.Reason, "Before deadline", AiAssetType.ScoringForward),
            PositionNeed.ProspectDepth => new TeamNeedProfile(TeamNeedType.Prospects, need.Priority, need.Reason, "This season", AiAssetType.ProspectRights),
            PositionNeed.DraftPicks => new TeamNeedProfile(TeamNeedType.DraftPicks, need.Priority, need.Reason, "Next draft cycle", AiAssetType.DraftPick),
            PositionNeed.BudgetRelief or PositionNeed.CapSpaceFuture => new TeamNeedProfile(TeamNeedType.BudgetRelief, need.Priority, need.Reason, "Immediate", AiAssetType.BudgetRelief),
            PositionNeed.VeteranLeadership => new TeamNeedProfile(TeamNeedType.VeteranLeadership, need.Priority, need.Reason, "Before playoffs or camp", AiAssetType.VeteranHelpNow),
            _ => new TeamNeedProfile(TeamNeedType.Prospects, need.Priority, need.Reason, "This season", AiAssetType.YoungUpside)
        };

    private static OrganizationStrategy BuildStrategy(
        OrganizationLeagueProfile profile,
        OrganizationAiPersonality personality,
        OrganizationStrategyPhase phase,
        int risk,
        IReadOnlyList<TeamNeedProfile> needs,
        BudgetSnapshot budget)
    {
        var topNeed = Readable(needs.First().NeedType).ToLowerInvariant();
        var draft = personality switch
        {
            OrganizationAiPersonality.GoalieFocused => "Draft philosophy: goalies stay high on the board when confidence is acceptable.",
            OrganizationAiPersonality.DefenseFirst => "Draft philosophy: prioritizes defenders, habits, and role certainty.",
            OrganizationAiPersonality.SkillFirst or OrganizationAiPersonality.RiskTaker => "Draft philosophy: chases skill and upside even when reports carry uncertainty.",
            OrganizationAiPersonality.DraftAndDevelop or OrganizationAiPersonality.PatientRebuilder => "Draft philosophy: values younger upside, character, and long-run development fit.",
            _ => profile.Behavior.DraftBehavior
        };
        var trade = phase switch
        {
            OrganizationStrategyPhase.Rebuilding => "Trade behavior: shops veterans carefully and prefers picks, prospects, and younger players.",
            OrganizationStrategyPhase.Contending or OrganizationStrategyPhase.AllIn => "Trade behavior: pays for help-now assets when they solve a top need.",
            OrganizationStrategyPhase.BudgetReset => "Trade behavior: avoids taking salary unless budget relief comes back.",
            _ => profile.Behavior.TradeBehavior
        };
        var freeAgency = personality switch
        {
            OrganizationAiPersonality.BigSpender => "Free agency behavior: comfortable winning expensive bidding if fit is clear.",
            OrganizationAiPersonality.BudgetConscious => "Free agency behavior: waits for late-market value and avoids bidding wars.",
            OrganizationAiPersonality.PatientRebuilder or OrganizationAiPersonality.DraftAndDevelop => "Free agency behavior: avoids expensive veterans unless they support development.",
            OrganizationAiPersonality.WinNow => "Free agency behavior: targets immediate lineup certainty.",
            _ => profile.Behavior.FreeAgencyBehavior
        };
        var staff = personality switch
        {
            OrganizationAiPersonality.DraftAndDevelop or OrganizationAiPersonality.PatientRebuilder => "Staff behavior: invests in development, teaching, and scouting continuity.",
            OrganizationAiPersonality.DefenseFirst => "Staff behavior: prefers defensive coaches and detail-oriented assistants.",
            OrganizationAiPersonality.GoalieFocused => "Staff behavior: prioritizes goalie coaching and goalie scouting.",
            OrganizationAiPersonality.BudgetConscious => "Staff behavior: chemistry and salary fit beat reputation.",
            _ => profile.Behavior.StaffHiringBehavior
        };
        var strategy = new OrganizationStrategy(
            phase,
            $"{profile.TeamName} is in {Readable(phase).ToLowerInvariant()} mode with {topNeed} as the clearest roster-building pressure.",
            draft,
            trade,
            freeAgency,
            budget.Status == BudgetStatus.OverBudget ? "Budget behavior: pressure is high; spending must be justified." : profile.BudgetStyle,
            profile.Behavior.ScoutingBehavior,
            staff,
            risk);
        strategy.Validate();
        return strategy;
    }

    private static void ApplyStrategyScore(OrganizationAiProfile profile, AiDecisionContext context, ref int score, List<string> reasons)
    {
        if (profile.Strategy.Phase is OrganizationStrategyPhase.Rebuilding or OrganizationStrategyPhase.Developing)
        {
            if (context.Assets.Any(asset => asset is AiAssetType.DraftPick or AiAssetType.ProspectRights or AiAssetType.YoungUpside))
            {
                score += 18;
                reasons.Add("Rebuilding/developing strategy increases value for picks, prospects, and young upside.");
            }

            if (context.Assets.Contains(AiAssetType.VeteranHelpNow) && context.BudgetImpact > 25_000m)
            {
                score -= 16;
                reasons.Add("Rebuilding team is cautious with expensive veteran help.");
            }
        }

        if (profile.Strategy.Phase is OrganizationStrategyPhase.Contending or OrganizationStrategyPhase.AllIn)
        {
            if (context.Assets.Any(asset => asset is AiAssetType.VeteranHelpNow or AiAssetType.RosterPlayer or AiAssetType.ScoringForward))
            {
                score += 17;
                reasons.Add("Contending strategy raises value for immediate lineup help.");
            }
        }

        if (profile.Strategy.Phase == OrganizationStrategyPhase.BudgetReset)
        {
            if (context.BudgetImpact > 0m)
            {
                score -= 22;
                reasons.Add("Budget reset lowers interest in added spending.");
            }
            else
            {
                score += 12;
                reasons.Add("Budget reset rewards options that preserve or clear money.");
            }
        }
    }

    private static void ApplyPersonalityScore(OrganizationAiProfile profile, AiDecisionContext context, ref int score, List<string> reasons)
    {
        switch (profile.Personality)
        {
            case OrganizationAiPersonality.ProspectHoarder:
            case OrganizationAiPersonality.DraftAndDevelop:
            case OrganizationAiPersonality.PatientRebuilder:
                if (context.Assets.Any(asset => asset is AiAssetType.ProspectRights or AiAssetType.DraftPick or AiAssetType.YoungUpside))
                {
                    score += 14;
                    reasons.Add("Organization identity overvalues future assets.");
                }

                break;
            case OrganizationAiPersonality.BigSpender:
                if (context.Category == AiDecisionCategory.FreeAgency && context.BudgetImpact > 50_000m)
                {
                    score += 12;
                    reasons.Add("Big-spender personality is comfortable with a premium free-agent ask.");
                }

                break;
            case OrganizationAiPersonality.BudgetConscious:
                if (context.BudgetImpact > 30_000m)
                {
                    score -= 18;
                    reasons.Add("Budget-conscious personality resists expensive commitments.");
                }

                break;
            case OrganizationAiPersonality.GoalieFocused:
                if (context.Assets.Contains(AiAssetType.Goalie) || context.Position == RosterPosition.Goalie)
                {
                    score += 14;
                    reasons.Add("Goalie-focused organization boosts goalie value.");
                }

                break;
            case OrganizationAiPersonality.DefenseFirst:
                if (context.Assets.Contains(AiAssetType.Defenseman) || context.Position == RosterPosition.Defense)
                {
                    score += 12;
                    reasons.Add("Defense-first organization boosts defensive fits.");
                }

                break;
            case OrganizationAiPersonality.RiskTaker:
            case OrganizationAiPersonality.SkillFirst:
                if (context.IsHighRisk || context.Assets.Contains(AiAssetType.ScoringForward))
                {
                    score += 10;
                    reasons.Add("Risk/skill identity tolerates upside uncertainty.");
                }

                break;
            case OrganizationAiPersonality.Conservative:
                if (context.IsHighRisk)
                {
                    score -= 12;
                    reasons.Add("Conservative personality penalizes risk.");
                }

                break;
        }
    }

    private static void ApplyNeedScore(OrganizationAiProfile profile, AiDecisionContext context, ref int score, List<string> reasons)
    {
        var matched = profile.CurrentNeeds
            .Where(need => context.Assets.Contains(need.SuggestedAssetType))
            .OrderByDescending(need => need.Priority)
            .ToArray();
        if (matched.Length == 0)
        {
            score -= 4;
            reasons.Add("The option does not clearly address a top current need.");
            return;
        }

        var best = matched.First();
        score += best.Priority switch
        {
            TradePriority.Urgent => 18,
            TradePriority.High => 13,
            TradePriority.Medium => 8,
            _ => 4
        };
        reasons.Add($"The option addresses {Readable(best.NeedType).ToLowerInvariant()}: {best.Reason}");
    }

    private static void ApplyCategoryScore(OrganizationAiProfile profile, AiDecisionContext context, ref int score, List<string> reasons)
    {
        if (context.Category == AiDecisionCategory.Draft)
        {
            if (context.DraftClassTheme == DraftClassTheme.StrongGoalieClass && context.Assets.Contains(AiAssetType.Goalie))
            {
                score += 9;
                reasons.Add("Draft class context supports a goalie reach.");
            }

            if (context.DraftClassTheme == DraftClassTheme.DeepDefenseClass && context.Assets.Contains(AiAssetType.Defenseman))
            {
                score += 8;
                reasons.Add("Draft class context supports leaning into defensive depth.");
            }
        }

        if (context.Category == AiDecisionCategory.StaffHiring)
        {
            if (context.StaffRole is StaffRole.DevelopmentCoach or StaffRole.SkillsCoach && profile.Personality is OrganizationAiPersonality.DraftAndDevelop or OrganizationAiPersonality.PatientRebuilder)
            {
                score += 14;
                reasons.Add("Development-focused identity values teaching staff.");
            }

            if (context.StaffRole is StaffRole.GoalieCoach or StaffRole.GoaltendingCoach or StaffRole.GoaltendingScout && profile.Personality == OrganizationAiPersonality.GoalieFocused)
            {
                score += 14;
                reasons.Add("Goalie-focused identity values goalie staff.");
            }

            if (context.BudgetImpact > 80_000m && profile.Personality == OrganizationAiPersonality.BudgetConscious)
            {
                score -= 12;
                reasons.Add("Staff salary ask is risky for a budget team.");
            }
        }
    }

    private static IReadOnlyList<AiAssetType> PreferredAssetsFor(OrganizationAiProfile profile) =>
        profile.CurrentNeeds
            .Select(need => need.SuggestedAssetType)
            .Distinct()
            .Take(4)
            .ToArray();

    private static IEnumerable<AiAssetType> AssetTypesFor(TradeAsset asset)
    {
        yield return asset.AssetType switch
        {
            TradeAssetType.DraftPick => AiAssetType.DraftPick,
            TradeAssetType.ProspectRights => AiAssetType.ProspectRights,
            TradeAssetType.FutureConsideration => AiAssetType.FutureConsideration,
            _ => AiAssetType.RosterPlayer
        };

        if (asset.Age <= 18)
        {
            yield return AiAssetType.YoungUpside;
        }
        else if (asset.Age >= 20)
        {
            yield return AiAssetType.VeteranHelpNow;
        }

        if (asset.Position == RosterPosition.Goalie)
        {
            yield return AiAssetType.Goalie;
        }
        else if (asset.Position == RosterPosition.Defense)
        {
            yield return AiAssetType.Defenseman;
        }
        else if (asset.Position is RosterPosition.Center or RosterPosition.LeftWing or RosterPosition.RightWing)
        {
            yield return AiAssetType.ScoringForward;
        }
    }

    private static int RiskToleranceFor(OrganizationAiPersonality personality) =>
        personality switch
        {
            OrganizationAiPersonality.RiskTaker or OrganizationAiPersonality.BigSpender or OrganizationAiPersonality.AggressiveTrader => 75,
            OrganizationAiPersonality.SkillFirst or OrganizationAiPersonality.WinNow => 64,
            OrganizationAiPersonality.Conservative or OrganizationAiPersonality.BudgetConscious => 32,
            OrganizationAiPersonality.PatientRebuilder or OrganizationAiPersonality.DraftAndDevelop => 48,
            _ => 52
        };

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
