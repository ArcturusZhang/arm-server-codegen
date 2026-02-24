using Generated.V20251201.Models;

namespace Generated.V20251201.Models {
    /// <summary>
    /// The resource model definition for an Azure Resource Manager tracked top level resource which has 'tags' and a 'location'
    /// </summary>
    public class TrackedResource : Resource
    {
        /// <summary>
        /// Resource tags.
        /// </summary>
        public IDictionary<string, string>? tags { get; set; }

        /// <summary>
        /// The geo-location where the resource lives
        /// </summary>
        public required string location { get; set; }
    }
}
