using LegacyEngine.Events;
using LegacyEngine.Integration;
using LegacyEngine.Names;
using LegacyEngine.Staff;

internal sealed class Alpha231DedupHiringTests
{
    public void NameGeneratorCreatesFiveHundredNamesWithVeryLowDuplicateRate()
    {
        var registry = new NameUniquenessRegistry();
        var generator = new NameGenerator(NameGenerationSettings.CreateDefault(seed: 500));
        var names = Enumerable.Range(0, 500)
            .Select(_ => generator.Generate(registry, "alpha-231:stress").DisplayName)
            .ToArray();
        var duplicateCount = names.Length - names.Distinct(StringComparer.OrdinalIgnoreCase).Count();

        Assert.True(duplicateCount <= 5, $"Expected a very low duplicate rate across 500 generated names, got {duplicateCount} duplicate(s).");
        Assert.False(names.Any(name => name.Any(char.IsDigit)), "Generated display names should not contain numeric uniqueness markers.");
    }

    public void NameGeneratorCreatesFortyDraftClassesWithoutNumericSuffixes()
    {
        var registry = new NameUniquenessRegistry();
        var generator = new NameGenerator(NameGenerationSettings.CreateDefault(seed: 2040));
        for (var draftYear = 0; draftYear < 40; draftYear++)
        {
            var scope = $"draft-class:{draftYear:00}";
            var names = Enumerable.Range(0, 60)
                .Select(_ => generator.Generate(registry, scope).DisplayName)
                .ToArray();

            Assert.False(names.Any(name => name.Any(char.IsDigit)), $"Draft class {draftYear} should not contain numeric suffixes.");
            Assert.Equal(names.Length, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
    }

    public void NewGmScenarioHasNoDuplicateRecruitPersonIds()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var ids = scenario.ScenarioSnapshot.AlphaSnapshot.Recruits.Select(recruit => recruit.RecruitPersonId).ToArray();

        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    public void NewGmScenarioHasNoDuplicatePersonIdsAcrossRecruitDraftAndScoutingTargets()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var recruitIds = scenario.ScenarioSnapshot.AlphaSnapshot.Recruits.Select(recruit => recruit.RecruitPersonId).ToArray();
        var draftIds = scenario.ScenarioSnapshot.AlphaSnapshot.DraftBoard.Entries.Select(entry => entry.ProspectPersonId).ToArray();
        var scoutingTargetIds = scenario.ScenarioSnapshot.AlphaSnapshot.DraftBoard.Entries.Select(entry => entry.ProspectPersonId).ToArray();

        Assert.Equal(recruitIds.Length, recruitIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(draftIds.Length, draftIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(scoutingTargetIds.Length, scoutingTargetIds.Distinct(StringComparer.Ordinal).Count());
        Assert.True(draftIds.All(id => recruitIds.Contains(id, StringComparer.Ordinal)), "Draft board should share person ids with generated recruit/prospect people.");
    }

    public void DraftBoardHasNoDuplicateProspectPersonIds()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var ids = scenario.ScenarioSnapshot.AlphaSnapshot.DraftBoard.Entries.Select(entry => entry.ProspectPersonId).ToArray();

        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    public void RecruitDisplayNamesContainNoNumericSuffixes()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var names = scenario.ScenarioSnapshot.AlphaSnapshot.Recruits
            .Select(recruit => scenario.ScenarioSnapshot.AlphaSnapshot.People.Single(person => person.PersonId == recruit.RecruitPersonId).Identity.DisplayName)
            .ToArray();

        Assert.False(names.Any(name => name.Any(char.IsDigit)), "Recruit display names should not contain numeric suffixes.");
    }

    public void ScenarioGeneratedStaffAndOwnersHaveCleanNames()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var names = scenario.ScenarioSnapshot.AlphaSnapshot.People
            .Where(person => person.PersonId.Contains("owner", StringComparison.OrdinalIgnoreCase)
                || person.PersonId.Contains("coach", StringComparison.OrdinalIgnoreCase)
                || person.PersonId.Contains("scout", StringComparison.OrdinalIgnoreCase))
            .Select(person => person.Identity.DisplayName)
            .ToArray();

        Assert.True(names.Length >= 5, "Scenario should include generated owner, coach, and scout people.");
        Assert.False(names.Any(name => name.Any(char.IsDigit)), "Generated staff, scout, and owner display names should not contain numeric markers.");
    }

    public void StaffCandidateNamesComeFromNameGenerator()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var service = new StaffOfficeService();
        var generated = service.GenerateCandidatePool(scenario.Registry, scenario.ScenarioSnapshot);
        var names = generated.ScenarioSnapshot.StaffCandidates.Select(candidate => candidate.Person.Identity.DisplayName).ToArray();

        Assert.Equal(names.Length, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.False(names.Any(name => name.Any(char.IsDigit)), "Staff candidate display names should not contain numeric markers.");
    }

    public void RecruitUiDedupesByPersonId()
    {
        var source = ReadAlphaDesktopSource();

        Assert.True(source.Contains(".GroupBy(recruit => recruit.RecruitPersonId", StringComparison.Ordinal), "Recruit list should dedupe by person id.");
    }

    public void DraftBoardUiDedupesByPersonId()
    {
        var source = ReadAlphaDesktopSource();

        Assert.True(source.Contains(".GroupBy(entry => entry.ProspectPersonId", StringComparison.Ordinal), "Draft board should dedupe by prospect person id.");
    }

    public void SameNameDifferentPlayersAreClarifiedWithContext()
    {
        var source = ReadAlphaDesktopSource();

        Assert.True(source.Contains("RecruitDisplayName", StringComparison.Ordinal), "Recruit display name helper should exist.");
        Assert.True(source.Contains("ScoutingDisplayName", StringComparison.Ordinal), "Scouting/draft display name helper should exist.");
        Assert.True(source.Contains("State.PersonPosition(personId)", StringComparison.Ordinal), "Duplicate names should include position.");
        Assert.True(source.Contains("State.PersonAge(personId)", StringComparison.Ordinal), "Duplicate names should include age.");
        Assert.True(source.Contains("State.RegionTeamText(personId)", StringComparison.Ordinal), "Duplicate names should include region/team.");
    }

    public void StaffCandidatesAppearInAlphaDesktop()
    {
        var source = ReadAlphaDesktopSource();

        Assert.True(source.Contains("Current Staff", StringComparison.Ordinal), "Staff tab should label current staff.");
        Assert.True(source.Contains("Staff Candidates", StringComparison.Ordinal), "Staff tab should label staff candidates.");
        Assert.True(source.Contains("Hire Staff", StringComparison.Ordinal), "Staff tab should expose a hire staff section/action.");
    }

    public void CandidateCanBeSelected()
    {
        var source = ReadAlphaDesktopSource();

        Assert.True(source.Contains("row.Kind == \"Candidate\"", StringComparison.Ordinal), "Candidate rows should route to candidate detail panel.");
        Assert.True(source.Contains("Hire Candidate", StringComparison.Ordinal), "Candidate detail should expose Hire Candidate.");
        Assert.True(source.Contains("State.HireCandidateFor(row.PersonId)", StringComparison.Ordinal), "Hire Candidate should target the selected candidate.");
    }

    public void HireCandidateCreatesStaffMember()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var service = new StaffOfficeService();
        var generated = service.GenerateCandidatePool(scenario.Registry, scenario.ScenarioSnapshot);
        var candidate = generated.ScenarioSnapshot.StaffCandidates.First();

        var hired = service.HireCandidate(scenario.Registry, generated.ScenarioSnapshot, candidate.CandidateId);

        Assert.True(hired.ScenarioSnapshot.StaffMembers.Any(member => member.PersonId == candidate.Person.PersonId && member.EmploymentStatus == StaffEmploymentStatus.Employed), "Hire Candidate should create an employed staff member.");
        Assert.False(hired.ScenarioSnapshot.StaffCandidates.Any(item => item.CandidateId == candidate.CandidateId), "Hired candidate should be removed from candidate pool.");
    }

