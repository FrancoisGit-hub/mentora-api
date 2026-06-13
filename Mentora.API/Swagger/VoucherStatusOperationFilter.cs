using Mentora.API.Controllers.Member;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Mentora.API.Swagger;

/// <summary>
/// Injects the allowed enum values (AVAILABLE, RESERVED, CONSUMED) onto the <c>status</c> query
/// parameter of <see cref="MemberVouchersController.GetAll"/> so the Swagger UI renders it as a
/// constrained string rather than a free-form input.
/// </summary>
public sealed class VoucherStatusOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(MemberVouchersController)
            || context.MethodInfo.Name != nameof(MemberVouchersController.GetAll))
            return;

        var param = operation.Parameters?.FirstOrDefault(p => p.Name == "status");
        if (param is null)
            return;

        param.Schema.Enum = new[] { "AVAILABLE", "RESERVED", "CONSUMED" }
            .Select(v => (IOpenApiAny)new OpenApiString(v))
            .ToList();
    }
}
