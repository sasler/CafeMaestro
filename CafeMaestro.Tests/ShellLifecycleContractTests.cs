using System.Xml.Linq;
using CafeMaestro.Models;
using CafeMaestro.Services;
using FluentAssertions;
using Moq;

namespace CafeMaestro.Tests;

public class ShellLifecycleContractTests
{
    [Fact]
    public void Shell_ContainsExactlyTheFourConfirmedTabs_InLaunchOrder()
    {
        string shellPath = Path.Combine(XamlResourceReader.RepositoryRoot, "CafeMaestro", "AppShell.xaml");
        XDocument shell = XDocument.Load(shellPath);

        XElement[] tabs = shell.Descendants()
            .Where(element => element.Name.LocalName == "ShellContent")
            .ToArray();

        tabs.Select(tab => tab.Attribute("Title")?.Value)
            .Should().Equal("Roast", "Log", "Beans", "Settings");
        tabs.Select(tab => tab.Attribute("Route")?.Value)
            .Should().Equal("RoastPage", "RoastLogPage", "BeanInventoryPage", "SettingsPage");
    }

    [Theory]
    [InlineData(RoastPresentationState.Setup, true)]
    [InlineData(RoastPresentationState.Active, false)]
    [InlineData(RoastPresentationState.Handoff, true)]
    [InlineData(RoastPresentationState.Recovery, false)]
    [InlineData(RoastPresentationState.PersistenceError, false)]
    public void RoastChromePolicy_RestoresTabsOnlyOutsideFocusedStates(
        RoastPresentationState state,
        bool expectedVisible)
    {
        RoastChromePolicy.IsTabBarVisible(state).Should().Be(expectedVisible);
    }

    [Fact]
    public async Task ActivationService_DeliversOneQueuedPayloadToTheCrossPlatformHandler()
    {
        Mock<IAppActivationHandler> handler = new();
        AppActivationPayload payload = new("cooling-ready", new Dictionary<string, string>
        {
            ["roastId"] = Guid.NewGuid().ToString()
        });
        AppActivationService service = new(handler.Object);

        service.Queue(payload);
        service.SetReady();
        await service.HandlePendingAsync();
        await service.HandlePendingAsync();

        handler.Verify(candidate => candidate.HandleAsync(payload, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ActivationService_RetainsPayloadWhenHandlerFails()
    {
        Mock<IAppActivationHandler> handler = new();
        AppActivationPayload payload = new("cooling-ready", new Dictionary<string, string>());
        handler.SetupSequence(candidate => candidate.HandleAsync(payload, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("not ready"))
            .Returns(Task.CompletedTask);
        AppActivationService service = new(handler.Object);
        service.Queue(payload);
        service.SetReady();

        Func<Task> firstAttempt = () => service.HandlePendingAsync();
        await firstAttempt.Should().ThrowAsync<InvalidOperationException>();
        await service.HandlePendingAsync();

        handler.Verify(candidate => candidate.HandleAsync(payload, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ActivationService_RetainsColdPayloadUntilInitializationAndShellAreReady()
    {
        Mock<IAppActivationHandler> handler = new();
        AppActivationPayload payload = new("cooling-ready", new Dictionary<string, string>());
        AppActivationService service = new(handler.Object);
        service.Queue(payload);

        await service.HandlePendingAsync();
        handler.Verify(candidate => candidate.HandleAsync(
            It.IsAny<AppActivationPayload>(), It.IsAny<CancellationToken>()), Times.Never);

        service.SetReady();
        await service.HandlePendingAsync();
        handler.Verify(candidate => candidate.HandleAsync(payload, It.IsAny<CancellationToken>()), Times.Once);
    }
}
