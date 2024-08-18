namespace Coplt.Dropping;

[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, Inherited = false)]
public sealed class DroppingAttribute : Attribute
{
    /// <summary>
    /// <c>false</c> to disable inherit
    /// </summary>
    public bool AllowInherit { get; set; }
    /// <summary>
    /// <c>true</c> for unmanaged by default, will gen the finalizer
    /// </summary>
    public bool Unmanaged { get; set; }
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property, Inherited = false)]
public sealed class DropAttribute : Attribute
{
    /// <summary>
    /// Calling order
    /// </summary>
    public int Order { get; set; }
    /// <summary>
    /// Will gen the finalizer, and call this in finalizer
    /// </summary>
    public bool Unmanaged { get; set; }
}
