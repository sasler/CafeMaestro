using CafeMaestro.Services;
using FluentAssertions;

namespace CafeMaestro.Tests;

public class NavigationServiceTests
{
    [Fact]
    public async Task GoToAsync_DelegatesToShellProxy()
    {
        var proxy = new RecordingShellNavigationProxy();
        var navigationService = new NavigationService(proxy);

        await navigationService.GoToAsync("route");

        proxy.Route.Should().Be("route");
        proxy.Parameters.Should().BeNull();
    }

    [Fact]
    public async Task GoToAsync_WithParameters_DelegatesToShellProxy()
    {
        var proxy = new RecordingShellNavigationProxy();
        var navigationService = new NavigationService(proxy);
        Dictionary<string, object> parameters = new()
        {
            ["RoastId"] = Guid.NewGuid().ToString()
        };

        await navigationService.GoToAsync("route", parameters);

        proxy.Route.Should().Be("route");
        proxy.Parameters.Should().BeSameAs(parameters);
    }

    [Fact]
    public async Task GoBackAsync_UsesParentRoute()
    {
        var proxy = new RecordingShellNavigationProxy();
        var navigationService = new NavigationService(proxy);

        await navigationService.GoBackAsync();

        proxy.Route.Should().Be("..");
        proxy.Parameters.Should().BeNull();
    }

    private sealed class RecordingShellNavigationProxy : NavigationService.IShellNavigationProxy
    {
        public string? Route { get; private set; }
        public IDictionary<string, object>? Parameters { get; private set; }

        public Task GoToAsync(string route)
        {
            Route = route;
            Parameters = null;
            return Task.CompletedTask;
        }

        public Task GoToAsync(string route, IDictionary<string, object> parameters)
        {
            Route = route;
            Parameters = parameters;
            return Task.CompletedTask;
        }
    }
}
