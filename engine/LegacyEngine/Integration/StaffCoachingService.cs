using LegacyEngine.Development;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;
using LegacyEngine.Staff;

namespace LegacyEngine.Integration;

public sealed class StaffCoachingService
{
    public IReadOnlyList<CoachingStaffProfile> BuildCoachProfiles(NewGmScenarioSnapshot scenario) =>
        EmployedStaff(scenario)
            .OrderBy(member => member.Department)
            .ThenBy(member => member.CurrentRole)
            .Select(member => BuildCoachProfile(scenario, member))
            .ToArray();

    public CoachingStaffProfile BuildCoachProfile(NewGmScenarioSnapshot scenario, StaffMember member)
    {
        var name = PersonName(scenario, member.PersonId);
        var philosophy = DeterminePhilosophy(member);
        var specialties = DetermineSpecialties(member).Distinct().DefaultIfEmpty(CoachSpecialty.Leadership).ToArray();
        var personality = DeterminePersonality(member);
        var career = scenario.StaffCareerHistory.FirstOrDefault(history => history.PersonId == member.PersonId);
        var profile = new CoachingStaffProfile(
            member.PersonId,
            name,
            member.CurrentRole,
            member.Department,
            philosophy,
            specialties,
            personality,
            BuildPhilosophySummary(name, member, philosophy, personality),
            BuildDevelopmentImpact(member, philosophy, specialties),
            BuildRosterRecommendationStyle(member, philosophy),
            career?.EvaluationSummary ?? $"{name} has {member.Profile.YearsExperience} year(s) of hockey operations experience and a {member.Profile.Reputation}/100 reputation.",
            ResponsibilitiesFor(member));
        profile.Validate();
        return profile;
    }

    public IReadOnlyList<StaffChemistryLink> BuildStaffChemistry(NewGmScenarioSnapshot scenario)
    {
        var staffIds = EmployedStaff(scenario).Select(member => member.PersonId).ToHashSet(StringComparer.Ordinal);
        staffIds.Add(scenario.GeneralManagerProfile.Person.PersonId);
        var links = scenario.AlphaSnapshot.Relationships
            .Where(relationship => staffIds.Contains(relationship.FromPersonId) && staffIds.Contains(relationship.ToPersonId))
            .GroupBy(relationship => (relationship.FromPersonId, relationship.ToPersonId))
            .Select(group => group.OrderByDescending(item => item.LastInteractionDate).First())
            .Select(relationship =>
            {
                var avg = (relationship.Trust + relationship.Respect + relationship.Confidence) / 3;
                var warning = avg < 45 || relationship.Rivalry > 60;
                var summary = warning
                    ? $"{PersonName(scenario, relationship.FromPersonId)} and {PersonName(scenario, relationship.ToPersonId)} need clearer communication."
                    : $"{PersonName(scenario, relationship.FromPersonId)} and {PersonName(scenario, relationship.ToPersonId)} have workable staff chemistry.";
                var link = new StaffChemistryLink(
                    relationship.FromPersonId,
                    PersonName(scenario, relationship.FromPersonId),
                    relationship.ToPersonId,
                    PersonName(scenario, relationship.ToPersonId),
                    relationship.Trust,
                    relationship.Respect,
                    relationship.Confidence,
                    summary,
                    warning);
                link.Validate();
                return link;
            })
            .ToArray();

        return links.Length == 0
            ? new[]
            {
                new StaffChemistryLink(
                    scenario.GeneralManagerProfile.Person.PersonId,
                    scenario.GeneralManagerProfile.Person.Identity.DisplayName,
                    "staff-room",
                    "Staff Room",
                    50,
                    50,
                    50,
                    "Staff chemistry is still forming; no major conflict has surfaced.",
                    false)
            }
            : links;
    }

