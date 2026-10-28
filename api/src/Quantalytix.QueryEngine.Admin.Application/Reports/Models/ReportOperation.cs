using System.Text.Json.Serialization;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// The contract's <c>ReportOperation</c> union, discriminated by <c>op</c> and named after the port's edit methods (OI-01). Report
/// content is named by key, so the report change set has no temporary ids. The derived types are declared in the contract's order,
/// which the conformance check pairs alternatives by.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "op")]
[JsonDerivedType(typeof(ReportUpdateSettingsOperation), "updateSettings")]
[JsonDerivedType(typeof(ReportSaveRowOperation), "saveRow")]
[JsonDerivedType(typeof(ReportMoveRowOperation), "moveRow")]
[JsonDerivedType(typeof(ReportDeleteRowOperation), "deleteRow")]
[JsonDerivedType(typeof(ReportSaveSourceOperation), "saveSource")]
[JsonDerivedType(typeof(ReportDeleteSourceOperation), "deleteSource")]
[JsonDerivedType(typeof(ReportSetSourceVariablesOperation), "setSourceVariables")]
[JsonDerivedType(typeof(ReportSetRowRulesOperation), "setRowRules")]
[JsonDerivedType(typeof(ReportSetLeftOutOperation), "setLeftOut")]
[JsonDerivedType(typeof(ReportSaveProfileOperation), "saveProfile")]
[JsonDerivedType(typeof(ReportDeleteProfileOperation), "deleteProfile")]
[JsonDerivedType(typeof(ReportSaveCalculationDimensionOperation), "saveCalculationDimension")]
[JsonDerivedType(typeof(ReportDeleteCalculationDimensionOperation), "deleteCalculationDimension")]
[JsonDerivedType(typeof(ReportSetScenariosOperation), "setScenarios")]
[JsonDerivedType(typeof(ReportSetVariableScopesOperation), "setVariableScopes")]
[JsonDerivedType(typeof(ReportSetColumnsOperation), "setColumns")]
[JsonDerivedType(typeof(ReportSaveStyleOperation), "saveStyle")]
[JsonDerivedType(typeof(ReportDeleteStyleOperation), "deleteStyle")]
public abstract record ReportOperation;

/// <summary><c>updateSettings</c>: changes the settings the patch names.</summary>
public sealed record ReportUpdateSettingsOperation : ReportOperation
{
    /// <summary>The settings to change.</summary>
    public required ReportSettingsPatch Patch { get; init; }
}

/// <summary><c>saveRow</c>: creates the row when its key is new, otherwise updates it, and places it among its siblings.</summary>
public sealed record ReportSaveRowOperation : ReportOperation
{
    /// <summary>The row.</summary>
    public required ReportRow Row { get; init; }

    /// <summary>The index among the row's siblings.</summary>
    public required int Index { get; init; }
}

/// <summary><c>moveRow</c>: places a row, with the rows under it, under a new parent.</summary>
public sealed record ReportMoveRowOperation : ReportOperation
{
    /// <summary>The row.</summary>
    public required string Key { get; init; }

    /// <summary>Where it goes.</summary>
    public required ReportRowPosition To { get; init; }
}

/// <summary><c>deleteRow</c>: removes a row; its children move up and what names it goes (OI-56).</summary>
public sealed record ReportDeleteRowOperation : ReportOperation
{
    /// <summary>The row.</summary>
    public required string Key { get; init; }
}

/// <summary><c>saveSource</c>: creates the source when its key is new, otherwise updates its name and time field.</summary>
public sealed record ReportSaveSourceOperation : ReportOperation
{
    /// <summary>The source.</summary>
    public required ReportSourceInput Input { get; init; }

    /// <summary>A source whose variables, rules and left-out items a new source starts with.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CopyFromSourceKey { get; init; }
}

/// <summary><c>deleteSource</c>: removes a source with its report bridge and the profile picks naming it (OI-56).</summary>
public sealed record ReportDeleteSourceOperation : ReportOperation
{
    /// <summary>The source.</summary>
    public required string Key { get; init; }
}

