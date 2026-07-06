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
        StaffRole.GeneralManager => StaffDepartment.Executive,
        StaffRole.AssistantGM => StaffDepartment.Executive,
        StaffRole.DirectorOfHockeyOperations => StaffDepartment.Executive,
        StaffRole.HeadCoach => StaffDepartment.Coaching,
        StaffRole.AssistantCoach => StaffDepartment.Coaching,
        StaffRole.GoalieCoach => StaffDepartment.Coaching,
        StaffRole.GoaltendingCoach => StaffDepartment.Coaching,
        StaffRole.DevelopmentCoach => StaffDepartment.Coaching,
        StaffRole.VideoCoach => StaffDepartment.Coaching,
        StaffRole.SkillsCoach => StaffDepartment.Coaching,
        StaffRole.StrengthCoach => StaffDepartment.Coaching,
        StaffRole.StrengthConditioningCoach => StaffDepartment.Coaching,
        StaffRole.HeadScout => StaffDepartment.Scouting,
        StaffRole.RegionalScout => StaffDepartment.Scouting,
        StaffRole.AmateurScout => StaffDepartment.Scouting,
        StaffRole.ProfessionalScout => StaffDepartment.Scouting,
        StaffRole.EuropeanScout => StaffDepartment.Scouting,
        StaffRole.GoaltendingScout => StaffDepartment.Scouting,
        StaffRole.Scout => StaffDepartment.Scouting,
        StaffRole.DirectorOfScouting => StaffDepartment.Scouting,
        StaffRole.HeadAthleticTherapist => StaffDepartment.Medical,
        StaffRole.AthleticTherapist => StaffDepartment.Medical,
        StaffRole.AssistantTrainer => StaffDepartment.Medical,
        StaffRole.TeamDoctor => StaffDepartment.Medical,
        StaffRole.Physiotherapist => StaffDepartment.Medical,
        StaffRole.MassageTherapist => StaffDepartment.Medical,
        StaffRole.HeadEquipmentManager => StaffDepartment.Equipment,
        StaffRole.EquipmentManager => StaffDepartment.Equipment,
        StaffRole.AssistantEquipmentManager => StaffDepartment.Equipment,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown staff role.")
    };

    public static string Title(StaffRole role) => role switch
    {
        StaffRole.GeneralManager => "General Manager",
        StaffRole.AssistantGM => "Assistant GM",
        StaffRole.DirectorOfHockeyOperations => "Director of Hockey Operations",
        StaffRole.HeadCoach => "Head Coach",
        StaffRole.AssistantCoach => "Assistant Coach",
        StaffRole.GoalieCoach => "Goalie Coach",
        StaffRole.GoaltendingCoach => "Goaltending Coach",
        StaffRole.DevelopmentCoach => "Development Coach",
        StaffRole.VideoCoach => "Video Coach",
        StaffRole.SkillsCoach => "Skills Coach",
        StaffRole.StrengthCoach => "Strength Coach",
        StaffRole.StrengthConditioningCoach => "Strength & Conditioning Coach",
        StaffRole.HeadScout => "Head Scout",
        StaffRole.RegionalScout => "Regional Scout",
        StaffRole.AmateurScout => "Amateur Scout",
        StaffRole.ProfessionalScout => "Professional Scout",
        StaffRole.EuropeanScout => "European Scout",
        StaffRole.GoaltendingScout => "Goaltending Scout",
        StaffRole.Scout => "Scout",
        StaffRole.DirectorOfScouting => "Director of Scouting",
        StaffRole.HeadAthleticTherapist => "Head Athletic Therapist",
        StaffRole.AthleticTherapist => "Athletic Therapist",
        StaffRole.AssistantTrainer => "Assistant Trainer",
        StaffRole.TeamDoctor => "Team Doctor",
        StaffRole.Physiotherapist => "Physiotherapist",
        StaffRole.MassageTherapist => "Massage Therapist",
        StaffRole.HeadEquipmentManager => "Head Equipment Manager",
        StaffRole.EquipmentManager => "Equipment Manager",
        StaffRole.AssistantEquipmentManager => "Assistant Equipment Manager",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown staff role.")
    };
}
