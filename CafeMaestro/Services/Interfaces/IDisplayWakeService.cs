namespace CafeMaestro.Services;

public interface IDisplayWakeService
{
    Task SetKeepScreenOnAsync(bool keepScreenOn);
}
