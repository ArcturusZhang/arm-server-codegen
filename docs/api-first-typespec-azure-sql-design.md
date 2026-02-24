# API-First Development with TypeSpec: Design for Azure SQL Resource Provider

## Introduction

This document describes how the Azure SQL resource provider team can adopt the API-first development approach using TypeSpec and automated server code generation. It builds on the principles established in the [general design document](./api-first-typespec-server-codegen-design.md) — including the incremental versioning pattern, automatic impact analysis, and generated vs. custom code separation — and maps them to SQL's specific versioning infrastructure.

For the foundational concepts, see the [general design document](./api-first-typespec-server-codegen-design.md). This document focuses on:

- How SQL's existing versioning infrastructure works today
- How the general API-first approach maps to SQL's specific components
- Where SQL's patterns differ from the general case
- What refactoring is needed to adopt the approach

---

## How Azure SQL Handles Versioning Today

Azure SQL follows the incremental versioning pattern described in the general design document, with several SQL-specific components.

### ApiVersion Enum

All supported API versions are defined as a C# enum in `ApiVersion.cs`. Each enum value carries `[ApiVersion]` attributes that map the version string to a **category** (Preview or Stable):

```csharp
public enum ApiVersion : int
{
    [ApiVersion("2026-02-01-preview", Category = ArmResourceProviderApiVersionCategory.Preview)]
    [ApiVersion("2026-02-01", Category = ArmResourceProviderApiVersionCategory.Stable)]
    V20260201 = 25,

    [ApiVersion("2025-08-01-preview", Category = ArmResourceProviderApiVersionCategory.Preview)]
    [ApiVersion("2025-08-01", Category = ArmResourceProviderApiVersionCategory.Stable)]
    V20250801 = 24,

    [ApiVersion("2025-02-01-preview", Category = ArmResourceProviderApiVersionCategory.Preview)]
    [ApiVersion("2025-02-01", Category = ArmResourceProviderApiVersionCategory.Stable)]
    V20250201 = 23,

    // ... older versions
}
```

The integer values establish an ordering — higher values are newer versions.

### Preview / Stable Categories

Each API version string belongs to a category:

| Category | Example version string | Description |
|----------|----------------------|-------------|
| **Preview** | `2025-08-01-preview` | Preview/pre-release API |
| **Stable** | `2025-08-01` | Stable/GA API |

The same enum value (e.g., `V20250801`) can serve both a preview and a stable version string. Categories are important because the routing framework **only matches within the same category** — a preview request never falls back to a stable controller, and vice versa.

### Controller Attributes

Controllers declare which versions they handle using two SQL-team-defined attributes:

| Attribute | Purpose |
|-----------|---------|
| `[VersionedRoute]` | Defines the route template the controller handles (e.g., the ARM resource path) |
| `[BaseApiVersion]` | Declares which API version and category this controller supports |

Example:

```csharp
[VersionedRoute(ArmResourceProviderRouteConstants.DatabasesRouteTemplate)]
[BaseApiVersion(ApiVersion.V20250801, ArmResourceProviderApiVersionCategory.Preview)]
[BaseApiVersion(ApiVersion.V20250801, ArmResourceProviderApiVersionCategory.Stable)]
public class Database : BaseDatabase<DatabaseResource>
{
    // Only the operations that changed in this version
}
```

### Version Fallback Algorithm

The core routing logic finds the correct controller using `VersionRouteSelectorHelper.GetControllerForVersion()`:

```csharp
public static T GetControllerForVersion<T>(ApiVersionInfo requestedApiVersion, ICollection<T> controllers)
    where T : IVersionedRouteEntry
{
    return controllers
        .Where(candidate =>
            // 1. Same category (Preview or Stable)
            candidate.Category == requestedApiVersion.Category
            // 2. Requested version >= controller's base version
            && EnumComparer.Compare(requestedApiVersion.EnumValue, candidate.ApiVersionEnum) >= 0
            // 3. No max version OR requested version <= controller's max version
            && (candidate.MaxApiVersionEnum == null
                || EnumComparer.Compare(requestedApiVersion.EnumValue, candidate.MaxApiVersionEnum) <= 0))
        .FirstOrDefault();  // Controllers sorted by version descending — picks the newest match
}
```

This is the same fallback algorithm described in the general design document, with SQL-specific additions: **category filtering** (Preview vs. Stable) and an optional **max version bound**.

### Request Flow

