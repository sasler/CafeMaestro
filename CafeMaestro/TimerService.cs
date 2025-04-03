using System;
using System.Timers;
using Microsoft.Maui.ApplicationModel;

namespace CafeMaestro
{
    public class TimerService
    {
        private System.Timers.Timer timer; // Explicitly specify System.Timers.Timer
        private DateTime startTime;
        private TimeSpan elapsedTime;
        private bool isRunning;

        public event Action<TimeSpan>? TimeUpdated; // Made nullable to fix warning

        public TimerService()
        {
            timer = new System.Timers.Timer(100); // Update every 100ms
            timer.Elapsed += OnTimerElapsed;
            elapsedTime = TimeSpan.Zero;
        }

        private void OnTimerElapsed(object? sender, ElapsedEventArgs e) // Adjusted sender to be nullable
        {
            if (isRunning)
            {
                elapsedTime = DateTime.Now - startTime;
                MainThread.BeginInvokeOnMainThread(() => TimeUpdated?.Invoke(elapsedTime));
            }
        }

        public void Start()
        {
            if (!isRunning)
            {
                startTime = DateTime.Now - elapsedTime;
                timer.Start();
                isRunning = true;
            }
        }

        public void Stop()
        {
            if (isRunning)
            {
                timer.Stop();
                isRunning = false;
            }
        }

        public void Reset()
        {
            Stop();
            elapsedTime = TimeSpan.Zero;
            MainThread.BeginInvokeOnMainThread(() => TimeUpdated?.Invoke(elapsedTime));
        }

        public TimeSpan GetElapsedTime()
        {
            return elapsedTime;
        }

        public bool IsRunning => isRunning;
    }
}