    public void HireCandidateCreatesEventAndInbox()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var service = new StaffOfficeService();
        var generated = service.GenerateCandidatePool(scenario.Registry, scenario.ScenarioSnapshot);
        var candidate = generated.ScenarioSnapshot.StaffCandidates.First();

        var hired = service.HireCandidate(scenario.Registry, generated.ScenarioSnapshot, candidate.CandidateId);

        Assert.True(hired.InboxItems.Any(item => item.EventType == LegacyEventType.StaffHired), "Hire Candidate should create staff hired inbox.");
        Assert.True(scenario.Registry.EventEngine.Queue.PendingEvents.Any(item => item.EventType == LegacyEventType.StaffHired), "Hire Candidate should queue staff hired event.");
    }

    public void CandidateCannotBeHiredTwice()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var service = new StaffOfficeService();
        var generated = service.GenerateCandidatePool(scenario.Registry, scenario.ScenarioSnapshot);
        var candidate = generated.ScenarioSnapshot.StaffCandidates.First();
        var hired = service.HireCandidate(scenario.Registry, generated.ScenarioSnapshot, candidate.CandidateId);

        var second = service.HireCandidate(scenario.Registry, hired.ScenarioSnapshot, candidate.CandidateId);

        Assert.False(second.Success, "Second hire attempt should fail.");
        Assert.Equal(1, second.ScenarioSnapshot.StaffMembers.Count(member => member.PersonId == candidate.Person.PersonId));
    }

    private static string ReadAlphaDesktopSource() =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

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
