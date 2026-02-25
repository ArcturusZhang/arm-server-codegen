using AzureSqlApiFirstDemo.Infrastructure;
using Asp.Versioning;
using Generated.V20251201.Controllers;
using Generated.V20251201.Models;
using Microsoft.AspNetCore.Mvc;

namespace AzureSqlApiFirstDemo.V20251201.Controllers;

/// <summary>
/// Database controller for API version 2025-12-01.
/// Only implements new operations: List (GET) and Update (PATCH).
/// Create, Get, and Delete fall back to V20251101 via VersionFallbackConvention.
/// </summary>
[ApiVersion("2025-12-01")]
public class DatabasesController : DatabasesControllerBase
{
    private readonly ILogger<DatabasesController> _logger;
    private readonly DatabaseStore _store;

    public DatabasesController(ILogger<DatabasesController> logger, DatabaseStore store)
    {
        _logger = logger;
        _store = store;
    }

    public override Task<IActionResult> ListByResourceGroup(
        string subscriptionId, string resourceGroupName, CancellationToken cancellationToken)
    {
        _logger.LogInformation("LIST Databases - served by V20251201 controller");

        var entities = _store.List(subscriptionId, resourceGroupName);
        var resources = entities.Select(ToResource).ToList();
        return Task.FromResult<IActionResult>(Ok(resources));
    }

    public override Task<IActionResult> Update(
        string subscriptionId, string resourceGroupName, string databaseName,
        Database body, CancellationToken cancellationToken)
    {
        _logger.LogInformation("PATCH Database - served by V20251201 controller");

        var key = DatabaseStore.BuildKey(subscriptionId, resourceGroupName, databaseName);
        var entity = _store.Patch(key, new DatabaseEntity
        {
            Id = DatabaseStore.BuildResourceId(subscriptionId, resourceGroupName, databaseName),
            Name = databaseName,
            Location = body.Location,
            Tags = body.Tags?.ToDictionary(kv => kv.Key, kv => kv.Value),
            Collation = body.Properties?.Collation,
            MaxSizeBytes = body.Properties?.MaxSizeBytes,
        });

        if (entity == null)
            return Task.FromResult<IActionResult>(NotFound(new { error = new { code = "ResourceNotFound", message = $"Database '{databaseName}' not found." } }));

        return Task.FromResult<IActionResult>(Ok(ToResource(entity)));
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

    // Create (PUT), Get (GET {name}), Delete (DELETE) fall back to V20251101.
}
