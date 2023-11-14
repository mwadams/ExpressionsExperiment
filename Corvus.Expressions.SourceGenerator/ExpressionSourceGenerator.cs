namespace Corvus.Expressions.SourceGenerator;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;

[Generator]
public class ExpressionSourceGenerator : ISourceGenerator
{
    private const string GenerateBonsaiAttributeName = "Corvus.Expressions.SourceGenerator.GenerateBonsaiAttribute";

    private const string AttributeText =
        """
        namespace Corvus.Expressions.SourceGenerator;
        
        using System;

        [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
        sealed class GenerateBonsaiAttribute : Attribute
        {
            public GenerateBonsaiAttribute()
            {
            }
        }
        """;

    public void Initialize(GeneratorInitializationContext context)
    {
        // Register the attribute source
        context.RegisterForPostInitialization((i) => i.AddSource("GenerateBonsaiAttribute.g.cs", AttributeText));

        // Register a syntax receiver that will be created for each generation pass
        context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
    }

    public void Execute(GeneratorExecutionContext context)
    {
        // retrieve the populated receiver 
        if (context.SyntaxContextReceiver is not SyntaxReceiver receiver)
            return;

        // get the added attribute, and INotifyPropertyChanged
        INamedTypeSymbol attributeSymbol = context.Compilation.GetTypeByMetadataName(GenerateBonsaiAttributeName);

        // group the fields by class, and generate the source
        foreach (IGrouping<INamedTypeSymbol, IPropertySymbol> group in receiver.Properties.GroupBy<IPropertySymbol, INamedTypeSymbol>(f => f.ContainingType, SymbolEqualityComparer.Default))
        {
            string classSource = ProcessClass(group.Key, [.. group], attributeSymbol, context);
            context.AddSource($"{group.Key.Name}_GenerateBonsai.g.cs", SourceText.From(classSource, Encoding.UTF8));
        }
    }

    private string ProcessClass(INamedTypeSymbol classSymbol, List<IPropertySymbol> properties, ISymbol attributeSymbol, GeneratorExecutionContext context)
    {
        if (!classSymbol.ContainingSymbol.Equals(classSymbol.ContainingNamespace, SymbolEqualityComparer.Default))
        {
            return null;
        }

        string namespaceName = classSymbol.ContainingNamespace.ToDisplayString();

        // begin building the generated source
        StringBuilder source = new StringBuilder(
            $$"""
            namespace {{namespaceName}};
            
            public partial class {{classSymbol.Name}}
            {

            """);

        // create properties for each field 
        foreach (IPropertySymbol propertySymbol in properties)
        {
            ProcessProperty(source, propertySymbol, attributeSymbol);
        }

        source.AppendLine(
            """
            }
            """);
        return source.ToString();
    }

    private void ProcessProperty(StringBuilder source, IPropertySymbol propertySymbol, ISymbol attributeSymbol)
    {
        // get the name and type of the field
        string propertyName = propertySymbol.Name;

        // get the BonsaiGeneratorAttribute attribute from the field, and any associated data
        AttributeData attributeData = propertySymbol.GetAttributes().Single(ad => ad.AttributeClass.Equals(attributeSymbol, SymbolEqualityComparer.Default));

        string staticModifier = propertySymbol.IsStatic ? "static " : string.Empty;


        SyntaxNode syntax = propertySymbol.GetMethod.DeclaringSyntaxReferences[0].GetSyntax();
        

        if (syntax is ArrowExpressionClauseSyntax arrowExpressionSyntax)
        {
            ProcessExpressionSyntax(staticModifier, propertyName, propertySymbol.GetMethod.ReturnType, arrowExpressionSyntax.Expression, source);
        }
    }

    private string ProcessTypeSyntax(ITypeSymbol typeSymbol)
    {
        INamedTypeSymbol namedTypeSymbol = typeSymbol as INamedTypeSymbol;
        StringBuilder result = new();

        if (namedTypeSymbol.TypeKind != TypeKind.Delegate)
        {
            return string.Empty;
        }

        result.Append(namedTypeSymbol.Name);

        int index = 0;
        foreach(var typeArgument in namedTypeSymbol.TypeArguments)
        {
            result.Append($" Argument {index++}: ");
            result.Append(typeArgument.Name);
        }

        // Rather than returning a string, you could process its syntax tree here and get whatever your
        // bonsai serializer needs.
        return result.ToString();
    }

    private void ProcessExpressionSyntax(string staticModifier, string propertyName, ITypeSymbol returnType, ExpressionSyntax expression, StringBuilder source)
    {
        // You could process the syntax tree to translate to bonsai
        source.AppendLine(
            $"""
                public {staticModifier}string {propertyName}Bonsai() => {SymbolDisplay.FormatLiteral(ProcessTypeSyntax(returnType) + " Expression: " + expression.GetText().ToString(), true)};
            """);
    }

    /// <summary>
    /// Created on demand before each generation pass
    /// </summary>
    class SyntaxReceiver : ISyntaxContextReceiver
    {
        public List<IPropertySymbol> Properties { get; } = [];

        /// <summary>
        /// Called for every syntax node in the compilation, we can inspect the nodes and save any information useful for generation
        /// </summary>
        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            // any property with at least one attribute is a candidate for expression tree property generation
            if (context.Node is PropertyDeclarationSyntax propertyDeclarationSyntax
                && propertyDeclarationSyntax.AttributeLists.Count > 0)
            {
                // Get the symbol being declared by the field, and keep it if its annotated
                IPropertySymbol propertySymbol = context.SemanticModel.GetDeclaredSymbol(propertyDeclarationSyntax) as IPropertySymbol;
                if (propertySymbol.GetAttributes().Any(ad => ad.AttributeClass.ToDisplayString() == GenerateBonsaiAttributeName))
                {
                    this.Properties.Add(propertySymbol);
                }
            }
        }
    }
}
