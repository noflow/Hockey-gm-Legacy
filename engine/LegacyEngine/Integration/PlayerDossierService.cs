using LegacyEngine.Contracts;
using LegacyEngine.Development;
using LegacyEngine.Injuries;
using LegacyEngine.People;
using LegacyEngine.Relationships;
using LegacyEngine.Rosters;
using LegacyEngine.Scouting;

namespace LegacyEngine.Integration;

public sealed class PlayerDossierService
{
    public PlayerDossierView CreateDossier(
        NewGmScenarioSnapshot scenario,
        string personId,
        PlayerDossierSource source = PlayerDossierSource.Unknown)
    {
        scenario.Validate();

        if (string.IsNullOrWhiteSpace(personId))
        {
            throw new ArgumentException("Dossier person id is required.", nameof(personId));
        }

        var person = FindPerson(scenario, personId)
            ?? throw new ArgumentException("Person was not found for dossier.", nameof(personId));
        var resolvedSource = source == PlayerDossierSource.Unknown ? ResolveSource(scenario, personId) : source;
        var position = ResolvePosition(scenario, personId);
        var status = ResolveStatus(scenario, personId);
        var teamOrRights = ResolveTeamOrRights(scenario, personId);
        var notes = ResolveGmNotes(scenario, personId);

        var sections = new[]
        {
            BuildOverview(scenario, person, position, status, teamOrRights, resolvedSource),
            BuildFacts(scenario, person, resolvedSource),
            BuildScoutingReports(scenario, personId),
            BuildDevelopment(scenario, personId),
            BuildMedical(scenario, personId),
            BuildContractRights(scenario, personId),
            BuildStaffOpinions(scenario, personId),
            BuildRelationships(scenario, personId),
            new PlayerDossierSection("GM Notes", string.IsNullOrWhiteSpace(notes) ? new[] { "No GM notes yet." } : new[] { notes })
        };

        var dossier = new PlayerDossierView(
            PersonId: personId,
            PlayerName: person.Identity.DisplayName,
            Age: person.CalculateAge(scenario.CurrentDate),
            Position: position,
            Status: status,
            TeamOrRights: teamOrRights,
            Source: resolvedSource,
            Sections: sections,
            GmNotes: notes);

        dossier.Validate();
        return dossier;
    }

    public PlayerDossierResult AddOrUpdateGmNote(NewGmScenarioSnapshot scenario, string personId, string note)
    {
        scenario.Validate();

        if (string.IsNullOrWhiteSpace(note))
        {
            throw new ArgumentException("GM dossier note is required.", nameof(note));
        }

        if (FindPerson(scenario, personId) is null)
        {
            throw new ArgumentException("Person was not found for dossier note.", nameof(personId));
        }

        var notes = new Dictionary<string, string>(scenario.PlayerDossierNotes, StringComparer.Ordinal)
        {
            [personId] = note.Trim()
        };
        var updated = scenario with { PlayerDossierNotes = notes };
        var dossier = CreateDossier(updated, personId);

        var result = new PlayerDossierResult(updated, dossier, $"GM note saved for {dossier.PlayerName}.");
        result.Validate();
        return result;
    }

    private static PlayerDossierSection BuildOverview(
        NewGmScenarioSnapshot scenario,
        Person person,
        RosterPosition position,
        string status,
        string teamOrRights,
        PlayerDossierSource source)
    {
        var lines = new List<string>
        {
            $"Name: {person.Identity.DisplayName}",
            $"Age: {person.CalculateAge(scenario.CurrentDate)}",
            $"Position: {position}",
            $"Status: {status}",
            $"Team/Rights: {teamOrRights}",
            $"Source: {source}"
        };

        var boardEntry = scenario.AlphaSnapshot.DraftBoard.Entries.SingleOrDefault(entry => entry.ProspectPersonId == person.PersonId);
        if (boardEntry?.Bio is not null)
        {
            lines.Add($"Shoots/Catches: {boardEntry.Bio.ShootsCatches}");
            lines.Add($"Height/Weight: {boardEntry.Bio.HeightDisplay}, {boardEntry.Bio.WeightDisplay}");
            lines.Add($"Current team/league: {boardEntry.Bio.CurrentTeam} / {boardEntry.Bio.League}");
            lines.Add($"Projection: {boardEntry.ProjectionText}");
            lines.Add($"Scouting confidence: {boardEntry.ScoutingConfidence?.ToString() ?? "Unknown"}");
        }

        return new PlayerDossierSection("Overview", lines);
    }