    public PlayerCoachFit EvaluatePlayerFit(NewGmScenarioSnapshot scenario, string personId)
    {
        var coach = FindDevelopmentCoach(scenario) ?? FindHeadCoach(scenario) ?? EmployedStaff(scenario).First();
        var coachProfile = BuildCoachProfile(scenario, coach);
        var playerName = PersonName(scenario, personId);
        var position = PositionText(scenario, personId);
        var plan = scenario.DevelopmentPlans.FirstOrDefault(plan => plan.PersonId == personId);
        var profile = scenario.AlphaSnapshot.DevelopmentProfiles.FirstOrDefault(profile => profile.PersonId == personId);
        var reasons = new List<string>();
        var score = 55;

        if (coachProfile.Philosophy is CoachPhilosophy.PlayerDevelopment or CoachPhilosophy.YouthFocus)
        {
            score += 12;
            reasons.Add("Coach philosophy supports young-player development.");
        }

        if (position == "G" || position.Contains("Goalie", StringComparison.OrdinalIgnoreCase))
        {
            if (coachProfile.Specialties.Contains(CoachSpecialty.Goalies))
            {
                score += 14;
                reasons.Add("Goalie specialty lines up with the player's position.");
            }
        }
        else if (position is "D" or "Defense")
        {
            if (coachProfile.Specialties.Contains(CoachSpecialty.Defense) || coachProfile.Philosophy == CoachPhilosophy.Defensive)
            {
                score += 10;
                reasons.Add("Defensive coaching emphasis matches this player's role.");
            }
        }
        else if (coachProfile.Specialties.Contains(CoachSpecialty.Forwards) || coachProfile.Philosophy is CoachPhilosophy.Offensive or CoachPhilosophy.Speed)
        {
            score += 8;
            reasons.Add("Forward development environment should support the player's role.");
        }

        if (plan is not null)
        {
            if (plan.Confidence >= 65)
            {
                score += 6;
                reasons.Add("Current development-plan confidence is healthy.");
            }
            else if (plan.Confidence < 45)
            {
                score -= 8;
                reasons.Add("Development-plan confidence needs staff attention.");
            }

            if (plan.FocusAreas.Any(focus => FocusMatchesSpecialty(focus, coachProfile.Specialties)))
            {
                score += 8;
                reasons.Add("Coach specialties match at least one current development focus.");
            }
        }

        if (profile is not null && profile.Stage is DevelopmentStage.Prospect or DevelopmentStage.Junior)
        {
            score += coachProfile.Philosophy == CoachPhilosophy.VeteranFocus ? -6 : 4;
            reasons.Add("Age/stage fit is being judged against the coach's preferred player mix.");
        }

        var grade = GradeFor(score);
        if (reasons.Count == 0)
        {
            reasons.Add("No strong fit or conflict signal is visible yet.");
        }

        var fit = new PlayerCoachFit(
            personId,
            playerName,
            coach.PersonId,
            coachProfile.StaffName,
            grade,
            position,
            $"{playerName} has a {grade} fit with {coachProfile.StaffName}'s {coachProfile.Philosophy} approach.",
            reasons);
        fit.Validate();
        return fit;
    }

    public StaffMeetingReport GenerateMonthlyMeetingReport(NewGmScenarioSnapshot scenario)
    {
        var coach = FindHeadCoach(scenario) ?? FindDevelopmentCoach(scenario) ?? EmployedStaff(scenario).First();
        var coachName = PersonName(scenario, coach.PersonId);
        var developmentNotes = scenario.DevelopmentRecommendations
            .Where(item => item.IsActive)
            .OrderByDescending(item => item.CreatedOn)
            .Take(3)
            .Select(item => $"{item.PlayerName}: {item.RecommendedAction}")
            .DefaultIfEmpty("No major development intervention is required this month.")
            .ToArray();
        var rosterNotes = new[]
        {
            $"Roster sits at {scenario.AlphaSnapshot.Roster.ActivePlayers.Count} active player(s).",
            scenario.PendingActions.Any(action => action.IsOpen)
                ? $"{scenario.PendingActions.Count(action => action.IsOpen)} GM decision(s) still need attention."
                : "No urgent roster decision is currently blocking the staff."
        };
        var medicalNotes = scenario.AlphaSnapshot.Injuries
            .Where(injury => injury.IsActive)
            .Take(2)
            .Select(injury => $"{PersonName(scenario, injury.PersonId)}: {injury.Severity} {injury.InjuryType}, expected return {injury.ExpectedReturnDate:yyyy-MM-dd}.")
            .DefaultIfEmpty("Medical room has no major active concern.")
            .ToArray();
        var recommendations = developmentNotes
            .Take(2)
            .Concat(rosterNotes.Take(1))
            .Concat(medicalNotes.Take(1))
            .Distinct(StringComparer.Ordinal)
            .Take(5)
            .ToArray();

        var report = new StaffMeetingReport(
            scenario.CurrentDate,
            coachName,
            $"{coachName} recommends keeping player development, roster compliance, and medical risk aligned before the next key date.",
            recommendations,
            developmentNotes,
            rosterNotes,
            medicalNotes);
        report.Validate();
        return report;
    }

