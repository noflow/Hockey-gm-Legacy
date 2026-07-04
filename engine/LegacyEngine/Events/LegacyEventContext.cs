namespace LegacyEngine.Events;

public sealed record LegacyEventContext(
    string? PrimaryPersonId = null,
    string? SecondaryPersonId = null,
    string? OrganizationId = null,
    string? LeagueId = null,
    string? SeasonId = null,
    string? GameId = null,
    string? RelationshipId = null,
    string? RulebookId = null)
{
    public bool HasPerson(string personId) =>
        !string.IsNullOrWhiteSpace(personId)
        && (PrimaryPersonId == personId || SecondaryPersonId == personId);
}
