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
        var rulebook = profile.Experience == LeagueExperience.Ahl
            ? AhlRulebookWithAffiliate(team.ParentOrganizationId ?? "parent-unassigned", team.OrganizationId)
            : profile.Rulebook;
        var result = new LeagueSelectionResult(profile with { Rulebook = rulebook }, team, settings, rulebook);
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
            BuildJuniorTeams(),
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
            BuildNhlTeams(),
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
            BuildAhlTeams(),
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

    private static IReadOnlyList<TeamSelectionOption> BuildNhlTeams()
    {
        var teams = NhlSeedTeams();
        var affiliates = AhlSeedTeams().ToDictionary(team => team.ParentOrganizationId, StringComparer.Ordinal);
        return teams.Select((team, index) => Team(
            team.Id,
            team.Name,
            team.City,
            team.Region,
            team.Country,
            RecordFor(index, 82),
            ExpectationFor(index, pro: true),
            68_000_000m + (index % 9) * 3_250_000m,
            42 + (index * 7 % 45),
            StrengthFor(index + 2),
            DifficultyFor(index),
            RosterQualityFor(index + 4),
            affiliateOrganizationId: affiliates.TryGetValue(team.Id, out var affiliate) ? affiliate.Id : null,
            leagueName: "NHL-style",
            divisionConference: team.Division,
            placeholderArena: $"{team.City} Civic Arena",
            staffQuality: StaffQualityFor(index),
            currentStrategy: StrategyFor(index, pro: true))).ToArray();
    }

    private static IReadOnlyList<TeamSelectionOption> BuildAhlTeams() =>
        AhlSeedTeams().Select((team, index) => Team(
            team.Id,
            team.Name,
            team.City,
            team.Region,
            team.Country,
            RecordFor(index + 5, 72),
            "Develop assigned players, balance veteran leadership, and stay ready for parent-club needs.",
            4_900_000m + (index % 8) * 325_000m,
            38 + (index * 5 % 38),
            index % 3 == 0 ? "Parent-driven" : StrengthFor(index + 1),
            DifficultyFor(index + 1),
            RosterQualityFor(index + 2),
            parentOrganizationId: team.ParentOrganizationId,
            leagueName: "AHL-style",
            divisionConference: team.Division,
            placeholderArena: $"{team.City} Development Center",
            staffQuality: StaffQualityFor(index + 1),
            currentStrategy: "Development focus with call-up risk and roster-balance pressure.")).ToArray();

    private static IReadOnlyList<TeamSelectionOption> BuildJuniorTeams()
    {
        var whl = JuniorSeedTeams("WHL", 23, new[]
        {
            ("org-prairie-falcons", "Prairie Falcons", "Moose Jaw", "SK"),
            ("org-red-deer-royals", "Red Deer Royals", "Red Deer", "AB"),
            ("org-brandon-steel", "Brandon Steel", "Brandon", "MB"),
            ("org-regina-plainsmen", "Regina Plainsmen", "Regina", "SK"),
            ("org-swift-current-riders", "Swift Current Riders", "Swift Current", "SK"),
            ("org-calgary-wolves", "Calgary Wolves", "Calgary", "AB"),
            ("org-edmonton-river", "Edmonton River", "Edmonton", "AB"),
            ("org-kelowna-flyers", "Kelowna Flyers", "Kelowna", "BC"),
            ("org-vancouver-island-tide", "Island Tide", "Victoria", "BC"),
            ("org-prince-george-pines", "Prince George Pines", "Prince George", "BC"),
            ("org-kamloops-copper", "Kamloops Copper", "Kamloops", "BC"),
            ("org-spokane-stars", "Spokane Stars", "Spokane", "WA"),
            ("org-portland-bridges", "Portland Bridges", "Portland", "OR"),
            ("org-everett-harbor", "Everett Harbor", "Everett", "WA"),
            ("org-tri-city-atoms", "Tri-City Atoms", "Kennewick", "WA"),
            ("org-medicine-hat-mustangs", "Medicine Hat Mustangs", "Medicine Hat", "AB"),
            ("org-lethbridge-wind", "Lethbridge Wind", "Lethbridge", "AB"),
            ("org-saskatoon-raiders", "Saskatoon Raiders", "Saskatoon", "SK"),
            ("org-prince-albert-north", "Prince Albert North", "Prince Albert", "SK"),
            ("org-winnipeg-royals", "Winnipeg Royals", "Winnipeg", "MB"),
            ("org-wenatchee-ridge", "Wenatchee Ridge", "Wenatchee", "WA"),
            ("org-red-deer-blazers", "Red Deer Blazers", "Red Deer", "AB"),
            ("org-bc-interior-saints", "Interior Saints", "Vernon", "BC")
        });
        var ohl = JuniorSeedTeams("OHL", 20, new[]
        {
            ("org-london-crowns", "London Crowns", "London", "ON"),
            ("org-ottawa-capitals-jr", "Ottawa Capitals Jr.", "Ottawa", "ON"),
            ("org-kingston-limestone", "Kingston Limestone", "Kingston", "ON"),
            ("org-kitchener-foundry", "Kitchener Foundry", "Kitchener", "ON"),
            ("org-windsor-border", "Windsor Border", "Windsor", "ON"),
            ("org-sarnia-lakers", "Sarnia Lakers", "Sarnia", "ON"),
            ("org-sault-north-stars", "Sault North Stars", "Sault Ste. Marie", "ON"),
            ("org-sudbury-miners", "Sudbury Miners", "Sudbury", "ON"),
            ("org-north-bay-trappers", "North Bay Trappers", "North Bay", "ON"),
            ("org-barrie-bays", "Barrie Bays", "Barrie", "ON"),
            ("org-oshawa-harbor", "Oshawa Harbor", "Oshawa", "ON"),
            ("org-peterborough-locks", "Peterborough Locks", "Peterborough", "ON"),
            ("org-guelph-royals", "Guelph Royals", "Guelph", "ON"),
            ("org-erie-lakefront", "Erie Lakefront", "Erie", "PA"),
            ("org-flint-forge", "Flint Forge", "Flint", "MI"),
            ("org-saginaw-river", "Saginaw River", "Saginaw", "MI"),
            ("org-niagara-vines", "Niagara Vines", "St. Catharines", "ON"),
            ("org-hamilton-steel", "Hamilton Steel", "Hamilton", "ON"),
            ("org-mississauga-metro", "Mississauga Metro", "Mississauga", "ON"),
            ("org-owen-sound-bayshore", "Owen Sound Bayshore", "Owen Sound", "ON")
        });
        var qmjhl = JuniorSeedTeams("QMJHL", 18, new[]
        {
            ("org-halifax-harbour", "Halifax Harbour", "Halifax", "NS"),
            ("org-moncton-tide", "Moncton Tide", "Moncton", "NB"),
            ("org-saint-john-fog", "Saint John Fog", "Saint John", "NB"),
            ("org-charlottetown-red", "Charlottetown Red", "Charlottetown", "PE"),
            ("org-cape-breton-coal", "Cape Breton Coal", "Sydney", "NS"),
            ("org-rimouski-river", "Rimouski River", "Rimouski", "QC"),
            ("org-quebec-citadels", "Quebec Citadels", "Quebec City", "QC"),
            ("org-chicoutimi-saguenay", "Chicoutimi Saguenay", "Saguenay", "QC"),
            ("org-drummondville-mills", "Drummondville Mills", "Drummondville", "QC"),
            ("org-victoriaville-maples", "Victoriaville Maples", "Victoriaville", "QC"),
            ("org-sherbrooke-green", "Sherbrooke Green", "Sherbrooke", "QC"),
            ("org-blainville-north", "Blainville North", "Boisbriand", "QC"),
            ("org-gatineau-river", "Gatineau River", "Gatineau", "QC"),
            ("org-rouyn-noranda-mines", "Rouyn-Noranda Mines", "Rouyn-Noranda", "QC"),
            ("org-val-dor-gold", "Val-d'Or Gold", "Val-d'Or", "QC"),
            ("org-baie-comeau-north", "Baie-Comeau North", "Baie-Comeau", "QC"),
            ("org-shawinigan-rapids", "Shawinigan Rapids", "Shawinigan", "QC"),
            ("org-acadie-bathurst-shore", "Acadie-Bathurst Shore", "Bathurst", "NB")
        });

        return whl.Concat(ohl).Concat(qmjhl).ToArray();
    }

    private static IReadOnlyList<TeamSelectionOption> JuniorSeedTeams(string leagueName, int expectedCount, IReadOnlyList<(string Id, string Name, string City, string Region)> seeds)
    {
        if (seeds.Count != expectedCount)
        {
            throw new InvalidOperationException($"{leagueName} seed count must be {expectedCount}.");
        }

        return seeds.Select((team, index) => Team(
            team.Id,
            team.Name,
            team.City,
            team.Region,
            team.Region is "WA" or "OR" or "PA" or "MI" ? "USA" : "Canada",
            RecordFor(index + leagueName.Length, 68),
            index % 3 == 0 ? "Develop prospects and protect community trust." : "Compete while keeping the pipeline healthy.",
            950_000m + (index % 7) * 75_000m,
            35 + (index * 6 % 40),
            StrengthFor(index),
            DifficultyFor(index),
            RosterQualityFor(index),
            leagueName: leagueName,
            divisionConference: leagueName switch
            {
                "WHL" => index < 12 ? "Eastern Conference" : "Western Conference",
                "OHL" => index < 10 ? "Eastern Conference" : "Western Conference",
                _ => index < 9 ? "Maritimes / East" : "Quebec / West"
            },
            placeholderArena: $"{team.City} Community Arena",
            staffQuality: StaffQualityFor(index),
            currentStrategy: StrategyFor(index, pro: false))).ToArray();
    }

    private static IReadOnlyList<(string Id, string Name, string City, string Region, string Country, string Division)> NhlSeedTeams() =>
    [
        ("org-seattle-cascades", "Seattle Cascades", "Seattle", "WA", "USA", "Pacific"),
        ("org-buffalo-lakes", "Buffalo Lakes", "Buffalo", "NY", "USA", "Atlantic"),
        ("org-arizona-sun", "Arizona Sun", "Phoenix", "AZ", "USA", "Central"),
        ("org-atlantic-maritimers", "Atlantic Maritimers", "Halifax", "NS", "Canada", "Atlantic"),
        ("org-boston-harbor", "Boston Harbor", "Boston", "MA", "USA", "Atlantic"),
        ("org-manhattan-guardians", "Manhattan Guardians", "New York", "NY", "USA", "Metropolitan"),
        ("org-philadelphia-foundry", "Philadelphia Foundry", "Philadelphia", "PA", "USA", "Metropolitan"),
        ("org-pittsburgh-rivers", "Pittsburgh Rivers", "Pittsburgh", "PA", "USA", "Metropolitan"),
        ("org-carolina-pilots", "Carolina Pilots", "Raleigh", "NC", "USA", "Metropolitan"),
        ("org-florida-reef", "Florida Reef", "Sunrise", "FL", "USA", "Atlantic"),
        ("org-tampa-bay-bolts", "Tampa Bay Bolts", "Tampa", "FL", "USA", "Atlantic"),
        ("org-montreal-royals", "Montreal Royals", "Montreal", "QC", "Canada", "Atlantic"),
        ("org-toronto-towers", "Toronto Towers", "Toronto", "ON", "Canada", "Atlantic"),
        ("org-ottawa-capitals", "Ottawa Capitals", "Ottawa", "ON", "Canada", "Atlantic"),
        ("org-detroit-motors", "Detroit Motors", "Detroit", "MI", "USA", "Atlantic"),
        ("org-columbus-cannons", "Columbus Cannons", "Columbus", "OH", "USA", "Metropolitan"),
        ("org-chicago-stockyards", "Chicago Stockyards", "Chicago", "IL", "USA", "Central"),
        ("org-minnesota-north", "Minnesota North", "St. Paul", "MN", "USA", "Central"),
        ("org-winnipeg-wings", "Winnipeg Wings", "Winnipeg", "MB", "Canada", "Central"),
        ("org-dallas-lone-stars", "Dallas Lone Stars", "Dallas", "TX", "USA", "Central"),
        ("org-st-louis-arches", "St. Louis Arches", "St. Louis", "MO", "USA", "Central"),
        ("org-nashville-strings", "Nashville Strings", "Nashville", "TN", "USA", "Central"),
        ("org-denver-peaks", "Denver Peaks", "Denver", "CO", "USA", "Central"),
        ("org-las-vegas-silver", "Las Vegas Silver", "Las Vegas", "NV", "USA", "Pacific"),
        ("org-los-angeles-crowns", "Los Angeles Crowns", "Los Angeles", "CA", "USA", "Pacific"),
        ("org-anaheim-orange", "Anaheim Orange", "Anaheim", "CA", "USA", "Pacific"),
        ("org-san-jose-bays", "San Jose Bays", "San Jose", "CA", "USA", "Pacific"),
        ("org-vancouver-evergreens", "Vancouver Evergreens", "Vancouver", "BC", "Canada", "Pacific"),
        ("org-calgary-flamehawks", "Calgary Flamehawks", "Calgary", "AB", "Canada", "Pacific"),
        ("org-edmonton-oil-kings-pro", "Edmonton Oil Kings Pro", "Edmonton", "AB", "Canada", "Pacific"),
        ("org-utah-canyons", "Utah Canyons", "Salt Lake City", "UT", "USA", "Central"),
        ("org-quebec-nord", "Quebec Nord", "Quebec City", "QC", "Canada", "Atlantic")
    ];

    private static IReadOnlyList<(string Id, string Name, string City, string Region, string Country, string Division, string ParentOrganizationId)> AhlSeedTeams() =>
    [
        ("org-evergreen-comets", "Evergreen Comets", "Spokane", "WA", "USA", "Pacific", "org-seattle-cascades"),
        ("org-rochester-riverhawks", "Rochester Riverhawks", "Rochester", "NY", "USA", "North", "org-buffalo-lakes"),
        ("org-tucson-road", "Tucson Road", "Tucson", "AZ", "USA", "Pacific", "org-arizona-sun"),
        ("org-halifax-voyagers", "Halifax Voyagers", "Halifax", "NS", "Canada", "Atlantic", "org-atlantic-maritimers"),
        ("org-providence-bays", "Providence Bays", "Providence", "RI", "USA", "Atlantic", "org-boston-harbor"),
        ("org-hartford-anchors", "Hartford Anchors", "Hartford", "CT", "USA", "Atlantic", "org-manhattan-guardians"),
        ("org-lehigh-rail", "Lehigh Rail", "Allentown", "PA", "USA", "Atlantic", "org-philadelphia-foundry"),
        ("org-wilkes-mountains", "Wilkes-Barre Mountains", "Wilkes-Barre", "PA", "USA", "Atlantic", "org-pittsburgh-rivers"),
        ("org-charlotte-wings", "Charlotte Wings", "Charlotte", "NC", "USA", "Atlantic", "org-carolina-pilots"),
        ("org-palm-coast-reef", "Palm Coast Reef", "Palm Coast", "FL", "USA", "Atlantic", "org-florida-reef"),
        ("org-orlando-bolts", "Orlando Bolts", "Orlando", "FL", "USA", "Atlantic", "org-tampa-bay-bolts"),
        ("org-laval-royals", "Laval Royals", "Laval", "QC", "Canada", "North", "org-montreal-royals"),
        ("org-toronto-marlins", "Toronto Marlins", "Toronto", "ON", "Canada", "North", "org-toronto-towers"),
        ("org-belleville-capitals", "Belleville Capitals", "Belleville", "ON", "Canada", "North", "org-ottawa-capitals"),
        ("org-grand-rapids-motors", "Grand Rapids Motors", "Grand Rapids", "MI", "USA", "Central", "org-detroit-motors"),
        ("org-cleveland-cannons", "Cleveland Cannons", "Cleveland", "OH", "USA", "North", "org-columbus-cannons"),
        ("org-rockford-yards", "Rockford Yards", "Rockford", "IL", "USA", "Central", "org-chicago-stockyards"),
        ("org-iowa-north", "Iowa North", "Des Moines", "IA", "USA", "Central", "org-minnesota-north"),
        ("org-manitoba-wings", "Manitoba Wings", "Winnipeg", "MB", "Canada", "North", "org-winnipeg-wings"),
        ("org-texas-stars", "Texas Stars", "Cedar Park", "TX", "USA", "Central", "org-dallas-lone-stars"),
        ("org-springfield-arches", "Springfield Arches", "Springfield", "MO", "USA", "Central", "org-st-louis-arches"),
        ("org-milwaukee-strings", "Milwaukee Strings", "Milwaukee", "WI", "USA", "Central", "org-nashville-strings"),
        ("org-colorado-eagles-alpha", "Colorado Eagles Alpha", "Loveland", "CO", "USA", "Pacific", "org-denver-peaks"),
        ("org-henderson-silver", "Henderson Silver", "Henderson", "NV", "USA", "Pacific", "org-las-vegas-silver"),
        ("org-ontario-crowns", "Ontario Crowns", "Ontario", "CA", "USA", "Pacific", "org-los-angeles-crowns"),
        ("org-san-diego-orange", "San Diego Orange", "San Diego", "CA", "USA", "Pacific", "org-anaheim-orange"),
        ("org-barracuda-bays", "Bay Barracuda", "San Jose", "CA", "USA", "Pacific", "org-san-jose-bays"),
        ("org-abbotsford-evergreens", "Abbotsford Evergreens", "Abbotsford", "BC", "Canada", "Pacific", "org-vancouver-evergreens"),
        ("org-calgary-wranglers-alpha", "Calgary Wranglers Alpha", "Calgary", "AB", "Canada", "Pacific", "org-calgary-flamehawks"),
        ("org-bakersfield-oil", "Bakersfield Oil", "Bakersfield", "CA", "USA", "Pacific", "org-edmonton-oil-kings-pro"),
        ("org-salt-lake-canyons", "Salt Lake Canyons", "Salt Lake City", "UT", "USA", "Pacific", "org-utah-canyons"),
        ("org-trois-rivieres-nord", "Trois-Rivieres Nord", "Trois-Rivieres", "QC", "Canada", "North", "org-quebec-nord")
    ];

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
        string? affiliateOrganizationId = null,
        string leagueName = "",
        string divisionConference = "",
        string placeholderArena = "",
        string staffQuality = "",
        string currentStrategy = "") =>
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
            affiliateOrganizationId,
            leagueName,
            divisionConference,
            placeholderArena,
            staffQuality,
            currentStrategy);

    private static string RecordFor(int index, int games)
    {
        var wins = Math.Clamp(20 + (index * 7 % 28), 12, games - 18);
        var overtime = games >= 80 ? index % 12 : index % 7;
        var losses = Math.Max(0, games - wins - overtime);
        return $"{wins}-{losses}-{overtime}";
    }

    private static string StrengthFor(int index) =>
        (index % 5) switch
        {
            0 => "Excellent",
            1 => "Strong",
            2 => "Average",
            3 => "Thin",
            _ => "Emerging"
        };

    private static string DifficultyFor(int index) =>
        (index % 4) switch
        {
            0 => "Approachable",
            1 => "Medium",
            2 => "Hard",
            _ => "Demanding"
        };

    private static string RosterQualityFor(int index) =>
        (index % 5) switch
        {
            0 => "Contender",
            1 => "High-end",
            2 => "Balanced",
            3 => "Developing",
            _ => "Raw"
        };

    private static string StaffQualityFor(int index) =>
        (index % 4) switch
        {
            0 => "Experienced",
            1 => "Balanced",
            2 => "Development-focused",
            _ => "Needs support"
        };

    private static string ExpectationFor(int index, bool pro) =>
        pro
            ? (index % 3) switch
            {
                0 => "Win now while keeping future flexibility.",
                1 => "Push toward the playoffs with a younger core.",
                _ => "Rebuild patiently and protect the prospect pipeline."
            }
            : "Develop prospects, stay competitive, and protect player progression.";

    private static string StrategyFor(int index, bool pro) =>
        pro
            ? (index % 4) switch
            {
                0 => "Win now",
                1 => "Prospect builder",
                2 => "Budget-conscious retool",
                _ => "Balanced roster management"
            }
            : (index % 4) switch
            {
                0 => "Draft and develop",
                1 => "Recruiting push",
                2 => "Patient rebuild",
                _ => "Compete with youth"
            };

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
            PlayerAssignmentRules = source.PlayerAssignmentRules,
            SalaryCapRules = source.SalaryCapRules,
            FreeAgentRightsRules = source.FreeAgentRightsRules,
            ArbitrationRules = source.ArbitrationRules,
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
