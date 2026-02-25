# Azure SQL API Versioning Demo

A demonstration of how Azure Resource Manager (ARM) API versioning works in ASP.NET Core, featuring two implementations of the same service and a TypeSpec-driven code generation pipeline.

## Overview

This repo explores a key challenge in ARM services: **how to evolve APIs across versions without duplicating unchanged code**. It contains:

- **A TypeSpec API definition** describing a SQL Database resource across three API versions
- **A hand-written service** implementing the API with per-version controllers and models
- **An API-first service** where controllers inherit from abstract bases generated from the TypeSpec
- **A server code emitter** that generates per-version C# controllers and models from TypeSpec
- **A client** that exercises all CRUD operations and version fallback behavior against either service

Both services produce identical behavior — the API-first approach demonstrates how code generation can reduce boilerplate while preserving the same versioning semantics.

## Projects

```
AzureSqlVersioningDemo/          Hand-written service (port 5188)
AzureSqlApiFirstDemo/            API-first service with generated bases (port 5189)
AzureSqlVersioningDemo.Client/   Console client exercising both services
typespec/                        TypeSpec API definition (3 versions)
server-code-emitter/             TypeSpec emitter → C# controllers & models
docs/                            Design documents
```

### [AzureSqlVersioningDemo](AzureSqlVersioningDemo/README.md) — Hand-Written Service

A fully hand-written ASP.NET Core service with per-version controllers and models. Each version folder contains its own controller and model classes. The `VersionFallbackConvention` automatically routes requests for unchanged operations to older controllers.

### [AzureSqlApiFirstDemo](AzureSqlApiFirstDemo/README.md) — API-First Service

Same API, but controllers inherit from abstract base classes generated from the TypeSpec definition. Models are also generated. The MSBuild targets run `tsp compile` before each build, so the generated code stays in sync with the spec. Developers only write the implementation logic in concrete controller overrides.

### [AzureSqlVersioningDemo.Client](AzureSqlVersioningDemo.Client/README.md) — Client

A 10-step console app that creates, reads, updates, patches, lists, and deletes databases across all three API versions, verifying that version fallback works correctly. Works against either service.

### [typespec/](typespec/) — API Definition

A TypeSpec project defining the `Microsoft.Sql/databases` resource using Azure Resource Manager templates. Three versions are declared:

| Version | What Changed |
|---------|-------------|
| `2025-11-01` | Baseline — Create, Get, Delete |
| `2025-12-01` | Adds List and Update (PATCH with `ResourceUpdateModel`) |
| `2026-02-01` | Adds `elasticPoolId` property — impacts Create, Get, Update, List |

### [server-code-emitter/](server-code-emitter/) — TypeSpec Server Emitter

A custom TypeSpec emitter that generates per-version C# code:
- **Abstract controller bases** with route attributes, API version attributes, and method signatures
- **Model classes** with the correct properties for each version (including update models from `ResourceUpdateModel`)
- **Impact analysis** showing which operations changed between versions

## Key Concepts

### Version Fallback Convention

The `VersionFallbackConvention` (`Infrastructure/VersionFallbackConvention.cs`) implements Azure SQL-style version routing:

1. At startup, scans all controllers and maps each **route + HTTP method** to its API version
2. Groups controllers by resource route template
3. For each version, identifies operations that are missing (not implemented in that version's controller)
4. Registers older controllers' actions to handle newer versions for those missing operations

```
GET  /{name}  ?api-version=2025-11-01  →  V1.Get      (direct)
GET  /{name}  ?api-version=2025-12-01  →  V1.Get      (fallback — V2 has no Get)
GET  /{name}  ?api-version=2026-02-01  →  V3.Get      (direct — reimplemented)

PATCH /{name} ?api-version=2025-12-01  →  V2.Update   (direct)
PATCH /{name} ?api-version=2026-02-01  →  V3.Update   (direct — reimplemented)

DELETE /{name} ?api-version=2025-12-01 →  V1.Delete   (fallback)
DELETE /{name} ?api-version=2026-02-01 →  V1.Delete   (fallback)

GET           ?api-version=2025-12-01  →  V2.List     (direct)
GET           ?api-version=2026-02-01  →  V3.List     (direct — reimplemented)
```

This means developers only write code for operations that actually changed in a given version.

### Version Evolution

| Operation | V1 (`2025-11-01`) | V2 (`2025-12-01`) | V3 (`2026-02-01`) |
|---|---|---|---|
| **Create** (PUT) | ✅ implemented | ← fallback to V1 | ✅ reimplemented |
| **Get** (GET `{name}`) | ✅ implemented | ← fallback to V1 | ✅ reimplemented |
| **Delete** (DELETE) | ✅ implemented | ← fallback to V1 | ← fallback to V1 |
| **List** (GET) | — | ✅ new | ✅ reimplemented |
| **Update** (PATCH) | — | ✅ new | ✅ reimplemented |

V3 reimplements List because the response model includes the new `elasticPoolId` property — falling back to V2's List would omit it.

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) (for TypeSpec compilation)

### Quick Start

```bash
# Run the hand-written service
cd AzureSqlVersioningDemo && dotnet run

# Or run the API-first service
cd AzureSqlApiFirstDemo && dotnet run

# Then run the client (defaults to port 5188; pass URL for API-first service)
cd AzureSqlVersioningDemo.Client
dotnet run                              # against hand-written service
dotnet run -- http://localhost:5189     # against API-first service
```

### Building the Emitter

```bash
cd server-code-emitter && npm install && npm run build
cd ../typespec && npm install
```

The API-first service's `.csproj` has MSBuild targets that run `tsp compile` automatically during build.

## Documentation

- [API-First TypeSpec Design](docs/api-first-typespec-azure-sql-design.md) — Design for the TypeSpec API definition
- [Server Code Generation Design](docs/api-first-typespec-server-codegen-design.md) — Design for the server code emitter
