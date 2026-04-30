using Microsoft.EntityFrameworkCore;
using Mentora.Core.Entities;

namespace Mentora.Infrastructure.Persistence;

public class MentoraDbContext(DbContextOptions<MentoraDbContext> options) : DbContext(options)
{
    public DbSet<HealthCheck> HealthChecks => Set<HealthCheck>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Coach> Coaches => Set<Coach>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<MemberCoach> MemberCoaches => Set<MemberCoach>();
    public DbSet<AuthOtp> AuthOtps => Set<AuthOtp>();
    public DbSet<AuthRefreshToken> AuthRefreshTokens => Set<AuthRefreshToken>();
    public DbSet<AgentToken> AgentTokens => Set<AgentToken>();
    public DbSet<CoachParameter> CoachParameters => Set<CoachParameter>();
    public DbSet<OfferProgram> OfferPrograms => Set<OfferProgram>();
    public DbSet<SessionSlot> SessionSlots => Set<SessionSlot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HealthCheck>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Message).IsRequired().HasMaxLength(500);

            // Seed data — prouve que l'API lit vraiment la BDD
            entity.HasData(new HealthCheck
            {
                Id = 1,
                Message = "Mentora API is live and connected to PostgreSQL!",
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        });

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MentoraDbContext).Assembly);
    }
}