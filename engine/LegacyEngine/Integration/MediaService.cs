namespace LegacyEngine.Integration;

public sealed class MediaService
{
    private static readonly MediaSource LeagueWire = new("media-source:league-wire", "League Wire", "official league transactions and major events", MediaTone.Neutral, 82);
    private static readonly MediaSource HockeyDaily = new("media-source:hockey-daily", "Hockey Daily", "league-wide analysis and team context", MediaTone.Neutral, 76);
    private static readonly MediaSource ProspectCentral = new("media-source:prospect-central", "Prospect Central", "draft, prospects, and development", MediaTone.Positive, 71);
    private static readonly MediaSource FrontOfficeReport = new("media-source:front-office-report", "Front Office Report", "management, ownership, and roster building", MediaTone.Concerned, 68);
    private static readonly MediaSource LocalBeat = new("media-source:local-beat", "Local Beat", "local team coverage", MediaTone.Neutral, 64);
    private static readonly MediaSource DraftDesk = new("media-source:draft-desk", "Draft Desk", "draft-day context and prospect notes", MediaTone.Speculative, 70);

    public IReadOnlyList<MediaSource> DefaultSources { get; } =
    [
        LeagueWire,
        HockeyDaily,
        ProspectCentral,
        FrontOfficeReport,
        LocalBeat,
        DraftDesk
    ];

    public NewGmScenarioSnapshot EnsureMediaFeed(
        NewGmScenarioSnapshot scenario,
        IEnumerable<LeagueTransaction>? leagueTransactions = null,
        EngineRegistry? registry = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var prepared = new StoryService().EnsureStories(scenario, registry);
        var articles = GenerateArticles(prepared, leagueTransactions ?? Array.Empty<LeagueTransaction>());
        var merged = prepared.MediaFeed.Articles
            .Concat(articles)
            .GroupBy(article => article.ArticleId, StringComparer.Ordinal)
            .Select(group => group.Last())
            .OrderByDescending(article => article.Date)
            .ThenByDescending(article => article.Importance)
            .ThenBy(article => article.Headline, StringComparer.Ordinal)
            .Take(80)
            .ToArray();
        var feed = new MediaFeed(DefaultSources, merged);
        feed.Validate();
        return prepared with { MediaFeed = feed };
    }

    public MediaFeed BuildFeed(NewGmScenarioSnapshot scenario, IEnumerable<LeagueTransaction>? leagueTransactions = null, EngineRegistry? registry = null) =>
        EnsureMediaFeed(scenario, leagueTransactions, registry).MediaFeed;

    public IReadOnlyList<MediaArticle> GenerateArticles(NewGmScenarioSnapshot scenario, IEnumerable<LeagueTransaction> leagueTransactions)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(leagueTransactions);

        var articles = new List<MediaArticle>();
        articles.AddRange(FromStories(scenario));
        articles.AddRange(FromLeagueTransactions(scenario, leagueTransactions));
        articles.AddRange(FromDraftContext(scenario));
        articles.AddRange(FromMilestones(scenario));
        articles.AddRange(FromRumors(scenario));

        var output = articles
            .GroupBy(article => article.ArticleId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderByDescending(article => article.Date)
            .ThenByDescending(article => article.Importance)
            .ThenBy(article => article.Headline, StringComparer.Ordinal)
            .Take(60)
            .ToArray();
        foreach (var article in output)
        {
            article.Validate();
        }

        return output;
    }

    public MediaArticle? TopHeadline(NewGmScenarioSnapshot scenario, IEnumerable<LeagueTransaction>? leagueTransactions = null, EngineRegistry? registry = null) =>
        BuildFeed(scenario, leagueTransactions, registry)
            .Articles
            .Where(article => article.Importance >= MediaImportance.Major)
            .OrderByDescending(article => article.Importance)
            .ThenByDescending(article => article.Date)
            .FirstOrDefault();

    public IReadOnlyList<string> BuildPlayerDossierLines(NewGmScenarioSnapshot scenario, string personId)
    {
        var feed = BuildFeed(scenario);
        var related = feed.Query(personId: personId).Take(5).ToArray();
        if (related.Length == 0)
        {
            return new[] { "No related media articles yet." };
        }

        return related
            .Select(article => $"{article.Date:yyyy-MM-dd} | {article.Source.Name} | {article.Headline} - {article.ShortSummary}")
            .ToArray();
    }

