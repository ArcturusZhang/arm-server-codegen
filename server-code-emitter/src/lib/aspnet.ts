import { createLibrary } from "@alloy-js/csharp";

/**
 * Microsoft.AspNetCore.Mvc type declarations for the emitter framework.
 * Using these as type references enables auto-managed `using` statements.
 */
export const Mvc = createLibrary("Microsoft.AspNetCore.Mvc", {
  ControllerBase: {
    kind: "class",
    members: {},
    isAbstract: true,
  },
  IActionResult: {
    kind: "interface",
    members: {},
  },
  ApiControllerAttribute: {
    kind: "class",
    members: {
      ApiControllerAttribute: {
        kind: "method",
        methodKind: "constructor" as const,
      },
    },
    isSealed: true,
  },
  RouteAttribute: {
    kind: "class",
    members: {
      RouteAttribute: {
        kind: "method",
        methodKind: "constructor" as const,
      },
    },
    isSealed: true,
  },
  HttpGetAttribute: {
    kind: "class",
    members: {
      HttpGetAttribute: {
        kind: "method",
        methodKind: "constructor" as const,
      },
    },
    isSealed: true,
  },
  HttpPutAttribute: {
    kind: "class",
    members: {
      HttpPutAttribute: {
        kind: "method",
        methodKind: "constructor" as const,
      },
    },
    isSealed: true,
  },
  HttpPostAttribute: {
    kind: "class",
    members: {
      HttpPostAttribute: {
        kind: "method",
        methodKind: "constructor" as const,
      },
    },
    isSealed: true,
  },
  HttpPatchAttribute: {
    kind: "class",
    members: {
      HttpPatchAttribute: {
        kind: "method",
        methodKind: "constructor" as const,
      },
    },
    isSealed: true,
  },
  HttpDeleteAttribute: {
    kind: "class",
    members: {
      HttpDeleteAttribute: {
        kind: "method",
        methodKind: "constructor" as const,
      },
    },
    isSealed: true,
  },
  FromBodyAttribute: {
    kind: "class",
    members: {
      FromBodyAttribute: {
        kind: "method",
        methodKind: "constructor" as const,
      },
    },
    isSealed: true,
  },
  FromHeaderAttribute: {
    kind: "class",
    members: {
      FromHeaderAttribute: {
        kind: "method",
        methodKind: "constructor" as const,
      },
    },
    isSealed: true,
  },
});

/**
 * Asp.Versioning type declarations.
 */
export const AspVersioning = createLibrary("Asp.Versioning", {
  ApiVersionAttribute: {
    kind: "class",
    members: {
      ApiVersionAttribute: {
        kind: "method",
        methodKind: "constructor" as const,
      },
    },
    isSealed: true,
  },
});
