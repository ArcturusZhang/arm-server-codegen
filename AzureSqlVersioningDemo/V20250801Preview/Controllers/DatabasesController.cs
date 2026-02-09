using Asp.Versioning;
using AzureSqlVersioningDemo.Common.Models;
using Microsoft.AspNetCore.Mvc;

namespace AzureSqlVersioningDemo.V20250801Preview.Controllers;

/// <summary>
/// Database controller for API version 2025-08-01-preview.
/// Implements operations impacted by the new ElasticPoolId property: Create, Get, and Update.
/// Delete falls back to V20211101, List falls back to V20250801.
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

    [HttpGet("{databaseName}")]
    public ActionResult<DatabaseResource> Get(
        string subscriptionId, string resourceGroupName, string serverName, string databaseName)
    {
        _logger.LogInformation("GET Database - served by V20250801Preview controller");

        return Ok(new DatabaseResource
        {
            Id = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Sql/servers/{serverName}/databases/{databaseName}",
            Name = databaseName,
            Type = "Microsoft.Sql/servers/databases",
            Location = "eastus",
            Properties = new V20250801Preview.Models.DatabaseProperties
            {
                Description = "Served by V20250801Preview controller",
                Collation = "SQL_Latin1_General_CP1_CI_AS",
                MaxSizeBytes = 268435456000,
                Status = "Online",
                CreationDate = DateTimeOffset.UtcNow.AddDays(-30),
                ElasticPoolId = "/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Sql/servers/srv1/elasticPools/pool1"
            }
        });
    }

    [HttpPut("{databaseName}")]
    public ActionResult<DatabaseResource> CreateOrUpdate(
        string subscriptionId, string resourceGroupName, string serverName, string databaseName,
        [FromBody] DatabaseResource request)
    {
        _logger.LogInformation("PUT Database - served by V20250801Preview controller");

        var previewProps = request.Properties as V20250801Preview.Models.DatabaseProperties;

        return Ok(new DatabaseResource
        {
            Id = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Sql/servers/{serverName}/databases/{databaseName}",
            Name = databaseName,
            Type = "Microsoft.Sql/servers/databases",
            Location = request.Location ?? "eastus",
            Tags = request.Tags,
            Properties = new V20250801Preview.Models.DatabaseProperties
            {
                Description = "Served by V20250801Preview controller",
                Status = "Creating",
                ElasticPoolId = previewProps?.ElasticPoolId
            }
        });
    }

    [HttpPatch("{databaseName}")]
    public ActionResult<DatabaseResource> Update(
        string subscriptionId, string resourceGroupName, string serverName, string databaseName,
        [FromBody] DatabaseResource request)
    {
        _logger.LogInformation("PATCH Database - served by V20250801Preview controller");

        var previewProps = request.Properties as V20250801Preview.Models.DatabaseProperties;

        return Ok(new DatabaseResource
        {
            Id = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Sql/servers/{serverName}/databases/{databaseName}",
            Name = databaseName,
            Type = "Microsoft.Sql/servers/databases",
            Location = "eastus",
            Properties = new V20250801Preview.Models.DatabaseProperties
            {
                Description = "Served by V20250801Preview controller",
                Collation = request.Properties?.Collation ?? "SQL_Latin1_General_CP1_CI_AS",
                Status = "Online",
                ElasticPoolId = previewProps?.ElasticPoolId ?? "/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Sql/servers/srv1/elasticPools/pool1"
            }
        });
    }

    // Delete (DELETE) falls back to V20211101.
    // List (GET) falls back to V20250801.
}
