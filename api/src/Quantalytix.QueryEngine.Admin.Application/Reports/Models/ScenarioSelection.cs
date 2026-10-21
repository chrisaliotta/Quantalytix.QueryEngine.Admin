namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// The scenario a comparison names: an assignable member of the report's scenario dimension, or, with <see cref="Latest"/>, the last
/// assignable member under a folder of it by sort order, so a comparison follows each new forecast without an edit.
/// </summary>
public sealed record ScenarioSelection
{
    /// <summary>The member key, or the folder's key when <see cref="Latest"/> is set.</summary>
    public required string Key { get; init; }

    /// <summary>Whether <see cref="Key"/> names a folder whose latest version the comparison reads.</summary>
    public required bool Latest { get; init; }
}
