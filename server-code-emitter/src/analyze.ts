import { Namespace, Program } from "@typespec/compiler";
import { unsafe_mutateSubgraphWithNamespace as mutateSubgraphWithNamespace } from "@typespec/compiler/experimental";
import { getVersioningMutators } from "@typespec/versioning";
import type { VersionImpactReport } from "./types.js";
import { extractSnapshot, diffSnapshots } from "./snapshot.js";
import { reportDiagnostic } from "./lib.js";

export function analyzeVersionImpact(
  program: Program,
  targetVersion: string,
): VersionImpactReport | undefined {
  const serviceNs = findServiceNamespace(program);
  if (!serviceNs) return undefined;

  const versioningMutators = getVersioningMutators(program, serviceNs);
  if (!versioningMutators || versioningMutators.kind !== "versioned")
    return undefined;

  const snapshots = versioningMutators.snapshots;

  const targetIndex = snapshots.findIndex(
    (s) => s.version.value === targetVersion,
  );
  if (targetIndex === -1) {
    reportDiagnostic(program, {
      code: "unknown-version",
      format: {
        version: targetVersion,
        available: snapshots.map((s) => s.version.value).join(", "),
      },
      target: program.getGlobalNamespaceType(),
    });
    return undefined;
  }

  const { type: currentNs } = mutateSubgraphWithNamespace(
    program,
    [snapshots[targetIndex].mutator],
    serviceNs,
  );
  const currentSnapshot = extractSnapshot(currentNs as Namespace);

  if (targetIndex === 0) {
    return {
      version: targetVersion,
      previousVersion: undefined,
      isFirstVersion: true,
      impactedOperations: currentSnapshot.operations.map((op) => ({
        operationName: op.name,
        reason: "first version — all operations included",
        operation: op.operation,
      })),
      allOperations: currentSnapshot.operations.map((op) => op.name),
      snapshot: currentSnapshot,
    };
  }

  const { type: previousNs } = mutateSubgraphWithNamespace(
    program,
    [snapshots[targetIndex - 1].mutator],
    serviceNs,
  );
  const previousSnapshot = extractSnapshot(previousNs as Namespace);
  const impacted = diffSnapshots(previousSnapshot, currentSnapshot);

  return {
    version: targetVersion,
    previousVersion: snapshots[targetIndex - 1].version.value,
    isFirstVersion: false,
    impactedOperations: impacted,
    allOperations: currentSnapshot.operations.map((op) => op.name),
    snapshot: currentSnapshot,
  };
}

function findServiceNamespace(program: Program): Namespace | undefined {
  for (const [, ns] of program.getGlobalNamespaceType().namespaces) {
    if (ns.decorators.some((d) => d.decorator.name === "$service")) {
      return ns;
    }
    for (const [, sub] of ns.namespaces) {
      if (sub.decorators.some((d) => d.decorator.name === "$service")) {
        return sub;
      }
    }
  }
  return undefined;
}
