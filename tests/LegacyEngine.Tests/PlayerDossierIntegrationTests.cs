using LegacyEngine.Contracts;
using LegacyEngine.Injuries;
using LegacyEngine.Integration;
using LegacyEngine.Rosters;
using LegacyEngine.Staff;

internal sealed class PlayerDossierIntegrationTests
{
    public void GeneratedDisplayNamesHaveNoNumericSuffixes()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();

        var numberedNames = scenario.ScenarioSnapshot.AlphaSnapshot.People
            .Select(person => person.Identity.DisplayName)
            .Where(name => name.Any(char.IsDigit))
            .ToArray();

        Assert.Equal(0, numberedNames.Length);
    }

    public void DossierCanBeCreatedForRosterPlayer()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var playerId = scenario.ScenarioSnapshot.AlphaSnapshot.Roster.Players.First().PersonId;
        var dossier = new PlayerDossierService().CreateDossier(scenario.ScenarioSnapshot, playerId, PlayerDossierSource.Roster);

        Assert.Equal(PlayerDossierSource.Roster, dossier.Source);
        Assert.Contains("Overview", dossier.Sections.Select(section => section.Title));
    }

    public void DossierCanBeCreatedForRecruit()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var recruitId = scenario.ScenarioSnapshot.AlphaSnapshot.Recruits.First().RecruitPersonId;
        var dossier = new PlayerDossierService().CreateDossier(scenario.ScenarioSnapshot, recruitId, PlayerDossierSource.Recruit);

        Assert.Equal(PlayerDossierSource.Recruit, dossier.Source);
        Assert.True(dossier.Status.Contains("Recruiting", StringComparison.Ordinal), "Recruit dossier should show recruiting status.");
    }

    public void DossierCanBeCreatedForDraftProspect()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var prospectId = scenario.ScenarioSnapshot.AlphaSnapshot.DraftBoard.Entries.First().ProspectPersonId;
        var dossier = new PlayerDossierService().CreateDossier(scenario.ScenarioSnapshot, prospectId, PlayerDossierSource.DraftBoard);

        Assert.Equal(PlayerDossierSource.DraftBoard, dossier.Source);
        Assert.Contains("Scouting Reports", dossier.Sections.Select(section => section.Title));
    }

    public void DossierHidesTrueRatings()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var playerId = scenario.ScenarioSnapshot.AlphaSnapshot.Roster.Players.First().PersonId;
        var profiles = scenario.ScenarioSnapshot.AlphaSnapshot.DevelopmentProfiles
            .Select(profile => profile.PersonId == playerId ? profile with { CurrentAbility = 97, Potential = 98 } : profile)
            .ToArray();
        var snapshot = scenario.ScenarioSnapshot.AlphaSnapshot with { DevelopmentProfiles = profiles };
        var updated = scenario.ScenarioSnapshot with { AlphaSnapshot = snapshot };

        var dossier = new PlayerDossierService().CreateDossier(updated, playerId);
        var text = DossierText(dossier);

        Assert.False(text.Contains("CurrentAbility", StringComparison.Ordinal), "Dossier must not expose current ability field names.");
        Assert.False(text.Contains("PotentialEstimate", StringComparison.Ordinal), "Dossier must not expose potential estimate field names.");
        Assert.False(text.Contains("97", StringComparison.Ordinal), "Dossier must not expose current ability values.");
        Assert.False(text.Contains("98", StringComparison.Ordinal), "Dossier must not expose potential values.");
    }

    public void GmNoteCanBeAdded()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var playerId = scenario.ScenarioSnapshot.AlphaSnapshot.Roster.Players.First().PersonId;
        var result = new PlayerDossierService().AddOrUpdateGmNote(scenario.ScenarioSnapshot, playerId, "Track confidence after camp.");

        Assert.Equal("Track confidence after camp.", result.Dossier.GmNotes);
        Assert.True(result.ScenarioSnapshot.PlayerDossierNotes.ContainsKey(playerId), "GM note should be stored in the scenario snapshot.");
    }

    public void ScoutingDevelopmentInjuryAndContractSectionsPopulateWhenDataExists()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var playerId = scenario.ScenarioSnapshot.AlphaSnapshot.Roster.Players.First().PersonId;
        var current = scenario.ScenarioSnapshot;
        var date = current.CurrentDate;
        var scoutingService = new ScoutingOperationsService();
        var scoutId = current.StaffMembers.First(member => member.Department == StaffDepartment.Scouting).PersonId;
        current = scoutingService.AssignScoutToPlayer(
            scenario.Registry,
            current,
            scoutId,
            playerId,
            ScoutingOperationPriority.High,
            "Dossier report test.",
            date).ScenarioSnapshot;
        current = scoutingService.AdvanceAssignments(scenario.Registry, current).ScenarioSnapshot;

        var injury = new Injury(
            InjuryId: "injury-dossier-test",
            PersonId: playerId,
            InjuryDate: date,
            BodyPart: InjuryBodyPart.Knee,
            InjuryType: InjuryType.Sprain,
            Severity: InjurySeverity.Moderate,
            ExpectedReturnDate: date.AddDays(10),
            ActualReturnDate: null,
            GamesMissed: 0,
            Status: InjuryStatus.Recovering,
            LongTermImpact: 8,
            RecurrenceRisk: 12,
            RecoveryProgress: 40,
            RecoveryPlan: new InjuryRecoveryPlan("injury-dossier-test", date, date.AddDays(10), "Conservative rehab plan."));
        var contract = new Contract(
            ContractId: "contract-dossier-test",
            PersonId: playerId,
            OrganizationId: current.Organization.OrganizationId,
            ContractType: ContractType.JuniorPlayerAgreement,
            Status: ContractStatus.Signed,
            Term: new ContractTerm(date, date.AddYears(1)),
            Money: new ContractMoney(1_000, 0, "USD"),
            Clauses: Array.Empty<ContractClause>(),
            OfferedOn: date,
            SignedOn: date,
            RejectedOn: null,
            TerminatedOn: null,
            ExpiredOn: null);
        current = current with
        {
            Contracts = current.Contracts.Append(contract).ToArray(),
            AlphaSnapshot = current.AlphaSnapshot with
            {
                Contracts = current.AlphaSnapshot.Contracts.Append(contract).ToArray(),
                Injuries = current.AlphaSnapshot.Injuries.Append(injury).ToArray()
            }
        };

        var dossier = new PlayerDossierService().CreateDossier(current, playerId);

        Assert.True(Section(dossier, "Scouting Reports").Lines.Any(line => line.Contains("Report", StringComparison.Ordinal)), "Scouting report should populate.");
        Assert.True(Section(dossier, "Development").Lines.Any(line => line.Contains("Stage", StringComparison.Ordinal)), "Development section should populate.");
        Assert.True(Section(dossier, "Injuries / Medical").Lines.Any(line => line.Contains("Sprain", StringComparison.Ordinal)), "Injury section should populate.");
        Assert.True(Section(dossier, "Contract / Rights Status").Lines.Any(line => line.Contains("JuniorPlayerAgreement", StringComparison.Ordinal)), "Contract section should populate.");
    }

    private static PlayerDossierSection Section(PlayerDossierView dossier, string title) =>
        dossier.Sections.Single(section => section.Title == title);

    private static string DossierText(PlayerDossierView dossier) =>
        string.Join('\n', dossier.Sections.SelectMany(section => section.Lines.Prepend(section.Title)));
}
