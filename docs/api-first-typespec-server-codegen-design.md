# API-First Development with TypeSpec: Server Code Generation Design

## Executive Summary

This document describes how Azure resource provider teams can adopt an API-first development approach using TypeSpec and automated server code generation. The goal is to make the TypeSpec specification the single source of truth for API contracts, from which server controllers, models, and versioning logic are generated — while preserving the incremental versioning pattern used by ARM services.

---

## The API-First Vision with TypeSpec

### Core Idea

Instead of manually writing incremental controllers, teams would:

1. **Define the API in TypeSpec** — all resources, operations, models, and their version history in a single spec
2. **Generate server code** — the TypeSpec compiler emits controllers and models that follow the same incremental versioning pattern
3. **Implement business logic** — developers extend generated stubs with concrete implementations

The TypeSpec specification becomes the single source of truth. The emitter understands version history and automatically determines which operations are impacted, generating only the necessary incremental controllers per version.

### Benefits

| Benefit | How |
|---------|-----|
| **Single source of truth** | TypeSpec spec defines the API contract, version history, and all models in one place. Server code and API specs (swagger) are no longer authored separately — eliminating the drift where swagger describes one API shape and the service implements another, which historically forces breaking swagger corrections that cascade into downstream tools (SDKs, CLI, PowerShell). |
| **Spec-level API review** | Review changes in a declarative spec rather than in scattered controller code |
| **Breaking change detection** | Caught at compile time, before code is generated or deployed |
| **Reduced boilerplate** | Focus on business logic; routing, models, and controller scaffolding are generated |

### What Changes and What Stays the Same

| Aspect | Today (Manual) | With TypeSpec (Generated) |
|--------|---------------|--------------------------|
| **API contract definition** | Implicitly defined by controller code and models | Explicitly defined in TypeSpec spec |
| **Version registry** | Hand-written | Generated from TypeSpec `@versioned` enum |
| **Identifying impacted operations** | Manual analysis by developers | Automatic — the emitter compares versions using `@added`, `@removed`, etc. |
| **Controller code for new versions** | Hand-written per impacted operation | Generated abstract base classes with versioning attributes |
| **Models and validation** | Hand-written classes | Generated from TypeSpec models |
| **Routing framework** | Unchanged | Unchanged — same version-aware routing |
| **Business logic** | Hand-written | Hand-written (in concrete class inheriting from generated base) |
| **Project structure (version directories)** | Unchanged | Unchanged — emitter outputs the same directory layout |

---

## The Incremental Versioning Pattern

### How ARM Services Handle Versioning

ARM resource providers typically use an **incremental versioning** approach. Rather than duplicating all controllers for every new API version, teams create a new version directory containing **only the operations that changed**. A version-aware routing framework then resolves each incoming request to the correct controller based on the requested `api-version`.

### Key Components

#### Version Registry

All supported API versions are defined in a central registry (typically an enum or version class). Each version entry maps a version string to metadata such as category (Preview or Stable) and ordering:

```
Version Registry
├── 2025-11-01  →  ordinal: 1
├── 2025-12-01  →  ordinal: 2
├── 2026-02-01  →  ordinal: 3
```

The ordinal values establish an ordering — higher values are newer versions.

#### Version Fallback Algorithm

The core routing logic finds the correct controller for a given request:

1. **Filter by category** — match the request's category (Preview or Stable)
2. **Filter by version range** — find controllers whose base version ≤ the requested version
3. **Select newest match** — pick the controller with the highest base version among the candidates

In plain terms: for a given operation and version, find the **newest controller** that implements that operation and whose base version is ≤ the requested version.

#### Request Flow

