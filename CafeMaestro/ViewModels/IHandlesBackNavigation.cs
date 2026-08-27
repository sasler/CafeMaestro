namespace CafeMaestro.ViewModels;

/// <summary>
/// A section that has its own idea of what system Back means.
///
/// On a phone the section owns a page, and that page's <c>OnBackButtonPressed</c> can close an
/// open sheet before Shell navigates. Hosted inline on a tablet there is no such page, so the
/// host asks the section first and only navigates when the section declines.
/// </summary>
public interface IHandlesBackNavigation
{
    /// <summary>Returns true when the section consumed Back itself.</summary>
    bool TryHandleBack();
}
