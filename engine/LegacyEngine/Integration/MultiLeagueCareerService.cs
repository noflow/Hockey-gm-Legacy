using LegacyEngine.RuleEngine;

namespace LegacyEngine.Integration;

public sealed class MultiLeagueCareerService
{
    public IReadOnlyList<LeagueProfile> BuildLeagueProfiles() =>
        new[]
        {
            BuildJuniorProfile(),
            BuildNhlProfile(),
            BuildAhlProfile(),
            BuildCustomPlaceholderProfile()
        };

    public LeagueProfile GetProfile(LeagueExperience experience) =>
        BuildLeagueProfiles().Single(profile => profile.Experience == experience);

    public IReadOnlyList<TeamSelectionOption> TeamsFor(LeagueExperience experience) =>
        GetProfile(experience).Teams;

    public LeagueSelectionResult SelectLeagueAndTeam(
        LeagueExperience experience,
        string organizationId,
        GmProfileCreationSettings? gmSettings = null)
    {
        var profile = GetProfile(experience);
        var team = profile.Teams.SingleOrDefault(item => string.Equals(item.OrganizationId, organizationId, StringComparison.Ordinal))
            ?? throw new ArgumentException($"Team '{organizationId}' is not available in {profile.Identity.Name}.", nameof(organizationId));
        var settings = SettingsFor(profile, team, gmSettings);
        var result = new LeagueSelectionResult(profile, team, settings, profile.Rulebook);
        result.Validate();
        return result;
    }

    public NewGmScenarioResult CreateScenario(LeagueSelectionResult selection)
    {
        selection.Validate();
        return NewGmScenarioBootstrapper.CreateScenario(selection.ScenarioSettings, selection.Rulebook);
    }

    private static NewGmScenarioSettings SettingsFor(
        LeagueProfile profile,
        TeamSelectionOption team,
        GmProfileCreationSettings? gmSettings)
    {
        var safe = team.OrganizationId.Replace("org-", string.Empty, StringComparison.Ordinal);
        return new NewGmScenarioSettings(
            WorldName: $"Hockey GM Legacy - {profile.Identity.ShortName}",
            LeagueId: profile.Identity.LeagueId,
            SeasonId: $"season-{profile.Identity.ShortName.ToLowerInvariant()}-2025",
            SeasonYear: 2025,
            OrganizationId: team.OrganizationId,
            RosterId: $"roster-{safe}-2026",
            DraftBoardId: $"draft-board-{safe}-2026",
            PlayerGmPersonId: "person-player-gm-001")
        {
            GmCreationSettings = gmSettings,
            LeagueExperience = profile.Experience,
            LeagueProfile = profile,
            TeamSelection = team,
            TeamName = team.TeamName,
            TeamCity = team.City,
            TeamRegion = team.Region,
            TeamCountry = team.Country,
            PreviousRecord = team.PreviousRecord,
            OwnerExpectations = team.OwnerExpectations,
            CurrentChampion = profile.Identity.CurrentChampion,
            ParentOrganizationId = team.ParentOrganizationId,
            AffiliateOrganizationId = team.AffiliateOrganizationId
        };
    }

    private static LeagueProfile BuildJuniorProfile()
    {
        var rulebook = RulebookPresets.CreateJuniorMajor();
        var identity = new LeagueIdentity(
            "junior-league-alpha",
            "Canadian Major Junior Alpha",
            "Junior",
            "Player recruitment, amateur scouting, the junior draft, development, and roster turnover.",
            "Approachable",
            new[] { "Recruiting", "Scouting", "Draft", "Development" },
            "Red Deer Royals",
            "Prairie clubs have traded recent titles while the Falcons try to rebuild trust.");
        return Profile(
            LeagueExperience.Junior,
            identity,
            rulebook,
            new[]
            {
                Team("org-prairie-falcons", "Prairie Falcons", "Moose Jaw", "SK", "Canada", "28-29-5", "Develop prospects and restore community trust.", 1_150_000m, 44, "Strong", "Balanced", "Young but promising"),
                Team("org-red-deer-royals", "Red Deer Royals", "Red Deer", "AB", "Canada", "42-16-4", "Defend contender status without emptying the pipeline.", 1_350_000m, 68, "Average", "Demanding", "High-end"),
                Team("org-halifax-harbour", "Halifax Harbour", "Halifax", "NS", "Canada", "22-35-5", "Rebuild through scouting and patient development.", 980_000m, 35, "Excellent", "Hard", "Thin")
            },
            new[] { "26-player junior roster target", "Age and import limits matter" },
            new[] { "Junior amateur draft enabled", "Reverse standings order" },
            new[] { "Basic player and pick trade logic enabled" },
            new[] { "Junior agreements, education, housing, staff contracts" },
            new[] { "Owner hockey operations budget" },
            new[] { "Development path and ice-time decisions are central" },
            new[] { "No parent affiliate flow by default" },
            new[] { "Recruiting is a daily GM focus" });
    }

