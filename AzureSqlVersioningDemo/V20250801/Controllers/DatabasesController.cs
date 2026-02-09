using Asp.Versioning;
using AzureSqlVersioningDemo.Common.Models;
using AzureSqlVersioningDemo.V20250801.Models;
using Microsoft.AspNetCore.Mvc;

namespace AzureSqlVersioningDemo.V20250801.Controllers;

/// <summary>
/// Database controller for API version 2025-08-01.
/// This version uses SKU instead of Edition/ServiceObjective.
/// </summary>
[ApiController]
[ApiVersion("2025-08-01")]
[Route("subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Sql/servers/{serverName}/databases")]
public class DatabasesController : ControllerBase
{
    private readonly ILogger<DatabasesController> _logger;

    private static readonly Dictionary<string, AsyncOperationResult> _operations = new();

    public DatabasesController(ILogger<DatabasesController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets a database (V2025-08-01 format with SKU).
    /// </summary>
    [HttpGet("{databaseName}")]
    public ActionResult<DatabaseResource> Get(
        string subscriptionId,
        string resourceGroupName,
        string serverName,
        string databaseName)
    {
        _logger.LogInformation("GET Database called with API version 2025-08-01");

        return Ok(new DatabaseResource
        {
            Id = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Sql/servers/{serverName}/databases/{databaseName}",
            Name = databaseName,
            Type = "Microsoft.Sql/servers/databases",
            Location = "eastus",
            Sku = new SkuInfo
            {
                Name = "S0",
                Tier = "Standard",
                Capacity = 10
            },
            Properties = new Models.DatabaseProperties
            {
                Description = "Served by V20250801 controller",
                Collation = "SQL_Latin1_General_CP1_CI_AS",
                MaxSizeBytes = 268435456000,
                Status = "Online",
                CreationDate = DateTimeOffset.UtcNow.AddDays(-30),
                ZoneRedundant = "Disabled",
                HighAvailabilityReplicaCount = 0
            }
        });
    }

    /// <summary>
    /// Lists all databases on a server (V2025-08-01 format).
    /// </summary>
    [HttpGet]
    public ActionResult<IEnumerable<DatabaseResource>> List(
        string subscriptionId,
        string resourceGroupName,
        string serverName)
    {
        _logger.LogInformation("LIST Databases called with API version 2025-08-01");

        return Ok(new[]
        {
            new DatabaseResource
            {
                Id = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Sql/servers/{serverName}/databases/db1",
                Name = "db1",
                Type = "Microsoft.Sql/servers/databases",
                Location = "eastus",
                Sku = new SkuInfo { Name = "S0", Tier = "Standard" },
                Properties = new Models.DatabaseProperties
                {
                    Description = "Served by V20250801 controller",
                    Status = "Online",
                    ZoneRedundant = "Disabled"
                }
            }
        });
    }

    /// <summary>
    /// Creates or updates a database (V2025-08-01).
    /// This is a long-running operation that returns 202 Accepted.
    /// </summary>
    [HttpPut("{databaseName}")]
    public IActionResult CreateOrUpdate(
        string subscriptionId,
        string resourceGroupName,
        string serverName,
        string databaseName,
        [FromBody] DatabaseCreateOrUpdateRequest request)
    {
        _logger.LogInformation("PUT Database called with API version 2025-08-01 - Long Running Operation");

        var operationId = Guid.NewGuid().ToString();

        var operation = new AsyncOperationResult
        {
            Id = operationId,
            Name = operationId,
            Status = "InProgress",
            StartTime = DateTimeOffset.UtcNow,
            PercentComplete = 0
        };
        _operations[operationId] = operation;

        var locationUrl = $"{Request.Scheme}://{Request.Host}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Sql/servers/{serverName}/databases/{databaseName}/operationResults/{operationId}";
        var azureAsyncOperationUrl = $"{Request.Scheme}://{Request.Host}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Sql/servers/{serverName}/databases/{databaseName}/azureAsyncOperation/{operationId}";

        Response.Headers.Append("Location", locationUrl);
        Response.Headers.Append("Azure-AsyncOperation", azureAsyncOperationUrl);
        Response.Headers.Append("Retry-After", "15");

        return Accepted(new DatabaseResource
        {
            Id = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Sql/servers/{serverName}/databases/{databaseName}",
            Name = databaseName,
            Type = "Microsoft.Sql/servers/databases",
            Location = request.Location ?? "eastus",
            Tags = request.Tags,
            Sku = request.Sku ?? new SkuInfo { Name = "S0", Tier = "Standard" },
            Properties = new Models.DatabaseProperties
            {
                Description = "Served by V20250801 controller",
                Status = "Creating"
            }
        });
    }

    /// <summary>
    /// Gets the status of an async operation (Azure-AsyncOperation endpoint).
    /// </summary>
    [HttpGet("{databaseName}/azureAsyncOperation/{operationId}")]
    public ActionResult<AsyncOperationResult> GetAsyncOperation(
        string subscriptionId,
        string resourceGroupName,
        string serverName,
        string databaseName,
        string operationId)
    {
        _logger.LogInformation("GET Azure-AsyncOperation called for operation {OperationId}", operationId);

        if (!_operations.TryGetValue(operationId, out var operation))
        {
            operation = new AsyncOperationResult
            {
                Id = operationId,
                Name = operationId,
                Status = "Succeeded",
                StartTime = DateTimeOffset.UtcNow.AddMinutes(-1),
                EndTime = DateTimeOffset.UtcNow,
                PercentComplete = 100
            };
        }
        else
        {
            operation.Status = "Succeeded";
            operation.PercentComplete = 100;
            operation.EndTime = DateTimeOffset.UtcNow;
        }

        return Ok(operation);
    }

    /// <summary>
    /// Gets the result of a long-running operation (Location endpoint).
    /// </summary>
    [HttpGet("{databaseName}/operationResults/{operationId}")]
    public ActionResult<DatabaseResource> GetOperationResult(
        string subscriptionId,
        string resourceGroupName,
        string serverName,
        string databaseName,
        string operationId)
    {
        _logger.LogInformation("GET OperationResults called for operation {OperationId}", operationId);

        return Ok(new DatabaseResource
        {
            Id = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Sql/servers/{serverName}/databases/{databaseName}",
            Name = databaseName,
            Type = "Microsoft.Sql/servers/databases",
            Location = "eastus",
            Sku = new SkuInfo { Name = "S0", Tier = "Standard" },
            Properties = new Models.DatabaseProperties
            {
                Description = "Served by V20250801 controller",
                Status = "Online",
                CreationDate = DateTimeOffset.UtcNow
            }
        });
    }

    // Delete intentionally omitted — VersionFallbackConvention will route to V20211101's Delete.
}
