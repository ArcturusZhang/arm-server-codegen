namespace AzureSqlVersioningDemo.Common.Models;

/// <summary>
/// Base database properties shared across all versions.
/// </summary>
public class DatabaseProperties
{
    public string? Description { get; set; }
    public string? Collation { get; set; }
    public long? MaxSizeBytes { get; set; }
    public string? Status { get; set; }
    public DateTimeOffset? CreationDate { get; set; }
}

/// <summary>
/// SKU information (introduced in newer API versions).
/// </summary>
public class SkuInfo
{
    public string? Name { get; set; }
    public string? Tier { get; set; }
    public string? Family { get; set; }
    public int? Capacity { get; set; }
}

/// <summary>
/// Create/Update request for Database.
/// </summary>
public class DatabaseCreateOrUpdateRequest
{
    public string? Location { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
    public SkuInfo? Sku { get; set; }
    public DatabaseProperties? Properties { get; set; }
}

/// <summary>
/// Async operation result (for long-running operations).
/// </summary>
public class AsyncOperationResult
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string Status { get; set; } = "InProgress";
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public double? PercentComplete { get; set; }
    public object? Properties { get; set; }
    public ErrorInfo? Error { get; set; }
}

/// <summary>
/// Error information for failed operations.
/// </summary>
public class ErrorInfo
{
    public string? Code { get; set; }
    public string? Message { get; set; }
}