    public IReadOnlyList<DepartmentGradeReport> BuildDepartmentGrades(NewGmScenarioSnapshot scenario)
    {
        var reports = new List<DepartmentGradeReport>();
        AddDepartment(reports, "Coaching", CoachingScore(scenario), "Coaching staff impact blends teaching, tactics, leadership, and communication.");
        AddDepartment(reports, "Development", DevelopmentScore(scenario), "Development grade reflects plans, recommendations, and coaching fit.");
        AddDepartment(reports, "Scouting", ScoutingScore(scenario), "Scouting grade reflects coverage, report volume, and scout confidence.");
        AddDepartment(reports, "Medical", MedicalScore(scenario), "Medical grade reflects active injury load and staff coverage.");
        AddDepartment(reports, "Equipment", EquipmentScore(scenario), "Equipment grade is a simple support placeholder until deeper operations arrive.");
        return reports;
    }

    public IReadOnlyList<OrganizationChartNode> BuildOrganizationChart(NewGmScenarioSnapshot scenario)
    {
        var budgetService = new StaffBudgetService();
        var rulebook = RulebookPresets.CreateJuniorMajor();
        var salaryByPerson = EmployedStaff(scenario)
            .ToDictionary(
                member => member.PersonId,
                member => budgetService.CompensationFor(member, scenario, rulebook).Salary.AnnualAmount,
                StringComparer.Ordinal);
        var gmId = scenario.GeneralManagerProfile.Person.PersonId;
        var ownerId = scenario.AlphaSnapshot.Owner.OwnerId;
        var nodes = new List<OrganizationChartNode>
        {
            new(ownerId, scenario.AlphaSnapshot.Owner.Name, "Owner", "board", "Sets budget, expectations, patience, and organization mandate.", "Ownership budget authority"),
            new(gmId, scenario.GeneralManagerProfile.Person.Identity.DisplayName, "General Manager", ownerId, "Owns roster, staff, contracts, scouting, and hockey operations decisions.", salaryByPerson.TryGetValue(gmId, out var gmSalary) ? gmSalary.ToString("C0") : "GM salary budgeted")
        };

        foreach (var staff in EmployedStaff(scenario).Where(staff => staff.PersonId != gmId))
        {
            var reportsTo = staff.CurrentRole is StaffRole.HeadCoach or StaffRole.HeadScout or StaffRole.DirectorOfScouting or StaffRole.AssistantGM
                ? gmId
                : HeadForDepartment(scenario, staff.Department) ?? gmId;
            nodes.Add(new OrganizationChartNode(
                staff.PersonId,
                PersonName(scenario, staff.PersonId),
                StaffRoles.Title(staff.CurrentRole),
                reportsTo,
                ResponsibilitiesFor(staff),
                salaryByPerson.TryGetValue(staff.PersonId, out var salary) ? salary.ToString("C0") : "salary not tracked"));
        }

        foreach (var node in nodes)
        {
            node.Validate();
        }

        return nodes;
    }

