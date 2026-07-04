using LegacyEngine.Contracts;
using LegacyEngine.Events;
using LegacyEngine.RuleEngine;

internal sealed class ContractEngineTests
{
    public void ContractOfferCreation()
    {
        var eventEngine = new EventEngine();
        var contract = new ContractEngine(eventEngine).CreateOffer(BuildOffer(), BuildRuleValidator());

        contract.Validate();
        Assert.Equal("contract-offer-001", contract.ContractId);
        Assert.Equal("person-001", contract.PersonId);
        Assert.Equal("org-001", contract.OrganizationId);
        Assert.Equal(ContractType.JuniorPlayerAgreement, contract.ContractType);
        Assert.Equal(ContractStatus.Offered, contract.Status);
        Assert.Equal(1, eventEngine.Queue.Count);
        Assert.Equal(LegacyEventType.ContractOffered, eventEngine.Queue.PendingEvents[0].EventType);
    }

    public void ContractSigning()
    {
        var engine = new ContractEngine();
        var contract = engine.CreateOffer(BuildOffer(), BuildRuleValidator());
        var result = engine.SignContract(contract, new DateOnly(2026, 8, 1));

        Assert.Equal(ContractDecision.Signed, result.Decision);
        Assert.Equal(ContractStatus.Signed, result.ResultingStatus);
        Assert.Equal(ContractStatus.Signed, result.Contract.Status);
        Assert.Equal(new DateOnly(2026, 8, 1), result.Contract.SignedOn);
        Assert.Equal(LegacyEventType.ContractSigned, result.CreatedEvent.EventType);
    }

    public void ContractRejection()
    {
        var engine = new ContractEngine();
        var contract = engine.CreateOffer(BuildOffer(), BuildRuleValidator());
        var result = engine.RejectContract(contract, new DateOnly(2026, 8, 2));

        Assert.Equal(ContractDecision.Rejected, result.Decision);
        Assert.Equal(ContractStatus.Rejected, result.ResultingStatus);
        Assert.Equal(new DateOnly(2026, 8, 2), result.Contract.RejectedOn);
        Assert.Equal(LegacyEventType.ContractRejected, result.CreatedEvent.EventType);
    }

    public void ContractTermination()
    {
        var engine = new ContractEngine();
        var contract = engine.CreateOffer(BuildOffer(), BuildRuleValidator());
        var signed = engine.SignContract(contract, new DateOnly(2026, 8, 1)).Contract;
        var result = engine.TerminateContract(signed, new DateOnly(2027, 1, 15));

        Assert.Equal(ContractDecision.Terminated, result.Decision);
        Assert.Equal(ContractStatus.Terminated, result.ResultingStatus);
        Assert.Equal(new DateOnly(2027, 1, 15), result.Contract.TerminatedOn);
        Assert.Equal(LegacyEventType.ContractTerminated, result.CreatedEvent.EventType);
        Assert.Throws<InvalidOperationException>(() => engine.TerminateContract(contract, new DateOnly(2027, 1, 15)));
    }

    public void StatusChanges()
    {
        var engine = new ContractEngine();
        var offered = engine.CreateOffer(BuildOffer(), BuildRuleValidator());
        var signed = engine.SignContract(offered, new DateOnly(2026, 8, 1)).Contract;
        var terminated = engine.TerminateContract(signed, new DateOnly(2027, 1, 15)).Contract;

        Assert.Equal(ContractStatus.Offered, offered.Status);
        Assert.Equal(ContractStatus.Signed, signed.Status);
        Assert.Equal(ContractStatus.Terminated, terminated.Status);
        Assert.Throws<InvalidOperationException>(() => engine.RejectContract(signed, new DateOnly(2026, 8, 2)));
    }

    public void StartAndEndDateTracking()
    {
        var contract = new ContractEngine().CreateOffer(BuildOffer(), BuildRuleValidator());

        Assert.Equal(new DateOnly(2026, 8, 1), contract.Term.StartDate);
        Assert.Equal(new DateOnly(2027, 5, 31), contract.Term.EndDate);
        Assert.True(contract.Term.LengthInDays > 0, "Contract term should have a positive duration.");
        Assert.Throws<ArgumentOutOfRangeException>(() => new ContractTerm(new DateOnly(2027, 1, 1), new DateOnly(2026, 1, 1)).Validate());
    }