    private static PlayerDossierSection BuildFacts(NewGmScenarioSnapshot scenario, Person person, PlayerDossierSource source)
    {
        var roles = person.ActiveRolesOn(scenario.CurrentDate)
            .Select(role => $"{role.Title} ({role.RoleType})")
            .DefaultIfEmpty("No active role")
            .ToArray();

        return new PlayerDossierSection(
            "Facts",
            new[]
            {
                $"Nationality: {person.Identity.Nationality}",
                $"Birthplace: {person.Identity.Birthplace}",
                $"Birth date: {person.Identity.BirthDate:yyyy-MM-dd}",
                $"Person status: {person.Status}",
                $"Active roles: {string.Join(", ", roles)}",
                $"Dossier opened from: {source}"
            });
    }

    private static PlayerDossierSection BuildScoutingReports(NewGmScenarioSnapshot scenario, string personId)
    {
        var lines = new List<string>();
        var boardEntry = scenario.AlphaSnapshot.DraftBoard.Entries.SingleOrDefault(entry => entry.ProspectPersonId == personId);
        if (boardEntry is not null)
        {
            lines.Add($"Draft board rank: {boardEntry.Rank}");
            lines.Add($"Projection: {boardEntry.ProjectionText}");
            lines.Add($"Confidence: {boardEntry.ScoutingConfidence?.ToString() ?? "Unknown"}");
            if (!string.IsNullOrWhiteSpace(boardEntry.AnalyticsSummary))
            {
                lines.Add($"Evidence: {boardEntry.AnalyticsSummary}");
            }
        }

        foreach (var report in scenario.CompletedScoutingReports.Where(report => report.PlayerId == personId).OrderByDescending(report => report.CreatedOn))
        {
            lines.Add($"Report {report.ReportId} ({report.CreatedOn:yyyy-MM-dd}) - {report.Confidence} confidence - {report.Recommendation}");
            lines.AddRange(report.Facts.Select(fact => $"Fact: {fact}"));
            lines.AddRange(report.Observations.Take(2).Select(observation => $"Observation: {observation}"));
            lines.AddRange(report.Opinions.Take(2).Select(opinion => $"Opinion: {opinion}"));
            lines.AddRange(report.Unknowns.Take(2).Select(unknown => $"Unknown: {unknown}"));
        }

        if (lines.Count == 0)
        {
            lines.Add("No scouting report has been completed yet.");
        }

        return new PlayerDossierSection("Scouting Reports", lines);
    }

    private static PlayerDossierSection BuildDevelopment(NewGmScenarioSnapshot scenario, string personId)
    {
        var profile = scenario.AlphaSnapshot.DevelopmentProfiles.SingleOrDefault(profile => profile.PersonId == personId);
        if (profile is null)
        {
            return new PlayerDossierSection("Development", new[] { "No player development profile is currently tracked." });
        }

        var strongest = profile.Traits.OrderByDescending(trait => trait.Value).FirstOrDefault()?.Attribute.ToString() ?? "Unknown";
        var focus = profile.Traits
            .Where(trait => trait.Attribute is DevelopmentAttribute.WorkEthic or DevelopmentAttribute.Coachability or DevelopmentAttribute.Confidence)
            .OrderByDescending(trait => trait.Value)
            .Select(trait => trait.Attribute.ToString())
            .FirstOrDefault() ?? strongest;

        return new PlayerDossierSection(
            "Development",
            new[]
            {
                $"Stage: {profile.Stage}",
                $"Current development theme: {strongest}",
                $"Staff confidence theme: {focus}",
                $"Last development review: {profile.LastUpdated:yyyy-MM-dd}",
                "Player-facing summary only; internal development values remain private."
            });
    }

    private static PlayerDossierSection BuildMedical(NewGmScenarioSnapshot scenario, string personId)
    {
        var injuries = scenario.AlphaSnapshot.Injuries
            .Where(injury => injury.PersonId == personId)
            .OrderByDescending(injury => injury.InjuryDate)
            .ToArray();
        if (injuries.Length == 0)
        {
            return new PlayerDossierSection("Injuries / Medical", new[] { "No injury record is currently tracked." });
        }

        return new PlayerDossierSection(
            "Injuries / Medical",
            injuries.Select(injury =>
                $"{injury.InjuryDate:yyyy-MM-dd}: {injury.Severity} {injury.InjuryType} ({injury.BodyPart}), {injury.Status}; expected return {injury.ExpectedReturnDate:yyyy-MM-dd}, games missed {injury.GamesMissed}.")
                .ToArray());
    }

