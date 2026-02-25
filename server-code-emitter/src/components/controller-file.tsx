import * as cs from "@alloy-js/csharp";
import Threading from "@alloy-js/csharp/global/System/Threading";
import Tasks from "@alloy-js/csharp/global/System/Threading/Tasks";
import { code, Children, For } from "@alloy-js/core";
import { Operation, Program } from "@typespec/compiler";
import { getHttpOperation } from "@typespec/http";
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
  route: string;
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
    route,
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
            {
              name: Mvc.RouteAttribute,
              args: [`"${route}"`],
            },
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

  // Build route suffix from the operation's path relative to the resource
  const routeSuffix = getRouteSuffix(httpOp.path);
  const attrArgs = routeSuffix ? [`"${routeSuffix}"`] : [];

  // Build method parameters from HTTP operation
  const params = buildMethodParameters(httpOp);

  // Determine method name (PascalCase from operation name)
  const methodName = pascalCase(operation.name);

  return (
    <cs.Method
      public
      abstract
      async
      name={methodName}
      returns={code`${Tasks.Task}<${Mvc.IActionResult}>`}
      attributes={[{ name: httpAttribute, args: attrArgs }]}
      parameters={params}
    />
  );
}

function getRouteSuffix(fullPath: string): string {
  // Extract the last segment if it contains a parameter (e.g., {databaseName})
  const segments = fullPath.split("/").filter(Boolean);
  const lastSegment = segments[segments.length - 1];
  if (lastSegment && lastSegment.startsWith("{") && lastSegment.endsWith("}")) {
    return lastSegment;
  }
  return "";
}

// Parameters to exclude — handled by the routing framework, not method signatures
const excludedParams = new Set(["api-version", "apiVersion"]);

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
