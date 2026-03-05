import * as cs from "@alloy-js/csharp";
import Generic from "@alloy-js/csharp/global/System/Collections/Generic";
import Threading from "@alloy-js/csharp/global/System/Threading";
import Tasks from "@alloy-js/csharp/global/System/Threading/Tasks";
import { code, Children, For } from "@alloy-js/core";
import { Model, Operation, Program, Type } from "@typespec/compiler";
import { getHttpOperation, HttpOperation } from "@typespec/http";
import { TypeExpression } from "@typespec/emitter-framework/csharp";
import { Mvc, AspVersioning } from "../lib/aspnet.js";
import { ServerCodeSourceFile } from "./source-file.js";
import type { OperationImpact } from "../types.js";

export interface ControllerFileProps {
  version: string;
  namespace: string;
  interfaceName: string;
  impactedOperations: OperationImpact[];
  program: Program;
}

const httpVerbToAttribute = {
  get: Mvc.HttpGetAttribute,
  put: Mvc.HttpPutAttribute,
  post: Mvc.HttpPostAttribute,
  patch: Mvc.HttpPatchAttribute,
  delete: Mvc.HttpDeleteAttribute,
} as const;

export function ControllerFile(props: ControllerFileProps): Children {
  const {
    version,
    namespace,
    interfaceName,
    impactedOperations,
    program,
  } = props;
  const className = `${interfaceName}ControllerBase`;

  return (
    <ServerCodeSourceFile path={`${className}.cs`}>
      <cs.Namespace name={namespace}>
        <cs.ClassDeclaration
          public
          abstract
          name={className}
          baseType={Mvc.ControllerBase}
          attributes={[
            { name: Mvc.ApiControllerAttribute },
            { name: AspVersioning.ApiVersionAttribute, args: [`"${version}"`] },
          ]}
        >
          <For
            each={impactedOperations.filter(
              (op) => op.operation !== undefined,
            )}
            doubleHardline
          >
            {(op) => (
              <ControllerMethod operation={op.operation!} program={program} />
            )}
          </For>
        </cs.ClassDeclaration>
      </cs.Namespace>
    </ServerCodeSourceFile>
  );
}

interface ControllerMethodProps {
  operation: Operation;
  program: Program;
}

function ControllerMethod(props: ControllerMethodProps): Children {
  const { operation, program } = props;
  const [httpOp] = getHttpOperation(program, operation);

  const httpAttribute =
    httpVerbToAttribute[httpOp.verb as keyof typeof httpVerbToAttribute] ??
    Mvc.HttpGetAttribute;

  // Build method parameters from HTTP operation
  const params = buildMethodParameters(httpOp);

  // Determine method name (PascalCase from operation name)
  const methodName = pascalCase(operation.name);

  // Determine return type based on response body
  const returnType = buildReturnType(httpOp);

  return (
    <cs.Method
      public
      abstract
      name={methodName}
      returns={returnType}
      attributes={[
        { name: Mvc.RouteAttribute, args: [`"${httpOp.path}"`] },
        { name: httpAttribute },
      ]}
      parameters={params}
    />
  );
}

// Parameters to exclude — handled by the routing framework, not method signatures
const excludedParams = new Set(["api-version", "apiVersion"]);

/**
 * Determine the return type for a controller method based on the HTTP response body.
 * - DELETE (no body) → Task<IActionResult>
 * - Returns array → Task<ActionResult<IEnumerable<T>>>
 * - Returns model → Task<ActionResult<T>>
 */
function buildReturnType(httpOp: HttpOperation): Children {
  // Find the success response body type (2xx)
  const responseBodyType = getSuccessResponseBodyType(httpOp);

  // Only use ActionResult<T> for named model types from the service namespace
  if (!responseBodyType || responseBodyType.kind !== "Model" || !responseBodyType.name) {
    return code`${Tasks.Task}<${Mvc.IActionResult}>`;
  }

  const model = responseBodyType as Model;

  // Check if the response is an array (Model with an integer indexer)
  if (isArrayType(model)) {
    const elementType = getArrayElementType(model);
    if (elementType && elementType.kind === "Model" && elementType.name && isServiceModel(elementType as Model)) {
      return code`${Tasks.Task}<${Mvc.ActionResult}<${Generic.IEnumerable}<${<TypeExpression type={elementType} />}>>>`;
    }
    return code`${Tasks.Task}<${Mvc.IActionResult}>`;
  }

  // Check for ARM page wrapper (e.g., ResourceListResult with a `value` array property)
  const listElementType = getPageElementType(model);
  if (listElementType && isServiceModel(listElementType as Model)) {
    return code`${Tasks.Task}<${Mvc.ActionResult}<${Generic.IEnumerable}<${<TypeExpression type={listElementType} />}>>>`;
  }
  if (listElementType) {
    return code`${Tasks.Task}<${Mvc.IActionResult}>`;
  }

  // Single model response — only use typed return if it's a service model
  if (!isServiceModel(model)) {
    return code`${Tasks.Task}<${Mvc.IActionResult}>`;
  }
  return code`${Tasks.Task}<${Mvc.ActionResult}<${<TypeExpression type={model} />}>>`;
}

