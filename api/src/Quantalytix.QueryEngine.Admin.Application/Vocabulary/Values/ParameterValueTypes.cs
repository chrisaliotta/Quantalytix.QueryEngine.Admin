namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>parameter_value_type</c> vocabulary: what a model parameter holds, which decides where formulas may read it.</summary>
public static class ParameterValueTypes
{
    /// <summary>A number, which formulas read wherever they read a value.</summary>
    public const string Number = "number";

    /// <summary>Text, which formulas compare with text by <c>=</c>, <c>&lt;&gt;</c> or <c>IN</c>, or pass to <c>YEARFRAC</c>.</summary>
    public const string Text = "text";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "parameter_value_type",
        Key = "parameterValueType",
        Description = "What a model parameter holds, which decides where its formulas may read it.",
        Values =
        [
            new() { Value = Number, Label = "Number", Description = "A number, which formulas read wherever they read a value." },
            new() { Value = Text, Label = "Text", Description = "Text, which formulas compare with text by =, <> or IN, or pass to YEARFRAC." }
        ]
    };
}
