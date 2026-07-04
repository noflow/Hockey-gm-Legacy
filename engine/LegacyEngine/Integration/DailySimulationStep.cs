namespace LegacyEngine.Integration;

public enum DailySimulationStep
{
    AdvanceWorldClock = 1,
    ProcessQueuedEvents = 2,
    ApplyRelationshipDecay = 3,
    ApplyPlayerDevelopmentUpdates = 4,
    ApplyInjuryRecoveryUpdates = 5,
    CheckContractStatuses = 6,
    ProgressRecruiting = 7,
    GenerateCommunicationMessages = 8,
    ConvertInboxItems = 9,
    ReturnSimulationResult = 10
}
