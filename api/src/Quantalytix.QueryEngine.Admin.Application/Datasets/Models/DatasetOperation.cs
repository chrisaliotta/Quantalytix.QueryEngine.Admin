using System.Text.Json.Serialization;
using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Measures;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// The contract's <c>DatasetOperation</c> union, discriminated by <c>op</c> and named after the port's edit methods (OI-01). One
/// change set covers one dataset version and its dataset's identity. The derived types are declared in the contract's order, which
/// the conformance check pairs alternatives by.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "op")]
[JsonDerivedType(typeof(DatasetUpdateDatasetOperation), "updateDataset")]
[JsonDerivedType(typeof(DatasetSaveSettingsOperation), "saveSettings")]
[JsonDerivedType(typeof(DatasetRenameSourceTableOperation), "renameSourceTable")]
[JsonDerivedType(typeof(DatasetSaveFieldOperation), "saveField")]
[JsonDerivedType(typeof(DatasetDeleteFieldOperation), "deleteField")]
[JsonDerivedType(typeof(DatasetAddFieldsFromColumnsOperation), "addFieldsFromColumns")]
[JsonDerivedType(typeof(DatasetSaveGrainOperation), "saveGrain")]
[JsonDerivedType(typeof(DatasetDeleteGrainOperation), "deleteGrain")]
[JsonDerivedType(typeof(DatasetSaveBindingOperation), "saveBinding")]
[JsonDerivedType(typeof(DatasetDeleteBindingOperation), "deleteBinding")]
[JsonDerivedType(typeof(DatasetSaveMeasureOperation), "saveMeasure")]
[JsonDerivedType(typeof(DatasetDeleteMeasureOperation), "deleteMeasure")]
public abstract record DatasetOperation;

/// <summary><c>updateDataset</c>: changes the identity fields; the only operation a version that is not a draft accepts.</summary>
public sealed record DatasetUpdateDatasetOperation : DatasetOperation
{
    /// <summary>The fields to change.</summary>
    public required DatasetPatch Patch { get; init; }
}

/// <summary><c>saveSettings</c>: replaces the version row's settings.</summary>
public sealed record DatasetSaveSettingsOperation : DatasetOperation
{
    /// <summary>The new settings.</summary>
    public required DatasetVersionSettings Settings { get; init; }
}

/// <summary><c>renameSourceTable</c>: renames the managed table the draft reads, while no other version reads it (OI-33).</summary>
public sealed record DatasetRenameSourceTableOperation : DatasetOperation
{
    /// <summary>The table's new name.</summary>
    public required string TableName { get; init; }
}

/// <summary><c>saveField</c>: creates or updates a field.</summary>
public sealed record DatasetSaveFieldOperation : DatasetOperation
{
    /// <summary>The field.</summary>
    public required DatasetFieldInput Input { get; init; }
}

/// <summary><c>deleteField</c>: removes a field nothing reads by id.</summary>
public sealed record DatasetDeleteFieldOperation : DatasetOperation
{
    /// <summary>The field, stored or temporary.</summary>
    public required string FieldId { get; init; }
}

/// <summary><c>addFieldsFromColumns</c>: creates one column field per named column of the source object.</summary>
public sealed record DatasetAddFieldsFromColumnsOperation : DatasetOperation
{
    /// <summary>The columns.</summary>
    public required IReadOnlyList<string> Columns { get; init; }
}

/// <summary><c>saveGrain</c>: creates or updates a grain.</summary>
public sealed record DatasetSaveGrainOperation : DatasetOperation
{
    /// <summary>The grain.</summary>
    public required DatasetGrainInput Input { get; init; }
}

/// <summary><c>deleteGrain</c>: removes a grain no time policy selects by.</summary>
public sealed record DatasetDeleteGrainOperation : DatasetOperation
{
    /// <summary>The grain, stored or temporary.</summary>
    public required string GrainId { get; init; }
}

/// <summary><c>saveBinding</c>: creates or updates a binding with its time axis.</summary>
public sealed record DatasetSaveBindingOperation : DatasetOperation
{
    /// <summary>The binding.</summary>
    public required DatasetBindingInput Input { get; init; }
}

/// <summary><c>deleteBinding</c>: removes a binding that is not required and that nothing reads.</summary>
public sealed record DatasetDeleteBindingOperation : DatasetOperation
{
    /// <summary>The binding, stored or temporary.</summary>
    public required string BindingId { get; init; }
}

/// <summary><c>saveMeasure</c>: creates or updates a measure with its components and time policy.</summary>
public sealed record DatasetSaveMeasureOperation : DatasetOperation
{
    /// <summary>The measure.</summary>
    public required DatasetMeasureInput Input { get; init; }
}

/// <summary><c>deleteMeasure</c>: removes a measure.</summary>
public sealed record DatasetDeleteMeasureOperation : DatasetOperation
{
    /// <summary>The measure, stored or temporary.</summary>
    public required string MeasureId { get; init; }
}
