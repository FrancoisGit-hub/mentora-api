using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Mentora.API.Swagger;

/// <summary>
/// Marks operations served under the deprecated <c>api/v1/coach/programs</c> alias
/// (see <see cref="Mentora.API.Controllers.Coach.OfferProgramsController"/>, Lot 9.1 route rename)
/// as <c>deprecated: true</c> in Swagger. Matches the exact route segment only — never
/// <c>api/v1/coach/product-programs</c> (the new home) nor <c>api/v1/coach/…/training-programs</c>
/// (a different controller entirely).
/// </summary>
public sealed class DeprecatedCoachProgramsRouteOperationFilter : IOperationFilter
{
    private const string DeprecatedPrefix = "api/v1/coach/programs";

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var path = context.ApiDescription.RelativePath;
        if (string.IsNullOrEmpty(path))
            return;

        if (path == DeprecatedPrefix || path.StartsWith(DeprecatedPrefix + "/", StringComparison.Ordinal))
            operation.Deprecated = true;
    }
}
