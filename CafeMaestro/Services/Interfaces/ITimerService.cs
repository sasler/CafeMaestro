namespace CafeMaestro.Services;

public interface ITimerService
{
    event Action<TimeSpan>? TimeUpdated;
    bool IsRunning { get; }
    void Start();
    void Stop();
    void Reset();
    TimeSpan GetElapsedTime();
}
