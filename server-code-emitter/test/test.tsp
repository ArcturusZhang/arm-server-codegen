import "@typespec/http";
import "@typespec/rest";
import "@typespec/versioning";

using TypeSpec.Http;
using TypeSpec.Rest;
using TypeSpec.Versioning;

@service(#{ title: "Test Widget Service" })
@versioned(Versions)
namespace TestService;

enum Versions {
  v1: "2024-01-01",
  v2: "2024-06-01",
  v3: "2025-01-01",
}

// ---- Models ----

model WidgetProperties {
  name: string;
  color: string;

  @added(Versions.v2)
  weight?: float32;

  @added(Versions.v3)
  material?: string;
}

model Widget {
  @key("widgetName")
  @segment("widgets")
  @path
  name: string;

  properties: WidgetProperties;
}

// ---- Operations ----

@route("/widgets")
interface Widgets {
  /** Get a widget by name. */
  @get op get(@path widgetName: string): Widget;

  /** List all widgets. */
  @get op list(): Widget[];

  /** Create or update a widget. */
  @put op createOrUpdate(@path widgetName: string, @body body: Widget): Widget;

  /** Delete a widget. */
  @delete op delete(@path widgetName: string): void;

  /** Analyze a widget — added in v2. */
  @added(Versions.v2)
  @post
  @route("{widgetName}/analyze")
  op analyze(@path widgetName: string): { result: string };

  /** Check material — added in v3. */
  @added(Versions.v3)
  @post
  @route("{widgetName}/checkMaterial")
  op checkMaterial(@path widgetName: string): { compatible: boolean };
}
