import * as cs from "@alloy-js/csharp";
import { Children } from "@alloy-js/core";
import { ClassDeclaration } from "@typespec/emitter-framework/csharp";
import { Model } from "@typespec/compiler";
import { pascalCase } from "change-case";
import { SourceFile } from "./source-file.js";

export interface ModelFileProps {
  model: Model;
  namespace: string;
}

export function ModelFile(props: ModelFileProps): Children {
  const { model, namespace } = props;

  return (
    <SourceFile path={`${pascalCase(model.name)}.cs`}>
      <cs.Namespace name={namespace}>
        <ClassDeclaration type={model} public />
      </cs.Namespace>
    </SourceFile>
  );
}
