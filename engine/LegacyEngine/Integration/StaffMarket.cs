namespace LegacyEngine.Integration;

public sealed record StaffMarket(
    string MarketId,
    DateOnly LastUpdated,
    IReadOnlyList<StaffMarketCandidate> Candidates,
    IReadOnlyList<StaffMovementRecord> MovementHistory)
{
    public IReadOnlyList<StaffMarketCandidate> AvailableCandidates =>
        Candidates
            .Where(candidate => candidate.Status is StaffMarketStatus.Available or StaffMarketStatus.Interested)
            .ToArray();

    public StaffMarketCandidate? FindByCandidateId(string candidateId) =>
        Candidates.FirstOrDefault(candidate => candidate.CandidateId == candidateId || candidate.MarketCandidateId == candidateId);

    public StaffMarketCandidate? FindByPersonId(string personId) =>
        Candidates.FirstOrDefault(candidate => candidate.PersonId == personId);

    public StaffMarket Replace(StaffMarketCandidate candidate) =>
        this with
        {
            Candidates = Candidates
                .Select(existing => existing.MarketCandidateId == candidate.MarketCandidateId ? candidate : existing)
                .ToArray()
        };

    public StaffMarket AddOrReplace(StaffMarketCandidate candidate) =>
        Candidates.Any(existing => existing.MarketCandidateId == candidate.MarketCandidateId)
            ? Replace(candidate)
            : this with { Candidates = Candidates.Append(candidate).ToArray() };

    public StaffMarket AddMovement(StaffMovementRecord record) =>
        this with
        {
            LastUpdated = record.Date,
            MovementHistory = MovementHistory.Append(record).ToArray()
        };

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(MarketId))
        {
            throw new ArgumentException("Staff market id is required.", nameof(MarketId));
        }

        foreach (var candidate in Candidates)
        {
            candidate.Validate();
        }

        foreach (var movement in MovementHistory)
        {
            movement.Validate();
        }
    }
}
