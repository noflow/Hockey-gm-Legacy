using LegacyEngine.Contracts;
using LegacyEngine.Rosters;
using LegacyEngine.Staff;

namespace LegacyEngine.Integration;

public sealed class RelationshipExpansionService
{
    public NewGmScenarioSnapshot EnsureExpansion(NewGmScenarioSnapshot scenario, EngineRegistry? registry = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var profiles = scenario.RelationshipProfiles.ToDictionary(profile => profile.RelationshipProfileId, StringComparer.Ordinal);
        foreach (var profile in BuildBaselineProfiles(scenario))
        {
            profiles.TryAdd(profile.RelationshipProfileId, profile);
        }

        var changes = scenario.RelationshipChangeHistory.ToList();
        var conflicts = scenario.RelationshipConflicts.ToList();
        AddHistoricalSignals(scenario, profiles, changes, conflicts);

        var chemistry = BuildChemistrySummary(profiles.Values, conflicts);
        var updated = scenario with
        {
            RelationshipProfiles = profiles.Values
                .OrderBy(profile => profile.RelationshipType)
                .ThenBy(profile => profile.TargetName, StringComparer.Ordinal)
                .ToArray(),
            RelationshipChangeHistory = changes
                .GroupBy(change => change.ChangeId, StringComparer.Ordinal)
                .Select(group => group.Last())
                .OrderByDescending(change => change.Date)
                .ThenBy(change => change.ChangeId, StringComparer.Ordinal)
                .ToArray(),
            RelationshipConflicts = conflicts
                .GroupBy(conflict => conflict.ConflictId, StringComparer.Ordinal)
                .Select(group => group.Last())
                .OrderByDescending(conflict => conflict.IsMajor)
                .ThenByDescending(conflict => conflict.Severity)
                .ThenBy(conflict => conflict.Date)
                .ToArray(),
            RelationshipChemistry = chemistry
        };

        updated.Validate();
        return updated;
    }

    public NewGmScenarioSnapshot RecordChange(
        NewGmScenarioSnapshot scenario,
        string sourceId,
        string targetId,
        ExpandedRelationshipType relationshipType,
        RelationshipChangeTrigger trigger,
        int amount,
        DateOnly date,
        string reason,
        string visibleExplanation,
        string? relatedEventId = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario = EnsureExpansion(scenario);

        var profile = FindOrCreateProfile(scenario, sourceId, targetId, relationshipType);
        var nextProfile = ApplyChange(profile, amount, trigger, visibleExplanation);
        var change = new RelationshipChangeRecord(
            $"relationship-change:{profile.RelationshipProfileId}:{trigger}:{date:yyyyMMdd}:{StableId(reason)}",
            profile.RelationshipProfileId,
            trigger,
            date,
            reason,
            amount,
            relatedEventId,
            visibleExplanation);
        change.Validate();

        var conflicts = scenario.RelationshipConflicts.ToList();
        if (trigger == RelationshipChangeTrigger.BrokenPromise || nextProfile.Conflict >= 70)
        {
            conflicts.Add(CreateConflict(nextProfile, RelationshipConflictType.BrokenPromise, date, Math.Max(nextProfile.Conflict, 70), reason, visibleExplanation));
        }

        var profiles = scenario.RelationshipProfiles
            .Where(item => item.RelationshipProfileId != profile.RelationshipProfileId)
            .Append(nextProfile)
            .ToArray();
        var changes = scenario.RelationshipChangeHistory
            .Where(item => item.ChangeId != change.ChangeId)
            .Append(change)
            .ToArray();

        var updated = scenario with
        {
            RelationshipProfiles = profiles,
            RelationshipChangeHistory = changes,
            RelationshipConflicts = conflicts,
            RelationshipChemistry = BuildChemistrySummary(profiles, conflicts)
        };
        updated.Validate();
        return updated;
    }

