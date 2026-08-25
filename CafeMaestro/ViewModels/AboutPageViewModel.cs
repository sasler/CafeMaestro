using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CafeMaestro.ViewModels;

/// <summary>Version, install history and licensing, moved out of the settings scroll.</summary>
public partial class AboutPageViewModel : ObservableObject
{
    private readonly IAppVersionProvider _versionProvider;

    public AboutPageViewModel(IAppVersionProvider versionProvider)
    {
        _versionProvider = versionProvider ?? throw new ArgumentNullException(nameof(versionProvider));
    }

    [ObservableProperty]
    public partial string VersionDisplay { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FirstInstalledVersionDisplay { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string VersionHistoryDisplay { get; set; } = string.Empty;

    public string ProductName => "CafeMaestro";

    public string ProductTagline => "Coffee roasting log and companion";

    public string LicenseDisplay =>
        "© 2025 CafeMaestro Team. Released under the MIT License.";

    public string PrivacyDisplay =>
        "Your beans and roasts stay on this device. CafeMaestro has no account, no cloud sync, " +
        "and no analytics.";

    public Task OnAppearingAsync()
    {
        LoadVersionInfo();
        return Task.CompletedTask;
    }

    private void LoadVersionInfo()
    {
        try
        {
            VersionDisplay = $"{_versionProvider.VersionString} (Build {_versionProvider.BuildString})";
            FirstInstalledVersionDisplay =
                $"First installed version: {_versionProvider.FirstInstalledVersion}";

            var history = new StringBuilder();
            foreach (string version in _versionProvider.VersionHistory.Take(5))
            {
                history.AppendLine($"• {version}");
            }

            VersionHistoryDisplay = history.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Version information unavailable: {ex.Message}");
            VersionDisplay = "Unavailable";
            FirstInstalledVersionDisplay = string.Empty;
            VersionHistoryDisplay = string.Empty;
        }
    }
}

/// <summary>
/// Seam over <c>AppInfo</c>/<c>VersionTracking</c>, which need a MAUI host to answer.
/// </summary>
public interface IAppVersionProvider
{
    string VersionString { get; }
    string BuildString { get; }
    string FirstInstalledVersion { get; }
    IReadOnlyList<string> VersionHistory { get; }
}

public sealed class PlatformAppVersionProvider : IAppVersionProvider
{
    public string VersionString => AppInfo.Current.VersionString;

    public string BuildString => AppInfo.Current.BuildString;

    public string FirstInstalledVersion => VersionTracking.FirstInstalledVersion ?? "Unknown";

    public IReadOnlyList<string> VersionHistory => VersionTracking.VersionHistory.ToList();
}
