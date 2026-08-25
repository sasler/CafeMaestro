using CafeMaestro.Models;

namespace CafeMaestro.Services;

internal sealed class AppDataMigrationPipeline
{
    private readonly IReadOnlyDictionary<int, IAppDataMigration> _migrations;

    public AppDataMigrationPipeline(IEnumerable<IAppDataMigration>? migrations = null)
    {
        IAppDataMigration[] configured = (migrations ?? [new V1ToV2AppDataMigration()]).ToArray();
        _migrations = configured.ToDictionary(migration => migration.SourceVersion);
    }

    public bool MigrateToCurrent(AppData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.DataSchemaVersion > AppDataSchema.CurrentVersion)
        {
            throw new InvalidDataException(
                $"This data file uses newer schema version {data.DataSchemaVersion}. " +
                $"CafeMaestro supports version {AppDataSchema.CurrentVersion}; keep the file unchanged for recovery with a newer app version.");
        }

        bool migrated = false;
        while (data.DataSchemaVersion < AppDataSchema.CurrentVersion)
        {
            if (!_migrations.TryGetValue(data.DataSchemaVersion, out IAppDataMigration? migration) ||
                migration.TargetVersion <= migration.SourceVersion)
            {
                throw new InvalidDataException(
                    $"No safe migration is available from data schema version {data.DataSchemaVersion}.");
            }

            migration.Migrate(data);
            if (data.DataSchemaVersion != migration.TargetVersion)
            {
                throw new InvalidDataException(
                    $"Migration from version {migration.SourceVersion} did not produce version {migration.TargetVersion}.");
            }

            migrated = true;
        }

        return migrated;
    }
}
