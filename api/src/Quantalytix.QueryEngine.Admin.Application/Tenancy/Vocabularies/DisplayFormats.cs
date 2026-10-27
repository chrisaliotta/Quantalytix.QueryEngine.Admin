namespace Quantalytix.QueryEngine.Admin.Application.Tenancy;

/// <summary>
/// The <c>display_format</c> vocabulary: the presentation hints offered for variables, calculated measures and report columns.
/// The columns are free text; these are the offered values. Served by the tenant module until the models and reports modules
/// declare it.
/// </summary>
public static class DisplayFormats
{
    /// <summary>The vocabulary's definition, as the web client reads it.</summary>
    public static readonly Vocabulary.Vocabulary Definition = new()
    {
        Name = "display_format",
        Key = "displayFormat",
        Description = "Presentation hints offered for variables, calculated measures and report columns.",
        Values =
        [
            new() { Value = "number", Label = "Number", Description = "A plain number." },
            new() { Value = "currency", Label = "Currency", Description = "An amount in the model's currency." },
            new() { Value = "percent", Label = "Percent", Description = "A ratio shown as a percentage." },
            new() { Value = "basis_points", Label = "Basis points", Description = "A ratio shown in hundredths of a percent." }
        ]
    };
}
