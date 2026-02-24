export interface OperationImpact {
  operationName: string;
  reason: string;
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
}

export interface ModelSnapshot {
  name: string;
  fingerprint: string;
}
