using System.Collections.Immutable;
using System.Text;
using Coplt.Analyzers.Utilities;
using Coplt.Analyzers.Generators.Templates;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Coplt.Analyzers.Generators;

[Generator]
public unsafe class DroppingGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        #region Gen Attrs

        var attrs_stream = (UnmanagedMemoryStream)typeof(DroppingGenerator).Assembly.GetManifestResourceStream("Coplt.Dropping.Analyzers.Dropping.cs")!;
        var attrs_code = Encoding.UTF8.GetString(attrs_stream.PositionPointer, (int)attrs_stream.Length)!;
        context.RegisterPostInitializationOutput(ctx =>
        {
            ctx.AddEmbeddedAttributeDefinition();
            ctx.AddSource("Coplt.Dropping", attrs_code);
        });

        #endregion

        #region Gen Dropping

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
                var genBase = Utils.BuildGenBase(syntax, symbol, ctx.SemanticModel.Compilation);
                var can_inherit = symbol is { IsValueType: false, IsSealed: false };

                var dropping_attr = new DroppingAttr(can_inherit, DropFrom.Always);
                {
                    foreach (var kv in attr.NamedArguments)
                    {
                        if (kv is { Key: "AllowInherit", Value.Value: false }) dropping_attr.Inherit = false;
                        else if (kv is { Key: "From", Value.Value: var drop_from }) dropping_attr.From = (DropFrom)drop_from!;
                    }
                }

                var members = symbol.GetMembers()
                    .Select((m, i) =>
                    {
                        var drop_attr = new DropAttr(0, dropping_attr.From);
                        var attrs = m.GetAttributes();
                        foreach (var attr in attrs)
                        {
                            if (attr.AttributeClass?.ToDisplayString() != "Coplt.Dropping.DropAttribute") continue;
                            foreach (var kv in attr.NamedArguments)
                            {
                                if (kv is { Key: "Order", Value.Value: int Order }) drop_attr.Order = Order;
                                else if (kv is { Key: "From", Value.Value: var drop_from })
                                    drop_attr.From = (DropFrom)drop_from!;
                            }
                            goto find;
                        }
                        return default(MemberInfo?);
                        find:
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

                        var nullable = m switch
                        {
                            IPropertySymbol a =>
                                a.NullableAnnotation is NullableAnnotation.Annotated
                                || a.Type.TypeKind is not TypeKind.Error && a.Type.IsReferenceType,
                            IFieldSymbol a =>
                                a.NullableAnnotation is NullableAnnotation.Annotated
                                || a.Type.TypeKind is not TypeKind.Error && a.Type.IsReferenceType,
                            _ => false,
                        };

                        return new MemberInfo(member_type, m.Name, m.IsStatic, drop_attr, disposing, nullable, i);
                    })
                    .Where(static a => a.HasValue)
                    .Select(static a => a!.Value)
                    .OrderBy(static a => a.attr.Order)
                    .ThenBy(static a => a.type is MemberType.Method ? 0 : 1)
                    .ThenBy(static a => a.type is MemberType.Method ? a.DeclOrder : int.MaxValue - a.DeclOrder)
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

                return (info, genBase, symbol.Name, AlwaysEq.Create(diagnostics));
            }
        );
        context.RegisterSourceOutput(sources, static (ctx, input) =>
        {
            var (info, genBase, name, diagnostics) = input;
            if (diagnostics.Value.Count > 0)
            {
                foreach (var diagnostic in diagnostics.Value)
                {
                    ctx.ReportDiagnostic(diagnostic);
                }
            }
            var code = new DroppingTemplate(
                    genBase, name, info
                )
                .Gen();
            var sourceText = SourceText.From(code, Encoding.UTF8);
            var rawSourceFileName = genBase.FileFullName;
            var sourceFileName = $"{rawSourceFileName}.dropping.g.cs";
            ctx.AddSource(sourceFileName, sourceText);
        });

        #endregion
    }
}