```
HTTP Request: PUT /subscriptions/.../databases/mydb?api-version=2026-02-01-preview
     │
     ▼
┌─────────────────────────────────────────────────────────────────────┐
│ ASP.NET Web API Pipeline                                            │
├─────────────────────────────────────────────────────────────────────┤
│ 1. Route matching → matches DatabasesRouteTemplate                  │
│ 2. ApiVersionControllerSelector.SelectController(request)           │
│    ├── Extract "api-version" from query string                      │
│    ├── Parse → ApiVersionInfo { EnumValue: V20260201,               │
│    │                            Category: "Preview" }               │
│    ├── Get all controllers registered for this route template       │
│    └── Find newest controller: baseVersion ≤ V20260201,             │
│         same category (Preview)                                     │
│         → V20260201/Database controller (exact match)               │
│ 3. Action selector → selects CreateOrUpdate action method           │
│ 4. Action invocation                                                │
└─────────────────────────────────────────────────────────────────────┘
```

### Routing Resolution Examples

| Request | Resolution |
|---------|------------|
| `PUT .../databases/mydb?api-version=2026-02-01-preview` (CreateOrUpdate) | → `V20260201/Database` (defines CreateOrUpdate) |
| `GET .../databases/mydb?api-version=2026-02-01-preview` (Get) | → `V20260201/Database` does NOT define Get → falls back to nearest older controller that does (e.g., `V20250801/Database`) |
| `PUT .../databases/mydb?api-version=2025-08-01` (CreateOrUpdate, Stable) | → `V20250801/Database` (Stable category match) |
| `GET .../databases/mydb?api-version=2025-05-01-preview` (no exact version) | → Falls back to `V20250201/Database` (newest controller with baseVersion ≤ requested, Preview category) |

### Directory Structure

```
Sql\xdb\manifest\svc\mgmt\WebApiHosting\ARM\
├── ApiVersion.cs                          # Version enum with string mappings and categories
├── Constants\
│   └── ArmResourceProviderApiVersionCategory.cs   # Preview / Stable constants
├── VCommon\
│   └── Database\
│       └── Controllers\
│           └── BaseDatabase.cs            # Shared base class for all versions
├── V20250201\
│   └── Databases\
│       └── Controllers\
│           └── Database.cs                # Operations that changed in 2025-02-01
├── V20250801\
│   └── Databases\
│       └── Controllers\
│           └── Database.cs                # Operations that changed in 2025-08-01
└── V20260201\
    └── Databases\
        └── Controllers\
            └── Database.cs                # Operations that changed in 2026-02-01
```

### What Developers Do Today

When adding a new API version, developers must:

1. **Add enum value** to `ApiVersion.cs` with `[ApiVersion]` attributes for both Preview and Stable categories
2. **Identify impacted operations** — manually determine which operations changed by analyzing model/type changes
3. **Create version directory** — `V20260201/Databases/Controllers/Database.cs`
4. **Write controller** with `[VersionedRoute]` and `[BaseApiVersion]` attributes, inheriting from the shared base
5. **Write or update models** for the new version
6. **Verify routing** — ensure fallback resolves correctly for unchanged operations

---

## Applying API-First to Azure SQL

The API-first approach described in the [general design document](./api-first-typespec-server-codegen-design.md) applies directly to Azure SQL. The TypeSpec specification becomes the single source of truth, and the emitter generates incremental controllers and version-specific models — using SQL's existing versioning infrastructure.

### What Changes and What Stays the Same

| Aspect | Today (Manual) | With TypeSpec (Generated) |
|--------|---------------|--------------------------|
| **API contract definition** | Implicitly defined by controller code and models | Explicitly defined in TypeSpec spec |
| **ApiVersion enum** | Hand-written with `[ApiVersion]` attributes | Generated from TypeSpec `@versioned` enum |
| **Identifying impacted operations** | Manual analysis by developers | Automatic — the emitter compares versions using `@added`, `@removed`, etc. |
| **Controller code for new versions** | Hand-written per impacted operation | Generated abstract base classes with `[VersionedRoute]` and `[BaseApiVersion]` |
| **Models and validation** | Hand-written C# classes | Generated from TypeSpec models |
| **Preview / Stable categories** | Hand-written on enum and controllers | Generated — emitter produces both category attributes |
| **Routing framework** | Unchanged | Unchanged — same `ApiVersionControllerSelector` and fallback algorithm |
| **Business logic** | Hand-written | Hand-written (in concrete controller inheriting from generated base) |
| **Project structure (version directories)** | Unchanged | Unchanged — emitter outputs the same directory layout |

