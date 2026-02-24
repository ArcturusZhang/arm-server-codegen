using Generated.V20251101.Models;

namespace Generated.V20251101.Models {
    /// <summary>
    /// A SQL Database resource.
    /// </summary>
    public class Database : TrackedResource
    {
        /// <summary>
        /// The resource-specific properties for this resource.
        /// </summary>
        public DatabaseProperties? properties { get; set; }

        /// <summary>
        /// The name of the database.
        /// </summary>
        public new required string name { get; set; }
    }
}