    public IReadOnlyList<string> BuildOrganizationLines(NewGmScenarioSnapshot scenario, IEnumerable<LeagueTransaction>? leagueTransactions = null)
    {
        var feed = BuildFeed(scenario, leagueTransactions);
        var related = feed.Query(teamId: scenario.Organization.OrganizationId).Take(5).ToArray();
        if (related.Length == 0)
        {
            return new[] { "No related media coverage yet." };
        }

        return related.Select(article => $"{article.Date:yyyy-MM-dd} | {article.Source.Name} | {article.Headline}").ToArray();
    }

    private IEnumerable<MediaArticle> FromStories(NewGmScenarioSnapshot scenario)
    {
        foreach (var story in scenario.Stories
            .Where(story => story.Importance >= StoryImportance.Notable)
            .OrderByDescending(story => story.Importance)
            .ThenByDescending(story => story.CurrentArc.Progress)
            .Take(8))
        {
            var type = story.SubjectKind switch
            {
                "Player" => MediaArticleType.PlayerFeature,
                "Staff" => MediaArticleType.StaffFeature,
                "Owner" or "GM" => MediaArticleType.OwnerFrontOffice,
                "Organization" => MediaArticleType.TeamFeature,
                _ => MediaArticleType.Analysis
            };
            var source = type switch
            {
                MediaArticleType.PlayerFeature => story.StoryType is StoryType.TopProspect or StoryType.DraftSteal ? ProspectCentral : LocalBeat,
                MediaArticleType.StaffFeature or MediaArticleType.OwnerFrontOffice => FrontOfficeReport,
                MediaArticleType.TeamFeature => HockeyDaily,
                _ => HockeyDaily
            };
            yield return Article(
                $"media:story:{story.StoryId}",
                story.LastUpdated,
                scenario,
                source,
                type,
                ToneForStory(story),
                story.Importance >= StoryImportance.Defining ? MediaImportance.Major : MediaImportance.Notable,
                story.Summary.Headline,
                story.Summary.ShortSummary,
                story.Summary.LongSummary,
                story.OrganizationId is null ? Array.Empty<string>() : new[] { story.OrganizationId },
                story.OrganizationName is null ? Array.Empty<string>() : new[] { story.OrganizationName },
                story.SubjectKind is "Player" or "Staff" or "GM" or "Owner" ? new[] { story.SubjectId } : Array.Empty<string>(),
                story.SubjectKind is "Player" or "Staff" or "GM" or "Owner" ? new[] { story.SubjectName } : Array.Empty<string>(),
                story.StoryId,
                null);
        }
    }

    private IEnumerable<MediaArticle> FromLeagueTransactions(NewGmScenarioSnapshot scenario, IEnumerable<LeagueTransaction> transactions)
    {
        foreach (var transaction in transactions
            .Where(IsMediaWorthy)
            .OrderByDescending(transaction => transaction.Date)
            .Take(16))
        {
            var type = TypeForTransaction(transaction.TransactionType);
            var source = SourceForArticleType(type);
            yield return Article(
                $"media:wire:{transaction.TransactionId}",
                DateOnly.FromDateTime(transaction.Date.DateTime),
                scenario,
                source,
                type,
                ToneForTransaction(transaction.TransactionType),
                ImportanceForTransaction(transaction.TransactionType),
                HeadlineForTransaction(transaction),
                transaction.Description,
                $"{transaction.Description} This short item is generated from the league transaction wire and is separate from the raw transaction feed.",
                transaction.OrganizationId is null ? Array.Empty<string>() : new[] { transaction.OrganizationId },
                new[] { transaction.TeamName },
                transaction.PersonId is null ? Array.Empty<string>() : new[] { transaction.PersonId },
                new[] { transaction.PersonName },
                null,
                transaction.TransactionId);
        }
    }

