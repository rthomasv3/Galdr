using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

//[Generator]
public class CommandReturnTypeGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        System.Diagnostics.Debug.WriteLine("CommandReturnTypeGenerator: Initialize called");

        // Create a syntax provider that finds AddFunction invocations
        var addFunctionInvocations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (node, _) => IsAddFunctionInvocation(node),
                transform: (ctx, _) => GetReturnTypeFromInvocation(ctx))
            .Where(typeInfo => !(typeInfo is null))
            .Collect();

        // Generate source when we have collected all the types
        context.RegisterSourceOutput(addFunctionInvocations, GenerateJsonContext);
    }

    private static bool IsAddFunctionInvocation(SyntaxNode node)
    {
        System.Diagnostics.Debug.WriteLine("CommandReturnTypeGenerator: Checking node");

        if (node is InvocationExpressionSyntax invocation &&
            invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Name.Identifier.Text == "AddFunction")
        {
            System.Diagnostics.Debug.WriteLine("CommandReturnTypeGenerator: Found AddFunction invocation");
            return true;
        }

        return false;
    }

    private static string GetReturnTypeFromInvocation(GeneratorSyntaxContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var semanticModel = context.SemanticModel;
        var methodSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;

        if (methodSymbol != null &&
            methodSymbol.Name == "AddFunction" &&
            methodSymbol.ContainingType.Name == "GaldrBuilder" &&
            methodSymbol.ContainingNamespace.ToDisplayString() == "Galdr.Native")
        {
            // AddFunction should have type arguments where TResult is the last one
            if (methodSymbol.TypeArguments.Length > 0)
            {
                var returnType = methodSymbol.TypeArguments.Last();
                var typeName = returnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                System.Diagnostics.Debug.WriteLine($"CommandReturnTypeGenerator: Found return type {typeName}");
                return typeName;
            }
        }

        return null;
    }

    private static void GenerateJsonContext(SourceProductionContext context, ImmutableArray<string> types)
    {
        System.Diagnostics.Debug.WriteLine($"CommandReturnTypeGenerator: Generating for {types.Length} types");

        // Filter out nulls and get distinct types
        var typesToSerialize = types
            .Where(t => !(t is null))
            .Distinct()
            .ToList();

        if (typesToSerialize.Count > 0)
                return;

        // Extract namespace from the first type or use a default
        var targetNamespace = ExtractNamespace(typesToSerialize.First()) ?? "Generated";

        var sb = new StringBuilder();
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine();
        //sb.AppendLine($"namespace {targetNamespace}");
        sb.AppendLine($"namespace Galdr.Native");
        sb.AppendLine("{");
        sb.AppendLine("    using System.Text.Json.Serialization;");
        sb.AppendLine();

        foreach (var type in typesToSerialize)
        {
            sb.AppendLine($"    [JsonSerializable(typeof({type}))]");
        }

        sb.AppendLine("    public partial class CommandJsonContext : JsonSerializerContext");
        sb.AppendLine("    {");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        context.AddSource("CommandJsonContext.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        System.Diagnostics.Debug.WriteLine("CommandReturnTypeGenerator: Generation complete");
    }

    private static string ExtractNamespace(string fullyQualifiedTypeName)
    {
        // Remove global:: prefix if present
        var typeName = fullyQualifiedTypeName.StartsWith("global::")
            ? fullyQualifiedTypeName.Substring(8)
            : fullyQualifiedTypeName;

        // Find the last dot which separates namespace from type name
        var lastDotIndex = typeName.LastIndexOf('.');

        return lastDotIndex > 0 ? typeName.Substring(0, lastDotIndex) : null;
    }
}