    public NewGmScenarioSnapshot RecordSigning(NewGmScenarioSnapshot scenario, string personId, DateOnly date, string? relatedEventId = null) =>
        RecordChange(
            scenario,
            scenario.AlphaSnapshot.GeneralManager.PersonId,
            personId,
            TypeForGmTarget(scenario, personId),
            RelationshipChangeTrigger.Signing,
            7,
            date,
            "Contract agreement created trust through a clear commitment.",
            $"{PersonName(scenario, personId)} appreciated a concrete contract path with the organization.",
            relatedEventId);

    public NewGmScenarioSnapshot RecordRejectedOffer(NewGmScenarioSnapshot scenario, string personId, DateOnly date, string? relatedEventId = null) =>
        RecordChange(
            scenario,
            scenario.AlphaSnapshot.GeneralManager.PersonId,
            personId,
            TypeForGmTarget(scenario, personId),
            RelationshipChangeTrigger.RejectedOffer,
            -8,
            date,
            "Rejected offer created negotiation frustration.",
            $"{PersonName(scenario, personId)} may need clearer role, trust, or contract terms before re-engaging.",
            relatedEventId);

    public NewGmScenarioSnapshot RecordTrade(NewGmScenarioSnapshot scenario, string personId, DateOnly date, string? relatedEventId = null) =>
        RecordChange(
            scenario,
            scenario.AlphaSnapshot.GeneralManager.PersonId,
            personId,
            TypeForGmTarget(scenario, personId),
            RelationshipChangeTrigger.Trade,
            -5,
            date,
            "Trade movement changed the relationship with the front office.",
            $"{PersonName(scenario, personId)} will judge the move by role clarity, communication, and fit.",
            relatedEventId);

    public NewGmScenarioSnapshot RecordBrokenPromise(NewGmScenarioSnapshot scenario, string personId, DateOnly date, string promise, string? relatedEventId = null) =>
        RecordChange(
            scenario,
            scenario.AlphaSnapshot.GeneralManager.PersonId,
            personId,
            TypeForGmTarget(scenario, personId),
            RelationshipChangeTrigger.BrokenPromise,
            -15,
            date,
            $"Broken promise: {promise}",
            $"{PersonName(scenario, personId)} has a trust issue because the promised path was not followed.",
            relatedEventId);

    public RelationshipImpactSummary BuildContractImpact(NewGmScenarioSnapshot scenario, string personId)
    {
        var profile = FindProfile(EnsureExpansion(scenario), scenario.AlphaSnapshot.GeneralManager.PersonId, personId)
            ?? FindProfile(EnsureExpansion(scenario), scenario.Organization.OrganizationId, personId);
        var modifier = ModestModifier(profile);
        var explanation = profile is null
            ? "Relationship context is neutral for this contract decision."
            : $"{profile.TargetName} relationship is {profile.Label.ToLowerInvariant()} with {profile.Trend.ToString().ToLowerInvariant()} trend; contract likelihood modifier {modifier}.";
        var summary = new RelationshipImpactSummary(RelationshipImpactArea.Contracts, modifier, explanation);
        summary.Validate();
        return summary;
    }

    public RelationshipImpactSummary BuildTradeImpact(NewGmScenarioSnapshot scenario, string personId)
    {
        var profile = FindProfile(EnsureExpansion(scenario), scenario.AlphaSnapshot.GeneralManager.PersonId, personId)
            ?? FindProfile(EnsureExpansion(scenario), scenario.Organization.OrganizationId, personId);
        var modifier = profile is null ? 0 : Math.Clamp((50 - profile.OverallScore) / 10, -8, 8);
        var summary = new RelationshipImpactSummary(
            RelationshipImpactArea.Trades,
            modifier,
            profile is null
                ? "Trade willingness is neutral; no meaningful relationship profile exists yet."
                : $"{profile.TargetName} trade reaction is shaped by a {profile.Label.ToLowerInvariant()} relationship and {profile.Trend.ToString().ToLowerInvariant()} trend.");
        summary.Validate();
        return summary;
    }

