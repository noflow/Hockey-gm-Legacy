using LegacyEngine.Events;

namespace LegacyEngine.Integration;

public sealed class PlayabilityPolishService
{
    public IReadOnlyList<AlphaInboxItem> FilterInboxItems(IEnumerable<AlphaInboxItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return items
            .Where(ShouldShowInInbox)
            .GroupBy(item => StableInboxKey(item), StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(item => item.Date).First())
            .OrderByDescending(item => item.Severity)
            .ThenByDescending(item => item.Date)
            .ToArray();
    }

    public IReadOnlyList<JournalEntry> BuildJournalEntries(IEnumerable<AlphaInboxItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return items
            .Where(item => !ShouldShowInInbox(item))
            .Select(ToJournalEntry)
            .ToArray();
    }

    public IReadOnlyList<JournalEntry> MergeJournalEntries(IEnumerable<JournalEntry> existing, IEnumerable<JournalEntry> additions) =>
        existing.Concat(additions)
            .GroupBy(entry => entry.JournalEntryId, StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(entry => entry.Date).First())
            .OrderByDescending(entry => entry.Date)
            .ThenBy(entry => entry.Title, StringComparer.Ordinal)
            .ToArray();

    public IReadOnlyList<ActionCenterItem> CleanActionCenterItems(IEnumerable<ActionCenterItem> items) =>
        items
            .Where(item => item.Status == ActionCenterStatus.Open)
            .GroupBy(item => $"{item.Category}:{item.RelatedPersonId}:{item.Title}", StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(item => item.Priority).ThenBy(item => item.DueDate ?? DateOnly.MaxValue).First())
            .OrderBy(item => item.Status)
            .ThenByDescending(item => item.Priority)
            .ThenBy(item => item.DueDate ?? DateOnly.MaxValue)
            .ThenBy(item => item.Title, StringComparer.Ordinal)
            .ToArray();

    public IReadOnlyList<GlobalSearchResult> Search(
        NewGmScenarioSnapshot scenario,
        IReadOnlyList<InboxMessage> inbox,
        IReadOnlyList<LeagueTransaction> leagueNews,
        IReadOnlyList<JournalEntry> journal,
        string query)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var text = query.Trim();
        if (text.Length < 2)
        {
            return Array.Empty<GlobalSearchResult>();
        }

        var results = new List<GlobalSearchResult>();
        AddPeopleResults(scenario, text, results);
        AddStaffResults(scenario, text, results);
        AddDraftResults(scenario, text, results);
        AddFreeAgentResults(scenario, text, results);
        AddHistoryResults(scenario, text, results);
        AddOrganizationResults(scenario, text, results);
        AddMessageResults(inbox, leagueNews, journal, text, results);

        var output = results
            .GroupBy(result => result.SearchResultId, StringComparer.Ordinal)
            .Select(group => group.First())
            .Take(40)
            .ToArray();
        foreach (var result in output)
        {
            result.Validate();
        }

