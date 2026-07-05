using LegacyEngine.Events;
using LegacyEngine.Staff;

internal sealed class StaffEngineTests
{
    public void StaffCreation()
    {
        var member = new StaffEngine().CreateStaffMember("person-001", "org-001", StaffRole.HeadCoach, yearsExperience: 12, reputation: 70);

        member.Validate();
        Assert.Equal("person-001", member.PersonId);
        Assert.Equal("org-001", member.OrganizationId);
        Assert.Equal(StaffRole.HeadCoach, member.CurrentRole);
        Assert.Equal(StaffDepartment.Coaching, member.Department);
        Assert.Equal(StaffEmploymentStatus.Prospective, member.EmploymentStatus);
        Assert.Equal(0, member.Assignments.Count);
    }

    public void RoleAssignment()
    {
        var engine = new StaffEngine();
        var member = engine.Hire(engine.CreateStaffMember("person-001", "org-001", StaffRole.AssistantCoach), new DateOnly(2026, 7, 1));

        var assigned = engine.AssignRole(member, StaffRole.AssistantCoach, new DateOnly(2026, 7, 2));

        Assert.Equal(1, assigned.Assignments.Count);
        Assert.True(assigned.CurrentAssignment is not null, "Assigned staff should have an active assignment.");
        Assert.Equal(StaffRole.AssistantCoach, assigned.CurrentAssignment!.Role);
        Assert.Equal(StaffDepartment.Coaching, assigned.CurrentAssignment!.Department);
    }

    public void RoleReassignment()
    {
        var engine = new StaffEngine();
        var member = engine.Hire(engine.CreateStaffMember("person-001", "org-001", StaffRole.AssistantCoach), new DateOnly(2026, 7, 1));
        member = engine.AssignRole(member, StaffRole.AssistantCoach, new DateOnly(2026, 7, 2));

        var reassigned = engine.ReassignRole(member, StaffRole.HeadCoach, new DateOnly(2026, 9, 1));

        Assert.Equal(StaffRole.HeadCoach, reassigned.CurrentRole);
        Assert.Equal(2, reassigned.Assignments.Count);
        Assert.Equal(1, reassigned.Assignments.Count(assignment => assignment.IsActive));
        Assert.Equal(new DateOnly(2026, 9, 1), reassigned.CurrentAssignment!.StartDate);

        var previous = reassigned.Assignments.Single(assignment => assignment.Role == StaffRole.AssistantCoach);
        Assert.Equal(new DateOnly(2026, 9, 1), previous.EndDate!.Value);
    }

    public void Hiring()
    {
        var engine = new StaffEngine();
        var member = engine.CreateStaffMember("person-001", "org-001", StaffRole.GoalieCoach);

        var hired = engine.Hire(member, new DateOnly(2026, 7, 1));

        Assert.Equal(StaffEmploymentStatus.Employed, hired.EmploymentStatus);
        Assert.True(
            engine.EventEngine.Queue.PendingEvents.Any(item => item.EventType == LegacyEventType.StaffHired),
            "Hiring should queue a StaffHired event.");
    }

    public void Releasing()
    {
        var engine = new StaffEngine();
        var member = engine.Hire(engine.CreateStaffMember("person-001", "org-001", StaffRole.Scout), new DateOnly(2026, 7, 1));
        member = engine.AssignRole(member, StaffRole.Scout, new DateOnly(2026, 7, 2));

        var released = engine.RemoveStaffMember(member, new DateOnly(2027, 6, 30), "End of season restructuring.");

        Assert.Equal(StaffEmploymentStatus.Released, released.EmploymentStatus);
        Assert.True(released.CurrentAssignment is null, "Released staff should have no active assignment.");
        Assert.True(
            engine.EventEngine.Queue.PendingEvents.Any(item => item.EventType == LegacyEventType.StaffReleased),
            "Releasing should queue a StaffReleased event.");
    }

    public void EvaluationGeneration()
    {
        var engine = new StaffEngine();
        var attributes = StaffAttributes.ForCoaching(new Dictionary<StaffCoachingAttribute, int>
        {
            [StaffCoachingAttribute.Teaching] = 85,
            [StaffCoachingAttribute.Tactics] = 80,
            [StaffCoachingAttribute.Communication] = 30,
            [StaffCoachingAttribute.Leadership] = 60,
            [StaffCoachingAttribute.Development] = 75,
            [StaffCoachingAttribute.Discipline] = 35,
            [StaffCoachingAttribute.Motivation] = 70,
            [StaffCoachingAttribute.Adaptability] = 65
        });
        var member = engine.CreateStaffMember("person-001", "org-001", StaffRole.HeadCoach, attributes: attributes);

        var evaluation = engine.EvaluateStaff(member, new DateOnly(2026, 12, 1));

        evaluation.Validate();
        Assert.True(evaluation.OverallEvaluation is >= 55 and <= 70, "Overall evaluation should reflect mixed attributes.");
        Assert.Equal(StaffRecommendation.Retain, evaluation.Recommendation);
        Assert.Contains("Teaching", evaluation.Strengths);
        Assert.Contains("Communication", evaluation.Weaknesses);
        Assert.True(evaluation.DevelopmentSuggestions.Count > 0, "Evaluation should include development suggestions.");
    }

