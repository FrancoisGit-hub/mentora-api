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

        // Lot 12.3a: nullable while the row is UPLOADING (the asset does not exist yet).
        builder.Property(e => e.ExerciseVideoMuxAssetId)
            .HasColumnName("EXERCISE_VIDEO_MUX_ASSET_ID")
            .HasMaxLength(100);

        builder.Property(e => e.ExerciseVideoMuxPlaybackId)
            .HasColumnName("EXERCISE_VIDEO_MUX_PLAYBACK_ID")
            .HasMaxLength(100);

        builder.Property(e => e.ExerciseVideoMuxUploadId)
            .HasColumnName("EXERCISE_VIDEO_MUX_UPLOAD_ID")
            .HasMaxLength(100);

        builder.Property(e => e.ExerciseVideoUploadExpiresDate)
            .HasColumnName("EXERCISE_VIDEO_UPLOAD_EXPIRES_DATE");

        builder.Property(e => e.ExerciseVideoError)
            .HasColumnName("EXERCISE_VIDEO_ERROR")
            .HasMaxLength(500);

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
            .HasDefaultValueSql("'WAITING'")
            // Lot 12.3a: Uploading is now the CLR default (0). Without a sentinel EF would omit the column
            // on insert for Uploading and the database default would store WAITING. -1 is never a real
            // value, so EF always sends the status.
            .HasSentinel((ExerciseVideoStatus)(-1));

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
        // lifts. CHECKs on policy and status are raw SQL in the Lot9_ExerciseCatalog migration;
        // Lot12_3a_MuxVideos re-creates the status CHECK (UPLOADING added) and adds
        // CK_EXERCISE_VIDEOS_MUX_REF (upload id or asset id set). NULL asset or upload ids do not
        // collide in the unique indexes (PostgreSQL NULLs are distinct).
        builder.HasIndex(e => e.ExerciseVideoExerciseId)
            .IsUnique()
            .HasDatabaseName("UX_EXERCISE_VIDEOS_EXERCISE");

        builder.HasIndex(e => e.ExerciseVideoMuxAssetId)
            .IsUnique()
            .HasDatabaseName("UX_EXERCISE_VIDEOS_MUX_ASSET");

        builder.HasIndex(e => e.ExerciseVideoMuxUploadId)
            .IsUnique()
            .HasDatabaseName("UX_EXERCISE_VIDEOS_MUX_UPLOAD");

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
        if (v == ExerciseVideoStatus.Uploading) return "UPLOADING";
        if (v == ExerciseVideoStatus.Waiting)   return "WAITING";
        if (v == ExerciseVideoStatus.Ready)     return "READY";
        if (v == ExerciseVideoStatus.Errored)   return "ERRORED";
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown ExerciseVideoStatus value.");
    }

    private static ExerciseVideoStatus ExerciseVideoStatusFromDb(string v)
    {
        if (v == "UPLOADING") return ExerciseVideoStatus.Uploading;
        if (v == "WAITING")   return ExerciseVideoStatus.Waiting;
        if (v == "READY")     return ExerciseVideoStatus.Ready;
        if (v == "ERRORED")   return ExerciseVideoStatus.Errored;
        throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown ExerciseVideoStatus DB value.");
    }
}
