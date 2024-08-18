using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Coplt.Analyzers.Utilities;

public static class Utils
{
    public static void GetUsings(SyntaxNode? node, HashSet<string> usings)
    {
        for (;;)
        {
            if (node == null) break;
            if (node is CompilationUnitSyntax cus)
            {
                foreach (var use in cus.Usings)
                {
                    usings.Add(use.ToString());
                }
                return;
            }
            node = node.Parent;
        }
    }

    public static string GetAccessStr(this Accessibility self) => self switch
    {
        Accessibility.Public => "public",
        Accessibility.Protected => "protected",
        Accessibility.Internal => "internal",
        Accessibility.Private => "private",
        _ => "",
    };

    public static NameWrap WrapName(this INamedTypeSymbol symbol)
    {
        var access = symbol.DeclaredAccessibility.GetAccessStr();
        var type_decl = symbol switch
        {
            { IsValueType: true, IsRecord: true, IsReadOnly: false } => "partial record struct",
            { IsValueType: true, IsRecord: true, IsReadOnly: true } => "readonly partial record struct",
            { IsValueType: true, IsRecord: false, IsReadOnly: true, IsRefLikeType: false } => "readonly partial struct",
            { IsValueType: true, IsRecord: false, IsReadOnly: false, IsRefLikeType: true } => "ref partial struct",
            { IsValueType: true, IsRecord: false, IsReadOnly: true, IsRefLikeType: true } =>
                "readonly ref partial struct",
            { IsValueType: true, IsRecord: false, IsReadOnly: false, IsRefLikeType: false } => "partial struct",
            { IsValueType: false, IsRecord: true, IsAbstract: false } => "partial record",
            { IsValueType: false, IsRecord: true, IsAbstract: true } => "abstract partial record",
            { IsValueType: false, IsStatic: true } => "static partial class",
            { IsValueType: false, IsAbstract: true, } => "abstract partial record",
            _ => "partial class",
        };
        var generic = string.Empty;
        if (symbol.IsGenericType)
        {
            var ps = new List<string>();
            foreach (var tp in symbol.TypeParameters)
            {
                var variance = tp.Variance switch
                {
                    VarianceKind.Out => "out ",
                    VarianceKind.In => "in ",
                    _ => "",
                };
                ps.Add($"{variance}{tp.ToDisplayString()}");
            }
            generic = $"<{string.Join(", ", ps)}>";
        }
        return new NameWrap($"{access} {type_decl} {symbol.Name}{generic}");
    }

    public static ImmutableList<NameWrap>? WrapNames(this INamedTypeSymbol symbol,
        ImmutableList<NameWrap>? childs = null)
    {
        NameWrap wrap;
        var parent = symbol.ContainingType;
        if (parent == null)
        {
            var ns = symbol.ContainingNamespace;
            if (ns == null || ns.IsGlobalNamespace) return childs;
            wrap = new NameWrap($"namespace {ns}");
            return childs?.Insert(0, wrap) ?? ImmutableList.Create(wrap);
        }
        wrap = parent.WrapName();
        return WrapNames(parent, childs?.Insert(0, wrap) ?? ImmutableList.Create(wrap));
    }

    public static DiagnosticDescriptor MakeError(LocalizableString msg)
        => new("EntityUniverse", msg, msg, "", DiagnosticSeverity.Error, true);

    public static DiagnosticDescriptor MakeWarning(LocalizableString msg)
        => new("EntityUniverse", msg, msg, "", DiagnosticSeverity.Warning, true);

    public static DiagnosticDescriptor MakeInfo(LocalizableString msg)
        => new("EntityUniverse", msg, msg, "", DiagnosticSeverity.Info, true);

    public static bool IsNotInstGenericType(this ITypeSymbol type) =>
        type is ITypeParameterSymbol
        || (type is INamedTypeSymbol { IsGenericType: true, TypeArguments: var typeArguments }
            && typeArguments.Any(IsNotInstGenericType))
        || (type is IArrayTypeSymbol { ElementType: var e } && e.IsNotInstGenericType())
        || (type is IPointerTypeSymbol { PointedAtType: var p } && p.IsNotInstGenericType());
    
    /// <summary>
    /// If the <paramref name="symbol"/> is a method symbol, returns <see langword="true"/> if the method's return type is "awaitable", but not if it's <see langword="dynamic"/>.
    /// If the <paramref name="symbol"/> is a type symbol, returns <see langword="true"/> if that type is "awaitable".
    /// An "awaitable" is any type that exposes a GetAwaiter method which returns a valid "awaiter". This GetAwaiter method may be an instance method or an extension method.
    /// </summary>
    public static bool IsAwaitableNonDynamic(this ISymbol symbol, SemanticModel semanticModel, int position)
    {
        IMethodSymbol? methodSymbol = symbol as IMethodSymbol;
        ITypeSymbol? typeSymbol = null;

        if (methodSymbol == null)
        {
            typeSymbol = symbol as ITypeSymbol;
            if (typeSymbol == null)
            {
                return false;
            }
        }

        // otherwise: needs valid GetAwaiter
        var potentialGetAwaiters = semanticModel.LookupSymbols(position,
                                                               container: typeSymbol ?? methodSymbol!.ReturnType.OriginalDefinition,
                                                               name: WellKnownMemberNames.GetAwaiter,
                                                               includeReducedExtensionMethods: true);
        var getAwaiters = potentialGetAwaiters.OfType<IMethodSymbol>().Where(x => !x.Parameters.Any());
        return getAwaiters.Any(VerifyGetAwaiter);
    }

    public static bool VerifyGetAwaiter(this IMethodSymbol getAwaiter)
    {
        var returnType = getAwaiter.ReturnType;

        // bool IsCompleted { get }
        if (!returnType.GetMembers().OfType<IPropertySymbol>().Any(p => p.Name == WellKnownMemberNames.IsCompleted && p.Type.SpecialType == SpecialType.System_Boolean && p.GetMethod != null))
        {
            return false;
        }

        var methods = returnType.GetMembers().OfType<IMethodSymbol>();

        // NOTE: (vladres) The current version of C# Spec, §7.7.7.3 'Runtime evaluation of await expressions', requires that
        // NOTE: the interface method INotifyCompletion.OnCompleted or ICriticalNotifyCompletion.UnsafeOnCompleted is invoked
        // NOTE: (rather than any OnCompleted method conforming to a certain pattern).
        // NOTE: Should this code be updated to match the spec?

        // void OnCompleted(Action) 
        // Actions are delegates, so we'll just check for delegates.
        // ReSharper disable once PossibleMultipleEnumeration
        if (!methods.Any(x => x.Name == WellKnownMemberNames.OnCompleted && x.ReturnsVoid && x.Parameters.Length == 1 && x.Parameters.First().Type.TypeKind == TypeKind.Delegate))
        {
            return false;
        }

        // void GetResult() || T GetResult()
        // ReSharper disable once PossibleMultipleEnumeration
        return methods.Any(m => m.Name == WellKnownMemberNames.GetResult && !m.Parameters.Any());
    }
}
