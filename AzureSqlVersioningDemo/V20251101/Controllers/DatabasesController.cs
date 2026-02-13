using Asp.Versioning;
using AzureSqlVersioningDemo.V20251101.Models;
using Microsoft.AspNetCore.Mvc;

namespace AzureSqlVersioningDemo.V20251101.Controllers;

/// <summary>
/// Database controller for API version 2025-11-01.
/// Supports: Create (PUT), Get (GET {name}), Delete (DELETE).
/// </summary>
[ApiController]
[ApiVersion("2025-11-01")]
[Route("subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Sql/servers/{serverName}/databases")]
public class DatabasesController : ControllerBase
{
    private readonly ILogger<DatabasesController> _logger;

    public DatabasesController(ILogger<DatabasesController> logger)
    {
        _logger = logger;
    }

    [HttpGet("{databaseName}")]
    public ActionResult<DatabaseResource> Get(
        string subscriptionId, string resourceGroupName, string serverName, string databaseName)
    {
        _logger.LogInformation("GET Database - served by V20251101 controller");

        return Ok(new DatabaseResource
        {
            Id = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Sql/servers/{serverName}/databases/{databaseName}",
            Name = databaseName,
            Type = "Microsoft.Sql/servers/databases",
            Location = "eastus",
            Properties = new DatabaseProperties
            {
                Description = "Served by V20251101 controller",
                Collation = "SQL_Latin1_General_CP1_CI_AS",
                MaxSizeBytes = 268435456000,
                Status = "Online",
                CreationDate = DateTimeOffset.UtcNow.AddDays(-30)
            }
        });
    }

    [HttpPut("{databaseName}")]
    public ActionResult<DatabaseResource> CreateOrUpdate(
        string subscriptionId, string resourceGroupName, string serverName, string databaseName,
        [FromBody] DatabaseResource request)
    {
        _logger.LogInformation("PUT Database - served by V20251101 controller");

        return Ok(new DatabaseResource
        {
            Id = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Sql/servers/{serverName}/databases/{databaseName}",
            Name = databaseName,
            Type = "Microsoft.Sql/servers/databases",
            Location = request.Location ?? "eastus",
            Tags = request.Tags,
            Properties = new DatabaseProperties
            {
                Description = "Served by V20251101 controller",
                Status = "Creating"
            }
        });
    }

    [HttpDelete("{databaseName}")]
    public IActionResult Delete(
        string subscriptionId, string resourceGroupName, string serverName, string databaseName)
    {
        _logger.LogInformation("DELETE Database - served by V20251101 controller");
        return Ok(new { description = "Served by V20251101 controller" });
    }
}
