using CafeMaestro.Services;
using FluentAssertions;
using Moq;

namespace CafeMaestro.Tests.Services;

public sealed class ManagedAppDataCorruptionTests : IDisposable
{
    private readonly string _testDirectory =
        Path.Combine(Path.GetTempPath(), "CafeMaestro.Tests", Guid.NewGuid().ToString("N"));

    public ManagedAppDataCorruptionTests()
    {
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public async Task InitializeAsync_MalformedCanonicalJsonThrowsWithoutReplacingIt()
    {
        string canonicalPath = Path.Combine(_testDirectory, "cafemaestro_data.json");
        const string malformedJson = "{\"Beans\":";
        await File.WriteAllTextAsync(canonicalPath, malformedJson);
        var service = new ManagedAppDataService(canonicalPath);

        Func<Task> action = () => service.InitializeAsync(Mock.Of<IPreferencesService>());

        await action.Should().ThrowAsync<InvalidDataException>();
        (await File.ReadAllTextAsync(canonicalPath)).Should().Be(malformedJson);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }
}
