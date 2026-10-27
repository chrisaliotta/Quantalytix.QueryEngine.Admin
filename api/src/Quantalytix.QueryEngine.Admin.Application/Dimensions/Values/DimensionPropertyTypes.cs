namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>The types of a <see cref="DimensionPropertyField"/>.</summary>
public static class DimensionPropertyTypes
{
    /// <summary>Free text; with format <c>geojson</c>, a GeoJSON document as text.</summary>
    public const string Text = "text";

    /// <summary>A JSON number.</summary>
    public const string Number = "number";

    /// <summary>An ISO date, <c>yyyy-MM-dd</c>.</summary>
    public const string Date = "date";

    /// <summary>An absolute URL.</summary>
    public const string Url = "url";

    /// <summary>An absolute URL of an image.</summary>
    public const string Image = "image";

    /// <summary>One of the field's options.</summary>
    public const string Select = "select";

    /// <summary>A location: text or an object such as <c>{ "lat": 0, "lng": 0 }</c>.</summary>
    public const string Geo = "geo";

    /// <summary>A JSON boolean.</summary>
    public const string Boolean = "boolean";

    /// <summary>The format of a text field that holds GeoJSON.</summary>
    public const string GeoJsonFormat = "geojson";
}
