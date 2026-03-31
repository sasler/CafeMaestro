using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace CafeMaestro.Services;

public sealed class AlertService : IAlertService
{
    public Task ShowAlertAsync(string title, string message, string cancel)
    {
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            Page? page = Shell.Current?.CurrentPage
                         ?? Application.Current?.Windows.FirstOrDefault()?.Page;

            if (page is null)
            {
                return;
            }

            await page.DisplayAlertAsync(title, message, cancel);
        });
    }
}
