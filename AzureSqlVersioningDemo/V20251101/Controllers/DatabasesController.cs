using Asp.Versioning;
using AzureSqlVersioningDemo.Infrastructure;
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
    private readonly DatabaseStore _store;

    public DatabasesController(ILogger<DatabasesController> logger, DatabaseStore store)
    {
        _logger = logger;
        _store = store;
    }

    [HttpGet("{databaseName}")]
    public ActionResult<DatabaseResource> Get(
        string subscriptionId, string resourceGroupName, string serverName, string databaseName)
    {
        _logger.LogInformation("GET Database - served by V20251101 controller");

        var key = DatabaseStore.BuildKey(subscriptionId, resourceGroupName, serverName, databaseName);
        var entity = _store.Get(key);
        if (entity == null)
            return NotFound(new { error = new { code = "ResourceNotFound", message = $"Database '{databaseName}' not found." } });

        return Ok(ToResource(entity));
    }

    [HttpPut("{databaseName}")]
    public ActionResult<DatabaseResource> CreateOrUpdate(
        string subscriptionId, string resourceGroupName, string serverName, string databaseName,
        [FromBody] DatabaseResource request)
    {
        _logger.LogInformation("PUT Database - served by V20251101 controller");

        var key = DatabaseStore.BuildKey(subscriptionId, resourceGroupName, serverName, databaseName);
        var isNew = _store.Get(key) == null;

        var entity = _store.CreateOrUpdate(key, new DatabaseEntity
        {
            Id = DatabaseStore.BuildResourceId(subscriptionId, resourceGroupName, serverName, databaseName),
            Name = databaseName,
            Location = request.Location ?? "eastus",
            Tags = request.Tags,
            Collation = request.Properties?.Collation,
            MaxSizeBytes = request.Properties?.MaxSizeBytes,
            Status = isNew ? "Creating" : "Online",
            CreationDate = isNew ? DateTimeOffset.UtcNow : null,
        });

        if (isNew)
            entity.Status = "Online";

        return isNew ? StatusCode(201, ToResource(entity)) : Ok(ToResource(entity));
    }

    [HttpDelete("{databaseName}")]
    public IActionResult Delete(
        string subscriptionId, string resourceGroupName, string serverName, string databaseName)
    {
        _logger.LogInformation("DELETE Database - served by V20251101 controller");

        var key = DatabaseStore.BuildKey(subscriptionId, resourceGroupName, serverName, databaseName);
        if (!_store.Delete(key))
            return NotFound(new { error = new { code = "ResourceNotFound", message = $"Database '{databaseName}' not found." } });

        return Ok();
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
}