### TypeSpec Spec for Azure SQL

#### Defining Versions

```tsp
import "@typespec/versioning";
using TypeSpec.Versioning;

@versioned(Versions)
namespace Microsoft.Sql;

enum Versions {
  v2023_01_01: "2023-01-01",
  v2024_01_01: "2024-01-01",
  v2025_02_01: "2025-02-01",
  v2026_02_01: "2026-02-01",
}
```

#### Tracking Changes with Versioning Decorators

```tsp
model SqlDatabaseProperties {
  name: string;
  location: string;
  sku: Sku;

  @added(Versions.v2024_01_01)
  maxSizeBytes?: int64;

  @added(Versions.v2025_02_01)
  collation?: string;

  @added(Versions.v2026_02_01)
  encryptionType?: EncryptionType;
}

@added(Versions.v2026_02_01)
enum EncryptionType {
  None,
  ServiceManaged,
  CustomerManaged,
}
```

### What the Emitter Generates

The emitter produces three categories of output — **controllers**, **models**, and the **ApiVersion enum** — following the same incremental directory structure:

```
Generated/
├── ApiVersion.cs                      ← Generated version registry
│
├── V20230101/
│   └── Databases/
│       ├── Controllers/
│       │   └── DatabaseControllerBase.cs    ← All operations (first version)
│       └── Models/
│           └── SqlDatabase.cs               ← name, location, sku
│
├── V20240101/
│   └── Databases/
│       ├── Controllers/
│       │   └── DatabaseControllerBase.cs    ← Only impacted operations
│       └── Models/
│           └── SqlDatabase.cs               ← + maxSizeBytes
│
├── V20250201/
│   └── Databases/
│       ├── Controllers/
│       │   └── DatabaseControllerBase.cs    ← Only impacted operations
│       └── Models/
│           └── SqlDatabase.cs               ← + collation
│
└── V20260201/
    └── Databases/
        ├── Controllers/
        │   └── DatabaseControllerBase.cs    ← Only impacted operations
        └── Models/
            └── SqlDatabase.cs               ← + encryptionType
```

#### Generated Controllers

The emitter generates abstract base controllers with SQL's versioning attributes — `[VersionedRoute]` and `[BaseApiVersion]` — containing only the operations impacted in each version:

```csharp
// Generated/V20260201/Databases/Controllers/DatabaseControllerBase.cs
// Only contains operations impacted in 2026-02-01

[VersionedRoute("subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Sql/databases/{databaseName}")]
[BaseApiVersion(ApiVersion.V20260201, ArmResourceProviderApiVersionCategory.Preview)]
[BaseApiVersion(ApiVersion.V20260201, ArmResourceProviderApiVersionCategory.Stable)]
public abstract class DatabaseControllerBase
{
    public abstract Task<IActionResult> CreateOrUpdate(
        string resourceGroupName,
        string databaseName,
        [FromBody] SqlDatabase body,       // ← Uses V20260201's model (includes encryptionType)
        CancellationToken cancellationToken);

    // Get, List, Delete are NOT here — they didn't change,
    // so the routing framework falls back to V20250201's controller.
}
```

