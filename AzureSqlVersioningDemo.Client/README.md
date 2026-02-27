# Azure SQL Versioning Demo — Client

A console application that exercises either the [AzureSqlVersioningDemo](../AzureSqlVersioningDemo/README.md) (hand-written) service or the [AzureSqlApiFirstDemo](../AzureSqlApiFirstDemo/README.md) (API-first, code-generated) service, demonstrating full CRUD operations across all three API versions and verifying version fallback behavior.

Both services expose the same API — the client works identically against either one.

## What It Does

The client runs 10 sequential steps that demonstrate:

| Step | Operation | API Version | What It Shows |
|------|-----------|-------------|---------------|
| 1 | **PUT** × 2 | V1 | Create two databases |
| 2 | **GET** | V1 | Retrieve — no `elasticPoolId` in response |
| 3 | **GET** | V2 | Same database — falls back to V1 controller |
| 4 | **GET** | V3 | Same database — `elasticPoolId` field present (null) |
| 5 | **GET** (list) | V2 | List all databases (2 results) |
| 6 | **PATCH** | V3 | Set `elasticPoolId` on mydb |
| 7 | **GET** | V1 | Same database after V3 patch — `elasticPoolId` not in response |
| 8 | **DELETE** | V3 | Delete testdb — falls back to V1 controller |
| 9 | **GET** | V1 | Get deleted database — 404 |
| 10 | **GET** (list) | V3 | List all — only mydb remains |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (or .NET 9+)
- One of the service projects running

## Running

### Against the hand-written service (port 5188)

```bash
cd AzureSqlVersioningDemo
dotnet run
```

Then in another terminal:

```bash
cd AzureSqlVersioningDemo.Client
dotnet run
```

### Against the API-first service (port 5189)

```bash
cd AzureSqlApiFirstDemo
dotnet run
```

Then in another terminal:

```bash
cd AzureSqlVersioningDemo.Client
dotnet run -- http://localhost:5189
```

## Service Differences

| | AzureSqlVersioningDemo | AzureSqlApiFirstDemo |
|---|---|---|
| **Port** | 5188 | 5189 |
| **Controllers** | Hand-written | Inherit from generated abstract bases |
| **Models** | Hand-written per-version models | Generated from TypeSpec |
| **PATCH body** | `DatabaseUpdate` (hand-written) | `ResourceUpdateModel` (generated) |
| **Routes** | Class-level `[Route]` + relative `[HttpGet]` | Per-method absolute `[Route]` |
| **Behavior** | Identical | Identical |

## Expected Output

```
=== Azure SQL Versioning Demo Client ===

── Step 1: Create two databases via V1 (2025-11-01) ──

  Created mydb:
  {
    "name": "mydb",
    "location": "eastus",
    "properties": { "collation": "SQL_Latin1_General_CP1_CI_AS", ... }
  }

  Created testdb:
  {
    "name": "testdb",
    "location": "westus",
    ...
  }

── Step 5: List databases via V2 ──

  Found 2 database(s):
    - mydb (eastus)
    - testdb (westus)

── Step 6: Patch mydb via V3 (set ElasticPoolId) ──

  PATCH mydb (V3):
  {
    ...,
    "properties": { ..., "elasticPoolId": "/.../elasticPools/pool1" }
  }

── Step 7: Get mydb via V1 after V3 patch (no ElasticPoolId) ──

  GET mydb (V1 after V3 patch):
  {
    ...,
    "properties": { "collation": "...", "status": "Online" }
  }
  // Note: no elasticPoolId — V1 model doesn't include it

── Step 9: Get deleted testdb — expect 404 ──

  GET testdb → 404 NotFound

── Step 10: List via V3 — only mydb remains ──

  Found 1 database(s):
    - mydb (eastus)

=== Done ===
```
