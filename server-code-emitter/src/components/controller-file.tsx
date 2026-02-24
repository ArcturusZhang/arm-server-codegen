import * as cs from "@alloy-js/csharp";
import { code, Children } from "@alloy-js/core";
import { Operation, Program } from "@typespec/compiler";
import { getHttpOperation } from "@typespec/http";
import { TypeExpression } from "@typespec/emitter-framework/csharp";
import type { OperationImpact } from "../types.js";

export interface ControllerFileProps {
  version: string;
  namespace: string;
  interfaceName: string;
  impactedOperations: OperationImpact[];
  program: Program;
  route: string;
}

const httpVerbToAttribute: Record<string, string> = {
  get: "HttpGet",
  put: "HttpPut",
  post: "HttpPost",
  patch: "HttpPatch",
  delete: "HttpDelete",
};

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
    <cs.SourceFile
      path={`${className}.cs`}
      using={[
        "System.Threading",
        "System.Threading.Tasks",
        "Asp.Versioning",
        "Microsoft.AspNetCore.Mvc",
      ]}
    >
      <cs.Namespace name={namespace}>
        <cs.ClassDeclaration
          public
          abstract
          name={className}
          baseType="ControllerBase"
          attributes={[
            { name: "ApiController" },
            { name: "ApiVersion", args: [`"${version}"`] },
            {
              name: "Route",
              args: [`"${route}"`],
            },
          ]}
        >
          {impactedOperations
            .filter((op) => op.operation !== undefined)
            .map((op) => (
              <ControllerMethod operation={op.operation!} program={program} />
            ))}
        </cs.ClassDeclaration>
      </cs.Namespace>
    </cs.SourceFile>
  );
}

interface ControllerMethodProps {
  operation: Operation;
  program: Program;
}

function ControllerMethod(props: ControllerMethodProps): Children {
  const { operation, program } = props;
  const [httpOp] = getHttpOperation(program, operation);

  const verb = httpOp.verb;
  const httpAttribute = httpVerbToAttribute[verb] ?? "HttpGet";

  // Build route suffix from the operation's path relative to the resource
  const routeSuffix = getRouteSuffix(httpOp.path);
  const attrArgs = routeSuffix ? [`"${routeSuffix}"`] : [];

  // Build method parameters
  const params = buildMethodParameters(httpOp);

  // Determine method name (PascalCase from operation name)
  const methodName = pascalCase(operation.name);

  return (
    <cs.Method
      public
      abstract
      async
      name={methodName}
      returns="Task<IActionResult>"
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

interface HttpOperation {
  verb: string;
  path: string;
  parameters: {
    parameters: Array<{
      type: string;
      name: string;
      param: { type: { kind: string } };
    }>;
    body?: { type: { kind: string } };
  };
}

// Parameters to exclude — handled by the routing framework, not method signatures
const excludedParams = new Set(["api-version", "apiVersion", "provider"]);

function buildMethodParameters(
  httpOp: ReturnType<typeof getHttpOperation>[0],
): cs.ParameterProps[] {
  const params: cs.ParameterProps[] = [];

  // Add path and query parameters (excluding framework-level ones)
  for (const param of httpOp.parameters.parameters) {
    if (excludedParams.has(param.name)) continue;
    if (param.type === "path" || param.type === "query") {
      params.push({
        name: param.name,
        type: "string",
      });
    }
  }

  // Add body parameter if present
  if (httpOp.parameters.body) {
    const bodyType = httpOp.parameters.body.type;
    const bodyTypeName =
      bodyType.kind === "Model" && "name" in bodyType
        ? (bodyType as any).name
        : "object";
    params.push({
      name: "body",
      type: bodyTypeName,
      attributes: [{ name: "FromBody" }],
    });
  }

  // Add CancellationToken
  params.push({
    name: "cancellationToken",
    type: "CancellationToken",
  });

  return params;
}

function pascalCase(name: string): string {
  return name.charAt(0).toUpperCase() + name.slice(1);
}
