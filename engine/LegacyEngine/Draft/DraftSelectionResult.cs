using LegacyEngine.Events;

namespace LegacyEngine.Draft;

public sealed record DraftSelectionResult(
    Draft Draft,
    DraftPick Pick,
    DraftSelection Selection,
    LegacyEvent CreatedEvent,
    string Message);
