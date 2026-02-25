using AzureSqlApiFirstDemo.Infrastructure;
using Asp.Versioning;
using Generated.V20251101.Controllers;
using Generated.V20251101.Models;
using Microsoft.AspNetCore.Mvc;

namespace AzureSqlApiFirstDemo.V20251101.Controllers;

/// <summary>
/// Database controller for API version 2025-11-01.
/// Supports: Create (PUT), Get (GET {name}), Delete (DELETE).
/// </summary>
[ApiVersion("2025-11-01")]
public class DatabasesController : DatabasesControllerBase
{
    private readonly ILogger<DatabasesController> _logger;
    private readonly DatabaseStore _store;

    public DatabasesController(ILogger<DatabasesController> logger, DatabaseStore store)
    {
        _logger = logger;
        _store = store;
    }

    public override Task<IActionResult> Get(
        string subscriptionId, string resourceGroupName, string databaseName, CancellationToken cancellationToken)
    {
        _logger.LogInformation("GET Database - served by V20251101 controller");

        var key = DatabaseStore.BuildKey(subscriptionId, resourceGroupName, databaseName);
        var entity = _store.Get(key);
        if (entity == null)
            return Task.FromResult<IActionResult>(NotFound(new { error = new { code = "ResourceNotFound", message = $"Database '{databaseName}' not found." } }));

        return Task.FromResult<IActionResult>(Ok(ToResource(entity)));
    }

    public override Task<IActionResult> CreateOrUpdate(
        string subscriptionId, string resourceGroupName, string databaseName,
        Database body, CancellationToken cancellationToken)
    {
        _logger.LogInformation("PUT Database - served by V20251101 controller");

        var key = DatabaseStore.BuildKey(subscriptionId, resourceGroupName, databaseName);
        var isNew = _store.Get(key) == null;

        var entity = _store.CreateOrUpdate(key, new DatabaseEntity
        {
            Id = DatabaseStore.BuildResourceId(subscriptionId, resourceGroupName, databaseName),
            Name = databaseName,
            Location = body.Location,
            Tags = body.Tags?.ToDictionary(kv => kv.Key, kv => kv.Value),
            Collation = body.Properties?.Collation,
            MaxSizeBytes = body.Properties?.MaxSizeBytes,
            Status = isNew ? "Creating" : "Online",
            CreationDate = isNew ? DateTimeOffset.UtcNow : null,
        });

        if (isNew)
            entity.Status = "Online";

        return Task.FromResult<IActionResult>(isNew ? StatusCode(201, ToResource(entity)) : Ok(ToResource(entity)));
    }

    public override Task<IActionResult> Delete(
        string subscriptionId, string resourceGroupName, string databaseName, CancellationToken cancellationToken)
    {
        _logger.LogInformation("DELETE Database - served by V20251101 controller");

        var key = DatabaseStore.BuildKey(subscriptionId, resourceGroupName, databaseName);
        if (!_store.Delete(key))
            return Task.FromResult<IActionResult>(NotFound(new { error = new { code = "ResourceNotFound", message = $"Database '{databaseName}' not found." } }));

        return Task.FromResult<IActionResult>(Ok());
    }

    private static Database ToResource(DatabaseEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Type = entity.Type,
        Location = entity.Location ?? "eastus",
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
