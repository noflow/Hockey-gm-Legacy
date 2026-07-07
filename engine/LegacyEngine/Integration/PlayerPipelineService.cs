using LegacyEngine.Rosters;

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
            ProspectStatus.Signed when scenario.LeagueProfile.Experience == LeagueExperience.Nhl => PlayerPipelineStatus.DraftRights,
            _ => scenario.LeagueProfile.Experience == LeagueExperience.Junior ? PlayerPipelineStatus.JuniorRights : PlayerPipelineStatus.DraftRights
        };
        var currentTeam = status == PlayerPipelineStatus.AssignedToAhl && affiliate is not null
            ? affiliate.TeamName
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
            AssignmentHistory: history);
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
            PlayerPipelineStatus.DraftRights => "Prospect Rights",
            PlayerPipelineStatus.FreeAgent => "Free Agent",
            PlayerPipelineStatus.Released => "Released",
            _ => "Tracked"
        };

    private static string ReadableStatus(PlayerPipelineStatus status) =>
        status switch
        {
            PlayerPipelineStatus.NhlRoster => "NHL Roster",
            PlayerPipelineStatus.AhlRoster => "AHL Roster",
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
}