```
HTTP Request: PUT /subscriptions/.../databases/mydb?api-version=2026-02-01
     │
     ▼
┌─────────────────────────────────────────────────────────────────┐
│ ASP.NET Pipeline                                                │
├─────────────────────────────────────────────────────────────────┤
│ 1. Route matching → matches resource route template             │
│ 2. Version-aware controller selection:                          │
│    ├── Extract "api-version" from query string                  │
│    ├── Get all controllers registered for this route template   │
│    └── Find newest controller with baseVersion ≤ requested      │
│         → selects the correct versioned controller              │
│ 3. Action selector → selects the action method                  │
│ 4. Action invocation                                            │
└─────────────────────────────────────────────────────────────────┘
```

#### Directory Structure

```
ARM/
├── V20251101/
│   ├── Controllers/
│   │   └── MyResource.cs        # All operations (first version)
│   └── Models/
├── V20251201/
│   ├── Controllers/
│   │   └── MyResource.cs        # Only operations that changed
│   └── Models/
└── V20260201/
    ├── Controllers/
    │   └── MyResource.cs        # Only operations that changed
    └── Models/
```

### What Developers Do Today (Manual Steps)

When adding a new API version, developers must:

1. **Register the version** — add entry to the version registry with appropriate metadata
2. **Identify impacted operations** — manually determine which operations changed by analyzing model/type changes
3. **Create version directory** — with a new controller containing only impacted operations
4. **Write or update models** for the new version
5. **Verify routing** — ensure fallback resolves correctly for unchanged operations

### Why This Pattern Matters for API-First

The incremental versioning pattern is what makes API-first server code generation both valuable and tractable for ARM services:

1. **Versioning is the hardest part to get right manually.** Identifying which operations are impacted by a model change requires tracing type dependencies through the entire API surface. A new property on a model might affect Create, Get, Update, and List — but not Delete. Missing an impacted operation means a client gets stale response shapes; including an unnecessary one means wasted code. The emitter automates this analysis with zero human error.

2. **The incremental pattern keeps generated output minimal.** Unlike a "full copy" approach where every version duplicates all operations, the incremental pattern means the emitter only needs to generate controllers for what actually changed. This keeps the generated codebase small, reviewable, and aligned with how teams already think about versioning.

3. **Version history is implicit in code but explicit in TypeSpec.** Today, the version history of a property or operation exists only in the developer's memory or in commit history. With TypeSpec, `@added`, `@removed`, and `@typeChangedFrom` decorators make the full version timeline declarative and machine-readable — enabling the emitter to reason about cross-version differences automatically.

4. **The pattern enables safe continuous regeneration.** Because generated code (abstract bases) and hand-written code (concrete implementations) are cleanly separated, the emitter can regenerate at any time without touching business logic. The incremental pattern ensures that only the affected version directories are updated, minimizing diff noise in pull requests.

---

## How TypeSpec Maps to the Incremental Versioning Pattern

### Defining Versions in TypeSpec

All supported API versions are declared in a single enum:

```tsp
import "@typespec/versioning";

using TypeSpec.Versioning;

@versioned(Versions)
namespace Microsoft.MyService;

enum Versions {
  v2025_11_01: "2025-11-01",
  v2025_12_01: "2025-12-01",
  v2026_02_01: "2026-02-01",
}
```

### Tracking Changes with Versioning Decorators

The spec captures what changed and when, using decorators:

```tsp
model MyResourceProperties {
  name: string;
  location: string;

  @added(Versions.v2025_12_01)
  description?: string;

  @added(Versions.v2026_02_01)
  elasticPoolId?: string;
}
```

### Automatic Impact Analysis

The emitter compares each version against its predecessor by:

1. **Projecting the type graph** per version using `@typespec/versioning` mutators
2. **Fingerprinting** each operation's full request/response signature (parameters, models, return types)
3. **Diffing** fingerprints between consecutive versions to detect changes

This produces a per-version impact report:

- **First version**: all operations are impacted (everything is new)
- **Subsequent versions**: only operations whose fingerprint changed are impacted

Operations can be impacted because:
- They are **new** (added in this version via `@added`)
- Their **signature changed** (a model used in parameters or return types was modified)
- They were **removed** (via `@removed`)

