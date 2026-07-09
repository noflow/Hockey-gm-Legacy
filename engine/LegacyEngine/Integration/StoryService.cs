using LegacyEngine.Rosters;
using LegacyEngine.Staff;

namespace LegacyEngine.Integration;

public sealed class StoryService
{
    public NewGmScenarioSnapshot EnsureStories(NewGmScenarioSnapshot scenario, EngineRegistry? registry = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var prepared = scenario.FranchiseIdentities.Count == 0
            ? new FranchiseIdentityService().EnsureIdentities(scenario)
            : scenario;

        var knownPersonIds = prepared.AlphaSnapshot.People.Select(person => person.PersonId).ToHashSet(StringComparer.Ordinal);
        var playerIds = prepared.AlphaSnapshot.Roster.Players.Take(5).Select(player => player.PersonId)
            .Concat(prepared.ProspectRights.Take(4).Select(prospect => prospect.ProspectPersonId))
            .Where(knownPersonIds.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var expectedMinimum = playerIds.Length + 4;
        if (prepared.Stories.Count >= expectedMinimum
            && playerIds.All(id => prepared.Stories.Any(story => story.SubjectId == id)))
        {
            return prepared;
        }

        var stories = new List<Story>();
        stories.AddRange(playerIds.Select(id => BuildPlayerStory(prepared, id)));
        stories.Add(BuildGmStory(prepared));
        stories.Add(BuildOrganizationStory(prepared));
        if (prepared.StaffMembers.FirstOrDefault(staff => IsScout(staff.CurrentRole)) is { } scout)
        {
            stories.Add(BuildStaffStory(prepared, scout.PersonId));
        }

        stories.Add(BuildOwnerStory(prepared));
        var merged = prepared.Stories
            .Concat(stories)
            .GroupBy(story => story.StoryId, StringComparer.Ordinal)
            .Select(group => group.Last())
            .ToArray();
        foreach (var story in merged)
        {
            story.Validate();
        }

        return prepared with { Stories = merged };
    }

    public Story ProgressStory(Story story, StoryEvent storyEvent, string arcName, StoryStatus status, int progressChange, string summary)
    {
        ArgumentNullException.ThrowIfNull(story);
        ArgumentNullException.ThrowIfNull(storyEvent);
        story.Validate();
        storyEvent.Validate();

        var current = story.CurrentArc;
        var nextArc = current with
        {
            Name = arcName,
            Status = status,
            Progress = Math.Clamp(current.Progress + progressChange, 0, 100),
            Events = current.Events.Append(storyEvent).ToArray(),
            Summary = summary
        };
        var next = story with
        {
            Status = status,
            LastUpdated = storyEvent.Date,
            Arcs = story.Arcs.Where(arc => arc.StoryArcId != current.StoryArcId).Append(nextArc).ToArray(),
            Summary = story.Summary with
            {
                ShortSummary = summary,
                LongSummary = $"{story.Summary.LongSummary} {storyEvent.Description}",
                KeyMoments = story.Summary.KeyMoments.Append($"{storyEvent.Date:yyyy-MM-dd}: {storyEvent.Title}").TakeLast(8).ToArray()
            }
        };
        next.Validate();
        return next;
    }

    public IReadOnlyList<string> BuildPlayerDossierLines(NewGmScenarioSnapshot scenario, string personId)
    {
        var withStories = EnsureStories(scenario);
        var stories = withStories.Stories
            .Where(story => string.Equals(story.SubjectId, personId, StringComparison.Ordinal))
            .OrderByDescending(story => story.Importance)
            .ThenByDescending(story => story.LastUpdated)
            .ToArray();
        if (stories.Length == 0)
        {
            return new[] { "No major player story is active yet." };
        }

        var story = stories.First();
        return new[]
        {
            $"Current Story: {Readable(story.StoryType)}",
            $"Status: {story.Status}; importance {story.Importance}",
            $"Story Progress: {story.CurrentArc.Progress}/100 - {story.CurrentArc.Name}",
            $"Career Summary: {story.Summary.LongSummary}"
        }
        .Concat(story.Summary.KeyMoments.TakeLast(5).Select(moment => $"Major Moment: {moment}"))
        .ToArray();
    }

    public IReadOnlyList<string> BuildOrganizationLines(NewGmScenarioSnapshot scenario)
    {
        var withStories = EnsureStories(scenario);
        var story = withStories.Stories
            .Where(story => story.SubjectKind == "Organization" && story.SubjectId == withStories.Organization.OrganizationId)
            .OrderByDescending(story => story.Importance)
            .FirstOrDefault();
        if (story is null)
        {
            return new[] { "No major organization story is active yet." };
        }

        return new[]
        {
            $"Current Story: {Readable(story.StoryType)}",
            $"Status: {story.Status}; progress {story.CurrentArc.Progress}/100",
            story.Summary.ShortSummary,
            $"Major moments: {string.Join("; ", story.Summary.KeyMoments.TakeLast(4))}"
        };
    }

    public IReadOnlyDictionary<string, string> BuildExecutiveReportItems(NewGmScenarioSnapshot scenario)
    {
        var withStories = EnsureStories(scenario);
        var top = withStories.Stories.OrderByDescending(story => story.Importance).ThenByDescending(story => story.CurrentArc.Progress).Take(5).ToArray();
        var items = new Dictionary<string, string>
        {
            ["Top Story"] = top.FirstOrDefault()?.Summary.Headline ?? "No story yet.",
            ["Organization Story"] = withStories.Stories.FirstOrDefault(story => story.SubjectKind == "Organization")?.Summary.ShortSummary ?? "No organization story yet.",
            ["GM Story"] = withStories.Stories.FirstOrDefault(story => story.SubjectKind == "GM")?.Summary.ShortSummary ?? "No GM story yet.",
            ["Player Story Count"] = withStories.Stories.Count(story => story.SubjectKind == "Player").ToString()
        };

        for (var index = 0; index < top.Length; index++)
        {
            items[$"Story {index + 1}"] = top[index].Summary.ShortSummary;
        }

        return items;
    }

    public IReadOnlyList<ActionCenterItem> BuildActionCenterItems(NewGmScenarioSnapshot scenario)
    {
        var withStories = EnsureStories(scenario);
        return withStories.Stories
            .Where(story => story.Importance >= StoryImportance.Major || story.Status == StoryStatus.AtRisk)
            .OrderByDescending(story => story.Importance)
            .Take(3)
            .Select(story => new ActionCenterItem(
                $"action-center:story:{story.StoryId}",
                $"{Readable(story.StoryType)} story updated",
                ActionCenterCategory.Storyline,
                story.Importance >= StoryImportance.Defining ? ActionCenterPriority.Important : ActionCenterPriority.Normal,
                story.LastUpdated.AddDays(7),
                story.SubjectKind == "Player" || story.SubjectKind == "Staff" || story.SubjectKind == "GM" ? story.SubjectId : null,
                story.SubjectKind == "Player" || story.SubjectKind == "Staff" || story.SubjectKind == "GM" ? story.SubjectName : null,
                story.OrganizationId,
                story.OrganizationName,
                story.Summary.ShortSummary,
                "Important stories can affect owner expectations, player morale, staff focus, and league perception.",
                "Review the related dossier, report, or command-center card before the story drifts.",
                null,
                null,
                null))
            .ToArray();
    }

    public IReadOnlyList<LeagueTransaction> BuildLeagueNews(NewGmScenarioSnapshot scenario, int maxItems = 3)
    {
        var withStories = EnsureStories(scenario);
        return withStories.Stories
            .Where(story => story.Importance >= StoryImportance.Notable)
            .OrderByDescending(story => story.Importance)
            .ThenByDescending(story => story.CurrentArc.Progress)
            .ThenBy(story => story.SubjectName, StringComparer.Ordinal)
            .Take(maxItems)
            .Select(story => new LeagueTransaction(
                $"story-news:{story.StoryId}:{scenario.CurrentDate:yyyyMM}",
                new DateTimeOffset(scenario.CurrentDate.Year, scenario.CurrentDate.Month, scenario.CurrentDate.Day, 13, 0, 0, TimeSpan.Zero),
                story.OrganizationId,
                story.OrganizationName ?? scenario.Organization.Name,
                story.SubjectKind is "Player" or "Staff" ? story.SubjectId : null,
                story.SubjectName,
                LeagueTransactionType.StoryUpdate,
                LeagueNewsCategory.League,
                story.Summary.Headline))
            .ToArray();
    }

    public Story? FindStoryForSubject(NewGmScenarioSnapshot scenario, string subjectId) =>
        EnsureStories(scenario).Stories
            .Where(story => story.SubjectId == subjectId)
            .OrderByDescending(story => story.Importance)
            .ThenByDescending(story => story.CurrentArc.Progress)
            .FirstOrDefault();

    private Story BuildPlayerStory(NewGmScenarioSnapshot scenario, string personId)
    {
        var person = scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)
            ?? throw new ArgumentException($"Player story person '{personId}' was not found.", nameof(personId));
        var summary = scenario.PlayerCareerSummaries.FirstOrDefault(summary => summary.PersonId == personId)
            ?? new PlayerLifeCycleService().FindSummary(new PlayerLifeCycleService().EnsureLifeCycle(scenario), personId);
        var prospect = scenario.ProspectRights.FirstOrDefault(prospect => prospect.ProspectPersonId == personId);
        var roster = scenario.AlphaSnapshot.Roster.Players.FirstOrDefault(player => player.PersonId == personId);
        var type = PlayerStoryType(summary, prospect, roster);
        var importance = summary?.LegacyScore >= 70 || prospect?.RoundNumber <= 2 ? StoryImportance.Major : StoryImportance.Notable;
        var headline = type switch
        {
            StoryType.TopProspect => $"{person.Identity.DisplayName} is becoming one of {scenario.Organization.Name}'s defining prospect stories.",
            StoryType.DraftSteal => $"{person.Identity.DisplayName} continues building a draft-steal story.",
            StoryType.InjuryComeback => $"{person.Identity.DisplayName}'s recovery has become a story to watch.",
            StoryType.VeteranDecline => $"{person.Identity.DisplayName} is trying to slow a veteran decline.",
            StoryType.LateBloomer => $"{person.Identity.DisplayName} is showing signs of a late-bloomer arc.",
            _ => $"{person.Identity.DisplayName} has an active {Readable(type).ToLowerInvariant()} story."
        };
        var moments = PlayerMoments(scenario, personId, summary, prospect).ToArray();
        return CreateStory(
            $"story:player:{personId}:{type}",
            type,
            StoryStatus.Active,
            importance,
            personId,
            person.Identity.DisplayName,
            "Player",
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            scenario.CurrentDate.AddDays(-30),
            "Player arc",
            Math.Clamp(45 + (summary?.LegacyScore ?? prospect?.PickNumber ?? 15) / 2, 25, 90),
            headline,
            $"{person.Identity.DisplayName} is currently framed as {Readable(type).ToLowerInvariant()}.",
            $"{person.Identity.DisplayName} is not just a roster entry; his recent milestones, development path, staff notes, and role context now connect into a single {Readable(type).ToLowerInvariant()} story.",
            moments);
    }