    public StaffPerformanceReview BuildPerformanceReview(NewGmScenarioSnapshot scenario, string personId)
    {
        var member = FindStaff(scenario, personId);
        var profile = BuildCoachProfile(scenario, member);
        var chemistry = BuildStaffChemistry(scenario).Where(link => link.FromPersonId == personId || link.ToPersonId == personId).ToArray();
        var warnings = chemistry.Count(link => link.IsWarning);
        var score = member.Profile.Reputation
            + member.Attributes.CoachingAttributes.Values.DefaultIfEmpty(0).Max() / 3
            + member.Attributes.ScoutingAttributes.Values.DefaultIfEmpty(0).Max() / 4
            + member.Attributes.MedicalAttributes.Values.DefaultIfEmpty(0).Max() / 4
            - (warnings * 10);
        var outcome = score >= 95 ? StaffPerformanceOutcome.Extend
            : score >= 70 ? StaffPerformanceOutcome.Retain
            : score >= 50 ? StaffPerformanceOutcome.Monitor
            : StaffPerformanceOutcome.Replace;
        var concerns = warnings > 0
            ? chemistry.Where(link => link.IsWarning).Select(link => link.Summary).Take(2).ToArray()
            : profile.Specialties.Count < 2
                ? new[] { "Specialty coverage is narrow for the current staff room." }
                : Array.Empty<string>();
        var review = new StaffPerformanceReview(
            personId,
            profile.StaffName,
            scenario.CurrentDate,
            outcome,
            $"{profile.StaffName} is reviewed as {outcome}. {profile.PhilosophySummary}",
            profile.Specialties.Select(specialty => specialty.ToString()).Take(4).ToArray(),
            concerns,
            outcome switch
            {
                StaffPerformanceOutcome.Extend => "Consider an extension if salary and role remain aligned.",
                StaffPerformanceOutcome.Retain => "Keep in role and reassess after the next development cycle.",
                StaffPerformanceOutcome.Monitor => "Monitor chemistry and department fit before committing long term.",
                _ => "Begin replacement planning or reduce responsibilities."
            });
        review.Validate();
        return review;
    }

    public StaffHiringFitSummary EvaluateHiringFit(NewGmScenarioSnapshot scenario, StaffCandidate candidate)
    {
        var budget = new StaffBudgetService().Build(scenario, LegacyEngine.RuleEngine.RulebookPresets.CreateJuniorMajor());
        var score = Math.Clamp((candidate.RoleFit + candidate.DepartmentFit + candidate.Reputation) / 3
            + (candidate.YearsExperience >= 8 ? 6 : 0)
            - (candidate.ChemistryRisk.Contains("High", StringComparison.OrdinalIgnoreCase) ? 12 : 0), 0, 100);
        var reasons = new List<string>
        {
            $"Role fit {candidate.RoleFit}/100 and department fit {candidate.DepartmentFit}/100.",
            $"Salary ask {candidate.ExpectedSalary.AnnualAmount:C0}; budget before hire is {budget.Status}.",
            candidate.ChemistryRisk,
            candidate.PersonalityFitSummary
        };
        var fit = new StaffHiringFitSummary(
            candidate.CandidateId,
            candidate.Person.Identity.DisplayName,
            StaffRoles.Title(candidate.StaffMember.CurrentRole),
            score,
            $"Expected salary {candidate.ExpectedSalary.AnnualAmount:C0}; remaining budget before hire {budget.RemainingBudget:C0}.",
            candidate.ChemistryRisk,
            $"{candidate.YearsExperience} year(s) experience, current employer: {candidate.CurrentEmployer}.",
            score >= 75 ? "Recommended if budget holds." : score >= 55 ? "Viable, but compare chemistry and salary." : "Risky hire unless the role need is urgent.",
            reasons);
        fit.Validate();
        return fit;
    }

    public IReadOnlyList<string> BuildDossierStaffOpinions(NewGmScenarioSnapshot scenario, string personId)
    {
        var fit = EvaluatePlayerFit(scenario, personId);
        var scoutReport = scenario.CompletedScoutingReports
            .Where(report => report.PlayerId == personId)
            .OrderByDescending(report => report.CreatedOn)
            .FirstOrDefault();
        var injury = scenario.AlphaSnapshot.Injuries
            .Where(injury => injury.PersonId == personId && injury.IsActive)
            .OrderByDescending(injury => injury.InjuryDate)
            .FirstOrDefault();
        var lines = new List<string>
        {
            $"Head coach/development fit: {fit.FitGrade}. {fit.Summary}",
            $"Development coach view: {string.Join(" ", fit.Reasons.Take(2))}",
            scoutReport is null
                ? "Scout view: no completed scouting report yet."
                : $"Scout view: {scoutReport.Confidence} confidence; {scoutReport.Recommendation}",
            injury is null
                ? "Medical view: no active injury limitation."
                : $"Medical view: {injury.Severity} {injury.InjuryType}; expected return {injury.ExpectedReturnDate:yyyy-MM-dd}.",
            $"Relationship view: GM trust context remains {RelationshipScore(scenario, scenario.GeneralManagerProfile.Person.PersonId, personId)}/100."
        };
        return lines;
    }

