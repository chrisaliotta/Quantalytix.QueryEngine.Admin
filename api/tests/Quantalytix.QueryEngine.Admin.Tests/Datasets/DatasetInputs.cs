using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Tests.Datasets;

/// <summary>Builds the dataset change-set operations the tests send, with the defaults every test shares.</summary>
internal static class DatasetInputs
{
    /// <summary>A change set of operations.</summary>
    public static ChangeSetRequest<DatasetOperation> Request(params DatasetOperation[] operations)
    {
        return new ChangeSetRequest<DatasetOperation> { Operations = operations };
    }

    /// <summary><c>saveField</c> of a string column field.</summary>
    public static DatasetSaveFieldOperation SaveColumnField(string? id, string key, string column, string dataType = DataTypes.String)
    {
        return new DatasetSaveFieldOperation { Input = Field(id, key) with { SourceColumn = column, DataType = dataType } };
    }

    /// <summary><c>saveField</c> of a JSON path field over <c>attributes</c>.</summary>
    public static DatasetSaveFieldOperation SaveJsonField(string? id, string key, string jsonPath, bool materialize = false)
    {
        return new DatasetSaveFieldOperation
        {
            Input = Field(id, key) with { FieldSourceKind = FieldSourceKinds.JsonPath, SourceColumn = "attributes", JsonPath = jsonPath, Materialize = materialize ? true : null }
        };
    }

    /// <summary><c>saveField</c> of a decimal expression field.</summary>
    public static DatasetSaveFieldOperation SaveExpressionField(string? id, string key, string expressionText)
    {
        return new DatasetSaveFieldOperation
        {
            Input = Field(id, key) with { FieldSourceKind = FieldSourceKinds.Expression, ExpressionText = expressionText, DataType = DataTypes.Decimal, NumericPrecision = 20, NumericScale = 2 }
        };
    }

    /// <summary><c>saveGrain</c> of a row grain over fields.</summary>
    public static DatasetSaveGrainOperation SaveGrain(string? id, string key, params string[] fieldIds)
    {
        return new DatasetSaveGrainOperation { Input = new DatasetGrainInput { Id = id, Key = key, GrainKind = GrainKinds.Row, FieldIds = fieldIds } };
    }

    /// <summary>A field input with the schema's default flags; callers set its source.</summary>
    private static DatasetFieldInput Field(string? id, string key)
    {
        return new DatasetFieldInput
        {
            Id = id,
            Key = key,
            DisplayName = key,
            FieldSourceKind = FieldSourceKinds.Column,
            SourceColumn = null,
            JsonPath = null,
            ExpressionText = null,
            DataType = DataTypes.String,
            NumericPrecision = null,
            NumericScale = null,
            IsNullable = true,
            UnitKey = null,
            IsSelectable = true,
            IsFilterable = true,
            IsSortable = true,
            IsGroupable = true,
            IsEnabled = true
        };
    }
}
