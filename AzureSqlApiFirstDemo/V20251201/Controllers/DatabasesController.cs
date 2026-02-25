using Asp.Versioning;
using AzureSqlApiFirstDemo.Infrastructure;
using AzureSqlApiFirstDemo.V20251201.Models;
using Microsoft.AspNetCore.Mvc;

namespace AzureSqlApiFirstDemo.V20251201.Controllers;

/// <summary>
/// Database controller for API version 2025-12-01.
/// Only implements new operations: List (GET) and Update (PATCH).
/// Create, Get, and Delete fall back to V20251101 via VersionFallbackConvention.
/// </summary>
[ApiController]
[ApiVersion("2025-12-01")]
[Route("subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Sql/databases")]
public class DatabasesController : ControllerBase
{
    private readonly ILogger<DatabasesController> _logger;
    private readonly DatabaseStore _store;

    public DatabasesController(ILogger<DatabasesController> logger, DatabaseStore store)
    {
        _logger = logger;
        _store = store;
    }

    [HttpGet]
    public ActionResult<IEnumerable<DatabaseResource>> List(
        string subscriptionId, string resourceGroupName)
    {
        _logger.LogInformation("LIST Databases - served by V20251201 controller");

        var entities = _store.List(subscriptionId, resourceGroupName);
        var resources = entities.Select(ToResource).ToList();
        return Ok(resources);
    }

    [HttpPatch("{databaseName}")]
    public ActionResult<DatabaseResource> Update(
        string subscriptionId, string resourceGroupName, string databaseName,
        [FromBody] DatabaseResource request)
    {
        _logger.LogInformation("PATCH Database - served by V20251201 controller");

        var key = DatabaseStore.BuildKey(subscriptionId, resourceGroupName, databaseName);
        var entity = _store.Patch(key, new DatabaseEntity
        {
            Id = DatabaseStore.BuildResourceId(subscriptionId, resourceGroupName, databaseName),
            Name = databaseName,
            Location = request.Location,
            Tags = request.Tags,
            Collation = request.Properties?.Collation,
            MaxSizeBytes = request.Properties?.MaxSizeBytes,
        });

        if (entity == null)
            return NotFound(new { error = new { code = "ResourceNotFound", message = $"Database '{databaseName}' not found." } });

        return Ok(ToResource(entity));
    }

    private static DatabaseResource ToResource(DatabaseEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Type = entity.Type,
        Location = entity.Location,
        Tags = entity.Tags,
        Properties = new DatabaseProperties
        {
            Collation = entity.Collation,
            MaxSizeBytes = entity.MaxSizeBytes,
            Status = entity.Status,
            CreationDate = entity.CreationDate,
        }
    };

    // Create (PUT), Get (GET {name}), Delete (DELETE) fall back to V20251101.
}