    private Story BuildGmStory(NewGmScenarioSnapshot scenario)
    {
        var gm = scenario.GeneralManagerProfile.Person;
        var type = scenario.TradeOffers.Count(offer => offer.Status == TradeOfferStatus.Accepted) >= 2
            ? StoryType.AggressiveTrader
            : scenario.ProspectRights.Count >= 4 ? StoryType.ProspectBuilder : StoryType.FirstSeason;
        return CreateStory(
            $"story:gm:{gm.PersonId}:{type}",
            type,
            StoryStatus.Active,
            StoryImportance.Major,
            gm.PersonId,
            gm.Identity.DisplayName,
            "GM",
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            scenario.CurrentDate.AddDays(-14),
            "GM tenure",
            55,
            $"{gm.Identity.DisplayName}'s first season is becoming a front-office identity story.",
            $"{gm.Identity.DisplayName} is shaping {scenario.Organization.Name}'s direction through draft, roster, staff, and budget decisions.",
            $"The new GM story connects the hiring, first draft class, owner expectations, pending decisions, and the club's franchise identity into one ongoing narrative.",
            new[] { $"Hired by {scenario.Organization.Name}.", $"Inherited {scenario.ProspectRights.Count} prospect-rights record(s).", $"Owner confidence: {scenario.AlphaSnapshot.Owner.Confidence}/100." });
    }

