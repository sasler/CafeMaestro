using CafeMaestro.Models;

namespace CafeMaestro.Services;

public interface IAppDataMigration
{
    int SourceVersion { get; }
    int TargetVersion { get; }
    void Migrate(AppData data);
}
