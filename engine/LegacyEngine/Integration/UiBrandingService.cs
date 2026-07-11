using System.Globalization;

namespace LegacyEngine.Integration;

public sealed class UiBrandingService
{
    private static readonly BrandColorPalette[] TeamPalettes =
    [
        new("#12395B", "#E8EEF4", "#C4472D", "#EEF5FB", "#0D263C", "#FFFFFF"),
        new("#1D4E3E", "#F0F3EC", "#D19A2A", "#EDF7F2", "#14372C", "#FFFFFF"),
        new("#4A2147", "#EEF1F5", "#3BA4B8", "#F5EEF4", "#321831", "#FFFFFF"),
        new("#234A7A", "#F4F6F7", "#E0A62D", "#EEF4FB", "#162F50", "#FFFFFF"),
        new("#5A2A1D", "#F1F3F0", "#2F8D73", "#FAF1ED", "#3A1C14", "#FFFFFF"),
        new("#2A4558", "#F2F4F6", "#A85F2E", "#EEF4F7", "#1A2E3B", "#FFFFFF"),
        new("#374B24", "#F5F6F1", "#B95F46", "#F1F7EA", "#243319", "#FFFFFF"),
        new("#612D38", "#F4F0ED", "#4B96C4", "#FAEEF1", "#3E1E25", "#FFFFFF"),
        new("#254559", "#EDEFF4", "#D17C22", "#EEF5F8", "#172D3A", "#FFFFFF"),
        new("#3E3A5F", "#F0F4F1", "#4BA36C", "#F1F0FA", "#292640", "#FFFFFF"),
        new("#18556A", "#F4F2EA", "#B7822D", "#EAF6FA", "#103946", "#FFFFFF"),
        new("#6B3426", "#F1F4F5", "#2E84A6", "#FBF0EC", "#472319", "#FFFFFF")
    ];

    private static readonly TeamLogoPlaceholder[] LogoShapes =
    [
        TeamLogoPlaceholder.CircularCrest,
        TeamLogoPlaceholder.Shield,
        TeamLogoPlaceholder.RingMonogram,
        TeamLogoPlaceholder.DiagonalStripeBadge,
        TeamLogoPlaceholder.PuckEmblem,
        TeamLogoPlaceholder.MountainBadge,
        TeamLogoPlaceholder.WaveBadge,
        TeamLogoPlaceholder.StarBadge
    ];

    private static readonly string[] VisualStyles =
    [
        "Traditional",
        "Modern",
        "Northern",
        "Coastal",
        "Prairie",
        "Metropolitan",
        "Industrial",
        "Heritage",
        "Aggressive",
        "Clean",
        "Youthful"
    ];

    public UiBrandingRegistry BuildRegistry(LeagueProfile leagueProfile)
    {
        ArgumentNullException.ThrowIfNull(leagueProfile);

        var leagueBrand = BuildLeagueProfile(leagueProfile);
        var teams = leagueProfile.Teams
            .Select((team, index) => BuildTeamProfile(leagueProfile, team, index))
            .ToDictionary(team => team.OrganizationId, team => team, StringComparer.Ordinal);

        var registry = new UiBrandingRegistry(
            teams,
            new Dictionary<string, LeagueBrandingProfile>(StringComparer.Ordinal)
            {
                [leagueBrand.LeagueId] = leagueBrand
            },
            BuildIconRegistry(),
            BuildWorkspaceRegistry());
        registry.Validate();
        return registry;
    }

    public NewGmScenarioSnapshot EnsureBranding(NewGmScenarioSnapshot scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var requiredTeamIds = scenario.LeagueProfile.Teams.Select(team => team.OrganizationId).ToHashSet(StringComparer.Ordinal);
        var hasLeague = scenario.BrandingRegistry.LeagueProfiles.ContainsKey(scenario.LeagueProfile.Identity.LeagueId);
        var hasTeams = requiredTeamIds.All(id => scenario.BrandingRegistry.TeamProfiles.ContainsKey(id));
        var hasVisuals = scenario.BrandingRegistry.Icons.Count > 0 && scenario.BrandingRegistry.Workspaces.Count > 0;

        if (hasLeague && hasTeams && hasVisuals)
        {
            return scenario;
        }

        var generated = BuildRegistry(scenario.LeagueProfile);
        var teams = generated.TeamProfiles
            .Concat(scenario.BrandingRegistry.TeamProfiles.Where(existing => !generated.TeamProfiles.ContainsKey(existing.Key)))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        var leagues = generated.LeagueProfiles
            .Concat(scenario.BrandingRegistry.LeagueProfiles.Where(existing => !generated.LeagueProfiles.ContainsKey(existing.Key)))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);

