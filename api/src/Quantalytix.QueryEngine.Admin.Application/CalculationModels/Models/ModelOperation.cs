using System.Text.Json.Serialization;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// The contract's <c>ModelOperation</c> union, discriminated by <c>op</c> and named after the port's edit methods (OI-01). Items are
/// named by key, which is fixed once saved, so the model change set has no temporary ids. The derived types are declared in the
/// contract's order, which the conformance check pairs alternatives by.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "op")]
[JsonDerivedType(typeof(ModelUpdateModelOperation), "updateModel")]
[JsonDerivedType(typeof(ModelSaveVariableOperation), "saveVariable")]
[JsonDerivedType(typeof(ModelDeleteVariableOperation), "deleteVariable")]
[JsonDerivedType(typeof(ModelSaveMeasureOperation), "saveMeasure")]
[JsonDerivedType(typeof(ModelDeleteMeasureOperation), "deleteMeasure")]
[JsonDerivedType(typeof(ModelSaveParameterOperation), "saveParameter")]
[JsonDerivedType(typeof(ModelDeleteParameterOperation), "deleteParameter")]
[JsonDerivedType(typeof(ModelSetAttributionOperation), "setAttribution")]
[JsonDerivedType(typeof(ModelClearAttributionOperation), "clearAttribution")]
public abstract record ModelOperation;

/// <summary>An operation that saves an item: a new one without <see cref="OriginalKey"/>, or the stored one it names.</summary>
public abstract record ModelSaveOperation : ModelOperation
{
    /// <summary>The key of the stored item edited; absent for a new item. A different input key is refused (keys are fixed).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OriginalKey { get; init; }
}

/// <summary><c>updateModel</c>: changes the model's settings.</summary>
public sealed record ModelUpdateModelOperation : ModelOperation
{
    /// <summary>The settings to change.</summary>
    public required CalcModelPatch Patch { get; init; }
}

/// <summary><c>saveVariable</c>: creates or edits a variable.</summary>
public sealed record ModelSaveVariableOperation : ModelSaveOperation
{
    /// <summary>The variable.</summary>
    public required ModelVariable Input { get; init; }
}

/// <summary><c>deleteVariable</c>: removes a variable with its attribution.</summary>
public sealed record ModelDeleteVariableOperation : ModelOperation
{
    /// <summary>The variable's key.</summary>
    public required string Key { get; init; }
}

/// <summary><c>saveMeasure</c>: creates or edits a calculated measure.</summary>
public sealed record ModelSaveMeasureOperation : ModelSaveOperation
{
    /// <summary>The calculated measure.</summary>
    public required ModelMeasure Input { get; init; }
}

/// <summary><c>deleteMeasure</c>: removes a calculated measure with its attribution.</summary>
public sealed record ModelDeleteMeasureOperation : ModelOperation
{
    /// <summary>The calculated measure's key.</summary>
    public required string Key { get; init; }
}

/// <summary><c>saveParameter</c>: creates or edits a parameter.</summary>
public sealed record ModelSaveParameterOperation : ModelSaveOperation
{
    /// <summary>The parameter.</summary>
    public required ModelParameterItem Input { get; init; }
}

/// <summary><c>deleteParameter</c>: removes a parameter.</summary>
public sealed record ModelDeleteParameterOperation : ModelOperation
{
    /// <summary>The parameter's key.</summary>
    public required string Key { get; init; }
}

/// <summary><c>setAttribution</c>: sets a metric's attribution in place of the one it has.</summary>
public sealed record ModelSetAttributionOperation : ModelOperation
{
    /// <summary>The attribution.</summary>
    public required Attribution Input { get; init; }
}

/// <summary><c>clearAttribution</c>: removes a metric's attribution, so the metric uses the default drivers.</summary>
public sealed record ModelClearAttributionOperation : ModelOperation
{
    /// <summary>The metric's key.</summary>
    public required string MetricKey { get; init; }
}
