namespace Coplt.Dropping;

/// <summary>
/// Mark this type as needing to be disposable
/// </summary>
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

/// <summary>
/// Mark the dispose target, can be a method, field, or property
/// </summary>
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
