namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>The contract's <c>ReleaseTargetArea</c> values other than the pin kinds, which name identities.</summary>
public static class ReleaseTargetAreas
{
    /// <summary>A manifest row; <c>itemId</c> is the dataset.</summary>
    public const string Dataset = "dataset";

    /// <summary>A report; <c>itemId</c> is the report and <c>itemKey</c> a source key.</summary>
    public const string Report = "report";
}
