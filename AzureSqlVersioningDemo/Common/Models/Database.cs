using System.Text.Json.Serialization;

namespace AzureSqlVersioningDemo.Common.Models;

/// <summary>
/// Base database properties shared across all versions.
/// </summary>
[JsonDerivedType(typeof(DatabaseProperties))]
[JsonDerivedType(typeof(V20211101.Models.DatabaseProperties))]
[JsonDerivedType(typeof(V20211201.Models.DatabaseProperties))]
[JsonDerivedType(typeof(V20260201.Models.DatabaseProperties))]
public class DatabaseProperties
{
    public string? Description { get; set; }
    public string? Collation { get; set; }
    public long? MaxSizeBytes { get; set; }
    public string? Status { get; set; }
    public DateTimeOffset? CreationDate { get; set; }
}

/// <summary>
/// ARM resource wrapper for Database (shared across versions).
/// </summary>
public class DatabaseResource
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Type { get; set; }
    public string? Location { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
    public DatabaseProperties? Properties { get; set; }
}