        return scenario with
        {
            BrandingRegistry = new UiBrandingRegistry(teams, leagues, generated.Icons, generated.Workspaces)
        };
    }

    public TeamBrandingProfile TeamFor(NewGmScenarioSnapshot scenario, string organizationId)
    {
        var prepared = EnsureBranding(scenario);
        if (prepared.BrandingRegistry.TeamProfiles.TryGetValue(organizationId, out var profile))
        {
            return profile;
        }

        var team = prepared.LeagueProfile.Teams.FirstOrDefault(item => item.OrganizationId == organizationId)
            ?? prepared.TeamSelection;
        return BuildTeamProfile(prepared.LeagueProfile, team, 0);
    }

    public LeagueBrandingProfile LeagueFor(LeagueProfile leagueProfile) =>
        BuildLeagueProfile(leagueProfile);

    private static TeamBrandingProfile BuildTeamProfile(LeagueProfile leagueProfile, TeamSelectionOption team, int index)
    {
        var seed = StableHash($"{leagueProfile.Identity.LeagueId}:{team.OrganizationId}:{team.TeamName}");
        var palette = TeamPalettes[Math.Abs(seed + index) % TeamPalettes.Length];
        var shape = LogoShapes[Math.Abs(seed / 7) % LogoShapes.Length];
        var style = StyleFor(team, seed);
        var abbreviation = AbbreviationFor(team.TeamName, team.City);
        var monogram = new TeamMonogram(MonogramFor(team.TeamName, abbreviation));
        var phrase = $"{style} {leagueProfile.Identity.ShortName} identity built around {team.DisplayCurrentStrategy.ToLowerInvariant()}";
        return new TeamBrandingProfile(
            team.OrganizationId,
            team.TeamName,
            string.IsNullOrWhiteSpace(team.City) ? team.TeamName : team.City,
            leagueProfile.Identity.LeagueId,
            leagueProfile.Identity.Name,
            team.DisplayDivisionConference,
            team.DisplayArena,
            abbreviation,
            monogram,
            shape,
            palette,
            style,
            BannerFor(style),
            StripeFor(shape),
            phrase);
    }

    private static LeagueBrandingProfile BuildLeagueProfile(LeagueProfile leagueProfile)
    {
        var palette = leagueProfile.Experience switch
        {
            LeagueExperience.Nhl => new BrandColorPalette("#10283F", "#F2F5F8", "#B8862F", "#EEF3F8", "#0B1B2B", "#FFFFFF"),
            LeagueExperience.Ahl => new BrandColorPalette("#173B48", "#F0F4F5", "#4AA37B", "#EDF6F4", "#102A34", "#FFFFFF"),
            LeagueExperience.Junior => new BrandColorPalette("#1C4A69", "#F5F7EF", "#D36D2E", "#EDF6FB", "#123247", "#FFFFFF"),
            _ => new BrandColorPalette("#384558", "#F4F5F6", "#6A92B8", "#F0F3F7", "#26303D", "#FFFFFF")
        };
        var descriptor = leagueProfile.Experience switch
        {
            LeagueExperience.Nhl => "Professional broadcast front office",
            LeagueExperience.Ahl => "Development affiliate operations",
            LeagueExperience.Junior => "Regional youth development",
            _ => "Neutral custom league"
        };

        return new LeagueBrandingProfile(
            leagueProfile.Identity.LeagueId,
            leagueProfile.Identity.Name,
            leagueProfile.Identity.ShortName,
            leagueProfile.Experience,
            palette,
            UiIcon.League,
            descriptor,
            $"{leagueProfile.Identity.ShortName} compact header with restrained accents",
            leagueProfile.Identity.Description);
    }

    private static IReadOnlyDictionary<string, UiVisualIdentity> BuildIconRegistry()
    {
        UiVisualIdentity Item(string key, string label, UiIcon icon, UiIconCategory category, string tooltip, string semantic = "neutral") =>
            new(key, label, icon, category, tooltip, semantic);

        return new[]
        {
            Item("nav.dashboard", "Dashboard", UiIcon.Dashboard, UiIconCategory.Navigation, "Open the GM dashboard."),
            Item("nav.inbox", "Inbox", UiIcon.Inbox, UiIconCategory.Navigation, "Open GM messages."),
            Item("nav.hockey_operations", "Hockey Operations", UiIcon.HockeyOperations, UiIconCategory.Navigation, "Open roster, scouting, draft, trades, and player decisions."),
            Item("nav.organization", "Organization", UiIcon.Organization, UiIconCategory.Navigation, "Open owner, staff, budget, and department management."),
            Item("nav.league", "League", UiIcon.League, UiIconCategory.Navigation, "Open league-wide information."),
            Item("nav.season", "Season", UiIcon.Season, UiIconCategory.Navigation, "Open schedule, standings, stats, and playoffs."),
            Item("nav.reports", "Reports / History", UiIcon.Reports, UiIconCategory.Navigation, "Open reports, history, media, awards, and records."),
            Item("nav.settings", "Settings", UiIcon.Settings, UiIconCategory.Navigation, "Open settings placeholder."),
            Item("status.healthy", "Healthy", UiIcon.Healthy, UiIconCategory.Status, "Player is healthy.", "healthy"),
            Item("status.injured", "Injured", UiIcon.Injured, UiIconCategory.Status, "Player has an injury.", "injured"),
            Item("status.improving", "Improving", UiIcon.Improving, UiIconCategory.Status, "Trend is improving.", "positive"),
            Item("status.declining", "Declining", UiIcon.Declining, UiIconCategory.Status, "Trend is declining.", "attention"),
            Item("status.rfa", "RFA", UiIcon.RestrictedFreeAgent, UiIconCategory.Status, "Restricted free agent status.", "caution"),
            Item("status.ufa", "UFA", UiIcon.UnrestrictedFreeAgent, UiIconCategory.Status, "Unrestricted free agent status.", "info"),
            Item("status.waivers", "Waivers", UiIcon.Waivers, UiIconCategory.Status, "Waiver status or requirement.", "caution"),
            Item("action.view", "View", UiIcon.View, UiIconCategory.Actions, "View details."),
            Item("action.assign", "Assign", UiIcon.Assign, UiIconCategory.Actions, "Assign this person or asset."),
            Item("action.trade", "Trade", UiIcon.Trade, UiIconCategory.Actions, "Open trade context."),
            Item("person.player", "Player", UiIcon.Player, UiIconCategory.People, "Player profile."),
            Item("person.goalie", "Goalie", UiIcon.Goalie, UiIconCategory.People, "Goalie profile."),
            Item("person.coach", "Coach", UiIcon.Coach, UiIconCategory.People, "Coach profile."),
            Item("person.scout", "Scout", UiIcon.Scout, UiIconCategory.People, "Scout profile."),
            Item("person.medical", "Medical", UiIcon.Medical, UiIconCategory.People, "Medical staff profile."),
            Item("person.owner", "Owner", UiIcon.Owner, UiIconCategory.People, "Owner profile."),
            Item("person.agent", "Agent", UiIcon.Agent, UiIconCategory.People, "Agent profile."),
            Item("person.gm", "GM", UiIcon.GeneralManager, UiIconCategory.People, "General manager profile.")
        }.ToDictionary(item => item.Key, item => item, StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, UiWorkspaceIdentity> BuildWorkspaceRegistry()
    {
        UiWorkspaceIdentity Workspace(string name, string subtitle, UiIcon icon, string accent, string hint) =>
            new(name, subtitle, icon, accent, hint);

        return new[]
        {
            Workspace("Dashboard", "Office overview, next decisions, and club pulse.", UiIcon.Dashboard, "primary", "Use dashboard cards for quick decisions."),
            Workspace("Inbox", "GM messages, team items, and league wire.", UiIcon.Inbox, "secondary", "Use message categories to keep mail focused."),
            Workspace("Hockey Operations", "Roster, scouting, draft, trades, development, and player decisions.", UiIcon.HockeyOperations, "primary", "Use player cards and action panels."),
            Workspace("Organization", "Owner, staff, budget, departments, and culture.", UiIcon.Organization, "accent", "Use department navigation and staff profiles."),
            Workspace("League", "League table, transactions, teams, and market context.", UiIcon.League, "secondary", "Use team cards and league filters."),
            Workspace("Season", "Schedule, standings, playoffs, stats, and readiness.", UiIcon.Season, "primary", "Use matchup and standings cards."),
            Workspace("Reports / History", "Archives, media, awards, records, and career history.", UiIcon.Reports, "accent", "Use report cards and history timelines."),
            Workspace("Settings placeholder", "Save/load and future appearance settings.", UiIcon.Settings, "secondary", "Settings remain lightweight for alpha.")
        }.ToDictionary(item => item.Workspace, item => item, StringComparer.Ordinal);
    }

    private static string AbbreviationFor(string teamName, string city)
    {
        var words = SignificantWords(teamName).ToArray();
        var letters = words.Length switch
        {
            >= 3 => string.Concat(words.Take(3).Select(word => word[0])),
            2 => $"{words[0][0]}{words[1][0]}",
            1 when words[0].Length >= 3 => words[0][..3],
            1 => words[0],
            _ => city.Length >= 3 ? city[..3] : "HGM"
        };

        return new string(letters.Where(char.IsLetter).ToArray()).ToUpperInvariant();
    }

    private static string MonogramFor(string teamName, string abbreviation)
    {
        var words = SignificantWords(teamName).ToArray();
        if (words.Length >= 2)
        {
            return $"{words[0][0]}{words[^1][0]}".ToUpperInvariant();
        }

        return abbreviation.Length >= 2 ? abbreviation[..2] : abbreviation;
    }

    private static IEnumerable<string> SignificantWords(string value) =>
        value.Split(' ', '-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(word => word.Any(char.IsLetter))
            .Select(word => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(new string(word.Where(char.IsLetter).ToArray()).ToLowerInvariant()))
            .Where(word => word.Length > 0);

    private static string StyleFor(TeamSelectionOption team, int seed)
    {
        var context = $"{team.City} {team.Region} {team.TeamName}".ToLowerInvariant();
        if (context.Contains("prairie", StringComparison.Ordinal) || context.Contains("sk", StringComparison.Ordinal) || context.Contains("mb", StringComparison.Ordinal))
        {
            return "Prairie";
        }

        if (context.Contains("sea", StringComparison.Ordinal) || context.Contains("coast", StringComparison.Ordinal) || context.Contains("wave", StringComparison.Ordinal))
        {
            return "Coastal";
        }

        if (context.Contains("north", StringComparison.Ordinal) || context.Contains("winter", StringComparison.Ordinal))
        {
            return "Northern";
        }

        return VisualStyles[Math.Abs(seed) % VisualStyles.Length];
    }

    private static string BannerFor(string style) =>
        style switch
        {
            "Traditional" or "Heritage" => "classic stripe",
            "Modern" or "Clean" => "minimal bar",
            "Coastal" => "wave accent",
            "Prairie" => "wide horizon stripe",
            "Northern" => "angled frost stripe",
            "Aggressive" => "bold diagonal",
            _ => "compact color edge"
        };

    private static string StripeFor(TeamLogoPlaceholder shape) =>
        shape switch
        {
            TeamLogoPlaceholder.DiagonalStripeBadge => "diagonal stripe",
            TeamLogoPlaceholder.RingMonogram => "ring trim",
            TeamLogoPlaceholder.MountainBadge => "mountain chevron",
            TeamLogoPlaceholder.WaveBadge => "wave band",
            TeamLogoPlaceholder.StarBadge => "star shoulder mark",
            TeamLogoPlaceholder.PuckEmblem => "puck ring",
            _ => "two-stripe placeholder"
        };

    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 23;
            foreach (var character in value)
            {
                hash = hash * 31 + character;
            }

            return hash == int.MinValue ? 0 : hash;
        }
    }
}
