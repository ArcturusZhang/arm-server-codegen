import { EmitContext } from "@typespec/compiler";
import { SourceDirectory } from "@alloy-js/core";
import { Output, writeOutput } from "@typespec/emitter-framework";
import type { ServerEmitterOptions } from "./lib.js";
import { analyzeVersionImpact } from "./analyze.js";
import { ImpactAnalysisReport } from "./report.js";
import { ModelFile } from "./components/model-file.js";
import { ControllerFile } from "./components/controller-file.js";

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

  // Collect models from the snapshot
  const models = Array.from(report.snapshot.models.values());

  // Determine the route from the first impacted operation's interface
  const route = getResourceRoute();

  // Get the interface name from operations (e.g., "Databases" from "Databases.get")
  const interfaceNames = getImpactedInterfaceNames(report);

  await writeOutput(
    context.program,
    <Output program={context.program}>
      <ImpactAnalysisReport report={report} />
      <SourceDirectory path="Models">
        {models.map((m) => (
          <ModelFile model={m.model} namespace={modelsNamespace} />
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
            route={route}
          />
        ))}
      </SourceDirectory>
    </Output>,
    context.emitterOutputDir,
  );
}

function getResourceRoute(): string {
  return "subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Sql/servers/{serverName}/databases";
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
