namespace Quantalytix.QueryEngine.Admin.Application.ReportDesigns;

/// <summary>The dataset measure that supplies one model variable from a design source.</summary>
public sealed record DesignVariableInput
{
    /// <summary>The key of the model variable; the model's published version must have it at publication.</summary>
    public required string VariableKey { get; init; }

    /// <summary>The dataset measure that supplies the variable.</summary>
    public required string MeasureKey { get; init; }
}