    private static PlayerDossierSection BuildContractRights(NewGmScenarioSnapshot scenario, string personId)
    {
        var lines = new List<string>();
        var prospect = scenario.ProspectRights.SingleOrDefault(prospect => prospect.ProspectPersonId == personId);
        if (prospect is not null)
        {
            lines.Add($"Draft rights: {prospect.Status}, round {prospect.RoundNumber}, pick {prospect.PickNumber}.");
            lines.Add($"Rights projection: {prospect.ProjectionText}");
        }

        foreach (var contract in scenario.Contracts.Concat(scenario.AlphaSnapshot.Contracts).Where(contract => contract.PersonId == personId).DistinctBy(contract => contract.ContractId))
        {
            lines.Add($"{contract.ContractType}: {contract.Status}, {contract.Term.StartDate:yyyy-MM-dd} to {contract.Term.EndDate:yyyy-MM-dd}, {contract.Money.Currency} {contract.Money.SalaryOrStipend:0}.");
        }

        foreach (var action in scenario.PendingActions.Where(action => action.PersonId == personId && action.IsOpen))
        {
            lines.Add($"Pending GM action: {action.Title} - {action.RecommendedAction}");
        }

        if (lines.Count == 0)
        {
            lines.Add("No contract, rights, or pending signing decision is currently tracked.");
        }

        return new PlayerDossierSection("Contract / Rights Status", lines);
    }

    private static PlayerDossierSection BuildStaffOpinions(NewGmScenarioSnapshot scenario, string personId)
    {
        var lines = new List<string>();
        var evaluation = scenario.TrainingCamp?.FindEvaluation(personId);
        if (evaluation is not null)
        {
            lines.Add($"Camp readiness: {evaluation.Readiness}");
            lines.Add($"Development upside: {evaluation.DevelopmentUpside}");
            lines.Add($"Coach note: {evaluation.CoachNote}");
            lines.Add($"Scout note: {evaluation.ScoutNote}");
            lines.Add($"Risk note: {evaluation.RiskNote}");
            lines.Add($"Recommendation: {evaluation.Recommendation}");
        }

        foreach (var assignment in scenario.ScoutingOperations.Where(assignment => assignment.TargetPlayerId == personId).OrderByDescending(assignment => assignment.StartDate).Take(3))
        {
            lines.Add($"Scouting operation: {assignment.ScoutName} assigned {assignment.AssignmentType} review, status {assignment.Status}, expected {assignment.ExpectedReportDate:yyyy-MM-dd}.");
        }

        if (lines.Count == 0)
        {
            lines.Add("No staff opinion has been attached yet.");
        }

        return new PlayerDossierSection("Staff Opinions", lines);
    }

    private static PlayerDossierSection BuildRelationships(NewGmScenarioSnapshot scenario, string personId)
    {
        var lines = scenario.AlphaSnapshot.Relationships
            .Where(relationship => relationship.FromPersonId == personId || relationship.ToPersonId == personId)
            .Take(8)
            .Select(relationship =>
            {
                var otherId = relationship.FromPersonId == personId ? relationship.ToPersonId : relationship.FromPersonId;
                return $"{relationship.RelationshipType}: {FindName(scenario, otherId)} - trust {Label(relationship.Trust)}, respect {Label(relationship.Respect)}, confidence {Label(relationship.Confidence)}.";
            })
            .ToArray();

        if (lines.Length == 0)
        {
            lines = new[] { "No relationship notes are currently tracked." };
        }

        return new PlayerDossierSection("Relationships", lines);
    }