    public StaffChemistryReport ApplyStaffChemistry(StaffChemistryReport report, NewGmScenarioSnapshot scenario)
    {
        var expanded = EnsureExpansion(scenario);
        var profile = FindProfile(expanded, scenario.AlphaSnapshot.GeneralManager.PersonId, report.PersonId);
        if (profile is null)
        {
            return report;
        }

        var warnings = report.ConflictWarnings.ToList();
        if (profile.Conflict >= 65)
        {
            warnings.Add($"Relationship conflict: {profile.Summary}");
        }

        foreach (var conflict in expanded.RelationshipConflicts.Where(conflict => conflict.RelationshipProfileId == profile.RelationshipProfileId && conflict.IsActive).Take(2))
        {
            warnings.Add($"Relationship conflict: {conflict.VisibleExplanation}");
        }

        var adjusted = report with
        {
            GmFit = Math.Clamp((report.GmFit + profile.OverallScore) / 2, 0, 100),
            ConflictWarnings = warnings.Distinct(StringComparer.Ordinal).ToArray(),
            Summary = $"{report.Summary} Relationship read: {profile.Label}, {profile.Trend} trend."
        };
        adjusted.Validate();
        return adjusted;
    }

    public IReadOnlyList<string> BuildDossierRelationshipLines(NewGmScenarioSnapshot scenario, string personId)
    {
        var expanded = EnsureExpansion(scenario);
        var profiles = expanded.RelationshipProfiles
            .Where(profile => profile.SourceId == personId || profile.TargetId == personId)
            .OrderByDescending(profile => profile.OverallScore)
            .Take(8)
            .ToArray();

        var lines = profiles
            .Select(profile =>
            {
                var other = profile.SourceId == personId ? profile.TargetName : profile.SourceName;
                return $"{profile.RelationshipType}: {other} - {profile.Label}, trust {profile.Trust}, respect {profile.Respect}, loyalty {profile.Loyalty}, conflict {profile.Conflict}, communication {profile.CommunicationQuality}, trend {profile.Trend}. {profile.Summary}";
            })
            .ToList();

        foreach (var conflict in expanded.RelationshipConflicts.Where(conflict => profiles.Any(profile => profile.RelationshipProfileId == conflict.RelationshipProfileId)).Take(4))
        {
            lines.Add($"Conflict: {conflict.ConflictType} ({conflict.Severity}/100) - {conflict.VisibleExplanation}");
        }

        foreach (var change in expanded.RelationshipChangeHistory.Where(change => profiles.Any(profile => profile.RelationshipProfileId == change.RelationshipProfileId)).Take(5))
        {
            lines.Add($"Recent change: {change.Date:yyyy-MM-dd} {change.Trigger} {change.Amount:+#;-#;0} - {change.VisibleExplanation}");
        }

        return lines.Count == 0 ? new[] { "No expanded relationship notes are currently tracked." } : lines;
    }

    public IReadOnlyList<ActionCenterItem> BuildActionItems(NewGmScenarioSnapshot scenario)
    {
        var expanded = EnsureExpansion(scenario);
        return expanded.RelationshipConflicts
            .Where(conflict => conflict.IsActive && conflict.IsMajor)
            .Take(4)
            .Select(conflict =>
            {
                var profile = expanded.RelationshipProfiles.First(item => item.RelationshipProfileId == conflict.RelationshipProfileId);
                return new ActionCenterItem(
                    $"action-center:relationship-conflict:{conflict.ConflictId}",
                    $"Relationship conflict: {profile.TargetName}",
                    CategoryFor(profile.RelationshipType),
                    ActionCenterPriority.Important,
                    conflict.Date.AddDays(7),
                    profile.TargetId,
                    profile.TargetName,
                    scenario.Organization.OrganizationId,
                    scenario.Organization.Name,
                    conflict.VisibleExplanation,
                    "Major relationship conflicts can weaken contracts, staff chemistry, morale, and communication quality.",
                    "Review the related dossier/profile and decide whether a meeting, role clarity, or follow-up is needed.",
                    null,
                    null,
                    null);
            })
            .ToArray();
    }

