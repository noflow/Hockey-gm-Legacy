using LegacyEngine.Contracts;
using LegacyEngine.Integration;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;
using LegacyEngine.Staff;

internal sealed class Alpha271RosterFrontOfficeRealismTests
{
    public void JuniorRosterTargetIsTwentySix()
    {
        var preset = RulebookPresets.CreateJuniorMajor();
        var loaded = new RulebookLoader().LoadFromFile(Path.Combine(FindRepositoryRoot(), "data", "rulebooks", "junior_v1.json"));

        Assert.Equal(26, preset.RosterRules!.MinRoster);
        Assert.Equal(26, preset.RosterRules.MaxRoster);
        Assert.Equal(26, preset.RosterRules.ActiveRoster);
        Assert.Equal(26, loaded.RosterRules!.ActiveRoster);
    }

    public void ScenarioStartsWithLegalRoster()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var report = new SeasonReadinessService().Evaluate(scenario.Registry, scenario.ScenarioSnapshot).RosterReport;

        Assert.Equal(26, scenario.ScenarioSnapshot.AlphaSnapshot.Roster.ActivePlayers.Count);
        Assert.Equal(26, report.RequiredRosterSize);
        Assert.True(report.ValidationResult.IsValid, report.ValidationResult.Message);
    }

    public void DraftProspectsIncludePhysicalAndTeamBio()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var entries = scenario.ScenarioSnapshot.AlphaSnapshot.DraftBoard.Entries.Take(12).ToArray();

        Assert.True(entries.Length > 0, "Draft board should contain prospects.");
        foreach (var entry in entries)
        {
            var bio = entry.Bio;
            Assert.True(bio is not null, "Draft prospect bio should exist.");
            Assert.True(bio!.Position != RosterPosition.Unknown, "Draft prospect position should be visible in basic bio.");
            Assert.True(!string.IsNullOrWhiteSpace(bio!.ShootsCatches), "Handedness should be present.");
            Assert.True(!string.IsNullOrWhiteSpace(bio.HeightDisplay), "Height display should be present.");
            Assert.True(!string.IsNullOrWhiteSpace(bio.WeightDisplay), "Weight display should be present.");
            Assert.True(!string.IsNullOrWhiteSpace(bio.CurrentTeam), "Current team should be present.");
            Assert.True(!string.IsNullOrWhiteSpace(bio.League), "League should be present.");
            Assert.True(!string.IsNullOrWhiteSpace(bio.CharacterSummary), "Character summary should be present.");
            Assert.True(!string.IsNullOrWhiteSpace(bio.PotentialLineupProjection), "Lineup projection should be present.");
        }
    }

    public void DraftProspectMeasurementsMatchPositionRanges()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        foreach (var entry in scenario.ScenarioSnapshot.AlphaSnapshot.DraftBoard.Entries.Take(20))
        {
            var bio = entry.Bio!;
            var position = bio.Position;
            if (position == RosterPosition.Goalie)
            {
                Assert.True(bio.HeightInches is >= 71 and <= 80, "Goalie height should be realistic.");
                Assert.True(bio.WeightPounds is >= 170 and <= 250, "Goalie weight should be realistic.");
            }
            else if (position == RosterPosition.Defense)
            {
                Assert.True(bio.HeightInches is >= 70 and <= 79, "Defense height should be realistic.");
                Assert.True(bio.WeightPounds is >= 175 and <= 240, "Defense weight should be realistic.");
            }
            else
            {
                Assert.True(bio.HeightInches is >= 68 and <= 77, "Forward height should be realistic.");
                Assert.True(bio.WeightPounds is >= 160 and <= 225, "Forward weight should be realistic.");
            }
        }
    }

    public void DraftProspectDescriptionsMatchKnownPosition()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        foreach (var entry in scenario.ScenarioSnapshot.AlphaSnapshot.DraftBoard.Entries)
        {
            var bio = entry.Bio!;
            var text = $"{bio.PotentialLineupProjection} {entry.ProjectionText} {entry.AnalyticsSummary}".ToLowerInvariant();

            if (bio.Position == RosterPosition.Goalie)
            {
                Assert.True(text.Contains("goalie"), $"Goalie projection should describe a goalie: {entry.ProjectionText}");
                Assert.False(text.Contains("center"), $"Goalie projection should not describe a center: {entry.ProjectionText}");
                Assert.False(text.Contains("winger"), $"Goalie projection should not describe a winger: {entry.ProjectionText}");
                Assert.False(text.Contains("defense"), $"Goalie projection should not describe defense: {entry.ProjectionText}");
            }
            else if (bio.Position == RosterPosition.Defense)
            {
                Assert.True(text.Contains("defense"), $"Defense projection should describe defense: {entry.ProjectionText}");
                Assert.False(text.Contains("center"), $"Defense projection should not describe a center: {entry.ProjectionText}");
                Assert.False(text.Contains("winger"), $"Defense projection should not describe a winger: {entry.ProjectionText}");
                Assert.False(text.Contains("goalie"), $"Defense projection should not describe a goalie: {entry.ProjectionText}");
            }
            else if (bio.Position == RosterPosition.Center)
            {
                Assert.True(text.Contains("center"), $"Center projection should describe a center: {entry.ProjectionText}");
                Assert.False(text.Contains("winger"), $"Center projection should not describe a winger: {entry.ProjectionText}");
                Assert.False(text.Contains("goalie"), $"Center projection should not describe a goalie: {entry.ProjectionText}");
                Assert.False(text.Contains("defense"), $"Center projection should not describe defense: {entry.ProjectionText}");
            }
            else
            {
                Assert.True(text.Contains("winger"), $"Winger projection should describe a winger: {entry.ProjectionText}");
                Assert.False(text.Contains("center"), $"Winger projection should not describe a center: {entry.ProjectionText}");
                Assert.False(text.Contains("goalie"), $"Winger projection should not describe a goalie: {entry.ProjectionText}");
                Assert.False(text.Contains("defense"), $"Winger projection should not describe defense: {entry.ProjectionText}");
            }
        }
    }

    public void ScenarioStartsWithInheritedScoutingReports()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var boardCount = scenario.ScenarioSnapshot.AlphaSnapshot.DraftBoard.Entries.Count;
        var reports = scenario.ScenarioSnapshot.CompletedScoutingReports;

        Assert.True(reports.Count >= boardCount / 2, "Most of the draft board should have inherited scouting work from previous staff.");
        Assert.True(reports.All(report => report.Details.ContainsKey("inherited_from_previous_staff")), "Inherited reports should be marked as previous-staff work.");
    }

    public void ScenarioRosterHasAgeMixAndContractDecisions()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var rosterPlayers = scenario.ScenarioSnapshot.AlphaSnapshot.Roster.ActivePlayers;
        var ages = rosterPlayers.Select(player => player.Age ?? scenario.ScenarioSnapshot.AlphaSnapshot.Players.First(person => person.PersonId == player.PersonId).CalculateAge(scenario.ScenarioSnapshot.CurrentDate)).ToArray();
        var playerContracts = scenario.ScenarioSnapshot.Contracts.Where(contract => contract.ContractType == ContractType.JuniorPlayerAgreement).ToArray();
        var currentExpiry = ContractExpiryCalendar.CommonExpiryDate(scenario.ScenarioSnapshot.Season.Year, scenario.ScenarioSnapshot.Season.Settings);

        Assert.True(ages.Any(age => age < 18), "Opening roster should include younger players.");
        Assert.True(ages.Any(age => age >= 20), "Opening roster should include older/overage players.");
        Assert.True(playerContracts.Any(contract => contract.Status == ContractStatus.Signed && contract.Term.EndDate == currentExpiry), "Some inherited player contracts should expire on the common pre-draft expiry day.");
        Assert.True(playerContracts.Any(contract => contract.Status == ContractStatus.Signed && contract.Term.EndDate <= scenario.ScenarioSnapshot.CurrentDate.AddDays(30)), "Some inherited player contracts should require renewal/walk-away review soon.");
    }

    public void ContractTermsUseCommonPreDraftExpiryDay()
    {
        var settings = NewGmScenarioSettings.CreateDefaultSeasonSettings();
        var oneYear = ContractExpiryCalendar.ExpiryDateForTerm(new DateOnly(2026, 7, 1), settings, 1);
        var eightYear = ContractExpiryCalendar.ExpiryDateForTerm(new DateOnly(2026, 7, 1), settings, 8);

        Assert.Equal(oneYear.Month, eightYear.Month);
        Assert.Equal(oneYear.Day, eightYear.Day);
        Assert.True(eightYear.Year > oneYear.Year, "Longer terms should expire on the same calendar day in a later year.");
    }

    public void ScenarioContractsExpireOnCommonPreDraftDates()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario().ScenarioSnapshot;
        var commonDates = Enumerable.Range(scenario.Season.Year, 5)
            .Select(year => ContractExpiryCalendar.CommonExpiryDate(year, scenario.Season.Settings))
            .ToArray();

        Assert.True(scenario.Contracts.Count > 0, "Scenario should include inherited contracts.");
        Assert.True(
            scenario.Contracts.All(contract => commonDates.Any(date => date == contract.Term.EndDate)),
            "Every scenario contract should expire on a common pre-draft expiry date.");
    }

    public void CompleteStaffPositionsExist()
    {
        var roles = Enum.GetNames<StaffRole>().ToHashSet(StringComparer.Ordinal);

        foreach (var role in new[]
        {
            "GeneralManager",
            "AssistantGM",
            "DirectorOfHockeyOperations",
            "HeadCoach",
            "AssistantCoach",
            "DevelopmentCoach",
            "GoaltendingCoach",
            "SkillsCoach",
            "VideoCoach",
            "StrengthConditioningCoach",
            "DirectorOfScouting",
            "HeadScout",
            "RegionalScout",
            "AmateurScout",
            "ProfessionalScout",
            "EuropeanScout",
            "GoaltendingScout",
            "HeadAthleticTherapist",
            "AssistantTrainer",
            "TeamDoctor",
            "Physiotherapist",
            "MassageTherapist",
            "HeadEquipmentManager",
            "AssistantEquipmentManager"
        })
        {
            Assert.True(roles.Contains(role), $"Missing staff role {role}.");
        }
    }

    public void VacanciesGenerated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var vacancies = new StaffOfficeService().BuildVacancies(scenario.ScenarioSnapshot, scenario.Registry.Rulebook!);

        Assert.True(vacancies.Count > 0, "Scenario should expose open hockey operations positions.");
        Assert.True(vacancies.Any(vacancy => vacancy.Role == StaffRole.AssistantGM), "Assistant GM vacancy should be generated.");
        Assert.True(vacancies.Any(vacancy => vacancy.Role == StaffRole.HeadAthleticTherapist), "Athletic therapist vacancy should be generated.");
    }

    public void CandidateHiringWorks()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var service = new StaffOfficeService();
        var generated = service.GenerateCandidatePool(scenario.Registry, scenario.ScenarioSnapshot);
        var candidate = generated.ScenarioSnapshot.StaffCandidates.First();
        var hired = service.HireCandidate(scenario.Registry, generated.ScenarioSnapshot, candidate.CandidateId);

        Assert.True(hired.Success, hired.Message);
        Assert.True(hired.ScenarioSnapshot.StaffMembers.Any(member => member.PersonId == candidate.Person.PersonId), "Candidate should become a staff member.");
    }

    public void StaffLimitsEnforced()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var service = new StaffOfficeService();
        var generated = service.GenerateCandidatePool(scenario.Registry, scenario.ScenarioSnapshot);
        var candidate = generated.ScenarioSnapshot.StaffCandidates.First(candidate => candidate.StaffMember.CurrentRole == StaffRole.AssistantGM);
        var hired = service.HireCandidate(scenario.Registry, generated.ScenarioSnapshot, candidate.CandidateId);
        var duplicateCandidateScenario = hired.ScenarioSnapshot with { StaffCandidates = new[] { candidate } };
        var blocked = service.HireCandidate(scenario.Registry, duplicateCandidateScenario, candidate.CandidateId);

        Assert.False(blocked.Success, "Second Assistant GM hire should be blocked by staff limit.");
        Assert.True(blocked.Message.Contains("limit", StringComparison.OrdinalIgnoreCase), blocked.Message);
    }

    public void DashboardWarningsGenerated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var warnings = new StaffOfficeService().BuildStaffWarnings(scenario.ScenarioSnapshot, scenario.Registry.Rulebook!);

        Assert.True(warnings.Count > 0, "Staff vacancy warnings should be generated.");
        Assert.True(warnings.Any(warning => warning.Contains("No", StringComparison.OrdinalIgnoreCase) || warning.Contains("Only", StringComparison.OrdinalIgnoreCase)), "Warnings should explain missing staff.");
    }

    public void AlphaDesktopExposesVacanciesCandidatesAndHiring()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(source.Contains("Vacant Positions", StringComparison.Ordinal), "Desktop should expose vacant positions.");
        Assert.True(source.Contains("Staff Market", StringComparison.Ordinal), "Desktop should expose the living staff market.");
        Assert.True(source.Contains("Hire Candidate", StringComparison.Ordinal), "Desktop should expose hire workflow.");
        Assert.True(source.Contains("Salary Offer", StringComparison.Ordinal), "Desktop should expose salary offer placeholder.");
    }

    private static RosterPosition PositionFor(NewGmScenarioSnapshot scenario, string personId) =>
        scenario.AlphaSnapshot.DraftBoard.Entries
            .OrderBy(entry => entry.Rank)
            .Select((entry, index) => new { entry.ProspectPersonId, Position = PositionForRank(index) })
            .First(item => item.ProspectPersonId == personId)
            .Position;

    private static RosterPosition PositionForRank(int index) =>
        index switch
        {
            0 or 1 => RosterPosition.Goalie,
            >= 2 and <= 8 => RosterPosition.Defense,
            _ when index % 3 == 0 => RosterPosition.Center,
            _ when index % 3 == 1 => RosterPosition.LeftWing,
            _ => RosterPosition.RightWing
        };

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HockeyGmLegacy.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find Hockey GM Legacy repository root.");
    }
}
