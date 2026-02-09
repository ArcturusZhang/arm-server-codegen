using Asp.Versioning;
using AzureSqlVersioningDemo.Common.Models;
using AzureSqlVersioningDemo.V20211101.Models;
using Microsoft.AspNetCore.Mvc;

namespace AzureSqlVersioningDemo.V20211101.Controllers;

/// <summary>
/// Database controller for API version 2021-11-01.
/// This is a legacy version that uses Edition/ServiceObjective instead of SKU.
/// </summary>
[ApiController]
[ApiVersion("2021-11-01")]
[Route("subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Sql/servers/{serverName}/databases")]
public class DatabasesController : ControllerBase
{
    private readonly ILogger<DatabasesController> _logger;

    public DatabasesController(ILogger<DatabasesController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets a database (V2021-11-01 format with Edition/ServiceObjective).
    /// </summary>
    [HttpGet("{databaseName}")]
    public ActionResult<DatabaseResource> Get(
        string subscriptionId,
        string resourceGroupName,
        string serverName,
        string databaseName)
    {
        _logger.LogInformation("GET Database called with API version 2021-11-01");

        return Ok(new DatabaseResource
        {
            Id = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Sql/servers/{serverName}/databases/{databaseName}",
            Name = databaseName,
            Type = "Microsoft.Sql/servers/databases",
            Location = "eastus",
            Properties = new Models.DatabaseProperties
            {
                Description = "Served by V20211101 controller",
                Collation = "SQL_Latin1_General_CP1_CI_AS",
                MaxSizeBytes = 268435456000,
                Status = "Online",
                CreationDate = DateTimeOffset.UtcNow.AddDays(-30),
                Edition = "Standard",
                ServiceObjective = "S0"
            }
        });
    }

    /// <summary>
    /// Lists all databases on a server (V2021-11-01 format).
    /// </summary>
    [HttpGet]
    public ActionResult<IEnumerable<DatabaseResource>> List(
        string subscriptionId,
        string resourceGroupName,
        string serverName)
    {
        _logger.LogInformation("LIST Databases called with API version 2021-11-01");

        return Ok(new[]
        {
            new DatabaseResource
            {
                Id = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Sql/servers/{serverName}/databases/db1",
                Name = "db1",
                Type = "Microsoft.Sql/servers/databases",
                Location = "eastus",
                Properties = new Models.DatabaseProperties
                {
                    Description = "Served by V20211101 controller",
                    Status = "Online",
                    Edition = "Standard",
                    ServiceObjective = "S0"
                }
            }
        });
    }

    /// <summary>
    /// Creates or updates a database (V2021-11-01).
    /// </summary>
    [HttpPut("{databaseName}")]
    public ActionResult<DatabaseResource> CreateOrUpdate(
        string subscriptionId,
        string resourceGroupName,
        string serverName,
        string databaseName,
        [FromBody] DatabaseCreateOrUpdateRequest request)
    {
        _logger.LogInformation("PUT Database called with API version 2021-11-01");

        return Ok(new DatabaseResource
        {
            Id = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Sql/servers/{serverName}/databases/{databaseName}",
            Name = databaseName,
            Type = "Microsoft.Sql/servers/databases",
            Location = request.Location ?? "eastus",
            Tags = request.Tags,
            Properties = new Models.DatabaseProperties
            {
                Description = "Served by V20211101 controller",
                Status = "Creating",
                Edition = "Standard",
                ServiceObjective = "S0"
            }
        });
    }

    /// <summary>
    /// Deletes a database (V2021-11-01).
    /// </summary>
    [HttpDelete("{databaseName}")]
    public IActionResult Delete(
        string subscriptionId,
        string resourceGroupName,
        string serverName,
        string databaseName)
    {
        _logger.LogInformation("DELETE Database called with API version 2021-11-01");
        return Ok();
    }
}
