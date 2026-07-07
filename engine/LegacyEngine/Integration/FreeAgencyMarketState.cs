namespace LegacyEngine.Integration;

public sealed record FreeAgencyMarketState(
    FreeAgencyWindow Window,
    IReadOnlyList<FreeAgentMotivationProfile> MotivationProfiles,
    IReadOnlyList<FreeAgencyCompetition> Competitions,
    IReadOnlyList<FreeAgencyOfferState> OfferStates,
    IReadOnlyList<FreeAgencyMarketUpdate> Updates)
{
    public static FreeAgencyMarketState Empty(FreeAgencyWindow window) =>
        new(window, Array.Empty<FreeAgentMotivationProfile>(), Array.Empty<FreeAgencyCompetition>(), Array.Empty<FreeAgencyOfferState>(), Array.Empty<FreeAgencyMarketUpdate>());

    public FreeAgencyOfferState? FindOffer(string personId) =>
        OfferStates
            .Where(offer => offer.PersonId == personId)
            .OrderByDescending(offer => offer.SubmittedOn)
            .ThenByDescending(offer => offer.OfferStateId, StringComparer.Ordinal)
            .FirstOrDefault();

    public FreeAgentMotivationProfile? FindMotivations(string personId) =>
        MotivationProfiles.FirstOrDefault(profile => profile.PersonId == personId);

    public IReadOnlyList<FreeAgencyCompetition> ActiveCompetitions(string personId) =>
        Competitions
            .Where(competition => competition.PersonId == personId && competition.IsActive)
            .OrderByDescending(competition => competition.PlayerInterest)
            .ThenBy(competition => competition.TeamName, StringComparer.Ordinal)
            .ToArray();

    public void Validate()
    {
        Window.Validate();
        foreach (var profile in MotivationProfiles)
        {
            profile.Validate();
        }

        foreach (var competition in Competitions)
        {
            competition.Validate();
        }

        foreach (var offer in OfferStates)
        {
            offer.Validate();
        }

        foreach (var update in Updates)
        {
            update.Validate();
        }
    }
}
