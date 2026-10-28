using FluentValidation.TestHelper;
using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Datasets;

/// <summary>
/// Confirms the field input rules of schema section 7: each source kind requires its own source attributes and rejects the
/// others, expressions must parse and read only fields, decimals carry a precision of 1-38 and a scale up to it while other
/// types carry neither, and keys, column names and JSON paths must match their patterns. The column name and JSON path
/// patterns are what make both safe to inline into the materialize DDL, which binds no parameters.
/// </summary>
public sealed class FieldInputValidatorTests
{
    /// <summary>The validator under test.</summary>
    private readonly DatasetFieldInputValidator _validator = new();

    /// <summary>A valid field of each source kind passes.</summary>
    /// <param name="sourceKind">The field's source kind.</param>
    [Theory]
    [InlineData(FieldSourceKinds.Column)]
    [InlineData(FieldSourceKinds.JsonPath)]
    [InlineData(FieldSourceKinds.Expression)]
    public void ValidFieldOfEachKind_Passes(string sourceKind)
    {
        _validator.TestValidate(Field(sourceKind)).ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>A column field needs its column and reads no JSON path or expression.</summary>
    [Fact]
    public void ColumnField_RequiresSourceColumnAndForbidsPathAndExpression()
    {
        var input = Field(FieldSourceKinds.Column) with { SourceColumn = null, JsonPath = "$.branch", ExpressionText = "[f.branch]" };

        var result = _validator.TestValidate(input);

        result.ShouldHaveValidationErrorFor(field => field.SourceColumn);
        result.ShouldHaveValidationErrorFor(field => field.JsonPath);
        result.ShouldHaveValidationErrorFor(field => field.ExpressionText);
    }

    /// <summary>A JSON path field needs its JSON column and path, and reads no expression.</summary>
    [Fact]
    public void JsonPathField_RequiresSourceColumnAndPathAndForbidsExpression()
    {
        var input = Field(FieldSourceKinds.JsonPath) with { SourceColumn = null, JsonPath = null, ExpressionText = "[f.branch]" };

        var result = _validator.TestValidate(input);

        result.ShouldHaveValidationErrorFor(field => field.SourceColumn);
        result.ShouldHaveValidationErrorFor(field => field.JsonPath);
        result.ShouldHaveValidationErrorFor(field => field.ExpressionText);
    }

    /// <summary>An expression field reads no columns directly, and its expression must parse; the error carries the engine's message.</summary>
    [Fact]
    public void ExpressionField_RequiresAParsingExpressionAndForbidsColumns()
    {
        var input = Field(FieldSourceKinds.Expression) with { SourceColumn = "branch_code", JsonPath = "$.branch", ExpressionText = "[f.balance] +" };

        var result = _validator.TestValidate(input);

        result.ShouldHaveValidationErrorFor(field => field.ExpressionText).WithErrorMessage(ExpressionError("[f.balance] +"));
        result.ShouldHaveValidationErrorFor(field => field.SourceColumn);
        result.ShouldHaveValidationErrorFor(field => field.JsonPath);
    }

    /// <summary>A field expression is evaluated per row, so it cannot reference measures.</summary>
    [Fact]
    public void ExpressionField_CannotReferenceMeasures()
    {
        var input = Field(FieldSourceKinds.Expression) with { ExpressionText = "[f.balance] / [m.totalBalance]" };

        _validator.TestValidate(input).ShouldHaveValidationErrorFor(field => field.ExpressionText);
    }

    /// <summary>The query engine renders field expressions with no request parameters bound, so one cannot read <c>@name</c>.</summary>
    [Fact]
    public void ExpressionField_CannotReadRequestParameters()
    {
        var input = Field(FieldSourceKinds.Expression) with { ExpressionText = "[f.balance] * @rate" };

        _validator.TestValidate(input).ShouldHaveValidationErrorFor(field => field.ExpressionText);
    }

    /// <summary>
    /// A decimal needs a precision of 1-38 and a scale from 0 up to the precision. A missing precision is reported on the
    /// precision alone, not also as a scale that exceeds it.
    /// </summary>
    /// <param name="precision">The precision sent.</param>
    /// <param name="scale">The scale sent.</param>
    /// <param name="precisionFails">Whether the precision is rejected.</param>
    /// <param name="scaleFails">Whether the scale is rejected.</param>
    [Theory]
    [InlineData(20, 2, false, false)]
    [InlineData(38, 38, false, false)]
    [InlineData(1, 0, false, false)]
    [InlineData(null, null, true, true)]
    [InlineData(null, 2, true, false)]
    [InlineData(0, 0, true, false)]
    [InlineData(39, 2, true, false)]
    [InlineData(10, 11, false, true)]
    [InlineData(10, -1, false, true)]
    public void Decimal_RequiresPrecisionAndScaleInRange(int? precision, int? scale, bool precisionFails, bool scaleFails)
    {
        var input = Field(FieldSourceKinds.Column) with { DataType = DataTypes.Decimal, NumericPrecision = precision, NumericScale = scale };

        var result = _validator.TestValidate(input);

        Assert.Equal(precisionFails, HasError(result, nameof(DatasetFieldInput.NumericPrecision)));
        Assert.Equal(scaleFails, HasError(result, nameof(DatasetFieldInput.NumericScale)));
    }

    /// <summary>Only decimals have a precision and scale.</summary>
    [Fact]
    public void NonDecimal_ForbidsPrecisionAndScale()
    {
        var input = Field(FieldSourceKinds.Column) with { DataType = DataTypes.Int64, NumericPrecision = 18, NumericScale = 0 };

        var result = _validator.TestValidate(input);

        result.ShouldHaveValidationErrorFor(field => field.NumericPrecision);
        result.ShouldHaveValidationErrorFor(field => field.NumericScale);
    }

    /// <summary>Source and data kinds must be vocabulary values.</summary>
    [Fact]
    public void UnknownVocabularyValues_AreRejected()
    {
        var input = Field(FieldSourceKinds.Column) with { FieldSourceKind = "sql", DataType = "money" };

        var result = _validator.TestValidate(input);

        result.ShouldHaveValidationErrorFor(field => field.FieldSourceKind);
        result.ShouldHaveValidationErrorFor(field => field.DataType);
    }

    /// <summary>A field key starts with a letter and holds only letters, digits and underscores.</summary>
    /// <param name="fieldKey">The key sent.</param>
    /// <param name="valid">Whether it is accepted.</param>
    [Theory]
    [InlineData("branchCode", true)]
    [InlineData("loan_id2", true)]
    [InlineData("2ndBranch", false)]
    [InlineData("_branch", false)]
    [InlineData("branch-code", false)]
    [InlineData("branch\n", false)]
    public void FieldKey_MustMatchThePattern(string fieldKey, bool valid)
    {
        var result = _validator.TestValidate(Field(FieldSourceKinds.Column) with { Key = fieldKey });

        Assert.Equal(!valid, HasError(result, nameof(DatasetFieldInput.Key)));
    }

    /// <summary>A source column is an identifier, so it can be quoted into DDL.</summary>
    /// <param name="sourceColumn">The column sent.</param>
    /// <param name="valid">Whether it is accepted.</param>
    [Theory]
    [InlineData("branch_code", true)]
    [InlineData("_hidden", true)]
    [InlineData("1branch", false)]
    [InlineData("branch code", false)]
    [InlineData("branch`; DROP TABLE loan_daily", false)]
    public void SourceColumn_MustMatchThePattern(string sourceColumn, bool valid)
    {
        var result = _validator.TestValidate(Field(FieldSourceKinds.Column) with { SourceColumn = sourceColumn });

        Assert.Equal(!valid, HasError(result, nameof(DatasetFieldInput.SourceColumn)));
    }

    /// <summary>A JSON path is <c>$</c> followed by member names and array indexes, with no quote that could end a DDL string literal.</summary>
    /// <param name="jsonPath">The path sent.</param>
    /// <param name="valid">Whether it is accepted.</param>
    [Theory]
    [InlineData("$.branch", true)]
    [InlineData("$.address.lines[0]", true)]
    [InlineData("$[2].code", true)]
    [InlineData("$", false)]
    [InlineData("branch", false)]
    [InlineData("$.branch-code", false)]
    [InlineData("$.a') || ('", false)]
    [InlineData("$.branch\n", false)]
    public void JsonPath_MustMatchThePattern(string jsonPath, bool valid)
    {
        var result = _validator.TestValidate(Field(FieldSourceKinds.JsonPath) with { JsonPath = jsonPath });

        Assert.Equal(!valid, HasError(result, nameof(DatasetFieldInput.JsonPath)));
    }

    /// <summary>A valid field of a source kind: a string column, a string JSON path into <c>attributes</c>, or a decimal expression.</summary>
    private static DatasetFieldInput Field(string sourceKind)
    {
        var field = new DatasetFieldInput
        {
            Key = "branchCode",
            DisplayName = "Branch code",
            FieldSourceKind = sourceKind,
            SourceColumn = null,
            JsonPath = null,
            ExpressionText = null,
            DataType = DataTypes.String,
            NumericPrecision = null,
            NumericScale = null,
            UnitKey = null,
            IsNullable = true,
            IsSelectable = true,
            IsFilterable = true,
            IsSortable = true,
            IsGroupable = true,
            IsEnabled = true
        };
        return sourceKind switch
        {
            FieldSourceKinds.Column => field with { SourceColumn = "branch_code" },
            FieldSourceKinds.JsonPath => field with { SourceColumn = "attributes", JsonPath = "$.branch.code" },
            _ => field with { ExpressionText = "[f.balance] * [f.rate]", DataType = DataTypes.Decimal, NumericPrecision = 20, NumericScale = 4 }
        };
    }

    /// <summary>Whether the result has an error for a property, however the validator names properties.</summary>
    private static bool HasError(TestValidationResult<DatasetFieldInput> result, string propertyName)
    {
        return result.Errors.Any(error => string.Equals(error.PropertyName, propertyName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The query engine's parse error for an expression.</summary>
    private static string ExpressionError(string text)
    {
        return ExpressionInspector.Inspect(text).Error!;
    }
}