    private static IEnumerable<StaffMember> EmployedStaff(NewGmScenarioSnapshot scenario) =>
        scenario.StaffMembers
            .Where(member => member.EmploymentStatus == StaffEmploymentStatus.Employed)
            .GroupBy(member => member.PersonId, StringComparer.Ordinal)
            .Select(group => group.Last());

    private static StaffMember FindStaff(NewGmScenarioSnapshot scenario, string personId) =>
        EmployedStaff(scenario).FirstOrDefault(member => member.PersonId == personId)
        ?? throw new ArgumentException("Staff member was not found.", nameof(personId));

    private static StaffMember? FindHeadCoach(NewGmScenarioSnapshot scenario) =>
        EmployedStaff(scenario).FirstOrDefault(member => member.CurrentRole == StaffRole.HeadCoach);

    private static StaffMember? FindDevelopmentCoach(NewGmScenarioSnapshot scenario) =>
        EmployedStaff(scenario).FirstOrDefault(member => member.CurrentRole is StaffRole.DevelopmentCoach or StaffRole.AssistantCoach or StaffRole.HeadCoach);

    private static CoachPhilosophy DeterminePhilosophy(StaffMember member)
    {
        if (member.CurrentRole is StaffRole.DevelopmentCoach or StaffRole.SkillsCoach)
        {
            return CoachPhilosophy.PlayerDevelopment;
        }

        if (member.CurrentRole is StaffRole.HeadScout or StaffRole.DirectorOfScouting or StaffRole.Scout)
        {
            return CoachPhilosophy.YouthFocus;
        }

        var tactics = member.Attributes.CoachingScore(StaffCoachingAttribute.Tactics);
        var discipline = member.Attributes.CoachingScore(StaffCoachingAttribute.Discipline);
        var development = member.Attributes.CoachingScore(StaffCoachingAttribute.Development);
        var adaptability = member.Attributes.CoachingScore(StaffCoachingAttribute.Adaptability);
        if (development >= Math.Max(tactics, discipline))
        {
            return CoachPhilosophy.PlayerDevelopment;
        }

        if (discipline >= 75)
        {
            return CoachPhilosophy.Discipline;
        }

        if (adaptability >= 72)
        {
            return CoachPhilosophy.Speed;
        }

        return tactics >= 72 ? CoachPhilosophy.PuckPossession : CoachPhilosophy.Balanced;
    }

    private static IEnumerable<CoachSpecialty> DetermineSpecialties(StaffMember member)
    {
        if (member.CurrentRole is StaffRole.GoalieCoach or StaffRole.GoaltendingCoach or StaffRole.GoaltendingScout)
        {
            yield return CoachSpecialty.Goalies;
        }

        if (member.Attributes.CoachingScore(StaffCoachingAttribute.Tactics) >= 65)
        {
            yield return CoachSpecialty.PowerPlay;
            yield return CoachSpecialty.PenaltyKill;
        }

        if (member.Attributes.CoachingScore(StaffCoachingAttribute.Leadership) >= 60)
        {
            yield return CoachSpecialty.Leadership;
        }

        if (member.Attributes.CoachingScore(StaffCoachingAttribute.Motivation) >= 60)
        {
            yield return CoachSpecialty.Confidence;
        }

        if (member.Attributes.CoachingScore(StaffCoachingAttribute.Teaching) >= 55)
        {
            yield return CoachSpecialty.Skating;
            yield return CoachSpecialty.Passing;
        }

        if (member.CurrentRole is StaffRole.HeadCoach or StaffRole.AssistantCoach)
        {
            yield return CoachSpecialty.Forwards;
            yield return CoachSpecialty.Defense;
        }

        if (member.Attributes.CoachingScore(StaffCoachingAttribute.Discipline) >= 65)
        {
            yield return CoachSpecialty.PhysicalPlay;
        }
    }

