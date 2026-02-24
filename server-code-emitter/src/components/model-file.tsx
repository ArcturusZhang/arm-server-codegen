import * as cs from "@alloy-js/csharp";
import { Children } from "@alloy-js/core";
import { ClassDeclaration } from "@typespec/emitter-framework/csharp";
import { Model } from "@typespec/compiler";

export interface ModelFileProps {
  model: Model;
  namespace: string;
  /** Set of model names being generated — used to detect external base types */
  generatedModels: Set<string>;
}

export function ModelFile(props: ModelFileProps): Children {
  const { model, namespace, generatedModels } = props;

  // If the base model is external (not in our generation scope),
  // provide its name as a string so the framework doesn't try to resolve a refkey.
  const baseType =
    model.baseModel && !generatedModels.has(model.baseModel.name)
      ? model.baseModel.name
      : undefined;

  return (
    <cs.SourceFile path={`${model.name}.cs`}>
      <cs.Namespace name={namespace}>
        <ClassDeclaration type={model} public baseType={baseType} />
      </cs.Namespace>
    </cs.SourceFile>
  );
}
