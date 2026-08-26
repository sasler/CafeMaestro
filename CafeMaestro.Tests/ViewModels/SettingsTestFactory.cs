using CafeMaestro.Models;
using CafeMaestro.Services;
using Moq;

namespace CafeMaestro.Tests.ViewModels;

/// <summary>
/// Shared seams for the settings ViewModels: a roast session that is idle unless a test says
/// otherwise, and the app data those pages summarise.
/// </summary>
internal static class SettingsTestFactory
{
    public static RoastSessionSnapshot IdleSnapshot() => new()
    {
        AsOfUtc = DateTimeOffset.UnixEpoch,
        NextBatchNumber = 1,
        RequiresRecovery = false,
        OpenWork = [],
        ActiveRoast = null
    };

    public static RoastSessionSnapshot ActiveSnapshot() => IdleSnapshot() with
    {
        SessionId = Guid.NewGuid(),
        ActiveRoast = new ActiveRoastSnapshot
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            BatchNumber = 1,
            BeanId = Guid.NewGuid(),
            BeanDisplaySnapshot = "Ethiopia Guji",
            BatchWeight = 240,
            Temperature = 218,
            Phase = ActiveRoastPhase.Roasting,
            StartedAtUtc = DateTimeOffset.UnixEpoch,
            ElapsedSeconds = 120,
            FirstCrackEnabled = false,
            CoolingDurationSeconds = 300,
            IsElapsedImplausible = false,
            RequiresCorrectedElapsed = false
        }
    };

    public static Mock<IRoastSessionService> IdleSession()
    {
        var session = new Mock<IRoastSessionService>();
        session
            .Setup(service => service.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdleSnapshot());
        return session;
    }

    public static Mock<IRoastSessionService> ActiveSession()
    {
        var session = new Mock<IRoastSessionService>();
        session
            .Setup(service => service.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveSnapshot());
        return session;
    }

    public static AppData Data(int beans, int roasts) => new()
    {
        LastModified = DateTime.UtcNow,
        Beans = Enumerable.Range(0, beans)
            .Select(index => new BeanData
            {
                CoffeeName = $"Bean {index}",
                Country = "Test",
                Quantity = 1,
                RemainingQuantity = 1
            })
            .ToList(),
        RoastLogs = Enumerable.Range(0, roasts)
            .Select(index => new RoastData
            {
                BeanType = $"Bean {index}",
                BatchWeight = 1,
                Temperature = 200
            })
            .ToList(),
        RoastLevels = AppDataFactory.CreateDefault().RoastLevels
    };
}
