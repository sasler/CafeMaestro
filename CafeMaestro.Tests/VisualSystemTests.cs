using System.Globalization;
using CafeMaestro.Services;
using FluentAssertions;
using Microsoft.Maui.ApplicationModel;

namespace CafeMaestro.Tests;

public class VisualSystemTests
{
    private static readonly string[] RequiredSemanticColorKeys =
    [
        "SurfaceColor",
        "ElevatedSurfaceColor",
        "RaisedSurfaceColor",
        "PrimaryTextColor",
        "SecondaryTextColor",
        "BorderColor",
        "RoastColor",
        "CoolingColor",
        "ReadyColor",
        "AttentionColor",
        "DangerColor",
        "FocusColor",
        "DisabledColor",
        "ScrimColor",
        "PlatformStatusBarColor",
        "PlatformNavigationBarColor"
    ];

    private static readonly string[] ForegroundKeysOnSurface =
    [
        "PrimaryTextColor",
        "SecondaryTextColor",
        "RoastColor",
        "CoolingColor",
        "ReadyColor",
        "AttentionColor",
        "DangerColor",
        "FocusColor"
    ];

    [Fact]
    public void ThemeDictionaries_ExposeIdenticalResourceKeys()
    {
        Dictionary<string, string> dark = XamlResourceReader.ReadDictionary("DarkTheme.xaml");
        Dictionary<string, string> light = XamlResourceReader.ReadDictionary("LightTheme.xaml");

        dark.Keys.Should().BeEquivalentTo(light.Keys);
        dark.Keys.Should().Contain(RequiredSemanticColorKeys);
    }

    [Theory]
    [InlineData("DarkTheme.xaml")]
    [InlineData("LightTheme.xaml")]
    public void ThemeSemanticTextAndStatusColors_MeetWcagTextContrast(string themeFile)
    {
        Dictionary<string, string> theme = XamlResourceReader.ReadDictionary(themeFile);
        string surface = theme["SurfaceColor"];

        foreach (string foregroundKey in ForegroundKeysOnSurface)
        {
            double ratio = ContrastRatio(theme[foregroundKey], surface);
            ratio.Should().BeGreaterThanOrEqualTo(4.5,
                $"{foregroundKey} in {themeFile} must remain readable on SurfaceColor");
        }
    }

    [Theory]
    [InlineData("DarkTheme.xaml")]
    [InlineData("LightTheme.xaml")]
    public void FilledActionColors_KeepTheirLabelsReadable(string themeFile)
    {
        Dictionary<string, string> theme = XamlResourceReader.ReadDictionary(themeFile);

        ContrastRatio(theme["OnRoastColor"], theme["RoastColor"]).Should().BeGreaterThanOrEqualTo(4.5,
            $"the roast/import action label in {themeFile} sits on RoastColor");
        ContrastRatio(theme["OnReadyColor"], theme["ReadyColor"]).Should().BeGreaterThanOrEqualTo(4.5,
            $"the confirm/save action label in {themeFile} sits on ReadyColor");
    }

    [Fact]
    public void DesignTokens_ExposeAccessibleSizingContract()
    {
        Dictionary<string, string> tokens = XamlResourceReader.ReadDictionary("DesignTokens.xaml");

        tokens.Keys.Should().Contain([
            "SpaceXs", "SpaceSm", "SpaceMd", "SpaceLg", "SpaceXl",
            "RadiusSm", "RadiusMd", "RadiusLg",
            "FontSizeBody", "FontSizeTitle", "FontSizeHeadline",
            "FontSizeStatus", "FontFamilyTabular",
            "TouchTargetMin", "PrimaryActionHeight",
            "IconSizeSm", "IconSizeMd", "IconSizeLg",
            "BreakpointMedium"
        ]);

        // The interaction contract from the UI/platform architecture: 48 dp minimum
        // controls, with the primary action larger still.
        ParseToken(tokens, "TouchTargetMin").Should().BeGreaterThanOrEqualTo(48);
        ParseToken(tokens, "PrimaryActionHeight").Should().BeGreaterThanOrEqualTo(56);
        tokens.Should().NotContainKey("IconButtonSize",
            "icon buttons consume the single 48 dp touch-target contract rather than a conflicting token");
        ParseToken(tokens, "IconSizeSm").Should().Be(18);
        ParseToken(tokens, "IconSizeMd").Should().Be(24);
        ParseToken(tokens, "IconSizeLg").Should().Be(32);
    }

    [Fact]
    public void IconButtonStyle_ConsumesTheSharedTouchTargetToken()
    {
        string components = File.ReadAllText(Path.Combine(XamlResourceReader.StylesDirectory, "ComponentStyles.xaml"));

        components.Should().Contain("<Style x:Key=\"IconButtonStyle\"");
        components.Should().Contain("MinimumHeightRequest\" Value=\"{StaticResource TouchTargetMin}\"");
        components.Should().Contain("MinimumWidthRequest\" Value=\"{StaticResource TouchTargetMin}\"");
    }

