import { EmitContext } from "@typespec/compiler";
import { SourceFile } from "@alloy-js/core";
import { Output, writeOutput } from "@typespec/emitter-framework";
import type { ServerEmitterOptions } from "./lib.js";
import { analyzeVersionImpact, formatReport } from "./analyze.js";

import { $lib } from "./lib.js";
export { $lib } from "./lib.js";

export async function $onEmit(context: EmitContext<ServerEmitterOptions>) {
  if (context.program.compilerOptions.noEmit) return;

  const report = analyzeVersionImpact(context.program, context.options["version"]);
  if (!report) return;

  const reportContent = formatReport(report);

  await writeOutput(
    context.program,
    <Output program={context.program}>
      <SourceFile path="impact-analysis.txt" filetype="txt">
        {reportContent}
      </SourceFile>
    </Output>,
    context.emitterOutputDir,
  );
}
