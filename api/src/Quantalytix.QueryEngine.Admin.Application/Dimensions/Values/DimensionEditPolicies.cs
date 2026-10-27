namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>The contract's <c>DimensionEditPolicy</c>: how a profile governs its dimension's edits (M11).</summary>
public static class DimensionEditPolicies
{
    /// <summary>Every edit is allowed; the dimension cannot be deleted.</summary>
    public const string Open = "open";

    /// <summary>Required members and roles cannot be deleted; everything else may change.</summary>
    public const string Extend = "extend";

    /// <summary>Every edit and freeze is refused; the owner changes the dimension through provisioning (OI-24).</summary>
    public const string Locked = "locked";
}
