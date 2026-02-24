import type { Operation, Model } from "@typespec/compiler";

export interface OperationImpact {
  operationName: string;
  reason: string;
  /** The operation instance from the current version's type graph. Undefined for removed operations. */
  operation?: Operation;
}

export interface VersionImpactReport {
  version: string;
  previousVersion: string | undefined;
  isFirstVersion: boolean;
  impactedOperations: OperationImpact[];
  allOperations: string[];
}

export interface VersionSnapshot {
  operations: OperationSnapshot[];
  models: Map<string, ModelSnapshot>;
}

export interface OperationSnapshot {
  name: string;
  fingerprint: string;
  operation: Operation;
}

export interface ModelSnapshot {
  name: string;
  fingerprint: string;
  model: Model;
}
