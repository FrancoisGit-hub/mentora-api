using Mentora.API.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Mentora.API.Swagger;

/// <summary>
/// Lot 12.1 — replaces the former global AddSecurityRequirement. The "admin" document requires
/// the AdminBearer scheme; mobile, coach and internal require Bearer, exactly as before. Each
/// document keeps only the scheme it uses in its components, so the three existing documents
/// stay identical to their pre-12.1 content.
/// </summary>
public sealed class SecuritySchemePerDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var used = context.DocumentName == AdminAuthDefaults.SwaggerDoc
            ? AdminAuthDefaults.Scheme
            : JwtBearerDefaults.AuthenticationScheme;
        var unused = used == AdminAuthDefaults.Scheme
            ? JwtBearerDefaults.AuthenticationScheme
            : AdminAuthDefaults.Scheme;

        swaggerDoc.Components?.SecuritySchemes?.Remove(unused);

        var reference = new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = used }
        };
        swaggerDoc.SecurityRequirements = new List<OpenApiSecurityRequirement>
        {
            new() { { reference, new List<string>() } }
        };
    }
}
