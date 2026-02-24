import { EmitContext } from "@typespec/compiler";
import { Output, writeOutput } from "@typespec/emitter-framework";
import type { ServerEmitterOptions } from "./lib.js";
import { analyzeVersionImpact } from "./analyze.js";
import { ImpactAnalysisReport } from "./report.js";

import { $lib } from "./lib.js";
export { $lib } from "./lib.js";

export async function $onEmit(context: EmitContext<ServerEmitterOptions>) {
  if (context.program.compilerOptions.noEmit) return;

  const report = analyzeVersionImpact(
    context.program,
    context.options["version"],
  );
  if (!report) return;

  await writeOutput(
    context.program,
    <Output program={context.program}>
      <ImpactAnalysisReport report={report} />
    </Output>,
    context.emitterOutputDir,
  );
}
