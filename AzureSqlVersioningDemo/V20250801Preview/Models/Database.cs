namespace AzureSqlVersioningDemo.V20250801Preview.Models;

/// <summary>
/// Database properties for V2025-08-01-preview.
/// Adds ElasticPoolId property.
/// </summary>
public class DatabaseProperties : Common.Models.DatabaseProperties
{
    public string? ElasticPoolId { get; set; }
}
