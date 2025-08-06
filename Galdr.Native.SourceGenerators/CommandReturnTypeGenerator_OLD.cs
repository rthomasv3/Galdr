using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

//[Generator]
public class CommandReturnTypeGenerator_OLD : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
        System.Diagnostics.Debug.WriteLine("CommandReturnTypeGenerator: Initialize called");

        context.RegisterForSyntaxNotifications(() => new CommandRegistrationSyntaxReceiver());
    }

    public void Execute(GeneratorExecutionContext context)
    {
        System.Diagnostics.Debug.WriteLine("CommandReturnTypeGenerator: Execute called");

        if (!(context.SyntaxContextReceiver is CommandRegistrationSyntaxReceiver receiver))
        {
            System.Diagnostics.Debug.WriteLine("CommandReturnTypeGenerator: SyntaxReceiver is null or not of expected type");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"CommandReturnTypeGenerator: Found {receiver.Invocations.Count} invocations");

        var typesToSerialize = new HashSet<string>();

        foreach (var invocation in receiver.Invocations)
        {
            var semanticModel = context.Compilation.GetSemanticModel(invocation.SyntaxTree);
            var methodSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;

            if (methodSymbol != null &&
                methodSymbol.Name == "AddFunction" &&
                methodSymbol.ContainingType.Name == "GaldrBuilder" &&
                methodSymbol.ContainingNamespace.ToDisplayString() == "Galdr.Native")
            {
                var returnType = methodSymbol.TypeArguments.Last(); // Assuming TResult is the last type arg
                var typeName = returnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                typesToSerialize.Add(typeName);
            }
        }

        if (typesToSerialize.Count == 0)
            return;

        var sb = new StringBuilder();
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine();
        foreach (var type in typesToSerialize)
        {
            sb.AppendLine($"[JsonSerializable(typeof({type}))]");
        }
        sb.AppendLine("public partial class CommandJsonContext : JsonSerializerContext");
        sb.AppendLine("{\n");
        sb.AppendLine("}");

        context.AddSource("CommandJsonContext.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        System.Diagnostics.Debug.WriteLine("CommandReturnTypeGenerator: Generation complete");
    }
}

