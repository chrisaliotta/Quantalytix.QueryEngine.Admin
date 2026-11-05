namespace Quantalytix.QueryEngine.Admin.Application.Tenancy;

/// <summary>
/// The <c>expression_language_version</c> vocabulary: the grammar versions a dataset version's expressions may declare. The
/// column is free text; these are the offered values. Served by the tenant module until the datasets module declares it.
/// </summary>
public static class ExpressionLanguageVersions
{
    /// <summary>The original grammar.</summary>
    public const string Version10 = "1.0";

    /// <summary>Grammar 1.0 plus <c>MONTHS_BETWEEN(start, end)</c> and <c>POWER(x, y)</c>.</summary>
    public const string Version11 = "1.1";

    /// <summary>Grammar 1.1 plus <c>@periodDays</c>, <c>@yearDays</c> and <c>YEARFRAC(convention)</c> in measure formulas.</summary>
    public const string Version12 = "1.2";

    /// <summary>The vocabulary's definition, as the web client reads it.</summary>
    public static readonly Vocabulary.Vocabulary Definition = new()
    {
        Name = "expression_language_version",
        Key = "expressionLanguageVersion",
        Description = "Grammar versions a dataset version's expressions may declare.",
        Values =
        [
            new() { Value = Version10, Label = "1.0", Description = "Original grammar." },
            new() { Value = Version11, Label = "1.1", Description = "Adds MONTHS_BETWEEN and POWER." },
            new() { Value = Version12, Label = "1.2", Description = "Adds @periodDays, @yearDays and YEARFRAC in measure formulas." }
        ]
    };

    /// <summary>Whether a language has what a version adds; each version has everything the versions before it have.</summary>
    /// <param name="version">A dataset version's <c>expression_language_version</c>.</param>
    /// <param name="required">The version that adds a feature, such as <see cref="Version11"/> for <c>POWER</c>.</param>
    /// <returns>True when <paramref name="version"/> is <paramref name="required"/> or later.</returns>
    public static bool Includes(string version, string required)
    {
        return Definition.IsAtLeast(version, required);
    }
}
