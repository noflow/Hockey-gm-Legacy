using LegacyEngine.Events;
using LegacyEngine.Integration;
using LegacyEngine.Relationships;
using LegacyEngine.Staff;

internal sealed class StaffControlTests
{
    public void StaffCandidateGenerated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var result = new StaffOfficeService().GenerateCandidatePool(scenario.Registry, scenario.ScenarioSnapshot);

        Assert.True(result.Success, result.Message);
        Assert.True(result.ScenarioSnapshot.StaffCandidates.Count > 0, "Candidate pool should be generated.");
        Assert.False(string.IsNullOrWhiteSpace(result.ScenarioSnapshot.StaffCandidates.First().Person.Identity.DisplayName), "Candidate should have a display name.");
    }

    public void CandidateHasRoleAndDepartmentFit()
    {
        var result = GenerateCandidates();
        var candidate = result.ScenarioSnapshot.StaffCandidates.First();

        Assert.True(candidate.RoleFit > 0, "Candidate should have role fit.");
        Assert.True(candidate.DepartmentFit > 0, "Candidate should have department fit.");
    }

    public void CandidateHasStrengthsAndWeaknesses()
    {
        var candidate = GenerateCandidates().ScenarioSnapshot.StaffCandidates.First();

        Assert.True(candidate.Strengths.Count > 0, "Candidate strengths should be present.");
        Assert.True(candidate.Weaknesses.Count > 0, "Candidate weaknesses should be present.");
    }

    public void StaffCanBeHired()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var service = new StaffOfficeService();
        var generated = service.GenerateCandidatePool(scenario.Registry, scenario.ScenarioSnapshot);
        var candidate = generated.ScenarioSnapshot.StaffCandidates.First();
        var hired = service.HireCandidate(scenario.Registry, generated.ScenarioSnapshot, candidate.CandidateId);

        Assert.True(hired.Success, hired.Message);
        Assert.True(hired.ScenarioSnapshot.StaffMembers.Any(member => member.PersonId == candidate.Person.PersonId && member.EmploymentStatus == StaffEmploymentStatus.Employed), "Candidate should be hired.");
    }

    public void StaffCanBeReleased()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var staff = scenario.ScenarioSnapshot.StaffMembers.First(member => member.CurrentRole == StaffRole.AssistantCoach);
        var result = new StaffOfficeService().ReleaseStaff(scenario.Registry, scenario.ScenarioSnapshot, staff.PersonId, "Front office restructuring.");

        Assert.True(result.Success, result.Message);
        Assert.Equal(StaffEmploymentStatus.Released, result.ScenarioSnapshot.StaffMembers.Single(member => member.PersonId == staff.PersonId).EmploymentStatus);
    }

    public void StaffRoleCanBeChanged()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var staff = scenario.ScenarioSnapshot.StaffMembers.First(member => member.CurrentRole == StaffRole.AssistantCoach);
        var result = new StaffOfficeService().ReassignStaffRole(scenario.Registry, scenario.ScenarioSnapshot, staff.PersonId, StaffRole.DevelopmentCoach);

        Assert.True(result.Success, result.Message);
        Assert.Equal(StaffRole.DevelopmentCoach, result.ScenarioSnapshot.StaffMembers.Single(member => member.PersonId == staff.PersonId).CurrentRole);
    }

    public void DevelopmentCoachFocusCanBeSet()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var service = new StaffOfficeService();
        var coach = scenario.ScenarioSnapshot.StaffMembers.First(member => member.Department == StaffDepartment.Coaching);
        var result = service.SetDevelopmentCoachFocus(scenario.Registry, scenario.ScenarioSnapshot, coach.PersonId, DevelopmentCoachFocus.Skating);

        Assert.True(result.Success, result.Message);
        Assert.Equal("Skating", result.FocusAssignment!.Focus);
    }

    public void MedicalStaffFocusCanBeSet()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var service = new StaffOfficeService();
        var generated = service.GenerateCandidatePool(scenario.Registry, scenario.ScenarioSnapshot);
        var medical = generated.ScenarioSnapshot.StaffCandidates.First(candidate => candidate.StaffMember.Department == StaffDepartment.Medical);
        var hired = service.HireCandidate(scenario.Registry, generated.ScenarioSnapshot, medical.CandidateId);
        var result = service.SetMedicalStaffFocus(scenario.Registry, hired.ScenarioSnapshot, medical.Person.PersonId, MedicalStaffFocus.InjuryPrevention);

        Assert.True(result.Success, result.Message);
        Assert.Equal("InjuryPrevention", result.FocusAssignment!.Focus);
    }

    public void ScoutingDepartmentFocusCanBeSet()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var scout = scenario.ScenarioSnapshot.StaffMembers.First(member => member.Department == StaffDepartment.Scouting);
        var result = new StaffOfficeService().SetScoutingDepartmentFocus(scenario.Registry, scenario.ScenarioSnapshot, scout.PersonId, ScoutingDepartmentFocus.WesternCanada);

        Assert.True(result.Success, result.Message);
        Assert.Equal("WesternCanada", result.FocusAssignment!.Focus);
    }

    public void StaffEvaluationGenerated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var staff = scenario.ScenarioSnapshot.StaffMembers.First();
        var result = new StaffOfficeService().GenerateStaffEvaluation(scenario.Registry, scenario.ScenarioSnapshot, staff.PersonId);

        Assert.True(result.Success, result.Message);
        Assert.True(result.ScenarioSnapshot.StaffEvaluations.Any(item => item.PersonId == staff.PersonId), "Staff evaluation should be stored.");
    }

    public void StaffProfilesTolerateDuplicatePersonIds()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var duplicate = scenario.ScenarioSnapshot.StaffMembers.First(member => member.EmploymentStatus == StaffEmploymentStatus.Employed);
        var snapshot = scenario.ScenarioSnapshot with
        {
            StaffMembers = scenario.ScenarioSnapshot.StaffMembers.Append(duplicate).ToArray(),
            AlphaSnapshot = scenario.ScenarioSnapshot.AlphaSnapshot with
            {
                StaffMembers = scenario.ScenarioSnapshot.AlphaSnapshot.StaffMembers.Append(duplicate).ToArray()
            }
        };

        var profiles = new StaffOfficeService().BuildStaffProfiles(snapshot, scenario.Registry.Rulebook!);

        Assert.Equal(1, profiles.Count(profile => profile.PersonId == duplicate.PersonId));
    }

    public void ChemistryWarningGenerated()
    {
        var scenario = WithPoorGmRelationship(NewGmScenarioBootstrapper.CreateScenario());
        var result = new StaffOfficeService().GenerateChemistryWarning(scenario.Registry, scenario.ScenarioSnapshot);

        Assert.True(result.InboxItems.Any(item => item.EventType == LegacyEventType.StaffConflictWarning), "Chemistry warning should create inbox.");
    }

    public void RelationshipAffectsChemistry()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var staff = scenario.ScenarioSnapshot.StaffMembers.First(member => member.Department == StaffDepartment.Scouting);
        var service = new StaffOfficeService();
        var normal = service.EvaluateChemistry(scenario.ScenarioSnapshot, staff.PersonId);
        var poorScenario = WithPoorGmRelationship(scenario, staff.PersonId);
        var poor = service.EvaluateChemistry(poorScenario.ScenarioSnapshot, staff.PersonId);

        Assert.True(poor.GmFit < normal.GmFit, "Lower relationship values should lower chemistry.");
        Assert.True(poor.ConflictWarnings.Count > 0, "Poor relationship should create a warning.");
    }

    public void EventsCreated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var service = new StaffOfficeService();
        var generated = service.GenerateCandidatePool(scenario.Registry, scenario.ScenarioSnapshot);
        var coach = generated.ScenarioSnapshot.StaffMembers.First(member => member.Department == StaffDepartment.Coaching);
        var focused = service.SetDevelopmentCoachFocus(scenario.Registry, generated.ScenarioSnapshot, coach.PersonId, DevelopmentCoachFocus.Confidence);
        service.GenerateStaffEvaluation(scenario.Registry, focused.ScenarioSnapshot, coach.PersonId);

        var events = scenario.Registry.EventEngine.Queue.PendingEvents.Select(item => item.EventType).ToArray();
        Assert.True(events.Contains(LegacyEventType.StaffCandidateGenerated), "Candidate event should be queued.");
        Assert.True(events.Contains(LegacyEventType.StaffFocusChanged), "Focus event should be queued.");
        Assert.True(events.Contains(LegacyEventType.StaffEvaluationCreated), "Evaluation event should be queued.");
    }

    public void InboxMessagesCreated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var service = new StaffOfficeService();
        var generated = service.GenerateCandidatePool(scenario.Registry, scenario.ScenarioSnapshot);
        var coach = generated.ScenarioSnapshot.StaffMembers.First(member => member.Department == StaffDepartment.Coaching);
        var focused = service.SetDevelopmentCoachFocus(scenario.Registry, generated.ScenarioSnapshot, coach.PersonId, DevelopmentCoachFocus.WorkEthic);
        var evaluated = service.GenerateStaffEvaluation(scenario.Registry, focused.ScenarioSnapshot, coach.PersonId);

        Assert.True(generated.InboxItems.Count > 0, "Candidate generation should create inbox.");
        Assert.True(focused.InboxItems.Count > 0, "Focus change should create inbox.");
        Assert.True(evaluated.InboxItems.Count > 0, "Evaluation should create inbox.");
        Assert.Equal(InboxCategory.Staff, InboxManager.Categorize(evaluated.InboxItems.First()));
    }

    public void AlphaDesktopExposesStaffControls()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Candidate Pool", StringComparison.Ordinal), "Desktop should expose candidate list.");
        Assert.True(source.Contains("Development Focus", StringComparison.Ordinal), "Desktop should expose development focus action.");
        Assert.True(source.Contains("Medical Focus", StringComparison.Ordinal), "Desktop should expose medical focus action.");
        Assert.True(source.Contains("Staff Evaluation", StringComparison.Ordinal), "Desktop should expose evaluation action.");
    }

    public void NoGodotSaveOrGameSimulation()
    {
        var files = Directory.GetFiles(Path.Combine(FindRepositoryRoot(), "engine", "LegacyEngine", "Integration"), "StaffOffice*.cs");
        var text = string.Join('\n', files.Select(File.ReadAllText));

        Assert.False(text.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Staff Control must not depend on Godot.");
        Assert.False(text.Contains("SaveGame", StringComparison.OrdinalIgnoreCase), "Staff Control must not build save/load.");
        Assert.False(text.Contains("GameSimulation", StringComparison.OrdinalIgnoreCase), "Staff Control must not build game simulation.");
    }

    private static StaffOfficeResult GenerateCandidates()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        return new StaffOfficeService().GenerateCandidatePool(scenario.Registry, scenario.ScenarioSnapshot);
    }

    private static NewGmScenarioResult WithPoorGmRelationship(NewGmScenarioResult scenario, string? staffPersonId = null)
    {
        staffPersonId ??= scenario.ScenarioSnapshot.StaffMembers.First(member => member.Department == StaffDepartment.Scouting).PersonId;
        var relationships = scenario.ScenarioSnapshot.AlphaSnapshot.Relationships
            .Select(relationship =>
                relationship.FromPersonId == scenario.ScenarioSnapshot.AlphaSnapshot.GeneralManager.PersonId
                && relationship.ToPersonId == staffPersonId
                    ? relationship with { Trust = 20, Respect = 25, Confidence = 20, Loyalty = 30 }
                    : relationship)
            .ToArray();
        var snapshot = scenario.ScenarioSnapshot.AlphaSnapshot with { Relationships = relationships };
        return scenario with { ScenarioSnapshot = scenario.ScenarioSnapshot with { AlphaSnapshot = snapshot } };
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HockeyGmLegacy.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find Hockey GM Legacy repository root.");
    }
}
