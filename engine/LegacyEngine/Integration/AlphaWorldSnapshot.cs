using LegacyEngine.Draft;
using LegacyEngine.Development;
using LegacyEngine.Injuries;
using LegacyEngine.Contracts;
using LegacyEngine.Organizations;
using LegacyEngine.Owners;
using LegacyEngine.People;
using LegacyEngine.Recruiting;
using LegacyEngine.Relationships;
using LegacyEngine.Rosters;
using LegacyEngine.Scouting;
using LegacyEngine.Seasons;
using LegacyEngine.Staff;
using LegacyEngine.World;

namespace LegacyEngine.Integration;

public sealed record AlphaWorldSnapshot(
    WorldState WorldState,
    string OrganizationId,
    Owner Owner,
    Person GeneralManager,
    Scout Scout,
    Person ScoutPerson,
    Person? CoachPerson,
    IReadOnlyList<Person> People,
    IReadOnlyList<Person> Players,
    IReadOnlyList<RecruitProfile> Recruits,
    Roster Roster,
    DraftBoard DraftBoard,
    IReadOnlyList<Relationship> Relationships,
    IReadOnlyList<PlayerDevelopmentProfile> DevelopmentProfiles,
    IReadOnlyList<Injury> Injuries)
{
    public Organization? Organization { get; init; }

    public Season? Season { get; init; }

    public IReadOnlyList<StaffMember> StaffMembers { get; init; } = Array.Empty<StaffMember>();

    public IReadOnlyList<Contract> Contracts { get; init; } = Array.Empty<Contract>();

    public DateOnly CurrentDate => WorldState.CurrentDate.Value;

    public void Validate()
    {
        WorldState.Validate();
        Organization?.Validate();
        Season?.Validate();
        Owner.Validate();
        GeneralManager.Validate();
        Scout.Validate();
        ScoutPerson.Validate();
        CoachPerson?.Validate();
        Roster.Validate();
        DraftBoard.Validate();

        if (string.IsNullOrWhiteSpace(OrganizationId))
        {
            throw new ArgumentException("Organization id is required.", nameof(OrganizationId));
        }

        if (People.Count == 0)
        {
            throw new ArgumentException("Alpha world must contain people.", nameof(People));
        }

        if (Players.Count == 0)
        {
            throw new ArgumentException("Alpha world must contain players.", nameof(Players));
        }

        if (Recruits.Count == 0)
        {
            throw new ArgumentException("Alpha world must contain recruits.", nameof(Recruits));
        }

        foreach (var relationship in Relationships)
        {
            relationship.Validate();
        }

        foreach (var profile in DevelopmentProfiles)
        {
            profile.Validate();
        }

        foreach (var injury in Injuries)
        {
            injury.Validate();
        }

        foreach (var staffMember in StaffMembers)
        {
            staffMember.Validate();
        }

        foreach (var contract in Contracts)
        {
            contract.Validate();
        }
    }
}
