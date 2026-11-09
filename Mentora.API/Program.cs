using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Mentora.API.Authorization;
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
using Mentora.Infrastructure.Services.Admin;
using Mentora.Infrastructure.Services.Email;
using Mentora.Infrastructure.Services.Quiz;
using Mentora.Infrastructure.Services.Visio;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
    // Lot 12.1 — admin back-office (Mentora associates), own JWT scheme AdminBearer
    c.SwaggerDoc(AdminAuthDefaults.SwaggerDoc, new OpenApiInfo
    {
        Title = "Mentora API — Admin",
        Version = "v1",
        Description = "Endpoints consumed by the Mentora admin back-office (associates only, AdminBearer tokens)."
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
    c.AddSecurityDefinition(AdminAuthDefaults.Scheme, new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your admin JWT access token.",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = AdminAuthDefaults.Scheme
        }
    });
    // Lot 12.1 — document-level requirement per doc (Bearer for mobile/coach/internal,
    // AdminBearer for admin); replaces the former global AddSecurityRequirement.
    c.DocumentFilter<SecuritySchemePerDocumentFilter>();

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
    c.OperationFilter<ExerciseListOperationFilter>();

    // Lot 9.1 — flag the deprecated api/v1/coach/programs alias (see OfferProgramsController)
    c.OperationFilter<DeprecatedCoachProgramsRouteOperationFilter>();
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
        // Pinned explicitly (Lot 7 / E1.1) ahead of the System.IdentityModel.Tokens.Jwt
        // upgrade so the inbound claim-type mapping behaviour is locked in and cannot
        // silently change as a side effect of the package bump. This preserves the
        // existing implicit default — "sub" resolves as ClaimTypes.NameIdentifier —
        // which AuthController/UserDevicesController/AccountController rely on.
        options.MapInboundClaims = true;

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
    })
    // Lot 12.1 — admin tokens: separate scheme, never the default. Key, issuer and audience come
    // from AdminAuthOptions (configured below, resolved after the startup validation).
    .AddJwtBearer(AdminAuthDefaults.Scheme, options =>
    {
        options.MapInboundClaims = false;
    });

builder.Services.AddOptions<AdminAuthOptions>()
    .Bind(builder.Configuration.GetSection(AdminAuthOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<AdminAuthOptions>, AdminAuthOptionsValidator>();
builder.Services.AddOptions<JwtBearerOptions>(AdminAuthDefaults.Scheme)
    .Configure<IOptions<AdminAuthOptions>>((options, adminOptions) =>
    {
        var admin = adminOptions.Value;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = admin.JwtIssuer,
            ValidAudience = admin.JwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(admin.JwtSecret)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.Zero
        };
    });

// Authorization policies — see AuthService.GenerateAccessToken for claim definitions
builder.Services.AddAuthorization(options =>
{
    // Lot 12.1 — admin routes: AdminBearer only (an app token is rejected with 401).
    options.AddPolicy(AdminAuthDefaults.Policy, policy =>
        policy.AddAuthenticationSchemes(AdminAuthDefaults.Scheme)
              .RequireAuthenticatedUser()
              .RequireClaim(AdminAuthDefaults.AdminIdClaim));

    options.AddPolicy("CoachOnly", policy =>
        policy.RequireClaim("userType", "COACH", "BOTH"));

    options.AddPolicy("MemberOnly", policy =>
        policy.RequireClaim("memberId")
              .RequireClaim("userType", "MEMBER"));

    // Lot 10.2 — anonymous device (no Authorization header) or coach; see QuizPlayerHandler.
    options.AddPolicy(QuizPlayerRequirement.PolicyName, policy =>
        policy.AddRequirements(new QuizPlayerRequirement()));
});

builder.Services.AddSingleton<IAuthorizationHandler, QuizPlayerHandler>();

// Lot 1
builder.Services.AddScoped<IAuthService, AuthService>();

// Lot 12.1 — admin authentication
builder.Services.AddScoped<IAdminAuthService, AdminAuthService>();

// Lot 2.1
builder.Services.AddScoped<ICoachParameterService, CoachParameterService>();
builder.Services.AddScoped<ICoachSessionPreferencesService, CoachSessionPreferencesService>();
builder.Services.AddScoped<ISessionTemplateService, SessionTemplateService>(); // Lot 11.5
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

// Lot 4 — Coach Me
builder.Services.AddScoped<ICoachMeService, CoachMeService>();

// Lot 2 fix — Member Orders (paginated list + detail with vouchers)
builder.Services.AddScoped<IMemberOrderService, MemberOrderService>();

// Lot 3.0 — Member Catalog
builder.Services.AddScoped<IMemberCatalogService, MemberCatalogService>();

// Lot 5 — Parameters (coach + member)
builder.Services.AddScoped<IMemberParameterService, MemberParameterService>();

// Lot 5 — Logout & device registration (coach + member)
builder.Services.AddScoped<IUserDeviceService, UserDeviceService>();

// Lot 5 — Coach-managed member settings + account deletion request
builder.Services.AddScoped<ICoachMemberSettingsService, CoachMemberSettingsService>();
builder.Services.AddScoped<IAccountService, AccountService>();

// Lot 3.1 — Conversations
builder.Services.AddScoped<IConversationService, ConversationService>();

// Lot 6.1 — Exercise catalogue
builder.Services.AddScoped<IExerciseService, ExerciseService>();

// Lot 6.2 — Program templates
builder.Services.AddScoped<IProgramTemplateService, ProgramTemplateService>();

// Lot 6.3 — Assigned programs
builder.Services.AddScoped<IProgramService, ProgramService>();

// Lot 6.5 — Merged agenda
builder.Services.AddScoped<IAgendaService, AgendaService>();

// Lot 9.1 — Coach voucher grants
builder.Services.AddScoped<IVoucherGrantService, VoucherGrantService>();

// Lot 8 — LiveKit visio-token issuance. ValidateOnStart fails loudly on a missing/malformed
// secret: this is new in the codebase and intentional — a missing secret must never silently
// degrade to "no check" for something as sensitive as room-join credentials.
builder.Services.AddOptions<LiveKitOptions>()
    .Bind(builder.Configuration.GetSection(LiveKitOptions.SectionName))
    .Validate(o => !string.IsNullOrWhiteSpace(o.Url) && (o.Url.StartsWith("wss://") || o.Url.StartsWith("ws://")),
        "LiveKit:Url must be set and start with wss:// or ws://.")
    .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey), "LiveKit:ApiKey must be set.")
    .Validate(o => o.ApiSecret.Length >= 32, "LiveKit:ApiSecret must be at least 32 characters.")
    .Validate(o => o.JoinBeforeMinutes >= 0, "LiveKit:JoinBeforeMinutes must not be negative.")
    .Validate(o => o.JoinAfterEndMinutes >= 0, "LiveKit:JoinAfterEndMinutes must not be negative.")
    .ValidateOnStart();
