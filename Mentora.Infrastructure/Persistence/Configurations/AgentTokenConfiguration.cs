using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mentora.Core.Entities;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class AgentTokenConfiguration : IEntityTypeConfiguration<AgentToken>
{
    public void Configure(EntityTypeBuilder<AgentToken> builder)
    {
        builder.ToTable("AGENT_TOKENS");

        builder.HasKey(e => e.AgentTokenId);
        builder.Property(e => e.AgentTokenId)
            .HasColumnName("AGENT_TOKEN_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.AgentTokenHash).HasColumnName("AGENT_TOKEN_HASH").IsRequired().HasMaxLength(255);
        builder.Property(e => e.AgentTokenCreatedDate).HasColumnName("AGENT_TOKEN_CREATED_DATE").IsRequired();
        builder.Property(e => e.AgentTokenExpirationDate).HasColumnName("AGENT_TOKEN_EXPIRATION_DATE").IsRequired();
        builder.Property(e => e.AgentTokenIsRevoked).HasColumnName("AGENT_TOKEN_IS_REVOKED").IsRequired();
        builder.Property(e => e.CoachId).HasColumnName("COACH_ID").IsRequired();
    }
}
