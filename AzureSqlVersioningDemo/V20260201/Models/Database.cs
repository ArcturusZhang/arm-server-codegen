namespace AzureSqlVersioningDemo.V20260201.Models;

/// <summary>
/// Database properties for V2026-02-01.
/// Adds ElasticPoolId property.
/// </summary>
public class DatabaseProperties : Common.Models.DatabaseProperties
{
    public string? ElasticPoolId { get; set; }
}
