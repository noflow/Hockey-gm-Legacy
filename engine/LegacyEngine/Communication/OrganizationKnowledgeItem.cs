namespace LegacyEngine.Communication;

public sealed record OrganizationKnowledgeItem(
    string KnowledgeId,
    string OrganizationId,
    DateOnly LearnedOn,
    string Topic,
    string Detail,
    RumorConfidence Confidence,
    string? SubjectPersonId = null,
    string? SourceMessageId = null,
    string? SourceRumorId = null,
    string? SourceEventId = null)
{
    public bool IsVerified => Confidence == RumorConfidence.Confirmed;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(KnowledgeId))
        {
            throw new ArgumentException("Knowledge id is required.", nameof(KnowledgeId));
        }

        if (string.IsNullOrWhiteSpace(OrganizationId))
        {
            throw new ArgumentException("Knowledge organization id is required.", nameof(OrganizationId));
        }

        if (string.IsNullOrWhiteSpace(Topic))
        {
            throw new ArgumentException("Knowledge topic is required.", nameof(Topic));
        }

        if (string.IsNullOrWhiteSpace(Detail))
        {
            throw new ArgumentException("Knowledge detail is required.", nameof(Detail));
        }
    }
}
