namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>The contract's <c>PinState</c>.</summary>
public static class PinStates
{
    /// <summary>The pinned version is published.</summary>
    public const string Live = "live";

    /// <summary>The pinned version is retired.</summary>
    public const string Frozen = "frozen";

    /// <summary>The identity belongs to a report; only the active set pins these.</summary>
    public const string ReportOwned = "report_owned";
}
