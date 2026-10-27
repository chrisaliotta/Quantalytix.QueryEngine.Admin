namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>The body of <c>POST &lt;kind&gt;-versions/{versionId}/freeze</c> for dimensions, mapping sets and bridges (OI-14).</summary>
public sealed record FreezeInput
{
    /// <summary>Why the version is frozen, such as <c>FY26 close</c>; stored as <c>freeze_note</c> (OI-15).</summary>
    public required string? Note { get; init; }
}
