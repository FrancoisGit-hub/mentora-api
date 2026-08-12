using Mentora.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class ProgramBlockConfiguration : IEntityTypeConfiguration<ProgramBlock>
{
    public void Configure(EntityTypeBuilder<ProgramBlock> builder)
    {
        builder.ToTable("PROGRAM_BLOCKS");

        builder.HasKey(e => e.ProgramBlockId);
        builder.Property(e => e.ProgramBlockId)
            .HasColumnName("PROGRAM_BLOCK_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.ProgramBlockProgramId)
            .HasColumnName("PROGRAM_BLOCK_PROGRAM_ID")
            .IsRequired();

        // Self-reference — NULL only for the root MACROCYCLE block
        builder.Property(e => e.ProgramBlockParentId)
            .HasColumnName("PROGRAM_BLOCK_PARENT_ID");

        builder.Property(e => e.ProgramBlockLevel)
            .HasColumnName("PROGRAM_BLOCK_LEVEL")
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(e => e.ProgramBlockName)
            .HasColumnName("PROGRAM_BLOCK_NAME")
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(e => e.ProgramBlockPosition)
            .HasColumnName("PROGRAM_BLOCK_POSITION")
            .IsRequired();

        builder.Property(e => e.ProgramBlockWeekNumber)
            .HasColumnName("PROGRAM_BLOCK_WEEK_NUMBER");

        // CHECK (LEVEL='MACROCYCLE' implies PARENT_ID IS NULL) and
        // CHECK (WEEK_NUMBER IS NOT NULL iff LEVEL='MICROCYCLE') are created via raw SQL in the
        // migration — cross-column business rules kept explicit rather than via the fluent
        // HasCheckConstraint API. The MACROCYCLE rule is one-directional: a coach may skip levels,
        // so a non-MACROCYCLE root block (MESOCYCLE or MICROCYCLE with PARENT_ID IS NULL) is valid
        // and intentionally not rejected — enforced by ProgramTemplateBodyValidator's level-descent
        // rule, not by this constraint.

        builder.HasOne<Program>()
            .WithMany()
            .HasForeignKey(e => e.ProgramBlockProgramId)
            .OnDelete(DeleteBehavior.Cascade);

        // Self-reference — Restrict: EF cannot cascade a self-reference safely alongside the
        // PROGRAM_ID cascade above (two simultaneous cascade paths into the same rows). Delete
        // order is handled by that program-level cascade, which removes every block for the
        // program in one action regardless of parent/child order.
        builder.HasOne<ProgramBlock>()
            .WithMany()
            .HasForeignKey(e => e.ProgramBlockParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
