using Asp.Versioning;
using AzureSqlVersioningDemo.Common.Models;
using AzureSqlVersioningDemo.V20250801Preview.Models;
using Microsoft.AspNetCore.Mvc;

namespace AzureSqlVersioningDemo.V20250801Preview.Controllers;

/// <summary>
/// Database controller for API version 2025-08-01-preview.
/// This preview version includes experimental features like PreferredEnclaveType and UseFreeLimit.
/// </summary>
[ApiController]
[ApiVersion("2025-08-01-preview")]
[Route("subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Sql/servers/{serverName}/databases")]
public class DatabasesController : ControllerBase
{
    private readonly ILogger<DatabasesController> _logger;

    public DatabasesController(ILogger<DatabasesController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets a database (V2025-08-01-preview format with preview features).
    /// </summary>
    [HttpGet("{databaseName}")]
    public ActionResult<DatabaseResource> Get(
        string subscriptionId,
        string resourceGroupName,
        string serverName,
        string databaseName)
    {
        _logger.LogInformation("GET Database called with API version 2025-08-01-preview");

        return Ok(new DatabaseResource
        {
            Id = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Sql/servers/{serverName}/databases/{databaseName}",
            Name = databaseName,
            Type = "Microsoft.Sql/servers/databases",
            Location = "eastus",
            Sku = new SkuInfo
            {
                Name = "GP_S_Gen5_2",
                Tier = "GeneralPurpose",
                Family = "Gen5",
                Capacity = 2
            },
            Properties = new Models.DatabaseProperties
            {
                Collation = "SQL_Latin1_General_CP1_CI_AS",
                MaxSizeBytes = 268435456000,
                Status = "Online",
                CreationDate = DateTimeOffset.UtcNow.AddDays(-30),
                ZoneRedundant = "Disabled",
                HighAvailabilityReplicaCount = 0,
                PreferredEnclaveType = "VBS",
                UseFreeLimit = true
            }
        });
    }

    /// <summary>
    /// Lists all databases with preview features.
    /// </summary>
    [HttpGet]
    public ActionResult<IEnumerable<DatabaseResource>> List(
        string subscriptionId,
        string resourceGroupName,
        string serverName)
    {
        _logger.LogInformation("LIST Databases called with API version 2025-08-01-preview");

        return Ok(new[]
        {
            new DatabaseResource
            {
                Id = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Sql/servers/{serverName}/databases/db1",
                Name = "db1",
                Type = "Microsoft.Sql/servers/databases",
                Location = "eastus",
                Sku = new SkuInfo { Name = "GP_S_Gen5_2", Tier = "GeneralPurpose", Family = "Gen5", Capacity = 2 },
                Properties = new Models.DatabaseProperties
                {
                    Status = "Online",
                    PreferredEnclaveType = "VBS",
                    UseFreeLimit = false
                }
            },
            new DatabaseResource
            {
                Id = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Sql/servers/{serverName}/databases/freeDb",
                Name = "freeDb",
                Type = "Microsoft.Sql/servers/databases",
                Location = "eastus",
                Sku = new SkuInfo { Name = "GP_S_Gen5_1", Tier = "GeneralPurpose", Family = "Gen5", Capacity = 1 },
                Properties = new Models.DatabaseProperties
                {
                    Status = "Online",
                    PreferredEnclaveType = "Default",
                    UseFreeLimit = true
                }
            }
        });
    }

    /// <summary>
    /// Creates or updates a database with preview features.
    /// </summary>
    [HttpPut("{databaseName}")]
    public IActionResult CreateOrUpdate(
        string subscriptionId,
        string resourceGroupName,
        string serverName,
        string databaseName,
        [FromBody] DatabaseCreateOrUpdateRequest request)
    {
        _logger.LogInformation("PUT Database called with API version 2025-08-01-preview - Long Running Operation");

        var operationId = Guid.NewGuid().ToString();

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
            Sku = request.Sku ?? new SkuInfo { Name = "GP_S_Gen5_2", Tier = "GeneralPurpose" },
            Properties = new Models.DatabaseProperties
            {
                Status = "Creating",
                PreferredEnclaveType = "VBS",
                UseFreeLimit = false
            }
        });
    }

    /// <summary>
    /// Gets the status of an async operation.
    /// </summary>
    [HttpGet("{databaseName}/azureAsyncOperation/{operationId}")]
    public ActionResult<AsyncOperationResult> GetAsyncOperation(
        string subscriptionId,
        string resourceGroupName,
        string serverName,
        string databaseName,
        string operationId)
    {
        return Ok(new AsyncOperationResult
        {
            Id = operationId,
            Name = operationId,
            Status = "Succeeded",
            StartTime = DateTimeOffset.UtcNow.AddMinutes(-1),
            EndTime = DateTimeOffset.UtcNow,
            PercentComplete = 100
        });
    }

    /// <summary>
    /// Gets the result of a long-running operation.
    /// </summary>
    [HttpGet("{databaseName}/operationResults/{operationId}")]
    public ActionResult<DatabaseResource> GetOperationResult(
        string subscriptionId,
        string resourceGroupName,
        string serverName,
        string databaseName,
        string operationId)
    {
        return Ok(new DatabaseResource
        {
            Id = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Sql/servers/{serverName}/databases/{databaseName}",
            Name = databaseName,
            Type = "Microsoft.Sql/servers/databases",
            Location = "eastus",
            Sku = new SkuInfo { Name = "GP_S_Gen5_2", Tier = "GeneralPurpose" },
            Properties = new Models.DatabaseProperties
            {
                Status = "Online",
                CreationDate = DateTimeOffset.UtcNow,
                PreferredEnclaveType = "VBS",
                UseFreeLimit = false
            }
        });
    }

    /// <summary>
    /// Deletes a database.
    /// </summary>
    [HttpDelete("{databaseName}")]
    public IActionResult Delete(
        string subscriptionId,
        string resourceGroupName,
        string serverName,
        string databaseName)
    {
        _logger.LogInformation("DELETE Database called with API version 2025-08-01-preview");

        var operationId = Guid.NewGuid().ToString();
        var locationUrl = $"{Request.Scheme}://{Request.Host}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Sql/servers/{serverName}/databases/{databaseName}/operationResults/{operationId}";

        Response.Headers.Append("Location", locationUrl);
        Response.Headers.Append("Retry-After", "15");

        return Accepted();
    }
}