    public string BuildOrganizationHealthSummary(NewGmScenarioSnapshot scenario)
    {
        var expanded = EnsureExpansion(scenario);
        var chemistry = expanded.RelationshipChemistry ?? BuildChemistrySummary(expanded.RelationshipProfiles, expanded.RelationshipConflicts);
        return string.Join(Environment.NewLine, chemistry.SummaryLines);
    }

    private IReadOnlyList<ExpandedRelationshipProfile> BuildBaselineProfiles(NewGmScenarioSnapshot scenario)
    {
        var gm = scenario.AlphaSnapshot.GeneralManager;
        var org = scenario.Organization;
        var owner = scenario.AlphaSnapshot.Owner;
        var profiles = new List<ExpandedRelationshipProfile>
        {
            CreateProfile(scenario, ExpandedRelationshipType.GmOwner, gm.PersonId, gm.Identity.DisplayName, owner.OwnerId, owner.Name, 58, 55, 54, "Owner-GM trust starts from the hiring process."),
            CreateProfile(scenario, ExpandedRelationshipType.OrganizationOrganization, org.OrganizationId, org.Name, scenario.LeagueProfile.Identity.LeagueId, scenario.LeagueProfile.Identity.Name, 55, 58, 50, "The club has a neutral working relationship with the league.")
        };

        foreach (var player in scenario.AlphaSnapshot.Roster.ActivePlayers.Take(30))
        {
            profiles.Add(CreateProfile(scenario, ExpandedRelationshipType.GmPlayer, gm.PersonId, gm.Identity.DisplayName, player.PersonId, PersonName(scenario, player.PersonId), 50 + StableNumber(player.PersonId, 18), 48 + StableNumber(player.PersonId + "r", 22), 45 + StableNumber(player.PersonId + "l", 20), "The new GM is still building player trust."));
            profiles.Add(CreateProfile(scenario, ExpandedRelationshipType.OrganizationPlayer, org.OrganizationId, org.Name, player.PersonId, PersonName(scenario, player.PersonId), 56, 55, 52, "Existing roster player has organizational history before the new GM arrived."));
        }

        foreach (var staff in scenario.AlphaSnapshot.StaffMembers.Concat(scenario.StaffMembers).DistinctBy(staff => staff.PersonId).Take(16))
        {
            var staffName = PersonName(scenario, staff.PersonId);
            profiles.Add(CreateProfile(scenario, ExpandedRelationshipType.GmStaff, gm.PersonId, gm.Identity.DisplayName, staff.PersonId, staffName, 53 + StableNumber(staff.PersonId, 17), 52 + StableNumber(staff.PersonId + "s", 18), 50 + StableNumber(staff.PersonId + "l", 18), "Front-office trust depends on role clarity and follow-through."));
            profiles.Add(CreateProfile(scenario, ExpandedRelationshipType.OrganizationStaff, org.OrganizationId, org.Name, staff.PersonId, staffName, 58, 56, 55, "Staff member has existing organization context."));
            profiles.Add(CreateProfile(scenario, ExpandedRelationshipType.StaffOwner, staff.PersonId, staffName, owner.OwnerId, owner.Name, 52, 54, 50, "Ownership expects staff to support the new GM plan."));
        }

        var coaches = scenario.AlphaSnapshot.StaffMembers.Concat(scenario.StaffMembers)
            .Where(staff => staff.CurrentRole is StaffRole.HeadCoach or StaffRole.AssistantCoach or StaffRole.DevelopmentCoach)
            .DistinctBy(staff => staff.PersonId)
            .Take(2)
            .ToArray();
        foreach (var coach in coaches)
        {
            foreach (var player in scenario.AlphaSnapshot.Roster.ActivePlayers.Take(8))
            {
                profiles.Add(CreateProfile(scenario, ExpandedRelationshipType.PlayerCoach, player.PersonId, PersonName(scenario, player.PersonId), coach.PersonId, PersonName(scenario, coach.PersonId), 54, 57, 50, "Coach-player fit is based on communication, role clarity, and development trust."));
            }
        }

        foreach (var agent in scenario.Agents.Take(10))
        {
            profiles.Add(CreateProfile(scenario, ExpandedRelationshipType.GmAgent, gm.PersonId, gm.Identity.DisplayName, agent.AgentId, agent.Name, agent.GmRelationship.Score, agent.GmRelationship.Trust, 50, "Agent negotiation tone reflects prior trust and communication."));
            profiles.Add(CreateProfile(scenario, ExpandedRelationshipType.OrganizationAgent, org.OrganizationId, org.Name, agent.AgentId, agent.Name, agent.OrganizationRelationship.Score, agent.OrganizationRelationship.Trust, 50, "Organization-agent relationship affects negotiation tone."));
        }

        var staffPairs = scenario.AlphaSnapshot.StaffMembers.Concat(scenario.StaffMembers).DistinctBy(staff => staff.PersonId).Take(5).ToArray();
        for (var i = 0; i < staffPairs.Length - 1; i++)
        {
            profiles.Add(CreateProfile(scenario, ExpandedRelationshipType.StaffStaff, staffPairs[i].PersonId, PersonName(scenario, staffPairs[i].PersonId), staffPairs[i + 1].PersonId, PersonName(scenario, staffPairs[i + 1].PersonId), 55, 57, 52, "Department chemistry depends on communication and shared priorities."));
        }

        return profiles;
    }