    private static LeagueProfile BuildNhlProfile()
    {
        var rulebook = RulebookPresets.CreateNhlStyle();
        var identity = new LeagueIdentity(
            "nhl-league-alpha",
            "North American Pro Alpha",
            "NHL",
            "Pro roster decisions, trades, prospect rights, free agency, staff, budget pressure, and placeholder salary planning.",
            "Hard",
            new[] { "Contracts", "Trades", "Free Agency", "Prospect Rights", "Salary Planning" },
            "Seattle Cascades",
            "Veteran clubs chase immediate results while prospect builders stockpile draft capital.");
        return Profile(
            LeagueExperience.Nhl,
            identity,
            rulebook,
            new[]
            {
                Team("org-seattle-cascades", "Seattle Cascades", "Seattle", "WA", "USA", "47-25-10", "Win now while keeping the prospect pipeline alive.", 92_000_000m, 72, "Average", "Hard", "Contender", affiliateOrganizationId: "org-evergreen-comets"),
                Team("org-buffalo-lakes", "Buffalo Lakes", "Buffalo", "NY", "USA", "31-39-12", "Turn young assets into a playoff push.", 82_500_000m, 48, "Strong", "Medium", "Developing"),
                Team("org-arizona-sun", "Arizona Sun", "Phoenix", "AZ", "USA", "24-45-13", "Rebuild patiently and protect flexibility.", 68_000_000m, 34, "Excellent", "Hard", "Low")
            },
            new[] { "23-player pro active roster" },
            new[] { "Seven-round draft enabled", "Prospect rights retained after draft" },
            new[] { "Multi-asset trades with organization strategy" },
            new[] { "Pro, staff, scout, coach, and GM contracts" },
            new[] { "Large hockey operations budget", "Salary planning placeholder only" },
            new[] { "Prospect development and AHL pathway matter" },
            new[] { "Affiliate references supported" },
            new[] { "Recruiting is secondary to draft and rights management" });
    }

    private static LeagueProfile BuildAhlProfile()
    {
        var rulebook = AhlRulebookWithAffiliate("org-seattle-cascades", "org-evergreen-comets");
        var identity = new LeagueIdentity(
            "ahl-league-alpha",
            "Development Pro Alpha",
            "AHL",
            "Development, assigned players, affiliate roster balance, call-up/send-down placeholders, and veteran leadership.",
            "Medium",
            new[] { "Development", "Affiliate Management", "Assigned Players", "Roster Balance" },
            "Evergreen Comets",
            "Parent clubs shape the league, but strong AHL staffs turn uncertainty into NHL-ready depth.");
        return Profile(
            LeagueExperience.Ahl,
            identity,
            rulebook,
            new[]
            {
                Team("org-evergreen-comets", "Evergreen Comets", "Spokane", "WA", "USA", "39-27-6", "Develop assigned prospects while staying competitive.", 6_500_000m, 56, "Parent-driven", "Medium", "Balanced", parentOrganizationId: "org-seattle-cascades"),
                Team("org-rochester-riverhawks", "Rochester Riverhawks", "Rochester", "NY", "USA", "34-31-7", "Stabilize veteran leadership and improve player readiness.", 5_900_000m, 51, "Average", "Medium", "Veteran-heavy", parentOrganizationId: "org-buffalo-lakes"),
                Team("org-tucson-road", "Tucson Road", "Tucson", "AZ", "USA", "28-36-8", "Absorb young assignments and improve development outcomes.", 5_200_000m, 42, "Strong", "Hard", "Raw", parentOrganizationId: "org-arizona-sun")
            },
            new[] { "AHL-style roster with assigned and contracted players" },
            new[] { "Amateur draft disabled" },
            new[] { "Roster and assigned-player movement placeholders" },
            new[] { "AHL contracts, two-way references, tryouts, staff contracts" },
            new[] { "Moderate hockey operations budget" },
            new[] { "Development outcomes are the main success measure" },
            new[] { "Parent and affiliate references supported", "AssignedFromParentClub source supported" },
            new[] { "Recruiting is limited; roster comes through assignments and signings" });
    }

