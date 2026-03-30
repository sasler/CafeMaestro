using System;
using CafeMaestro.Models;

namespace CafeMaestro
{
    public class NavigationParameters
    {
        public AppData AppData { get; set; }

        public NavigationParameters(AppData appData)
        {
            AppData = appData ?? throw new ArgumentNullException(nameof(appData));
        }

        public NavigationParameters()
        {
            AppData = new AppData();
        }
    }
}
