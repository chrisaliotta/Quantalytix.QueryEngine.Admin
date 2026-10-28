using System.Text.Json.Serialization;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// The contract's <c>ReportSettingsPatch</c>: the settings to change; an absent property keeps its value. The three settings that
/// can be cleared record whether the body named them, because an explicit null clears them and an absent one keeps them.
/// </summary>
public sealed record ReportSettingsPatch
{
    /// <summary>The default profile key; null for none.</summary>
    private readonly string? _defaultProfileKey;

    /// <summary>The periodicity; null for none.</summary>
    private readonly string? _periodicity;

    /// <summary>The default filter; null for none.</summary>
    private readonly FilterNode? _defaultFilter;

    /// <summary>The new display name.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DisplayName { get; init; }

    /// <summary>The new classification.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReportType { get; init; }

    /// <summary>The new <c>subtotal_position</c> value.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SubtotalPosition { get; init; }

    /// <summary>The new default profile key; null clears it.</summary>
    public string? DefaultProfileKey
    {
        get => _defaultProfileKey;
        init
        {
            _defaultProfileKey = value;
            SetsDefaultProfileKey = true;
        }
    }

    /// <summary>The new <c>periodicity</c> value; null clears it.</summary>
    public string? Periodicity
    {
        get => _periodicity;
        init
        {
            _periodicity = value;
            SetsPeriodicity = true;
        }
    }

    /// <summary>The new <c>report_layout</c> value.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Layout { get; init; }

    /// <summary>The new default breakout, calculation dimension keys.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? DefaultBreakout { get; init; }

    /// <summary>The new default filter over calculation dimension keys; null clears it.</summary>
    public FilterNode? DefaultFilter
    {
        get => _defaultFilter;
        init
        {
            _defaultFilter = value;
            SetsDefaultFilter = true;
        }
    }

    /// <summary>Whether the patch names the default profile key.</summary>
    [JsonIgnore]
    public bool SetsDefaultProfileKey { get; private init; }

    /// <summary>Whether the patch names the periodicity.</summary>
    [JsonIgnore]
    public bool SetsPeriodicity { get; private init; }

    /// <summary>Whether the patch names the default filter.</summary>
    [JsonIgnore]
    public bool SetsDefaultFilter { get; private init; }

    /// <summary>Whether the patch names no setting.</summary>
    [JsonIgnore]
    public bool IsEmpty =>
        DisplayName is null && ReportType is null && SubtotalPosition is null && Layout is null && DefaultBreakout is null
        && !SetsDefaultProfileKey && !SetsPeriodicity && !SetsDefaultFilter;
}
