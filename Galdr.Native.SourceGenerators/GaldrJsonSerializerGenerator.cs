using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

[Generator]
public class GaldrJsonSerializerGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        //System.Diagnostics.Debugger.Launch();

        // Find all AddFunction calls and extract return types with their properties
        IncrementalValueProvider<ImmutableArray<TypeInfo>> returnTypesWithInfo = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (node, _) => IsAddFunctionOrActionInvocation(node),
                transform: (ctx, _) => GetAllTypesFromInvocation(ctx))
            .Where(typeInfos => typeInfos.Length > 0)
            .SelectMany((typeInfos, _) => typeInfos)
            .Where(typeInfo => typeInfo != null)
            .Collect();

        // Generate serialization code
        context.RegisterSourceOutput(returnTypesWithInfo, GenerateSerializers);
    }

    private static bool IsAddFunctionOrActionInvocation(SyntaxNode node)
    {
        return node is InvocationExpressionSyntax invocation &&
               invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
               (memberAccess.Name.Identifier.Text == "AddFunction" || 
                memberAccess.Name.Identifier.Text == "AddAction");
    }

    private static TypeInfo GetReturnTypeInfo(GeneratorSyntaxContext context)
    {
        InvocationExpressionSyntax invocation = (InvocationExpressionSyntax)context.Node;
        SemanticModel semanticModel = context.SemanticModel;
        IMethodSymbol methodSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;

        if (methodSymbol == null ||
            methodSymbol.Name != "AddFunction" ||
            methodSymbol.ContainingType.Name != "GaldrBuilder" ||
            methodSymbol.ContainingNamespace.ToDisplayString() != "Galdr.Native")
        {
            return null;
        }

        if (methodSymbol.TypeArguments.Length > 0)
        {
            if (methodSymbol.Name == "AddFunction")
            {
                // Get the return type (last type argument)
                ITypeSymbol returnType = methodSymbol.TypeArguments.Last();

                // Skip void and primitive types
                if (returnType.SpecialType != SpecialType.None ||
                    returnType.TypeKind == TypeKind.Enum ||
                    returnType.Name == "String")
                    return null;

                return ExtractTypeInfo(returnType);
            }
        }

        return null;
    }

    private static ImmutableArray<TypeInfo> GetAllTypesFromInvocation(GeneratorSyntaxContext context)
    {
        InvocationExpressionSyntax invocation = (InvocationExpressionSyntax)context.Node;
        SemanticModel semanticModel = context.SemanticModel;
        IMethodSymbol methodSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;

        if (methodSymbol == null ||
            (methodSymbol.Name != "AddFunction" && methodSymbol.Name != "AddAction") ||
            methodSymbol.ContainingType.Name != "GaldrBuilder" ||
            methodSymbol.ContainingNamespace.ToDisplayString() != "Galdr.Native")
        {
            return ImmutableArray<TypeInfo>.Empty;
        }

        List<TypeInfo> discoveredTypes = new List<TypeInfo>();

        // For AddFunction: Func<T1, T2, ..., TResult> - last type arg is return type, rest are parameters
        // For AddAction: Action<T1, T2, ...> - all type args are parameters
        if (methodSymbol.TypeArguments.Length > 0)
        {
            if (methodSymbol.Name == "AddFunction")
            {
                // For AddFunction, the return type is the last type argument
                if (methodSymbol.TypeArguments.Length > 0)
                {
                    ITypeSymbol returnType = methodSymbol.TypeArguments.Last();

                    // Skip void and primitive types for return types
                    if (returnType.SpecialType == SpecialType.None &&
                        returnType.TypeKind != TypeKind.Enum &&
                        returnType.Name != "String")
                    {
                        TypeInfo returnTypeInfo = ExtractTypeInfo(returnType);
                        if (returnTypeInfo != null)
                        {
                            discoveredTypes.Add(returnTypeInfo);
                        }
                    }
                }

                // Parameter types are all type arguments except the last one
                for (int i = 0; i < methodSymbol.TypeArguments.Length - 1; i++)
                {
                    ITypeSymbol parameterType = methodSymbol.TypeArguments[i];
                    if (ShouldGenerateSerializerForParameter(parameterType))
                    {
                        TypeInfo parameterTypeInfo = ExtractTypeInfo(parameterType);
                        if (parameterTypeInfo != null)
                        {
                            discoveredTypes.Add(parameterTypeInfo);
                        }
                    }
                }
            }
            else if (methodSymbol.Name == "AddAction")
            {
                // For AddAction, all type arguments are parameters
                for (int i = 0; i < methodSymbol.TypeArguments.Length; i++)
                {
                    ITypeSymbol parameterType = methodSymbol.TypeArguments[i];
                    if (ShouldGenerateSerializerForParameter(parameterType))
                    {
                        TypeInfo parameterTypeInfo = ExtractTypeInfo(parameterType);
                        if (parameterTypeInfo != null)
                        {
                            discoveredTypes.Add(parameterTypeInfo);
                        }
                    }
                }
            }
        }

        return discoveredTypes.ToImmutableArray();
    }

    private static bool ShouldGenerateSerializerForParameter(ITypeSymbol parameterType)
    {
        // Skip interfaces (likely DI services)
        if (parameterType.TypeKind == TypeKind.Interface)
            return false;

        // Skip abstract classes (likely DI services)
        if (parameterType.IsAbstract)
            return false;

        // Skip obvious framework types
        string fullName = parameterType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // Skip Microsoft and most System types (but allow basic data types to be handled by ShouldSerializeType)
        if (fullName.StartsWith("Microsoft.") || fullName.StartsWith("global::Microsoft."))
            return false;

        // Skip System types that are clearly services/framework types
        if ((fullName.StartsWith("System.") || fullName.StartsWith("global::System.")) &&
            (fullName.Contains("IServiceProvider") ||
             fullName.Contains("ILogger") ||
             fullName.Contains("IConfiguration") ||
             fullName.Contains("IHosting") ||
             fullName.Contains("IMemoryCache") ||
             fullName.Contains("HttpContext")))
            return false;

        // Use the existing logic for everything else
        return ShouldSerializeType(parameterType);
    }

    private static TypeInfo ExtractTypeInfo(ITypeSymbol typeSymbol)
    {
        TypeInfo typeInfo = new TypeInfo
        {
            FullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            Name = typeSymbol.Name,
            Namespace = typeSymbol.ContainingNamespace?.ToDisplayString() ?? "Global",
            Properties = new List<PropertyInfo>()
        };

        // Extract all public properties
        ImmutableArray<ISymbol> members = typeSymbol.GetMembers();
        foreach (ISymbol member in members)
        {
            if (member is IPropertySymbol property &&
                property.DeclaredAccessibility == Accessibility.Public &&
                property.GetMethod != null &&
                !property.IsStatic &&
                !property.IsIndexer)
            {
                typeInfo.Properties.Add(new PropertyInfo
                {
                    Name = property.Name,
                    Type = property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    TypeSymbol = property.Type,
                    JsonName = GetJsonPropertyName(property),
                    CanWrite = property.SetMethod != null && property.SetMethod.DeclaredAccessibility == Accessibility.Public
                });
            }
        }

        return typeInfo.Properties.Count > 0 ? typeInfo : null;
    }

    private static string GetJsonPropertyName(IPropertySymbol property)
    {
        // Check for JsonPropertyName attribute
        AttributeData jsonPropertyAttr = property.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "System.Text.Json.Serialization.JsonPropertyNameAttribute");

        if (jsonPropertyAttr?.ConstructorArguments.Length > 0)
        {
            return jsonPropertyAttr.ConstructorArguments[0].Value?.ToString() ?? property.Name;
        }

        // Default to camelCase
        return ToCamelCase(property.Name);
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name) || char.IsLower(name[0]))
            return name;

        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    private static bool ShouldSerializeType(ITypeSymbol typeSymbol)
    {
        // Skip primitive types, enums, and strings
        if (typeSymbol.SpecialType != SpecialType.None ||
            typeSymbol.TypeKind == TypeKind.Enum ||
            typeSymbol.Name == "String")
            return false;

        // Handle nullable types - unwrap and check the underlying type
        if (typeSymbol.NullableAnnotation == NullableAnnotation.Annotated &&
            typeSymbol is INamedTypeSymbol namedType &&
            namedType.TypeArguments.Length == 1)
        {
            return ShouldSerializeType(namedType.TypeArguments[0]);
        }

        // Skip system types like Guid, DateTime, etc. that have built-in JSON support
        string fullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if ((fullName.StartsWith("System.") || fullName.StartsWith("global::System.")) &&
            (fullName.Contains("Guid") || fullName.Contains("DateTime") || fullName.Contains("TimeSpan")))
            return false;

        return true;
    }

    private static bool IsCollectionType(ITypeSymbol typeSymbol)
    {
        // Handle arrays
        if (typeSymbol.TypeKind == TypeKind.Array)
            return true;

        // Handle generic collections
        if (typeSymbol is INamedTypeSymbol namedTypeSymbol)
        {
            string fullName = namedTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            // Check for common collection types
            if (fullName.Contains("System.Collections.Generic.List<") ||
                fullName.Contains("System.Collections.Generic.IList<") ||
                fullName.Contains("System.Collections.Generic.ICollection<") ||
                fullName.Contains("System.Collections.Generic.IEnumerable<"))
                return true;
        }

        return false;
    }

    private static ITypeSymbol GetCollectionElementType(ITypeSymbol collectionType)
    {
        // Handle arrays
        if (collectionType is IArrayTypeSymbol arrayType)
            return arrayType.ElementType;

        // Handle generic collections
        if (collectionType is INamedTypeSymbol namedType && namedType.TypeArguments.Length == 1)
            return namedType.TypeArguments[0];

        return null;
    }

    private static List<TypeInfo> DiscoverAllTypes(ImmutableArray<TypeInfo> rootTypes)
    {
        var allTypes = new Dictionary<string, TypeInfo>();
        var typesToProcess = new Queue<TypeInfo>();

        // Start with the root types
        foreach (var rootType in rootTypes.Where(t => t != null))
        {
            if (!allTypes.ContainsKey(rootType.FullName))
            {
                allTypes[rootType.FullName] = rootType;
                typesToProcess.Enqueue(rootType);
            }
        }

        // Process types recursively
        while (typesToProcess.Count > 0)
        {
            TypeInfo currentType = typesToProcess.Dequeue();

            foreach (PropertyInfo property in currentType.Properties)
            {
                // Handle collections
                if (IsCollectionType(property.TypeSymbol))
                {
                    ITypeSymbol elementType = GetCollectionElementType(property.TypeSymbol);

                    if (elementType != null && ShouldSerializeType(elementType))
                    {
                        string elementTypeFullName = elementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                        if (!allTypes.ContainsKey(elementTypeFullName))
                        {
                            TypeInfo elementTypeInfo = ExtractTypeInfo(elementType);

                            if (elementTypeInfo != null)
                            {
                                allTypes[elementTypeFullName] = elementTypeInfo;
                                typesToProcess.Enqueue(elementTypeInfo);
                            }
                        }
                    }
                }
                // Handle non-collection complex types
                else if (ShouldSerializeType(property.TypeSymbol))
                {
                    string propertyTypeFullName = property.TypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                    // If we haven't processed this type yet, extract its info and add to queue
                    if (!allTypes.ContainsKey(propertyTypeFullName))
                    {
                        TypeInfo propertyTypeInfo = ExtractTypeInfo(property.TypeSymbol);

                        if (propertyTypeInfo != null)
                        {
                            allTypes[propertyTypeFullName] = propertyTypeInfo;
                            typesToProcess.Enqueue(propertyTypeInfo);
                        }
                    }
                }
            }
        }

        return allTypes.Values.ToList();
    }

    private static void GenerateSerializers(SourceProductionContext context, ImmutableArray<TypeInfo> types)
    {
        List<TypeInfo> allTypes = DiscoverAllTypes(types);

        if (allTypes.Count == 0)
            return;

        //System.Diagnostics.Debugger.Launch();

        StringBuilder sb = new StringBuilder();

        // Generate the file header
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine("using Galdr.Native;");
        sb.AppendLine();
        sb.AppendLine("namespace Galdr.Native.Generated");
        sb.AppendLine("{");

        // Generate collection helper methods for all element types used in collections
        GenerateCollectionHelpers(sb, allTypes);

        // Generate converter for each type
        foreach (TypeInfo typeInfo in allTypes)
        {
            GenerateTypeConverter(sb, typeInfo);
        }

        // Generate the serializer implementation
        GenerateSerializerImplementation(sb, allTypes);

        // Generate the module initializer
        GenerateModuleInitializer(sb);

        sb.AppendLine("}");

        context.AddSource("GaldrJsonSerializers.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private static void GenerateCollectionHelpers(StringBuilder sb, List<TypeInfo> allTypes)
    {
        var elementTypesUsedInCollections = new HashSet<string>();

        // Find all element types used in collections across all types
        foreach (var typeInfo in allTypes)
        {
            foreach (var property in typeInfo.Properties)
            {
                if (IsCollectionType(property.TypeSymbol))
                {
                    var elementType = GetCollectionElementType(property.TypeSymbol);
                    if (elementType != null)
                    {
                        string elementTypeName = elementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        elementTypesUsedInCollections.Add(elementTypeName);
                    }
                }
            }
        }

        sb.AppendLine("    internal static class CollectionHelpers");
        sb.AppendLine("    {");

        // Generate helper methods for each element type
        foreach (var elementTypeName in elementTypesUsedInCollections)
        {
            GenerateCollectionHelperMethods(sb, elementTypeName);
        }

        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void GenerateCollectionHelperMethods(StringBuilder sb, string elementTypeName)
    {
        string safeTypeName = GetSafeTypeName(elementTypeName);
        string elementTypeDisplayName = elementTypeName;

        // Determine how to read/write the element
        bool isPrimitive = IsPrimitiveTypeName(elementTypeName);
        bool isSystemType = IsSystemTypeName(elementTypeName);

        // Generate Read method
        sb.AppendLine($"        public static object? ReadCollection_{safeTypeName}(ref Utf8JsonReader reader, JsonSerializerOptions options, bool isArray)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (reader.TokenType == JsonTokenType.Null)");
        sb.AppendLine("                return null;");
        sb.AppendLine();
        sb.AppendLine("            if (reader.TokenType != JsonTokenType.StartArray)");
        sb.AppendLine("                throw new JsonException(\"Expected StartArray token\");");
        sb.AppendLine();
        sb.AppendLine($"            var list = new List<{elementTypeDisplayName}>();");
        sb.AppendLine();
        sb.AppendLine("            while (reader.Read())");
        sb.AppendLine("            {");
        sb.AppendLine("                if (reader.TokenType == JsonTokenType.EndArray)");
        sb.AppendLine("                    break;");
        sb.AppendLine();

        // Generate element reading code
        string elementReadCode = GetElementReadCode(elementTypeName, isPrimitive, isSystemType);
        sb.AppendLine($"                var element = {elementReadCode};");
        sb.AppendLine("                list.Add(element);");

        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            return isArray ? list.ToArray() : list;");
        sb.AppendLine("        }");
        sb.AppendLine();

        // Generate Write method
        sb.AppendLine($"        public static void WriteCollection_{safeTypeName}(Utf8JsonWriter writer, object? collection, JsonSerializerOptions options)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (collection == null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNullValue();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteStartArray();");
        sb.AppendLine();
        sb.AppendLine($"            foreach (var item in (System.Collections.Generic.IEnumerable<{elementTypeDisplayName}>)collection)");
        sb.AppendLine("            {");

        // Generate element writing code
        string elementWriteCode = GetElementWriteCode(elementTypeName, isPrimitive, isSystemType);
        sb.AppendLine($"                {elementWriteCode}");

        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteEndArray();");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static bool IsPrimitiveTypeName(string typeName)
    {
        return
            typeName == "System.Int32"      ||
            typeName == "System.Int64"      ||
            typeName == "System.Single"     ||
            typeName == "System.Double"     ||
            typeName == "System.Boolean"    ||
            typeName == "System.String"     ||
            typeName == "System.DateTime";
    }

    private static bool IsSystemTypeName(string typeName)
    {
        return typeName.StartsWith("System.") &&
               (typeName.Contains("Guid") || typeName.Contains("DateTime") || typeName.Contains("TimeSpan"));
    }

    private static string GetElementReadCode(string elementTypeName, bool isPrimitive, bool isSystemType)
    {
        if (isPrimitive)
        {
            string readCode = null;

            switch (elementTypeName)
            {
                case "System.Int32":
                    readCode = "reader.GetInt32()";
                    break;
                case "System.Int64":
                    readCode = "reader.GetInt64()";
                    break;
                case "System.Single":
                    readCode = "reader.GetSingle()";
                    break;
                case "System.Double":
                    readCode = "reader.GetDouble()";
                    break;
                case "System.Boolean":
                    readCode = "reader.GetBoolean()";
                    break;
                case "System.String":
                    readCode = "reader.GetString() ?? string.Empty";
                    break;
                case "System.DateTime":
                    readCode = "reader.GetDateTime()";
                    break;
                default:
                    throw new NotSupportedException($"Primitive type {elementTypeName} not supported");
            }

            return readCode;
        }

        if (isSystemType && elementTypeName.Contains("Guid"))
        {
            return "reader.GetGuid()";
        }

        // For complex types, use the generated converter
        string baseTypeName = GetBaseTypeNameFromFullName(elementTypeName);
        string converterName = $"{baseTypeName}JsonConverter";
        return $"new {converterName}().Read(ref reader, typeof({elementTypeName}), options) ?? new {elementTypeName}()";
    }

    private static string GetElementWriteCode(string elementTypeName, bool isPrimitive, bool isSystemType)
    {
        if (isPrimitive)
        {
            string writeCode = null;

            switch (elementTypeName)
            {
                case "System.Int32":
                case "System.Int64":
                case "System.Single":
                case "System.Double":
                    writeCode = "writer.WriteNumberValue(item);";
                    break;

                case "System.Boolean":
                    writeCode = "writer.WriteBooleanValue(item);";
                    break;

                case "System.String":
                    writeCode = "writer.WriteStringValue(item);";
                    break;

                case "System.DateTime":
                    writeCode = "writer.WriteStringValue(item);";
                    break;

                default:
                    throw new NotSupportedException($"Primitive type {elementTypeName} not supported");
            }

            return writeCode;
        }

        if (isSystemType && elementTypeName.Contains("Guid"))
        {
            return "writer.WriteStringValue(item);";
        }

        // For complex types, use the generated converter
        string baseTypeName = GetBaseTypeNameFromFullName(elementTypeName);
        string converterName = $"{baseTypeName}JsonConverter";
        return $"new {converterName}().Write(writer, item, options);";
    }

    private static string GetBaseTypeNameFromFullName(string fullTypeName)
    {
        var parts = fullTypeName.Split('.');
        return parts[parts.Length - 1];
    }

    private static void GenerateTypeConverter(StringBuilder sb, TypeInfo typeInfo)
    {
        string converterName = $"{typeInfo.Name}JsonConverter";
        string typeName = typeInfo.FullName;

        sb.AppendLine($"    internal sealed class {converterName} : JsonConverter<{typeName}>");
        sb.AppendLine("    {");

        // Generate Read method
        sb.AppendLine($"        public override {typeName}? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (reader.TokenType == JsonTokenType.Null)");
        sb.AppendLine("                return null;");
        sb.AppendLine();
        sb.AppendLine("            if (reader.TokenType != JsonTokenType.StartObject)");
        sb.AppendLine("                throw new JsonException(\"Expected StartObject token\");");
        sb.AppendLine();
        sb.AppendLine($"            var result = new {typeName}();");
        sb.AppendLine();
        sb.AppendLine("            while (reader.Read())");
        sb.AppendLine("            {");
        sb.AppendLine("                if (reader.TokenType == JsonTokenType.EndObject)");
        sb.AppendLine("                    return result;");
        sb.AppendLine();
        sb.AppendLine("                if (reader.TokenType != JsonTokenType.PropertyName)");
        sb.AppendLine("                    continue;");
        sb.AppendLine();
        sb.AppendLine("                var propertyName = reader.GetString();");
        sb.AppendLine("                reader.Read();");
        sb.AppendLine();
        sb.AppendLine("                switch (propertyName)");
        sb.AppendLine("                {");

        // Generate case for each property
        foreach (PropertyInfo prop in typeInfo.Properties.Where(p => p.CanWrite))
        {
            sb.AppendLine($"                    case \"{prop.JsonName}\":");
            GeneratePropertyRead(sb, prop, "result");
            sb.AppendLine("                        break;");
        }

        sb.AppendLine("                    default:");
        sb.AppendLine("                        reader.Skip();");
        sb.AppendLine("                        break;");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            throw new JsonException(\"Expected EndObject token\");");
        sb.AppendLine("        }");
        sb.AppendLine();

        // Generate Write method
        sb.AppendLine($"        public override void Write(Utf8JsonWriter writer, {typeName} value, JsonSerializerOptions options)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (value == null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNullValue();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteStartObject();");

        foreach (PropertyInfo prop in typeInfo.Properties)
        {
            GeneratePropertyWrite(sb, prop);
        }

        sb.AppendLine("            writer.WriteEndObject();");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void GeneratePropertyRead(StringBuilder sb, PropertyInfo property, string targetVariable)
    {
        string typeStr = property.Type;

        string readCode = String.Empty;

        switch (property.TypeSymbol.SpecialType)
        {
            case SpecialType.System_Int32:
                readCode = "reader.GetInt32()";
                break;

            case SpecialType.System_Int64:
                readCode = "reader.GetInt64()";
                break;

            case SpecialType.System_Single:
                readCode = "reader.GetSingle()";
                break;

            case SpecialType.System_Double:
                readCode = "reader.GetDouble()";
                break;

            case SpecialType.System_Boolean:
                readCode = "reader.GetBoolean()";
                break;

            case SpecialType.System_String:
                readCode = "reader.GetString() ?? string.Empty";
                break;

            case SpecialType.System_DateTime:
                readCode = "reader.GetDateTime()";
                break;

            default:
                readCode = GetComplexTypeReadCode(property);
                break;
        }

        sb.AppendLine($"                        {targetVariable}.{property.Name} = {readCode};");
    }

    private static string GetComplexTypeReadCode(PropertyInfo property)
    {
        // Handle Guid
        if (property.Type == "System.Guid" || property.Type == "global::System.Guid")
            return "reader.GetGuid()";

        if (IsCollectionType(property.TypeSymbol))
        {
            return GenerateCollectionReadCode(property);
        }

        // For complex types that we're generating converters for, use the generated converter directly
        string baseTypeName = GetBaseTypeName(property.TypeSymbol);
        string converterName = $"{baseTypeName}JsonConverter";

        // Handle nullable types
        if (property.TypeSymbol.NullableAnnotation == NullableAnnotation.Annotated)
        {
            return $"new {converterName}().Read(ref reader, typeof({property.Type}), options)";
        }

        // Default for complex types - use generated converter
        return $"new {converterName}().Read(ref reader, typeof({property.Type}), options) ?? new {property.Type}()";
    }

    private static string GenerateCollectionReadCode(PropertyInfo property)
    {
        var elementType = GetCollectionElementType(property.TypeSymbol);
        string elementTypeName = elementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // Determine the collection type
        bool isArray = property.TypeSymbol.TypeKind == TypeKind.Array;

        return $@"({property.Type})CollectionHelpers.ReadCollection_{GetSafeTypeName(elementTypeName)}(ref reader, options, {isArray.ToString().ToLower()})";
    }

    private static string GetSafeTypeName(string typeName)
    {
        return typeName
            .Replace("global::", "")
            .Replace(".", "_")
            .Replace("<", "_")
            .Replace(">", "_")
            .Replace("?", "_Nullable");
    }

    private static string GetBaseTypeName(ITypeSymbol typeSymbol)
    {
        // Handle nullable types - get the underlying type name
        if (typeSymbol.NullableAnnotation == NullableAnnotation.Annotated &&
            typeSymbol is INamedTypeSymbol namedType &&
            namedType.TypeArguments.Length == 1)
        {
            return namedType.TypeArguments[0].Name;
        }

        return typeSymbol.Name;
    }

    private static void GeneratePropertyWrite(StringBuilder sb, PropertyInfo property)
    {
        string writeCode = String.Empty;

        switch (property.TypeSymbol.SpecialType)
        {
            case SpecialType.System_Int32:
                writeCode = $"writer.WriteNumber(\"{property.JsonName}\", value.{property.Name});";
                break;

            case SpecialType.System_Int64:
                writeCode = $"writer.WriteNumber(\"{property.JsonName}\", value.{property.Name});";
                break;

            case SpecialType.System_Single:
                writeCode = $"writer.WriteNumber(\"{property.JsonName}\", value.{property.Name});";
                break;

            case SpecialType.System_Double:
                writeCode = $"writer.WriteNumber(\"{property.JsonName}\", value.{property.Name});";
                break;

            case SpecialType.System_Boolean:
                writeCode = $"writer.WriteBoolean(\"{property.JsonName}\", value.{property.Name});";
                break;

            case SpecialType.System_String:
                writeCode = GenerateStringWriteCode(property);
                break;

            case SpecialType.System_DateTime:
                writeCode = $"writer.WriteString(\"{property.JsonName}\", value.{property.Name});";
                break;


            default:
                writeCode = GenerateComplexTypeWriteCode(property);
                break;
        }

        sb.AppendLine($"            {writeCode}");
    }

    private static string GenerateStringWriteCode(PropertyInfo property)
    {
        return $@"if (value.{property.Name} != null)
                writer.WriteString(""{property.JsonName}"", value.{property.Name});
            else
                writer.WriteNull(""{property.JsonName}"");";
    }

    private static string GenerateComplexTypeWriteCode(PropertyInfo property)
    {
        // Handle Guid and other system types with built-in support
        if (property.Type == "System.Guid" || property.Type == "global::System.Guid")
        {
            return $"writer.WriteString(\"{property.JsonName}\", value.{property.Name});";
        }

        if (IsCollectionType(property.TypeSymbol))
        {
            return GenerateCollectionWriteCode(property);
        }

        // For complex types that we're generating converters for, use the generated converter directly
        string baseTypeName = GetBaseTypeName(property.TypeSymbol);
        string converterName = $"{baseTypeName}JsonConverter";

        return $@"writer.WritePropertyName(""{property.JsonName}"");
            new {converterName}().Write(writer, value.{property.Name}, options);";
    }

    private static string GenerateCollectionWriteCode(PropertyInfo property)
    {
        var elementType = GetCollectionElementType(property.TypeSymbol);
        string elementTypeName = elementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        return $@"writer.WritePropertyName(""{property.JsonName}"");
            CollectionHelpers.WriteCollection_{GetSafeTypeName(elementTypeName)}(writer, value.{property.Name}, options);";
    }

    private static void GenerateSerializerImplementation(StringBuilder sb, List<TypeInfo> types)
    {
        sb.AppendLine("    internal class GeneratedJsonSerializer : IGaldrJsonSerializer");
        sb.AppendLine("    {");
        sb.AppendLine("        public bool CanSerialize(Type type)");
        sb.AppendLine("        {");
        sb.AppendLine("            return type switch");
        sb.AppendLine("            {");
        foreach (TypeInfo type in types)
        {
            sb.AppendLine($"                Type t when t == typeof({type.FullName}) => true,");
        }
        sb.AppendLine("                _ => false");
        sb.AppendLine("            };");
        sb.AppendLine("        }");
        sb.AppendLine();

        sb.AppendLine("        public string Serialize(object value, Type type)");
        sb.AppendLine("        {");
        sb.AppendLine("            using var stream = new System.IO.MemoryStream();");
        sb.AppendLine("            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });");
        sb.AppendLine();
        sb.AppendLine("            switch (type)");
        sb.AppendLine("            {");

        foreach (TypeInfo type in types)
        {
            sb.AppendLine($"                case Type t when t == typeof({type.FullName}):");
            sb.AppendLine($"                    new {type.Name}JsonConverter().Write(writer, ({type.FullName})value, JsonSerializerOptions.Default);");
            sb.AppendLine("                    break;");
        }

        sb.AppendLine("                default:");
        sb.AppendLine("                    throw new NotSupportedException($\"Type {type} is not registered for serialization\");");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            writer.Flush();");
        sb.AppendLine("            return System.Text.Encoding.UTF8.GetString(stream.ToArray());");
        sb.AppendLine("        }");
        sb.AppendLine();

        sb.AppendLine("        public object? Deserialize(string json, Type type)");
        sb.AppendLine("        {");
        sb.AppendLine("            var bytes = System.Text.Encoding.UTF8.GetBytes(json);");
        sb.AppendLine("            var reader = new Utf8JsonReader(bytes);");
        sb.AppendLine("            reader.Read(); // Move to first token");
        sb.AppendLine();
        sb.AppendLine("            return type switch");
        sb.AppendLine("            {");

        foreach (TypeInfo type in types)
        {
            sb.AppendLine($"                Type t when t == typeof({type.FullName}) => new {type.Name}JsonConverter().Read(ref reader, typeof({type.FullName}), JsonSerializerOptions.Default),");
        }

        sb.AppendLine("                _ => throw new NotSupportedException($\"Type {type} is not registered for deserialization\")");
        sb.AppendLine("            };");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
    }

    private static void GenerateModuleInitializer(StringBuilder sb)
    {
        sb.AppendLine("    internal static class GaldrGeneratedInitializer");
        sb.AppendLine("    {");
        sb.AppendLine("        [ModuleInitializer]");
        sb.AppendLine("        public static void Initialize()");
        sb.AppendLine("        {");
        sb.AppendLine("            GaldrJsonSerializerRegistry.Register(new GeneratedJsonSerializer());");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
    }

    // Helper classes
    private class TypeInfo
    {
        public string FullName { get; set; } = "";
        public string Name { get; set; } = "";
        public string Namespace { get; set; } = "";
        public List<PropertyInfo> Properties { get; set; } = new List<PropertyInfo>();
    }

    private class PropertyInfo
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public ITypeSymbol TypeSymbol { get; set; } = null;
        public string JsonName { get; set; } = "";
        public bool CanWrite { get; set; }
    }
}