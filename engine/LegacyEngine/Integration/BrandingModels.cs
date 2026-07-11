namespace LegacyEngine.Integration;

public enum BrandColorRole
{
    Primary,
    Secondary,
    Accent,
    LightBackgroundTint,
    DarkBackgroundTint,
    ReadableForeground
}

public enum TeamLogoPlaceholder
{
    CircularCrest,
    Shield,
    RingMonogram,
    DiagonalStripeBadge,
    PuckEmblem,
    MountainBadge,
    WaveBadge,
    StarBadge
}

public enum UiIconCategory
{
    Navigation,
    HockeyOperations,
    People,
    Status,
    Actions,
    League,
    Workspace
}

public enum UiIcon
{
    Dashboard,
    Inbox,
    HockeyOperations,
    Organization,
    League,
    Season,
    Reports,
    Settings,
    Roster,
    Lines,
    Prospects,
    Development,
    Contracts,
    Scouting,
    Transactions,
    Tactics,
    SpecialTeams,
    Player,
    Goalie,
    Coach,
    Scout,
    Medical,
    Owner,
    Agent,
    GeneralManager,
    StaffCandidate,
    Healthy,
    Injured,
    Improving,
    Declining,
    Expiring,
    Waivers,
    RestrictedFreeAgent,
    UnrestrictedFreeAgent,
    Warning,
    Critical,
    Completed,
    Locked,
    Unknown,
    View,
    Edit,
    Assign,
    Swap,
    Trade,
    Sign,
    Release,
    Compare,
    Save,
    Load,
    Search,
    Filter,
    Back,
    Close,
    Confirm,
    Cancel
}

public sealed record BrandColorPalette(
    string Primary,
    string Secondary,
    string Accent,
    string LightBackgroundTint,
    string DarkBackgroundTint,
    string ReadableForeground)
{
    public string ColorFor(BrandColorRole role) =>
        role switch
        {
            BrandColorRole.Primary => Primary,
            BrandColorRole.Secondary => Secondary,
            BrandColorRole.Accent => Accent,
            BrandColorRole.LightBackgroundTint => LightBackgroundTint,
            BrandColorRole.DarkBackgroundTint => DarkBackgroundTint,
            BrandColorRole.ReadableForeground => ReadableForeground,
            _ => Primary
        };

    public void Validate()
    {
        foreach (var value in new[] { Primary, Secondary, Accent, LightBackgroundTint, DarkBackgroundTint, ReadableForeground })
        {
            if (!IsHexColor(value))
            {
                throw new ArgumentException($"Brand color '{value}' must be a #RRGGBB hex value.");
            }
        }
    }

    private static bool IsHexColor(string value) =>
        value.Length == 7
        && value[0] == '#'
        && value.Skip(1).All(Uri.IsHexDigit);
}

public sealed record TeamMonogram(string Letters)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Letters) || Letters.Length is < 1 or > 4 || Letters.Any(char.IsDigit))
        {
            throw new ArgumentException("Team monogram must use one to four non-numeric letters.", nameof(Letters));
        }
    }
}

public sealed record TeamBrandingProfile(
    string OrganizationId,
    string OrganizationDisplayName,
    string Market,
    string LeagueId,
    string LeagueName,
    string ConferenceDivision,
    string ArenaName,
    string TeamAbbreviation,
    TeamMonogram Monogram,
    TeamLogoPlaceholder LogoPlaceholder,
    BrandColorPalette Palette,
    string VisualStyleDescriptor,
    string BannerStyle,
    string JerseyStripePattern,
    string IdentityPhrase)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OrganizationId)
            || string.IsNullOrWhiteSpace(OrganizationDisplayName)
            || string.IsNullOrWhiteSpace(Market)
            || string.IsNullOrWhiteSpace(LeagueId)
            || string.IsNullOrWhiteSpace(LeagueName)
            || string.IsNullOrWhiteSpace(ConferenceDivision)
            || string.IsNullOrWhiteSpace(ArenaName)
            || string.IsNullOrWhiteSpace(TeamAbbreviation)
            || string.IsNullOrWhiteSpace(VisualStyleDescriptor)
            || string.IsNullOrWhiteSpace(BannerStyle)
            || string.IsNullOrWhiteSpace(JerseyStripePattern)
            || string.IsNullOrWhiteSpace(IdentityPhrase))
        {
            throw new ArgumentException("Team branding profile requires identity, colors, and presentation descriptors.");
        }

        if (TeamAbbreviation.Any(char.IsDigit))
        {
            throw new ArgumentException("Team abbreviation must not contain numeric uniqueness markers.", nameof(TeamAbbreviation));
        }

        Monogram.Validate();
        Palette.Validate();
    }
}