This eliminates the need for **manual impact analysis** — developers no longer have to trace type dependencies to figure out which operations changed. The emitter uses this analysis to drive both outputs: **incremental controllers** (only for impacted operations) and **version-specific models** (reflecting the API shape at each version).

### What the Emitter Generates

Given the spec, the emitter produces two categories of output for each version — **controllers** and **models** — following the same incremental directory structure:

```
Generated/
├── V20251101/
│   ├── Controllers/
│   │   └── MyResourceControllerBase.cs    ← All operations (first version)
│   └── Models/
│       └── MyResource.cs                  ← Full model at this version
│
├── V20251201/
│   ├── Controllers/
│   │   └── MyResourceControllerBase.cs    ← Only impacted operations
│   └── Models/
│       └── MyResource.cs                  ← Model with new properties
│
└── V20260201/
    ├── Controllers/
    │   └── MyResourceControllerBase.cs    ← Only impacted operations
    └── Models/
        └── MyResource.cs                  ← Model with new properties
```

#### Generated Controllers

The emitter generates **abstract base controllers** containing only the operations impacted in each version. Each controller carries versioning attributes that the routing framework uses to resolve requests.

- **First version** — all operations are included (everything is new)
- **Subsequent versions** — only operations whose request or response signature changed

The generated controllers define operation signatures but contain no implementation:

```csharp
// Generated/V20251201/Controllers/MyResourceControllerBase.cs
// Only contains operations impacted in 2025-12-01

[ApiVersion("2025-12-01")]
[Route("subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/.../myResources/{resourceName}")]
public abstract class MyResourceControllerBase : ControllerBase
{
    [HttpPut]
    public abstract Task<IActionResult> CreateOrUpdate(
        string resourceGroupName,
        string resourceName,
        [FromBody] MyResource body,       // ← Uses V20251201's model
        CancellationToken cancellationToken);

    // Only impacted operations appear here.
    // Unchanged operations (e.g., Delete) are not included —
    // the routing framework falls back to V20251101's controller.
}
```

#### Generated Models

The emitter generates **version-specific model classes** for each API version. Each model reflects the exact API shape at that version — only the properties that exist in that version are included.

Given the TypeSpec model with versioning decorators shown earlier:

```csharp
// Generated/V20251101/Models/MyResource.cs
public class MyResource
{
    public string Name { get; set; }
    public string Location { get; set; }
}
```

```csharp
// Generated/V20251201/Models/MyResource.cs
public class MyResource
{
    public string Name { get; set; }
    public string Location { get; set; }
    public string? Description { get; set; }    // Added in 2025-12-01
}
```

```csharp
// Generated/V20260201/Models/MyResource.cs
public class MyResource
{
    public string Name { get; set; }
    public string Location { get; set; }
    public string? Description { get; set; }
    public string? ElasticPoolId { get; set; }  // Added in 2026-02-01
}
```

Each model is self-contained — it includes exactly the properties that the API exposes in that version. This means:

- **Serialization is version-correct** — a response for `api-version=2025-11-01` will never include `description`, because the model class for that version simply doesn't have the property.
- **Validation is version-specific** — required vs. optional constraints match the spec for each version.
- **Controllers reference their own version's models** — the generated abstract base for V20251201 uses V20251201's `MyResource`, not a shared model with conditional properties.

This per-version model approach avoids the complexity of a single "mega-model" with version-conditional logic, keeping each version's contract explicit and independent.

### Implementing Business Logic

Developers write a concrete controller that inherits from the generated abstract base:

```csharp
// Your code — survives regeneration
public class MyResourceController : MyResourceControllerBase
{
    public override async Task<IActionResult> CreateOrUpdate(
        string resourceGroupName,
        string resourceName,
        MyResource body,
        CancellationToken cancellationToken)
    {
        // Business logic: validate, persist, return response
        var result = await _service.CreateOrUpdateAsync(
            resourceGroupName, resourceName, body, cancellationToken);
        return Ok(result);
    }
}
```

