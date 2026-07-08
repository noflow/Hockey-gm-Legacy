using LegacyEngine.Contracts;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;

namespace LegacyEngine.Integration;

public sealed class PlayerPipelineService
{
    public IReadOnlyList<AffiliateLink> BuildAffiliateLinks(LeagueProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var teamsById = profile.Teams.ToDictionary(team => team.OrganizationId, StringComparer.Ordinal);
        var parentLinks = profile.Teams
            .Where(team => !string.IsNullOrWhiteSpace(team.AffiliateOrganizationId))
            .Select(team =>
            {
                var affiliate = teamsById.GetValueOrDefault(team.AffiliateOrganizationId!);
                return new AffiliateLink(team.OrganizationId, team.TeamName, team.AffiliateOrganizationId!, affiliate?.TeamName ?? team.AffiliateOrganizationId!);
            });
        var affiliateLinks = profile.Teams
            .Where(team => !string.IsNullOrWhiteSpace(team.ParentOrganizationId))
            .Select(team =>
            {
                var parent = teamsById.GetValueOrDefault(team.ParentOrganizationId!);
                return new AffiliateLink(team.ParentOrganizationId!, parent?.TeamName ?? team.ParentOrganizationId!, team.OrganizationId, team.TeamName);
            });
        var links = parentLinks
            .Concat(affiliateLinks)
            .DistinctBy(link => link.AffiliateOrganizationId, StringComparer.Ordinal)
            .ToArray();

        foreach (var link in links)
        {
            link.Validate();
        }

        return links;
    }