    public void MoneyAndStipendTracking()
    {
        var contract = new ContractEngine().CreateOffer(BuildOffer(
            money: new ContractMoney(SalaryOrStipend: 2_500m, SigningBonus: 250m, Currency: "CAD")), BuildRuleValidator());

        Assert.Equal(2_500m, contract.Money.SalaryOrStipend);
        Assert.Equal(250m, contract.Money.SigningBonus);
        Assert.Equal("CAD", contract.Money.Currency);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ContractMoney(-1).Validate());
    }

    public void AllowedClauseAccepted()
    {
        var contract = new ContractEngine().CreateOffer(BuildOffer(clauses: new[]
        {
            BuildClause("clause-education", ContractClauseType.EducationPackage),
            BuildClause("clause-housing", ContractClauseType.HousingSupport)
        }), BuildRuleValidator());

        Assert.Equal(2, contract.Clauses.Count);
    }

    public void DisallowedClauseRejectedWhenRuleEngineSaysNo()
    {
        var engine = new ContractEngine();
        var offer = BuildOffer(clauses: new[] { BuildClause("clause-no-trade", ContractClauseType.NoTradeClause) });

        Assert.Throws<InvalidOperationException>(() => engine.CreateOffer(offer, BuildRuleValidator()));
    }

    public void JuniorEducationPackageClauseWorks()
    {
        var contract = new ContractEngine().CreateOffer(BuildOffer(clauses: new[]
        {
            BuildClause("clause-education", ContractClauseType.EducationPackage)
        }), BuildRuleValidator());

        Assert.Equal(ContractClauseType.EducationPackage, contract.Clauses[0].ClauseType);
    }

    public void JuniorHousingSupportClauseWorks()
    {
        var contract = new ContractEngine().CreateOffer(BuildOffer(clauses: new[]
        {
            BuildClause("clause-housing", ContractClauseType.HousingSupport)
        }), BuildRuleValidator());

        Assert.Equal(ContractClauseType.HousingSupport, contract.Clauses[0].ClauseType);
    }

    public void EventsCreatedForOfferSignRejectTerminate()
    {
        var eventEngine = new EventEngine();
        var engine = new ContractEngine(eventEngine);
        var signedOffer = engine.CreateOffer(BuildOffer("offer-sign"), BuildRuleValidator());
        var signed = engine.SignContract(signedOffer, new DateOnly(2026, 8, 1)).Contract;
        engine.TerminateContract(signed, new DateOnly(2027, 1, 15));
        var rejectedOffer = engine.CreateOffer(BuildOffer("offer-reject"), BuildRuleValidator());
        engine.RejectContract(rejectedOffer, new DateOnly(2026, 8, 2));

        Assert.Equal(5, eventEngine.Queue.Count);
        Assert.True(eventEngine.Queue.PendingEvents.Any(item => item.EventType == LegacyEventType.ContractOffered), "Offer event should exist.");
        Assert.True(eventEngine.Queue.PendingEvents.Any(item => item.EventType == LegacyEventType.ContractSigned), "Signed event should exist.");
        Assert.True(eventEngine.Queue.PendingEvents.Any(item => item.EventType == LegacyEventType.ContractRejected), "Rejected event should exist.");
        Assert.True(eventEngine.Queue.PendingEvents.Any(item => item.EventType == LegacyEventType.ContractTerminated), "Terminated event should exist.");
    }

    public void NoRosterModificationOccurs()
    {
        var roster = new List<string> { "player-a", "player-b" };
        var engine = new ContractEngine();
        var contract = engine.CreateOffer(BuildOffer(), BuildRuleValidator());
        engine.SignContract(contract, new DateOnly(2026, 8, 1));

        Assert.Equal(2, roster.Count);
        Assert.Equal("player-a", roster[0]);
        Assert.Equal("player-b", roster[1]);
    }

    private static ContractOffer BuildOffer(
        string offerId = "offer-001",
        ContractMoney? money = null,
        IReadOnlyList<ContractClause>? clauses = null) =>
        new(
            OfferId: offerId,
            PersonId: "person-001",
            OrganizationId: "org-001",
            ContractType: ContractType.JuniorPlayerAgreement,
            Term: new ContractTerm(new DateOnly(2026, 8, 1), new DateOnly(2027, 5, 31)),
            Money: money ?? new ContractMoney(SalaryOrStipend: 1_500m),
            Clauses: clauses ?? new[]
            {
                BuildClause("clause-education", ContractClauseType.EducationPackage),
                BuildClause("clause-housing", ContractClauseType.HousingSupport),
                BuildClause("clause-role", ContractClauseType.PlayingRolePromise),
                BuildClause("clause-development", ContractClauseType.DevelopmentPromise),
                BuildClause("clause-release", ContractClauseType.ReleaseTransferCondition)
            },
            OfferedOn: new DateOnly(2026, 7, 15),
            Notes: "Junior player agreement offer.");

    private static ContractClause BuildClause(string clauseId, ContractClauseType clauseType) =>
        new(
            ClauseId: clauseId,
            ClauseType: clauseType,
            Description: $"{clauseType} clause for v1 contract.");

    private static ContractRuleValidator BuildRuleValidator()
    {
        var path = Path.Combine(FindRepositoryRoot(), "data", "rulebooks", "junior_v1.json");
        return new ContractRuleValidator(new RulebookLoader().LoadFromFile(path));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var rulebookPath = Path.Combine(directory.FullName, "data", "rulebooks", "junior_v1.json");
            if (File.Exists(rulebookPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find Hockey GM Legacy repository root.");
    }
}
