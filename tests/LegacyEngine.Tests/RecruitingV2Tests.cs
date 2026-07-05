using LegacyEngine.Events;
using LegacyEngine.Integration;
using LegacyEngine.Recruiting;

internal sealed class RecruitingV2Tests
{
    public void RecruitProfileIncludesPriorities()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var recruitId = RecruitId(scenario);
        var profile = new RecruitingV2Service().BuildProfile(scenario.ScenarioSnapshot, recruitId);

        Assert.True(profile.Priorities.ContainsKey(RecruitPriority.TeamCulture), "Recruit profile should include team culture.");
        Assert.True(profile.Priorities.ContainsKey(RecruitPriority.TrustInGm), "Recruit profile should include trust in GM.");
        Assert.True(profile.Priorities.ContainsKey(RecruitPriority.PlayingRole), "Recruit profile should include playing role.");
    }

    public void RecruitProfileIncludesFamilyPriorities()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var profile = new RecruitingV2Service().BuildProfile(scenario.ScenarioSnapshot, RecruitId(scenario));

        Assert.True(profile.FamilyPriorities.ContainsKey(RecruitingFamilyPriority.Education), "Family education priority should be present.");
        Assert.True(profile.FamilyPriorities.ContainsKey(RecruitingFamilyPriority.BilletQuality), "Billet quality priority should be present.");
        Assert.True(profile.FamilyPriorities.ContainsKey(RecruitingFamilyPriority.TrustInOrganization), "Family trust priority should be present.");
    }

    public void RecruitHasCompetingTeams()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var profile = new RecruitingV2Service().BuildProfile(scenario.ScenarioSnapshot, RecruitId(scenario));

        Assert.True(profile.CompetingTeams.Count >= 2, "Recruit profile should include competing teams.");
        Assert.True(profile.TopCompetitor is not null, "Recruit profile should identify top competitor.");
        Assert.True(profile.WhyTheyMayRejectUs.Contains(profile.TopCompetitor!.TeamName, StringComparison.Ordinal), "Rejection context should name the top competitor.");
    }

    public void CallRecruitChangesInterestAndTrust()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var recruitId = RecruitId(scenario);
        var before = new RecruitingV2Service().BuildProfile(scenario.ScenarioSnapshot, recruitId);

        var result = new RecruitingV2Service().CallRecruit(scenario.Registry, scenario.ScenarioSnapshot, recruitId);

        Assert.True(result.Profile.InterestLevel > before.InterestLevel, "Recruit call should increase interest.");
        Assert.True(result.Profile.RelationshipWithGm > before.RelationshipWithGm, "Recruit call should improve GM trust.");
    }

    public void CallFamilyCanAffectInterest()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var recruitId = RecruitId(scenario);
        var before = new RecruitingV2Service().BuildProfile(scenario.ScenarioSnapshot, recruitId);

        var result = new RecruitingV2Service().CallFamily(scenario.Registry, scenario.ScenarioSnapshot, recruitId);

        Assert.True(result.Profile.InterestLevel > before.InterestLevel, "Family call should affect interest.");
        Assert.Equal(LegacyEventType.RecruitFamilyCallResult, result.InboxItems[0].EventType);
    }

    public void VisitCanBeAcceptedOrDeclined()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var result = new RecruitingV2Service().InviteVisit(scenario.Registry, scenario.ScenarioSnapshot, RecruitId(scenario));

        Assert.True(result.Message.Contains("accepted", StringComparison.OrdinalIgnoreCase) || result.Message.Contains("declined", StringComparison.OrdinalIgnoreCase), "Visit result should explain accepted or declined.");
        Assert.Equal(LegacyEventType.RecruitVisitUpdated, result.InboxItems[0].EventType);
    }

    public void PromiseIsRecorded()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var recruitId = RecruitId(scenario);

        var result = new RecruitingV2Service().MakePromise(scenario.Registry, scenario.ScenarioSnapshot, recruitId, RecruitingPromiseType.TopSixRole);
        var updated = result.ScenarioSnapshot.AlphaSnapshot.Recruits.Single(recruit => recruit.RecruitPersonId == recruitId);

        Assert.True(updated.Promises.Any(promise => promise.PromiseType == RecruitingPromiseType.TopSixRole), "Promise should be recorded on the recruit profile.");
        Assert.True(updated.Promises.Any(promise => promise.TargetDate is not null && !promise.IsBroken), "Promise should be future-trackable without broken-promise enforcement.");
    }

    public void PromiseAffectsInterestAndTrust()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var recruitId = RecruitId(scenario);
        var before = new RecruitingV2Service().BuildProfile(scenario.ScenarioSnapshot, recruitId);

        var result = new RecruitingV2Service().MakePromise(scenario.Registry, scenario.ScenarioSnapshot, recruitId, RecruitingPromiseType.DevelopmentFocus);

        Assert.True(result.Profile.InterestLevel > before.InterestLevel, "Promise should improve interest.");
        Assert.True(result.Profile.RelationshipWithGm > before.RelationshipWithGm, "Promise should improve trust.");
    }

    public void OfferCanBeWithdrawn()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var recruitId = RecruitId(scenario);
        var offered = new NewGmScenarioActions().MakeRecruitingOffer(scenario.Registry, scenario.ScenarioSnapshot, recruitId);

        var result = new RecruitingV2Service().WithdrawOffer(scenario.Registry, offered.ScenarioSnapshot, recruitId);

        Assert.Equal(RecruitStatus.Withdrawn, result.Profile.Status);
        Assert.Equal(LegacyEventType.RecruitOfferWithdrawn, result.InboxItems[0].EventType);
    }

    public void DecisionExplanationIsGenerated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var recruitId = RecruitId(scenario);
        var service = new RecruitingV2Service();
        var called = service.CallRecruit(scenario.Registry, scenario.ScenarioSnapshot, recruitId);
        var promised = service.MakePromise(scenario.Registry, called.ScenarioSnapshot, recruitId, RecruitingPromiseType.TopSixRole);

        var result = service.MakeDecision(scenario.Registry, promised.ScenarioSnapshot, recruitId);

        Assert.True(result.DecisionExplanation.Contains(result.Profile.Name, StringComparison.Ordinal), "Decision explanation should name the recruit.");
        Assert.True(result.DecisionExplanation.Contains("because", StringComparison.OrdinalIgnoreCase), "Decision explanation should explain why.");
    }

    public void CompetingTeamCanWinRecruit()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var recruitId = RecruitId(scenario);
        var lowInterest = WithInterest(scenario.ScenarioSnapshot, recruitId, -100);

        var result = new RecruitingV2Service().MakeDecision(scenario.Registry, lowInterest, recruitId);

        Assert.Equal(RecruitStatus.Rejected, result.Profile.Status);
        Assert.True(result.DecisionExplanation.Contains(result.Profile.TopCompetitor!.TeamName, StringComparison.Ordinal), "Decision explanation should identify the winning competitor.");
    }

    public void HumanIntelligenceAffectsDecision()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var recruitId = RecruitId(scenario);
        var service = new RecruitingV2Service();
        var low = service.MakeDecision(scenario.Registry, WithInterest(scenario.ScenarioSnapshot, recruitId, -100), recruitId);
        var called = service.CallRecruit(scenario.Registry, scenario.ScenarioSnapshot, recruitId);
        var promised = service.MakePromise(scenario.Registry, called.ScenarioSnapshot, recruitId, RecruitingPromiseType.ImmediateRosterSpot);
        var high = service.MakeDecision(scenario.Registry, promised.ScenarioSnapshot, recruitId);

        Assert.True(low.Profile.Status != high.Profile.Status || high.DecisionExplanation != low.DecisionExplanation, "Human Intelligence inputs should affect the decision path.");
    }

    public void InboxMessagesAreCreated()
    {
        var scenario = NewGmScenarioBootstrapper.CreateScenario();
        var recruitId = RecruitId(scenario);
        var service = new RecruitingV2Service();

        var call = service.CallRecruit(scenario.Registry, scenario.ScenarioSnapshot, recruitId);
        var promise = service.MakePromise(scenario.Registry, call.ScenarioSnapshot, recruitId, RecruitingPromiseType.CampInvite);
        var scout = service.AskScoutForMoreInformation(scenario.Registry, promise.ScenarioSnapshot, recruitId);

        Assert.True(call.InboxItems.Count > 0, "Recruit call should create inbox.");
        Assert.True(promise.InboxItems.Count > 0, "Promise should create inbox.");
        Assert.True(scout.InboxItems.Any(item => item.Summary.Contains(scout.Profile.Name, StringComparison.Ordinal)), "Scout recruiting note should name the recruit.");
    }

    public void AlphaDesktopExposesRecruitingActions()
    {
        var text = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(text.Contains("Call Recruit", StringComparison.Ordinal), "Desktop should expose Call Recruit.");
        Assert.True(text.Contains("Call Family", StringComparison.Ordinal), "Desktop should expose Call Family.");
        Assert.True(text.Contains("Invite Visit", StringComparison.Ordinal), "Desktop should expose Invite Visit.");
        Assert.True(text.Contains("Make Offer", StringComparison.Ordinal), "Desktop should expose Make Offer.");
        Assert.True(text.Contains("Make Promise", StringComparison.Ordinal), "Desktop should expose Make Promise.");
        Assert.True(text.Contains("Ask Scout", StringComparison.Ordinal), "Desktop should expose Ask Scout.");
        Assert.True(text.Contains("Withdraw Offer", StringComparison.Ordinal), "Desktop should expose Withdraw Offer.");
    }

    public void DossierOpensFromRecruit()
    {
        var text = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "client", "AlphaDesktop", "Program.cs"));

        Assert.True(text.Contains("if (tab == \"Recruits\")", StringComparison.Ordinal), "Recruit detail should have a selected recruit section.");
        Assert.True(text.Contains("View Dossier", StringComparison.Ordinal), "Recruit detail should expose View Dossier.");
        Assert.True(text.Contains("OpenDossierFor(row.PersonId)", StringComparison.Ordinal), "Recruit dossier action should use selected person id.");
    }

    public void RecruitingV2HasNoGodotSaveOrGameSimulationDependency()
    {
        var root = FindRepositoryRoot();
        var files = Directory.GetFiles(Path.Combine(root, "engine", "LegacyEngine", "Integration"), "RecruitingV2*.cs");
        var text = string.Join(Environment.NewLine, files.Select(File.ReadAllText));

        Assert.False(text.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Recruiting v2 should not depend on Godot.");
        Assert.False(text.Contains("Save", StringComparison.Ordinal), "Recruiting v2 should not implement save/load.");
        Assert.False(text.Contains("GameSimulation", StringComparison.Ordinal), "Recruiting v2 should not implement game simulation.");
    }

    private static string RecruitId(NewGmScenarioResult scenario) =>
        scenario.ScenarioSnapshot.AlphaSnapshot.Recruits.First().RecruitPersonId;

    private static NewGmScenarioSnapshot WithInterest(NewGmScenarioSnapshot scenario, string recruitId, int delta)
    {
        var recruit = scenario.AlphaSnapshot.Recruits.Single(item => item.RecruitPersonId == recruitId)
            .ChangeInterest(scenario.Organization.OrganizationId, delta, scenario.CurrentDate)
            .SetStatus(RecruitStatus.Interested);
        var recruits = scenario.AlphaSnapshot.Recruits
            .Select(item => item.RecruitPersonId == recruitId ? recruit : item)
            .ToArray();
        var alpha = scenario.AlphaSnapshot with { Recruits = recruits };
        var updated = scenario with { AlphaSnapshot = alpha };
        updated.Validate();
        return updated;
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
