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
            var position = PositionFor(scenario.ScenarioSnapshot, entry.ProspectPersonId);
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
        Assert.True(source.Contains("Available Candidates", StringComparison.Ordinal), "Desktop should expose available candidates.");
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
