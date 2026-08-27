using System.Xml.Linq;
using FluentAssertions;

namespace CafeMaestro.Tests;

/// <summary>
/// The layout rules a tablet exposes that a phone hides: full-width content that needs a cap,
/// tab roots that would otherwise draw two titles, and settings sections that must exist as
/// embeddable views rather than pages only Shell can reach.
/// </summary>
public class TabletLayoutContractTests
{
    private static readonly XNamespace Maui = "http://schemas.microsoft.com/dotnet/2021/maui";

    /// <summary>Tab roots that draw their own heading inside the page.</summary>
    private static readonly string[] TabRootPages =
    [
        "RoastPage.xaml",
        "RoastLogPage.xaml",
        "BeanInventoryPage.xaml",
        "SettingsPage.xaml"
    ];

    /// <summary>
    /// Every settings section is an embeddable ContentView, so the tablet pane and the phone
    /// page render the identical body instead of drifting into two implementations.
    /// </summary>
    private static readonly string[] SettingsSectionViews =
    [
        "RoastingSettingsView.xaml",
        "AppearanceSettingsView.xaml",
        "DataSettingsView.xaml",
        "RoastLevelSettingsView.xaml",
        "AboutView.xaml"
    ];

    [Theory]
    [InlineData("RoastPage.xaml")]
    [InlineData("RoastLogPage.xaml")]
    [InlineData("BeanInventoryPage.xaml")]
    [InlineData("SettingsPage.xaml")]
    public void TabRootPages_HideTheShellNavigationBar(string pageFile)
    {
        XDocument page = XDocument.Load(Path.Combine(
            XamlResourceReader.RepositoryRoot, "CafeMaestro", pageFile));

        page.Root!.Attribute(Maui + "NavBarIsVisible")?.Value
            .Should().Be("False", "the page already draws its own title, so Shell must not draw a second one");
    }

    [Theory]
    [MemberData(nameof(SettingsSectionViewFiles))]
    public void EverySettingsSection_IsAContentViewTheTabletPaneCanHost(string viewFile)
    {
        string path = Path.Combine(
            XamlResourceReader.RepositoryRoot, "CafeMaestro", "Views", "Settings", viewFile);

        File.Exists(path).Should().BeTrue();
        XDocument.Load(path).Root!.Name.LocalName.Should().Be("ContentView");
    }

    [Theory]
    [MemberData(nameof(SettingsSectionPageFiles))]
    public void EverySettingsPage_DelegatesToTheSharedSectionViewRatherThanRepeatingIt(string pageFile)
    {
        XDocument page = XDocument.Load(Path.Combine(
            XamlResourceReader.RepositoryRoot, "CafeMaestro", pageFile));

        page.Root!.Elements().Should().ContainSingle(
            "a settings page is only a Shell wrapper around the shared section view");
    }

    [Fact]
    public void TheSettingsPane_ShowsTheSectionItselfNotAButtonThatOpensIt()
    {
        string xaml = File.ReadAllText(Path.Combine(
            XamlResourceReader.RepositoryRoot, "CafeMaestro", "SettingsPage.xaml"));

        // The old pane summarised a section and made the user tap a second time to reach it.
        xaml.Should().NotContain("OPEN DATA TOOLS");
        xaml.Should().NotContain("EDIT ROAST LEVELS");
        xaml.Should().NotContain("CHOOSE THEME");
        xaml.Should().Contain("SectionHost", "the pane hosts the section's own body");
    }

    [Fact]
    public void FullWidthFormsAndConsoles_CapTheirContentSoTabletsDoNotStretchThem()
    {
        string[] mustCap =
        [
            Path.Combine("Views", "Roast", "RoastSetupView.xaml"),
            Path.Combine("Views", "Roast", "ActiveRoastView.xaml"),
            Path.Combine("Views", "Settings", "RoastingSettingsView.xaml"),
            Path.Combine("Views", "Settings", "AppearanceSettingsView.xaml"),
            Path.Combine("Views", "Settings", "DataSettingsView.xaml"),
            Path.Combine("Views", "Settings", "RoastLevelSettingsView.xaml"),
            Path.Combine("Views", "Settings", "AboutView.xaml"),
            "BeanDetailPage.xaml",
            "BeanEditPage.xaml",
            "RoastDetailPage.xaml",
            "RoastEditPage.xaml",
            "ImportPage.xaml"
        ];

        foreach (string relativePath in mustCap)
        {
            string xaml = File.ReadAllText(Path.Combine(
                XamlResourceReader.RepositoryRoot, "CafeMaestro", relativePath));

            xaml.Should().Contain(
                "ResponsiveLayout.MaxContentWidth",
                $"{relativePath} runs edge to edge and would stretch on a tablet");
        }
    }

    public static TheoryData<string> SettingsSectionViewFiles() => [.. SettingsSectionViews];

    public static TheoryData<string> SettingsSectionPageFiles() =>
    [
        "RoastingSettingsPage.xaml",
        "AppearanceSettingsPage.xaml",
        "DataSettingsPage.xaml",
        "RoastLevelSettingsPage.xaml",
        "AboutPage.xaml"
    ];
}
