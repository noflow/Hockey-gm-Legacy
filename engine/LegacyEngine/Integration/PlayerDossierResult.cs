namespace LegacyEngine.Integration;

public sealed record PlayerDossierResult(
    NewGmScenarioSnapshot ScenarioSnapshot,
    PlayerDossierView Dossier,
    string Message)
{
    public void Validate()
    {
        ScenarioSnapshot.Validate();
        Dossier.Validate();

        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Dossier result message is required.", nameof(Message));
        }
    }
}