        return output;
    }

    public IReadOnlyList<PlaytestChecklistItem> BuildPlaytestChecklist(NewGmScenarioSnapshot scenario, IReadOnlyList<ActionCenterItem> actions)
    {
        var items = new[]
        {
            Checklist("new-gm", "New GM", "Can a new player understand their first day?", "Dashboard shows date, owner mood, agenda, inbox, and next recommended action.", actions.Any(item => item.Category == ActionCenterCategory.Owner) || scenario.FirstDayInbox.Count > 0, "Owner/context messaging is available."),
            Checklist("draft", "Draft", "Can the player reach draft decisions without hunting?", "Draft board rows show bio/scouting info and live draft modal remains available on draft day.", scenario.AlphaSnapshot.DraftBoard.Entries.Count > 0, "Draft board has prospects."),
            Checklist("training-camp", "Training Camp", "Can the player see roster deadline pressure?", "Training Camp and Season Readiness surfaces expose roster compliance and decisions.", scenario.SeasonReadiness.ReviewsGenerated || scenario.TrainingCamp is not null, "Camp/readiness data is present or queued."),
            Checklist("trades", "Trades", "Can a trade be evaluated clearly?", "Trades screen shows target, needs, value, reactions, and pending approval workflow.", scenario.TradeBlock is not null, "Trade block is available."),
            Checklist("free-agency", "Free Agency", "Can the GM compare players and budget impact?", "Free Agents rows include ask, motivation, competition, and decision status.", scenario.FreeAgentMarket is not null, "Free agent market is present."),
            Checklist("scouting", "Scouting", "Can assignments and reports be found?", "Scouting operations shows active/completed reports and the inbox only keeps completed report messages.", scenario.ScoutingOperations.Count > 0 || scenario.CompletedScoutingReports.Count > 0, "Scouting data exists."),
            Checklist("roster", "Roster", "Can the GM understand roster state quickly?", "Roster breakdown and filters show size, position, age, role, status, and warnings.", scenario.AlphaSnapshot.Roster.Players.Count > 0, "Roster exists."),
            Checklist("save-load", "Save/Load", "Can the player preserve a career?", "Header exposes Save Career, Save As, and Load Career actions.", true, "Career persistence remains in the desktop shell."),
            Checklist("inbox", "Inbox", "Is the inbox decision-focused?", "Routine updates route to Journal while important owner/medical/contract/scout/development items remain.", true, "Playability polish routing is active.")
        };
        foreach (var item in items)
        {
            item.Validate();
        }

        return items;
    }

    public static bool ShouldShowInInbox(AlphaInboxItem item)
    {
        if (item.Severity is LegacyEventSeverity.Warning or LegacyEventSeverity.Critical)
        {
            return true;
        }

        if (IsRoutineText(item.Title) || IsRoutineText(item.Summary))
        {
            return false;
        }

        return item.EventType switch
        {
            LegacyEventType.OwnerGoalSet or LegacyEventType.OwnerOffseasonReview or LegacyEventType.BudgetApproved => true,
            LegacyEventType.CoachRosterReview => true,
            LegacyEventType.ScoutAssignmentCompleted or LegacyEventType.ScoutingReportCreated or LegacyEventType.ScoutingReportUpdated => true,
            LegacyEventType.ContractOfferAccepted or LegacyEventType.ContractOfferNeedsRevision or LegacyEventType.ContractRejected or LegacyEventType.ContractExpired or LegacyEventType.ExpiringContractReminder or LegacyEventType.PendingGmActionCreated => true,
            LegacyEventType.TradeAccepted or LegacyEventType.TradeCountered or LegacyEventType.TradeRejected or LegacyEventType.TradeFailedValidation => true,
            LegacyEventType.PlayerInjured or LegacyEventType.InjuryReAggravated or LegacyEventType.InjuryCareerThreatening => true,
            LegacyEventType.PlayerBreakout or LegacyEventType.PlayerRegression => true,
            LegacyEventType.RecruitCommitted or LegacyEventType.RecruitRejected => true,
            LegacyEventType.ProspectContractOffered or LegacyEventType.ProspectSigned or LegacyEventType.DraftRecapCreated => true,
            LegacyEventType.FrontOfficeReadinessReportCreated or LegacyEventType.EndOfSeasonExecutiveReviewCreated or LegacyEventType.MonthlyGmSummaryCreated => true,
            LegacyEventType.GamePlayed => item.Title.Contains("recap", StringComparison.OrdinalIgnoreCase) || item.Summary.Contains("record", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static JournalEntry ToJournalEntry(AlphaInboxItem item)
    {
        var entry = new JournalEntry(
            $"journal:{item.InboxItemId}",
            item.Date,
            JournalCategoryFor(item.EventType),
            item.Title,
            item.Summary,
            item.PrimaryPersonId,
            $"{item.Title} {item.Summary} {item.EventType}");
        entry.Validate();
        return entry;
    }

    private static JournalCategory JournalCategoryFor(LegacyEventType eventType) =>
        eventType switch
        {
            LegacyEventType.ScoutAssigned or LegacyEventType.ScoutAssignedToPlayer or LegacyEventType.ScoutAssignedToRegion or LegacyEventType.ScoutingReportCreated or LegacyEventType.ScoutingReportUpdated => JournalCategory.Scouting,
            LegacyEventType.PlayerDevelopmentUpdated => JournalCategory.Development,
            LegacyEventType.PlayerAddedToRoster or LegacyEventType.PlayerRemovedFromRoster or LegacyEventType.PlayerReleased => JournalCategory.Roster,
            LegacyEventType.StaffFocusChanged or LegacyEventType.StaffRoleChanged or LegacyEventType.StaffCandidateGenerated => JournalCategory.Staff,
            LegacyEventType.ContractOffered or LegacyEventType.ContractSigned or LegacyEventType.ContractOfferSubmitted => JournalCategory.Contracts,
            LegacyEventType.PlayerDrafted or LegacyEventType.DraftBoardChanged or LegacyEventType.ProspectInvitedToCamp or LegacyEventType.ProspectReturned => JournalCategory.Draft,
            LegacyEventType.GamePlayed or LegacyEventType.PhaseChanged or LegacyEventType.MilestoneReached => JournalCategory.League,
            _ => JournalCategory.System
        };

    private static void AddPeopleResults(NewGmScenarioSnapshot scenario, string query, List<GlobalSearchResult> results)
    {
        foreach (var person in scenario.AlphaSnapshot.People.Concat(scenario.AlphaSnapshot.Players).GroupBy(person => person.PersonId, StringComparer.Ordinal).Select(group => group.First()))
        {
            var name = person.Identity.DisplayName;
            if (!Matches(query, name, person.Identity.Nationality, person.Identity.Birthplace))
            {
                continue;
            }

            results.Add(new GlobalSearchResult(
                $"search:person:{person.PersonId}",
                "Person",
                name,
                $"{PositionText(scenario, person.PersonId)} | age {person.CalculateAge(scenario.CurrentDate)}",
                "Player Dossier",
                person.PersonId,
                $"Open dossier/profile for {name}."));
        }
    }

    private static void AddStaffResults(NewGmScenarioSnapshot scenario, string query, List<GlobalSearchResult> results)
    {
        foreach (var staff in scenario.StaffMembers)
        {
            var name = PersonName(scenario, staff.PersonId);
            if (!Matches(query, name, staff.CurrentRole.ToString(), staff.Department.ToString()))
            {
                continue;
            }

            results.Add(new GlobalSearchResult(
                $"search:staff:{staff.PersonId}",
                "Staff",
                name,
                $"{staff.CurrentRole} | {staff.Department}",
                "Organization > Staff",
                staff.PersonId,
                "Open staff profile and actions."));
        }
    }

    private static void AddDraftResults(NewGmScenarioSnapshot scenario, string query, List<GlobalSearchResult> results)
    {
        foreach (var pick in scenario.DraftPickHistory)
        {
            if (!Matches(query, pick.PlayerName, pick.TeamDraftedFrom, pick.ScoutingProjectionAtDraft, pick.OutcomeSummary))
            {
                continue;
            }

            results.Add(new GlobalSearchResult(
                $"search:draft-pick:{pick.Year}:{pick.PlayerPersonId}",
                "Draft Pick",
                pick.PlayerName,
                $"{pick.Year} R{pick.Round} P{pick.OverallPick} | {pick.Position}",
                "Reports / History > Drafted Players",
                pick.PlayerPersonId,
                $"{pick.PlayerName} was drafted from {pick.TeamDraftedFrom}. {pick.OutcomeSummary}"));
        }
    }

    private static void AddFreeAgentResults(NewGmScenarioSnapshot scenario, string query, List<GlobalSearchResult> results)
    {
        foreach (var agent in scenario.FreeAgentMarket?.FreeAgents ?? Array.Empty<FreeAgent>())
        {
            if (!Matches(query, agent.Name, agent.PreviousTeam, agent.Position.ToString(), agent.Status.ToString()))
            {
                continue;
            }

            results.Add(new GlobalSearchResult(
                $"search:free-agent:{agent.PersonId}",
                "Free Agent",
                agent.Name,
                $"{agent.Position} | age {agent.Age} | ask {agent.ContractAsk.AnnualAmount:C0}",
                "Hockey Operations > Free Agents",
                agent.PersonId,
                agent.FitSummary.StaffRecommendation));
        }
    }

    private static void AddHistoryResults(NewGmScenarioSnapshot scenario, string query, List<GlobalSearchResult> results)
    {
        foreach (var entry in scenario.CareerTimeline.Entries)
        {
            if (!Matches(query, entry.Title, entry.Description, entry.TeamName ?? string.Empty))
            {
                continue;
            }

            results.Add(new GlobalSearchResult(
                $"search:history:{entry.EntryId}",
                "History",
                entry.Title,
                $"{entry.Date:yyyy-MM-dd} | {entry.EntryType}",
                "Reports / History",
                entry.PersonId,
                entry.Description));
        }
    }

    private static void AddOrganizationResults(NewGmScenarioSnapshot scenario, string query, List<GlobalSearchResult> results)
    {
        var league = new LeagueAiService().BuildReport(scenario).Profiles;
        foreach (var profile in league)
        {
            if (!Matches(query, profile.TeamName, profile.Identity.ToString(), profile.CurrentStrategy.ToString(), profile.Summary))
            {
                continue;
            }

            results.Add(new GlobalSearchResult(
                $"search:organization:{profile.OrganizationId}",
                "Organization",
                profile.TeamName,
                $"{profile.Identity} | {profile.CurrentStrategy}",
                "Organization > Organization Health",
                null,
                profile.Summary));
        }
    }

    private static void AddMessageResults(IReadOnlyList<InboxMessage> inbox, IReadOnlyList<LeagueTransaction> leagueNews, IReadOnlyList<JournalEntry> journal, string query, List<GlobalSearchResult> results)
    {
        foreach (var message in inbox)
        {
            if (!Matches(query, message.Item.Title, message.Item.Summary))
            {
                continue;
            }

            results.Add(new GlobalSearchResult($"search:inbox:{message.InboxItemId}", "Inbox", message.Item.Title, $"{message.Category} | {message.Priority}", "Inbox", message.Item.PrimaryPersonId, message.Item.Summary));
        }

        foreach (var transaction in leagueNews)
        {
            if (!Matches(query, transaction.TeamName, transaction.PersonName, transaction.Description))
            {
                continue;
            }

            results.Add(new GlobalSearchResult($"search:league:{transaction.TransactionId}", "League News", transaction.TeamName, $"{transaction.Category} | {transaction.TransactionType}", "Inbox > League News", transaction.PersonId, transaction.Description));
        }

        foreach (var entry in journal)
        {
            if (!Matches(query, entry.SearchText))
            {
                continue;
            }

            results.Add(new GlobalSearchResult($"search:journal:{entry.JournalEntryId}", "Journal", entry.Title, $"{entry.Category} | {entry.Date:yyyy-MM-dd}", "Reports / History > Journal", entry.RelatedPersonId, entry.Summary));
        }
    }

    private static PlaytestChecklistItem Checklist(string id, string area, string question, string expected, bool passing, string notes) =>
        new(id, area, question, expected, passing, notes);

    private static bool Matches(string query, params string[] values) =>
        values.Any(value => !string.IsNullOrWhiteSpace(value) && value.Contains(query, StringComparison.OrdinalIgnoreCase));

    private static string StableInboxKey(AlphaInboxItem item) =>
        $"{item.EventType}:{item.PrimaryPersonId}:{item.Title}:{DateOnly.FromDateTime(item.Date.Date)}";

    private static bool IsRoutineText(string text)
    {
        var normalized = text.Trim().TrimEnd('.').ToLowerInvariant();
        return normalized is "player added to roster"
            or "contract signed"
            or "contract offered"
            or "staff focus changed"
            or "draft board changed"
            or "free agent shortlisted";
    }

    private static string PersonName(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? scenario.AlphaSnapshot.Players.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? scenario.ProspectRights.FirstOrDefault(prospect => prospect.ProspectPersonId == personId)?.ProspectName
        ?? scenario.FreeAgentMarket?.Find(personId)?.Name
        ?? personId;

    private static string PositionText(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.Roster.FindPlayer(personId)?.Position.ToString()
        ?? scenario.ProspectRights.FirstOrDefault(prospect => prospect.ProspectPersonId == personId)?.Position.ToString()
        ?? scenario.AlphaSnapshot.DraftBoard.Entries.FirstOrDefault(entry => entry.ProspectPersonId == personId)?.Bio?.Position.ToString()
        ?? "Unknown";
}
