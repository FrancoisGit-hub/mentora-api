using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Mentora.API.Hubs;
using Mentora.API.Middleware;
using Mentora.API.Swagger;
using Mentora.Core.Validators.Catalog;
using Mentora.Infrastructure.Services.Stripe;
using Npgsql;
using Mentora.Core.Interfaces;
using Mentora.Core.Options;
using Mentora.Core.Settings;
using Mentora.Infrastructure.Persistence;
using Mentora.Infrastructure.Seeding;
using Mentora.Infrastructure.Services;
using Mentora.Infrastructure.Services.Email;
using Mentora.Infrastructure.Services.Visio;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("mobile", new OpenApiInfo
    {
        Title = "Mentora API — Mobile",
        Version = "v1",
        Description = "Endpoints consumed by the Mentora mobile application (members)."
    });
    c.SwaggerDoc("coach", new OpenApiInfo
    {
        Title = "Mentora API — Coach Back-Office",
        Version = "v1",
        Description = "Endpoints consumed by the coach back-office (currently the AI agent, later a web app)."
    });
    c.SwaggerDoc("internal", new OpenApiInfo
    {
        Title = "Mentora API — Internal",
        Version = "v1",
        Description = "Internal endpoints: health checks, webhooks, debug, monitoring. Not consumed by clients."
    });

    // Endpoints without an explicit GroupName default to "internal" (safe: least exposed)
    c.DocInclusionPredicate((docName, apiDesc) =>
    {
        var groupName = apiDesc.GroupName ?? "internal";
        return groupName == docName;
    });

    // Read [Tags(...)] from endpoint metadata; fall back to controller name
    c.TagActionsBy(api =>
    {
        var metadataTags = api.ActionDescriptor.EndpointMetadata
            .OfType<Microsoft.AspNetCore.Http.Metadata.ITagsMetadata>()
            .SelectMany(t => t.Tags)
            .Distinct()
            .ToList();
        if (metadataTags.Count > 0)
            return metadataTags;

        if (api.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor cad)
        {
            var attrTags = cad.ControllerTypeInfo
                .GetCustomAttributes(inherit: true)
                .OfType<Microsoft.AspNetCore.Http.Metadata.ITagsMetadata>()
                .SelectMany(t => t.Tags)
                .Distinct()
                .ToList();
            if (attrTags.Count > 0) return attrTags;
            return new[] { cad.ControllerName };
        }

        return new[] { "Other" };
    });

    var jwtScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT access token.",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = JwtBearerDefaults.AuthenticationScheme
        }
    };
    c.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, jwtScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { jwtScheme, [] }
    });

    var xmlFiles = new[]
    {
        $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml",
        "Mentora.Core.xml"
    };
    foreach (var xmlFile in xmlFiles)
    {
        var xmlPath = System.IO.Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (System.IO.File.Exists(xmlPath))
            c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }

    c.DescribeAllParametersInCamelCase();
    c.CustomSchemaIds(t => t.FullName);

    // Expose VoucherStatus query param as a constrained string enum in Swagger
    c.OperationFilter<VoucherStatusOperationFilter>();
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.EnableDynamicJson();
var dataSource = dataSourceBuilder.Build();
builder.Services.AddSingleton(dataSource);
builder.Services.AddDbContext<MentoraDbContext>(options =>
    options.UseNpgsql(dataSource));

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));

var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()!;
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
            ClockSkew = TimeSpan.Zero
        };

        // Lot 3.3 — SignalR: read JWT from query string for /hubs/chat WebSocket upgrade requests.
        // Browsers cannot set Authorization headers on WebSocket connections.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/chat"))
                    context.Token = accessToken;

                return Task.CompletedTask;
            }
        };
    });

// Authorization policies — see AuthService.GenerateAccessToken for claim definitions
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CoachOnly", policy =>
        policy.RequireClaim("userType", "COACH", "BOTH"));

    options.AddPolicy("MemberOnly", policy =>
        policy.RequireClaim("memberId"));
});

// Lot 1
builder.Services.AddScoped<IAuthService, AuthService>();

// Lot 2.1
builder.Services.AddScoped<ICoachParameterService, CoachParameterService>();
builder.Services.AddScoped<IOfferProgramService, OfferProgramService>();
builder.Services.AddScoped<ISessionSlotService, SessionSlotService>();

// Lot 2.2
builder.Services.AddValidatorsFromAssemblyContaining<ProductRequestValidator>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IProductPackService, ProductPackService>();

// Lot 2.3
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IStripeCheckoutService, StripeCheckoutStub>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IVoucherService, VoucherService>();
builder.Services.AddScoped<IStripeWebhookHandler, StripeWebhookHandler>();

// Lot 2.4
builder.Services.AddScoped<IVisioUrlGenerator, JitsiVisioUrlGenerator>();
builder.Services.AddScoped<ISessionService, SessionService>();

// Lot 2 fix — Member Me
builder.Services.AddScoped<IMemberMeService, MemberMeService>();

// Lot 2 fix — Member Orders (paginated list + detail with vouchers)
builder.Services.AddScoped<IMemberOrderService, MemberOrderService>();

// Lot 3.0 — Member Catalog
builder.Services.AddScoped<IMemberCatalogService, MemberCatalogService>();

// Lot 3.1 — Conversations
builder.Services.AddScoped<IConversationService, ConversationService>();

// Lot 3.3 — SignalR (enum serialization mirrors the REST JSON convention)
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
    });

// Lot 3.0 — Email (OTP delivery)
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
var emailProvider = builder.Configuration["Email:Provider"] ?? "Logging";
if (string.Equals(emailProvider, "Smtp", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
else
    builder.Services.AddScoped<IEmailSender, LoggingEmailSender>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Serialize all enum properties as UPPERCASE strings on the wire (e.g. "AVAILABLE").
        // SnakeCaseUpper converts PascalCase enum member names to SCREAMING_SNAKE_CASE,
        // which matches the project's EnumMappings wire convention.
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("MentoraCorsPolicy", policy =>
    {
        policy.WithOrigins(
            "http://localhost:4200",
            "http://localhost:8100",
            "http://localhost",
            "capacitor://localhost"
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// Auto-migrate and seed at startup
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MentoraDbContext>();
    await db.Database.MigrateAsync();

    await CoachParameterSeeder.SeedAsync(scope.ServiceProvider);
    await OfferProgramSeeder.SeedAsync(scope.ServiceProvider);
}

// Must run first so downstream middleware (and Request.Scheme in URL generation) see the
// original client scheme/IP forwarded by nginx, not the http:// of the container-internal hop.
app.UseForwardedHeaders();

// Global exception handler must be outermost so it wraps all middleware
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/mobile/swagger.json",   "Mobile API");
    c.SwaggerEndpoint("/swagger/coach/swagger.json",    "Coach Back-Office API");
    c.SwaggerEndpoint("/swagger/internal/swagger.json", "Internal API");
    c.RoutePrefix    = "swagger";
    c.DocumentTitle  = "Mentora API";
});

app.UseCors("MentoraCorsPolicy");
app.UseAuthentication();
app.UseAuthorization();

// Health check endpoint — lit la BDD
app.MapGet("/api/v1/health", async (MentoraDbContext db) =>
{
    var checks = await db.HealthChecks.ToListAsync();
    return Results.Ok(new
    {
        success = true,
        data = checks,
        error = (string?)null,
        statusCode = 200
    });
})
.WithName("GetHealth")
.WithGroupName("internal")
.WithTags("System — Health");

app.MapControllers();

// Lot 3.3 — SignalR Chat Hub
app.MapHub<ChatHub>("/hubs/chat");

app.Run();
