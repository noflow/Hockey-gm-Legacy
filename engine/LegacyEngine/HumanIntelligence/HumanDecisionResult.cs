namespace LegacyEngine.HumanIntelligence;

public sealed record HumanDecisionResult(
    HumanDecisionContext Context,
    HumanDecisionOption SelectedOption,
    IReadOnlyList<HumanDecisionOptionScore> RankedOptions,
    IReadOnlyList<HumanDecisionReason> Reasons,
    string Summary)
{
    public decimal SelectedScore => RankedOptions.First(item => item.Option.OptionId == SelectedOption.OptionId).Score;
}
