using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public class CommandRegistrationSyntaxReceiver : ISyntaxContextReceiver
{
    public List<InvocationExpressionSyntax> Invocations { get; } = new List<InvocationExpressionSyntax>();

    public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
    {
        System.Diagnostics.Debug.WriteLine("CommandRegistrationSyntaxReceiver: Visiting node");

        if (context.Node is InvocationExpressionSyntax invocation &&
            invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Name.Identifier.Text == "AddFunction")
        {
            System.Diagnostics.Debug.WriteLine("CommandRegistrationSyntaxReceiver: Found AddFunction invocation");

            Invocations.Add(invocation);
        }
    }
}
