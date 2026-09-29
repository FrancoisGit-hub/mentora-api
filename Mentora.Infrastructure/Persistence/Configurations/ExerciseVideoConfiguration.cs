using Mentora.Core.Entities;
using Mentora.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mentora.Infrastructure.Persistence.Configurations;

public class ExerciseVideoConfiguration : IEntityTypeConfiguration<ExerciseVideo>
{
    public void Configure(EntityTypeBuilder<ExerciseVideo> builder)
    {
        builder.ToTable("EXERCISE_VIDEOS");

        builder.HasKey(e => e.ExerciseVideoId);
        builder.Property(e => e.ExerciseVideoId)
            .HasColumnName("EXERCISE_VIDEO_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.ExerciseVideoExerciseId)
            .HasColumnName("EXERCISE_VIDEO_EXERCISE_ID")
            .IsRequired();

        builder.Property(e => e.ExerciseVideoMuxAssetId)
            .HasColumnName("EXERCISE_VIDEO_MUX_ASSET_ID")
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.ExerciseVideoMuxPlaybackId)
            .HasColumnName("EXERCISE_VIDEO_MUX_PLAYBACK_ID")
            .HasMaxLength(100);

        var policyConverter = new ValueConverter<VideoPlaybackPolicy, string>(
            v => VideoPlaybackPolicyToDb(v),
            v => VideoPlaybackPolicyFromDb(v));

        builder.Property(e => e.ExerciseVideoPlaybackPolicy)
            .HasColumnName("EXERCISE_VIDEO_PLAYBACK_POLICY")
            .IsRequired()
            .HasMaxLength(10)
            .HasConversion(policyConverter)
            .HasDefaultValueSql("'PUBLIC'");

        var statusConverter = new ValueConverter<ExerciseVideoStatus, string>(
            v => ExerciseVideoStatusToDb(v),
            v => ExerciseVideoStatusFromDb(v));

        builder.Property(e => e.ExerciseVideoStatus)
            .HasColumnName("EXERCISE_VIDEO_STATUS")
            .IsRequired()
            .HasMaxLength(10)
            .HasConversion(statusConverter)
            .HasDefaultValueSql("'WAITING'");

        builder.Property(e => e.ExerciseVideoDurationSeconds)
            .HasColumnName("EXERCISE_VIDEO_DURATION_SECONDS")
            .HasColumnType("numeric(8,2)");

        builder.Property(e => e.ExerciseVideoAspectRatio)
            .HasColumnName("EXERCISE_VIDEO_ASPECT_RATIO")
            .HasMaxLength(10);

        builder.Property(e => e.ExerciseVideoCreatedDate)
            .HasColumnName("EXERCISE_VIDEO_CREATED_DATE")
            .IsRequired();

        builder.Property(e => e.ExerciseVideoUpdatedDate)
            .HasColumnName("EXERCISE_VIDEO_UPDATED_DATE")
            .IsRequired();

        // One video per exercise for now: the uniqueness on the exercise id is what a later lot
        // lifts. CHECKs on policy and status are raw SQL in the Lot9_ExerciseCatalog migration.
        builder.HasIndex(e => e.ExerciseVideoExerciseId)
            .IsUnique()
            .HasDatabaseName("UX_EXERCISE_VIDEOS_EXERCISE");

        builder.HasIndex(e => e.ExerciseVideoMuxAssetId)
            .IsUnique()
            .HasDatabaseName("UX_EXERCISE_VIDEOS_MUX_ASSET");

        builder.HasOne(e => e.Exercise)
            .WithOne(x => x.Video)
            .HasForeignKey<ExerciseVideo>(e => e.ExerciseVideoExerciseId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static string VideoPlaybackPolicyToDb(VideoPlaybackPolicy v)
    {
        if (v == VideoPlaybackPolicy.Public) return "PUBLIC";
        if (v == VideoPlaybackPolicy.Signed) return "SIGNED";
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown VideoPlaybackPolicy value.");
    }

    private static VideoPlaybackPolicy VideoPlaybackPolicyFromDb(string v)
    {
        if (v == "PUBLIC") return VideoPlaybackPolicy.Public;
        if (v == "SIGNED") return VideoPlaybackPolicy.Signed;
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown VideoPlaybackPolicy DB value.");
    }

    private static string ExerciseVideoStatusToDb(ExerciseVideoStatus v)
    {
        if (v == ExerciseVideoStatus.Waiting) return "WAITING";
        if (v == ExerciseVideoStatus.Ready)   return "READY";
        if (v == ExerciseVideoStatus.Errored) return "ERRORED";
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown ExerciseVideoStatus value.");
    }

    private static ExerciseVideoStatus ExerciseVideoStatusFromDb(string v)
    {
        if (v == "WAITING") return ExerciseVideoStatus.Waiting;
        if (v == "READY")   return ExerciseVideoStatus.Ready;
        if (v == "ERRORED") return ExerciseVideoStatus.Errored;
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown ExerciseVideoStatus DB value.");
    }
}