    private IEnumerable<MediaArticle> FromDraftContext(NewGmScenarioSnapshot scenario)
    {
        foreach (var pick in scenario.DraftPickHistory.Take(4))
        {
            yield return Article(
                $"media:draft-history:{pick.Year}:{pick.Round}:{pick.OverallPick}:{pick.PlayerPersonId}",
                new DateOnly(pick.Year, scenario.DraftDate.Month, scenario.DraftDate.Day),
                scenario,
                DraftDesk,
                MediaArticleType.Draft,
                MediaTone.Positive,
                pick.Round <= 1 ? MediaImportance.Major : MediaImportance.Notable,
                $"{scenario.Organization.Name} adds {pick.PlayerName} to the prospect story",
                $"{pick.PlayerName} was selected in round {pick.Round}, overall pick {pick.OverallPick}.",
                $"{pick.PlayerName}'s draft context is tracked as a prospect article. The item summarizes draft history and scouting confidence without exposing hidden ratings.",
                new[] { scenario.Organization.OrganizationId },
                new[] { scenario.Organization.Name },
                new[] { pick.PlayerPersonId },
                new[] { pick.PlayerName });
        }

        foreach (var prospect in scenario.ProspectRights.Take(3))
        {
            yield return Article(
                $"media:prospect:{prospect.ProspectPersonId}:{prospect.RoundNumber}:{prospect.PickNumber}",
                scenario.CurrentDate,
                scenario,
                ProspectCentral,
                MediaArticleType.ProspectWatch,
                MediaTone.Speculative,
                prospect.RoundNumber <= 2 ? MediaImportance.Notable : MediaImportance.Routine,
                $"Prospect watch: {prospect.ProspectName}",
                $"{prospect.ProspectName} remains part of {scenario.Organization.Name}'s rights list.",
                $"{prospect.ProspectName} is tracked as {prospect.Status} with scouting confidence {prospect.ScoutingConfidence?.ToString() ?? "Unknown"}.",
                new[] { scenario.Organization.OrganizationId },
                new[] { scenario.Organization.Name },
                new[] { prospect.ProspectPersonId },
                new[] { prospect.ProspectName });
        }
    }

    private IEnumerable<MediaArticle> FromMilestones(NewGmScenarioSnapshot scenario)
    {
        foreach (var milestone in scenario.PlayerMilestones.Where(milestone => milestone.IsNotable).Take(4))
        {
            yield return Article(
                $"media:milestone:{milestone.MilestoneId}",
                milestone.Date,
                scenario,
                HockeyDaily,
                MediaArticleType.Milestone,
                MediaTone.Celebratory,
                MediaImportance.Notable,
                $"{milestone.PlayerName} reaches a new milestone",
                milestone.Summary,
                $"{milestone.Summary} The milestone is linked to the player's long-term career story.",
                new[] { scenario.Organization.OrganizationId },
                new[] { scenario.Organization.Name },
                new[] { milestone.PersonId },
                new[] { milestone.PlayerName });
        }

        foreach (var milestone in scenario.StaffMilestones.Where(milestone => milestone.IsNotable).Take(2))
        {
            yield return Article(
                $"media:staff-milestone:{milestone.MilestoneId}",
                milestone.Date,
                scenario,
                FrontOfficeReport,
                MediaArticleType.StaffFeature,
                MediaTone.Positive,
                MediaImportance.Notable,
                $"{milestone.StaffName} draws front-office attention",
                milestone.Summary,
                $"{milestone.Summary} Staff movement and reputation remain part of the club's wider story.",
                new[] { scenario.Organization.OrganizationId },
                new[] { scenario.Organization.Name },
                new[] { milestone.PersonId },
                new[] { milestone.StaffName });
        }

        foreach (var milestone in scenario.OwnerMilestones.Where(milestone => milestone.IsNotable).Take(2))
        {
            yield return Article(
                $"media:owner-milestone:{milestone.MilestoneId}",
                milestone.Date,
                scenario,
                FrontOfficeReport,
                MediaArticleType.OwnerFrontOffice,
                milestone.MilestoneType == OwnerMilestoneType.JobSecurityChanged ? MediaTone.Concerned : MediaTone.Neutral,
                MediaImportance.Notable,
                $"{milestone.OwnerName}'s front-office stance comes into focus",
                milestone.Summary,
                $"{milestone.Summary} The owner angle is tracked as front-office media coverage, not a press-conference system.",
                new[] { scenario.Organization.OrganizationId },
                new[] { scenario.Organization.Name },
                new[] { milestone.OwnerId },
                new[] { milestone.OwnerName });
        }
    }