builder.Services.AddScoped<IVisioTokenService, VisioTokenService>();

// Lot 10.2 — Basic quiz. Options fail loudly at startup when out of range.
builder.Services.AddOptions<QuizOptions>()
    .Bind(builder.Configuration.GetSection(QuizOptions.SectionName))
    .Validate(o => o.QuestionCount is >= 1 and <= 50, "Quiz:QuestionCount must be between 1 and 50.")
    .Validate(o => o.SessionLifetimeMinutes > 0, "Quiz:SessionLifetimeMinutes must be greater than 0.")
    .Validate(o => o.AnonymousPermitLimit > 0, "Quiz:AnonymousPermitLimit must be greater than 0.")
    .Validate(o => o.AnonymousWindowMinutes > 0, "Quiz:AnonymousWindowMinutes must be greater than 0.")
    .ValidateOnStart();
builder.Services.AddScoped<IQuizBasicService, QuizBasicService>();
builder.Services.AddScoped<IQuizDeviceClaimService, QuizDeviceClaimService>();
builder.Services.AddRateLimiter(o =>
{
    o.AddQuizPolicy();
    o.AddAdminAuthPolicy(); // Lot 12.1 — shares the 429 envelope set by AddQuizPolicy
});

// Lot 3.3 — SignalR (enum serialization mirrors the REST JSON convention)
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
    });

// Lot 7 / E1.2 — daily purge of revoked/expired refresh tokens, count-only by default
builder.Services.Configure<RefreshTokenPurgeOptions>(
    builder.Configuration.GetSection(RefreshTokenPurgeOptions.SectionName));
builder.Services.AddHostedService<RefreshTokenPurgeService>();

