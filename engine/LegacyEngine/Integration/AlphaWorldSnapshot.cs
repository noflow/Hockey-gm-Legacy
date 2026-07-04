using LegacyEngine.Draft;
using LegacyEngine.Owners;
using LegacyEngine.People;
using LegacyEngine.Recruiting;
using LegacyEngine.Rosters;
using LegacyEngine.Scouting;
using LegacyEngine.World;

namespace LegacyEngine.Integration;

public sealed record AlphaWorldSnapshot(
    WorldState WorldState,
    string OrganizationId,
    Owner Owner,
    Person GeneralManager,
    Scout Scout,
    Person ScoutPerson,
    IReadOnlyList<Person> People,
    IReadOnlyList<Person> Players,
    IReadOnlyList<RecruitProfile> Recruits,
    Roster Roster,
    DraftBoard DraftBoard)
{
    public DateOnly CurrentDate => WorldState.CurrentDate.Value;

    public void Validate()
    {
        WorldState.Validate();
        Owner.Validate();
        GeneralManager.Validate();
        Scout.Validate();
        ScoutPerson.Validate();
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
    }
}
