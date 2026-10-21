namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>formula_language_version</c> vocabulary: the expression grammar of a calculation model's formulas.</summary>
public static class FormulaLanguageVersions
{
    /// <summary>Arithmetic, comparisons, <c>CASE</c>, <c>COALESCE</c>, <c>NULLIF</c> and <c>ABS</c>.</summary>
    public const string Version10 = "1.0";

    /// <summary>Language 1.0 plus <c>POWER(x, y)</c>.</summary>
    public const string Version11 = "1.1";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "formula_language_version",
        Description = "The expression grammar of a calculation model's formulas and attribution expressions.",
        Values =
        [
            new() { Value = Version10, Description = "Arithmetic, comparisons, CASE, COALESCE, NULLIF and ABS." },
            new() { Value = Version11, Description = "Language 1.0 plus POWER(x, y)." }
        ]
    };
}
