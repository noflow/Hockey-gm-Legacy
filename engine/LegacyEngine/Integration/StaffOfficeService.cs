using LegacyEngine.Contracts;
using LegacyEngine.Events;
using LegacyEngine.Names;
using LegacyEngine.People;
using LegacyEngine.RuleEngine;
using LegacyEngine.Staff;
using PeopleCareerTimelineEntry = LegacyEngine.People.CareerTimelineEntry;

namespace LegacyEngine.Integration;

public sealed class StaffOfficeService
{
    public IReadOnlyList<StaffOfficeProfile> BuildStaffProfiles(NewGmScenarioSnapshot scenario) =>
        BuildStaffProfiles(scenario, RulebookPresets.CreateJuniorMajor());

    public IReadOnlyList<StaffOfficeProfile> BuildStaffProfiles(NewGmScenarioSnapshot scenario, Rulebook rulebook) =>
        scenario.StaffMembers
            .Where(member => member.EmploymentStatus == StaffEmploymentStatus.Employed)
            .GroupBy(member => member.PersonId, StringComparer.Ordinal)
            .Select(group => group.Last())
            .OrderBy(member => member.Department)
            .ThenBy(member => member.CurrentRole)
            .Select(member => BuildProfile(scenario, member, rulebook))
            .ToArray();

    public IReadOnlyList<StaffVacancy> BuildVacancies(NewGmScenarioSnapshot scenario, Rulebook rulebook)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(rulebook);
        scenario.Validate();

        var limits = StaffLimits(rulebook);
        var vacancies = limits
            .Select(limit =>
            {
                var current = CurrentStaffCount(scenario, limit.Role);
                var warning = current < limit.Minimum
                    ? MissingWarning(limit.Role, current)
                    : $"{StaffRoles.Title(limit.Role)} staffing is covered.";
                return new StaffVacancy(limit.Role, StaffRoles.DepartmentFor(limit.Role), limit.Minimum, current, limit.Maximum, warning);
            })
            .Where(vacancy => vacancy.IsOpen)
            .ToArray();

        foreach (var vacancy in vacancies)
        {
            vacancy.Validate();
        }

