import * as cs from "@alloy-js/csharp";
import { Children } from "@alloy-js/core";
import { EnumDeclaration } from "@typespec/emitter-framework/csharp";
import { Enum, Union } from "@typespec/compiler";

export interface EnumFileProps {
  type: Union | Enum;
  namespace: string;
}

export function EnumFile(props: EnumFileProps): Children {
  const { type, namespace } = props;

  return (
    <cs.SourceFile path={`${type.name!}.cs`}>
      <cs.Namespace name={namespace}>
        <EnumDeclaration type={type} public />
      </cs.Namespace>
    </cs.SourceFile>
  );
}