    private IEnumerable<MediaArticle> FromRumors(NewGmScenarioSnapshot scenario)
    {
        foreach (var rumor in scenario.TradeDeadlineState?.Rumors.Take(4) ?? Array.Empty<DeadlineRumor>())
        {
            yield return Article(
                $"media:rumor:{rumor.RumorId}",
                rumor.Date,
                scenario,
                HockeyDaily,
                MediaArticleType.Rumor,
                MediaTone.Speculative,
                rumor.Confidence == DeadlineRumorConfidence.High ? MediaImportance.Notable : MediaImportance.Routine,
                $"Rumor watch: {rumor.TeamName}",
                rumor.Summary,
                $"{rumor.Summary} Rumor confidence is {rumor.Confidence}; this item may never become a transaction.",
                Array.Empty<string>(),
                new[] { rumor.TeamName },
                Array.Empty<string>(),
                Array.Empty<string>(),
                null,
                null,
                ToMediaConfidence(rumor.Confidence));
        }

        if (scenario.TradeDeadlineState?.Rumors.Count > 0)
        {
            yield break;
        }

        var need = scenario.OrganizationAiProfiles
            .FirstOrDefault(profile => profile.OrganizationId == scenario.Organization.OrganizationId)
            ?.CurrentNeeds.FirstOrDefault();
        if (need is not null)
        {
            yield return Article(
                $"media:rumor:team-need:{scenario.Organization.OrganizationId}:{scenario.CurrentDate:yyyyMMdd}",
                scenario.CurrentDate,
                scenario,
                HockeyDaily,
                MediaArticleType.Rumor,
                MediaTone.Speculative,
                MediaImportance.Routine,
                $"{scenario.Organization.Name} linked to search for {Readable(need.NeedType)}",
                $"{scenario.Organization.Name}'s roster need has created light league speculation.",
                $"A league source believes {scenario.Organization.Name} could explore {Readable(need.NeedType).ToLowerInvariant()} options. Confidence is Low; no transaction is implied.",
                new[] { scenario.Organization.OrganizationId },
                new[] { scenario.Organization.Name },
                Array.Empty<string>(),
                Array.Empty<string>(),
                null,
                null,
                MediaRumorConfidence.Low);
        }
    }

    private MediaArticle Article(
        string id,
        DateOnly date,
        NewGmScenarioSnapshot scenario,
        MediaSource source,
        MediaArticleType type,
        MediaTone tone,
        MediaImportance importance,
        string headline,
        string summary,
        string? body,
        IReadOnlyList<string> teamIds,
        IReadOnlyList<string> teamNames,
        IReadOnlyList<string> personIds,
        IReadOnlyList<string> personNames,
        string? storyId = null,
        string? transactionId = null,
        MediaRumorConfidence rumorConfidence = MediaRumorConfidence.None)
    {
        return new MediaArticle(
            id,
            new DateTimeOffset(date.Year, date.Month, date.Day, 12, 0, 0, TimeSpan.Zero),
            scenario.LeagueProfile.Identity.LeagueId,
            scenario.LeagueProfile.Identity.Name,
            source,
            type,
            tone,
            importance,
            headline.Trim(),
            summary.Trim(),
            string.IsNullOrWhiteSpace(body) ? null : body.Trim(),
            teamIds.Distinct(StringComparer.Ordinal).ToArray(),
            teamNames.Distinct(StringComparer.Ordinal).ToArray(),
            personIds.Distinct(StringComparer.Ordinal).ToArray(),
            personNames.Distinct(StringComparer.Ordinal).ToArray(),
            storyId,
            transactionId,
            rumorConfidence);
    }

    private static bool IsMediaWorthy(LeagueTransaction transaction) =>
        transaction.TransactionType is LeagueTransactionType.TradeCompleted
            or LeagueTransactionType.PlayerSigned
            or LeagueTransactionType.ContractSigned
            or LeagueTransactionType.DraftPick
            or LeagueTransactionType.Injury
            or LeagueTransactionType.StaffHired
            or LeagueTransactionType.StaffReleased
            or LeagueTransactionType.TradeDeadline
            or LeagueTransactionType.SeasonCompleted
            or LeagueTransactionType.TeamIdentityUpdate
            or LeagueTransactionType.PlayerMilestone
            or LeagueTransactionType.StaffMilestone
            or LeagueTransactionType.OwnerMilestone
            or LeagueTransactionType.StoryUpdate;

