using Microsoft.EntityFrameworkCore;
using Mentora.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Mentora API", Version = "v1" });
});

builder.Services.AddDbContext<MentoraDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Auto-migrate at startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MentoraDbContext>();
    db.Database.Migrate();
}

app.UseSwagger();
app.UseSwaggerUI();

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

app.Run();