        return vacancies;
    }

    public IReadOnlyList<string> BuildStaffWarnings(NewGmScenarioSnapshot scenario, Rulebook rulebook) =>
        BuildVacancies(scenario, rulebook)
            .Select(vacancy => vacancy.Warning)
            .ToArray();

    public StaffOfficeResult GenerateCandidatePool(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        if (scenario.StaffCandidates.Count > 0)
        {
            return Result(true, scenario, scenario.StaffCandidates.First(), null, null, null, Array.Empty<AlphaInboxItem>(), "Staff candidate pool already exists.");
        }

        var nameRegistry = new NameUniquenessRegistry();
        foreach (var person in scenario.AlphaSnapshot.People)
        {
            nameRegistry.RegisterExisting(CandidateScope(scenario), person.Identity.DisplayName);
        }

        var nameGenerator = new NameGenerator(NameGenerationSettings.CreateDefault(scenario.Season.Year + scenario.StaffMembers.Count + 41));
        var rulebook = registry.Rulebook ?? RulebookPresets.CreateJuniorMajor();
        var openRoles = BuildVacancies(scenario, rulebook)
            .Where(vacancy => vacancy.Role != StaffRole.GeneralManager)
            .Select(vacancy => vacancy.Role)
            .DefaultIfEmpty(StaffRole.DevelopmentCoach)
            .Take(8)
            .ToArray();
        var candidates = openRoles
            .Select((role, index) => BuildCandidate(
                registry,
                scenario,
                rulebook,
                $"candidate-staff-{index + 1:000}",
                GenerateCandidateName(nameGenerator, nameRegistry, scenario),
                role,
                68 + ((index * 7) % 16),
                65 + ((index * 5) % 18),
                52 + ((index * 6) % 20),
                StrengthsFor(role),
                WeaknessesFor(role),
                PersonalityFor(role),
                ChemistryRiskFor(role, index)))
            .ToArray();

        var updatedPeople = scenario.AlphaSnapshot.People
            .Concat(candidates.Select(candidate => candidate.Person))
            .GroupBy(person => person.PersonId, StringComparer.Ordinal)
            .Select(group => group.Last())
            .ToArray();
        var updated = scenario with
        {
            StaffCandidates = candidates,
            AlphaSnapshot = scenario.AlphaSnapshot with { People = updatedPeople }
        };

        foreach (var candidate in candidates)
        {
            QueueEvent(registry, updated, LegacyEventType.StaffCandidateGenerated, "Staff candidate generated", $"{candidate.Person.Identity.DisplayName} is available for {StaffRoles.Title(candidate.StaffMember.CurrentRole)}.", candidate.Person.PersonId);
        }

        var recommended = candidates.OrderByDescending(candidate => candidate.RoleFit + candidate.DepartmentFit + candidate.Reputation).First();
        var inbox = new[]
        {
            Inbox(updated, LegacyEventType.StaffCandidateGenerated, "Recommended staff hire", $"{recommended.Person.Identity.DisplayName} is the strongest current candidate: {recommended.HiringRecommendation}", recommended.Person.PersonId)
        };

        return Result(true, updated, recommended, null, null, null, inbox, $"Generated {candidates.Length} staff candidate(s).");
    }

    public StaffOfficeResult HireCandidate(EngineRegistry registry, NewGmScenarioSnapshot scenario, string candidateId)
    {
        var candidate = scenario.StaffCandidates.SingleOrDefault(candidate => candidate.CandidateId == candidateId);
        if (candidate is null)
        {
            return Result(false, scenario, null, null, null, null, Array.Empty<AlphaInboxItem>(), "Staff candidate is no longer available for hiring.");
        }

        var role = candidate.StaffMember.CurrentRole;
        var rulebook = registry.Rulebook ?? RulebookPresets.CreateJuniorMajor();
        var limit = StaffLimits(rulebook).FirstOrDefault(limit => limit.Role == role);
        if (limit is not null && CurrentStaffCount(scenario, role) >= limit.Maximum)
        {
            return Result(false, scenario, candidate, null, null, null, Array.Empty<AlphaInboxItem>(), $"{StaffRoles.Title(role)} staff limit is already full.");
        }

        var hired = registry.StaffEngine.AssignRole(registry.StaffEngine.Hire(candidate.StaffMember, scenario.CurrentDate), role, scenario.CurrentDate);
        var people = scenario.AlphaSnapshot.People
            .Concat(new[] { candidate.Person })
            .GroupBy(person => person.PersonId, StringComparer.Ordinal)
            .Select(group => group.Last())
            .ToArray();
        var updated = scenario with
        {
            StaffCandidates = scenario.StaffCandidates.Where(item => item.CandidateId != candidateId).ToArray(),
            StaffMembers = UpsertStaff(scenario.StaffMembers, hired),
            AlphaSnapshot = scenario.AlphaSnapshot with
            {
                People = people,
                StaffMembers = UpsertStaff(scenario.AlphaSnapshot.StaffMembers, hired)
            }
        };
        var marketCandidate = updated.StaffMarket?.FindByCandidateId(candidateId);
        var transactions = new List<LeagueTransaction>();
        if (updated.StaffMarket is not null && marketCandidate is not null)
        {
            var hiredMarketCandidate = StaffMarketService.MarkHired(marketCandidate, updated);
            var movement = StaffMarketService.Movement(
                updated,
                hiredMarketCandidate,
                marketCandidate.CurrentEmployerOrganizationId,
                marketCandidate.CurrentEmployer,
                updated.Organization.OrganizationId,
                updated.Organization.Name,
                StaffMarketStatus.Hired,
                $"{candidate.Person.Identity.DisplayName} was hired by {updated.Organization.Name} as {StaffRoles.Title(role)}.");
            updated = updated with
            {
                StaffMarket = updated.StaffMarket.Replace(hiredMarketCandidate).AddMovement(movement),
                StaffMovementHistory = updated.StaffMovementHistory.Append(movement).ToArray()
            };
            transactions.Add(StaffMarketService.Transaction(
                updated,
                updated.Organization.OrganizationId,
                updated.Organization.Name,
                candidate.Person.PersonId,
                candidate.Person.Identity.DisplayName,
                LeagueTransactionType.StaffHired,
                movement.Summary));
        }

        QueueEvent(registry, updated, LegacyEventType.StaffHired, "Staff hired", $"{candidate.Person.Identity.DisplayName} was hired as {StaffRoles.Title(role)}.", candidate.Person.PersonId);
        var inbox = new List<AlphaInboxItem>
        {
            Inbox(updated, LegacyEventType.StaffHired, "Staff hired", $"{candidate.Person.Identity.DisplayName} joined the front office as {StaffRoles.Title(role)} at {candidate.ExpectedSalary.AnnualAmount:C0}.", candidate.Person.PersonId)
        };
        var budget = new StaffBudgetService().Build(updated, rulebook);
        if (budget.Status == BudgetStatus.OverBudget)
        {
            QueueEvent(registry, updated, LegacyEventType.BudgetApproved, "Owner budget warning", budget.Warnings.FirstOrDefault() ?? "Hockey operations budget is over limit.", candidate.Person.PersonId);
            inbox.Add(Inbox(updated, LegacyEventType.BudgetApproved, "Owner budget warning", budget.Warnings.FirstOrDefault() ?? "Hockey operations budget is over limit.", candidate.Person.PersonId, LegacyEventSeverity.Warning));
        }

        return Result(true, updated, candidate, null, null, null, inbox, transactions, $"{candidate.Person.Identity.DisplayName} hired as {StaffRoles.Title(role)}.");
    }

    public StaffOfficeResult ReassignStaffRole(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId, StaffRole newRole)
    {
        var member = FindStaff(scenario, personId);
        var updatedMember = member.CurrentAssignment is null
            ? registry.StaffEngine.AssignRole(member, newRole, scenario.CurrentDate)
            : registry.StaffEngine.ReassignRole(member, newRole, scenario.CurrentDate);
        var updated = ReplaceStaff(scenario, updatedMember);

        QueueEvent(registry, updated, LegacyEventType.StaffRoleChanged, "Staff role changed", $"{Name(updated, personId)} was reassigned to {StaffRoles.Title(newRole)}.", personId);
        var inbox = new[] { Inbox(updated, LegacyEventType.StaffRoleChanged, "Staff role changed", $"{Name(updated, personId)} is now {StaffRoles.Title(newRole)}.", personId) };
        return Result(true, updated, null, null, null, null, inbox, $"{Name(updated, personId)} reassigned to {StaffRoles.Title(newRole)}.");
    }

    public StaffOfficeResult ReleaseStaff(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId, string reason)
    {
        var member = FindStaff(scenario, personId);
        var released = registry.StaffEngine.RemoveStaffMember(member, scenario.CurrentDate, reason);
        var updated = ReplaceStaff(scenario, released);
        var person = updated.AlphaSnapshot.People.FirstOrDefault(person => person.PersonId == personId);
        var transactions = new List<LeagueTransaction>();
        if (person is not null)
        {
            var rulebook = registry.Rulebook ?? RulebookPresets.CreateJuniorMajor();
            var salary = new StaffBudgetService().CompensationFor(member, updated, rulebook).Salary;
            var marketCandidate = new StaffMarketService().CandidateReturnedToMarket(updated, person, released, salary, reason);
            var movement = StaffMarketService.Movement(
                updated,
                marketCandidate,
                updated.Organization.OrganizationId,
                updated.Organization.Name,
                null,
                null,
                StaffMarketStatus.Available,
                $"{person.Identity.DisplayName} was released by {updated.Organization.Name} and returned to the staff market.");
            var market = (updated.StaffMarket ?? new StaffMarket($"staff-market:{updated.Season.SeasonId}", updated.CurrentDate, Array.Empty<StaffMarketCandidate>(), Array.Empty<StaffMovementRecord>()))
                .AddOrReplace(marketCandidate)
                .AddMovement(movement);
            updated = updated with
            {
                StaffMarket = market,
                StaffMovementHistory = updated.StaffMovementHistory.Append(movement).ToArray()
            };
            transactions.Add(StaffMarketService.Transaction(
                updated,
                updated.Organization.OrganizationId,
                updated.Organization.Name,
                person.PersonId,
                person.Identity.DisplayName,
                LeagueTransactionType.StaffReleased,
                movement.Summary));
        }

        QueueEvent(registry, updated, LegacyEventType.StaffReleased, "Staff released", $"{Name(updated, personId)} was released. Reason: {reason}", personId);
        var inbox = new[] { Inbox(updated, LegacyEventType.StaffReleased, "Staff released", $"{Name(updated, personId)} was released. {reason}", personId) };
        return Result(true, updated, null, null, null, null, inbox, transactions, $"{Name(updated, personId)} released.");
    }

    public StaffOfficeResult SetDevelopmentCoachFocus(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId, DevelopmentCoachFocus focus)
    {
        var member = FindStaff(scenario, personId);
        if (member.Department != StaffDepartment.Coaching)
        {
            return Result(false, scenario, null, null, null, null, Array.Empty<AlphaInboxItem>(), "Development coach focus requires a coaching staff member.");
        }

        return SetFocus(registry, scenario, member, focus.ToString(), $"Development focus set to {focus}.");
    }

    public StaffOfficeResult SetMedicalStaffFocus(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId, MedicalStaffFocus focus)
    {
        var member = FindStaff(scenario, personId);
        if (member.Department != StaffDepartment.Medical)
        {
            return Result(false, scenario, null, null, null, null, Array.Empty<AlphaInboxItem>(), "Medical focus requires a medical staff member.");
        }

        return SetFocus(registry, scenario, member, focus.ToString(), $"Medical focus set to {focus}.");
    }

    public StaffOfficeResult SetScoutingDepartmentFocus(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId, ScoutingDepartmentFocus focus)
    {
        var member = FindStaff(scenario, personId);
        if (member.Department != StaffDepartment.Scouting)
        {
            return Result(false, scenario, null, null, null, null, Array.Empty<AlphaInboxItem>(), "Scouting department focus requires a scouting staff member.");
        }

        return SetFocus(registry, scenario, member, focus.ToString(), $"Scouting focus set to {focus}. Scouting Operations assignments remain compatible.");
    }

    public StaffOfficeResult GenerateStaffEvaluation(EngineRegistry registry, NewGmScenarioSnapshot scenario, string personId)
    {
        var member = FindStaff(scenario, personId);
        var evaluation = registry.StaffEngine.EvaluateStaff(member, scenario.CurrentDate);
        var updatedMember = member.AddPerformanceReview(new StaffPerformance(
            ReviewId: $"staff-performance:{personId}:{scenario.CurrentDate:yyyyMMdd}:{member.PerformanceHistory.Count + 1}",
            PersonId: personId,
            OrganizationId: scenario.Organization.OrganizationId,
            ReviewDate: scenario.CurrentDate,
            Rating: evaluation.OverallEvaluation,
            Summary: evaluation.Summary,
            Metrics: new Dictionary<string, object?>
            {
                ["recommendation"] = evaluation.Recommendation.ToString(),
                ["strength_count"] = evaluation.Strengths.Count,
                ["weakness_count"] = evaluation.Weaknesses.Count
            }));
        var updated = ReplaceStaff(scenario, updatedMember) with
        {
            StaffEvaluations = scenario.StaffEvaluations.Append(evaluation).ToArray()
        };

        QueueEvent(registry, updated, LegacyEventType.StaffEvaluationCreated, "Staff evaluation created", evaluation.Summary, personId);
        var inbox = new[] { Inbox(updated, LegacyEventType.StaffEvaluationCreated, "Staff evaluation", $"{Name(updated, personId)}: {evaluation.Summary}", personId) };
        return Result(true, updated, null, null, evaluation, null, inbox, evaluation.Summary);
    }

    public StaffOfficeResult GenerateChemistryWarning(EngineRegistry registry, NewGmScenarioSnapshot scenario)
    {
        var reports = BuildStaffProfiles(scenario, registry.Rulebook ?? RulebookPresets.CreateJuniorMajor()).Select(profile => profile.Chemistry).ToArray();
        var warning = reports
            .OrderBy(report => report.GmFit)
            .FirstOrDefault(report => report.ConflictWarnings.Count > 0);

        if (warning is null)
        {
            return Result(true, scenario, null, null, null, null, Array.Empty<AlphaInboxItem>(), "No staff chemistry warning generated.");
        }

        QueueEvent(registry, scenario, LegacyEventType.StaffConflictWarning, "Staff conflict warning", warning.Summary, warning.PersonId);
        var inbox = new[] { Inbox(scenario, LegacyEventType.StaffConflictWarning, "Staff chemistry warning", warning.Summary, warning.PersonId, LegacyEventSeverity.Warning) };
        return Result(true, scenario, null, null, null, warning, inbox, warning.Summary);
    }

    public StaffChemistryReport EvaluateChemistry(NewGmScenarioSnapshot scenario, string personId)
    {
        var member = FindStaff(scenario, personId);
        var name = Name(scenario, personId);
        var gmFit = RelationshipWithGm(scenario, personId);
        var departmentFit = DepartmentFit(member);
        var warnings = new List<string>();
        if (gmFit < 45)
        {
            warnings.Add("GM relationship is strained; communication quality may suffer.");
        }

        if (departmentFit < 45)
        {
            warnings.Add("Department fit is thin for the current role.");
        }

        var partnerships = scenario.StaffMembers
            .Where(other => other.PersonId != personId && other.Department == member.Department && other.EmploymentStatus == StaffEmploymentStatus.Employed)
            .Select(other => Name(scenario, other.PersonId))
            .Take(3)
            .ToArray();

        if (partnerships.Length == 0 && gmFit >= 60 && departmentFit >= 60)
        {
            partnerships = new[] { "Strong partnership potential with the GM." };
        }

        var summary = warnings.Count > 0
            ? $"{name}: {string.Join(" ", warnings)}"
            : $"{name}: staff chemistry is stable; GM fit {Label(gmFit)} and department fit {Label(departmentFit)}.";

        var report = new StaffChemistryReport(personId, name, gmFit, departmentFit, warnings, partnerships, summary);
        report.Validate();
        return report;
    }

    private StaffOfficeProfile BuildProfile(NewGmScenarioSnapshot scenario, StaffMember member, Rulebook rulebook)
    {
        var focus = scenario.StaffFocusAssignments
            .Where(item => item.PersonId == member.PersonId)
            .OrderBy(item => item.SetOn)
            .ThenBy(item => item.FocusId, StringComparer.Ordinal)
            .LastOrDefault();
        var contract = member.ContractId is null
            ? "No contract reference"
            : scenario.Contracts.Concat(scenario.AlphaSnapshot.Contracts)
                .Where(contract => contract.ContractId == member.ContractId)
                .Select(contract => $"{contract.Status}, {contract.Term.StartDate:yyyy-MM-dd} to {contract.Term.EndDate:yyyy-MM-dd}")
                .DefaultIfEmpty($"Reference {member.ContractId}")
                .First();

        var profile = new StaffOfficeProfile(
            PersonId: member.PersonId,
            Name: Name(scenario, member.PersonId),
            CurrentRole: member.CurrentRole,
            Department: member.Department,
            ContractStatus: contract,
            Salary: new StaffBudgetService().CompensationFor(member, scenario, rulebook).Salary,
            Strengths: Strengths(member),
            Weaknesses: Weaknesses(member),
            RelationshipWithGm: RelationshipWithGm(scenario, member.PersonId),
            Chemistry: EvaluateChemistry(scenario, member.PersonId),
            CurrentAssignment: member.CurrentAssignment is null
                ? "No active assignment"
                : $"{StaffRoles.Title(member.CurrentAssignment.Role)} since {member.CurrentAssignment.StartDate:yyyy-MM-dd}",
            CurrentFocus: focus is null ? "No focus set" : $"{focus.Focus} ({focus.SetOn:yyyy-MM-dd})");
        profile.Validate();
        return profile;
    }

    private StaffOfficeResult SetFocus(EngineRegistry registry, NewGmScenarioSnapshot scenario, StaffMember member, string focus, string notes)
    {
        var assignment = new StaffFocusAssignment(
            FocusId: $"staff-focus:{member.PersonId}:{scenario.CurrentDate:yyyyMMdd}:{scenario.StaffFocusAssignments.Count + 1}",
            PersonId: member.PersonId,
            Department: member.Department,
            Focus: focus,
            SetOn: scenario.CurrentDate,
            Notes: notes);
        assignment.Validate();

        var updated = scenario with
        {
            StaffFocusAssignments = scenario.StaffFocusAssignments
                .Where(item => item.PersonId != member.PersonId || item.Department != member.Department)
                .Append(assignment)
                .ToArray()
        };

        QueueEvent(registry, updated, LegacyEventType.StaffFocusChanged, "Staff focus changed", $"{Name(updated, member.PersonId)}: {notes}", member.PersonId);
        var inbox = new[] { Inbox(updated, LegacyEventType.StaffFocusChanged, "Staff focus changed", $"{Name(updated, member.PersonId)}: {notes}", member.PersonId) };
        return Result(true, updated, null, assignment, null, null, inbox, $"{Name(updated, member.PersonId)} focus updated.");
    }

    private static StaffCandidate BuildCandidate(
        EngineRegistry registry,
        NewGmScenarioSnapshot scenario,
        Rulebook rulebook,
        string candidateId,
        GeneratedName generatedName,
        StaffRole role,
        int roleFit,
        int departmentFit,
        int reputation,
        IReadOnlyList<string> strengths,
        IReadOnlyList<string> weaknesses,
        string personality,
        string chemistryRisk)
    {
        var person = new Person(
            PersonId: $"person-{candidateId}",
            Identity: new PersonIdentity(generatedName.FirstName, generatedName.LastName, Gender.NonBinary, new DateOnly(1984 + (reputation % 9), 5, 10), generatedName.Nationality, generatedName.Birthplace),
            Status: PersonStatus.Active,
            Roles: Array.Empty<PersonRole>(),
            Reputation: new PersonReputation(reputation, Math.Max(20, reputation - 8), Math.Max(10, reputation - 20)),
            Personality: new PersonalityProfile(Ambition: 62, Loyalty: 66, Temperament: 58, Adaptability: 70, Professionalism: 72),
            CareerTimeline: Array.Empty<PeopleCareerTimelineEntry>());

        var attributes = AttributesFor(role, roleFit, departmentFit);
        var member = registry.StaffEngine.CreateStaffMember(
            person.PersonId,
            scenario.Organization.OrganizationId,
            role,
            yearsExperience: Math.Max(2, reputation / 7),
            reputation: reputation,
            attributes: attributes);
        var budgetService = new StaffBudgetService();
        var expectedSalary = budgetService.EstimateSalaryForRole(role, reputation, rulebook);
        var expensive = expectedSalary.AnnualAmount > budgetService.RangeFor(role, rulebook).Midpoint;
        var recommendation = roleFit + departmentFit + reputation >= 205 && !expensive
            ? "Recommended hire for the current staff plan."
            : expensive && chemistryRisk.Contains("risk", StringComparison.OrdinalIgnoreCase)
                ? "High-cost candidate; review chemistry risk before committing budget."
                : "Useful candidate, but fit and salary should be reviewed before hiring.";
        var currentEmployer = role switch
        {
            StaffRole.AssistantGM or StaffRole.DirectorOfHockeyOperations => "Independent hockey operations consultant",
            StaffRole.TeamDoctor or StaffRole.Physiotherapist or StaffRole.MassageTherapist => "Regional sports medicine clinic",
            StaffRole.HeadEquipmentManager or StaffRole.AssistantEquipmentManager => "Minor hockey equipment room",
            _ => "Available"
        };
        var candidate = new StaffCandidate(
            candidateId,
            person,
            member,
            roleFit,
            departmentFit,
            reputation,
            expectedSalary,
            strengths,
            weaknesses,
            personality,
            chemistryRisk,
            recommendation,
            currentEmployer,
            member.Profile.YearsExperience);
        candidate.Validate();
        return candidate;
    }

    private static GeneratedName GenerateCandidateName(NameGenerator generator, NameUniquenessRegistry registry, NewGmScenarioSnapshot scenario) =>
        generator.Generate(
            registry,
            CandidateScope(scenario),
            NameOrigin.CanadaEnglish,
            NameOrigin.CanadaFrench,
            NameOrigin.Usa,
            NameOrigin.Finland,
            NameOrigin.Sweden,
            NameOrigin.Czechia,
            NameOrigin.Germany,
            NameOrigin.Switzerland,
            NameOrigin.GenericEuropean);

    private static string CandidateScope(NewGmScenarioSnapshot scenario) =>
        $"staff-candidates:{scenario.Season.Year}:{scenario.Organization.OrganizationId}";

    private static StaffAttributes AttributesFor(StaffRole role, int roleFit, int departmentFit) =>
        StaffRoles.DepartmentFor(role) switch
        {
            StaffDepartment.Coaching => StaffAttributes.ForCoaching(new Dictionary<StaffCoachingAttribute, int>
            {
                [StaffCoachingAttribute.Development] = roleFit,
                [StaffCoachingAttribute.Teaching] = departmentFit,
                [StaffCoachingAttribute.Communication] = Math.Max(45, (roleFit + departmentFit) / 2)
            }),
            StaffDepartment.Medical => StaffAttributes.ForMedical(new Dictionary<StaffMedicalAttribute, int>
            {
                [StaffMedicalAttribute.InjuryPrevention] = roleFit,
                [StaffMedicalAttribute.Rehabilitation] = departmentFit,
                [StaffMedicalAttribute.Diagnosis] = Math.Max(45, (roleFit + departmentFit) / 2)
            }),
            StaffDepartment.Scouting => StaffAttributes.ForScouting(new Dictionary<StaffScoutingAttribute, int>
            {
                [StaffScoutingAttribute.TalentEvaluation] = roleFit,
                [StaffScoutingAttribute.CharacterEvaluation] = departmentFit,
                [StaffScoutingAttribute.RegionalKnowledge] = Math.Max(45, (roleFit + departmentFit) / 2)
            }),
            _ => StaffAttributes.Empty
        };

    private sealed record StaffLimit(StaffRole Role, int Minimum, int Maximum);

    private static IReadOnlyList<StaffLimit> StaffLimits(Rulebook rulebook)
    {
        var configured = rulebook.StaffRules?.PositionLimits
            .Select(limit => TryBuildLimit(limit, out var built) ? built : null)
            .Where(limit => limit is not null)
            .Cast<StaffLimit>()
            .ToArray();

        return configured is { Length: > 0 }
            ? configured
            : DefaultJuniorStaffLimits();
    }

    private static bool TryBuildLimit(StaffPositionLimit limit, out StaffLimit? staffLimit)
    {
        if (Enum.TryParse<StaffRole>(limit.Role, ignoreCase: true, out var role))
        {
            staffLimit = new StaffLimit(role, limit.Minimum, limit.Maximum);
            return true;
        }

        staffLimit = null;
        return false;
    }

    private static IReadOnlyList<StaffLimit> DefaultJuniorStaffLimits() =>
        new[]
        {
            new StaffLimit(StaffRole.GeneralManager, 1, 1),
            new StaffLimit(StaffRole.AssistantGM, 1, 1),
            new StaffLimit(StaffRole.HeadCoach, 1, 1),
            new StaffLimit(StaffRole.AssistantCoach, 2, 2),
            new StaffLimit(StaffRole.DevelopmentCoach, 1, 1),
            new StaffLimit(StaffRole.HeadScout, 1, 1),
            new StaffLimit(StaffRole.Scout, 3, 3),
            new StaffLimit(StaffRole.HeadAthleticTherapist, 1, 1),
            new StaffLimit(StaffRole.TeamDoctor, 1, 1),
            new StaffLimit(StaffRole.HeadEquipmentManager, 1, 1)
        };

    private static int CurrentStaffCount(NewGmScenarioSnapshot scenario, StaffRole role)
    {
        if (role == StaffRole.GeneralManager)
        {
            return string.IsNullOrWhiteSpace(scenario.AlphaSnapshot.GeneralManager.PersonId) ? 0 : 1;
        }

        return scenario.StaffMembers.Count(member => member.CurrentRole == role && member.EmploymentStatus == StaffEmploymentStatus.Employed);
    }

    private static string MissingWarning(StaffRole role, int current) =>
        role switch
        {
            StaffRole.HeadScout => "No Head Scout employed.",
            StaffRole.HeadAthleticTherapist => "No Athletic Therapist.",
            StaffRole.Scout => current == 0 ? "No regional scouts assigned." : $"Only {current} regional scout assigned.",
            StaffRole.TeamDoctor => "No Team Doctor attached to hockey operations.",
            StaffRole.HeadEquipmentManager => "No Equipment Manager employed.",
            _ => $"No {StaffRoles.Title(role)} employed."
        };

    private static IReadOnlyList<string> StrengthsFor(StaffRole role) =>
        StaffRoles.DepartmentFor(role) switch
        {
            StaffDepartment.Executive => new[] { "operations planning", "budget discipline" },
            StaffDepartment.Coaching => new[] { "player teaching", "practice standards" },
            StaffDepartment.Scouting => new[] { "live viewings", "regional contacts" },
            StaffDepartment.Medical => new[] { "injury prevention", "clear communication" },
            StaffDepartment.Equipment => new[] { "room organization", "travel preparation" },
            _ => new[] { "professional habits", "department fit" }
        };

    private static IReadOnlyList<string> WeaknessesFor(StaffRole role) =>
        StaffRoles.DepartmentFor(role) switch
        {
            StaffDepartment.Executive => new[] { "limited history with this owner" },
            StaffDepartment.Coaching => new[] { "needs roster familiarity" },
            StaffDepartment.Scouting => new[] { "network still being verified" },
            StaffDepartment.Medical => new[] { "junior hockey travel demands" },
            StaffDepartment.Equipment => new[] { "limited league-specific setup knowledge" },
            _ => new[] { "fit still being evaluated" }
        };

    private static string PersonalityFor(StaffRole role) =>
        StaffRoles.DepartmentFor(role) switch
        {
            StaffDepartment.Executive => "Detail-oriented hockey operations thinker with a steady communication style.",
            StaffDepartment.Coaching => "Teaching-first staffer who values role clarity and player growth.",
            StaffDepartment.Scouting => "Evidence-driven evaluator who prefers clear assignment priorities.",
            StaffDepartment.Medical => "Calm support staffer who prioritizes prevention and honest updates.",
            StaffDepartment.Equipment => "Practical organizer who keeps routines quiet and dependable.",
            _ => "Professional staff candidate with fit still under review."
        };

    private static string ChemistryRiskFor(StaffRole role, int index) =>
        index % 3 == 0
            ? $"Moderate risk; {StaffRoles.Title(role)} candidate expects defined authority."
            : $"Low risk; {StaffRoles.Title(role)} candidate profiles as collaborative.";

    private static StaffMember FindStaff(NewGmScenarioSnapshot scenario, string personId)
    {
        var matches = scenario.StaffMembers
            .Where(member => member.PersonId == personId)
            .ToArray();
        return matches.LastOrDefault(member => member.EmploymentStatus == StaffEmploymentStatus.Employed)
            ?? matches.LastOrDefault()
            ?? throw new ArgumentException("Staff member was not found.", nameof(personId));
    }

    private static NewGmScenarioSnapshot ReplaceStaff(NewGmScenarioSnapshot scenario, StaffMember member)
    {
        return scenario with
        {
            StaffMembers = UpsertStaff(scenario.StaffMembers, member),
            AlphaSnapshot = scenario.AlphaSnapshot with { StaffMembers = UpsertStaff(scenario.AlphaSnapshot.StaffMembers, member) }
        };
    }

    private static StaffMember[] UpsertStaff(IEnumerable<StaffMember> staffMembers, StaffMember member) =>
        staffMembers
            .Where(existing => existing.PersonId != member.PersonId)
            .Append(member)
            .ToArray();

    private static IReadOnlyList<string> Strengths(StaffMember member)
    {
        var values = RelevantValues(member).OrderByDescending(item => item.Value).Take(2).Select(item => item.Label).ToArray();
        return values.Length == 0 ? new[] { "professional habits" } : values;
    }

    private static IReadOnlyList<string> Weaknesses(StaffMember member)
    {
        var values = RelevantValues(member).OrderBy(item => item.Value).Take(2).Select(item => item.Label).ToArray();
        return values.Length == 0 ? new[] { "fit still being evaluated" } : values;
    }

    private static int DepartmentFit(StaffMember member)
    {
        var values = RelevantValues(member).ToArray();
        return values.Length == 0 ? member.Profile.Reputation : Math.Clamp((int)Math.Round(values.Average(item => item.Value)), 0, 100);
    }

    private static IEnumerable<(string Label, int Value)> RelevantValues(StaffMember member) =>
        member.Department switch
        {
            StaffDepartment.Coaching => member.Attributes.CoachingAttributes.Select(pair => (pair.Key.ToString(), pair.Value)),
            StaffDepartment.Scouting => member.Attributes.ScoutingAttributes.Select(pair => (pair.Key.ToString(), pair.Value)),
            StaffDepartment.Medical => member.Attributes.MedicalAttributes.Select(pair => (pair.Key.ToString(), pair.Value)),
            _ => Array.Empty<(string, int)>()
        };

    private static int RelationshipWithGm(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.Relationships
            .Where(relationship => relationship.FromPersonId == scenario.AlphaSnapshot.GeneralManager.PersonId && relationship.ToPersonId == personId)
            .Select(relationship => (relationship.Trust + relationship.Respect + relationship.Confidence + relationship.Loyalty) / 4)
            .DefaultIfEmpty(50)
            .First();

    private static string Name(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.People.SingleOrDefault(person => person.PersonId == personId)?.Identity.DisplayName ?? personId;

    private static string Label(int value) =>
        value >= 70 ? "strong" :
        value >= 55 ? "positive" :
        value >= 40 ? "mixed" :
        "strained";

    private static void QueueEvent(EngineRegistry registry, NewGmScenarioSnapshot scenario, LegacyEventType eventType, string title, string description, string? primaryPersonId)
    {
        var date = scenario.CurrentDate;
        var legacyEvent = registry.EventEngine.CreateEvent(
            new DateTimeOffset(date.Year, date.Month, date.Day, 10, 0, 0, TimeSpan.Zero),
            eventType,
            eventType == LegacyEventType.StaffConflictWarning ? LegacyEventSeverity.Warning : LegacyEventSeverity.Notice,
            LegacyEventVisibility.Organization,
            title,
            description,
            new LegacyEventContext(primaryPersonId, OrganizationId: scenario.Organization.OrganizationId, SeasonId: scenario.Season.SeasonId),
            new Dictionary<string, object?> { ["scenario"] = "alpha_2_1_staff_control" });
        registry.EventEngine.QueueEvent(legacyEvent);
    }

    private static AlphaInboxItem Inbox(NewGmScenarioSnapshot scenario, LegacyEventType eventType, string title, string summary, string? primaryPersonId, LegacyEventSeverity severity = LegacyEventSeverity.Notice) =>
        new(
            InboxItemId: $"inbox:staff-control:{Guid.NewGuid():N}",
            Date: new DateTimeOffset(scenario.CurrentDate.Year, scenario.CurrentDate.Month, scenario.CurrentDate.Day, 10, 30, 0, TimeSpan.Zero),
            EventType: eventType,
            Severity: severity,
            Title: title,
            Summary: summary,
            PrimaryPersonId: primaryPersonId);

    private static StaffOfficeResult Result(
        bool success,
        NewGmScenarioSnapshot scenario,
        StaffCandidate? candidate,
        StaffFocusAssignment? focus,
        StaffEvaluation? evaluation,
        StaffChemistryReport? chemistry,
        IReadOnlyList<AlphaInboxItem> inbox,
        string message)
    {
        return Result(success, scenario, candidate, focus, evaluation, chemistry, inbox, Array.Empty<LeagueTransaction>(), message);
    }

    private static StaffOfficeResult Result(
        bool success,
        NewGmScenarioSnapshot scenario,
        StaffCandidate? candidate,
        StaffFocusAssignment? focus,
        StaffEvaluation? evaluation,
        StaffChemistryReport? chemistry,
        IReadOnlyList<AlphaInboxItem> inbox,
        IReadOnlyList<LeagueTransaction> transactions,
        string message)
    {
        var result = new StaffOfficeResult(success, scenario, candidate, focus, evaluation, chemistry, inbox, transactions, message);
        result.Validate();
        return result;
    }
}
