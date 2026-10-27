namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>formula_language_version</c> vocabulary: the expression grammar of a calculation model's formulas.</summary>
public static class FormulaLanguageVersions
{
    /// <summary>Arithmetic, comparisons, <c>CASE</c>, <c>COALESCE</c>, <c>NULLIF</c> and <c>ABS</c>.</summary>
    public const string Version10 = "1.0";

    /// <summary>Language 1.0 plus <c>POWER(x, y)</c>.</summary>
    public const string Version11 = "1.1";

    /// <summary>
    /// Language 1.1 plus the model's declared parameters as <c>@key</c>, string literals compared with text, and
    /// <c>YEARFRAC(convention)</c>.
    /// </summary>
    public const string Version12 = "1.2";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "formula_language_version",
        Key = "formulaLanguageVersion",
        Description = "The expression grammar of a calculation model's formulas and attribution expressions.",
        Values =
        [
            new() { Value = Version10, Label = "1.0", Description = "Arithmetic, comparisons, CASE, COALESCE, NULLIF and ABS." },
            new() { Value = Version11, Label = "1.1", Description = "Language 1.0 plus POWER(x, y)." },
            new() { Value = Version12, Label = "1.2", Description = "Language 1.1 plus declared parameters (@key), text comparisons and YEARFRAC(convention)." }
        ]
    };

    /// <summary>Whether a language has what a version adds; each version has everything the versions before it have.</summary>
    /// <param name="version">A model's <c>formula_language_version</c>.</param>
    /// <param name="required">The version that adds a feature, such as <see cref="Version11"/> for <c>POWER</c>.</param>
    /// <returns>True when <paramref name="version"/> is <paramref name="required"/> or later.</returns>
    public static bool Includes(string version, string required)
    {
        return Order(version) >= Order(required);
    }

    /// <summary>A version's place in the vocabulary, which lists the versions oldest first.</summary>
    private static int Order(string version)
    {
        return Definition.Values.Select(value => value.Value).ToList().IndexOf(version);
    }
}
