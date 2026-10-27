namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>report_mapping_method</c> vocabulary: how a report source's rows reach the report's rows.</summary>
public static class ReportMappingMethods
{
    /// <summary>A field of the dataset holds row keys of the report.</summary>
    public const string Direct = "direct";

    /// <summary>The source's own bridge maps the members of a role the dataset binds to rows of the report.</summary>
    public const string Bridge = "bridge";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "report_mapping_method",
        Key = "mappingMethod",
        Description = "How a report source's rows reach the report's rows.",
        Values =
        [
            new() { Value = Direct, Label = "Direct", Description = "A field of the dataset holds row keys of the report." },
            new() { Value = Bridge, Label = "Bridge", Description = "The source's own bridge maps the members of a role the dataset binds to rows of the report." }
        ]
    };
}