/// <summary><c>setSourceVariables</c>: replaces the variables a source supplies.</summary>
public sealed record ReportSetSourceVariablesOperation : ReportOperation
{
    /// <summary>The source.</summary>
    public required string SourceKey { get; init; }

    /// <summary>Its complete variable list.</summary>
    public required IReadOnlyList<ReportSourceVariable> Variables { get; init; }
}

/// <summary><c>setRowRules</c>: replaces the rules of one row of a bridge source.</summary>
public sealed record ReportSetRowRulesOperation : ReportOperation
{
    /// <summary>The bridge source.</summary>
    public required string SourceKey { get; init; }

    /// <summary>The data row.</summary>
    public required string RowKey { get; init; }

    /// <summary>The row's complete rule list.</summary>
    public required IReadOnlyList<ReportSourceRule> Rules { get; init; }
}

/// <summary><c>setLeftOut</c>: replaces the items a bridge source leaves out on purpose.</summary>
public sealed record ReportSetLeftOutOperation : ReportOperation
{
    /// <summary>The bridge source.</summary>
    public required string SourceKey { get; init; }

    /// <summary>The left-out item keys.</summary>
    public required IReadOnlyList<string> ItemKeys { get; init; }
}

/// <summary><c>saveProfile</c>: creates a profile, or updates the one <see cref="OriginalKey"/> names.</summary>
public sealed record ReportSaveProfileOperation : ReportOperation
{
    /// <summary>The profile.</summary>
    public required ReportProfile Profile { get; init; }

    /// <summary>The key of the stored profile edited; absent for a new one.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OriginalKey { get; init; }
}

/// <summary><c>deleteProfile</c>: removes a profile other than the default.</summary>
public sealed record ReportDeleteProfileOperation : ReportOperation
{
    /// <summary>The profile.</summary>
    public required string Key { get; init; }
}

/// <summary><c>saveCalculationDimension</c>: creates a calculation dimension, or updates the one <see cref="OriginalKey"/> names.</summary>
public sealed record ReportSaveCalculationDimensionOperation : ReportOperation
{
    /// <summary>The calculation dimension.</summary>
    public required ReportCalculationDimension Input { get; init; }

    /// <summary>The key of the stored calculation dimension edited; absent for a new one.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OriginalKey { get; init; }
}

/// <summary><c>deleteCalculationDimension</c>: removes a calculation dimension and its key from the defaults (OI-56).</summary>
public sealed record ReportDeleteCalculationDimensionOperation : ReportOperation
{
    /// <summary>The calculation dimension.</summary>
    public required string Key { get; init; }
}

/// <summary><c>setScenarios</c>: replaces the scenario roles.</summary>
public sealed record ReportSetScenariosOperation : ReportOperation
{
    /// <summary>The roles.</summary>
    public required IReadOnlyList<long> RoleIds { get; init; }
}

/// <summary><c>setVariableScopes</c>: replaces the variable scopes.</summary>
public sealed record ReportSetVariableScopesOperation : ReportOperation
{
    /// <summary>One scope per scoped variable.</summary>
    public required IReadOnlyList<ReportVariableScope> Scopes { get; init; }
}

/// <summary><c>setColumns</c>: replaces the columns, in order.</summary>
public sealed record ReportSetColumnsOperation : ReportOperation
{
    /// <summary>The columns.</summary>
    public required IReadOnlyList<ReportColumn> Columns { get; init; }
}

/// <summary><c>saveStyle</c>: creates the style when its key is new, otherwise replaces it.</summary>
public sealed record ReportSaveStyleOperation : ReportOperation
{
    /// <summary>The style.</summary>
    public required ReportStyle Style { get; init; }
}

/// <summary><c>deleteStyle</c>: removes a style no row names.</summary>
public sealed record ReportDeleteStyleOperation : ReportOperation
{
    /// <summary>The style.</summary>
    public required string Key { get; init; }
}