- **V20230101** includes all operations (first version — everything is new)
- **V20240101–V20260201** include only operations whose request/response signature changed
- In this example, adding a property to `SqlDatabaseProperties` impacts `CreateOrUpdate` (which takes the model as input) but not `Delete` (which doesn't reference the model)

#### Generated Models

The emitter generates version-specific model classes. Each model reflects the exact API shape at that version:

```csharp
// Generated/V20230101/Databases/Models/SqlDatabase.cs
public class SqlDatabase
{
    public string Name { get; set; }
    public string Location { get; set; }
    public Sku Sku { get; set; }
}
```

```csharp
// Generated/V20240101/Databases/Models/SqlDatabase.cs
public class SqlDatabase
{
    public string Name { get; set; }
    public string Location { get; set; }
    public Sku Sku { get; set; }
    public long? MaxSizeBytes { get; set; }       // Added in 2024-01-01
}
```

```csharp
// Generated/V20260201/Databases/Models/SqlDatabase.cs
public class SqlDatabase
{
    public string Name { get; set; }
    public string Location { get; set; }
    public Sku Sku { get; set; }
    public long? MaxSizeBytes { get; set; }
    public string? Collation { get; set; }
    public EncryptionType? EncryptionType { get; set; }  // Added in 2026-02-01
}
```

Each model is self-contained — serialization, validation, and controller references are all version-correct, as described in the [general design document](./api-first-typespec-server-codegen-design.md#generated-models).

#### Generated ApiVersion Enum

Unlike the general pattern, SQL requires a **generated ApiVersion enum** with category attributes for both Preview and Stable:

```csharp
// Generated/ApiVersion.cs
public enum ApiVersion : int
{
    [ApiVersion("2023-01-01-preview", Category = ArmResourceProviderApiVersionCategory.Preview)]
    [ApiVersion("2023-01-01", Category = ArmResourceProviderApiVersionCategory.Stable)]
    V20230101 = 1,

    [ApiVersion("2024-01-01-preview", Category = ArmResourceProviderApiVersionCategory.Preview)]
    [ApiVersion("2024-01-01", Category = ArmResourceProviderApiVersionCategory.Stable)]
    V20240101 = 2,

    [ApiVersion("2025-02-01-preview", Category = ArmResourceProviderApiVersionCategory.Preview)]
    [ApiVersion("2025-02-01", Category = ArmResourceProviderApiVersionCategory.Stable)]
    V20250201 = 3,

    [ApiVersion("2026-02-01-preview", Category = ArmResourceProviderApiVersionCategory.Preview)]
    [ApiVersion("2026-02-01", Category = ArmResourceProviderApiVersionCategory.Stable)]
    V20260201 = 4,
}
```

This is SQL-specific: the general pattern uses standard `[ApiVersion]` attributes from `Asp.Versioning`, while SQL uses its own `[ApiVersion]` attribute with `Category` support and `ArmResourceProviderApiVersionCategory`.

### Implementing Business Logic

Developers write concrete controllers that inherit from the generated abstract base:

```csharp
// Your code — survives regeneration
public class DatabaseController : DatabaseControllerBase
{
    public override async Task<IActionResult> CreateOrUpdate(
        string resourceGroupName,
        string databaseName,
        SqlDatabase body,
        CancellationToken cancellationToken)
    {
        var result = await _service.CreateOrUpdateDatabaseAsync(
            resourceGroupName, databaseName, body, cancellationToken);
        return Ok(result);
    }
}
```

The `[VersionedRoute]` and `[BaseApiVersion]` attributes are on the generated base class, so the concrete class inherits them automatically. The routing framework resolves requests the same way it does today.

---

## SQL-Specific Variations

The general design document describes API-first server code generation using standard ASP.NET API versioning. Azure SQL's versioning infrastructure differs in several ways:

| Aspect | General Pattern | Azure SQL |
|--------|----------------|-----------|
| **Versioning framework** | `Asp.Versioning.Mvc` (standard) | Custom `ApiVersionControllerSelector` |
| **Version attribute** | `[ApiVersion("2025-12-01")]` | `[BaseApiVersion(ApiVersion.V20251201, Category)]` |
| **Route attribute** | `[Route("...")]` | `[VersionedRoute("...")]` |
| **Version registry** | Configuration-based or convention-based | `ApiVersion` C# enum with integer ordinals |
| **Category support** | Not built-in | Preview / Stable dual categories per version |
| **Shared base classes** | None assumed | `VCommon/BaseDatabase<T>` pattern |

### Preview / Stable Dual Categories

In the general pattern, each version string maps to one version. In SQL, each version number produces **two** version strings — one preview and one stable:

- `2026-02-01` → `"2026-02-01"` (Stable) and `"2026-02-01-preview"` (Preview)

The emitter generates both `[BaseApiVersion]` attributes on each controller, and both `[ApiVersion]` attributes on each enum value. Routing isolation is preserved — preview requests never fall back to stable controllers.

### VCommon Shared Base Classes

SQL uses shared base classes (e.g., `BaseDatabase<T>`) in `VCommon/` that contain logic shared across all versions. The emitter has no knowledge of these — it generates standalone abstract base classes.

The team bridges the two by composing shared logic into their concrete controllers:

```csharp
// Team's concrete controller — bridges generated base with existing shared logic
public class DatabaseController : DatabaseControllerBase
{
    private readonly BaseDatabaseLogic<DatabaseResource> _sharedLogic;

    public override async Task<IActionResult> CreateOrUpdate(...)
    {
        return await _sharedLogic.HandleCreateOrUpdate(...);
    }
}
```

### Team-Defined Routing Attributes

The attributes `[VersionedRoute]`, `[BaseApiVersion]`, and the `ApiVersion` enum are defined by the SQL team, not by a shared framework. For the pilot, the emitter generates code referencing these SQL-team types directly. Long-term, these should be extracted into a shared package so the emitter's output is portable across teams.

---

## Required Refactoring

The emitter generates code based on the TypeSpec specification. Where there is a mismatch between the generated output and the existing codebase, **the SQL team adapts their codebase to consume the generated output** — not the other way around.

### 1. ApiVersion Enum → Generated Version Registry

**Problem**: C# enums cannot be generated incrementally — there is no "partial enum." The emitter cannot add a new version entry without regenerating the entire enum, risking conflicts with hand-written entries for versions not yet in the TypeSpec spec.

**Options**:

| Option | Approach | Trade-off |
|--------|----------|-----------|
| **Fully generated enum** | The emitter owns the entire `ApiVersion` enum. All versions come from the spec. | Requires all versions in spec — clean but needs full coverage |
| **Class-based registry** | Replace enum with a class supporting registration patterns | Flexible, allows gradual migration, but changes routing framework |
| **Split + merge** | Emitter generates partial file; build step merges with hand-written entries | Preserves enum but adds build complexity |

**Recommendation**: For the pilot, **fully generated enum** is simplest if the team commits to defining all versions in the spec.

### 2. VCommon Base Classes

**Problem**: `BaseDatabase<T>` contains shared business logic via inheritance. The generated abstract base controllers don't know about it.

**What the team needs to do**: Separate shared logic from the controller interface so concrete controllers can bridge both — composition over inheritance.

### 3. Route Constants

**Problem**: Route templates are in `ArmResourceProviderRouteConstants`. The emitter generates routes from TypeSpec resource paths.

**What the team needs to do**: Align constants with generated routes, or adopt generated routes directly.

### 4. Preview / Stable Category Mapping

**Problem**: TypeSpec defines versions as simple strings. The emitter needs to know that each version produces both Preview and Stable entries.

**Proposed approach**: The emitter generates both by convention (matching SQL's current pattern). A decorator (e.g., `@previewOnly`) could mark exceptions.

### 5. Routing Attributes as Shared Package

**Problem**: `[VersionedRoute]` and `[BaseApiVersion]` are SQL-team-defined types. The emitter generates code referencing them.

| Timeframe | Approach |
|-----------|----------|
| **Pilot** | Emit SQL-team-defined attributes directly |
| **Post-pilot** | Extract into shared NuGet package for portability |

### Summary

| Component | Who Changes | Effort |
|-----------|------------|--------|
| **ApiVersion enum** | Both (emitter + team) | High |
| **VCommon base classes** | SQL team | Medium |
| **Route constants** | SQL team | Low |
| **Preview/Stable categories** | Emitter | Low |
| **Routing attributes** | Both (short-term emitter, long-term shared package) | Low → High |

---

## Pilot Plan

### Scope

| Item | Details |
|------|---------|
| **Team** | Azure SQL Resource Provider |
| **Initial scope** | 1–2 representative resource types (e.g., `Database`, `Server`) |
| **Approach** | Parallel generation alongside existing hand-written code for validation |

### Phases

1. **Phase 1 — Proof of Concept**: Define TypeSpec spec for one resource type covering 2–3 API versions. Generate controllers and compare against existing hand-written code.
2. **Phase 2 — Validation**: Run generated code against integration tests. Verify routing fallback behavior matches current behavior.
3. **Phase 3 — Adoption**: Expand to additional resource types. Transition to spec-first workflow for new API versions.

### Success Criteria

- Generated incremental controllers match the structure of existing hand-written controllers
- Routing resolves correctly across all API versions (new and fallback)
- Business logic can be cleanly separated into concrete class implementations
- Adding a new API version requires only spec changes + business logic (no manual controller scaffolding)

---

## References

- [General API-First Server Code Generation Design](./api-first-typespec-server-codegen-design.md) — foundational concepts and patterns
- [@typespec/versioning](https://github.com/microsoft/typespec/tree/main/packages/versioning) — decorator reference and versioning examples
- [Azure Resource Manager Patterns](https://azure.github.io/typespec-azure/)
