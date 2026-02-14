# API-First Development with TypeSpec: Design for Azure SQL Resource Provider

## Executive Summary

This document describes how the Azure SQL resource provider team can adopt an API-first development approach using TypeSpec and automated server code generation. The goal is to make the TypeSpec specification the single source of truth for API contracts, from which server controllers, models, and versioning logic are generated — while preserving the team's existing incremental versioning pattern.

---

## Current State: How Azure SQL Handles Versioning Today

### Incremental Controller Pattern

The Azure SQL team uses an **incremental versioning** approach. Rather than duplicating all controllers for every new API version, they create a new version directory containing **only the operations that changed**. A custom routing framework then resolves each incoming request to the correct controller based on the requested `api-version`.

### Key Components

#### ApiVersion Enum

All supported API versions are defined as an enum in `ApiVersion.cs`. Each enum value carries `[ApiVersion]` attributes that map the version string to a **category** (Preview or Stable):

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

#### API Version Categories

Each API version string belongs to a category:

| Category | Example version string | Description |
|----------|----------------------|-------------|
| **Preview** | `2025-08-01-preview` | Preview/pre-release API |
| **Stable** | `2025-08-01` | Stable/GA API |

The same enum value (e.g., `V20250801`) can serve both a preview and a stable version string. Categories are important because the routing framework **only matches within the same category** — a preview request never falls back to a stable controller, and vice versa.

#### Controller Attributes

Controllers declare which versions they handle using two attributes:

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

#### Version Fallback Algorithm

The core routing logic finds the correct controller for a given request using `VersionRouteSelectorHelper.GetControllerForVersion()`:

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

In plain terms: for a given request, find the **newest controller** whose base version is ≤ the requested version, in the same category, and optionally bounded by a max version.

#### Request Flow

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

#### Routing Resolution Examples

| Request | Resolution |
|---------|------------|
| `PUT .../databases/mydb?api-version=2026-02-01-preview` (CreateOrUpdate) | → `V20260201/Database` (defines CreateOrUpdate) |
| `GET .../databases/mydb?api-version=2026-02-01-preview` (Get) | → `V20260201/Database` does NOT define Get → falls back to nearest older controller that does (e.g., `V20250801/Database`) |
| `PUT .../databases/mydb?api-version=2025-08-01` (CreateOrUpdate, Stable) | → `V20250801/Database` (Stable category match) |
| `GET .../databases/mydb?api-version=2025-05-01-preview` (no exact version) | → Falls back to `V20250201/Database` (newest controller with baseVersion ≤ requested, Preview category) |

### Current Directory Structure

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

### What Developers Do Today (Manual Steps)

When adding a new API version, developers must:

1. **Add enum value** to `ApiVersion.cs` with `[ApiVersion]` attributes for both Preview and Stable categories
2. **Identify impacted operations** — manually determine which operations changed by analyzing model/type changes
3. **Create version directory** — `V20260201/Databases/Controllers/Database.cs`
4. **Write controller** with `[VersionedRoute]` and `[BaseApiVersion]` attributes, inheriting from the shared base
5. **Write or update models** for the new version
6. **Verify routing** — ensure fallback resolves correctly for unchanged operations

---

## The API-First Vision with TypeSpec

### Core Idea

Instead of manually writing incremental controllers, the team would:

1. **Define the API in TypeSpec** — all resources, operations, models, and their version history in a single spec
2. **Generate server code** — the TypeSpec compiler emits controllers and models that follow the same incremental versioning pattern the team already uses
3. **Implement business logic** — developers extend generated stubs with business logic using partial classes

The TypeSpec specification becomes the single source of truth. The emitter understands version history and automatically determines which operations are impacted, generating only the necessary incremental controllers per version.

### What Changes and What Stays the Same