    public void AttributeStorage()
    {
        var attributes = new StaffAttributes(
            new Dictionary<StaffCoachingAttribute, int> { [StaffCoachingAttribute.Teaching] = 90 },
            new Dictionary<StaffScoutingAttribute, int> { [StaffScoutingAttribute.TalentEvaluation] = 88 },
            new Dictionary<StaffMedicalAttribute, int> { [StaffMedicalAttribute.Rehabilitation] = 77 });
        var member = new StaffEngine().CreateStaffMember("person-001", "org-001", StaffRole.DevelopmentCoach, attributes: attributes);

        member.Validate();
        Assert.Equal(90, member.Attributes.CoachingScore(StaffCoachingAttribute.Teaching));
        Assert.Equal(88, member.Attributes.ScoutingScore(StaffScoutingAttribute.TalentEvaluation));
        Assert.Equal(77, member.Attributes.MedicalScore(StaffMedicalAttribute.Rehabilitation));
        Assert.Equal(0, member.Attributes.CoachingScore(StaffCoachingAttribute.Tactics));
    }

    public void DepartmentAssignment()
    {
        Assert.Equal(StaffDepartment.Coaching, StaffEngine.DepartmentFor(StaffRole.HeadCoach));
        Assert.Equal(StaffDepartment.Scouting, StaffEngine.DepartmentFor(StaffRole.DirectorOfScouting));
        Assert.Equal(StaffDepartment.Medical, StaffEngine.DepartmentFor(StaffRole.TeamDoctor));
        Assert.Equal(StaffDepartment.Equipment, StaffEngine.DepartmentFor(StaffRole.EquipmentManager));
        Assert.Equal(StaffDepartment.Management, StaffEngine.DepartmentFor(StaffRole.AssistantGM));

        var scout = new StaffEngine().CreateStaffMember("person-002", "org-001", StaffRole.HeadScout);
        Assert.Equal(StaffDepartment.Scouting, scout.Department);
    }

    public void ContractReference()
    {
        var contract = new StaffContractReference("contract-777", "org-001", new DateOnly(2026, 7, 1), new DateOnly(2027, 6, 30));
        contract.Validate();

        var engine = new StaffEngine();
        var member = engine.CreateStaffMember("person-001", "org-001", StaffRole.TeamDoctor);
        var hired = engine.Hire(member, new DateOnly(2026, 7, 1), contract);

        Assert.Equal("contract-777", hired.ContractId);
        Assert.Throws<ArgumentException>(() => new StaffContractReference(string.Empty, "org-001").Validate());
    }

    public void EventCreation()
    {
        var eventEngine = new EventEngine();
        var engine = new StaffEngine(eventEngine);

        var member = engine.CreateStaffMember("person-001", "org-001", StaffRole.AssistantCoach);
        member = engine.Hire(member, new DateOnly(2026, 7, 1));
        member = engine.AssignRole(member, StaffRole.AssistantCoach, new DateOnly(2026, 7, 2));
        member = engine.ReassignRole(member, StaffRole.HeadCoach, new DateOnly(2026, 9, 1));
        engine.EvaluateStaff(member, new DateOnly(2026, 12, 1));
        engine.RemoveStaffMember(member, new DateOnly(2027, 6, 30));

        var pending = eventEngine.Queue.PendingEvents;
        Assert.True(pending.Any(item => item.EventType == LegacyEventType.StaffHired), "StaffHired event should be queued.");
        Assert.True(pending.Any(item => item.EventType == LegacyEventType.StaffAssigned), "StaffAssigned event should be queued.");
        Assert.True(pending.Any(item => item.EventType == LegacyEventType.StaffReassigned), "StaffReassigned event should be queued.");
        Assert.True(pending.Any(item => item.EventType == LegacyEventType.StaffEvaluated), "StaffEvaluated event should be queued.");
        Assert.True(pending.Any(item => item.EventType == LegacyEventType.StaffReleased), "StaffReleased event should be queued.");
    }

    public void NoUiOrGodotDependencyExists()
    {
        var staffFiles = Directory.GetFiles(
            Path.Combine(FindRepositoryRoot(), "engine", "LegacyEngine", "Staff"),
            "*.cs",
            SearchOption.AllDirectories);

        foreach (var file in staffFiles)
        {
            var text = File.ReadAllText(file);
            Assert.False(text.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Staff module should not reference Godot.");
            Assert.False(text.Contains("Control", StringComparison.Ordinal), "Staff module should not define UI controls.");
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var rulebookPath = Path.Combine(directory.FullName, "data", "rulebooks", "junior_v1.json");
            if (File.Exists(rulebookPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find Hockey GM Legacy repository root.");
    }
}
