# Azure SQL Versioning Demo

A standalone ASP.NET Core 10 project demonstrating how Azure SQL's API version routing works using `Asp.Versioning.Mvc`. It shows how incremental versioning with a **fallback convention** lets you only implement the operations that changed in each version — unchanged operations are automatically served by the previous version's controller.

Each controller response includes a `description` field inside `properties` (e.g. `"Served by V20211101 controller"`) so you can verify exactly which controller handled the request.

## Version Evolution

The demo uses three API versions with a simple, incremental evolution:

| Operation | V1 (`2021-11-01`) | V2 (`2025-08-01`) | V3 (`2025-08-01-preview`) |
|---|---|---|---|
| **Create** (PUT) | ✅ implemented | ← fallback to V1 | ✅ reimplemented (new property) |
| **Get** (GET `{name}`) | ✅ implemented | ← fallback to V1 | ✅ reimplemented (new property) |
| **Delete** (DELETE) | ✅ implemented | ← fallback to V1 | ← fallback to V1 |
| **List** (GET) | — | ✅ new | ← fallback to V2 |
| **Update** (PATCH) | — | ✅ new | ✅ reimplemented (new property) |

**V1 (2021-11-01):** Introduces Create, Get, Delete — the baseline operations.

**V2 (2025-08-01):** Adds List and Update. Since Create, Get, and Delete are unchanged, they fall back to V1's controller automatically.

**V3 (2025-08-01-preview):** Adds an `elasticPoolId` property to `DatabaseProperties`. This impacts Create, Get, and Update (their request/response shapes changed), so those three are reimplemented. Delete falls back to V1; List falls back to V2.

## Project Structure

```
AzureSqlVersioningDemo/
├── Common/
│   └── Models/Database.cs              # Shared base (DatabaseProperties, DatabaseResource)
├── Infrastructure/
│   └── VersionFallbackConvention.cs    # Azure SQL-style version fallback (IControllerConvention)
├── V20211101/                          # API version 2021-11-01
│   ├── Controllers/DatabasesController.cs   # Create, Get, Delete
│   └── Models/Database.cs
├── V20250801/                          # API version 2025-08-01
│   ├── Controllers/DatabasesController.cs   # List, Update (new operations only)
│   └── Models/Database.cs
├── V20250801Preview/                   # API version 2025-08-01-preview
│   ├── Controllers/DatabasesController.cs   # Create, Get, Update (impacted by new property)
│   └── Models/Database.cs                   # Adds ElasticPoolId
├── Program.cs
└── README.md
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (or .NET 9+)

## Getting Started

```bash
cd AzureSqlVersioningDemo
dotnet build
dotnet run
```

The server starts on `http://localhost:5188`.

## Testing Version Routing

### Direct routing — operation exists in requested version

```bash
# Get (V1 — direct)
curl "http://localhost:5188/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Sql/servers/srv1/databases/mydb?api-version=2021-11-01"
```
```json
{
  "properties": {
    "description": "Served by V20211101 controller",
    "collation": "SQL_Latin1_General_CP1_CI_AS",
    "status": "Online"
  }
}
```

### Fallback routing — operation falls back to an earlier version

```bash
# Get with V2 — V20250801 has no Get, falls back to V20211101
curl "http://localhost:5188/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Sql/servers/srv1/databases/mydb?api-version=2025-08-01"
```
```json
{
  "properties": {
    "description": "Served by V20211101 controller",
    "status": "Online"
  }
}
```
Note: The `description` confirms V20211101 handled the request even though V2 was requested.

### New operations added in V2

```bash
# List (new in V2)
curl "http://localhost:5188/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Sql/servers/srv1/databases?api-version=2025-08-01"
```
```json
[{
  "properties": {
    "description": "Served by V20250801 controller",
    "status": "Online"
  }
}]
```

```bash
# Update (new in V2)
curl -X PATCH "http://localhost:5188/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Sql/servers/srv1/databases/mydb?api-version=2025-08-01" \
  -H "Content-Type: application/json" -d '{"properties":{"collation":"Latin1_General_100_CI_AS"}}'
```
```json
{
  "properties": {
    "description": "Served by V20250801 controller",
    "collation": "Latin1_General_100_CI_AS",
    "status": "Online"
  }
}
```

### V3 reimplements impacted operations (new property)

```bash
# Get with V3 — reimplemented, returns elasticPoolId
curl "http://localhost:5188/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Sql/servers/srv1/databases/mydb?api-version=2025-08-01-preview"
```
```json
{
  "properties": {
    "description": "Served by V20250801Preview controller",
    "status": "Online",
    "elasticPoolId": "/subscriptions/sub1/.../elasticPools/pool1"
  }
}
```

### V3 fallback chains

```bash
# Delete with V3 — falls back to V1
curl -X DELETE "http://localhost:5188/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Sql/servers/srv1/databases/mydb?api-version=2025-08-01-preview"
# → { "description": "Served by V20211101 controller" }

# List with V3 — falls back to V2
curl "http://localhost:5188/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Sql/servers/srv1/databases?api-version=2025-08-01-preview"
# → description: "Served by V20250801 controller"
```

## How It Works

### Version Fallback Convention

The `VersionFallbackConvention` (in `Infrastructure/`) implements Azure SQL-style version fallback:

1. At startup, it scans all controllers and maps each **route + HTTP method** to its API version
2. For each version gap (a route+method exists in V1 but not V2), it registers the older controller's action to also handle the newer version
3. This eliminates duplicating unchanged code across versions

```
GET {name} ?api-version=2021-11-01         →  V20211101.Get      (direct)
GET {name} ?api-version=2025-08-01         →  V20211101.Get      (fallback — V2 has no Get)
GET {name} ?api-version=2025-08-01-preview →  V20250801Preview.Get (direct — reimplemented)

GET        ?api-version=2025-08-01         →  V20250801.List     (direct)
GET        ?api-version=2025-08-01-preview →  V20250801.List     (fallback — V3 has no List)

DELETE     ?api-version=2025-08-01         →  V20211101.Delete   (fallback)
DELETE     ?api-version=2025-08-01-preview →  V20211101.Delete   (fallback)
```

**Fallback rules:**
- Stable versions only fall back to stable versions
- Preview versions can fall back to both preview and stable versions
- Uses per-action `MapToApiVersion` to avoid ambiguous route matches

## Adding a New API Version

1. Create a new version directory (e.g., `V20260201/`)
2. **Only implement operations that changed** — everything else falls back automatically
3. Set `Description = "Served by V20260201 controller"` in responses for verification
4. Build and run — the new version is routed automatically
