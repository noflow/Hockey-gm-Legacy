using LegacyEngine.Contracts;
using LegacyEngine.Development;
using LegacyEngine.Draft;
using LegacyEngine.Events;
using LegacyEngine.Injuries;
using LegacyEngine.Owners;
using LegacyEngine.Recruiting;
using LegacyEngine.Relationships;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;
using LegacyEngine.Scouting;
using LegacyEngine.World;

namespace LegacyEngine.Integration;

public sealed record EngineRegistry(
    EventEngine EventEngine,
    WorldEngine WorldEngine,
    RecruitingEngine RecruitingEngine,
    ContractEngine ContractEngine,
    DraftEngine DraftEngine,
    RosterEngine RosterEngine,
    DevelopmentEngine DevelopmentEngine,
    InjuryEngine InjuryEngine,
    RelationshipEngine RelationshipEngine,
    OwnerEvaluator OwnerEvaluator,
    ScoutingReportGenerator ScoutingReportGenerator,
    Rulebook? Rulebook)
{
    public static EngineRegistry Create(WorldEngine worldEngine, Rulebook? rulebook = null)
    {
        var eventEngine = worldEngine.EventEngine;
        return new EngineRegistry(
            EventEngine: eventEngine,
            WorldEngine: worldEngine,
            RecruitingEngine: new RecruitingEngine(eventEngine),
            ContractEngine: new ContractEngine(eventEngine),
            DraftEngine: new DraftEngine(eventEngine),
            RosterEngine: new RosterEngine(eventEngine),
            DevelopmentEngine: new DevelopmentEngine(eventEngine),
            InjuryEngine: new InjuryEngine(eventEngine),
            RelationshipEngine: new RelationshipEngine(),
            OwnerEvaluator: new OwnerEvaluator(),
            ScoutingReportGenerator: new ScoutingReportGenerator(),
            Rulebook: rulebook);
    }
}
