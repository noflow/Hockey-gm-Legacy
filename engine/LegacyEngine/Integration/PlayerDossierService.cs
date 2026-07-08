using LegacyEngine.Contracts;
using LegacyEngine.Development;
using LegacyEngine.Draft;
using LegacyEngine.Injuries;
using LegacyEngine.People;
using LegacyEngine.Relationships;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;
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
            BuildAgentRepresentation(scenario, personId),
            BuildStaffOpinions(scenario, personId),
            BuildRelationships(scenario, personId),
            BuildCareerHistory(scenario, personId),
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
            lines.Add($"Known position: {boardEntry.Bio.Position}");
            lines.Add($"Shoots/Catches: {boardEntry.Bio.ShootsCatches}");
            lines.Add($"Height/Weight: {boardEntry.Bio.HeightDisplay}, {boardEntry.Bio.WeightDisplay}");
            lines.Add($"Current team/league: {boardEntry.Bio.CurrentTeam} / {boardEntry.Bio.League}");
            lines.Add($"Projection: {boardEntry.ProjectionText}");
            lines.Add($"Scouting confidence: {boardEntry.ScoutingConfidence?.ToString() ?? "Unknown"}");
        }

        var freeAgent = scenario.FreeAgentMarket?.Find(person.PersonId);
        if (freeAgent is not null)
        {
            lines.Add($"Free agent status: {freeAgent.Status}");
            lines.Add($"Shoots/Catches: {freeAgent.ShootsCatches}");
            lines.Add($"Height/Weight: {freeAgent.HeightDisplay}, {freeAgent.WeightDisplay}");
            lines.Add($"Previous team: {freeAgent.PreviousTeam}");
            lines.Add($"Projected role: {freeAgent.ProjectedLineupRole}");
            lines.Add($"Contract ask: {freeAgent.ContractAsk.TermYears} year(s), {freeAgent.ContractAsk.AnnualAmount:C0} {freeAgent.ContractAsk.Currency}");
            lines.Add($"Interest: {freeAgent.Interest.PlayerOrganizationInterest}/100");
            lines.Add($"Staff recommendation: {freeAgent.FitSummary.StaffRecommendation}");
        }

        var tradeBlock = scenario.TradeBlock?.Find(person.PersonId);
        if (tradeBlock is not null)
        {
            lines.Add($"Trade block team: {tradeBlock.TeamName}");
            lines.Add($"Trade block role: {tradeBlock.CurrentRole}");
            lines.Add($"Trade block reason: {tradeBlock.ReasonAvailable}");
            lines.Add($"Asking price: {tradeBlock.AskingPriceSummary}");
            lines.Add($"Trade interest: {tradeBlock.InterestLevel}");
            lines.Add($"Budget impact: {tradeBlock.SalaryImpact:C0}");
        }

        var pipeline = scenario.PlayerPipeline.FirstOrDefault(record => record.PersonId == person.PersonId);
        if (pipeline is not null)
        {
            lines.Add($"Current level: {pipeline.CurrentLevel}");
            lines.Add($"Pipeline status: {pipeline.PipelineStatus}");
            lines.Add($"Development path: {pipeline.DevelopmentLevel}");
            lines.Add($"Rights holder: {pipeline.RightsHolderTeamName ?? "none"}");
            lines.Add($"Signed/unsigned status: {(pipeline.IsSigned ? "Signed" : "Unsigned")}");
            lines.Add($"AHL eligibility: {(pipeline.IsAhlEligible ? "Eligible" : "Not eligible")}");
            lines.Add($"Junior eligibility: {(pipeline.IsJuniorEligible ? "Eligible" : "Not eligible")}");
            lines.Add($"Contract slide status: {pipeline.ContractSlideSummary}");
            lines.Add($"Parent club: {pipeline.ParentOrganization?.TeamName ?? "none"}");
            lines.Add($"Affiliate club: {pipeline.AffiliateOrganization?.TeamName ?? "none"}");
            lines.Add($"Recommended assignment: {pipeline.RecommendedAssignment}");
            lines.Add($"Staff recommendation: {pipeline.StaffRecommendation}");
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
        var intelligence = new ScoutingIntelligenceService();
        var boardEntry = scenario.AlphaSnapshot.DraftBoard.Entries.SingleOrDefault(entry => entry.ProspectPersonId == personId);
        if (boardEntry is not null)
        {
            lines.Add($"Draft board rank: {boardEntry.Rank}");
            if (boardEntry.Bio is not null)
            {
                lines.Add($"Current picture: {CurrentScoutingPicture(boardEntry)}");
                lines.Add($"Future picture: {FutureScoutingPicture(boardEntry)}");
            }

            lines.Add($"Projection: {boardEntry.ProjectionText}");
            lines.Add($"Confidence: {boardEntry.ScoutingConfidence?.ToString() ?? "Unknown"}");
            if (!string.IsNullOrWhiteSpace(boardEntry.AnalyticsSummary))
            {
                lines.Add($"Evidence: {boardEntry.AnalyticsSummary}");
            }
        }

        var reports = intelligence.BuildReportCards(scenario, personId, RulebookPresets.CreateJuniorMajor());
        foreach (var report in reports.OrderByDescending(report => report.CreatedOn))
        {
            lines.Add($"{report.Source} - {report.ScoutName} ({report.CreatedOn:yyyy-MM-dd})");
            lines.Add($"Report source: {report.Source} from {report.ScoutName}");
            lines.Add($"Confidence: {report.ConfidenceStars}");
            lines.Add($"Current picture: {report.CurrentPicture}");
            lines.Add($"Future projection: {report.FutureProjection}");
            lines.Add($"Recommendation: {report.Recommendation}");
            lines.AddRange(report.Evidence.Take(3).Select(evidence => $"Evidence: {evidence}"));
            lines.AddRange(report.Concerns.Take(2).Select(concern => $"Concern: {concern}"));
            lines.Add($"Scout tendency: {string.Join(", ", report.ScoutTraits.Take(3))}");
        }

        if (reports.Count > 1)
        {
            var comparison = intelligence.CompareReports(scenario, personId, RulebookPresets.CreateJuniorMajor());
            lines.Add($"Report comparison: {comparison.ConfidenceSummary}");
            lines.Add($"Agreement: {string.Join(" ", comparison.Agreements)}");
            lines.Add($"Disagreement: {string.Join(" ", comparison.Disagreements)}");
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
        var planning = new DevelopmentPlanningService();
        var planSummary = planning.BuildDossierSummary(scenario, personId)
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        return new PlayerDossierSection(
            "Development",
            new[]
            {
                $"Stage: {profile.Stage}",
                $"Current development theme: {strongest}",
                $"Staff confidence theme: {focus}",
                $"Last development review: {profile.LastUpdated:yyyy-MM-dd}",
                "Player-facing summary only; internal development values remain private."
            }.Concat(planSummary).ToArray());
    }

    private static PlayerDossierSection BuildMedical(NewGmScenarioSnapshot scenario, string personId)
    {
        var medicalLines = new MedicalHealthService().BuildDossierMedicalLines(scenario, personId).ToList();
        var injuries = scenario.AlphaSnapshot.Injuries
            .Where(injury => injury.PersonId == personId)
            .OrderByDescending(injury => injury.InjuryDate)
            .ToArray();
        if (injuries.Length == 0)
        {
            return new PlayerDossierSection("Injuries / Medical", medicalLines);
        }

        medicalLines.Add("Injury timeline:");
        medicalLines.AddRange(injuries.Select(injury =>
            $"{injury.InjuryDate:yyyy-MM-dd}: {injury.Severity} {injury.InjuryType} ({injury.BodyPart}), {injury.Status}; expected return {injury.ExpectedReturnDate:yyyy-MM-dd}, games missed {injury.GamesMissed}."));

        return new PlayerDossierSection(
            "Injuries / Medical",
            medicalLines);
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

        var pipeline = scenario.PlayerPipeline.FirstOrDefault(record => record.PersonId == personId);
        if (pipeline is not null)
        {
            lines.Add($"Pipeline rights holder: {pipeline.RightsHolderTeamName ?? "none"}.");
            lines.Add($"Assignment status: {pipeline.AssignmentStatus}.");
            lines.Add($"Signed status: {(pipeline.IsSigned ? "Signed" : "Unsigned")}.");
            lines.Add($"Contract slide: {pipeline.ContractSlideSummary}");
            lines.Add($"AHL eligible: {(pipeline.IsAhlEligible ? "yes" : "no")}.");
            lines.Add($"Junior eligible: {(pipeline.IsJuniorEligible ? "yes" : "no")}.");
            lines.Add($"Recommended assignment: {pipeline.RecommendedAssignment}");
            lines.Add($"Staff recommendation: {pipeline.StaffRecommendation}");
        }

        if (lines.Count == 0)
        {
            lines.Add("No contract, rights, or pending signing decision is currently tracked.");
        }

        return new PlayerDossierSection("Contract / Rights Status", lines);
    }

    private static PlayerDossierSection BuildAgentRepresentation(NewGmScenarioSnapshot scenario, string personId)
    {
        var engine = new AgentEngine();
        var representation = engine.FindRepresentation(scenario, personId);
        if (representation is null)
        {
            return new PlayerDossierSection("Agent / Representation", new[] { "No agent or representation record is currently tracked." });
        }

        var agent = representation.AgentId is null ? null : engine.FindAgent(scenario, representation.AgentId);
        var lines = new List<string>
        {
            $"Representation type: {representation.RepresentationType}",
            $"Agent: {agent?.Name ?? "No formal agent"}",
            $"Agency: {agent?.Profile.AgencyName ?? "Family / advisor only"}",
            $"Representation start: {representation.RepresentationStart:yyyy-MM-dd}",
            $"GM relationship: {(agent is null ? "not applicable" : $"{agent.GmRelationship.Score}/100 - {agent.GmRelationship.Summary}")}",
            $"Organization relationship: {(agent is null ? "not applicable" : $"{agent.OrganizationRelationship.Score}/100 - {agent.OrganizationRelationship.Summary}")}",
            $"Negotiation style: {agent?.NegotiationStyle.ToString() ?? "Informal"}",
            $"Agent reputation: {(agent is null ? "not applicable" : $"{agent.Reputation.Overall}/100 - {agent.Reputation.Summary}")}"
        };

        lines.AddRange(representation.RepresentationHistory.Select(entry => $"Representation history: {entry}"));
        foreach (var history in scenario.AgentHistory.Where(history => history.AgentId == representation.AgentId || history.PersonId == personId).Take(5))
        {
            lines.Add($"Agent history: {history.Date:yyyy-MM-dd} - {history.Category}: {history.Summary}");
        }

        var recentOffers = scenario.FreeAgencyMarketState?.OfferStates
            .Where(offer => offer.PersonId == personId)
            .OrderByDescending(offer => offer.SubmittedOn)
            .Take(3)
            .Select(offer => $"Recent negotiation: {offer.SubmittedOn:yyyy-MM-dd} {offer.ResponseStatus} - {offer.Evaluation.AgentOpinion}")
            ?? Array.Empty<string>();
        lines.AddRange(recentOffers);

        return new PlayerDossierSection("Agent / Representation", lines);
    }

    private static PlayerDossierSection BuildStaffOpinions(NewGmScenarioSnapshot scenario, string personId)
    {
        var lines = new List<string>();
        lines.AddRange(new StaffCoachingService().BuildDossierStaffOpinions(scenario, personId));

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

    private static PlayerDossierSection BuildCareerHistory(NewGmScenarioSnapshot scenario, string personId)
    {
        var lines = new List<string>();
        foreach (var stat in scenario.PriorSeasonStats.Where(stat => stat.PersonId == personId).OrderByDescending(stat => stat.SeasonYear))
        {
            lines.Add($"Last-season stats: {stat.SummaryText}");
        }

        foreach (var summary in scenario.CareerStatSummaries.Where(summary => summary.PersonId == personId).Take(1))
        {
            lines.Add($"Career summary: {summary.DisplaySummary}");
        }

        foreach (var history in scenario.PlayerTeamHistories.Where(history => history.PersonId == personId).OrderByDescending(history => history.ToSeasonYear).Take(3))
        {
            lines.Add($"Team history: {history.FromSeasonYear}-{history.ToSeasonYear} {history.TeamName} ({history.LeagueName}) - {history.Role}. {history.Notes}");
        }

        foreach (var timeline in scenario.PlayerCareerTimelines.Where(timeline => timeline.PersonId == personId).Take(1))
        {
            lines.AddRange(timeline.Entries.Select(entry => $"Timeline: {entry}"));
        }

        foreach (var entry in scenario.CareerTimeline.ForPerson(personId).Take(6))
        {
            lines.Add($"Career timeline: {entry.Date:yyyy-MM-dd} - {entry.Title}. {entry.Description}");
        }

        var draft = scenario.DraftPickHistory.FirstOrDefault(pick => pick.PlayerPersonId == personId);
        if (draft is not null)
        {
            lines.Add($"Draft history: {draft.Year} round {draft.Round}, pick {draft.OverallPick}; outcome so far {draft.Outcome}. {draft.OutcomeSummary}");
        }

        var pipeline = scenario.PlayerPipeline.FirstOrDefault(record => record.PersonId == personId);
        if (pipeline is not null)
        {
            lines.Add($"Pipeline: {pipeline.CurrentLevel}, {pipeline.PipelineStatus}, assignment {pipeline.AssignmentStatus}.");
            lines.Add($"Development path: {pipeline.DevelopmentLevel}; rights {pipeline.RightsStatus}; slide {pipeline.ContractSlideSummary}");
            lines.AddRange(pipeline.AssignmentHistory.Take(5).Select(entry => $"Assignment history: {entry}"));
        }

        if (lines.Count == 0)
        {
            lines.Add("No prior stats or career history are currently tracked.");
        }

        return new PlayerDossierSection("Career History", lines);
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

        if (scenario.FreeAgentMarket?.FreeAgents.Any(agent => agent.PersonId == personId) == true)
        {
            return PlayerDossierSource.FreeAgent;
        }

        if (scenario.TradeBlock?.Entries.Any(entry => entry.PersonId == personId) == true)
        {
            return PlayerDossierSource.TradeBlock;
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
        ?? scenario.FreeAgentMarket?.Find(personId)?.Position
        ?? scenario.TradeBlock?.Find(personId)?.Position
        ?? scenario.AlphaSnapshot.DraftBoard.Entries.SingleOrDefault(entry => entry.ProspectPersonId == personId)?.Bio?.Position
        ?? RosterPosition.Unknown;

    private static string CurrentScoutingPicture(DraftBoardEntry entry)
    {
        var position = entry.Bio?.Position.ToString() ?? "prospect";
        var role = entry.Bio?.PotentialLineupProjection ?? "lineup role still being defined";
        return entry.ScoutingConfidence switch
        {
            ScoutingConfidenceLevel.VeryHigh or ScoutingConfidenceLevel.High => $"Staff have a clear read: current {position} profile with enough evidence to project as {role}.",
            ScoutingConfidenceLevel.Medium => $"Staff have a working read on this {position}; another viewing would sharpen the current-role estimate.",
            ScoutingConfidenceLevel.Low or ScoutingConfidenceLevel.Unknown or null => $"Basic bio is known for this {position}, but current quality remains lightly scouted.",
            _ => $"Current quality is still being gathered for this {position}."
        };
    }

    private static string FutureScoutingPicture(DraftBoardEntry entry) =>
        $"{entry.Bio?.PotentialLineupProjection ?? "Future role still forming"}; {entry.ProjectionText}";

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

        var tradeBlock = scenario.TradeBlock?.Find(personId);
        if (tradeBlock is not null)
        {
            return $"Trade block: {tradeBlock.InterestLevel} interest";
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

        var freeAgent = scenario.FreeAgentMarket?.Find(personId);
        if (freeAgent is not null)
        {
            return $"Free agent market - previous team {freeAgent.PreviousTeam}";
        }

        var tradeBlock = scenario.TradeBlock?.Find(personId);
        if (tradeBlock is not null)
        {
            return $"Trade block - {tradeBlock.TeamName}";
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
