using CafeMaestro.Models;

namespace CafeMaestro.Services;

/// <summary>One source of truth for whether the roast presentation allows browsing tabs.</summary>
public static class RoastChromePolicy
{
    public static bool IsTabBarVisible(RoastPresentationState state) =>
        state is RoastPresentationState.Setup or RoastPresentationState.Handoff;
}