    [Fact]
    public void NumericValueStyle_UsesTheTabularMonospaceToken()
    {
        Dictionary<string, string> tokens = XamlResourceReader.ReadDictionary("DesignTokens.xaml");
        string components = File.ReadAllText(Path.Combine(XamlResourceReader.StylesDirectory, "ComponentStyles.xaml"));

        tokens.Should().ContainKey("FontFamilyTabular");
        string tokenSource = File.ReadAllText(Path.Combine(XamlResourceReader.StylesDirectory, "DesignTokens.xaml"));
        tokenSource.Should().Contain("Platform=\"Android\" Value=\"monospace\"");
        tokenSource.Should().Contain("Platform=\"WinUI\" Value=\"Cascadia Mono\"");
        components.Should().Contain("<Style x:Key=\"NumericValueStyle\"");
        components.Should().Contain("FontFamily\" Value=\"{StaticResource FontFamilyTabular}\"");
    }

    [Fact]
    public void LightTheme_StatusHuesAndSurfaceLevelsStayPerceptuallySeparated()
    {
        Dictionary<string, string> light = XamlResourceReader.ReadDictionary("LightTheme.xaml");

        HueDistance(Hue(light["RoastColor"]), Hue(light["DangerColor"])).Should().BeGreaterThan(25);
        HueDistance(Hue(light["RoastColor"]), Hue(light["AttentionColor"])).Should().BeGreaterThan(10);
        RgbDistance(light["ElevatedSurfaceColor"], light["RaisedSurfaceColor"]).Should().BeGreaterThan(12,
            "raised sheets must remain visible against ordinary elevated cards");

        (int secondaryRed, _, int secondaryBlue) = HexChannels(light["SecondaryTextColor"]);
        (int mutedRed, _, int mutedBlue) = HexChannels(light["MutedTextColor"]);
        secondaryRed.Should().BeGreaterThanOrEqualTo(secondaryBlue, "light neutrals should stay warm, not slate blue");
        mutedRed.Should().BeGreaterThanOrEqualTo(mutedBlue, "light neutrals should stay warm, not slate blue");
    }

    [Fact]
    public void StatusComponents_ExposeGlyphAndChannelEdgeVariants()
    {
        string components = File.ReadAllText(Path.Combine(XamlResourceReader.StylesDirectory, "ComponentStyles.xaml"));
        string gallery = File.ReadAllText(Path.Combine(
            XamlResourceReader.RepositoryRoot, "CafeMaestro", "Views", "ComponentGalleryPage.xaml"));

        foreach (string family in new[] { "Roast", "Cooling", "Ready", "Attention", "Danger" })
        {
            components.Should().Contain($"x:Key=\"{family}StatusGlyphStyle\"");
            components.Should().Contain($"x:Key=\"{family}ChannelEdgeStyle\"");
            gallery.Should().Contain($"{family}StatusGlyphStyle");
        }

        components.Should().Contain("x:Key=\"StatusEdgeCardStyle\"",
            "lists need a neutral-card plus semantic-edge option instead of full-card tint everywhere");
    }

    [Fact]
    public void SharedVisualResources_AreReachableFromTheAppMergeGraph()
    {
        HashSet<string> reachable = XamlResourceReader.AppMergedResourceKeys();

        reachable.Should().Contain([
            "TouchTargetMin", "FontFamilyTabular", "IconBeanData", "CardStyle",
            "StatusEdgeCardStyle", "RoastStatusGlyphStyle", "PlatformStatusBarColor"
        ]);
    }

    [Theory]
    [InlineData(ThemePreference.System, AppTheme.Dark, AppTheme.Dark)]
    [InlineData(ThemePreference.System, AppTheme.Light, AppTheme.Light)]
    [InlineData(ThemePreference.Light, AppTheme.Dark, AppTheme.Light)]
    [InlineData(ThemePreference.Dark, AppTheme.Light, AppTheme.Dark)]
    public void ThemePreferencePolicy_ResolvesLiveSystemThemeWithoutOverridingExplicitChoices(
        ThemePreference preference,
        AppTheme requestedTheme,
        AppTheme expected)
    {
        ThemePreferencePolicy.ResolveEffectiveTheme(preference, requestedTheme).Should().Be(expected);
    }

    [Fact]
    public void App_SubscribesToLiveRequestedThemeChanges()
    {
        string appSource = File.ReadAllText(Path.Combine(XamlResourceReader.RepositoryRoot, "CafeMaestro", "App.xaml.cs"));

        appSource.Should().Contain("RequestedThemeChanged += OnRequestedThemeChanged");
        appSource.Should().Contain("if (_activeThemePreference == ThemePreference.System)");
    }

    [Fact]
    public void AndroidChrome_NoLongerUsesTemplatePurple()
    {
        string colors = File.ReadAllText(Path.Combine(
            XamlResourceReader.RepositoryRoot, "CafeMaestro", "Platforms", "Android", "Resources", "values", "colors.xml"));

        colors.Should().NotContain("#512BD4");
        colors.Should().NotContain("#2B0B98");
    }