/**
 * Check if a model belongs to the service namespace (not an ARM/Azure infrastructure type).
 * Service models are defined in the service namespace, while ARM types like
 * ErrorResponse, Operation, etc. come from Azure.ResourceManager or Azure.Core.
 */
function isServiceModel(model: Model): boolean {
  let ns = model.namespace;
  while (ns) {
    const name = ns.name;
    if (name === "Azure" || name === "Autorest") return false;
    ns = ns.namespace;
  }
  return true;
}

function getSuccessResponseBodyType(httpOp: HttpOperation): Type | undefined {
  for (const response of httpOp.responses) {
    const statusCodes = response.statusCodes;
    // Match specific 2xx success codes (skip wildcard "*" and 204 No Content)
    const isSuccess =
      (typeof statusCodes === "number" &&
        statusCodes >= 200 &&
        statusCodes < 300 &&
        statusCodes !== 204) ||
      (typeof statusCodes === "object" &&
        "start" in statusCodes &&
        statusCodes.start >= 200 &&
        statusCodes.start < 300);

    if (isSuccess) {
      for (const content of response.responses) {
        if (content.body && content.body.bodyKind === "single") {
          return content.body.type;
        }
      }
    }
  }
  return undefined;
}

function isArrayType(type: Type): type is Model {
  return (
    type.kind === "Model" &&
    !!(type as Model).indexer &&
    (type as Model).indexer!.key.name === "integer"
  );
}

function getArrayElementType(type: Model): Type | undefined {
  return type.indexer?.value;
}

/**
 * Detect ARM page wrapper models (e.g., ResourceListResult) that have a `value`
 * property which is an array. Returns the array element type if found.
 */
function getPageElementType(model: Model): Type | undefined {
  const valueProp = model.properties.get("value");
  if (
    valueProp &&
    valueProp.type.kind === "Model" &&
    isArrayType(valueProp.type)
  ) {
    const elementType = getArrayElementType(valueProp.type);
    if (elementType && elementType.kind === "Model" && elementType.name) {
      return elementType;
    }
  }
  return undefined;
}

function buildMethodParameters(
  httpOp: ReturnType<typeof getHttpOperation>[0],
): cs.ParameterProps[] {
  const pathParams: cs.ParameterProps[] = [];
  const queryParams: cs.ParameterProps[] = [];
  const headerParams: cs.ParameterProps[] = [];

  // Classify parameters by location in a single pass
  for (const param of httpOp.parameters.parameters) {
    if (excludedParams.has(param.name)) continue;
    switch (param.type) {
      case "path":
        pathParams.push({ name: param.name, type: "string" });
        break;
      case "query":
        queryParams.push({ name: param.name, type: "string" });
        break;
      case "header":
        headerParams.push({
          name: param.name,
          type: "string",
          attributes: [{ name: Mvc.FromHeaderAttribute }],
        });
        break;
    }
  }

  // Assemble in order: path, query, header, body, cancellationToken
  const params: cs.ParameterProps[] = [
    ...pathParams,
    ...queryParams,
    ...headerParams,
  ];

  // Add body parameter if present
  if (httpOp.parameters.body) {
    const bodyType = httpOp.parameters.body.type;
    params.push({
      name: "body",
      type: code`${<TypeExpression type={bodyType} />}`,
      attributes: [{ name: Mvc.FromBodyAttribute }],
    });
  }

  // Add CancellationToken
  params.push({
    name: "cancellationToken",
    type: code`${Threading.CancellationToken}`,
  });

  return params;
}

function pascalCase(name: string): string {
  return name.charAt(0).toUpperCase() + name.slice(1);
}