    private static void AddHistoricalSignals(
        NewGmScenarioSnapshot scenario,
        Dictionary<string, ExpandedRelationshipProfile> profiles,
        List<RelationshipChangeRecord> changes,
        List<RelationshipConflict> conflicts)
    {
        foreach (var contract in scenario.Contracts.Concat(scenario.AlphaSnapshot.Contracts).Where(contract => contract.Status == ContractStatus.Signed).Take(10))
        {
            var id = ProfileId(scenario.AlphaSnapshot.GeneralManager.PersonId, contract.PersonId, ExpandedRelationshipType.GmPlayer);
            if (!profiles.TryGetValue(id, out var profile))
            {
                continue;
            }

            var change = new RelationshipChangeRecord(
                $"relationship-change:{id}:signed:{contract.Term.StartDate:yyyyMMdd}",
                id,
                RelationshipChangeTrigger.Signing,
                contract.Term.StartDate,
                "Signed contract created positive shared history.",
                5,
                null,
                $"{profile.TargetName} has a signed agreement on record, which supports trust.");
            if (!changes.Any(item => item.ChangeId == change.ChangeId))
            {
                changes.Add(change);
                profiles[id] = ApplyChange(profile, 5, RelationshipChangeTrigger.Signing, change.VisibleExplanation);
            }
        }

        foreach (var offer in scenario.TradeOffers.Where(offer => offer.Status is TradeOfferStatus.Rejected or TradeOfferStatus.Countered).Take(5))
        {
            foreach (var asset in offer.PlayerGives.Concat(offer.PlayerReceives).Where(asset => asset.AssetType is TradeAssetType.Player or TradeAssetType.ProspectRights).Take(2))
            {
                var id = ProfileId(scenario.AlphaSnapshot.GeneralManager.PersonId, asset.AssetId, ExpandedRelationshipType.GmPlayer);
                if (!profiles.TryGetValue(id, out var profile))
                {
                    continue;
                }

                var change = new RelationshipChangeRecord(
                    $"relationship-change:{id}:trade:{offer.ProposedOn:yyyyMMdd}:{offer.TradeOfferId}",
                    id,
                    RelationshipChangeTrigger.Trade,
                    offer.ProposedOn,
                    "Trade discussion affected player trust and communication.",
                    -4,
                    null,
                    $"{profile.TargetName} noticed trade activity and may need clear communication.");
                if (!changes.Any(item => item.ChangeId == change.ChangeId))
                {
                    changes.Add(change);
                    profiles[id] = ApplyChange(profile, -4, RelationshipChangeTrigger.Trade, change.VisibleExplanation);
                }
            }
        }

        foreach (var profile in profiles.Values.Where(profile => profile.Conflict >= 72))
        {
            var conflict = CreateConflict(profile, RelationshipConflictType.TrustIssue, scenario.CurrentDate, profile.Conflict, "Relationship conflict threshold reached.", profile.Summary);
            if (!conflicts.Any(item => item.ConflictId == conflict.ConflictId))
            {
                conflicts.Add(conflict);
            }
        }
    }