    public IReadOnlyList<PlayerPipelineRecord> BuildInitialPipeline(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var links = BuildAffiliateLinks(scenario.LeagueProfile);
        var team = scenario.TeamSelection;
        var parent = ParentFor(scenario);
        var affiliate = AffiliateFor(scenario);
        var records = new List<PlayerPipelineRecord>();

        foreach (var rosterPlayer in scenario.AlphaSnapshot.Roster.Players)
        {
            var name = FindName(scenario, rosterPlayer.PersonId);
            var status = StatusForRoster(scenario, rosterPlayer);
            var history = new List<string>
            {
                $"{scenario.CurrentDate:yyyy-MM-dd}: Listed on {team.TeamName} as {ReadableStatus(status)}."
            };
            if (scenario.LeagueProfile.Experience == LeagueExperience.Ahl && rosterPlayer.AcquisitionSource == PlayerAcquisitionSource.AssignedFromParentClub && parent is not null)
            {
                history.Add($"{scenario.CurrentDate:yyyy-MM-dd}: Assigned from parent club {parent.TeamName}.");
            }

            records.Add(new PlayerPipelineRecord(
                rosterPlayer.PersonId,
                name,
                scenario.Organization.OrganizationId,
                scenario.Organization.Name,
                LevelFor(status),
                RightsHolderOrganizationId: scenario.Organization.OrganizationId,
                RightsHolderTeamName: scenario.Organization.Name,
                ParentOrganization: parent,
                AffiliateOrganization: affiliate,
                PipelineStatus: status,
                AssignmentStatus: AssignmentFor(status),
                AssignmentHistory: history));
        }

        foreach (var prospect in scenario.ProspectRights)
        {
            records.Add(RecordForProspect(scenario, prospect, parent, affiliate, links));
        }

        var deduped = records
            .GroupBy(record => record.PersonId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        foreach (var record in deduped)
        {
            record.Validate();
        }

        return deduped;
    }

    public NewGmScenarioSnapshot EnsurePipeline(NewGmScenarioSnapshot scenario)
    {
        if (scenario.PlayerPipeline.Count > 0)
        {
            return scenario with
            {
                AffiliateLinks = scenario.AffiliateLinks.Count > 0 ? scenario.AffiliateLinks : BuildAffiliateLinks(scenario.LeagueProfile)
            };
        }

        return scenario with
        {
            AffiliateLinks = BuildAffiliateLinks(scenario.LeagueProfile),
            PlayerPipeline = BuildInitialPipeline(scenario)
        };
    }

    public NewGmScenarioSnapshot UpsertProspect(NewGmScenarioSnapshot scenario, DraftRightsRecord prospect, string reason)
    {
        var parent = ParentFor(scenario);
        var affiliate = AffiliateFor(scenario);
        var record = RecordForProspect(scenario, prospect, parent, affiliate, BuildAffiliateLinks(scenario.LeagueProfile));
        var existing = scenario.PlayerPipeline.FirstOrDefault(item => item.PersonId == prospect.ProspectPersonId);
        if (existing is not null)
        {
            record = record with
            {
                AssignmentHistory = existing.AssignmentHistory
                    .Append($"{scenario.CurrentDate:yyyy-MM-dd}: {reason}")
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()
            };
        }

        var next = scenario.PlayerPipeline.Any(item => item.PersonId == record.PersonId)
            ? scenario.PlayerPipeline.Select(item => item.PersonId == record.PersonId ? record : item).ToArray()
            : scenario.PlayerPipeline.Append(record).ToArray();

        return scenario with
        {
            AffiliateLinks = scenario.AffiliateLinks.Count == 0 ? BuildAffiliateLinks(scenario.LeagueProfile) : scenario.AffiliateLinks,
            PlayerPipeline = next
        };
    }

    public PlayerAssignmentEligibility EvaluateAssignment(NewGmScenarioSnapshot scenario, DraftRightsRecord prospect, Rulebook? rulebook = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(prospect);
        var resolvedRulebook = rulebook ?? scenario.LeagueProfile.Rulebook ?? RulebookPresets.CreateNhlStyle();
        var rule = RuleFor(resolvedRulebook);
        var enriched = EnrichProspectFromBoard(scenario, prospect);
        var level = DevelopmentLevelFor(enriched, scenario);
        var isChlProtected = IsChlProtected(enriched, scenario);
        var isSigned = IsSignedProspect(scenario, enriched);
        var nhlGames = NhlGamesFor(scenario, enriched.ProspectPersonId);
        var juniorEligible = enriched.Age <= rule.JuniorAgeCutoff && (level == PlayerDevelopmentLevel.Junior || isChlProtected);
        var warnings = new List<string>();
        var invalid = new List<string>();
        var hasAffiliate = !string.IsNullOrWhiteSpace(scenario.Organization.AffiliateOrganizationId);

        if (!hasAffiliate)
        {
            invalid.Add("Cannot assign to AHL: this organization has no affiliate configured.");
        }

        if (!isSigned)
        {
            invalid.Add("Cannot assign to AHL: player must be signed first.");
        }

        var ageEligible = enriched.Age >= rule.AhlEligibilityAge;
        var exceptionEligible = enriched.Age == 19 && isChlProtected && rule.OneNineteenYearOldChlExceptionEnabled;
        var nonChlEarlyEligible = !isChlProtected
            && level is PlayerDevelopmentLevel.Europe or PlayerDevelopmentLevel.College
            && enriched.Age >= 18
            && rule.EuropeanAndCollegeProspectsCanPlayAhlAt18;

        if (rule.ChlToAhlRestrictionEnabled && isChlProtected && juniorEligible && !exceptionEligible)
        {
            invalid.Add($"Cannot assign to AHL: player is {enriched.Age} and still CHL/junior eligible.");
        }
        else if (!ageEligible && !exceptionEligible && !nonChlEarlyEligible)
        {
            invalid.Add($"Cannot assign to AHL: player is {enriched.Age}; rulebook AHL eligibility starts at {rule.AhlEligibilityAge}.");
        }

        var ahlEligible = isSigned && hasAffiliate && invalid.All(reason => !reason.Contains("AHL", StringComparison.OrdinalIgnoreCase));
        var slideEligible = isSigned && enriched.Age <= rule.ElcSlideAgeCutoff;
        var slideCanBeUsed = slideEligible && nhlGames < rule.ElcSlideNhlGameThreshold;
        if (slideEligible && !slideCanBeUsed)
        {
            warnings.Add($"ELC slide unavailable: {nhlGames} NHL games reaches the {rule.ElcSlideNhlGameThreshold}-game threshold.");
        }

        var recommendation = RecommendationFor(enriched, level, isSigned, juniorEligible, ahlEligible, slideCanBeUsed);
        var eligibility = new PlayerAssignmentEligibility(
            enriched.ProspectPersonId,
            isSigned,
            juniorEligible,
            ahlEligible,
            isSigned,
            isChlProtected,
            slideEligible,
            slideCanBeUsed,
            SlideHasBeenRecorded(scenario, enriched.ProspectPersonId),
            nhlGames,
            recommendation,
            invalid.Distinct(StringComparer.Ordinal).ToArray(),
            warnings.ToArray());
        eligibility.Validate();
        return eligibility;
    }

    public ProspectAssignmentResult ApplyAssignmentDecision(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        ProspectAssignmentDecision decision)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        decision.Validate();
        scenario.Validate();

        var prospect = scenario.ProspectRights.SingleOrDefault(item => item.ProspectPersonId == decision.ProspectPersonId)
            ?? throw new ArgumentException("Drafted prospect was not found.", nameof(decision));
        var eligibility = EvaluateAssignment(scenario, prospect, registry.Rulebook ?? scenario.LeagueProfile.Rulebook);
        var validation = ValidatePipelineDecision(decision.DecisionType, eligibility);
        if (validation is not null)
        {
            return AssignmentResult(false, scenario, PipelineFor(scenario, prospect.ProspectPersonId), eligibility, validation);
        }

        var nextStatus = decision.DecisionType switch
        {
            ProspectDecisionType.ReturnToJunior => ProspectStatus.ReturnedToJunior,
            ProspectDecisionType.ReturnToYouthTeam => ProspectStatus.ReturnedToYouthTeam,
            ProspectDecisionType.AssignToAffiliate => ProspectStatus.AssignedToAffiliate,
            ProspectDecisionType.KeepOnNhlRoster => ProspectStatus.Signed,
            ProspectDecisionType.ReleaseRights => ProspectStatus.Released,
            _ => prospect.Status
        };
        var updatedProspect = prospect with { Status = nextStatus };
        var next = scenario with
        {
            ProspectRights = scenario.ProspectRights
                .Select(item => item.ProspectPersonId == prospect.ProspectPersonId ? updatedProspect : item)
                .ToArray()
        };
        next = UpsertProspect(next, updatedProspect, $"{updatedProspect.ProspectName} assignment decision: {decision.DecisionType}.");
        next = RecordAssignmentTimeline(next, updatedProspect, decision.DecisionType);
        var record = PipelineFor(next, prospect.ProspectPersonId);
        return AssignmentResult(true, next, record, EvaluateAssignment(next, updatedProspect, registry.Rulebook ?? next.LeagueProfile.Rulebook), $"{updatedProspect.ProspectName}: {decision.DecisionType} recorded.");
    }

