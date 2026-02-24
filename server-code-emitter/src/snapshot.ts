import { Model, Namespace, Operation, Type } from "@typespec/compiler";
import type {
  VersionSnapshot,
  OperationSnapshot,
  ModelSnapshot,
  OperationImpact,
} from "./types.js";

export function extractSnapshot(ns: Namespace): VersionSnapshot {
  const operations: OperationSnapshot[] = [];
  const models = new Map<string, ModelSnapshot>();

  for (const [, iface] of ns.interfaces) {
    for (const [, op] of iface.operations) {
      operations.push({
        name: `${iface.name}.${op.name}`,
        fingerprint: fingerprintOperation(op),
        operation: op,
      });
    }
  }

  for (const [, op] of ns.operations) {
    operations.push({
      name: op.name,
      fingerprint: fingerprintOperation(op),
      operation: op,
    });
  }

  for (const [, model] of ns.models) {
    models.set(model.name, {
      name: model.name,
      fingerprint: fingerprintModel(model),
      model: model,
    });
  }

  return { operations, models };
}

export function diffSnapshots(
  prev: VersionSnapshot,
  curr: VersionSnapshot,
): OperationImpact[] {
  const impacts: OperationImpact[] = [];
  const prevOpMap = new Map(prev.operations.map((o) => [o.name, o]));
  const currOpMap = new Map(curr.operations.map((o) => [o.name, o]));

  for (const [name, currOp] of currOpMap) {
    const prevOp = prevOpMap.get(name);
    if (!prevOp) {
      impacts.push({
        operationName: name,
        reason: "new operation (not present in previous version)",
        operation: currOp.operation,
      });
    } else if (prevOp.fingerprint !== currOp.fingerprint) {
      impacts.push({
        operationName: name,
        reason: `signature changed:\n      prev: ${prevOp.fingerprint}\n      curr: ${currOp.fingerprint}`,
        operation: currOp.operation,
      });
    }
  }

  for (const [name] of prevOpMap) {
    if (!currOpMap.has(name)) {
      impacts.push({
        operationName: name,
        reason: "removed in this version",
      });
    }
  }

  return impacts;
}

function fingerprintOperation(op: Operation): string {
  const seen = new Set<Type>();
  const params = fingerprintModel(op.parameters, seen);
  const returnType = fingerprintType(op.returnType, seen);
  return `(${params}) => ${returnType}`;
}

function fingerprintModel(model: Model, seen: Set<Type> = new Set()): string {
  if (seen.has(model)) return `<circular:${model.name || "anonymous"}>`;
  seen.add(model);
  const props: string[] = [];
  for (const [name, prop] of model.properties) {
    props.push(
      `${name}${prop.optional ? "?" : ""}: ${fingerprintType(prop.type, seen)}`,
    );
  }
  seen.delete(model);
  return `{ ${props.join(", ")} }`;
}

function fingerprintType(type: Type, seen: Set<Type> = new Set()): string {
  switch (type.kind) {
    case "Scalar":
      return type.name;
    case "Model": {
      if (type.indexer && type.name === "Array") {
        return `${fingerprintType(type.indexer.value, seen)}[]`;
      }
      if (
        type.name &&
        type.name !== "" &&
        !type.name.startsWith("(anonymous")
      ) {
        return `${type.name}${fingerprintModel(type, seen)}`;
      }
      return fingerprintModel(type, seen);
    }
    case "Enum":
      return type.name;
    case "Union": {
      const variants: string[] = [];
      for (const [, v] of type.variants) {
        variants.push(fingerprintType(v.type, seen));
      }
      return variants.join(" | ");
    }
    case "Tuple": {
      const elements = type.values.map((v) => fingerprintType(v, seen));
      return `[${elements.join(", ")}]`;
    }
    default:
      return type.kind;
  }
}
