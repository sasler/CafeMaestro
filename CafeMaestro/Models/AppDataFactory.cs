namespace CafeMaestro.Models;

public static class AppDataFactory
{
    public static AppData CreateDefault()
    {
        return new AppData
        {
            DataSchemaVersion = AppDataSchema.CurrentVersion,
            LastModified = DateTime.UtcNow,
            AppVersion = typeof(AppDataFactory).Assembly.GetName().Version?.ToString(3) ?? "1.0.0",
            Beans = [],
            RoastLogs = [],
            RoastLevels =
            [
                new RoastLevelData("Under Developed", 0.0, 11.0),
                new RoastLevelData("Light", 11.0, 13.0),
                new RoastLevelData("Medium-Light", 13.0, 14.0),
                new RoastLevelData("Medium", 14.0, 16.0),
                new RoastLevelData("Dark", 16.0, 18.0),
                new RoastLevelData("Extra Dark", 18.0, 22.0),
                new RoastLevelData("Burned", 22.0, 100.0)
            ]
        };
    }
}
