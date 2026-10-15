using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Domain.Expressions;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Expressions;

/// <summary>
/// Confirms that the inspector reports what the query engine's parser reports: the distinct field keys, measure keys and
/// request parameter names of a valid expression in reading order, and the engine's own message and position for invalid
/// text. Formula dependencies and expression validation are derived from it, so the admin API must accept exactly the
/// expressions queries can run.
/// </summary>
public sealed class ExpressionInspectorTests
{
    /// <summary>A valid expression lists each referenced field, measure and parameter once, in the order the text first mentions it.</summary>
    [Fact]
    public void ValidExpression_ListsDistinctReferencesInReadingOrder()
    {
        var inspection = ExpressionInspector.Inspect("[f.principal] * [m.rate] + COALESCE([f.fee], @fee, [f.principal]) / [m.rate] - @fee");

        Assert.True(inspection.IsValid);
        Assert.Null(inspection.Error);
        Assert.Null(inspection.Position);
        Assert.Equal(["principal", "fee"], inspection.FieldKeys);
        Assert.Equal(["rate"], inspection.MeasureKeys);
        Assert.Equal(["fee"], inspection.ParameterNames);
    }

    /// <summary>Invalid text returns the engine's message and the zero-based position of the failing character.</summary>
    [Fact]
    public void InvalidText_ReturnsTheEngineMessageAndPosition()
    {
        const string text = "[f.balance] + * 2";
        var engineError = Assert.Throws<ExpressionSyntaxException>(() => ExpressionParser.Parse(text));

        var inspection = ExpressionInspector.Inspect(text);

        Assert.False(inspection.IsValid);
        Assert.Equal(engineError.Message, inspection.Error);
        Assert.Equal(text.IndexOf('*', StringComparison.Ordinal), inspection.Position);
        Assert.Empty(inspection.FieldKeys);
        Assert.Empty(inspection.MeasureKeys);
        Assert.Empty(inspection.ParameterNames);
    }
}
