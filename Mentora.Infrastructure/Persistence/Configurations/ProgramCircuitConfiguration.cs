using Mentora.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class ProgramCircuitConfiguration : IEntityTypeConfiguration<ProgramCircuit>
{
    public void Configure(EntityTypeBuilder<ProgramCircuit> builder)
    {
        builder.ToTable("PROGRAM_CIRCUITS");

        builder.HasKey(e => e.ProgramCircuitId);
        builder.Property(e => e.ProgramCircuitId)
            .HasColumnName("PROGRAM_CIRCUIT_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        // Denormalized on purpose — see WHY PROGRAM_ID IS DENORMALIZED (ProgramConfiguration.cs)
        builder.Property(e => e.ProgramCircuitProgramId)
            .HasColumnName("PROGRAM_CIRCUIT_PROGRAM_ID")
            .IsRequired();

        builder.Property(e => e.ProgramCircuitProgramSessionId)
            .HasColumnName("PROGRAM_CIRCUIT_PROGRAM_SESSION_ID")
            .IsRequired();

        builder.Property(e => e.ProgramCircuitName)
            .HasColumnName("PROGRAM_CIRCUIT_NAME")
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(e => e.ProgramCircuitPosition)
            .HasColumnName("PROGRAM_CIRCUIT_POSITION")
            .IsRequired();

        builder.Property(e => e.ProgramCircuitMode)
            .HasColumnName("PROGRAM_CIRCUIT_MODE")
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(e => e.ProgramCircuitRounds)
            .HasColumnName("PROGRAM_CIRCUIT_ROUNDS");

        builder.Property(e => e.ProgramCircuitRestBetweenRoundsSeconds)
            .HasColumnName("PROGRAM_CIRCUIT_REST_BETWEEN_ROUNDS_SECONDS");

        builder.Property(e => e.ProgramCircuitNote)
            .HasColumnName("PROGRAM_CIRCUIT_NOTE")
            .HasColumnType("text");

        builder.HasOne<Program>()
            .WithMany()
            .HasForeignKey(e => e.ProgramCircuitProgramId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ProgramSession>()
            .WithMany()
            .HasForeignKey(e => e.ProgramCircuitProgramSessionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
