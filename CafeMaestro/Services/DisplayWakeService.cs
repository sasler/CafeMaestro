using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;

namespace CafeMaestro.Services;

public sealed class DisplayWakeService : IDisplayWakeService
{
    public Task SetKeepScreenOnAsync(bool keepScreenOn) => MainThread.InvokeOnMainThreadAsync(() =>
    {
        DeviceDisplay.Current.KeepScreenOn = keepScreenOn;
    });
}
