using CafeMaestro.Services;
using CafeMaestro.ViewModels;
using FluentAssertions;
using Moq;

namespace CafeMaestro.Tests.ViewModels;

public sealed class AppearanceSettingsPageViewModelTests
{
    [Theory]
    [InlineData(ThemePreference.System)]
    [InlineData(ThemePreference.Light)]
    [InlineData(ThemePreference.Dark)]
    public async Task OnAppearing_LoadsTheExplicitChoiceAlreadyOnTheDevice(ThemePreference stored)
    {
        var preferences = new Mock<IPreferencesService>();
        preferences.Setup(service => service.GetThemePreferenceAsync()).ReturnsAsync(stored);
        var viewModel = new AppearanceSettingsPageViewModel(
            preferences.Object,
            Mock.Of<IThemeService>());

        await viewModel.OnAppearingAsync();

        viewModel.SelectedTheme.Should().Be(stored);
        preferences.Verify(
            service => service.SaveThemePreferenceAsync(It.IsAny<ThemePreference>()),
            Times.Never);
    }

    [Fact]
    public async Task OnAppearing_WithNoStoredPreference_LandsOnDark()
    {
        var preferences = new Mock<IPreferencesService>();
        // PreferencesService maps unreadable/absent storage through ThemePreferencePolicy.
        preferences
            .Setup(service => service.GetThemePreferenceAsync())
            .ReturnsAsync(ThemePreferencePolicy.FromStoredValue(null));
        var viewModel = new AppearanceSettingsPageViewModel(
            preferences.Object,
            Mock.Of<IThemeService>());

        await viewModel.OnAppearingAsync();

        viewModel.SelectedTheme.Should().Be(ThemePreference.Dark);
        viewModel.IsDarkSelected.Should().BeTrue();
    }

    [Fact]
    public async Task SelectingATheme_PersistsItAndUpdatesTheSelectionFlags()
    {
        var preferences = new Mock<IPreferencesService>();
        preferences.Setup(service => service.GetThemePreferenceAsync()).ReturnsAsync(ThemePreference.Dark);
        var themeService = new Mock<IThemeService>();
        var viewModel = new AppearanceSettingsPageViewModel(
            preferences.Object,
            themeService.Object);
        await viewModel.OnAppearingAsync();

        await viewModel.SelectThemeCommand.ExecuteAsync(ThemePreference.Light);

        preferences.Verify(
            service => service.SaveThemePreferenceAsync(ThemePreference.Light),
            Times.Once);
        themeService.Verify(service => service.ApplyAsync(ThemePreference.Light), Times.Once);
        viewModel.IsLightSelected.Should().BeTrue();
        viewModel.IsDarkSelected.Should().BeFalse();
        viewModel.SelectedThemeDisplay.Should().Be("Light");
    }
}