    private Story BuildOrganizationStory(NewGmScenarioSnapshot scenario)
    {
        var identity = scenario.FranchiseIdentities.FirstOrDefault(identity => identity.OrganizationId == scenario.Organization.OrganizationId);
        if (identity is null)
        {
            return CreateStory(
                $"story:organization:{scenario.Organization.OrganizationId}:{StoryType.YouthMovement}",
                StoryType.YouthMovement,
                StoryStatus.Active,
                StoryImportance.Defining,
                scenario.Organization.OrganizationId,
                scenario.Organization.Name,
                "Organization",
                scenario.Organization.OrganizationId,
                scenario.Organization.Name,
                scenario.CurrentDate.AddDays(-60),
                "Organization arc",
                50,
                $"{scenario.Organization.Name}'s organization story is beginning to take shape.",
                $"{scenario.Organization.Name} is establishing its current hockey direction.",
                $"{scenario.Organization.Name}'s roster state, staff setup, owner expectations, and prospect pipeline are now connected into an organization story even before a full franchise identity profile exists.",
                new[] { scenario.ScenarioSummary, $"League context: {scenario.LeagueProfile.Identity.Name}.", $"Team selection: {scenario.TeamSelection.TeamName}." });
        }

        var type = identity.FutureDirection switch
        {
            FranchiseDirection.Rebuild => StoryType.Rebuild,
            FranchiseDirection.Contend => StoryType.ChampionshipWindow,
            FranchiseDirection.DevelopCore => StoryType.YouthMovement,
            _ when identity.CurrentIdentity == FranchisePhilosophy.DefenseFirst => StoryType.DefenseFirst,
            _ => StoryType.YouthMovement
        };
        return CreateStory(
            $"story:organization:{scenario.Organization.OrganizationId}:{type}",
            type,
            StoryStatus.Active,
            StoryImportance.Defining,
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            "Organization",
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            identity.CurrentEra.StartYear < scenario.CurrentDate.Year ? new DateOnly(identity.CurrentEra.StartYear, 7, 1) : scenario.CurrentDate.AddDays(-60),
            "Organization arc",
            60,
            $"{scenario.Organization.Name}'s {Readable(type).ToLowerInvariant()} is now the club's central story.",
            $"{scenario.Organization.Name} is known for {Readable(identity.CurrentIdentity).ToLowerInvariant()} with a {Readable(identity.Culture).ToLowerInvariant()} culture.",
            $"{scenario.Organization.Name}'s current era, franchise identity, roster state, owner expectations, and prospect pipeline connect into a long-running {Readable(type).ToLowerInvariant()} story.",
            new[] { identity.CurrentEra.Name, identity.Summary, $"Team DNA: {string.Join(", ", identity.TeamDna.Take(3))}." });
    }

