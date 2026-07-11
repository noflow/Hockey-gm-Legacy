using LegacyEngine.Integration;

internal sealed class Alpha273DraftStaffLayoutTests
{
    public void LiveDraftMiddleRowsIncludeNamePositionTeamAndConfidence()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("BuildLiveDraftMiddleRow", StringComparison.Ordinal), "Live draft should use quick-scan middle rows.");
        Assert.True(source.Contains("State.DraftQuickScan(entry)", StringComparison.Ordinal), "Middle rows should include visible draft bio.");
        Assert.True(source.Contains("entry.Bio?.Position", StringComparison.Ordinal) || source.Contains("entry.Bio.Position", StringComparison.Ordinal), "Middle rows should use public bio position.");
        Assert.True(source.Contains("entry.Bio.CurrentTeam", StringComparison.Ordinal), "Middle rows should include current team.");
        Assert.True(source.Contains("Confidence:", StringComparison.Ordinal), "Middle rows should include confidence.");
    }

    public void LiveDraftRowsUseCurrentAvailableRank()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("State.LiveDraftAvailableEntries", StringComparison.Ordinal), "Live draft should use the current available board order.");
        Assert.True(source.Contains("AvailableRank = index + 1", StringComparison.Ordinal), "Live draft should compute a fresh available rank after drafted players are removed.");
        Assert.True(source.Contains("BuildLiveDraftMiddleRow(item.Entry, item.AvailableRank)", StringComparison.Ordinal), "Live draft rows should display the fresh available rank.");
    }

    public void DraftAndScoutingRowsUseCurrentWarRoomOrder()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("DraftBoardEntriesByCurrentWarRoom", StringComparison.Ordinal), "Draft and scouting rows should use the scouting-adjusted War Room order.");
        Assert.True(source.Contains("public IReadOnlyList<DraftBoardEntry> DraftBoardEntriesByCurrentWarRoom", StringComparison.Ordinal), "AlphaDesktop state should expose current War Room ordered draft entries.");
    }

    public void DraftWarRoomUsesCachedPresentationData()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("_cachedDraftWarRoom", StringComparison.Ordinal), "Draft War Room state should be cached for desktop presentation.");
        Assert.True(source.Contains("_draftCardCache", StringComparison.Ordinal), "Draft prospect intelligence cards should be cached after first build.");
        Assert.True(source.Contains("_draftConsensusCache", StringComparison.Ordinal), "Draft scout consensus should be cached after first build.");
        Assert.True(source.Contains("ClearDraftPresentationCache", StringComparison.Ordinal), "Draft presentation cache should be cleared when scenario state changes.");
        Assert.False(source.Contains(".OrderBy(entry => BoardRankFor(entry))", StringComparison.Ordinal), "War Room row sorting should not call expensive card-backed rank logic per row.");
    }

    public void SelectingDraftProspectPopulatesLeftProspectCard()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("Selected Prospect Card", StringComparison.Ordinal), "Live draft should have a selected prospect card.");
        Assert.True(source.Contains("prospectList.SelectionChanged", StringComparison.Ordinal), "Selecting a draft row should update the prospect card.");
        Assert.True(source.Contains("BuildLiveDraftProspectCard", StringComparison.Ordinal), "Prospect card should be generated from selected prospect.");
    }

    public void ProspectCardIncludesBioAndScoutingInfo()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("Shoots/Catches", StringComparison.Ordinal), "Prospect card should include handedness.");
        Assert.True(source.Contains("Height:", StringComparison.Ordinal), "Prospect card should include height.");
        Assert.True(source.Contains("Weight:", StringComparison.Ordinal), "Prospect card should include weight.");
        Assert.True(source.Contains("Current team:", StringComparison.Ordinal), "Prospect card should include current team.");
        Assert.True(source.Contains("Current picture:", StringComparison.Ordinal), "Prospect card should include a current scouting picture.");
        Assert.True(source.Contains("Future picture:", StringComparison.Ordinal), "Prospect card should include a future projection picture.");
        Assert.True(source.Contains("Scouting reports:", StringComparison.Ordinal), "Prospect card should include scouting reports.");
        Assert.True(source.Contains("Staff/scout recommendation", StringComparison.Ordinal), "Prospect card should include staff/scout recommendation.");
    }

    public void DraftStatusAppearsOnRight()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("Draft Status", StringComparison.Ordinal), "Right panel should be Draft Status.");
        Assert.True(source.Contains("Current round:", StringComparison.Ordinal), "Draft status should include round.");
        Assert.True(source.Contains("Team selecting:", StringComparison.Ordinal), "Draft status should include team selecting.");
        Assert.True(source.Contains("Your Selections / Draft Rights", StringComparison.Ordinal), "Draft status should include player selections/rights.");
    }

    public void DraftingSelectedProspectRemovesThemAndAddsRights()
    {
        var ready = ReadyForPlayerPick();
        var prospect = ready.ScenarioSnapshot.AlphaSnapshot.DraftBoard.Entries.OrderBy(entry => entry.Rank).First();

        var drafted = new AlphaDraftExperienceService().MakePlayerSelection(ready.Registry, ready.ScenarioSnapshot, prospect.ProspectPersonId);

        Assert.False(drafted.ScenarioSnapshot.AlphaSnapshot.DraftBoard.Entries.Any(entry => entry.ProspectPersonId == prospect.ProspectPersonId), "Drafted prospect should disappear from available draft list.");
        Assert.True(drafted.ScenarioSnapshot.ProspectRights.Any(record => record.ProspectPersonId == prospect.ProspectPersonId), "Drafted prospect should appear in draft rights.");
    }

    public void DraftAndProspectRowsExposeBasicBioWithoutHiddenRatings()
    {
        var source = AlphaDesktopSource();
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var prospect = scenario.ScenarioSnapshot.AlphaSnapshot.DraftBoard.Entries.First();
        var dossier = new PlayerDossierService().CreateDossier(scenario.ScenarioSnapshot, prospect.ProspectPersonId);
        var dossierText = string.Join(" ", dossier.Sections.SelectMany(section => section.Lines));

        Assert.True(source.Contains("Height/Weight", StringComparison.Ordinal) || source.Contains("Height:", StringComparison.Ordinal), "Draft UI should expose height/weight.");
        Assert.True(source.Contains("Shoots/Catches", StringComparison.Ordinal), "Draft UI should expose handedness.");
        Assert.True(source.Contains("Known position", StringComparison.Ordinal), "Draft UI should expose known public position.");
        Assert.True(source.Contains("Projection:", StringComparison.Ordinal), "Draft/prospect rows should include projection.");
        Assert.True(prospect.Bio!.Position != LegacyEngine.Rosters.RosterPosition.Unknown, "Scenario draft prospect should have a known public position.");
        Assert.False(dossierText.Contains("Position: Unknown", StringComparison.Ordinal), "Dossier should not show Unknown for draft prospect position.");
        Assert.False(dossierText.Contains("CurrentAbility", StringComparison.Ordinal), "Dossier should not expose hidden current ability ratings.");
        Assert.False(dossierText.Contains("Potential =", StringComparison.Ordinal), "Dossier should not expose hidden potential ratings.");
    }

    public void CurrentStaffExcludesCandidatesAndHireStaffContainsCandidates()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var generated = new StaffOfficeService().GenerateCandidatePool(scenario.Registry, scenario.ScenarioSnapshot);
        var candidateId = generated.ScenarioSnapshot.StaffCandidates[0].Person.PersonId;

        Assert.False(generated.ScenarioSnapshot.StaffMembers.Any(member => member.PersonId == candidateId), "Candidate should not appear in current staff before hiring.");
        Assert.True(generated.ScenarioSnapshot.StaffCandidates.Any(candidate => candidate.Person.PersonId == candidateId), "Hire Staff should contain available candidates.");
    }

    public void HiredCandidateMovesToCurrentStaff()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var service = new StaffOfficeService();
        var generated = service.GenerateCandidatePool(scenario.Registry, scenario.ScenarioSnapshot);
        var candidate = generated.ScenarioSnapshot.StaffCandidates[0];

        var hired = service.HireCandidate(scenario.Registry, generated.ScenarioSnapshot, candidate.CandidateId);

        Assert.True(hired.ScenarioSnapshot.StaffMembers.Any(member => member.PersonId == candidate.Person.PersonId), "Hired candidate should move into current staff.");
        Assert.False(hired.ScenarioSnapshot.StaffCandidates.Any(item => item.Person.PersonId == candidate.Person.PersonId), "Hired candidate should be removed from candidates.");
    }

    public void StaffContextButtonsAreCorrect()
    {
        var source = AlphaDesktopSource();

        Assert.True(source.Contains("Hire Staff / Staff Market", StringComparison.Ordinal), "Staff UI should separate staff market candidates.");
        Assert.True(source.Contains("Current Staff", StringComparison.Ordinal), "Staff UI should expose current staff section.");
        Assert.True(source.Contains("Vacancies", StringComparison.Ordinal), "Staff UI should expose vacancies section.");
        Assert.True(source.Contains("CreateDetailButton(\"Hire Candidate\"", StringComparison.Ordinal), "Hire button should exist for candidates.");
        Assert.True(source.Contains("CreateDetailButton(\"Release Staff\"", StringComparison.Ordinal), "Release button should exist for current staff.");
        Assert.True(source.Contains("CreateDetailButton(\"Reassign Role\"", StringComparison.Ordinal), "Reassign button should exist for current staff.");
    }

    private static string AlphaDesktopSource() =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

    private static (EngineRegistry Registry, NewGmScenarioSnapshot ScenarioSnapshot) ReadyForPlayerPick()
    {
        var scenario = AdvanceToDraftDay(NewGmScenarioBootstrapper.CreateScenario());
        var service = new AlphaDraftExperienceService();
        var started = service.StartDraftDay(scenario.Registry, scenario.ScenarioSnapshot);
        var ai = service.RunAiPicksUntilPlayerTurn(scenario.Registry, started.ScenarioSnapshot);
        return (scenario.Registry, ai.ScenarioSnapshot);
    }

    private static NewGmScenarioResult AdvanceToDraftDay(NewGmScenarioResult scenario)
    {
        var snapshot = scenario.AlphaSnapshot;
        var scenarioSnapshot = scenario.ScenarioSnapshot;
        var coordinator = new DailySimulationCoordinator();

        while (snapshot.CurrentDate < scenarioSnapshot.DraftDate)
        {
            var result = coordinator.AdvanceOneDay(scenario.Registry, snapshot);
            snapshot = result.WorldSnapshot;
            scenarioSnapshot = scenarioSnapshot with
            {
                AlphaSnapshot = snapshot,
                Season = snapshot.Season ?? scenarioSnapshot.Season
            };
        }

        return new NewGmScenarioResult(scenario.Registry, scenarioSnapshot, snapshot, scenario.FirstDayInbox, scenario.Summary);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var projectPath = Path.Combine(directory.FullName, "engine", "LegacyEngine", "LegacyEngine.csproj");
            if (File.Exists(projectPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find Hockey GM Legacy repository root.");
    }
}
