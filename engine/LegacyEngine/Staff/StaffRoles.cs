namespace LegacyEngine.Staff;

/// <summary>
/// Static catalog mapping each v1 <see cref="StaffRole"/> to its owning
/// <see cref="StaffDepartment"/> and a human-readable title. This is descriptive
/// organization data only; it does not encode league rules or gameplay logic.
/// </summary>
public static class StaffRoles
{
    public static StaffDepartment DepartmentFor(StaffRole role) => role switch
    {
        StaffRole.HeadCoach => StaffDepartment.Coaching,
        StaffRole.AssistantCoach => StaffDepartment.Coaching,
        StaffRole.GoalieCoach => StaffDepartment.Coaching,
        StaffRole.DevelopmentCoach => StaffDepartment.Coaching,
        StaffRole.VideoCoach => StaffDepartment.Coaching,
        StaffRole.SkillsCoach => StaffDepartment.Coaching,
        StaffRole.StrengthCoach => StaffDepartment.Coaching,
        StaffRole.HeadScout => StaffDepartment.Scouting,
        StaffRole.Scout => StaffDepartment.Scouting,
        StaffRole.DirectorOfScouting => StaffDepartment.Scouting,
        StaffRole.AthleticTherapist => StaffDepartment.Medical,
        StaffRole.TeamDoctor => StaffDepartment.Medical,
        StaffRole.EquipmentManager => StaffDepartment.Equipment,
        StaffRole.AssistantGM => StaffDepartment.Management,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown staff role.")
    };

    public static string Title(StaffRole role) => role switch
    {
        StaffRole.HeadCoach => "Head Coach",
        StaffRole.AssistantCoach => "Assistant Coach",
        StaffRole.GoalieCoach => "Goalie Coach",
        StaffRole.DevelopmentCoach => "Development Coach",
        StaffRole.VideoCoach => "Video Coach",
        StaffRole.SkillsCoach => "Skills Coach",
        StaffRole.StrengthCoach => "Strength Coach",
        StaffRole.HeadScout => "Head Scout",
        StaffRole.Scout => "Scout",
        StaffRole.DirectorOfScouting => "Director of Scouting",
        StaffRole.AthleticTherapist => "Athletic Therapist",
        StaffRole.TeamDoctor => "Team Doctor",
        StaffRole.EquipmentManager => "Equipment Manager",
        StaffRole.AssistantGM => "Assistant GM",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown staff role.")
    };
}
