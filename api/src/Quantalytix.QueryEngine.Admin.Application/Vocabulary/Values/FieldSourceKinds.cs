namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>field_source_kind</c> vocabulary: where a logical field's value comes from.</summary>
public static class FieldSourceKinds
{
    /// <summary>A physical column; the source column is required.</summary>
    public const string Column = "column";

    /// <summary>Typed extraction from a JSON column; a value that does not convert to the data type is null.</summary>
    public const string JsonPath = "json_path";

    /// <summary>A validated row-level expression over other fields.</summary>
    public const string Expression = "expression";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "field_source_kind",
        Key = "fieldSourceKind",
        Description = "Where a logical field's value comes from.",
        Values =
        [
            new() { Value = Column, Label = "Column", Description = "A physical column; the source column is required." },
            new() { Value = JsonPath, Label = "Json path", Description = "Typed extraction from a JSON column; a value that does not convert to the data type is null." },
            new() { Value = Expression, Label = "Expression", Description = "A validated row-level expression over other fields." }
        ]
    };
}
