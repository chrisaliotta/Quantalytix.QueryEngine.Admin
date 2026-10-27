namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>The contract's <c>DimensionMemberImportMode</c>.</summary>
public static class DimensionImportModes
{
    /// <summary>Upserts by key and keeps every other member.</summary>
    public const string Merge = "merge";

    /// <summary>Makes the version's members exactly the imported rows.</summary>
    public const string Replace = "replace";

    /// <summary>Both modes.</summary>
    public static readonly IReadOnlyList<string> All = [Merge, Replace];
}
