using LegacyEngine.Events;
using LegacyEngine.RuleEngine;

namespace LegacyEngine.Contracts;

public sealed class ContractEngine
{
    private readonly EventEngine _eventEngine;

    public ContractEngine(EventEngine? eventEngine = null)
    {
        _eventEngine = eventEngine ?? new EventEngine();
    }

    public EventEngine EventEngine => _eventEngine;

    public Contract CreateOffer(ContractOffer offer, ContractRuleValidator? ruleValidator = null)
    {
        offer.Validate();
        ValidateAgainstRulebook(offer, ruleValidator);

        var contract = new Contract(
            ContractId: $"contract-{offer.OfferId}",
            PersonId: offer.PersonId,
            OrganizationId: offer.OrganizationId,
            ContractType: offer.ContractType,
            Status: ContractStatus.Offered,
            Term: offer.Term,
            Money: offer.Money,
            Clauses: offer.Clauses,
            OfferedOn: offer.OfferedOn,
            SignedOn: null,
            RejectedOn: null,
            TerminatedOn: null,
            ExpiredOn: null)
        {
            PositionPromise = offer.PositionPromise,
            IceTimePromise = offer.IceTimePromise,
            NhlRosterPromise = offer.NhlRosterPromise
        };

        contract.Validate();
        QueueContractEvent(
            contract,
            LegacyEventType.ContractOffered,
            offer.OfferedOn,
            "Contract offered",
            "A contract offer was created.");

        return contract;
    }

    public ContractDecisionResult SignContract(Contract contract, DateOnly signedOn)
    {
        contract.Validate();
        var signed = contract.Sign(signedOn);
        var legacyEvent = QueueContractEvent(
            signed,
            LegacyEventType.ContractSigned,
            signedOn,
            "Contract signed",
            "A contract was signed.");

        return new ContractDecisionResult(
            ContractDecision.Signed,
            signed.Status,
            signed,
            legacyEvent,
            "Contract signed.",
            BuildDetails(signed));
    }

    public ContractDecisionResult RejectContract(Contract contract, DateOnly rejectedOn)
    {
        contract.Validate();
        var rejected = contract.Reject(rejectedOn);
        var legacyEvent = QueueContractEvent(
            rejected,
            LegacyEventType.ContractRejected,
            rejectedOn,
            "Contract rejected",
            "A contract offer was rejected.");

        return new ContractDecisionResult(
            ContractDecision.Rejected,
            rejected.Status,
            rejected,
            legacyEvent,
            "Contract rejected.",
            BuildDetails(rejected));
    }

    public ContractDecisionResult TerminateContract(Contract contract, DateOnly terminatedOn)
    {
        contract.Validate();
        var terminated = contract.Terminate(terminatedOn);
        var legacyEvent = QueueContractEvent(
            terminated,
            LegacyEventType.ContractTerminated,
            terminatedOn,
            "Contract terminated",
            "A contract was terminated.");

        return new ContractDecisionResult(
            ContractDecision.Terminated,
            terminated.Status,
            terminated,
            legacyEvent,
            "Contract terminated.",
            BuildDetails(terminated));
    }

    private static void ValidateAgainstRulebook(ContractOffer offer, ContractRuleValidator? ruleValidator)
    {
        if (ruleValidator is null)
        {
            return;
        }

        var result = ruleValidator.Validate(new ContractValidationRequest(
            ToRulebookContractType(offer.ContractType),
            offer.Clauses.Select(ToRulebookClause).Where(clause => clause is not null).Cast<string>().ToArray(),
            TeamPayrollAfterSigning: offer.Money.SalaryOrStipend));

        if (!result.IsValid)
        {
            throw new InvalidOperationException(result.Message);
        }
    }

    private LegacyEvent QueueContractEvent(
        Contract contract,
        LegacyEventType eventType,
        DateOnly date,
        string title,
        string description)
    {
        var legacyEvent = _eventEngine.CreateEvent(
            new DateTimeOffset(date.Year, date.Month, date.Day, 12, 0, 0, TimeSpan.Zero),
            eventType,
            LegacyEventSeverity.Notice,
            LegacyEventVisibility.Organization,
            title,
            description,
            new LegacyEventContext(
                PrimaryPersonId: contract.PersonId,
                OrganizationId: contract.OrganizationId),
            BuildDetails(contract));

        return _eventEngine.QueueEvent(legacyEvent);
    }

    private static IReadOnlyDictionary<string, object?> BuildDetails(Contract contract) =>
        new Dictionary<string, object?>
        {
            ["contract_id"] = contract.ContractId,
            ["contract_type"] = contract.ContractType.ToString(),
            ["status"] = contract.Status.ToString(),
            ["start_date"] = contract.Term.StartDate,
            ["end_date"] = contract.Term.EndDate,
            ["salary_or_stipend"] = contract.Money.SalaryOrStipend,
            ["signing_bonus"] = contract.Money.SigningBonus,
            ["clause_count"] = contract.Clauses.Count
        };

    private static string ToRulebookContractType(ContractType contractType) =>
        contractType switch
        {
            ContractType.JuniorPlayerAgreement => "junior_player_agreement",
            ContractType.StaffContract => "staff_contract",
            ContractType.GMContract => "gm_contract",
            ContractType.ScoutContract => "scout_contract",
            ContractType.CoachContract => "coach_contract",
            _ => throw new ArgumentOutOfRangeException(nameof(contractType), contractType, "Unsupported contract type.")
        };

    private static string? ToRulebookClause(ContractClause clause) =>
        clause.ClauseType switch
        {
            ContractClauseType.EducationPackage => "education_package",
            ContractClauseType.HousingSupport => "housing_support",
            ContractClauseType.NoTradeClause => "no_trade_clause",
            ContractClauseType.NoMoveClause => "no_move_clause",
            ContractClauseType.Arbitration => "arbitration",
            ContractClauseType.OfferSheet => "offer_sheet",
            _ => null
        };
}
