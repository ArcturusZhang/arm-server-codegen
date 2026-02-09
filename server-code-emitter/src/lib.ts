import { createTypeSpecLibrary, JSONSchemaType, paramMessage } from "@typespec/compiler";

export interface ServerEmitterOptions {
  /** The API version to analyze (e.g. "2024-06-01"). Compares against its previous version. */
  "version": string;
}

const EmitterOptionsSchema: JSONSchemaType<ServerEmitterOptions> = {
  type: "object",
  additionalProperties: false,
  properties: {
    "version": { type: "string" },
  },
  required: ["version"],
};

export const $lib = createTypeSpecLibrary({
  name: "@azure-tools/typespec-server-emitter",
  diagnostics: {
    "unknown-version": {
      severity: "error",
      messages: {
        default: paramMessage`Version "${"version"}" not found. Available versions: ${"available"}`,
      },
    },
  },
  state: {},
  emitter: {
    options: EmitterOptionsSchema,
  },
} as const);

export const { reportDiagnostic, createDiagnostic } = $lib;
