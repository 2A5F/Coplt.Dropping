using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Coplt.Dropping.Analyzers.Generators.Templates;

public record struct DroppingAttr(bool Inherit, bool Unmanaged);
public record struct DropAttr(int Order, bool Unmanaged);

public record struct TargetInfo(
    DroppingAttr attr,
    bool Struct,
    ImmutableArray<MemberInfo> members,
    Accessibility? BaseDispose
);

public enum MemberType
{
    Filed,
    Prop,
    Method,
}

public record struct MemberInfo(MemberType type, string name, bool Static, DropAttr attr);

public class DroppingTemplate(GenBase GenBase, TargetInfo info) : ATemplate(GenBase)
{
    protected override void DoGen()
    {
        sb.Append(GenBase.Target.Code);
        sb.AppendLine($" : global::System.IDisposable");
        sb.AppendLine("{");

        var no_base = info.BaseDispose is null;

        var any_unmanaged = info.members.Any(m => m.attr.Unmanaged);

        var finalizer = !info.Struct && no_base && (info.attr.Inherit || any_unmanaged);

        #region Dispose bool

        if (info.attr.Inherit)
        {
            sb.AppendLine();
            sb.AppendLine($"    protected {(no_base ? "virtual" : "override")} void Dispose(bool disposing)");
            sb.AppendLine($"    {{");
            foreach (var member in info.members)
            {
                var cond = member.attr.Unmanaged ? "" : $"if (disposing) ";
                if (member.type is MemberType.Filed or MemberType.Prop)
                {
                    sb.AppendLine($"        {cond}{member.name}.Dispose();");
                }
                else
                {
                    if (member.Static)
                        sb.AppendLine($"        {cond}{member.name}(this);");
                    else
                        sb.AppendLine($"        {cond}{member.name}();");
                }
            }
            if (!no_base) sb.AppendLine($"        base.Dispose(disposing);");
            sb.AppendLine($"    }}");
        }

        #endregion

        #region Dispose

        if (no_base)
        {
            sb.AppendLine();
            sb.AppendLine($"    public void Dispose()");
            sb.AppendLine($"    {{");
            if (info.attr.Inherit)
            {
                sb.AppendLine($"        Dispose(true);");
            }
            else
            {
                foreach (var member in info.members)
                {
                    if (member.type is MemberType.Filed or MemberType.Prop)
                    {
                        sb.AppendLine($"        {member.name}.Dispose();");
                    }
                    else
                    {
                        if (member.Static)
                            sb.AppendLine($"        {member.name}(this);");
                        else
                            sb.AppendLine($"        {member.name}();");
                    }
                }
            }
            if (finalizer)
            {
                sb.AppendLine($"        GC.SuppressFinalize(this);");
            }
            sb.AppendLine($"    }}");
        }

        #endregion

        #region Finalizer

        if (finalizer)
        {
            sb.AppendLine();
            sb.AppendLine($"    ~{TypeName}()");
            sb.AppendLine($"    {{");
            if (info.attr.Inherit)
            {
                sb.AppendLine($"        Dispose(false);");
            }
            else
            {
                sb.AppendLine($"        Dispose();");
            }
            sb.AppendLine($"    }}");
        }

        #endregion

        sb.AppendLine();
        sb.AppendLine("}");
    }
}