    private Story BuildStaffStory(NewGmScenarioSnapshot scenario, string personId)
    {
        var personName = scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
            ?? scenario.StaffCareerSummaries.FirstOrDefault(summary => summary.PersonId == personId)?.StaffName
            ?? (scenario.StaffMembers.FirstOrDefault(staff => staff.PersonId == personId) is { } staffMember
                ? StaffRoles.Title(staffMember.CurrentRole)
                : "Staff member");
        var summary = scenario.StaffCareerSummaries.FirstOrDefault(summary => summary.PersonId == personId);
        var type = summary?.PlayersDiscovered.Count > 2 ? StoryType.LegendaryScout : StoryType.PlayerWhisperer;
        return CreateStory(
            $"story:staff:{personId}:{type}",
            type,
            StoryStatus.Active,
            StoryImportance.Notable,
            personId,
            personName,
            "Staff",
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            scenario.CurrentDate.AddDays(-45),
            "Staff arc",
            52,
            $"{personName}'s staff story is gaining shape inside {scenario.Organization.Name}.",
            $"{personName} is connected to the club's scouting and development story.",
            $"{personName}'s role, staff history, discoveries, relationships, and department fit now form a staff story rather than isolated notes.",
            new[] { summary?.PersonalLegacy ?? "Staff career baseline established.", summary?.PromotionReadiness ?? "Promotion readiness under review." });
    }

    private Story BuildOwnerStory(NewGmScenarioSnapshot scenario)
    {
        var owner = scenario.AlphaSnapshot.Owner;
        var type = scenario.OwnerCareerState?.ConfidenceTrend == OwnerTrend.Falling
            ? StoryType.LosingConfidence
            : owner.Patience >= 60 ? StoryType.PatientBuilder : StoryType.ChampionshipDemand;
        return CreateStory(
            $"story:owner:{owner.OwnerId}:{type}",
            type,
            StoryStatus.Active,
            StoryImportance.Major,
            owner.OwnerId,
            owner.Name,
            "Owner",
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            scenario.CurrentDate.AddDays(-90),
            "Ownership arc",
            Math.Clamp((owner.Trust + owner.Confidence + owner.Patience) / 3, 20, 90),
            $"{owner.Name}'s ownership story is shaping the pressure around {scenario.Organization.Name}.",
            $"{owner.Name} is currently a {Readable(type).ToLowerInvariant()} owner story.",
            $"{owner.Name}'s expectations, budget support, patience, confidence, and relationship with the GM now connect into one ownership story.",
            new[] { $"Trust {owner.Trust}/100.", $"Confidence {owner.Confidence}/100.", scenario.OwnerCareerSummary?.CareerSummaryText ?? "Owner life-cycle baseline established." });
    }