    private ExpandedRelationshipProfile FindOrCreateProfile(NewGmScenarioSnapshot scenario, string sourceId, string targetId, ExpandedRelationshipType type)
    {
        var profile = FindProfile(scenario, sourceId, targetId, type);
        return profile ?? CreateProfile(scenario, type, sourceId, PersonName(scenario, sourceId), targetId, PersonName(scenario, targetId), 50, 50, 50, "Relationship profile was created from a new hockey event.");
    }

    private static ExpandedRelationshipProfile? FindProfile(NewGmScenarioSnapshot scenario, string sourceId, string targetId, ExpandedRelationshipType? type = null) =>
        scenario.RelationshipProfiles.FirstOrDefault(profile =>
            profile.SourceId == sourceId
            && profile.TargetId == targetId
            && (type is null || profile.RelationshipType == type));

    private static ExpandedRelationshipProfile CreateProfile(
        NewGmScenarioSnapshot scenario,
        ExpandedRelationshipType type,
        string sourceId,
        string sourceName,
        string targetId,
        string targetName,
        int trust,
        int respect,
        int loyalty,
        string summary)
    {
        var conflict = Math.Clamp(100 - ((trust + respect + loyalty) / 3) + StableNumber(sourceId + targetId, 8) - 4, 0, 100);
        var communication = Math.Clamp((trust + respect) / 2 - Math.Max(0, conflict - 60) / 3, 0, 100);
        var profile = new ExpandedRelationshipProfile(
            ProfileId(sourceId, targetId, type),
            type,
            sourceId,
            sourceName,
            targetId,
            targetName,
            Math.Clamp(trust, 0, 100),
            Math.Clamp(respect, 0, 100),
            Math.Clamp(loyalty, 0, 100),
            conflict,
            communication,
            TrendFor(trust, respect, loyalty, conflict),
            new[] { $"Baseline relationship read created on {scenario.CurrentDate:yyyy-MM-dd}." },
            new[] { summary },
            summary);
        profile.Validate();
        return profile;
    }

    private static ExpandedRelationshipProfile ApplyChange(
        ExpandedRelationshipProfile profile,
        int amount,
        RelationshipChangeTrigger trigger,
        string visibleExplanation)
    {
        var trustDelta = trigger switch
        {
            RelationshipChangeTrigger.BrokenPromise or RelationshipChangeTrigger.RejectedOffer => amount,
            RelationshipChangeTrigger.Signing or RelationshipChangeTrigger.FulfilledPromise => amount,
            RelationshipChangeTrigger.Trade or RelationshipChangeTrigger.Release => amount / 2,
            _ => amount / 2
        };
        var respectDelta = trigger is RelationshipChangeTrigger.DevelopmentSuccess or RelationshipChangeTrigger.ScoutingSuccess or RelationshipChangeTrigger.Promotion ? amount : amount / 3;
        var loyaltyDelta = trigger is RelationshipChangeTrigger.BrokenPromise or RelationshipChangeTrigger.Release or RelationshipChangeTrigger.Demotion ? amount : amount / 3;
        var conflictDelta = amount < 0 ? Math.Abs(amount) : -Math.Max(1, amount / 2);

        var trust = Math.Clamp(profile.Trust + trustDelta, 0, 100);
        var respect = Math.Clamp(profile.Respect + respectDelta, 0, 100);
        var loyalty = Math.Clamp(profile.Loyalty + loyaltyDelta, 0, 100);
        var conflict = Math.Clamp(profile.Conflict + conflictDelta, 0, 100);
        var communication = Math.Clamp((trust + respect + loyalty + (100 - conflict)) / 4, 0, 100);
        var updated = profile with
        {
            Trust = trust,
            Respect = respect,
            Loyalty = loyalty,
            Conflict = conflict,
            CommunicationQuality = communication,
            Trend = amount > 0 ? ExpandedRelationshipTrend.Rising : conflict >= 70 ? ExpandedRelationshipTrend.Strained : ExpandedRelationshipTrend.Falling,
            KeyMoments = profile.KeyMoments.Append(visibleExplanation).TakeLast(8).ToArray(),
            History = profile.History.Append($"{trigger}: {visibleExplanation}").TakeLast(10).ToArray(),
            Summary = visibleExplanation
        };
        updated.Validate();
        return updated;
    }

