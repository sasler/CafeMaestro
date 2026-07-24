namespace CafeMaestro.Services;

public interface IAlertService
{
    Task ShowAlertAsync(string title, string message, string cancel);
    Task<bool> ShowConfirmationAsync(string title, string message, string accept, string cancel);
}