    private static Story CreateStory(
        string storyId,
        StoryType type,
        StoryStatus status,
        StoryImportance importance,
        string subjectId,
        string subjectName,
        string subjectKind,
        string? organizationId,
        string? organizationName,
        DateOnly startedOn,
        string arcName,
        int progress,
        string headline,
        string shortSummary,
        string longSummary,
        IReadOnlyList<string> keyMoments)
    {
        var events = keyMoments.Select((moment, index) => new StoryEvent(
            $"{storyId}:event:{index + 1}",
            startedOn.AddDays(index * 14),
            moment.Length > 72 ? moment[..72] : moment,
            moment,
            null,
            subjectKind is "Player" or "Staff" or "GM" ? subjectId : null,
            organizationId,
            index == keyMoments.Count - 1 ? importance : StoryImportance.Normal)).ToArray();
        var story = new Story(
            storyId,
            type,
            status,
            importance,
            subjectId,
            subjectName,
            subjectKind,
            organizationId,
            organizationName,
            startedOn,
            events.Last().Date,
            new[] { new StoryArc($"{storyId}:arc:1", arcName, status, progress, events, shortSummary) },
            new StorySummary(headline, shortSummary, longSummary, keyMoments));
        story.Validate();
        return story;
    }

    private static StoryType PlayerStoryType(PlayerCareerSummary? summary, DraftRightsRecord? prospect, Rosters.RosterPlayer? roster)
    {
        if (summary?.CareerPhase == PlayerCareerPhase.LateBloomer)
        {
            return StoryType.LateBloomer;
        }

        if (summary?.CareerPhase == PlayerCareerPhase.CareerRevival)
        {
            return StoryType.CareerRevival;
        }

        if (summary?.CareerPhase == PlayerCareerPhase.CareerDecline)
        {
            return StoryType.VeteranDecline;
        }

        if (summary?.Achievements.Any(achievement => achievement.AchievementType == PlayerAchievementType.ComebackSeason) == true)
        {
            return StoryType.InjuryComeback;
        }

        if (prospect is not null && prospect.RoundNumber <= 1)
        {
            return StoryType.TopProspect;
        }

        if (prospect is not null && prospect.RoundNumber >= 3)
        {
            return StoryType.DraftSteal;
        }

        if (roster?.IsOverage() == true)
        {
            return StoryType.CaptainJourney;
        }

        return summary?.CareerPhase == PlayerCareerPhase.Breakout ? StoryType.YoungStarRising : StoryType.CareerYear;
    }

    private static IEnumerable<string> PlayerMoments(NewGmScenarioSnapshot scenario, string personId, PlayerCareerSummary? summary, DraftRightsRecord? prospect)
    {
        if (prospect is not null)
        {
            yield return $"Drafted round {prospect.RoundNumber}, pick {prospect.PickNumber} by {scenario.Organization.Name}.";
        }

        foreach (var milestone in summary?.Milestones.Take(3) ?? Array.Empty<PlayerMilestone>())
        {
            yield return $"{milestone.Date:yyyy-MM-dd}: {milestone.Summary}";
        }

        foreach (var entry in scenario.CareerTimeline.ForPerson(personId).Take(3))
        {
            yield return $"{entry.Date:yyyy-MM-dd}: {entry.Title}.";
        }

        yield return summary?.CareerSummaryText ?? "Career story baseline established.";
    }

    private static bool IsScout(StaffRole role) =>
        role is StaffRole.DirectorOfScouting or StaffRole.HeadScout or StaffRole.Scout or StaffRole.RegionalScout or StaffRole.AmateurScout or StaffRole.ProfessionalScout or StaffRole.EuropeanScout or StaffRole.GoaltendingScout;

    private static string Readable(Enum value)
    {
        var text = value.ToString();
        return string.Concat(text.Select((letter, index) => index > 0 && char.IsUpper(letter) ? $" {letter}" : letter.ToString()));
    }

}
