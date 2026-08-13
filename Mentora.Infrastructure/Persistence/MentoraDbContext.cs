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
    public DbSet<MemberParameter> MemberParameters => Set<MemberParameter>();
    public DbSet<UserDevice> UserDevices => Set<UserDevice>();
    public DbSet<OfferProgram> OfferPrograms => Set<OfferProgram>();
    public DbSet<SessionSlot> SessionSlots => Set<SessionSlot>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductPack> ProductPacks => Set<ProductPack>();
    public DbSet<ProductPackItem> ProductPackItems => Set<ProductPackItem>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<SessionVoucher> SessionVouchers => Set<SessionVoucher>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<SessionParticipant> SessionParticipants => Set<SessionParticipant>();
    public DbSet<SessionMemberRow> SessionMembers => Set<SessionMemberRow>();
    public DbSet<StripeWebhookEvent> StripeWebhookEvents => Set<StripeWebhookEvent>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<ProgramTemplate> ProgramTemplates => Set<ProgramTemplate>();
    public DbSet<Program> Programs => Set<Program>();
    public DbSet<ProgramBlock> ProgramBlocks => Set<ProgramBlock>();
    public DbSet<ProgramSession> ProgramSessions => Set<ProgramSession>();
    public DbSet<ProgramCircuit> ProgramCircuits => Set<ProgramCircuit>();
    public DbSet<ProgramExercise> ProgramExercises => Set<ProgramExercise>();

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