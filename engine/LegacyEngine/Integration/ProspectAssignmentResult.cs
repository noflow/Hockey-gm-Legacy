namespace LegacyEngine.Integration;

public sealed record ProspectAssignmentResult(
    bool Success,
    NewGmScenarioSnapshot ScenarioSnapshot,
    PlayerPipelineRecord? PipelineRecord,
    PlayerAssignmentEligibility Eligibility,
    string Message)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        PipelineRecord?.Validate();
        Eligibility.Validate();

        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Prospect assignment result message is required.", nameof(Message));
        }
    }
}