    private static CoachPersonality DeterminePersonality(StaffMember member)
    {
        var teaching = member.Attributes.CoachingScore(StaffCoachingAttribute.Teaching);
        var communication = member.Attributes.CoachingScore(StaffCoachingAttribute.Communication);
        var discipline = member.Attributes.CoachingScore(StaffCoachingAttribute.Discipline);
        var motivation = member.Attributes.CoachingScore(StaffCoachingAttribute.Motivation);
        if (teaching >= 75)
        {
            return CoachPersonality.Teacher;
        }

        if (discipline >= 75)
        {
            return CoachPersonality.Demanding;
        }

        if (motivation >= 72)
        {
            return CoachPersonality.Intense;
        }

        return communication >= 65 ? CoachPersonality.PlayerFriendly : CoachPersonality.Calm;
    }

    private static string BuildPhilosophySummary(string name, StaffMember member, CoachPhilosophy philosophy, CoachPersonality personality) =>
        $"{name} leans {philosophy} with a {personality} staff-room style; recommendations will favor {PhilosophyEffect(philosophy)}.";

    private static string PhilosophyEffect(CoachPhilosophy philosophy) =>
        philosophy switch
        {
            CoachPhilosophy.Offensive => "skill and attacking upside",
            CoachPhilosophy.Defensive => "structure and defensive reliability",
            CoachPhilosophy.Physical => "size, edge, and hard minutes",
            CoachPhilosophy.Speed => "pace and transition ability",
            CoachPhilosophy.PuckPossession => "players who can hold and move pucks under pressure",
            CoachPhilosophy.PlayerDevelopment => "patience, teaching, and long-term growth",
            CoachPhilosophy.VeteranFocus => "older players who already know pro habits",
            CoachPhilosophy.YouthFocus => "prospects and coachable young players",
            CoachPhilosophy.Discipline => "safe habits and low-risk choices",
            CoachPhilosophy.Creativity => "instinct, problem-solving, and offensive freedom",
            _ => "balanced roster decisions"
        };

    private static string BuildDevelopmentImpact(StaffMember member, CoachPhilosophy philosophy, IReadOnlyList<CoachSpecialty> specialties)
    {
        var development = member.Attributes.CoachingScore(StaffCoachingAttribute.Development);
        var teaching = member.Attributes.CoachingScore(StaffCoachingAttribute.Teaching);
        var signal = development + teaching + (philosophy == CoachPhilosophy.PlayerDevelopment ? 20 : 0);
        return signal >= 160
            ? $"Strong player-growth influence through {string.Join(", ", specialties.Take(3))}."
            : signal >= 115
                ? $"Steady development influence; best used with focused plans around {string.Join(", ", specialties.Take(2))}."
                : "Limited development influence; pair with stronger development support when possible.";
    }

    private static string BuildRosterRecommendationStyle(StaffMember member, CoachPhilosophy philosophy) =>
        member.CurrentRole switch
        {
            StaffRole.HeadCoach => $"Head coach recommendations will lean toward {PhilosophyEffect(philosophy)}.",
            StaffRole.AssistantCoach => $"Assistant coach input will support {PhilosophyEffect(philosophy)}.",
            StaffRole.DevelopmentCoach => "Development coach recommendations will prioritize growth opportunities and confidence.",
            StaffRole.HeadScout or StaffRole.DirectorOfScouting or StaffRole.Scout => "Scouting recommendations will emphasize evidence, viewings, and projection confidence.",
            StaffRole.HeadAthleticTherapist or StaffRole.TeamDoctor => "Medical recommendations will prioritize rest, recovery, and long-term risk.",
            _ => "Staff recommendations will support the current department need."
        };

