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
    public Task<bool> ShowConfirmationAsync(string title, string message, string accept, string cancel)
    {
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            Page? page = Shell.Current?.CurrentPage
                         ?? Application.Current?.Windows.FirstOrDefault()?.Page;

            return page is not null &&
                   await page.DisplayAlertAsync(title, message, accept, cancel);
        });
    }

}
