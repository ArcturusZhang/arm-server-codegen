# Azure SQL Versioning Demo

A standalone ASP.NET Core 10 project demonstrating how Azure SQL's API version routing mechanism works using the `Asp.Versioning.Mvc` package. It mimics the Azure Resource Manager (ARM) patterns including versioned controllers, version-specific response models, and long-running operations (LRO).

## Project Structure

```
AzureSqlVersioningDemo/
├── Common/
│   └── Models/Database.cs              # Shared base types (DatabaseProperties, SkuInfo, AsyncOperationResult)
├── Infrastructure/
│   └── VersionFallbackConvention.cs    # Azure SQL-style version fallback (IControllerConvention)
├── V20211101/                          # API version 2021-11-01 (Legacy Stable)
│   ├── Controllers/DatabasesController.cs
│   └── Models/Database.cs              # Edition, ServiceObjective (legacy properties)
├── V20250801/                          # API version 2025-08-01 (Current Stable)
│   ├── Controllers/DatabasesController.cs
│   └── Models/Database.cs              # SKU, ZoneRedundant, HighAvailabilityReplicaCount
├── V20250801Preview/                   # API version 2025-08-01-preview
│   ├── Controllers/DatabasesController.cs
│   └── Models/Database.cs              # PreferredEnclaveType, UseFreeLimit (preview features)
├── Program.cs                          # API versioning configuration
├── AzureSqlVersioningDemo.http         # HTTP test file (VS Code REST Client / Visual Studio)
└── README.md
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (or .NET 9+)

## Getting Started

### 1. Build the project

```bash
cd AzureSqlVersioningDemo
dotnet build
```

### 2. Run the project

```bash
dotnet run
```

The server starts on `http://localhost:5188` by default.

### 3. Test with curl

**Get a database using the legacy API version (2021-11-01):**

```bash
curl "http://localhost:5188/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Sql/servers/srv1/databases/mydb?api-version=2021-11-01"
```

Response includes legacy properties (`edition`, `serviceObjective`):
```json
{
  "id": "/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Sql/servers/srv1/databases/mydb",
  "name": "mydb",
  "type": "Microsoft.Sql/servers/databases",
  "location": "eastus",
  "properties": {
    "edition": "Standard",
    "serviceObjective": "S0",
    "status": "Online"
  }
}
```

**Get a database using the current stable version (2025-08-01):**

```bash
curl "http://localhost:5188/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Sql/servers/srv1/databases/mydb?api-version=2025-08-01"
```

Response uses SKU model instead:
```json
{
  "id": "/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Sql/servers/srv1/databases/mydb",
  "name": "mydb",
  "type": "Microsoft.Sql/servers/databases",
  "location": "eastus",
  "sku": { "name": "S0", "tier": "Standard", "capacity": 10 },
  "properties": {
    "zoneRedundant": "Disabled",
    "highAvailabilityReplicaCount": 0,
    "status": "Online"
  }
}
```

**Get a database using the preview version (2025-08-01-preview):**

```bash
curl "http://localhost:5188/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Sql/servers/srv1/databases/mydb?api-version=2025-08-01-preview"
```

Response includes preview-only features:
```json
{
  "id": "/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Sql/servers/srv1/databases/mydb",
  "name": "mydb",
  "type": "Microsoft.Sql/servers/databases",
  "location": "eastus",
  "sku": { "name": "GP_S_Gen5_2", "tier": "GeneralPurpose", "family": "Gen5", "capacity": 2 },
  "properties": {
    "preferredEnclaveType": "VBS",
    "useFreeLimit": true,
    "status": "Online"
  }
}
```

### 4. Test Long-Running Operations (LRO)

**Create a database (returns 202 Accepted):**

```bash
curl -i -X PUT "http://localhost:5188/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Sql/servers/srv1/databases/newdb?api-version=2025-08-01" \
  -H "Content-Type: application/json" \
  -d '{"location":"eastus","sku":{"name":"S0","tier":"Standard"}}'
```

