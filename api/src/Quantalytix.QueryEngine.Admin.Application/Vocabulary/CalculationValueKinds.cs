namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>calculation_value_kind</c> vocabulary: how a calculation dimension's routes produce its values.</summary>
public static class CalculationValueKinds
{
    /// <summary>Routes resolve members of one canonical dimension.</summary>
    public const string Canonical = "canonical";

    /// <summary>Routes read raw values that mean the same in every dataset.</summary>
    public const string Scalar = "scalar";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "calculation_value_kind",
        Description = "How a calculation dimension's routes produce its values.",
        Values =
        [
            new() { Value = Canonical, Description = "Routes resolve members of one canonical dimension." },
            new() { Value = Scalar, Description = "Routes read raw values that mean the same in every dataset." }
        ]
    };
}
