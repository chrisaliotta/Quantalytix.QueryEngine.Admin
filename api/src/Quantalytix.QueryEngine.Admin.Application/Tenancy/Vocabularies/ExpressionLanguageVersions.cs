namespace Quantalytix.QueryEngine.Admin.Application.Tenancy;

/// <summary>
/// The <c>expression_language_version</c> vocabulary: the grammar versions a dataset version's expressions may declare. The
/// column is free text; these are the offered values. Served by the tenant module until the datasets module declares it.
/// </summary>
public static class ExpressionLanguageVersions
{
    /// <summary>The current grammar.</summary>
    public const string Version10 = "1.0";

    /// <summary>The vocabulary's definition, as the web client reads it.</summary>
    public static readonly Vocabulary.Vocabulary Definition = new()
    {
        Name = "expression_language_version",
        Key = "expressionLanguageVersion",
        Description = "Grammar versions a dataset version's expressions may declare.",
        Values =
        [
            new() { Value = Version10, Label = "1.0", Description = "Current grammar." }
        ]
    };
}
