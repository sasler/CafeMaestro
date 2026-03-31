namespace CafeMaestro.Services;

public class NavigationService : INavigationService
{
    private readonly IShellNavigationProxy _shellNavigationProxy;

    public NavigationService()
        : this(new ShellNavigationProxy())
    {
    }

    internal NavigationService(IShellNavigationProxy shellNavigationProxy)
    {
        _shellNavigationProxy = shellNavigationProxy ?? throw new ArgumentNullException(nameof(shellNavigationProxy));
    }

    public Task GoToAsync(string route)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        return _shellNavigationProxy.GoToAsync(route);
    }

    public Task GoToAsync(string route, IDictionary<string, object> parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        ArgumentNullException.ThrowIfNull(parameters);
        return _shellNavigationProxy.GoToAsync(route, parameters);
    }

    public Task GoBackAsync()
    {
        return _shellNavigationProxy.GoToAsync("..");
    }

    internal interface IShellNavigationProxy
    {
        Task GoToAsync(string route);
        Task GoToAsync(string route, IDictionary<string, object> parameters);
    }

    internal sealed class ShellNavigationProxy : IShellNavigationProxy
    {
        public Task GoToAsync(string route)
        {
            return GetCurrentShell().GoToAsync(route);
        }

        public Task GoToAsync(string route, IDictionary<string, object> parameters)
        {
            return GetCurrentShell().GoToAsync(route, parameters);
        }

        private static Shell GetCurrentShell()
        {
            return Shell.Current ?? throw new InvalidOperationException("Shell.Current is not available.");
        }
    }
}
