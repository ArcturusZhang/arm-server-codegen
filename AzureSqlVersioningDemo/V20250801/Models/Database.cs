using AzureSqlVersioningDemo.Common.Models;

namespace AzureSqlVersioningDemo.V20250801.Models;

/// <summary>
/// Database properties for V2025-08-01 (current stable).
/// </summary>
public class DatabaseProperties : Common.Models.DatabaseProperties
{
    public SkuInfo? Sku { get; set; }
    public string? ZoneRedundant { get; set; }
    public int? HighAvailabilityReplicaCount { get; set; }
}

/// <summary>
/// ARM resource wrapper for Database (V2025-08-01).
/// </summary>
public class DatabaseResource
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Type { get; set; }
    public string? Location { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
    public SkuInfo? Sku { get; set; }
    public DatabaseProperties? Properties { get; set; }
}
