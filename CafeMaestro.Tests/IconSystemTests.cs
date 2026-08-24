using CafeMaestro.Controls;
using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System.Xml.Linq;

namespace CafeMaestro.Tests;

public class IconSystemTests
{
    /// <summary>Icons later tickets are allowed to assume exist.</summary>
    private static readonly string[] RequiredIconKeys =
    [
        "IconBeanData",
        "IconDrumData",
        "IconTimerData",
        "IconFirstCrackData",
        "IconDropData",
        "IconCoolingData",
        "IconWeighData",
        "IconTemperatureData",
        "IconNewBatchData",
        "IconResetData",
        "IconPauseData",
        "IconPlayData",
        "IconCheckData",
        "IconAlertData",
        "IconLogData",
        "IconSettingsData",
        "IconAddData",
        "IconEditData",
        "IconDeleteData",
        "IconSearchData",
        "IconImportData",
        "IconExportData",
        "IconMoreData",
        "IconChevronData",
        "IconCloseData"
    ];

    private static Dictionary<string, string> Icons => XamlResourceReader.ReadDictionary("IconGeometries.xaml");

    [Fact]
    public void IconDictionary_ProvidesEveryGlyphLaterTicketsNeed()
    {
        Icons.Keys.Should().Contain(RequiredIconKeys);
    }

    [Fact]
    public void EveryIcon_IsDrawnOnTheTwentyFourUnitGrid()
    {
        foreach ((string key, string data) in Icons)
        {
            PathF path = new PathBuilder().BuildPath(data);
            RectF bounds = path.Bounds;

            // A small tolerance absorbs the flattening error of arc segments.
            bounds.Left.Should().BeGreaterThanOrEqualTo(-0.25f, $"{key} must stay inside the 24 grid");
            bounds.Top.Should().BeGreaterThanOrEqualTo(-0.25f, $"{key} must stay inside the 24 grid");
            bounds.Right.Should().BeLessThanOrEqualTo(24.25f, $"{key} must stay inside the 24 grid");
            bounds.Bottom.Should().BeLessThanOrEqualTo(24.25f, $"{key} must stay inside the 24 grid");

            // A glyph that occupies almost none of the grid is a transcription mistake.
            Math.Max(bounds.Width, bounds.Height).Should().BeGreaterThan(8f,
                $"{key} must fill a usable share of the 24 grid");
        }
    }

    /// <summary>
    /// The circular arrow is reserved exclusively for Reset. Sharing geometry between
    /// glyphs is how "next batch" started reading as Reset in the first place.
    /// </summary>
    [Fact]
    public void NoTwoIcons_ShareTheSameGeometry()
    {
        List<string> duplicated = Icons
            .GroupBy(icon => Normalize(icon.Value), StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => string.Join(" == ", group.Select(icon => icon.Key)))
            .ToList();

        duplicated.Should().BeEmpty();
    }

    [Fact]
    public void NewBatchAndReset_AreVisuallyIndependentGlyphs()
    {
        Dictionary<string, string> icons = Icons;

        PathF newBatch = new PathBuilder().BuildPath(icons["IconNewBatchData"]);
        PathF reset = new PathBuilder().BuildPath(icons["IconResetData"]);

        // The bean-plus badge is a multi-figure glyph; the reset arrow is one arc plus its head.
        newBatch.OperationCount.Should().BeGreaterThan(reset.OperationCount,
            "New batch is a bean with a plus badge, not a restyled circular arrow");
    }

