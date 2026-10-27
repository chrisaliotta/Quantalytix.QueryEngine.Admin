using System.Collections.Frozen;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>The column field a physical column gets when fields are made from columns: keyed and named after it, typed as it maps.</summary>
internal static class ColumnFields
{
    /// <summary>The data types whose fields group on their own by default.</summary>
    private static readonly FrozenSet<string> GroupableDataTypes = FrozenSet.Create(
        StringComparer.Ordinal,
        DataTypes.String,
        DataTypes.Boolean,
        DataTypes.Int32,
        DataTypes.Int64,
        DataTypes.Date);

    /// <summary>A column field over a column.</summary>
    /// <param name="fieldId">The field's id.</param>
    /// <param name="versionId">The field's version.</param>
    /// <param name="key">The field key, unique in the version.</param>
    /// <param name="columnName">The column.</param>
    /// <param name="type">The field type the column maps to.</param>
    /// <param name="isNullable">Whether the column accepts null.</param>
    /// <returns>The field.</returns>
    public static FieldRecord Of(long fieldId, long versionId, string key, string columnName, FieldType type, bool isNullable)
    {
        return new FieldRecord
        {
            FieldId = fieldId,
            DatasetVersionId = versionId,
            FieldKey = key,
            DisplayName = ColumnNames.ToDisplayName(columnName),
            FieldSourceKind = FieldSourceKinds.Column,
            SourceColumn = columnName,
            DataType = type.DataType,
            NumericPrecision = type.NumericPrecision,
            NumericScale = type.NumericScale,
            IsNullable = isNullable,
            IsSelectable = true,
            IsFilterable = true,
            IsSortable = type.DataType != DataTypes.Json,
            IsGroupable = GroupableDataTypes.Contains(type.DataType),
            IsEnabled = true
        };
    }

    /// <summary>A field key for a column that no key already taken has, suffixed with <c>_</c> until it is free.</summary>
    /// <param name="columnName">The column.</param>
    /// <param name="taken">The keys taken so far; the new key is added.</param>
    /// <returns>The key.</returns>
    public static string KeyOf(string columnName, ISet<string> taken)
    {
        var key = ColumnNames.ToFieldKey(columnName);
        while (!taken.Add(key)) key += "_";
        return key;
    }
}
