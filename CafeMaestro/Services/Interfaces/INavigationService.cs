namespace CafeMaestro.Services;

public interface INavigationService
{
    Task GoToAsync(string route);
    Task GoToAsync(string route, IDictionary<string, object> parameters);
    Task GoBackAsync();
}