    private static PlayerDossierSource ResolveSource(NewGmScenarioSnapshot scenario, string personId)
    {
        if (scenario.AlphaSnapshot.Roster.Players.Any(player => player.PersonId == personId))
        {
            return PlayerDossierSource.Roster;
        }

        if (scenario.ProspectRights.Any(prospect => prospect.ProspectPersonId == personId))
        {
            return PlayerDossierSource.ProspectList;
        }

        if (scenario.TrainingCamp?.Players.Any(player => player.PersonId == personId) == true)
        {
            return PlayerDossierSource.TrainingCamp;
        }

        if (scenario.AlphaSnapshot.Recruits.Any(recruit => recruit.RecruitPersonId == personId))
        {
            return PlayerDossierSource.Recruit;
        }

        if (scenario.AlphaSnapshot.DraftBoard.Entries.Any(entry => entry.ProspectPersonId == personId))
        {
            return PlayerDossierSource.DraftBoard;
        }

        if (scenario.CompletedScoutingReports.Any(report => report.PlayerId == personId))
        {
            return PlayerDossierSource.Scouting;
        }

        return PlayerDossierSource.Unknown;
    }

    private static RosterPosition ResolvePosition(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.Roster.Players.SingleOrDefault(player => player.PersonId == personId)?.Position
        ?? scenario.ProspectRights.SingleOrDefault(prospect => prospect.ProspectPersonId == personId)?.Position
        ?? scenario.TrainingCamp?.FindPlayer(personId)?.Position
        ?? RosterPosition.Unknown;

    private static string ResolveStatus(NewGmScenarioSnapshot scenario, string personId)
    {
        var roster = scenario.AlphaSnapshot.Roster.Players.SingleOrDefault(player => player.PersonId == personId);
        if (roster is not null)
        {
            return $"Roster: {roster.Status}";
        }

        var prospect = scenario.ProspectRights.SingleOrDefault(prospect => prospect.ProspectPersonId == personId);
        if (prospect is not null)
        {
            return $"Prospect rights: {prospect.Status}";
        }

        var camp = scenario.TrainingCamp?.FindPlayer(personId);
        if (camp is not null)
        {
            return $"Training camp: {camp.Status}";
        }

        var recruit = scenario.AlphaSnapshot.Recruits.SingleOrDefault(recruit => recruit.RecruitPersonId == personId);
        if (recruit is not null)
        {
            return $"Recruiting: {recruit.Status}";
        }

        return "Tracked player/prospect";
    }

    private static string ResolveTeamOrRights(NewGmScenarioSnapshot scenario, string personId)
    {
        if (scenario.AlphaSnapshot.Roster.Players.Any(player => player.PersonId == personId))
        {
            return scenario.Organization.Name;
        }

        var prospect = scenario.ProspectRights.SingleOrDefault(prospect => prospect.ProspectPersonId == personId);
        if (prospect is not null)
        {
            return $"Rights held by {scenario.Organization.Name}";
        }

        if (scenario.AlphaSnapshot.Recruits.Any(recruit => recruit.RecruitPersonId == personId))
        {
            return "Recruit pool";
        }

        if (scenario.AlphaSnapshot.DraftBoard.Entries.Any(entry => entry.ProspectPersonId == personId))
        {
            return "Draft board";
        }

        return "Unassigned";
    }

    private static string ResolveGmNotes(NewGmScenarioSnapshot scenario, string personId)
    {
        if (scenario.PlayerDossierNotes.TryGetValue(personId, out var note) && !string.IsNullOrWhiteSpace(note))
        {
            return note;
        }

        var prospectNote = scenario.ProspectRights.SingleOrDefault(prospect => prospect.ProspectPersonId == personId)?.GmNotes;
        if (!string.IsNullOrWhiteSpace(prospectNote))
        {
            return prospectNote;
        }

        var boardNote = scenario.AlphaSnapshot.DraftBoard.Entries.SingleOrDefault(entry => entry.ProspectPersonId == personId)?.PersonalNotes;
        return string.IsNullOrWhiteSpace(boardNote) ? string.Empty : boardNote;
    }

    private static Person? FindPerson(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.SingleOrDefault(person => person.PersonId == personId)
        ?? scenario.AlphaSnapshot.Players.SingleOrDefault(person => person.PersonId == personId);

    private static string FindName(NewGmScenarioSnapshot scenario, string personId) =>
        string.Equals(personId, scenario.AlphaSnapshot.Owner.OwnerId, StringComparison.Ordinal)
            ? scenario.AlphaSnapshot.Owner.Name
            : FindPerson(scenario, personId)?.Identity.DisplayName ?? personId;

    private static string Label(int value) =>
        value >= 70 ? "strong" :
        value >= 45 ? "steady" :
        value >= 30 ? "strained" :
        "fragile";
}
