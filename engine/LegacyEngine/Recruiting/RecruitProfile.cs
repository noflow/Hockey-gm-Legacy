namespace LegacyEngine.Recruiting;

public sealed record RecruitProfile(
    string RecruitPersonId,
    RecruitStatus Status,
    IReadOnlyDictionary<RecruitPriority, int> Priorities,
    IReadOnlyList<RecruitInterest> Interests,
    IReadOnlyList<RecruitingPitch> Pitches,
    IReadOnlyList<RecruitingPromise> Promises,
    IReadOnlyList<RecruitingVisit> Visits)
{
    public static RecruitProfile Create(
        string recruitPersonId,
        IReadOnlyDictionary<RecruitPriority, int> priorities)
    {
        var profile = new RecruitProfile(
            RecruitPersonId: recruitPersonId,
            Status: RecruitStatus.Available,
            Priorities: priorities,
            Interests: Array.Empty<RecruitInterest>(),
            Pitches: Array.Empty<RecruitingPitch>(),
            Promises: Array.Empty<RecruitingPromise>(),
            Visits: Array.Empty<RecruitingVisit>());

        profile.Validate();
        return profile;
    }

    public int GetInterest(string organizationId) =>
        Interests.SingleOrDefault(interest => interest.OrganizationId == organizationId)?.Value ?? 0;

    public RecruitProfile SetStatus(RecruitStatus status) => this with { Status = status };

    public RecruitProfile ChangeInterest(string organizationId, int amount, DateOnly date)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            throw new ArgumentException("Organization id is required.", nameof(organizationId));
        }

        var existing = Interests.SingleOrDefault(interest => interest.OrganizationId == organizationId);
        var updatedInterest = existing is null
            ? new RecruitInterest(organizationId, Math.Clamp(amount, 0, 100), date)
            : existing.Change(amount, date);

        var interests = Interests
            .Where(interest => interest.OrganizationId != organizationId)
            .Append(updatedInterest)
            .OrderBy(interest => interest.OrganizationId, StringComparer.Ordinal)
            .ToArray();

        var status = Status == RecruitStatus.Available && updatedInterest.Value > 0
            ? RecruitStatus.Interested
            : Status;

        return this with { Interests = interests, Status = status };
    }

    public RecruitProfile AddPitch(RecruitingPitch pitch)
    {
        pitch.Validate();
        EnsureUnique(Pitches.Select(item => item.PitchId), pitch.PitchId, "Pitch ids must be unique.");

        var interestDelta = Math.Max(1, pitch.AverageFit / 10);
        return ChangeInterest(pitch.OrganizationId, interestDelta, pitch.Date)
            with { Pitches = Pitches.Append(pitch).OrderBy(item => item.Date).ToArray() };
    }

    public RecruitProfile AddPromise(RecruitingPromise promise)
    {
        promise.Validate();
        EnsureUnique(Promises.Select(item => item.PromiseId), promise.PromiseId, "Promise ids must be unique.");

        var interestDelta = Math.Max(1, promise.Strength / 12);
        return ChangeInterest(promise.OrganizationId, interestDelta, promise.Date)
            with { Promises = Promises.Append(promise).OrderBy(item => item.Date).ToArray() };
    }

    public RecruitProfile AddVisit(RecruitingVisit visit)
    {
        visit.Validate();
        EnsureUnique(Visits.Select(item => item.VisitId), visit.VisitId, "Visit ids must be unique.");

        var interestDelta = Math.Max(1, visit.FitScore / 8);
        return ChangeInterest(visit.OrganizationId, interestDelta, visit.Date)
            with { Visits = Visits.Append(visit).OrderBy(item => item.Date).ToArray() };
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(RecruitPersonId))
        {
            throw new ArgumentException("Recruit person id is required.", nameof(RecruitPersonId));
        }

        if (Priorities.Count == 0)
        {
            throw new ArgumentException("Recruit profile must include priorities.", nameof(Priorities));
        }

        foreach (var priorityValue in Priorities.Values)
        {
            if (priorityValue is < 0 or > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(Priorities), "Recruit priority values must be between 0 and 100.");
            }
        }

        foreach (var interest in Interests)
        {
            interest.Validate();
        }

        foreach (var pitch in Pitches)
        {
            pitch.Validate();
        }

        foreach (var promise in Promises)
        {
            promise.Validate();
        }

        foreach (var visit in Visits)
        {
            visit.Validate();
        }
    }

    private static void EnsureUnique(IEnumerable<string> existingIds, string newId, string message)
    {
        if (existingIds.Contains(newId, StringComparer.Ordinal))
        {
            throw new ArgumentException(message, nameof(newId));
        }
    }
}