    [Fact]
    public void AndroidChrome_ProvidesNavigationIconContrastAcrossSupportedApiLevels()
    {
        string chrome = File.ReadAllText(Path.Combine(
            XamlResourceReader.RepositoryRoot, "CafeMaestro", "Platforms", "Android", "App.PlatformChrome.cs"));

        chrome.Should().Contain("OperatingSystem.IsAndroidVersionAtLeast(26)");
        chrome.Should().Contain("SystemUiFlags.LightNavigationBar",
            "API 26-29 support dark navigation icons through the legacy system UI flag");
        chrome.Should().Contain("? statusBarColor\n                        : navigationBarColor",
            "API 21-25 need a dark navigation background because dark navigation icons are unavailable");
    }

    [Fact]
    public void EveryResourceReferenceInAppXaml_ResolvesToADeclaredKey()
    {
        HashSet<string> declaredKeys = XamlResourceReader.DeclaredResourceKeys();

        List<string> dangling = XamlResourceReader.ResourceReferences()
            .Where(reference => !declaredKeys.Contains(reference.Key))
            .Select(reference => $"{reference.File} -> {reference.Key}")
            .Distinct(StringComparer.Ordinal)
            .ToList();

        dangling.Should().BeEmpty("existing pages must keep rendering while later tickets adopt the new styles");
    }

    /// <summary>
    /// MAUI rasterises <c>MauiImage</c> SVGs to PNG at build time, so a raw <c>.svg</c>
    /// name silently resolves to nothing at runtime.
    /// </summary>
    [Fact]
    public void NoImageSourceUsesARawSvgName()
    {
        List<string> offenders = XamlResourceReader.AppSourceFiles()
            .Where(file => File.ReadAllText(file.Path).Contains(".svg\"", StringComparison.OrdinalIgnoreCase))
            .Select(file => file.RelativePath)
            .ToList();

        offenders.Should().BeEmpty("XAML and C# reference MauiImage output as .png, never the .svg input");
    }

    [Theory]
    [InlineData(null, ThemePreference.Dark)]
    [InlineData("", ThemePreference.Dark)]
    [InlineData("unexpected", ThemePreference.Dark)]
    [InlineData("System", ThemePreference.System)]
    [InlineData("Light", ThemePreference.Light)]
    [InlineData("Dark", ThemePreference.Dark)]
    public void ThemePreferencePolicy_PreservesExplicitChoice_AndDefaultsToDark(
        string? storedValue,
        ThemePreference expected)
    {
        ThemePreferencePolicy.FromStoredValue(storedValue).Should().Be(expected);
    }

    private static double ParseToken(Dictionary<string, string> tokens, string key) =>
        double.Parse(tokens[key], CultureInfo.InvariantCulture);

    private static double RgbDistance(string firstHex, string secondHex)
    {
        (int firstRed, int firstGreen, int firstBlue) = HexChannels(firstHex);
        (int secondRed, int secondGreen, int secondBlue) = HexChannels(secondHex);

        return Math.Sqrt(
            Math.Pow(firstRed - secondRed, 2)
            + Math.Pow(firstGreen - secondGreen, 2)
            + Math.Pow(firstBlue - secondBlue, 2));
    }

    private static double Hue(string hex)
    {
        (int redByte, int greenByte, int blueByte) = HexChannels(hex);
        double red = redByte / 255d;
        double green = greenByte / 255d;
        double blue = blueByte / 255d;
        double max = Math.Max(red, Math.Max(green, blue));
        double min = Math.Min(red, Math.Min(green, blue));
        double delta = max - min;

        if (delta == 0)
        {
            return 0;
        }

        double hue = max == red
            ? 60 * (((green - blue) / delta) % 6)
            : max == green
                ? 60 * (((blue - red) / delta) + 2)
                : 60 * (((red - green) / delta) + 4);

        return hue < 0 ? hue + 360 : hue;
    }

    private static double HueDistance(double first, double second)
    {
        double distance = Math.Abs(first - second);
        return Math.Min(distance, 360 - distance);
    }

    private static (int Red, int Green, int Blue) HexChannels(string hex)
    {
        string value = hex.TrimStart('#');
        return (
            int.Parse(value[0..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            int.Parse(value[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            int.Parse(value[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    private static double ContrastRatio(string foregroundHex, string backgroundHex)
    {
        double foreground = RelativeLuminance(foregroundHex);
        double background = RelativeLuminance(backgroundHex);
        double lighter = Math.Max(foreground, background);
        double darker = Math.Min(foreground, background);

        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(string hex)
    {
        string value = hex.TrimStart('#');
        value.Should().HaveLength(6, "contrast test colors must use #RRGGBB values");

        double red = ParseChannel(value[0..2]);
        double green = ParseChannel(value[2..4]);
        double blue = ParseChannel(value[4..6]);

        return (0.2126 * Linearize(red)) + (0.7152 * Linearize(green)) + (0.0722 * Linearize(blue));
    }

    private static double ParseChannel(string value) =>
        int.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d;

    private static double Linearize(double channel) =>
        channel <= 0.04045
            ? channel / 12.92
            : Math.Pow((channel + 0.055) / 1.055, 2.4);
}