Response headers include polling URLs:
```
HTTP/1.1 202 Accepted
Location: http://localhost:5188/.../operationResults/{operationId}
Azure-AsyncOperation: http://localhost:5188/.../azureAsyncOperation/{operationId}
Retry-After: 15
```

**Poll the operation status:**

```bash
# Check status only (Azure-AsyncOperation URL)
curl "http://localhost:5188/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Sql/servers/srv1/databases/newdb/azureAsyncOperation/{operationId}?api-version=2025-08-01"

# Get completed resource (Location URL)
curl "http://localhost:5188/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Sql/servers/srv1/databases/newdb/operationResults/{operationId}?api-version=2025-08-01"
```

### 5. Test with the .http file

Open `AzureSqlVersioningDemo.http` in Visual Studio or VS Code (with REST Client extension) to run pre-built requests for all versions.

### 6. Request an unsupported version

```bash
curl "http://localhost:5188/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Sql/servers/srv1/databases/mydb?api-version=2019-01-01"
```

Returns `400 Bad Request` — the requested API version is not supported.

## How API Versioning Works

The versioning is configured in `Program.cs`:

```csharp
builder.Services.AddApiVersioning(options =>
{
    options.AssumeDefaultVersionWhenUnspecified = false;   // Clients must always specify ?api-version=
    options.ReportApiVersions = true;                     // Response headers list supported versions
    options.ApiVersionReader = new QueryStringApiVersionReader("api-version");  // Read from query string
})
.AddMvc();
```

Each controller declares which version it handles:

```csharp
[ApiVersion("2025-08-01")]
[Route("subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Sql/servers/{serverName}/databases")]
public class DatabasesController : ControllerBase { ... }
```

When a request arrives with `?api-version=2025-08-01`, ASP.NET Core's endpoint routing matches it to the controller decorated with `[ApiVersion("2025-08-01")]`.

### Version Fallback Convention

The `VersionFallbackConvention` (in `Infrastructure/`) implements Azure SQL-style version fallback. It eliminates the need to duplicate unchanged actions across version controllers.

**How it works:**

At startup, the convention scans all controllers and their actions. For each route template + HTTP method combination, it finds which API versions are explicitly supported. If a newer version doesn't have an action that an older version does (same route + method), the older controller's action is automatically registered to also handle the newer version.

**Example:** V20211101 has `GET`, `PUT`, and `DELETE`. V20250801 only has `GET` and `PUT` (because `DELETE` hasn't changed). The convention automatically makes V20211101's `DELETE` also respond to `?api-version=2025-08-01`.

```
DELETE ?api-version=2021-11-01  →  V20211101.DELETE (direct)
DELETE ?api-version=2025-08-01  →  V20211101.DELETE (fallback — V20250801 has no DELETE)
GET    ?api-version=2025-08-01  →  V20250801.GET    (direct — no fallback needed)
```

**Rules:**
- Stable versions only fall back to stable versions
- Preview versions can fall back to both preview and stable versions
- The convention uses per-action `MapToApiVersion` to avoid ambiguous route matches

This is registered in `Program.cs` via the versioning library's conventions API:

```csharp
.AddMvc(options =>
{
    options.Conventions.Add(new VersionFallbackConvention());
})
```

## API Versions

| Version | Type | Key Differences |
|---------|------|-----------------|
| `2021-11-01` | Stable (Legacy) | Uses `Edition` and `ServiceObjective` properties |
| `2025-08-01` | Stable (Current) | Uses `SKU` model, adds `ZoneRedundant` and `HighAvailabilityReplicaCount` |
| `2025-08-01-preview` | Preview | Adds `PreferredEnclaveType` and `UseFreeLimit` preview features |

## Adding a New API Version

1. Create a new version directory: `V20260201/`
2. Add `Controllers/` and `Models/` subdirectories
3. Define version-specific models inheriting from `Common.Models.DatabaseProperties`
4. Create a controller with `[ApiVersion("2026-02-01")]`
5. **Only implement actions that have changed** — unchanged actions automatically fall back to the previous version's controller via `VersionFallbackConvention`
6. Build and run — the new version is automatically routed
