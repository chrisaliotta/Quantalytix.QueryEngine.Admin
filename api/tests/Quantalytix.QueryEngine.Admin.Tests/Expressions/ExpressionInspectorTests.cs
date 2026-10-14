using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Domain.Expressions;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Expressions;

/// <summary>
/// Confirms that the inspector reports what the query engine's parser reports: the distinct field and measure keys of a
/// valid expression in reading order, and the engine's own message and position for invalid text. Formula dependencies and
/// expression validation are derived from it, so the admin API must accept exactly the expressions queries can run.
/// </summary>
public sealed class ExpressionInspectorTests
{
    /// <summary>A valid formula lists each referenced field and measure key once, in the order the text first mentions it.</summary>
    [Fact]
    public void ValidFormula_ListsDistinctKeysInReadingOrder()
    {
        var inspection = ExpressionInspector.Inspect("[f.principal] * [m.rate] + COALESCE([f.fee], [f.principal]) / [m.rate]");

        Assert.True(inspection.IsValid);
        Assert.Null(inspection.Error);
        Assert.Null(inspection.Position);
        Assert.Equal(["principal", "fee"], inspection.FieldKeys);
        Assert.Equal(["rate"], inspection.MeasureKeys);
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
    }
}