// Lot 3.0 — Email (OTP delivery)
// Lot 11.4 — options fail loudly at startup (messages never print a value or an address).
builder.Services.AddOptions<EmailOptions>()
    .Bind(builder.Configuration.GetSection(EmailOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<EmailOptions>, EmailOptionsValidator>();
builder.Services.AddOptions<EmailRoutingOptions>()
    .Bind(builder.Configuration.GetSection(EmailRoutingOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<EmailRoutingOptions>, EmailRoutingOptionsValidator>();
// Every mail goes through RoutingEmailSender (11.Z): concrete providers are registered as themselves,
// IEmailSender always resolves to the decorator.
builder.Services.AddScoped<SmtpEmailSender>();
builder.Services.AddScoped<LoggingEmailSender>();
builder.Services.AddScoped<IEmailSender>(sp =>
{
    IEmailSender provider = sp.GetRequiredService<IOptions<EmailOptions>>().Value.IsSmtp
        ? sp.GetRequiredService<SmtpEmailSender>()
        : sp.GetRequiredService<LoggingEmailSender>();
    return new RoutingEmailSender(
        provider,
        sp.GetRequiredService<IOptions<EmailRoutingOptions>>().Value,
        sp.GetRequiredService<ILogger<RoutingEmailSender>>());
});

builder.Services.AddControllers(options =>
    {
        // Lot 6 fix (C2) — [ApiController]'s automatic ModelState validation used to run
        // before ownership/existence checks and FluentValidation, so an invalid body on a
        // resource owned by another coach came back as a native 400/422 instead of 404.
        // Suppressing it lets every action reach its service, where ownership is checked
        // first; FluentValidation (422) and NotFoundException (404) take over from here.
        options.Filters.Add<NullBodyGuardFilter>();
    })
    .ConfigureApiBehaviorOptions(o =>
    {
        o.SuppressModelStateInvalidFilter = true;
    })
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
            "https://localhost",
            "https://localhost:4200",
            "https://localhost:8100",
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

// Lot 11.4 — reading .Value runs the validators now, so an invalid email setup stops the API
// before migrations and seeders. Counts only: no address in the startup line.
{
    var emailOpts = app.Services.GetRequiredService<IOptions<EmailOptions>>().Value;
    var routingOpts = app.Services.GetRequiredService<IOptions<EmailRoutingOptions>>().Value;
    if (routingOpts.IsUnrecognizedMode)
        app.Logger.LogWarning(
            "[EMAIL] EmailRouting:Mode is set but is not \"Production\": TEST routing applies.");
    app.Logger.LogInformation(
        "[EMAIL] routing mode={Mode} provider={Provider} testRecipients={TestCount} productionBcc={BccCount}",
        routingOpts.ModeLabel, emailOpts.EffectiveProvider,
        routingOpts.TestRecipientList.Count, routingOpts.ProductionBccList.Count);
}

// Lot 12.1 — same for the admin authentication: .Value runs AdminAuthOptionsValidator, then the
// admin key and audience must differ from the app ones. Messages and log line carry no value.
{
    var adminOpts = app.Services.GetRequiredService<IOptions<AdminAuthOptions>>().Value;
    var appJwt = app.Services.GetRequiredService<IOptions<JwtSettings>>().Value;
    if (string.Equals(adminOpts.JwtSecret, appJwt.Secret, StringComparison.Ordinal))
        throw new InvalidOperationException("AdminAuth:JwtSecret must differ from Jwt:Secret.");
    if (string.Equals(adminOpts.JwtAudience, appJwt.Audience, StringComparison.Ordinal))
        throw new InvalidOperationException("AdminAuth:JwtAudience must differ from Jwt:Audience.");
    app.Logger.LogInformation(
        "[ADMIN] auth emails={Emails} accessMinutes={AccessMinutes} refreshHours={RefreshHours}",
        adminOpts.EmailList.Count, adminOpts.AccessTokenMinutes, adminOpts.RefreshTokenHours);
}

// Auto-migrate and seed at startup
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MentoraDbContext>();
    await db.Database.MigrateAsync();

    await CoachParameterSeeder.SeedAsync(scope.ServiceProvider);
    await OfferProgramSeeder.SeedAsync(scope.ServiceProvider);
    // Lot 12.2a order: reference lists, legacy codes, create-only catalog seeders, one-time normalization.
    await MuscleGroupSeeder.SeedAsync(scope.ServiceProvider);
    await ExerciseReferenceSeeder.SeedAsync(scope.ServiceProvider);
    await LegacyExerciseCodePatch.RunAsync(scope.ServiceProvider);
    await CatalogSeeder.SeedAsync(scope.ServiceProvider);
    await ExerciseSeeder.SeedAsync(scope.ServiceProvider);
    await ReferenceNormalizationPatch.RunAsync(scope.ServiceProvider);
    await ProgramTemplateSeeder.SeedAsync(scope.ServiceProvider);
    await QuizQuestionSeeder.SeedAsync(scope.ServiceProvider);
    await AdminUserSeeder.SeedAsync(scope.ServiceProvider); // Lot 12.1
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
    c.SwaggerEndpoint("/swagger/admin/swagger.json",    "Admin API");
    c.RoutePrefix    = "swagger";
    c.DocumentTitle  = "Mentora API";
});

app.UseCors("MentoraCorsPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

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
