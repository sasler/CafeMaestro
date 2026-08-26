namespace CafeMaestro.Navigation;

public static class Routes
{
    // Tab routes (prefixed with //)
    public const string Roast = "//RoastPage";
    public const string RoastLog = "//RoastLogPage";
    public const string BeanInventory = "//BeanInventoryPage";
    public const string Settings = "//SettingsPage";

    // Registered route names (no // prefix)
    public const string RoastPage = "RoastPage";
    public const string RoastLogPage = "RoastLogPage";
    public const string BeanInventoryPage = "BeanInventoryPage";
    public const string SettingsPage = "SettingsPage";

#if DEBUG
    // Review harness for the shared visual system. Never registered in a release build.
    public const string ComponentGallery = "ComponentGalleryPage";
#endif

    // Detail routes (no // prefix)
    public const string BeanEdit = "BeanEditPage";
    public const string BeanDetail = "BeanDetailPage";
    public const string RoastEdit = "RoastEditPage";
    public const string RoastDetail = "RoastDetailPage";
    /// <summary>
    /// The single guided CSV import flow. Callers pass <c>ImportPageViewModel.KindParameter</c>
    /// to preselect Beans or Roasts.
    /// </summary>
    public const string Import = "ImportPage";
}
