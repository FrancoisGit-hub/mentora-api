using Mentora.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mentora.Infrastructure.Persistence.Configurations;

// Keyless entity mapped to the V_SESSION_MEMBERS view (created via raw SQL in the migration).
public class SessionMemberRowConfiguration : IEntityTypeConfiguration<SessionMemberRow>
{
    public void Configure(EntityTypeBuilder<SessionMemberRow> builder)
    {
        builder.HasNoKey();
        builder.ToView("V_SESSION_MEMBERS");

        builder.Property(e => e.SessionId).HasColumnName("SESSION_ID");
        builder.Property(e => e.MemberId).HasColumnName("MEMBER_ID");
    }
}
