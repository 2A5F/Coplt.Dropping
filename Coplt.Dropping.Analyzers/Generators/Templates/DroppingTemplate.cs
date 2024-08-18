using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Coplt.Analyzers.Utilities;

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

public record struct MemberInfo(MemberType type, string name, bool Static, DropAttr attr, bool disposing);

public class DroppingTemplate(GenBase GenBase, TargetInfo info) : ATemplate(GenBase)
{
    protected override void DoGen()
    {
        sb.Append(GenBase.Target.Code);
        sb.AppendLine($" : global::System.IDisposable");
        sb.AppendLine("{");

        var no_base = info.BaseDispose is null;

        var any_unmanaged = info.members.Any(m => m.attr.Unmanaged);
        var any_disposing = info.members.Any(m => m.disposing);

        var finalizer = !info.Struct && no_base && (info.attr.Inherit || any_unmanaged);

        var disposing_acc = info.BaseDispose?.GetAccessStr() ?? (info.attr.Inherit ? "protected" : "private");

        var virtual_override = info.attr.Inherit ? no_base ? " virtual" : " override" : "";

        var should_disposing = !info.Struct && (info.attr.Inherit || any_disposing);

        #region Dispose bool

        if (should_disposing)
        {
            sb.AppendLine();
            sb.AppendLine($"    {disposing_acc}{virtual_override} void Dispose(bool disposing)");
            sb.AppendLine($"    {{");
            foreach (var member in info.members)
            {
                var cond = member.disposing || member.attr.Unmanaged ? "" : $"if (disposing) ";
                var disposing = member.disposing ? "disposing" : "";
                var this_disposing = member.disposing ? ", disposing" : "";
                if (member.type is MemberType.Filed or MemberType.Prop)
                {
                    sb.AppendLine($"        {cond}{member.name}.Dispose();");
                }
                else
                {
                    if (member.Static)
                        sb.AppendLine($"        {cond}{member.name}(this{this_disposing});");
                    else
                        sb.AppendLine($"        {cond}{member.name}({disposing});");
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
            if (should_disposing)
            {
                sb.AppendLine($"        Dispose(true);");
            }
            else
            {
                foreach (var member in info.members)
                {
                    var disposing = member.disposing ? "true" : "";
                    var this_disposing = member.disposing ? ", true" : "";
                    if (member.type is MemberType.Filed or MemberType.Prop)
                    {
                        sb.AppendLine($"        {member.name}.Dispose();");
                    }
                    else
                    {
                        if (member.Static)
                            sb.AppendLine($"        {member.name}(this{this_disposing});");
                        else
                            sb.AppendLine($"        {member.name}({disposing});");
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
            if (should_disposing)
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
