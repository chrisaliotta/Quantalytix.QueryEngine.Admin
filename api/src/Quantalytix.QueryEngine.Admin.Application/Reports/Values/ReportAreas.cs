using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>The report editor areas a report violation's target names; the item is named by its key.</summary>
public static class ReportAreas
{
    /// <summary>The settings and defaults.</summary>
    public const string Settings = "settings";

    /// <summary>The rows; <c>itemKey</c> is a row key.</summary>
    public const string Rows = "rows";

    /// <summary>The sources; <c>itemKey</c> is a source key.</summary>
    public const string Sources = "sources";

    /// <summary>The source profiles; <c>itemKey</c> is a profile key.</summary>
    public const string Profiles = "profiles";

    /// <summary>The calculation dimensions; <c>itemKey</c> is a calculation dimension key.</summary>
    public const string Dimensions = "dimensions";

    /// <summary>The variable scopes; <c>itemKey</c> is a variable key.</summary>
    public const string Scopes = "scopes";

    /// <summary>The columns; <c>itemKey</c> is a column key.</summary>
    public const string Columns = "columns";

    /// <summary>The styles; <c>itemKey</c> is a style key.</summary>
    public const string Styles = "styles";

    /// <summary>A target in an area.</summary>
    /// <param name="area">One of this class's areas.</param>
    /// <param name="itemKey">The item's key; null when the violation names no item.</param>
    /// <returns>The target.</returns>
    public static ViolationTarget Target(string area, string? itemKey = null)
    {
        return new ViolationTarget { Area = area, ItemKey = itemKey };
    }
}
