# Server Code Emitter Framework Comparison: Alloy vs. Microsoft.TypeSpec.Generator

## Background

The [server code generation design](./api-first-typespec-server-codegen-design.md) and its [Azure SQL adaptation](./api-first-typespec-azure-sql-design.md) describe an API-first workflow where TypeSpec is the single source of truth and a per-service emitter produces ASP.NET Core controllers, models, and version registries.

Two candidate frameworks exist for building such an emitter:

| | Framework | Language | Status in this repo |
|---|---|---|---|
| **Approach A** | [Alloy](https://alloy-framework.github.io/alloy/) (`@alloy-js/core`, `@alloy-js/csharp`) on top of [`@typespec/emitter-framework`](https://typespec.io/docs/extending-typespec/emitter-framework) | TypeScript / JSX | Implemented in `server-code-emitter/` |
| **Approach B** | [Microsoft.TypeSpec.Generator](https://github.com/microsoft/typespec/tree/main/packages/http-client-csharp) ("MTG") | C# (with a thin TS emitter shim) | Not implemented; mature in [`Azure/azure-sdk-for-net/eng/packages/http-client-csharp`](https://github.com/Azure/azure-sdk-for-net/tree/main/eng/packages/http-client-csharp) |

### The driving requirement

Azure SQL's server runtime is **not** general-purpose. It uses bespoke versioning attributes (`[VersionedRoute]`, `[BaseApiVersion]`, the `ApiVersion` enum with Preview/Stable categories), bespoke shared-base classes (`VCommon/BaseDatabase<T>`), and bespoke routing infrastructure (`ApiVersionControllerSelector`). Other ARM services have their own equivalent oddities.

Therefore the chosen framework **must support extension**: a generic "server code emitter" must exist as a reusable core, and each service team (SQL, Compute, Storage, …) must be able to layer service-specific behaviour on top *without forking the core*.

Specifically, the framework must let a downstream service:

1. **Replace or wrap individual outputs** — e.g., emit `[VersionedRoute]` instead of `[Route]`, emit a SQL-flavoured `ApiVersion` enum, swap the controller base class.
2. **Inject new outputs** — e.g., add a generated `Constants/RouteConstants.cs`, or a service-specific manifest file.
3. **Hook into the type / operation pipeline** — e.g., remove or rewrite types before they are emitted, or attach extra metadata (categories, max-version bounds).
4. **Be installed as a separate package** — so the SQL extension lives in the SQL repo and is versioned independently from the generic core.

This document describes how each framework supports those four needs and recommends an approach.

---

## Approach A — Alloy + `@typespec/emitter-framework`

### How the current implementation works

The emitter in `server-code-emitter/` is a TypeScript package that exposes a single TypeSpec entry point:

```ts
export async function $onEmit(context: EmitContext<ServerEmitterOptions>) {
  const report = analyzeVersionImpact(context.program, context.options.version);
  // ...
  await writeOutput(
    context.program,
    <Output program={context.program} namePolicy={createCSharpNamePolicy()}>
      <ImpactAnalysisReport report={report} />
      <SourceDirectory path="Models">
        {models.map((m) => <ModelFile model={m} namespace={modelsNs} />)}
        {enums.map((e) => <EnumFile  type={e}  namespace={modelsNs} />)}
      </SourceDirectory>
      <SourceDirectory path="Controllers">
        {interfaceNames.map((n) => <ControllerFile … />)}
      </SourceDirectory>
    </Output>,
    context.emitterOutputDir,
  );
}
```

The output tree is a JSX expression. Each leaf (`ModelFile`, `ControllerFile`, `EnumFile`) is an Alloy component that returns more JSX (C# class declarations, attributes, method signatures, etc.). Cross-file references are wired up at render time through Alloy's `refkey` system, which also handles imports and naming policies automatically.

### Extension surface today

Alloy's extension model is *compositional*, in the same spirit as React/Solid:

| Mechanism | What it enables |
|---|---|
| **Component composition** | A downstream package imports `ControllerFile` (or any child component) and wraps it: `<MyControllerFile> = <ControllerFile {...props}>{extraAttributes}</ControllerFile>`. |
| **Children/slot pattern** | A core component can accept `children` to let downstream code inject content into a specific spot (e.g., an "extra attributes" slot on `ControllerFile`). |
| **React-style `Context`** | `createContext` / `useContext` lets a wrapper provide service-specific data (e.g., a `VersioningAttributesContext`) that core components read instead of hard-coding `[ApiVersion]`. |
| **Refkeys** | A downstream package can declare its own symbols (`[VersionedRoute]`, `ApiVersion.V20260201`) and core controller code can reference them by refkey, so the SQL package owns the actual import paths. |
| **Custom name policies** | Downstream supplies its own `NamePolicy` to `<Output>` to change casing, prefixes, etc. |

### Gaps for "extensible by another team"

The current emitter was written as a single monolithic top-level `$onEmit`. To make it extensible **as a library** that SQL can depend on, the following changes are required — none of which Alloy provides out of the box:

1. **There is no plugin/registration system.** Alloy itself doesn't define "an emitter" — it only renders a JSX tree to files. Today, the entry point `$onEmit` *is* the extension point, and there is exactly one. To support a SQL extension, we have to invent the contract: e.g., expose `renderServerCode(context, { controllerComponent, apiVersionEnumComponent, … })` so SQL can pass replacements, and ship `$onEmit` only as a default wrapper.
2. **All outputs must be parameterised.** Every place that hard-codes `[Route]`, `[ApiVersion]`, `ControllerBase`, the namespace layout, or the file names has to be replaced with a context value or a slot. The current `controller-file.tsx` hard-codes `Mvc.HttpGetAttribute`, `AspVersioning`, and the `…ControllerBase` suffix; SQL would need every one of those to be overridable.
3. **No standard "transform the type graph" hook.** Alloy operates on whatever the caller passes it; there is no equivalent of a TypeProvider visitor. Cross-cutting changes (e.g., "split every union with a string variant into a closed enum", which the current code does by mutating union variants in `index.tsx`) live in the top-level emitter. A SQL extension that wants different behaviour has to fork that logic or run before/after a published transform pipeline that we'd have to design.
4. **No first-class "post-emit customization" story.** Alloy renders straight to text. There is no equivalent of MTG's `partial class` + `[CodeGenType]/[CodeGenMember]` customization mechanism for end-developers to override individual generated members. A team that wants to hand-tweak one method has to either modify the spec, edit a downstream component, or post-process the output.

### Achievable extension architecture (with work)

A workable design exists, but it is a convention we would design ourselves:

```
@azure-tools/typespec-server-emitter-core   (generic Alloy components + contexts)
        ▲
        │ imports + composes
        │
@azure-tools/typespec-server-emitter-sql    (SQL extension)
   ├── overrides VersioningAttributesContext → [VersionedRoute], [BaseApiVersion]
   ├── replaces ApiVersionEnumFile component
   ├── wraps ControllerFile to add [BaseApiVersion] x2 (Preview/Stable)
   └── exposes its own $onEmit
```

This works, but every extension point is a hand-rolled convention. There is no framework-enforced contract, no compile-time guarantee that a wrapper is feature-complete, and no shared customization vocabulary across teams.

---

## Approach B — Microsoft.TypeSpec.Generator (MTG)

### How MTG works

MTG splits the pipeline in two:

1. A **TypeScript emitter shim** runs inside `tsp compile`, walks the TypeSpec program (using `@azure-tools/typespec-client-generator-core`), and serializes a language-agnostic `tspCodeModel.json`.
2. A **.NET generator** is invoked as a subprocess. It reads `tspCodeModel.json` into a strongly-typed C# object graph (`InputClient`, `InputModelType`, `InputOperation`, …), constructs an in-memory C# code model via `TypeProvider` objects (`ModelProvider`, `ClientProvider`, `MethodProvider`, …), runs registered visitors over it, and finally writes `.cs` files via the Roslyn-based code writer.

MTG is what `@azure-typespec/http-client-csharp` (Azure's data-plane SDK generator) is built on. `Azure.Generator` itself extends the upstream `ScmCodeModelGenerator`, which proves the pattern works in production.

### Extension surface

MTG is designed around extension. It provides four well-defined mechanisms:

| Mechanism | Description |
|---|---|
| **`CodeModelGenerator` subclassing** | A downstream generator declares `class SqlServerGenerator : ServerCodeModelGenerator` and overrides factory methods (e.g., `CreateModelProvider`, `CreateClientProvider`) to return service-specific TypeProvider subclasses. The `generator-name` emitter option selects which generator class to load. |
| **`GeneratorPlugin` DLLs** | The `plugins` emitter option points at one or more compiled assemblies. Each plugin ships a class extending `GeneratorPlugin` and is composed into the pipeline alongside the base generator. This is how SQL would package its extension as a NuGet/DLL. |
| **`LibraryVisitor` pattern** | Visitors run after the code model is built and can rewrite/insert/remove any TypeProvider, method, property, or attribute. This is the canonical place for cross-cutting changes (e.g., "rewrite every `[Route]` to `[VersionedRoute]`"). |
| **Spec-author and end-developer customization** | `[CodeGenType("Original")]` / `[CodeGenMember("Original")]` partial-class attributes let humans rename, retype, internalize, or replace any generated member without touching the generator. Documented in the [http-client-csharp customization guide](https://github.com/microsoft/typespec/blob/main/packages/http-client-csharp/.tspd/docs/customization.md). |

### What a SQL extension looks like in MTG

```
Microsoft.TypeSpec.Generator.ServerCode        (generic)
   ├── ServerCodeModelGenerator              : CodeModelGenerator
   ├── ControllerProvider                    : TypeProvider
   ├── VersionRegistryProvider               : TypeProvider
   └── default LibraryVisitors

Microsoft.TypeSpec.Generator.ServerCode.Sql   (SQL extension, separate NuGet)
   ├── SqlServerCodeModelGenerator           : ServerCodeModelGenerator
   │     overrides CreateControllerProvider   → SqlControllerProvider
   │     overrides CreateVersionRegistryProvider → SqlApiVersionEnumProvider
   ├── SqlVersionedRouteVisitor              : LibraryVisitor
   │     rewrites [Route] / [ApiVersion] → [VersionedRoute] / [BaseApiVersion]
   └── SqlPreviewStableCategoryVisitor       : LibraryVisitor
         duplicates each [BaseApiVersion] as Preview + Stable
```

`tspconfig.yaml` then activates the SQL generator with one line:

```yaml
options:
  "@azure-tools/typespec-server-emitter":
    generator-name: SqlServerCodeModelGenerator
    plugins: [./Microsoft.TypeSpec.Generator.ServerCode.Sql.dll]
```

### Trade-offs

- **Two-process architecture.** Debugging spans TypeScript and C#; a developer change to a generator requires a .NET build. Mitigation: MTG already supports `--debug` to attach to the C# generator.
- **C# code model is a layer of indirection.** Generated output is built by mutating `TypeProvider` objects, not by writing JSX that "looks like" the output. This is less direct than Alloy but exactly what makes visitor-based extension safe.
- **Tied to the .NET ecosystem.** MTG cannot generate Java/Python/etc. For server codegen across multiple stacks this would be a real cost; for ARM (which is .NET-only on the service side) it is a non-issue.

---

## Side-by-side comparison

| Capability | Alloy + emitter-framework | Microsoft.TypeSpec.Generator |
|---|---|---|
| **Output directness** | Very high — JSX components mirror the file shape | Medium — output goes through a C# code model |
| **Cross-file refs / imports / naming** | Built-in (`refkey`, name policies) | Built-in (Roslyn-based writer) |
| **Service-team extension as a package** | No built-in contract; we must design our own component / context conventions | First-class: `CodeModelGenerator` subclass + `GeneratorPlugin` DLLs |
| **Replace one piece of output** | Component composition / context override (per convention) | Override one factory method or register one visitor |
| **Add new outputs** | Append a JSX child to the tree (downstream wraps `<Output>`) | Add a new `TypeProvider` in the generator subclass |
| **Cross-cutting transforms** | None standard — write your own pre-/post-processor | `LibraryVisitor` pipeline |
| **End-developer customization (per spec)** | Not supported — spec-only or fork the component | `partial class` + `[CodeGenType]/[CodeGenMember]` |
| **Multi-language reuse of core** | Yes (Alloy supports C#/Java/TS) | No — .NET only |
| **Maturity for this use case** | Pre-beta framework; current emitter is the only consumer | Powers production Azure SDK for .NET; battle-tested extension model |
| **Skill set required** | TS + JSX | TS shim + C# generator |
| **Closest precedent for "service-specific extension"** | None published | `Azure.Generator` extending `ScmCodeModelGenerator` in `Azure/azure-sdk-for-net/eng/packages/http-client-csharp` |

---

## Recommendation

For a server code emitter whose **explicit charter** is to host service-specific extensions (SQL first, others later), **Microsoft.TypeSpec.Generator is the better fit**.

The decisive factor is not output quality — both can produce identical C# — it is the *contract* the framework defines for an extending team:

- MTG ships a documented, versioned, three-layer extension model (subclass / plugin / visitor) plus an end-developer customization vocabulary (`partial class` + `CodeGen*` attributes). The Azure SDK's `Azure.Generator` is concrete proof that a downstream team can layer significant behaviour on top of an upstream generator without forking it.
- Alloy is a code-rendering framework, not an emitter framework. It can be made extensible, but every extension point would be a convention we invent and maintain ourselves, with no framework-level guarantees and no precedent of another team actually doing it. The work to harden the existing emitter into a "core + SQL extension" package pair is non-trivial and would essentially mean re-inventing what MTG already provides.

### Suggested path forward

1. **Keep the current Alloy emitter as a working prototype.** It already validates the spec design (impact analysis, incremental controllers, version-specific models) end-to-end and is useful for spec-side iteration.
2. **Build the production emitter on MTG**, structured as:
   - `Microsoft.TypeSpec.Generator.ServerCode` — the generic core (controller, model, version-registry providers; default visitors).
   - `Microsoft.TypeSpec.Generator.ServerCode.Sql` — the SQL extension (subclassed generator + visitors that emit `[VersionedRoute]`, `[BaseApiVersion]`, the SQL `ApiVersion` enum, and Preview/Stable duplication).
3. **Pilot with one Azure SQL resource type** (`Database`), per the [SQL design's pilot plan](./api-first-typespec-azure-sql-design.md#pilot-plan), and use it to validate that the extension surface is sufficient before onboarding additional teams.

### When to reconsider Alloy

If a future requirement emerges to generate **server code in multiple languages from the same emitter** (e.g., Go or Java services), Alloy's multi-language story becomes a meaningful advantage and would justify revisiting this decision.

---

## References

- [General API-First Server Code Generation Design](./api-first-typespec-server-codegen-design.md)
- [Azure SQL adaptation](./api-first-typespec-azure-sql-design.md)
- [Alloy framework](https://alloy-framework.github.io/alloy/) — [basic concepts](https://alloy-framework.github.io/alloy/guides/basic-concepts/)
- [`@typespec/emitter-framework`](https://typespec.io/docs/extending-typespec/emitter-framework)
- [`http-client-csharp` (MTG core)](https://github.com/microsoft/typespec/tree/main/packages/http-client-csharp)
- [Azure SDK for .NET — `eng/packages/http-client-csharp` (production MTG extension)](https://github.com/Azure/azure-sdk-for-net/tree/main/eng/packages/http-client-csharp)
- [MTG end-developer customization guide](https://github.com/microsoft/typespec/blob/main/packages/http-client-csharp/.tspd/docs/customization.md)
