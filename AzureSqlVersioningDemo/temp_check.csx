using System;
using System.Reflection;
using System.Linq;

var asm = Assembly.LoadFrom(@"");
var types = asm.GetExportedTypes().Where(t => t.Name.Contains("Convention")).ToArray();
foreach (var t in types) {
    Console.WriteLine($"{t.FullName}");
    foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        Console.WriteLine($"  {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})");
}
