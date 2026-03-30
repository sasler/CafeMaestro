namespace CafeMaestro.Navigation;

public static class Routes
{
    // Tab routes (prefixed with //)
    public const string Main = "//MainPage";
    public const string Roast = "//RoastPage";
    public const string RoastLog = "//RoastLogPage";
    public const string BeanInventory = "//BeanInventoryPage";
    public const string Settings = "//SettingsPage";

    // Registered route names (no // prefix)
    public const string MainPage = "MainPage";
    public const string RoastPage = "RoastPage";
    public const string RoastLogPage = "RoastLogPage";
    public const string BeanInventoryPage = "BeanInventoryPage";
    public const string SettingsPage = "SettingsPage";

    // Detail routes (no // prefix)
    public const string BeanEdit = "BeanEditPage";
    public const string BeanImport = "BeanImportPage";
    public const string RoastImport = "RoastImportPage";
}
