using Asp.Versioning;
using AzureSqlApiFirstDemo.Infrastructure;
using AzureSqlApiFirstDemo.V20260201.Models;
using Microsoft.AspNetCore.Mvc;

namespace AzureSqlApiFirstDemo.V20260201.Controllers;

/// <summary>
/// Database controller for API version 2026-02-01.
/// Implements operations impacted by the new ElasticPoolId property: Create, Get, and Update.
/// Delete falls back to V20251101, List falls back to V20251201.
/// </summary>
[ApiController]
[ApiVersion("2026-02-01")]
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

    [HttpGet("{databaseName}")]
    public ActionResult<DatabaseResource> Get(
        string subscriptionId, string resourceGroupName, string databaseName)
    {
        _logger.LogInformation("GET Database - served by V20260201 controller");

        var key = DatabaseStore.BuildKey(subscriptionId, resourceGroupName, databaseName);
        var entity = _store.Get(key);
        if (entity == null)
            return NotFound(new { error = new { code = "ResourceNotFound", message = $"Database '{databaseName}' not found." } });

        return Ok(ToResource(entity));
    }

    [HttpPut("{databaseName}")]
    public ActionResult<DatabaseResource> CreateOrUpdate(
        string subscriptionId, string resourceGroupName, string databaseName,
        [FromBody] DatabaseResource request)
    {
        _logger.LogInformation("PUT Database - served by V20260201 controller");

        var key = DatabaseStore.BuildKey(subscriptionId, resourceGroupName, databaseName);
        var isNew = _store.Get(key) == null;

        var entity = _store.CreateOrUpdate(key, new DatabaseEntity
        {
            Id = DatabaseStore.BuildResourceId(subscriptionId, resourceGroupName, databaseName),
            Name = databaseName,
            Location = request.Location ?? "eastus",
            Tags = request.Tags,
            Collation = request.Properties?.Collation,
            MaxSizeBytes = request.Properties?.MaxSizeBytes,
            ElasticPoolId = request.Properties?.ElasticPoolId,
            Status = isNew ? "Creating" : "Online",
            CreationDate = isNew ? DateTimeOffset.UtcNow : null,
        });

        if (isNew)
            entity.Status = "Online";

        return isNew ? StatusCode(201, ToResource(entity)) : Ok(ToResource(entity));
    }

    [HttpPatch("{databaseName}")]
    public ActionResult<DatabaseResource> Update(
        string subscriptionId, string resourceGroupName, string databaseName,
        [FromBody] DatabaseResource request)
    {
        _logger.LogInformation("PATCH Database - served by V20260201 controller");

        var key = DatabaseStore.BuildKey(subscriptionId, resourceGroupName, databaseName);
        var entity = _store.Patch(key, new DatabaseEntity
        {
            Id = DatabaseStore.BuildResourceId(subscriptionId, resourceGroupName, databaseName),
            Name = databaseName,
            Location = request.Location,
            Tags = request.Tags,
            Collation = request.Properties?.Collation,
            MaxSizeBytes = request.Properties?.MaxSizeBytes,
            ElasticPoolId = request.Properties?.ElasticPoolId,
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
            ElasticPoolId = entity.ElasticPoolId,
        }
    };

    // Delete (DELETE) falls back to V20251101.
    // List (GET) falls back to V20251201.
}