    private static LeagueProfile BuildCustomPlaceholderProfile()
    {
        var rulebook = RulebookPresets.Create(DraftLeaguePreset.Custom, customRounds: 10);
        var identity = new LeagueIdentity(
            "custom-league-alpha",
            "Custom League Placeholder",
            "Custom",
            "Placeholder for future user-defined rulebooks and league formats.",
            "Variable",
            new[] { "Custom Rulebook", "Custom Teams", "Custom Career" },
            "Not set",
            "Custom league history will come from future custom setup.");
        return Profile(
            LeagueExperience.Custom,
            identity,
            rulebook,
            new[]
            {
                Team("org-custom-north", "Custom North", "Custom City", "NA", "Canada", "0-0-0", "Placeholder expectations.", 1_000_000m, 50, "Unknown", "Variable", "Unknown")
            },
            new[] { "Uses placeholder custom roster rules" },
            new[] { "Custom draft rounds placeholder" },
            new[] { "Uses existing trade engine rules" },
            new[] { "Uses existing contract rules" },
            new[] { "Uses existing budget rules" },
            new[] { "Uses existing development rules" },
            new[] { "Affiliate settings depend on future custom rulebook" },
            new[] { "Recruiting settings depend on future custom rulebook" });
    }

    private static LeagueProfile Profile(
        LeagueExperience experience,
        LeagueIdentity identity,
        Rulebook rulebook,
        IReadOnlyList<TeamSelectionOption> teams,
        IReadOnlyList<string> roster,
        IReadOnlyList<string> draft,
        IReadOnlyList<string> trade,
        IReadOnlyList<string> contract,
        IReadOnlyList<string> budget,
        IReadOnlyList<string> development,
        IReadOnlyList<string> affiliate,
        IReadOnlyList<string> recruiting)
    {
        var profile = new LeagueProfile(experience, identity, rulebook, teams, roster, draft, trade, contract, budget, development, affiliate, recruiting);
        profile.Validate();
        return profile;
    }

    private static TeamSelectionOption Team(
        string organizationId,
        string teamName,
        string city,
        string region,
        string country,
        string previousRecord,
        string ownerExpectations,
        decimal budget,
        int currentGmReputation,
        string prospectStrength,
        string difficulty,
        string rosterQuality,
        string? parentOrganizationId = null,
        string? affiliateOrganizationId = null) =>
        new(
            organizationId,
            teamName,
            city,
            region,
            country,
            "crest placeholder",
            previousRecord,
            ownerExpectations,
            budget,
            currentGmReputation,
            prospectStrength,
            difficulty,
            rosterQuality,
            parentOrganizationId,
            affiliateOrganizationId);

    private static Rulebook AhlRulebookWithAffiliate(string parentOrganizationId, string affiliateOrganizationId)
    {
        var source = RulebookPresets.CreateAhlStyle();
        return new Rulebook
        {
            RulebookId = source.RulebookId,
            LeagueType = source.LeagueType,
            Version = source.Version,
            RosterRules = source.RosterRules,
            EligibilityRules = source.EligibilityRules,
            ContractRules = source.ContractRules,
            DraftRules = source.DraftRules,
            PlayoffRules = source.PlayoffRules,
            BudgetRules = source.BudgetRules,
            SeasonRules = source.SeasonRules,
            StaffRules = source.StaffRules,
            AffiliateRules = source.AffiliateRules is null
                ? null
                : new AffiliateRules
                {
                    AffiliateEnabled = source.AffiliateRules.AffiliateEnabled,
                    ParentOrganizationId = parentOrganizationId,
                    AffiliateOrganizationId = affiliateOrganizationId,
                    ReceivesNonNhlReadyDraftedProspects = source.AffiliateRules.ReceivesNonNhlReadyDraftedProspects,
                    AllowedAcquisitionSources = source.AffiliateRules.AllowedAcquisitionSources,
                    GmResponsibilities = source.AffiliateRules.GmResponsibilities
                }
        };
    }
}
