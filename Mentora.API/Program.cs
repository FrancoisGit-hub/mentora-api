using System.Text;
using FluentValidation;
using Mentora.API.Middleware;
using Mentora.Core.Validators.Catalog;
using Npgsql;
using Mentora.Core.Interfaces;
using Mentora.Core.Settings;
using Mentora.Infrastructure.Persistence;
using Mentora.Infrastructure.Seeding;
using Mentora.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Mentora API",
        Version = "v1",
        Description = "Mentora coaching platform API"
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

builder.Services.AddControllers();

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

var app = builder.Build();

// Auto-migrate and seed at startup
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MentoraDbContext>();
    await db.Database.MigrateAsync();

    await CoachParameterSeeder.SeedAsync(scope.ServiceProvider);
    await OfferProgramSeeder.SeedAsync(scope.ServiceProvider);
}

// Global exception handler must be outermost so it wraps all middleware
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

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
.WithName("GetHealth");

app.MapControllers();

app.Run();
