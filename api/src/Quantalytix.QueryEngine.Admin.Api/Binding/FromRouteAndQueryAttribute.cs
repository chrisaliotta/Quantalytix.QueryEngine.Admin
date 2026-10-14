using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Quantalytix.QueryEngine.Admin.Api.Binding;

/// <summary>
/// Binds a complex action parameter from route values and the query string together, such as a lookup's kind (route) and
/// parent ID (query). Input records live in the Application project and carry no MVC attributes, so the source is set on
/// the parameter; OpenAPI lists properties named in the route as path parameters and the rest as query parameters.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class FromRouteAndQueryAttribute : Attribute, IBindingSourceMetadata
{
    /// <summary>The model-binding source, which reads route values and the query string.</summary>
    public BindingSource BindingSource { get; } = BindingSource.ModelBinding;
}
