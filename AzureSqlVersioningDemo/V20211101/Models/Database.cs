using AzureSqlVersioningDemo.Common.Models;

namespace AzureSqlVersioningDemo.V20211101.Models;

/// <summary>
/// Database properties for V2021-11-01 (legacy version).
/// </summary>
public class DatabaseProperties : Common.Models.DatabaseProperties
{
    public string? Edition { get; set; }
    public string? ServiceObjective { get; set; }
}

/// <summary>
/// ARM resource wrapper for Database (V2021-11-01).
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