public sealed record LeagueBrandingProfile(
    string LeagueId,
    string LeagueName,
    string ShortName,
    LeagueExperience Experience,
    BrandColorPalette Palette,
    UiIcon Icon,
    string VisualStyleDescriptor,
    string HeaderTreatment,
    string IdentityPhrase)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(LeagueId)
            || string.IsNullOrWhiteSpace(LeagueName)
            || string.IsNullOrWhiteSpace(ShortName)
            || string.IsNullOrWhiteSpace(VisualStyleDescriptor)
            || string.IsNullOrWhiteSpace(HeaderTreatment)
            || string.IsNullOrWhiteSpace(IdentityPhrase))
        {
            throw new ArgumentException("League branding profile requires identity, colors, and presentation descriptors.");
        }

        Palette.Validate();
    }
}

public sealed record UiVisualIdentity(
    string Key,
    string Label,
    UiIcon Icon,
    UiIconCategory Category,
    string Tooltip,
    string Semantic)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Key)
            || string.IsNullOrWhiteSpace(Label)
            || string.IsNullOrWhiteSpace(Tooltip)
            || string.IsNullOrWhiteSpace(Semantic))
        {
            throw new ArgumentException("Visual identity requires key, label, tooltip, and semantic text.");
        }
    }
}

public sealed record UiWorkspaceIdentity(
    string Workspace,
    string Subtitle,
    UiIcon Icon,
    string AccentRole,
    string LocalNavigationHint)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Workspace)
            || string.IsNullOrWhiteSpace(Subtitle)
            || string.IsNullOrWhiteSpace(AccentRole)
            || string.IsNullOrWhiteSpace(LocalNavigationHint))
        {
            throw new ArgumentException("Workspace identity requires workspace, subtitle, accent role, and navigation hint.");
        }
    }
}

public sealed record UiBrandingRegistry(
    IReadOnlyDictionary<string, TeamBrandingProfile> TeamProfiles,
    IReadOnlyDictionary<string, LeagueBrandingProfile> LeagueProfiles,
    IReadOnlyDictionary<string, UiVisualIdentity> Icons,
    IReadOnlyDictionary<string, UiWorkspaceIdentity> Workspaces)
{
    public static UiBrandingRegistry Empty { get; } = new(
        new Dictionary<string, TeamBrandingProfile>(StringComparer.Ordinal),
        new Dictionary<string, LeagueBrandingProfile>(StringComparer.Ordinal),
        new Dictionary<string, UiVisualIdentity>(StringComparer.Ordinal),
        new Dictionary<string, UiWorkspaceIdentity>(StringComparer.Ordinal));

    public void Validate()
    {
        foreach (var team in TeamProfiles)
        {
            if (string.IsNullOrWhiteSpace(team.Key))
            {
                throw new ArgumentException("Team branding key is required.", nameof(TeamProfiles));
            }

            team.Value.Validate();
        }

        foreach (var league in LeagueProfiles)
        {
            if (string.IsNullOrWhiteSpace(league.Key))
            {
                throw new ArgumentException("League branding key is required.", nameof(LeagueProfiles));
            }

            league.Value.Validate();
        }

        foreach (var icon in Icons.Values)
        {
            icon.Validate();
        }

        foreach (var workspace in Workspaces.Values)
        {
            workspace.Validate();
        }
    }
}