    private static string ResponsibilitiesFor(StaffMember staff) =>
        staff.CurrentRole switch
        {
            StaffRole.HeadCoach => "Leads staff, sets team philosophy, advises roster and player usage.",
            StaffRole.AssistantCoach => "Supports player teaching, game preparation, and role recommendations.",
            StaffRole.DevelopmentCoach or StaffRole.SkillsCoach => "Owns development plans, player confidence, and skill-growth feedback.",
            StaffRole.HeadScout or StaffRole.DirectorOfScouting => "Sets scouting priorities, manages scout coverage, and reports draft intelligence.",
            StaffRole.Scout or StaffRole.RegionalScout or StaffRole.AmateurScout => "Completes player viewings and submits scouting reports.",
            StaffRole.HeadAthleticTherapist or StaffRole.AthleticTherapist or StaffRole.TeamDoctor => "Monitors player health, recovery, and injury risk.",
            StaffRole.EquipmentManager or StaffRole.HeadEquipmentManager => "Supports player equipment readiness and travel operations.",
            _ => "Supports hockey operations."
        };

    private static bool FocusMatchesSpecialty(DevelopmentPlanFocus focus, IReadOnlyList<CoachSpecialty> specialties) =>
        (focus == DevelopmentPlanFocus.Skating && specialties.Contains(CoachSpecialty.Skating))
        || (focus == DevelopmentPlanFocus.Shooting && specialties.Contains(CoachSpecialty.Shooting))
        || (focus == DevelopmentPlanFocus.Playmaking && specialties.Contains(CoachSpecialty.Passing))
        || (focus == DevelopmentPlanFocus.Defensive && specialties.Contains(CoachSpecialty.Defense))
        || (focus == DevelopmentPlanFocus.Physical && specialties.Contains(CoachSpecialty.PhysicalPlay))
        || (focus == DevelopmentPlanFocus.Confidence && specialties.Contains(CoachSpecialty.Confidence));

    private static CoachPlayerFitGrade GradeFor(int score) =>
        score switch
        {
            >= 82 => CoachPlayerFitGrade.Excellent,
            >= 68 => CoachPlayerFitGrade.Good,
            >= 48 => CoachPlayerFitGrade.Average,
            >= 32 => CoachPlayerFitGrade.Poor,
            _ => CoachPlayerFitGrade.Terrible
        };

    private static int CoachingScore(NewGmScenarioSnapshot scenario) =>
        ScoreDepartment(scenario, StaffDepartment.Coaching, member =>
            (member.Attributes.CoachingScore(StaffCoachingAttribute.Teaching)
                + member.Attributes.CoachingScore(StaffCoachingAttribute.Tactics)
                + member.Attributes.CoachingScore(StaffCoachingAttribute.Leadership)
                + member.Attributes.CoachingScore(StaffCoachingAttribute.Communication)) / 4);

    private static int DevelopmentScore(NewGmScenarioSnapshot scenario) =>
        Math.Clamp(ScoreDepartment(scenario, StaffDepartment.Coaching, member =>
            (member.Attributes.CoachingScore(StaffCoachingAttribute.Development)
                + member.Attributes.CoachingScore(StaffCoachingAttribute.Teaching)
                + member.Attributes.CoachingScore(StaffCoachingAttribute.Motivation)) / 3)
            + Math.Min(10, scenario.DevelopmentPlans.Count / 3), 0, 100);

    private static int ScoutingScore(NewGmScenarioSnapshot scenario) =>
        Math.Clamp(ScoreDepartment(scenario, StaffDepartment.Scouting, member =>
            member.Attributes.ScoutingAttributes.Values.DefaultIfEmpty(member.Profile.Reputation).AverageAsInt())
            + Math.Min(12, scenario.CompletedScoutingReports.Count / 3), 0, 100);

    private static int MedicalScore(NewGmScenarioSnapshot scenario) =>
        Math.Clamp(ScoreDepartment(scenario, StaffDepartment.Medical, member =>
            member.Attributes.MedicalAttributes.Values.DefaultIfEmpty(member.Profile.Reputation).AverageAsInt())
            - Math.Min(15, scenario.AlphaSnapshot.Injuries.Count(injury => injury.IsActive) * 3), 0, 100);