    private static RelationshipConflict CreateConflict(ExpandedRelationshipProfile profile, RelationshipConflictType type, DateOnly date, int severity, string reason, string visibleExplanation)
    {
        var conflict = new RelationshipConflict(
            $"relationship-conflict:{profile.RelationshipProfileId}:{type}:{date:yyyyMMdd}",
            profile.RelationshipProfileId,
            type,
            date,
            Math.Clamp(severity, 0, 100),
            reason,
            visibleExplanation,
            severity >= 65);
        conflict.Validate();
        return conflict;
    }

    private static RelationshipChemistrySummary BuildChemistrySummary(IEnumerable<ExpandedRelationshipProfile> profiles, IReadOnlyList<RelationshipConflict> conflicts)
    {
        var profileArray = profiles.ToArray();
        var roster = Average(profileArray, ExpandedRelationshipType.GmPlayer, ExpandedRelationshipType.OrganizationPlayer);
        var staff = Average(profileArray, ExpandedRelationshipType.GmStaff, ExpandedRelationshipType.StaffStaff, ExpandedRelationshipType.OrganizationStaff);
        var scouts = Average(profileArray.Where(profile => profile.TargetName.Contains("Scout", StringComparison.OrdinalIgnoreCase)), ExpandedRelationshipType.GmStaff, ExpandedRelationshipType.OrganizationStaff);
        var coachPlayer = Average(profileArray, ExpandedRelationshipType.PlayerCoach);
        var office = Average(profileArray, ExpandedRelationshipType.GmOwner, ExpandedRelationshipType.GmStaff, ExpandedRelationshipType.GmAgent);

        if (conflicts.Any(conflict => conflict.IsMajor && conflict.IsActive))
        {
            staff = Downgrade(staff);
            office = Downgrade(office);
        }

        var summary = new RelationshipChemistrySummary(
            roster,
            staff,
            scouts,
            coachPlayer,
            office,
            new[]
            {
                $"Roster chemistry: {roster}.",
                $"Staff chemistry: {staff}.",
                $"Scouting department chemistry: {scouts}.",
                $"Coach-player fit: {coachPlayer}.",
                $"GM office relationship: {office}.",
                conflicts.Any(conflict => conflict.IsMajor && conflict.IsActive)
                    ? "Major conflicts need GM attention before they erode communication."
                    : "No major relationship conflict is currently active."
            });
        summary.Validate();
        return summary;
    }

    private static RelationshipChemistryLevel Average(IEnumerable<ExpandedRelationshipProfile> profiles, params ExpandedRelationshipType[] types)
    {
        var values = profiles.Where(profile => types.Contains(profile.RelationshipType)).Select(profile => profile.OverallScore).ToArray();
        if (values.Length == 0)
        {
            return RelationshipChemistryLevel.Neutral;
        }

        return LevelFor((int)Math.Round(values.Average()));
    }

    private static RelationshipChemistryLevel LevelFor(int score) =>
        score >= 78 ? RelationshipChemistryLevel.Excellent :
        score >= 63 ? RelationshipChemistryLevel.Good :
        score >= 45 ? RelationshipChemistryLevel.Neutral :
        score >= 30 ? RelationshipChemistryLevel.Poor :
        RelationshipChemistryLevel.Problem;

