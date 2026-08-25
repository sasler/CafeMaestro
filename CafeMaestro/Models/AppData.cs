using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CafeMaestro.Models
{
    /// <summary>
    /// Container class that holds all application data in a single structure
    /// </summary>
    public class AppData
    {
        internal long PersistenceRevision { get; set; }

        /// <summary>
        /// Version of the persisted data contract.
        /// </summary>
        public int DataSchemaVersion { get; set; } = AppDataSchema.CurrentVersion;

        /// <summary>
        /// All bean inventory items
        /// </summary>
        public List<BeanData> Beans { get; set; } = new List<BeanData>();
        
        /// <summary>
        /// All roast log entries
        /// </summary>
        public List<RoastData> RoastLogs { get; set; } = new List<RoastData>();
        
        /// <summary>
        /// User-configurable roast levels
        /// </summary>
        public List<RoastLevelData> RoastLevels { get; set; } = new List<RoastLevelData>();

        /// <summary>
        /// Durable state for the current roasting session, when one exists.
        /// </summary>
        public RoastSessionData? ActiveRoastSession { get; set; }
        
        /// <summary>
        /// Timestamp of last modification
        /// </summary>
        public DateTime LastModified { get; set; } = DateTime.Now;
        
        /// <summary>
        /// App version that created/modified this data
        /// </summary>
        public string AppVersion { get; set; } = "1.0.0";
    }
}
