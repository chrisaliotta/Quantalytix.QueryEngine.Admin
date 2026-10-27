namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>dimension_kind</c> vocabulary: the kind of a canonical dimension.</summary>
public static class DimensionKinds
{
    /// <summary>Flat or hierarchy-capable set of named members without inherent range semantics.</summary>
    public const string Categorical = "categorical";

    /// <summary>Parent/child member hierarchy is a first-class grouping and filtering concept.</summary>
    public const string Hierarchical = "hierarchical";

    /// <summary>Members are assigned from typed, non-overlapping bands.</summary>
    public const string Range = "range";

    /// <summary>Canonical temporal dimension used with a time binding.</summary>
    public const string Time = "time";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "dimension_kind",
        Key = "dimensionKind",
        Description = "Kind of a canonical dimension.",
        Values =
        [
            new() { Value = Categorical, Label = "Categorical", Description = "Flat or hierarchy-capable set of named members without inherent range semantics." },
            new() { Value = Hierarchical, Label = "Hierarchical", Description = "Parent/child member hierarchy is a first-class grouping and filtering concept." },
            new() { Value = Range, Label = "Range", Description = "Members are assigned from typed, non-overlapping bands." },
            new() { Value = Time, Label = "Time", Description = "Canonical temporal dimension used with a time binding." }
        ]
    };
}