| Aspect | Today (Manual) | With TypeSpec (Generated) |
|--------|---------------|--------------------------|
| **API contract definition** | Implicitly defined by controller code and models | Explicitly defined in TypeSpec spec |
| **ApiVersion enum** | Hand-written with `[ApiVersion]` attributes | Generated from TypeSpec `@versioned` enum |
| **Identifying impacted operations** | Manual analysis by developers | Automatic — the emitter compares versions using `@added`, `@removed`, etc. |
| **Controller code for new versions** | Hand-written per impacted operation | Generated abstract base classes with `[VersionedRoute]` and `[BaseApiVersion]` |
| **Models and validation** | Hand-written C# classes | Generated from TypeSpec models |
| **Versioning attributes on controllers** | Hand-written `[BaseApiVersion]` | Generated on abstract base classes |
| **Routing framework** | Unchanged | Unchanged — same `ApiVersionControllerSelector` and fallback algorithm |
| **Preview / Stable categories** | Hand-written on enum and controllers | Generated — emitter produces both category attributes |
| **Business logic** | Hand-written | Hand-written (in concrete controller inheriting from generated base) |
| **Project structure (version directories)** | Unchanged | Unchanged — emitter outputs the same directory layout |

---

## How TypeSpec Maps to the Incremental Versioning Pattern

### Defining Versions in TypeSpec

All supported API versions are declared in a single enum:

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

### Tracking Changes with Versioning Decorators

The spec captures what changed and when, using decorators:

```tsp
model SqlDatabase {
  name: string;
  location: string;
  sku: Sku;

  @added(Versions.v2024_01_01)
  maxSizeBytes?: int64;

  @added(Versions.v2025_02_01)
  collation?: string;

  @added(Versions.v2026_02_01)
  encryptionType?: EncryptionType;   // ← This is the new property in 2026-02-01
}

@added(Versions.v2026_02_01)
enum EncryptionType {
  None,
  ServiceManaged,
  CustomerManaged,
}
```

### What the Emitter Generates

Given the spec above, the emitter compares each version against the previous to determine what changed, and generates **only the impacted operations** per version — matching the team's existing incremental pattern.

The emitter generates **abstract base controllers** — they define the operation signatures but contain no implementation. Developers then write concrete controllers that inherit from these base classes.

The emitter generates:

```
Generated/
├── ApiVersion.cs                      ← Generated enum with [ApiVersion] attributes and categories
│
├── V20230101/
│   └── Databases/
│       └── Controllers/
│           └── DatabaseControllerBase.cs      ← Abstract base: CreateOrUpdate, Get, List, Delete
│       └── Models/
│           └── SqlDatabase.cs                 ← name, location, sku
│
├── V20240101/
│   └── Databases/
│       └── Controllers/
│           └── DatabaseControllerBase.cs      ← Abstract base: only CreateOrUpdate (maxSizeBytes added)
│       └── Models/
│           └── SqlDatabase.cs                 ← name, location, sku, maxSizeBytes
│
├── V20250201/
│   └── Databases/
│       └── Controllers/
│           └── DatabaseControllerBase.cs      ← Abstract base: only CreateOrUpdate (collation added)
│       └── Models/
│           └── SqlDatabase.cs                 ← name, location, sku, maxSizeBytes, collation
│
└── V20260201/
    └── Databases/
        └── Controllers/
            └── DatabaseControllerBase.cs      ← Abstract base: only CreateOrUpdate (encryptionType added)
        └── Models/
            └── SqlDatabase.cs                 ← name, location, sku, maxSizeBytes, collation, encryptionType
```

- **V20230101** is the first version, so all operations are included.
- **V20240101** through **V20260201** are incremental — each contains only the operations impacted by that version's changes.
- In this example, `Get`, `List`, and `Delete` are never regenerated after V20230101 because their request/response models were not affected. A request for `GET .../databases/mydb?api-version=2026-02-01` falls back through the routing chain to V20230101's controller.

The generated `V20260201/DatabaseControllerBase.cs` contains only the impacted operation as an abstract method, with the correct routing attributes:

