using System;
using System.Collections.Generic;
using CafeMaestro.Models;

namespace CafeMaestro
{
    // This class contains navigation parameters for passing data between pages
    public class NavigationParameters
    {
        // The current application data
        public AppData AppData { get; set; }
        
        // Constructor with AppData
        public NavigationParameters(AppData appData)
        {
            AppData = appData ?? throw new ArgumentNullException(nameof(appData));
        }
        
        // Default constructor for serialization
        public NavigationParameters()
        {
            AppData = new AppData();
        }
    }
}