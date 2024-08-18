using System.Collections.Immutable;
using System.Text;
using Coplt.Analyzers.Utilities;
using Coplt.Dropping.Analyzers.Generators.Templates;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Coplt.Dropping.Analyzers.Generators;

[Generator]
public class DroppingGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var sources = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Coplt.Dropping.DroppingAttribute",
            static (syntax, _) =>
                syntax is ClassDeclarationSyntax or StructDeclarationSyntax or RecordDeclarationSyntax,
            static (ctx, _) =>
            {
                var diagnostics = new List<Diagnostic>();
                var attr = ctx.Attributes.First();
                var syntax = (TypeDeclarationSyntax)ctx.TargetNode;
                var symbol = (INamedTypeSymbol)ctx.TargetSymbol;
                var nullable = ctx.SemanticModel.Compilation.Options.NullableContextOptions;
                var rawFullName = symbol.ToDisplayString();
                var nameWraps = symbol.WrapNames();
                var nameWrap = symbol.WrapName();

                var usings = new HashSet<string>();
                Utils.GetUsings(syntax, usings);
                var genBase = new GenBase(rawFullName, nullable, usings, nameWraps, nameWrap);

                var can_inherit = symbol is { IsValueType: false, IsSealed: false };

                var dropping_attr = new DroppingAttr(can_inherit, false);
                {
                    foreach (var kv in attr.NamedArguments)
                    {
                        if (kv is { Key: "AllowInherit", Value.Value: false }) dropping_attr.Inherit = false;
                        else if (kv is { Key: "Unmanaged", Value.Value: true }) dropping_attr.Unmanaged = true;
                    }
                }

                var members = symbol.GetMembers()
                    .Where(static m => m is (IMethodSymbol or IPropertySymbol or IFieldSymbol) and
                        { CanBeReferencedByName: true })
                    .Select(m =>
                    {
                        var drop_attr = new DropAttr(0, dropping_attr.Unmanaged);
                        var attrs = m.GetAttributes();
                        foreach (var attr in attrs)
                        {
                            if (attr.AttributeClass?.ToDisplayString() != "Coplt.Dropping.DropAttribute") continue;
                            foreach (var kv in attr.NamedArguments)
                            {
                                if (kv is { Key: "Order", Value.Value: int Order }) drop_attr.Order = Order;
                                else if (kv is { Key: "Unmanaged", Value.Value: bool Unmanaged })
                                    drop_attr.Unmanaged = Unmanaged;
                            }
                        }
                        var member_type = m switch
                        {
                            IPropertySymbol => MemberType.Prop,
                            IFieldSymbol => MemberType.Filed,
                            _ => MemberType.Method,
                        };

                        var disposing = m is IMethodSymbol
                            {
                                Parameters: [{ Type.SpecialType: SpecialType.System_Boolean }]
                            }
                            or IMethodSymbol
                            {
                                IsStatic: true, Parameters: [_, { Type.SpecialType: SpecialType.System_Boolean }]
                            };

                        return new MemberInfo(member_type, m.Name, m.IsStatic, drop_attr, disposing);
                    })
                    .OrderBy(a => a.attr.Order)
                    .ToImmutableArray();

                Accessibility? BaseDispose = null;

                {
                    var bt = symbol.BaseType;
                    while (bt != null)
                    {
                        var ba = bt.GetAttributes().FirstOrDefault(a =>
                            a.AttributeClass?.ToDisplayString() == "Coplt.Dropping.DroppingAttribute");
                        if (ba != null)
                        {
                            BaseDispose = Accessibility.Protected;
                            dropping_attr.Inherit = true;
                            break;
                        }

                        var base_dispose = bt.GetMembers().FirstOrDefault(m => m is IMethodSymbol
                        {
                            Name: "Dispose", IsVirtual: true,
                            DeclaredAccessibility: not Accessibility.Private,
                            Parameters: [{ Type.SpecialType: SpecialType.System_Boolean }],
                        });
                        if (base_dispose != null)
                        {
                            BaseDispose = base_dispose.DeclaredAccessibility;
                            dropping_attr.Inherit = true;
                            break;
                        }

                        bt = bt.BaseType;
                    }
                }

                var info = new TargetInfo(dropping_attr, symbol.IsValueType, members, BaseDispose);

                return (info, genBase, AlwaysEq.Create(diagnostics));
            }
        );
        context.RegisterSourceOutput(sources, static (ctx, input) =>
        {
            var (info, genBase, diagnostics) = input;
            if (diagnostics.Value.Count > 0)
            {
                foreach (var diagnostic in diagnostics.Value)
                {
                    ctx.ReportDiagnostic(diagnostic);
                }
            }
            var code = new DroppingTemplate(
                    genBase, info
                )
                .Gen();
            var sourceText = SourceText.From(code, Encoding.UTF8);
            var rawSourceFileName = genBase.FileFullName;
            var sourceFileName = $"{rawSourceFileName}.dropping.g.cs";
            ctx.AddSource(sourceFileName, sourceText);
        });
    }
}