```csharp
// Generated — do not modify directly
[VersionedRoute("subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Sql/databases/{databaseName}")]
[BaseApiVersion(ApiVersion.V20260201, ArmResourceProviderApiVersionCategory.Preview)]
[BaseApiVersion(ApiVersion.V20260201, ArmResourceProviderApiVersionCategory.Stable)]
public abstract class DatabaseControllerBase
{
    public abstract Task<IActionResult> CreateOrUpdate(
        string resourceGroupName,
        string databaseName,
        SqlDatabase body,       // ← includes encryptionType
        CancellationToken cancellationToken);

    // Get, List, Delete are NOT generated here — routing falls back to V20250201
}
```

The emitter also generates the **ApiVersion enum** with the correct attributes and categories:

```csharp
// Generated — do not modify directly
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

### Implementing Business Logic

Developers write a concrete controller that inherits from the generated abstract base:

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
        // Business logic: validate, persist, return response
        var result = await _service.CreateOrUpdateDatabaseAsync(
            resourceGroupName, databaseName, body, cancellationToken);
        return Ok(result);
    }
}
```

The `[VersionedRoute]` and `[BaseApiVersion]` attributes are on the generated base class, so the concrete class inherits them automatically. The routing framework resolves requests the same way it does today — no changes to the routing infrastructure are needed.

---

## Implementation Workflow

### Adding a New API Version

```
┌─────────────────────────────────────────────────────────────────┐
│  1. Update TypeSpec Spec                                        │
│     ├── Add new version to the Versions enum                    │
│     ├── Annotate new/changed properties with @added, @removed   │
│     └── Add new operations if needed                            │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  2. API Review                                                  │
│     ├── Review spec changes (diff is clear and declarative)     │
│     ├── Validate ARM compliance                                 │
│     └── Confirm versioning strategy                             │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  3. Generate Server Code                                        │
│     tsp compile . --emit @azure-tools/typespec-providerhub-controller
│                                                                 │
│     Emitter automatically:                                      │
│     ├── Determines which operations are impacted                │
│     ├── Generates incremental controllers per version           │
│     ├── Generates version-specific models                       │
│     └── Applies versioning attributes for routing               │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  4. Implement Business Logic                                    │
│     ├── Extend generated partial classes                        │
│     ├── Implement new or changed operations                     │
│     └── Reuse existing logic where operations didn't change     │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  5. Test & Deploy                                               │
│     ├── Contract tests to validate spec compliance              │
│     ├── Integration tests with RPaaS OneBox                     │
│     └── Verify routing resolves correctly across versions       │
└─────────────────────────────────────────────────────────────────┘
```

### What Happens When You Regenerate

When the spec changes and code is regenerated:

| File type | What happens |
|-----------|-------------|
| Generated abstract base controllers | **Overwritten** — always reflects latest spec |
| Generated models | **Overwritten** — always reflects latest spec |
| Your concrete controller implementations | **Untouched** — separate files, never overwritten |

This is why the abstract base class pattern is essential: generated code and business logic live in separate files and can evolve independently.

---

## Notable Considerations

### 1. Impact Analysis Is Automated

Today, developers must manually figure out which operations are impacted by a model change. With TypeSpec, this analysis is built into the emitter — it walks the type graph from the changed type to all operations that reference it, and generates incremental controllers accordingly.

### 2. Generated vs. Custom Code Separation

Generated code and hand-written code live in separate directories. The `Generated/` directory is fully owned by the emitter and may be overwritten at any time. The team's concrete implementations live outside of it:

```
Project/
├── Generated/                                         ← Emitter-owned (overwritten on regeneration)
│   ├── ApiVersion.cs                                  ← Generated enum
│   └── V20260201/
│       └── Databases/
│           └── Controllers/
│               └── DatabaseControllerBase.cs           ← Generated abstract base
│           └── Models/
│               └── SqlDatabase.cs                      ← Generated model
│
└── V20260201/                                         ← Team-owned (never touched by emitter)
    └── Databases/
        └── Controllers/
            └── DatabaseController.cs                   ← Your concrete implementation
```

