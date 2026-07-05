using LegacyEngine.Integration;
using LegacyEngine.Organizations;
using LegacyEngine.Rosters;
using LegacyEngine.RuleEngine;

internal sealed class AhlAffiliateRulebookTests
{
    public void AhlRulebookHasDraftDisabled()
    {
        var rulebook = new RulebookLoader().LoadFromFile(Path.Combine(FindRepositoryRoot(), "data", "rulebooks", "ahl_style_v1.json"));

        Assert.Equal("ahl_style", rulebook.LeagueType);
        Assert.False(rulebook.DraftRules!.DraftEnabled, "AHL-style rulebook should disable the amateur draft.");
        Assert.Equal(0, rulebook.DraftRules.Rounds);
        Assert.True(rulebook.AffiliateRules!.ReceivesNonNhlReadyDraftedProspects, "AHL affiliates should receive non-NHL-ready drafted prospects from parent clubs.");
    }

    public void AhlPresetHasDraftDisabled()
    {
        var rulebook = RulebookPresets.Create(DraftLeaguePreset.AhlStyle);

        Assert.False(rulebook.DraftRules!.DraftEnabled, "AHL-style preset should disable draft.");
        Assert.True(rulebook.AffiliateRules!.AllowedAcquisitionSources.Contains(nameof(PlayerAcquisitionSource.AssignedFromParentClub)), "AHL preset should allow parent-club assignments.");
    }

    public void AhlOrganizationCanReferenceParentNhlOrganization()
    {
        var organization = new OrganizationEngine().CreateOrganization(
            "org-ahl-001",
            OrganizationType.Team,
            new OrganizationIdentity("Metro Comets", "Utica", "NY", "USA"),
            new DateOnly(2026, 7, 1),
            leagueId: "league-ahl-style",
            parentOrganizationId: "org-nhl-parent-001");

        Assert.Equal("org-nhl-parent-001", organization.ParentOrganizationId);
    }

    public void NhlOrganizationCanReferenceAhlAffiliateOrganization()
    {
        var organization = new OrganizationEngine().CreateOrganization(
            "org-nhl-parent-001",
            OrganizationType.Team,
            new OrganizationIdentity("Metro NHL Club", "New York", "NY", "USA"),
            new DateOnly(2026, 7, 1),
            leagueId: "league-nhl-style",
            affiliateOrganizationId: "org-ahl-001");

        Assert.Equal("org-ahl-001", organization.AffiliateOrganizationId);
    }

    public void PlayerCanBeAddedAsAssignedFromParentClub()
    {
        var engine = new RosterEngine();
        var roster = engine.CreateRoster("roster-ahl-001", "org-ahl-001");
        var result = engine.AddPlayer(
            roster,
            new RosterMove(
                RosterMoveType.Add,
                "person-prospect-001",
                new DateOnly(2026, 10, 1),
                RosterPosition.Center,
                RosterStatus.Active,
                Age: 20,
                AcquisitionSource: PlayerAcquisitionSource.AssignedFromParentClub));

        Assert.True(result.Success, "Assigned parent-club prospect should be added to AHL roster.");
        Assert.Equal(PlayerAcquisitionSource.AssignedFromParentClub, result.Roster.Players[0].AcquisitionSource);
    }

    public void AhlDraftUiIsDisabledByRulebook()
    {
        var rulebook = RulebookPresets.Create(DraftLeaguePreset.AhlStyle);

        Assert.False(DraftUiPolicy.IsDraftUiEnabled(rulebook), "Draft UI should be hidden for AHL-style leagues unless custom rules enable a draft.");
    }

    public void JuniorAndNhlDraftBehaviorRemainEnabled()
    {
        Assert.True(DraftUiPolicy.IsDraftUiEnabled(RulebookPresets.Create(DraftLeaguePreset.JuniorMajor)), "Junior draft UI should remain enabled.");
        Assert.True(DraftUiPolicy.IsDraftUiEnabled(RulebookPresets.Create(DraftLeaguePreset.NhlStyle)), "NHL-style draft UI should remain enabled.");
        Assert.Equal(15, RulebookPresets.Create(DraftLeaguePreset.JuniorMajor).DraftRules!.Rounds);
        Assert.Equal(7, RulebookPresets.Create(DraftLeaguePreset.NhlStyle).DraftRules!.Rounds);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var rulebookPath = Path.Combine(directory.FullName, "data", "rulebooks", "ahl_style_v1.json");
            if (File.Exists(rulebookPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find Hockey GM Legacy repository root.");
    }
}
