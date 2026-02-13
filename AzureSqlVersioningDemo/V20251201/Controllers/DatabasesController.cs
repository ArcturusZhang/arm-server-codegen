using Asp.Versioning;
using AzureSqlVersioningDemo.V20251201.Models;
using Microsoft.AspNetCore.Mvc;

namespace AzureSqlVersioningDemo.V20251201.Controllers;

/// <summary>
/// Database controller for API version 2025-12-01.
/// Only implements new operations: List (GET) and Update (PATCH).
/// Create, Get, and Delete fall back to V20251101 via VersionFallbackConvention.
/// </summary>
[ApiController]
[ApiVersion("2025-12-01")]
[Route("subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Sql/servers/{serverName}/databases")]
public class DatabasesController : ControllerBase
{
    private readonly ILogger<DatabasesController> _logger;

    public DatabasesController(ILogger<DatabasesController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public ActionResult<IEnumerable<DatabaseResource>> List(
        string subscriptionId, string resourceGroupName, string serverName)
    {
        _logger.LogInformation("LIST Databases - served by V20251201 controller");

        return Ok(new[]
        {
            new DatabaseResource
            {
                Id = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Sql/servers/{serverName}/databases/db1",
                Name = "db1",
                Type = "Microsoft.Sql/servers/databases",
                Location = "eastus",
                Properties = new DatabaseProperties
                {
                    Description = "Served by V20251201 controller",
                    Status = "Online"
                }
            }
        });
    }

    [HttpPatch("{databaseName}")]
    public ActionResult<DatabaseResource> Update(
        string subscriptionId, string resourceGroupName, string serverName, string databaseName,
        [FromBody] DatabaseResource request)
    {
        _logger.LogInformation("PATCH Database - served by V20251201 controller");

        return Ok(new DatabaseResource
        {
            Id = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Sql/servers/{serverName}/databases/{databaseName}",
            Name = databaseName,
            Type = "Microsoft.Sql/servers/databases",
            Location = "eastus",
            Properties = new DatabaseProperties
            {
                Description = "Served by V20251201 controller",
                Collation = request.Properties?.Collation ?? "SQL_Latin1_General_CP1_CI_AS",
                Status = "Online"
            }
        });
    }

    // Create (PUT), Get (GET {name}), Delete (DELETE) fall back to V20251101.
}