    private static RelationshipChemistryLevel Downgrade(RelationshipChemistryLevel level) =>
        level switch
        {
            RelationshipChemistryLevel.Excellent => RelationshipChemistryLevel.Good,
            RelationshipChemistryLevel.Good => RelationshipChemistryLevel.Neutral,
            RelationshipChemistryLevel.Neutral => RelationshipChemistryLevel.Poor,
            _ => RelationshipChemistryLevel.Problem
        };

    private static int ModestModifier(ExpandedRelationshipProfile? profile)
    {
        if (profile is null)
        {
            return 0;
        }

        var score = profile.OverallScore - 50;
        return Math.Clamp(score / 6, -8, 8);
    }

    private static ActionCenterCategory CategoryFor(ExpandedRelationshipType type) =>
        type switch
        {
            ExpandedRelationshipType.GmOwner or ExpandedRelationshipType.StaffOwner => ActionCenterCategory.Owner,
            ExpandedRelationshipType.GmStaff or ExpandedRelationshipType.StaffStaff or ExpandedRelationshipType.OrganizationStaff => ActionCenterCategory.Staff,
            ExpandedRelationshipType.GmAgent or ExpandedRelationshipType.OrganizationAgent => ActionCenterCategory.Contracts,
            ExpandedRelationshipType.PlayerCoach or ExpandedRelationshipType.PlayerStaff or ExpandedRelationshipType.PlayerPlayer or ExpandedRelationshipType.GmPlayer or ExpandedRelationshipType.OrganizationPlayer => ActionCenterCategory.PlayerDevelopment,
            _ => ActionCenterCategory.System
        };

    private static ExpandedRelationshipType TypeForGmTarget(NewGmScenarioSnapshot scenario, string targetId)
    {
        if (scenario.Agents.Any(agent => agent.AgentId == targetId))
        {
            return ExpandedRelationshipType.GmAgent;
        }

        if (scenario.StaffMembers.Concat(scenario.AlphaSnapshot.StaffMembers).Any(staff => staff.PersonId == targetId))
        {
            return ExpandedRelationshipType.GmStaff;
        }

        if (targetId == scenario.AlphaSnapshot.Owner.OwnerId)
        {
            return ExpandedRelationshipType.GmOwner;
        }

        return ExpandedRelationshipType.GmPlayer;
    }

    private static ExpandedRelationshipTrend TrendFor(int trust, int respect, int loyalty, int conflict)
    {
        if (conflict >= 70)
        {
            return ExpandedRelationshipTrend.Strained;
        }

        var score = (trust + respect + loyalty) / 3;
        return score >= 68 ? ExpandedRelationshipTrend.Rising :
            score <= 42 ? ExpandedRelationshipTrend.Falling :
            ExpandedRelationshipTrend.Stable;
    }

    private static string ProfileId(string sourceId, string targetId, ExpandedRelationshipType type) =>
        $"relationship-profile:{type}:{sourceId}:{targetId}";

    private static string PersonName(NewGmScenarioSnapshot scenario, string personId)
    {
        if (personId == scenario.Organization.OrganizationId)
        {
            return scenario.Organization.Name;
        }

        if (personId == scenario.AlphaSnapshot.Owner.OwnerId)
        {
            return scenario.AlphaSnapshot.Owner.Name;
        }

        var agent = scenario.Agents.FirstOrDefault(agent => agent.AgentId == personId);
        if (agent is not null)
        {
            return agent.Name;
        }

        return scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
            ?? scenario.AlphaSnapshot.Players.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
            ?? personId;
    }

    private static int StableNumber(string text, int modulo)
    {
        var hash = 17;
        foreach (var character in text)
        {
            hash = hash * 31 + character;
        }

        return Math.Abs(hash) % Math.Max(1, modulo);
    }

    private static string StableId(string text) =>
        new string(text.ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) ? character : '-').ToArray()).Trim('-');
}