    private static int EquipmentScore(NewGmScenarioSnapshot scenario) =>
        Math.Clamp(ScoreDepartment(scenario, StaffDepartment.Equipment, member => member.Profile.Reputation), 45, 82);

    private static int ScoreDepartment(NewGmScenarioSnapshot scenario, StaffDepartment department, Func<StaffMember, int> scorer)
    {
        var staff = EmployedStaff(scenario).Where(member => member.Department == department).ToArray();
        return staff.Length == 0 ? 50 : Math.Clamp((int)Math.Round(staff.Average(scorer)), 0, 100);
    }

    private static void AddDepartment(List<DepartmentGradeReport> reports, string department, int score, string summary)
    {
        var grade = score >= 85 ? DepartmentGrade.A
            : score >= 70 ? DepartmentGrade.B
            : score >= 55 ? DepartmentGrade.C
            : score >= 40 ? DepartmentGrade.D
            : DepartmentGrade.F;
        reports.Add(new DepartmentGradeReport(department, grade, score, $"{department}: {grade} ({score}/100). {summary}", new[] { summary }));
    }

    private static string? HeadForDepartment(NewGmScenarioSnapshot scenario, StaffDepartment department) =>
        department switch
        {
            StaffDepartment.Coaching => EmployedStaff(scenario).FirstOrDefault(member => member.CurrentRole == StaffRole.HeadCoach)?.PersonId,
            StaffDepartment.Scouting => EmployedStaff(scenario).FirstOrDefault(member => member.CurrentRole is StaffRole.HeadScout or StaffRole.DirectorOfScouting)?.PersonId,
            StaffDepartment.Medical => EmployedStaff(scenario).FirstOrDefault(member => member.CurrentRole is StaffRole.HeadAthleticTherapist or StaffRole.TeamDoctor)?.PersonId,
            StaffDepartment.Equipment => EmployedStaff(scenario).FirstOrDefault(member => member.CurrentRole == StaffRole.HeadEquipmentManager)?.PersonId,
            _ => null
        };

    private static int RelationshipScore(NewGmScenarioSnapshot scenario, string fromId, string toId) =>
        scenario.AlphaSnapshot.Relationships
            .Where(relationship => relationship.FromPersonId == fromId && relationship.ToPersonId == toId)
            .Select(relationship => (relationship.Trust + relationship.Respect + relationship.Confidence + relationship.Loyalty) / 4)
            .DefaultIfEmpty(50)
            .First();

    private static string PersonName(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId)?.Identity.DisplayName
        ?? scenario.StaffCandidates.FirstOrDefault(candidate => candidate.Person.PersonId == personId)?.Person.Identity.DisplayName
        ?? scenario.ProspectRights.FirstOrDefault(prospect => prospect.ProspectPersonId == personId)?.ProspectName
        ?? personId;

    private static string PositionText(NewGmScenarioSnapshot scenario, string personId)
    {
        var roster = scenario.AlphaSnapshot.Roster.Players.FirstOrDefault(player => player.PersonId == personId);
        if (roster is not null)
        {
            return PositionShort(roster.Position);
        }

        var prospect = scenario.ProspectRights.FirstOrDefault(prospect => prospect.ProspectPersonId == personId);
        if (prospect is not null)
        {
            return prospect.Position.ToString();
        }

        var draft = scenario.AlphaSnapshot.DraftBoard.Entries.FirstOrDefault(entry => entry.ProspectPersonId == personId);
        return draft?.Bio is null ? "Unknown" : PositionShort(draft.Bio.Position);
    }

    private static string PositionShort(RosterPosition position) =>
        position switch
        {
            RosterPosition.Center => "C",
            RosterPosition.LeftWing => "LW",
            RosterPosition.RightWing => "RW",
            RosterPosition.Defense => "D",
            RosterPosition.Goalie => "G",
            _ => "Unknown"
        };
}

internal static class StaffCoachingServiceEnumerableExtensions
{
    public static int AverageAsInt(this IEnumerable<int> values)
    {
        var materialized = values.ToArray();
        return materialized.Length == 0 ? 0 : (int)Math.Round(materialized.Average());
    }
}