    public IReadOnlyList<PlayerPipelineRecord> AhlAffiliateRosterForNhlTeam(NewGmScenarioSnapshot scenario)
    {
        var affiliateId = scenario.Organization.AffiliateOrganizationId;
        if (string.IsNullOrWhiteSpace(affiliateId))
        {
            return Array.Empty<PlayerPipelineRecord>();
        }

        return scenario.PlayerPipeline
            .Where(record => string.Equals(record.CurrentOrganizationId, affiliateId, StringComparison.Ordinal)
                || record.PipelineStatus is PlayerPipelineStatus.AssignedToAhl or PlayerPipelineStatus.AhlRoster)
            .OrderBy(record => record.PlayerName, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<PlayerPipelineRecord> ParentClubProspectsForAhlTeam(NewGmScenarioSnapshot scenario)
    {
        var parentId = scenario.Organization.ParentOrganizationId;
        if (string.IsNullOrWhiteSpace(parentId))
        {
            return Array.Empty<PlayerPipelineRecord>();
        }

        return scenario.PlayerPipeline
            .Where(record => string.Equals(record.ParentOrganization?.OrganizationId, parentId, StringComparison.Ordinal)
                || string.Equals(record.RightsHolderOrganizationId, parentId, StringComparison.Ordinal)
                || record.PipelineStatus is PlayerPipelineStatus.AssignedToAhl or PlayerPipelineStatus.AhlRoster)
            .OrderBy(record => record.PlayerName, StringComparer.Ordinal)
            .ToArray();
    }

    private static PlayerPipelineRecord RecordForProspect(
        NewGmScenarioSnapshot scenario,
        DraftRightsRecord prospect,
        ParentOrganizationReference? parent,
        AffiliateOrganizationReference? affiliate,
        IReadOnlyList<AffiliateLink> links)
    {
        var status = prospect.Status switch
        {
            ProspectStatus.ReturnedToJunior or ProspectStatus.ReturnedToYouthTeam => PlayerPipelineStatus.ReturnedToJunior,
            ProspectStatus.AssignedToAffiliate => PlayerPipelineStatus.AssignedToAhl,
            ProspectStatus.Released or ProspectStatus.Declined => PlayerPipelineStatus.Released,
            ProspectStatus.Signed when scenario.LeagueProfile.Experience == LeagueExperience.Nhl => PlayerPipelineStatus.SignedProspect,
            ProspectStatus.ContractOffered => PlayerPipelineStatus.UnsignedProspect,
            ProspectStatus.DraftRightsHeld => PlayerPipelineStatus.DraftedRightsHeld,
            _ => scenario.LeagueProfile.Experience == LeagueExperience.Junior ? PlayerPipelineStatus.JuniorRights : PlayerPipelineStatus.DraftRights
        };
        var enriched = EnrichProspectFromBoard(scenario, prospect);
        var eligibility = new PlayerPipelineService().EvaluateAssignment(scenario, enriched, scenario.LeagueProfile.Rulebook);
        var level = DevelopmentLevelFor(enriched, scenario);
        var currentTeam = status == PlayerPipelineStatus.AssignedToAhl && affiliate is not null
            ? affiliate.TeamName
            : status == PlayerPipelineStatus.ReturnedToJunior && !string.IsNullOrWhiteSpace(enriched.CurrentTeam)
                ? enriched.CurrentTeam
            : scenario.Organization.Name;
        var currentOrg = status == PlayerPipelineStatus.AssignedToAhl && affiliate is not null
            ? affiliate.OrganizationId
            : scenario.Organization.OrganizationId;
        var history = new List<string>
        {
            $"{scenario.CurrentDate:yyyy-MM-dd}: {ReadableStatus(status)} for {scenario.Organization.Name}."
        };
        var profileLink = links.FirstOrDefault(link => link.ParentOrganizationId == scenario.Organization.OrganizationId);
        var linkedAffiliate = affiliate ?? (profileLink is null
            ? null
            : new AffiliateOrganizationReference(profileLink.AffiliateOrganizationId, profileLink.AffiliateTeamName));

        return new PlayerPipelineRecord(
            prospect.ProspectPersonId,
            prospect.ProspectName,
            currentOrg,
            currentTeam,
            LevelFor(status),
            RightsHolderOrganizationId: status == PlayerPipelineStatus.Released ? null : scenario.Organization.OrganizationId,
            RightsHolderTeamName: status == PlayerPipelineStatus.Released ? null : scenario.Organization.Name,
            ParentOrganization: parent,
            AffiliateOrganization: linkedAffiliate,
            PipelineStatus: status,
            AssignmentStatus: AssignmentFor(status),
            AssignmentHistory: history,
            DevelopmentLevel: level,
            RightsStatus: RightsFor(status, prospect.Status),
            IsSigned: eligibility.IsSigned,
            IsAhlEligible: eligibility.IsAhlEligible,
            IsJuniorEligible: eligibility.IsJuniorEligible,
            IsContractSlideEligible: eligibility.IsSlideEligible,
            IsContractSlideUsed: eligibility.SlideUsed,
            NhlGamesTowardSlideThreshold: eligibility.NhlGamesTowardSlideThreshold,
            ContractSlideSummary: SlideSummary(eligibility, RuleFor(scenario.LeagueProfile.Rulebook)),
            RecommendedAssignment: eligibility.RecommendedAssignment,
            StaffRecommendation: StaffRecommendationFor(eligibility, enriched));
    }

    private static PlayerPipelineStatus StatusForRoster(NewGmScenarioSnapshot scenario, RosterPlayer player)
    {
        if (player.Status == RosterStatus.Released)
        {
            return PlayerPipelineStatus.Released;
        }

        return scenario.LeagueProfile.Experience switch
        {
            LeagueExperience.Nhl => PlayerPipelineStatus.NhlRoster,
            LeagueExperience.Ahl => PlayerPipelineStatus.AhlRoster,
            LeagueExperience.Junior => PlayerPipelineStatus.JuniorRights,
            _ => PlayerPipelineStatus.DraftRights
        };
    }

    private static ParentOrganizationReference? ParentFor(NewGmScenarioSnapshot scenario)
    {
        if (string.IsNullOrWhiteSpace(scenario.Organization.ParentOrganizationId))
        {
            return null;
        }

        var team = scenario.LeagueProfile.Teams.FirstOrDefault(team => team.OrganizationId == scenario.Organization.ParentOrganizationId);
        return new ParentOrganizationReference(scenario.Organization.ParentOrganizationId, team?.TeamName ?? scenario.Organization.ParentOrganizationId);
    }

    private static AffiliateOrganizationReference? AffiliateFor(NewGmScenarioSnapshot scenario)
    {
        if (string.IsNullOrWhiteSpace(scenario.Organization.AffiliateOrganizationId))
        {
            return null;
        }

        var team = scenario.LeagueProfile.Teams.FirstOrDefault(team => team.OrganizationId == scenario.Organization.AffiliateOrganizationId);
        return new AffiliateOrganizationReference(scenario.Organization.AffiliateOrganizationId, team?.TeamName ?? scenario.Organization.AffiliateOrganizationId);
    }

    private static PlayerAssignmentStatus AssignmentFor(PlayerPipelineStatus status) =>
        status switch
        {
            PlayerPipelineStatus.NhlRoster => PlayerAssignmentStatus.NhlRoster,
            PlayerPipelineStatus.AhlRoster => PlayerAssignmentStatus.AhlRoster,
            PlayerPipelineStatus.DraftEligible => PlayerAssignmentStatus.DraftEligible,
            PlayerPipelineStatus.DraftedRightsHeld => PlayerAssignmentStatus.DraftedRightsHeld,
            PlayerPipelineStatus.UnsignedProspect => PlayerAssignmentStatus.UnsignedProspect,
            PlayerPipelineStatus.SignedProspect => PlayerAssignmentStatus.SignedProspect,
            PlayerPipelineStatus.JuniorRights => PlayerAssignmentStatus.JuniorRights,
            PlayerPipelineStatus.DraftRights => PlayerAssignmentStatus.DraftRights,
            PlayerPipelineStatus.ReturnedToJunior => PlayerAssignmentStatus.ReturnedToJunior,
            PlayerPipelineStatus.AssignedToAhl => PlayerAssignmentStatus.AssignedToAhl,
            PlayerPipelineStatus.CalledUp => PlayerAssignmentStatus.CalledUp,
            PlayerPipelineStatus.SentDown => PlayerAssignmentStatus.SentDown,
            PlayerPipelineStatus.FreeAgent => PlayerAssignmentStatus.FreeAgent,
            PlayerPipelineStatus.Released => PlayerAssignmentStatus.Released,
            _ => PlayerAssignmentStatus.None
        };

    private static string LevelFor(PlayerPipelineStatus status) =>
        status switch
        {
            PlayerPipelineStatus.NhlRoster or PlayerPipelineStatus.CalledUp => "NHL",
            PlayerPipelineStatus.AhlRoster or PlayerPipelineStatus.AssignedToAhl or PlayerPipelineStatus.SentDown => "AHL",
            PlayerPipelineStatus.JuniorRights or PlayerPipelineStatus.ReturnedToJunior => "Junior",
            PlayerPipelineStatus.DraftEligible => "Draft Eligible",
            PlayerPipelineStatus.DraftedRightsHeld or PlayerPipelineStatus.UnsignedProspect or PlayerPipelineStatus.SignedProspect or PlayerPipelineStatus.DraftRights => "Prospect Rights",
            PlayerPipelineStatus.FreeAgent => "Free Agent",
            PlayerPipelineStatus.Released => "Released",
            _ => "Tracked"
        };

    private static string ReadableStatus(PlayerPipelineStatus status) =>
        status switch
        {
            PlayerPipelineStatus.NhlRoster => "NHL Roster",
            PlayerPipelineStatus.AhlRoster => "AHL Roster",
            PlayerPipelineStatus.DraftEligible => "Draft Eligible",
            PlayerPipelineStatus.DraftedRightsHeld => "Drafted Rights Held",
            PlayerPipelineStatus.UnsignedProspect => "Unsigned Prospect",
            PlayerPipelineStatus.SignedProspect => "Signed Prospect",
            PlayerPipelineStatus.JuniorRights => "Junior Rights",
            PlayerPipelineStatus.DraftRights => "Draft Rights",
            PlayerPipelineStatus.ReturnedToJunior => "Returned to Junior",
            PlayerPipelineStatus.AssignedToAhl => "Assigned to AHL",
            PlayerPipelineStatus.CalledUp => "Called Up",
            PlayerPipelineStatus.SentDown => "Sent Down",
            PlayerPipelineStatus.FreeAgent => "Free Agent",
            PlayerPipelineStatus.Released => "Released",
            _ => status.ToString()
        };

    private static string FindName(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? scenario.ProspectRights.FirstOrDefault(prospect => prospect.ProspectPersonId == personId)?.ProspectName
        ?? personId;

    private static PlayerAssignmentRule RuleFor(Rulebook? rulebook)
    {
        var source = rulebook?.PlayerAssignmentRules;
        var rule = new PlayerAssignmentRule(
            source?.JuniorAgeCutoff ?? 19,
            source?.AhlEligibilityAge ?? 20,
            source?.ChlToAhlRestrictionEnabled ?? true,
            source?.OneNineteenYearOldChlExceptionEnabled ?? false,
            source?.EuropeanAndCollegeProspectsCanPlayAhlAt18 ?? true,
            source?.ElcSlideAgeCutoff ?? 19,
            source?.ElcSlideNhlGameThreshold ?? 10);
        rule.Validate();
        return rule;
    }

    private static DraftRightsRecord EnrichProspectFromBoard(NewGmScenarioSnapshot scenario, DraftRightsRecord prospect)
    {
        var entry = scenario.AlphaSnapshot.DraftBoard.Entries.FirstOrDefault(item => item.ProspectPersonId == prospect.ProspectPersonId);
        var bio = entry?.Bio;
        var level = prospect.DevelopmentLevel != PlayerDevelopmentLevel.Junior || bio is null
            ? prospect.DevelopmentLevel
            : DevelopmentLevelForLeague(bio.League);
        return prospect with
        {
            Position = prospect.Position == RosterPosition.Unknown && bio is not null ? bio.Position : prospect.Position,
            DevelopmentLevel = level,
            CurrentTeam = string.IsNullOrWhiteSpace(prospect.CurrentTeam) ? bio?.CurrentTeam ?? string.Empty : prospect.CurrentTeam,
            CurrentLeague = string.IsNullOrWhiteSpace(prospect.CurrentLeague) ? bio?.League ?? string.Empty : prospect.CurrentLeague,
            IsChlProtected = prospect.IsChlProtected
                || (string.IsNullOrWhiteSpace(prospect.CurrentLeague) && IsChlProtectedLeague(bio?.League, bio?.Country))
        };
    }

    private static PlayerDevelopmentLevel DevelopmentLevelFor(DraftRightsRecord prospect, NewGmScenarioSnapshot scenario) =>
        prospect.DevelopmentLevel != PlayerDevelopmentLevel.Junior
            ? prospect.DevelopmentLevel
            : DevelopmentLevelForLeague(scenario.AlphaSnapshot.DraftBoard.Entries.FirstOrDefault(item => item.ProspectPersonId == prospect.ProspectPersonId)?.Bio?.League ?? prospect.CurrentLeague);

    private static PlayerDevelopmentLevel DevelopmentLevelForLeague(string? league)
    {
        if (string.IsNullOrWhiteSpace(league))
        {
            return PlayerDevelopmentLevel.Junior;
        }

        if (league.Contains("NCAA", StringComparison.OrdinalIgnoreCase) || league.Contains("College", StringComparison.OrdinalIgnoreCase))
        {
            return PlayerDevelopmentLevel.College;
        }

        if (league.Contains("SM-sarja", StringComparison.OrdinalIgnoreCase)
            || league.Contains("Nationell", StringComparison.OrdinalIgnoreCase)
            || league.Contains("Czech", StringComparison.OrdinalIgnoreCase)
            || league.Contains("U20-Elit", StringComparison.OrdinalIgnoreCase))
        {
            return PlayerDevelopmentLevel.Europe;
        }

        if (league.Contains("AHL", StringComparison.OrdinalIgnoreCase))
        {
            return PlayerDevelopmentLevel.Ahl;
        }

        if (league.Contains("NHL", StringComparison.OrdinalIgnoreCase))
        {
            return PlayerDevelopmentLevel.Nhl;
        }

        return PlayerDevelopmentLevel.Junior;
    }

    private static bool IsChlProtected(DraftRightsRecord prospect, NewGmScenarioSnapshot scenario)
    {
        var bio = scenario.AlphaSnapshot.DraftBoard.Entries.FirstOrDefault(item => item.ProspectPersonId == prospect.ProspectPersonId)?.Bio;
        if (!string.IsNullOrWhiteSpace(prospect.CurrentLeague))
        {
            return prospect.IsChlProtected || IsChlProtectedLeague(prospect.CurrentLeague, bio?.Country);
        }

        return prospect.IsChlProtected || IsChlProtectedLeague(bio?.League, bio?.Country);
    }

    private static bool IsChlProtectedLeague(string? league, string? country) =>
        !string.IsNullOrWhiteSpace(league)
        && (league.Contains("CHL", StringComparison.OrdinalIgnoreCase)
            || league.Contains("WHL", StringComparison.OrdinalIgnoreCase)
            || league.Contains("OHL", StringComparison.OrdinalIgnoreCase)
            || league.Contains("QMJHL", StringComparison.OrdinalIgnoreCase)
            || league.Contains("CSSHL", StringComparison.OrdinalIgnoreCase)
            || league.Contains("SMAAAHL", StringComparison.OrdinalIgnoreCase)
            || league.Contains("AEHL", StringComparison.OrdinalIgnoreCase)
            || (string.Equals(country, "Canada", StringComparison.OrdinalIgnoreCase) && league.Contains("U18", StringComparison.OrdinalIgnoreCase)));

    private static bool IsSignedProspect(NewGmScenarioSnapshot scenario, DraftRightsRecord prospect) =>
        prospect.Status == ProspectStatus.Signed
        || scenario.Contracts.Concat(scenario.AlphaSnapshot.Contracts).Any(contract =>
            contract.PersonId == prospect.ProspectPersonId
            && contract.Status == ContractStatus.Signed);

    private static int NhlGamesFor(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.PlayerStats.FirstOrDefault(stat => stat.PersonId == personId)?.GamesPlayed
        ?? scenario.GoalieStats.FirstOrDefault(stat => stat.PersonId == personId)?.GamesPlayed
        ?? 0;

    private static bool SlideHasBeenRecorded(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.CareerTimeline.ForPerson(personId).Any(entry => entry.Title.Contains("Contract slid", StringComparison.OrdinalIgnoreCase));

    private static string RecommendationFor(DraftRightsRecord prospect, PlayerDevelopmentLevel level, bool signed, bool juniorEligible, bool ahlEligible, bool slideCanBeUsed)
    {
        if (!signed)
        {
            return "Hold rights or negotiate an entry-level contract before pro assignment.";
        }

        if (ahlEligible)
        {
            return "AHL assignment is valid if the GM wants pro development minutes.";
        }

        if (juniorEligible)
        {
            return slideCanBeUsed
                ? "Return to junior unless the player earns NHL games; ELC slide remains available."
                : "Return to junior or keep on NHL roster; monitor the ELC slide threshold.";
        }

        return level == PlayerDevelopmentLevel.Europe
            ? "European pathway is valid; review AHL/NHL readiness with scouting staff."
            : "Review NHL roster readiness before assignment.";
    }

    private static string SlideSummary(PlayerAssignmentEligibility eligibility, PlayerAssignmentRule rule)
    {
        if (!eligibility.IsSigned)
        {
            return "Unsigned: no ELC slide status yet.";
        }

        if (!eligibility.IsSlideEligible)
        {
            return "Contract active; not slide eligible by age.";
        }

        return eligibility.SlideCanBeUsed
            ? $"Contract active; slide eligible ({eligibility.NhlGamesTowardSlideThreshold}/{rule.ElcSlideNhlGameThreshold} NHL games)."
            : $"Contract active; slide blocked ({eligibility.NhlGamesTowardSlideThreshold}/{rule.ElcSlideNhlGameThreshold} NHL games).";
    }

    private static string StaffRecommendationFor(PlayerAssignmentEligibility eligibility, DraftRightsRecord prospect)
    {
        if (eligibility.IsAhlEligible)
        {
            return $"{prospect.ProspectName} can be assigned to the affiliate if development minutes are available.";
        }

        if (eligibility.IsJuniorEligible)
        {
            return $"{prospect.ProspectName} should likely return to junior/youth unless he wins an NHL roster spot.";
        }

        return eligibility.RecommendedAssignment;
    }

    private static PlayerRightsStatus RightsFor(PlayerPipelineStatus pipelineStatus, ProspectStatus prospectStatus) =>
        pipelineStatus switch
        {
            PlayerPipelineStatus.Released => PlayerRightsStatus.Released,
            PlayerPipelineStatus.FreeAgent => PlayerRightsStatus.FreeAgent,
            PlayerPipelineStatus.SignedProspect or PlayerPipelineStatus.NhlRoster or PlayerPipelineStatus.AhlRoster or PlayerPipelineStatus.AssignedToAhl => PlayerRightsStatus.SignedProspect,
            PlayerPipelineStatus.DraftEligible => PlayerRightsStatus.DraftEligible,
            _ => prospectStatus == ProspectStatus.Signed ? PlayerRightsStatus.SignedProspect : PlayerRightsStatus.UnsignedProspect
        };

    private static string? ValidatePipelineDecision(ProspectDecisionType decisionType, PlayerAssignmentEligibility eligibility) =>
        decisionType switch
        {
            ProspectDecisionType.AssignToAffiliate when !eligibility.IsAhlEligible => eligibility.InvalidReasons.FirstOrDefault(reason => reason.Contains("AHL", StringComparison.OrdinalIgnoreCase)) ?? "Cannot assign to AHL under current rulebook.",
            ProspectDecisionType.KeepOnNhlRoster when !eligibility.IsSigned => "Cannot keep on NHL roster: player must be signed first.",
            ProspectDecisionType.ReturnToJunior when !eligibility.IsJuniorEligible => "Cannot return to junior: player is no longer junior eligible.",
            _ => null
        };

    private static PlayerPipelineRecord? PipelineFor(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.PlayerPipeline.FirstOrDefault(record => record.PersonId == personId);

    private static ProspectAssignmentResult AssignmentResult(bool success, NewGmScenarioSnapshot scenario, PlayerPipelineRecord? record, PlayerAssignmentEligibility eligibility, string message)
    {
        var result = new ProspectAssignmentResult(success, scenario, record, eligibility, message);
        result.Validate();
        return result;
    }

    private static NewGmScenarioSnapshot RecordAssignmentTimeline(NewGmScenarioSnapshot scenario, DraftRightsRecord prospect, ProspectDecisionType decisionType)
    {
        var entryType = decisionType switch
        {
            ProspectDecisionType.AssignToAffiliate => CareerTimelineEntryType.Assigned,
            ProspectDecisionType.ReturnToJunior or ProspectDecisionType.ReturnToYouthTeam => CareerTimelineEntryType.ReturnedToJunior,
            ProspectDecisionType.KeepOnNhlRoster => CareerTimelineEntryType.Debut,
            ProspectDecisionType.ReleaseRights => CareerTimelineEntryType.Released,
            _ => CareerTimelineEntryType.Assigned
        };
        var title = decisionType switch
        {
            ProspectDecisionType.AssignToAffiliate => "Assigned to AHL",
            ProspectDecisionType.ReturnToJunior => "Returned to junior",
            ProspectDecisionType.ReturnToYouthTeam => "Returned to youth team",
            ProspectDecisionType.KeepOnNhlRoster => "Made NHL roster",
            ProspectDecisionType.ReleaseRights => "Rights released",
            _ => $"Assignment: {decisionType}"
        };
        var timeline = scenario.CareerTimeline.Add(new CareerTimelineEntry(
            $"career:pipeline:{decisionType}:{prospect.ProspectPersonId}:{scenario.CurrentDate:yyyyMMdd}",
            entryType,
            scenario.CurrentDate,
            scenario.Season.Year,
            prospect.ProspectPersonId,
            scenario.Organization.OrganizationId,
            scenario.Organization.Name,
            title,
            $"{prospect.ProspectName}: {title} by {scenario.Organization.Name}.",
            null,
            HistoryImportance.Important));
        return scenario with { CareerTimeline = timeline };
    }
}
