using Generated.V20251201.Models;

namespace Generated.V20251201.Models {
    /// <summary>
    /// Metadata pertaining to creation and last modification of the resource.
    /// </summary>
    public class SystemData
    {
        /// <summary>
        /// The identity that created the resource.
        /// </summary>
        public string? createdBy { get; set; }

        /// <summary>
        /// The type of identity that created the resource.
        /// </summary>
        public createdByType? createdByType { get; set; }

        /// <summary>
        /// The timestamp of resource creation (UTC).
        /// </summary>
        public DateTimeOffset? createdAt { get; set; }

        /// <summary>
        /// The identity that last modified the resource.
        /// </summary>
        public string? lastModifiedBy { get; set; }

        /// <summary>
        /// The type of identity that last modified the resource.
        /// </summary>
        public createdByType? lastModifiedByType { get; set; }

        /// <summary>
        /// The timestamp of resource last modification (UTC)
        /// </summary>
        public DateTimeOffset? lastModifiedAt { get; set; }
    }
}
