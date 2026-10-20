namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>
/// A <c>qe_variable_source</c> row: one dataset measure a report version can fill a model variable from, with how its rows
/// classify into the report's dimension; dataset content is named by key in the dataset version a release pins.
/// </summary>
public sealed record VariableSourceRecord
{
    /// <summary>Primary key.</summary>
    public required long VariableSourceId { get; init; }

    /// <summary>The owning report version.</summary>
    public required long ReportVersionId { get; init; }

    /// <summary>The key of the model variable the source supplies.</summary>
    public required string VariableKey { get; init; }

    /// <summary>Key, unique among the version's sources.</summary>
    public required string SourceKey { get; init; }

    /// <summary>The dataset; the release decides its version.</summary>
    public required long DatasetId { get; init; }

    /// <summary>A <c>classification_kind</c> value.</summary>
    public required string ClassificationKind { get; init; }

    /// <summary>The dataset field a <c>direct</c> or <c>mapped</c> classification reads.</summary>
    public string? ClassificationFieldKey { get; init; }

    /// <summary>The role whose dataset binding a <c>bridge</c> classification starts from.</summary>
    public long? ClassificationRoleId { get; init; }

    /// <summary>The mapping set of a <c>mapped</c> classification, into the report's dimension.</summary>
    public long? MappingSetId { get; init; }

    /// <summary>The bridge of a <c>bridge</c> classification, into the report's dimension.</summary>
    public long? BridgeId { get; init; }

    /// <summary>The time field the source's periods are read on.</summary>
    public required string TimeFieldKey { get; init; }

    /// <summary>The dataset measure that supplies the variable.</summary>
    public required string MeasureKey { get; init; }

    /// <summary>Optional structured filter over the dataset's fields.</summary>
    public string? SourceFilterJson { get; init; }

    /// <summary>Whether profiles may pick the source.</summary>
    public required bool IsEnabled { get; init; }
}