    [Theory]
    [InlineData(24d, 1d)]
    [InlineData(18d, 0.75d)]
    [InlineData(32d, 4d / 3d)]
    public void IconView_ScalesTheDesignGridToTheRequestedSize(double iconSize, double expectedScale)
    {
        IconView.ScaleFor(iconSize).Should().BeApproximately(expectedScale, 1e-9);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-8d)]
    [InlineData(double.NaN)]
    public void IconView_FallsBackToTheDesignGridForUnusableSizes(double iconSize)
    {
        IconView.ScaleFor(iconSize).Should().Be(1d);
    }

    [Theory]
    [InlineData(18d)]
    [InlineData(24d)]
    [InlineData(32d)]
    public void IconView_HoldsTheRenderedOutlineWeightAcrossShippedSizes(double iconSize)
    {
        double renderedStroke = IconView.StrokeThicknessFor(iconSize, isFilled: false)
            * IconView.ScaleFor(iconSize);

        renderedStroke.Should().BeApproximately(IconView.DesignStrokeThickness, 1e-9,
            "18 dp icons must not become optically thinner than 24 or 32 dp icons");
        IconView.StrokeThicknessFor(iconSize, isFilled: true).Should().Be(0d);
    }

    [Fact]
    public void IconView_UsesOnlyExplicitOrSemanticColour_AndHasNoUnthemedFallback()
    {
        ResourceDictionary resources = new();
        Color semantic = Color.FromArgb("#123456");
        Color explicitColor = Color.FromArgb("#654321");
        resources["PrimaryTextColor"] = semantic;

        IconView.ResolveColor(explicitColor, resources).Should().Be(explicitColor);
        IconView.ResolveColor(null, resources).Should().Be(semantic);
        IconView.ResolveColor(null, new ResourceDictionary()).Should().BeNull(
            "a missing semantic resource must render no colour, never a hardcoded fallback");
    }

    [Fact]
    public void BeanGlyphs_UseMultipleBeanBodies_NotAClosedOvalWithALongSlash()
    {
        Icons["IconBeanData"].Count(character => character is 'Z' or 'z').Should().BeGreaterThanOrEqualTo(2);
        Icons["IconNewBatchData"].Count(character => character is 'Z' or 'z').Should().BeGreaterThanOrEqualTo(3,
            "New batch is two beans plus a closed plus badge");
    }

    [Fact]
    public void ShellBeansAsset_MatchesTheSharedTwoBeanGeometry()
    {
        string assetPath = Path.Combine(
            XamlResourceReader.RepositoryRoot, "CafeMaestro", "Resources", "Images", "tab_beans_icon.svg");
        XDocument asset = XDocument.Load(assetPath);
        string assetGeometry = asset.Descendants()
            .Single(element => element.Name.LocalName == "path")
            .Attribute("d")!.Value;

        Normalize(assetGeometry).Should().Be(Normalize(Icons["IconBeanData"]),
            "the Beans Shell destination must use the same unambiguous two-bean silhouette as IconView");
    }

    [Fact]
    public void DropHasMoreDomainDetailThanGenericFileImport()
    {
        PathF drop = new PathBuilder().BuildPath(Icons["IconDropData"]);
        PathF import = new PathBuilder().BuildPath(Icons["IconImportData"]);

        drop.OperationCount.Should().BeGreaterThan(import.OperationCount + 4,
            "Drop must depict beans leaving a drum for a cooler, not a lightly rounded download tray");
    }

    [Fact]
    public void CoolingUsesCurvedAirStreams_NotTheHorizontalLogSilhouette()
    {
        Icons["IconCoolingData"].Count(character => character is 'C' or 'c')
            .Should().BeGreaterThanOrEqualTo(3);
        Icons["IconLogData"].Should().NotContain("C");
    }

    [Fact]
    public void WeighGlyph_KeepsOpticalMarginsInsideTheGrid()
    {
        RectF bounds = new PathBuilder().BuildPath(Icons["IconWeighData"]).Bounds;

        bounds.Left.Should().BeGreaterThanOrEqualTo(2f);
        bounds.Right.Should().BeLessThanOrEqualTo(22f);
    }

    /// <summary>
    /// The glyphs are parsed twice: by XAML's path mini-language at runtime and by
    /// <see cref="PathBuilder"/> here. Keeping to the shared command set is what makes
    /// both parsers agree.
    /// </summary>
    [Fact]
    public void EveryIcon_UsesOnlyCommandsBothPathParsersUnderstand()
    {
        const string supportedCommands = "MmLlHhVvCcSsQqTtAaZz";

        foreach ((string key, string data) in Icons)
        {
            char[] unsupported = data
                .Where(char.IsLetter)
                .Where(character => !supportedCommands.Contains(character, StringComparison.Ordinal))
                .Distinct()
                .ToArray();

            unsupported.Should().BeEmpty($"{key} must use only the shared SVG/XAML path commands");
        }
    }

    private static string Normalize(string data) =>
        new(data.Where(character => !char.IsWhiteSpace(character)).ToArray());
}
