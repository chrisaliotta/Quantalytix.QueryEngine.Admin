namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>The codes of a refused item delete inside a dataset version's change set, one per item kind.</summary>
public static class DatasetItemBlockerCodes
{
    /// <summary>A field that grains, bindings or measure components read.</summary>
    public const string FieldInUse = "field_in_use";

    /// <summary>A grain a measure's time policy selects by.</summary>
    public const string GrainInUse = "grain_in_use";

    /// <summary>A binding that bridge bindings, time policies or the default time binding read.</summary>
    public const string BindingInUse = "binding_in_use";

    /// <summary>A binding the dataset requires.</summary>
    public const string RequiredBinding = "required_binding";
}
