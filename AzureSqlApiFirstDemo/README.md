# Azure SQL Versioning Demo

A standalone ASP.NET Core 10 project demonstrating how Azure SQL's API version routing works using `Asp.Versioning.Mvc`. It shows how incremental versioning with a **fallback convention** lets you only implement the operations that changed in each version — unchanged operations are automatically served by the previous version's controller.

All controllers share an in-memory `DatabaseStore` — a `ConcurrentDictionary`-backed singleton — so CRUD operations work across versions with real state. A database created via V1 is visible via V3 (with the additional `ElasticPoolId` field), and vice versa. Each version's controller exposes only the properties defined in that version's model.

## How It Works

### Version Fallback Convention

The `VersionFallbackConvention` (in `Infrastructure/`) implements Azure SQL-style version fallback:

1. At startup, it scans all controllers and maps each **route + HTTP method** to its API version
2. For each version gap (a route+method exists in V1 but not V2), it registers the older controller's action to also handle the newer version
3. This eliminates duplicating unchanged code across versions

```
GET {name} ?api-version=2025-11-01  →  V20251101.Get    (direct)
GET {name} ?api-version=2025-12-01  →  V20251101.Get    (fallback — V2 has no Get)
GET {name} ?api-version=2026-02-01  →  V20260201.Get    (direct — reimplemented)

GET        ?api-version=2025-12-01  →  V20251201.List   (direct)
GET        ?api-version=2026-02-01  →  V20260201.List   (direct — reimplemented)

DELETE     ?api-version=2025-12-01  →  V20251101.Delete (fallback)
DELETE     ?api-version=2026-02-01  →  V20251101.Delete (fallback)
```

**Fallback rules:**
- Stable versions only fall back to stable versions
- Preview versions can fall back to both preview and stable versions
- Uses per-action `MapToApiVersion` to avoid ambiguous route matches

### In-Memory Database Store

The `DatabaseStore` (in `Infrastructure/`) provides:
- **Shared state** — all versions read/write the same `ConcurrentDictionary<string, DatabaseEntity>`
- **Canonical entity** — `DatabaseEntity` is the superset of all properties across all versions
- **Version-specific views** — each controller's `ToResource()` maps to its version's model, omitting properties not defined in that version
- **CRUD operations** — PUT (create/update, 201/200), GET (200/404), PATCH (merge non-null fields, 404), DELETE (200/404), LIST

### Version Evolution

The demo uses three API versions with a simple, incremental evolution:

| Operation | V1 (`2025-11-01`) | V2 (`2025-12-01`) | V3 (`2026-02-01`) |
|---|---|---|---|
| **Create** (PUT) | ✅ implemented | ← fallback to V1 | ✅ reimplemented (new property) |
| **Get** (GET `{name}`) | ✅ implemented | ← fallback to V1 | ✅ reimplemented (new property) |
| **Delete** (DELETE) | ✅ implemented | ← fallback to V1 | ← fallback to V1 |
| **List** (GET) | — | ✅ new | ✅ reimplemented (new property) |
| **Update** (PATCH) | — | ✅ new | ✅ reimplemented (new property) |

**V1 (2025-11-01):** Introduces Create, Get, Delete — the baseline operations.

**V2 (2025-12-01):** Adds List and Update. Since Create, Get, and Delete are unchanged, they fall back to V1's controller automatically.

**V3 (2026-02-01):** Adds an `elasticPoolId` property to `DatabaseProperties`. This impacts Create, Get, Update, and List (their request/response shapes changed), so those four are reimplemented. Delete falls back to V1.

## Project Structure

```
AzureSqlApiFirstDemo/
├── Infrastructure/
│   ├── DatabaseStore.cs                     # In-memory store (ConcurrentDictionary singleton)
│   └── VersionFallbackConvention.cs         # Azure SQL-style version fallback (IControllerConvention)
├── V20251101/                               # API version 2025-11-01
│   ├── Controllers/DatabasesController.cs   # Create, Get, Delete
│   └── Models/Database.cs
├── V20251201/                               # API version 2025-12-01
│   ├── Controllers/DatabasesController.cs   # List, Update (new operations only)
│   └── Models/Database.cs
├── V20260201/                               # API version 2026-02-01
│   ├── Controllers/DatabasesController.cs   # Create, Get, Update, List (impacted by new property)
│   └── Models/Database.cs                   # Adds ElasticPoolId
├── Program.cs
└── README.md
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (or .NET 9+)

## Getting Started

```bash
cd AzureSqlApiFirstDemo
dotnet build
dotnet run
```

The server starts on `http://localhost:5188`.

## Testing

Use the companion client project (`AzureSqlApiFirstDemo.Client`) to exercise all CRUD operations across versions. See the [client README](../AzureSqlApiFirstDemo.Client/README.md) for details.

Or test manually with curl:

```bash
# Create a database via V1
curl -X PUT "http://localhost:5188/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Sql/databases/mydb?api-version=2025-11-01" \
  -H "Content-Type: application/json" \
  -d '{"location":"eastus","properties":{"collation":"SQL_Latin1_General_CP1_CI_AS","maxSizeBytes":268435456000}}'

# Get via V1 (no ElasticPoolId)
curl "http://localhost:5188/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Sql/databases/mydb?api-version=2025-11-01"

# Get via V3 (includes ElasticPoolId field)
curl "http://localhost:5188/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Sql/databases/mydb?api-version=2026-02-01"

# List via V2
curl "http://localhost:5188/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Sql/databases?api-version=2025-12-01"

# Patch via V3 (set ElasticPoolId)
curl -X PATCH "http://localhost:5188/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Sql/databases/mydb?api-version=2026-02-01" \
  -H "Content-Type: application/json" \
  -d '{"properties":{"elasticPoolId":"/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Sql/elasticPools/pool1"}}'

# Delete
curl -X DELETE "http://localhost:5188/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Sql/databases/mydb?api-version=2025-11-01"
```
