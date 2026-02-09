using AzureSqlVersioningDemo.Common.Models;

namespace AzureSqlVersioningDemo.V20250801Preview.Models;

/// <summary>
/// Database properties for V2025-08-01-preview.
/// Includes preview features: PreferredEnclaveType, UseFreeLimit.
/// </summary>
public class DatabaseProperties : V20250801.Models.DatabaseProperties
{
    public string? PreferredEnclaveType { get; set; }
    public bool? UseFreeLimit { get; set; }
}

/// <summary>
/// ARM resource wrapper for Database (V2025-08-01-preview).
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