Versioning attributes are on the generated base class, so the concrete class inherits them automatically. The routing framework resolves requests the same way it does today — no changes to the routing infrastructure are needed.

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
│     tsp compile . --emit @azure-tools/typespec-server-emitter   │
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
│     ├── Extend generated abstract base classes                  │
│     ├── Implement new or changed operations                     │
│     └── Reuse existing logic where operations didn't change     │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  5. Test & Deploy                                               │
│     ├── Contract tests to validate spec compliance              │
│     ├── Integration tests                                       │
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

**If the spec is unchanged** since the last generation, the regenerated output is identical — a no-op diff.

**If the spec changed incrementally** (e.g., a new property added with `@added`, a new operation introduced), the emitter regenerates only the affected version directories. Generated abstract base classes gain new or updated method signatures. Concrete controllers that already implement the previous signatures continue to compile — the new abstract methods require implementation, and the compiler tells the developer exactly which methods need to be added.

**If the spec changed in a breaking way** (e.g., a property type changed, a required parameter was removed, an operation was renamed), the regenerated abstract base classes will have different signatures than before. Existing concrete controllers that override those methods will **fail to compile** — the compiler surfaces every broken override as an error. This is by design: breaking spec changes are immediately visible as build failures in the business logic layer, forcing the developer to reconcile them before the code can ship.

---

## Design Considerations

### 1. Impact Analysis Is Automated

Today, developers must manually figure out which operations are impacted by a model change. With TypeSpec, this analysis is built into the emitter — it walks the type graph from the changed type to all operations that reference it, and generates incremental controllers accordingly.

### 2. Generated vs. Custom Code Separation

Generated code and hand-written code must be clearly separated. There are two common layout options:

**Option A: Top-level split** — a single `Generated/` directory alongside team-owned version directories:

```
Project/
├── Generated/                              ← Emitter-owned (overwritten)
│   ├── V20251101/
│   │   ├── Controllers/
│   │   │   └── MyResourceControllerBase.cs  ← Generated abstract base
│   │   └── Models/
│   │       └── MyResource.cs                ← Generated model
│   └── V20260201/
│       ├── Controllers/
│       │   └── MyResourceControllerBase.cs  ← Generated abstract base
│       └── Models/
│           └── MyResource.cs                ← Generated model
│
├── V20251101/                              ← Team-owned (never touched)
│   └── Controllers/
│       └── MyResourceController.cs          ← Your concrete implementation
│
└── V20260201/                              ← Team-owned (never touched)
    └── Controllers/
        └── MyResourceController.cs          ← Your concrete implementation
```

**Option B: Per-version split** — each version directory contains both generated and hand-written code side by side:

```
Project/
├── V20251101/
│   ├── Generated/                          ← Emitter-owned (overwritten)
│   │   ├── Controllers/
│   │   │   └── MyResourceControllerBase.cs  ← Generated abstract base
│   │   └── Models/
│   │       └── MyResource.cs                ← Generated model
│   └── Controllers/                        ← Team-owned (never touched)
│       └── MyResourceController.cs          ← Your concrete implementation
│
└── V20260201/
    ├── Generated/                          ← Emitter-owned (overwritten)
    │   ├── Controllers/
    │   │   └── MyResourceControllerBase.cs  ← Generated abstract base
    │   └── Models/
    │       └── MyResource.cs                ← Generated model
    └── Controllers/                        ← Team-owned (never touched)
        └── MyResourceController.cs          ← Your concrete implementation
```

The general principle is the same regardless of layout: generated code is owned by the emitter and may be overwritten at any time; hand-written business logic lives in separate files and is never touched by the emitter.

### 3. Breaking Change Detection

The TypeSpec compiler can detect breaking changes at spec-review time:

- Removing a required property without `@removed` decorator
- Changing a property type without `@typeChangedFrom`
- Removing an operation

This catches issues **before code generation**, shifting errors left in the development process.

> 📖 **For full versioning decorator details, see [@typespec/versioning](https://github.com/microsoft/typespec/tree/main/packages/versioning)**