    private static MediaArticleType TypeForTransaction(LeagueTransactionType type) =>
        type switch
        {
            LeagueTransactionType.TradeCompleted => MediaArticleType.Trade,
            LeagueTransactionType.PlayerSigned or LeagueTransactionType.ContractSigned => MediaArticleType.FreeAgency,
            LeagueTransactionType.DraftPick => MediaArticleType.Draft,
            LeagueTransactionType.Injury => MediaArticleType.Injury,
            LeagueTransactionType.StaffHired or LeagueTransactionType.StaffReleased or LeagueTransactionType.OwnerMilestone => MediaArticleType.OwnerFrontOffice,
            LeagueTransactionType.TeamIdentityUpdate or LeagueTransactionType.StoryUpdate => MediaArticleType.Analysis,
            LeagueTransactionType.SeasonCompleted => MediaArticleType.Playoff,
            LeagueTransactionType.PlayerMilestone or LeagueTransactionType.StaffMilestone => MediaArticleType.Milestone,
            _ => MediaArticleType.Transaction
        };

    private static MediaSource SourceForArticleType(MediaArticleType type) =>
        type switch
        {
            MediaArticleType.Draft or MediaArticleType.ProspectWatch => DraftDesk,
            MediaArticleType.Trade or MediaArticleType.Transaction => LeagueWire,
            MediaArticleType.FreeAgency or MediaArticleType.OwnerFrontOffice or MediaArticleType.StaffFeature => FrontOfficeReport,
            MediaArticleType.PlayerFeature => LocalBeat,
            _ => HockeyDaily
        };

    private static MediaTone ToneForStory(Story story) =>
        story.Status == StoryStatus.AtRisk
            ? MediaTone.Concerned
            : story.StoryType is StoryType.VeteranDecline or StoryType.FirstOverallBust or StoryType.LosingConfidence or StoryType.GoaltendingCrisis
                ? MediaTone.Critical
                : story.Importance >= StoryImportance.Major ? MediaTone.Positive : MediaTone.Neutral;

    private static MediaTone ToneForTransaction(LeagueTransactionType type) =>
        type switch
        {
            LeagueTransactionType.Injury => MediaTone.Concerned,
            LeagueTransactionType.TradeDeadline => MediaTone.Speculative,
            LeagueTransactionType.SeasonCompleted or LeagueTransactionType.PlayerMilestone or LeagueTransactionType.StaffMilestone => MediaTone.Celebratory,
            LeagueTransactionType.StaffReleased => MediaTone.Critical,
            _ => MediaTone.Neutral
        };

    private static MediaImportance ImportanceForTransaction(LeagueTransactionType type) =>
        type switch
        {
            LeagueTransactionType.TradeCompleted or LeagueTransactionType.SeasonCompleted => MediaImportance.Major,
            LeagueTransactionType.PlayerSigned or LeagueTransactionType.ContractSigned or LeagueTransactionType.DraftPick or LeagueTransactionType.Injury => MediaImportance.Notable,
            LeagueTransactionType.PlayerMilestone or LeagueTransactionType.StaffMilestone or LeagueTransactionType.OwnerMilestone or LeagueTransactionType.StoryUpdate => MediaImportance.Notable,
            _ => MediaImportance.Routine
        };

    private static string HeadlineForTransaction(LeagueTransaction transaction) =>
        transaction.TransactionType switch
        {
            LeagueTransactionType.TradeCompleted => $"{transaction.TeamName} completes move involving {transaction.PersonName}",
            LeagueTransactionType.PlayerSigned or LeagueTransactionType.ContractSigned => $"{transaction.PersonName} signing draws attention around {transaction.TeamName}",
            LeagueTransactionType.DraftPick => $"{transaction.TeamName} adds {transaction.PersonName} through draft",
            LeagueTransactionType.Injury => $"{transaction.TeamName} faces injury concern with {transaction.PersonName}",
            LeagueTransactionType.StaffHired => $"{transaction.TeamName} adds {transaction.PersonName} to staff",
            LeagueTransactionType.StaffReleased => $"{transaction.TeamName} parts with {transaction.PersonName}",
            LeagueTransactionType.StoryUpdate => transaction.Description,
            _ => $"{transaction.TeamName}: {transaction.TransactionType} involving {transaction.PersonName}"
        };

    private static MediaRumorConfidence ToMediaConfidence(DeadlineRumorConfidence confidence) =>
        confidence switch
        {
            DeadlineRumorConfidence.High => MediaRumorConfidence.High,
            DeadlineRumorConfidence.Medium => MediaRumorConfidence.Medium,
            _ => MediaRumorConfidence.Low
        };

    private static string Readable(Enum value)
    {
        var text = value.ToString();
        return string.Concat(text.Select((letter, index) => index > 0 && char.IsUpper(letter) ? $" {letter}" : letter.ToString()));
    }
}
