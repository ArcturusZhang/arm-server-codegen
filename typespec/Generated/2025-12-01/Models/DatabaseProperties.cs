namespace Generated.V20251201.Models {
    /// <summary>
    /// Database resource properties.
    /// </summary>
    public class DatabaseProperties
    {
        /// <summary>
        /// The collation of the database.
        /// </summary>
        public string? collation { get; set; }

        /// <summary>
        /// The max size of the database in bytes.
        /// </summary>
        public long? maxSizeBytes { get; set; }

        /// <summary>
        /// The status of the database.
        /// </summary>
        public string? status { get; set; }

        /// <summary>
        /// The creation date of the database.
        /// </summary>
        public DateTimeOffset? creationDate { get; set; }

        /// <summary>
        /// The provisioning state.
        /// </summary>
        public <Unresolved Symbol: refkey[sSymbol(emitter-framework:csharp)⁣o1]>? provisioningState { get; set; }
    }
}
