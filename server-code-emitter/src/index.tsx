import { EmitContext, Enum, Model, Union } from "@typespec/compiler";
import { SourceDirectory } from "@alloy-js/core";
import { createCSharpNamePolicy } from "@alloy-js/csharp";
import { Output, writeOutput } from "@typespec/emitter-framework";
import { getHttpOperation } from "@typespec/http";
import type { ServerEmitterOptions } from "./lib.js";
import { analyzeVersionImpact } from "./analyze.js";
import { ImpactAnalysisReport } from "./report.js";
import { ModelFile } from "./components/model-file.js";
import { ControllerFile } from "./components/controller-file.js";
import { EnumFile } from "./components/enum-file.js";
import type { ModelSnapshot } from "./types.js";

import { $lib } from "./lib.js";
export { $lib } from "./lib.js";

export async function $onEmit(context: EmitContext<ServerEmitterOptions>) {
  if (context.program.compilerOptions.noEmit) return;

  const report = analyzeVersionImpact(
    context.program,
    context.options["version"],
  );
  if (!report) return;

  const versionTag = report.version.replace(/-/g, "");
  const modelsNamespace = `Generated.V${versionTag}.Models`;
  const controllersNamespace = `Generated.V${versionTag}.Controllers`;

  // Collect body parameter models from impacted operations that aren't
  // already in the namespace (e.g., DatabaseUpdate from ResourceUpdateModel)
  const bodyModels: Model[] = [];
  for (const op of report.impactedOperations) {
    if (op.operation) {
      const [httpOp] = getHttpOperation(context.program, op.operation);
      const bodyType = httpOp.parameters.body?.type;
      if (
        bodyType?.kind === "Model" &&
        bodyType.name &&
        !report.snapshot.models.has(bodyType.name)
      ) {
        bodyModels.push(bodyType);
      }
    }
  }

  // Collect models and enums/unions from the full dependency tree
  const { models, enums } = collectTypeDependencies(
    report.snapshot.models,
    bodyModels,
  );

  // Close extensible unions by removing the open scalar variant so the
  // emitter-framework can represent them as enums. We mutate the projected
  // union (safe — it's a versioned copy) to preserve object identity for refkeys.
  for (const e of enums) {
    if (e.kind === "Union") {
      closeExtensibleUnion(e);
    }
  }

  // Get the interface name from operations (e.g., "Databases" from "Databases.get")
  const interfaceNames = getImpactedInterfaceNames(report);

  await writeOutput(
    context.program,
    <Output program={context.program} namePolicy={createCSharpNamePolicy()}>
      <ImpactAnalysisReport report={report} />
      <SourceDirectory path="Models">
        {models.map((m) => (
          <ModelFile model={m} namespace={modelsNamespace} />
        ))}
        {enums.map((e) => (
          <EnumFile type={e} namespace={modelsNamespace} />
        ))}
      </SourceDirectory>
      <SourceDirectory path="Controllers">
        {interfaceNames.map((ifaceName) => (
          <ControllerFile
            version={report.version}
            namespace={controllersNamespace}
            interfaceName={ifaceName}
            impactedOperations={report.impactedOperations.filter((op) =>
              op.operationName.startsWith(`${ifaceName}.`),
            )}
            program={context.program}
          />
        ))}
      </SourceDirectory>
    </Output>,
    context.emitterOutputDir,
  );
}

function getImpactedInterfaceNames(report: {
  impactedOperations: { operationName: string }[];
}): string[] {
  const names = new Set<string>();
  for (const op of report.impactedOperations) {
    const dotIndex = op.operationName.indexOf(".");
    if (dotIndex > 0) {
      names.add(op.operationName.substring(0, dotIndex));
    }
  }
  return Array.from(names);
}

/**
 * Walk the full type dependency tree for every snapshot model, collecting
 * base types, property model types, and union/enum types referenced by properties.
 */
function collectTypeDependencies(
  snapshotModels: Map<string, ModelSnapshot>,
  additionalModels?: Model[],
): {
  models: Model[];
  enums: (Union | Enum)[];
} {
  const collectedModels = new Map<string, Model>();
  const collectedEnums = new Map<string, Union | Enum>();
  for (const snapshot of snapshotModels.values()) {
    walkModelDeps(snapshot.model, collectedModels, collectedEnums);
  }
  if (additionalModels) {
    for (const model of additionalModels) {
      walkModelDeps(model, collectedModels, collectedEnums);
    }
  }
  return {
    models: Array.from(collectedModels.values()),
    enums: Array.from(collectedEnums.values()),
  };
}

function walkModelDeps(
  model: Model,
  collectedModels: Map<string, Model>,
  collectedEnums: Map<string, Union | Enum>,
): void {
  if (collectedModels.has(model.name)) return;
  collectedModels.set(model.name, model);
  if (model.baseModel) {
    walkModelDeps(model.baseModel, collectedModels, collectedEnums);
  }
  for (const [, prop] of model.properties) {
    if (prop.type.kind === "Model" && prop.type.name) {
      // Skip collection types (Record/Array) — they map to built-in C# types
      if (!prop.type.indexer) {
        walkModelDeps(prop.type, collectedModels, collectedEnums);
      }
    } else if (prop.type.kind === "Union" && prop.type.name) {
      if (hasStringLiteralVariants(prop.type)) {
        collectedEnums.set(prop.type.name, prop.type);
      }
    } else if (prop.type.kind === "Enum" && prop.type.name) {
      collectedEnums.set(prop.type.name, prop.type);
    }
  }
}

/** A union has enum-representable content if it contains at least one string literal variant. */
function hasStringLiteralVariants(union: Union): boolean {
  for (const [, variant] of union.variants) {
    if (variant.type.kind === "String") {
      return true;
    }
  }
  return false;
}

/**
 * Remove the open `string` scalar variant from an extensible union,
 * making it representable as a C# enum. Mutates the union in place
 * to preserve object identity for the refkey system.
 */
function closeExtensibleUnion(union: Union): void {
  for (const [key, variant] of union.variants) {
    if (variant.type.kind !== "String") {
      union.variants.delete(key);
    }
  }
}
