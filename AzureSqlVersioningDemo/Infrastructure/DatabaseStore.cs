using System.Collections.Concurrent;

namespace AzureSqlVersioningDemo.Infrastructure;

/// <summary>
/// In-memory store for database resources. All API versions share the same store —
/// the canonical representation includes all properties across all versions.
/// Each version's controller maps to/from this canonical form, exposing only
/// the properties defined in that version.
/// </summary>
public class DatabaseStore
{
    // Key: "{subscriptionId}/{resourceGroupName}/{serverName}/{databaseName}" (case-insensitive)
    private readonly ConcurrentDictionary<string, DatabaseEntity> _databases = new(StringComparer.OrdinalIgnoreCase);

    public static string BuildKey(string subscriptionId, string resourceGroupName, string serverName, string databaseName)
        => $"{subscriptionId}/{resourceGroupName}/{serverName}/{databaseName}";

    public static string BuildResourceId(string subscriptionId, string resourceGroupName, string serverName, string databaseName)
        => $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Sql/servers/{serverName}/databases/{databaseName}";

    public DatabaseEntity? Get(string key) => _databases.GetValueOrDefault(key);

    public DatabaseEntity CreateOrUpdate(string key, DatabaseEntity entity)
    {
        return _databases.AddOrUpdate(key, entity, (_, existing) =>
        {
            // Merge: update only non-null fields from the incoming entity
            existing.Location = entity.Location ?? existing.Location;
            existing.Tags = entity.Tags ?? existing.Tags;
            existing.Collation = entity.Collation ?? existing.Collation;
            existing.MaxSizeBytes = entity.MaxSizeBytes ?? existing.MaxSizeBytes;
            existing.ElasticPoolId = entity.ElasticPoolId ?? existing.ElasticPoolId;
            return existing;
        });
    }

    public DatabaseEntity? Patch(string key, DatabaseEntity patch)
    {
        if (!_databases.TryGetValue(key, out var existing))
            return null;

        // Merge only non-null fields
        if (patch.Location != null) existing.Location = patch.Location;
        if (patch.Tags != null) existing.Tags = patch.Tags;
        if (patch.Collation != null) existing.Collation = patch.Collation;
        if (patch.MaxSizeBytes != null) existing.MaxSizeBytes = patch.MaxSizeBytes;
        if (patch.ElasticPoolId != null) existing.ElasticPoolId = patch.ElasticPoolId;

        return existing;
    }

    public bool Delete(string key) => _databases.TryRemove(key, out _);

    public IEnumerable<DatabaseEntity> List(string subscriptionId, string resourceGroupName, string serverName)
    {
        var prefix = $"{subscriptionId}/{resourceGroupName}/{serverName}/";
        return _databases
            .Where(kv => kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(kv => kv.Value);
    }
}

/// <summary>
/// Canonical database entity — the superset of all properties across all API versions.
/// </summary>
public class DatabaseEntity
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string Type { get; set; } = "Microsoft.Sql/servers/databases";
    public string? Location { get; set; }
    public Dictionary<string, string>? Tags { get; set; }

    // Properties (V1: 2025-11-01)
    public string? Collation { get; set; }
    public long? MaxSizeBytes { get; set; }
    public string? Status { get; set; }
    public DateTimeOffset? CreationDate { get; set; }

    // Properties added in V3 (2026-02-01)
    public string? ElasticPoolId { get; set; }
}