**Rule**: Never modify anything in the `Generated/` directory. All implementation goes in the team-owned directory, in concrete controllers that inherit from the generated abstract bases.

### 3. Breaking Change Detection

The TypeSpec compiler can detect breaking changes at spec-review time:

- Removing a required property without `@removed` decorator
- Changing a property type without `@typeChangedFrom`
- Removing an operation

This catches issues **before code generation**, shifting errors left in the development process.

### 4. Continuous Regeneration

| Trigger | Action |
|---------|--------|
| Spec change merged | Regenerate in CI/CD pipeline |
| Local development | Regenerate via MSBuild target on build |
| PR review | Include generated code diff for review |

### 5. Versioning Decorator Reference

| Decorator | Use when... |
|-----------|-------------|
| `@added(version)` | A new property, operation, or type is introduced |
| `@removed(version)` | A property, operation, or type is removed/deprecated |
| `@renamedFrom(version, oldName)` | A property or type is renamed |
| `@typeChangedFrom(version, oldType)` | A property's type changes |
| `@madeOptional(version)` | A required property becomes optional |
| `@madeRequired(version)` | An optional property becomes required |

> 📖 **For full versioning decorator details, see [@typespec/versioning](https://github.com/microsoft/typespec/tree/main/packages/versioning)**

---

## Benefits for the Azure SQL Team

| Benefit | How |
|---------|-----|
| **No more manual impact analysis** | Emitter automatically determines impacted operations per version |
| **No more hand-writing incremental controllers** | Generated from spec with correct versioning attributes |
| **Single source of truth** | TypeSpec spec defines the API contract, version history, and all models in one place |
| **Spec-level API review** | Review changes in a declarative spec rather than in scattered controller code |
| **Breaking change detection** | Caught at compile time, before code is generated or deployed |
| **Reduced boilerplate** | Focus on business logic; routing, models, and controller scaffolding are generated |
| **Consistent structure** | Every version directory follows the same pattern, generated uniformly |

---

## Required Refactoring in the SQL Codebase

The emitter generates code based purely on the TypeSpec specification. It produces a clean, spec-driven output — abstract base controllers, models, and version metadata. It cannot and should not have knowledge of team-specific structures like `BaseDatabase<T>`, custom route constants, or the specific shape of the existing routing framework.

This means: **where there is a mismatch between the generated output and the existing codebase, it is the SQL team's responsibility to adapt their codebase to consume the generated output** — not the other way around. The emitter may offer limited configuration options (e.g., output directory, namespace), but it will not be customized to produce code that fits arbitrary existing class hierarchies.

### Guiding Principle

The emitter generates **interfaces and contracts** (abstract base controllers, models). The SQL team is responsible for **wiring those into their existing infrastructure** (routing, base classes, shared logic). This separation is similar to how the team already consumes other generated artifacts — the generated code defines _what_ operations exist; the team's code defines _how_ they are implemented and integrated.

### 1. ApiVersion Enum → Generated Version Registry

**Problem**: The current `ApiVersion` is a C# `enum`. Enums cannot be generated incrementally — you cannot have a "partial enum" in C# the way you can have partial classes. This means the emitter cannot simply add a new version entry without regenerating the entire enum, which risks conflicting with hand-written entries for versions that are not yet in the TypeSpec spec.

**Today's structure (not code-gen friendly)**:

```csharp
// Hand-written enum — cannot be extended by generated code
public enum ApiVersion : int
{
    [ApiVersion("2026-02-01-preview", Category = ArmResourceProviderApiVersionCategory.Preview)]
    [ApiVersion("2026-02-01", Category = ArmResourceProviderApiVersionCategory.Stable)]
    V20260201 = 25,

    [ApiVersion("2025-08-01-preview", Category = ArmResourceProviderApiVersionCategory.Preview)]
    [ApiVersion("2025-08-01", Category = ArmResourceProviderApiVersionCategory.Stable)]
    V20250801 = 24,
    // ...
}
```

**Proposed refactoring options**:

| Option | Approach | Trade-off |
|--------|----------|-----------|
| **A. Fully generated enum** | The emitter owns the entire `ApiVersion` enum. All versions come from the TypeSpec spec. | Requires all versions to be defined in the spec — no hand-written versions allowed. Clean but requires full spec coverage. |
| **B. Class-based version registry** | Replace the enum with a class that supports partial definitions or a registration pattern. The emitter generates version entries, and hand-written code can add additional entries. | More flexible, allows gradual migration, but requires changes to the routing framework that consumes the enum. |
| **C. Split enum + merge at build time** | The emitter generates a partial version file. A build step merges hand-written and generated entries into the final enum. | Preserves the enum type, but adds build complexity. |

**Recommendation**: For the pilot, **Option A** (fully generated enum) is the simplest if the team can commit to defining all versions in the TypeSpec spec. For a gradual migration where some versions remain hand-written, **Option B** would be necessary.

### 2. Controller Base Classes (VCommon/) — Team Responsibility

**Problem**: The team currently has shared base classes in `VCommon/` (e.g., `BaseDatabase<T>`) that contain shared logic across all versions. The generated abstract base controllers do not and cannot know about these — `BaseDatabase<T>` is a team-specific implementation detail that has no representation in the TypeSpec spec.

**Today's structure**:

```
VCommon/
  Database/
    Controllers/
      BaseDatabase.cs      ← Hand-written, contains shared business logic
```

**What the emitter generates**:

The emitter produces a standalone abstract base class — it does not inherit from anything team-specific:

```csharp
// Generated — no knowledge of BaseDatabase<T>
public abstract class DatabaseControllerBase
{
    public abstract Task<IActionResult> CreateOrUpdate(...);
}
```

**What the SQL team needs to do**: The team must separate their shared logic from the controller interface. Their concrete controller implementation bridges the generated abstract base and their existing infrastructure:

```csharp
// Team's code — bridges generated base with existing shared logic
public class DatabaseController : DatabaseControllerBase
{
    private readonly BaseDatabase<DatabaseResource> _sharedLogic;

    public override async Task<IActionResult> CreateOrUpdate(...)
    {
        // Delegate to existing shared logic, or implement directly
    }
}
```

Alternatively, the team could refactor `BaseDatabase<T>` so that it is composed into (rather than inherited by) their controllers. The key point is: **the emitter defines the API contract surface; the team decides how to wire that into their implementation layer.**

### 3. Route Constants — Team Responsibility

**Problem**: Route templates are currently defined as hand-written constants in `ArmResourceProviderRouteConstants`. The emitter will generate `[VersionedRoute]` attributes with route strings derived from the TypeSpec resource paths (e.g., ARM resource URIs).

**What the emitter generates**:

```csharp
// Generated — route derived from TypeSpec resource path
[VersionedRoute("subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Sql/databases/{databaseName}")]
public abstract class DatabaseControllerBase { ... }
```

**What the SQL team needs to do**: If the team wants to continue using their `ArmResourceProviderRouteConstants`, they would need to ensure the constant values match the generated route strings, or adapt their code to use the generated routes directly. The emitter will not reference team-specific constants it has no knowledge of.

### 4. Preview / Stable Category Mapping

**Problem**: The TypeSpec spec defines versions as simple strings (e.g., `"2026-02-01"`), but the SQL codebase distinguishes between Preview and Stable categories for the same version number. The emitter needs to know that `"2026-02-01"` should produce both `"2026-02-01-preview"` (Preview) and `"2026-02-01"` (Stable) entries.

**Proposed approach**: This can be handled in one of two ways:

| Option | Approach |
|--------|----------|
| **Convention-based** | The emitter always generates both Preview and Stable attributes for every version. This matches the current SQL team pattern where every version has both. |
| **Decorator-based** | Introduce a custom decorator (e.g., `@previewOnly`) to mark versions that should only have a Preview category. By default, both are generated. |

### 5. Versioned Routing Attributes — Long-Term Concern

**Problem**: The attributes used for version routing — `[VersionedRoute]`, `[BaseApiVersion]`, and the `ApiVersion` enum itself — are **defined by the SQL team**, not by a shared framework or platform. They are team-specific implementations in the SQL codebase (`Sql\xdb\common\WebApiHosting\Framework\`). This means the emitter would need to generate code that references types defined in the SQL team's own assemblies.

**Short-term approach**: For the pilot, the emitter can generate code that uses these SQL-team-defined attributes directly. This is pragmatic — it gets the team up and running without requiring infrastructure changes. The emitter would need to know the attribute type names and their assembly references.

**Long-term concern**: This is not sustainable as the emitter scales beyond the SQL team. The emitter should not take a dependency on team-specific attribute types. If other teams adopt the same API-first approach, each team would have their own routing attributes, and the emitter cannot generate team-specific code for all of them.

**Long-term direction**: The routing attributes and version resolution mechanism should be **extracted into a shared framework or NuGet package** that:

- Defines standard attributes (`[VersionedRoute]`, `[BaseApiVersion]`, etc.) that any RP team can use
- Provides the version fallback routing logic (`ApiVersionControllerSelector`) as reusable infrastructure
- Is referenced by both the emitter (to generate code against) and the consuming teams (to run it)

This would make the emitter's generated code portable across teams, and avoid coupling the emitter to any single team's internal types.

| Timeframe | Approach | Risk |
|-----------|----------|------|
| **Pilot** | Emit SQL-team-defined attributes directly | Acceptable — limited scope, gets feedback quickly |
| **Post-pilot** | Extract attributes into shared package; emitter targets the shared types | Required before scaling to other teams |

### Summary of Refactoring Effort

| Component | Current Form | Who Needs to Change | Effort |
|-----------|-------------|-------------------|--------|
| **ApiVersion enum** | C# enum (not extendable) | **Both** — emitter generates version metadata; team refactors routing to consume it | **High** — touches routing framework |
| **VCommon base classes** | `BaseDatabase<T>` inheritance | **SQL team** — separate shared logic from controller interface so concrete controllers can bridge both | **Medium** — architectural change in how controllers compose logic |
| **Route constants** | Hand-written constants | **SQL team** — align constants with generated routes, or adopt generated routes directly | **Low** — straightforward alignment |
| **Preview/Stable categories** | Dual attributes per version | **Emitter** — generate both by convention | **Low** — emitter logic only |
| **Routing framework** | `ApiVersionControllerSelector` | **SQL team** — may need updates if ApiVersion changes from enum to class | **Medium** — depends on version registry approach |
| **Routing attributes** | SQL-team-defined types | **Both** — emitter uses them short-term; long-term extract to shared package | **Low** (pilot) / **High** (long-term) |

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

- [ ] Generated incremental controllers match the structure of existing hand-written controllers
- [ ] Routing resolves correctly across all API versions (new and fallback)
- [ ] Business logic can be cleanly separated into partial class extensions
- [ ] Adding a new API version requires only spec changes + business logic (no manual controller scaffolding)

---

## References

- [@typespec/versioning](https://github.com/microsoft/typespec/tree/main/packages/versioning) — Decorator reference and versioning examples
- [TypeSpec ProviderHub Controller Documentation](../packages/typespec-providerhub-controller/README.md)
- [@typespec/versioning Package](https://github.com/microsoft/typespec/tree/main/packages/versioning)
- [Azure Resource Manager Patterns](https://azure.github.io/typespec-azure/)
