using Mentora.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mentora.Infrastructure.Persistence.Configurations;

/// <summary>
/// Lot 12.2a — the six closed reference lists of the exercise catalog share one shape:
/// &lt;P&gt;_ID uuid PK, &lt;P&gt;_CODE varchar(40), &lt;P&gt;_LABEL varchar(80), &lt;P&gt;_SORT_ORDER integer,
/// &lt;P&gt;_IS_ACTIVE boolean, created/updated timestamptz; unique UX_&lt;TABLE&gt;_CODE and UX_&lt;TABLE&gt;_LABEL.
/// CK_&lt;TABLE&gt;_CODE_FORMAT and CK_&lt;TABLE&gt;_SORT_ORDER are created by the migration (migrationBuilder.Sql).
/// IS_ACTIVE has no database default (C# initializer true) so that EF raises no sentinel warning.
/// </summary>
internal static class ExerciseReferenceMapping
{
    public static void Map<T>(EntityTypeBuilder<T> builder, string table, string columnPrefix, string propertyPrefix)
        where T : class
    {
        builder.ToTable(table);

        builder.HasKey(propertyPrefix + "Id");
        builder.Property<Guid>(propertyPrefix + "Id")
            .HasColumnName(columnPrefix + "_ID")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property<string>(propertyPrefix + "Code").HasColumnName(columnPrefix + "_CODE").IsRequired().HasMaxLength(40);
        builder.Property<string>(propertyPrefix + "Label").HasColumnName(columnPrefix + "_LABEL").IsRequired().HasMaxLength(80);
        builder.Property<int>(propertyPrefix + "SortOrder").HasColumnName(columnPrefix + "_SORT_ORDER").IsRequired();
        builder.Property<bool>(propertyPrefix + "IsActive").HasColumnName(columnPrefix + "_IS_ACTIVE").IsRequired();
        builder.Property<DateTime>(propertyPrefix + "CreatedDate").HasColumnName(columnPrefix + "_CREATED_DATE").IsRequired();
        builder.Property<DateTime>(propertyPrefix + "UpdatedDate").HasColumnName(columnPrefix + "_UPDATED_DATE").IsRequired();

        builder.HasIndex(propertyPrefix + "Code").IsUnique().HasDatabaseName($"UX_{table}_CODE");
        builder.HasIndex(propertyPrefix + "Label").IsUnique().HasDatabaseName($"UX_{table}_LABEL");
    }
}

public class ExerciseCategoryItemConfiguration : IEntityTypeConfiguration<ExerciseCategoryItem>
{
    public void Configure(EntityTypeBuilder<ExerciseCategoryItem> builder) =>
        ExerciseReferenceMapping.Map(builder, "EXERCISE_CATEGORIES", "EXERCISE_CATEGORY", "ExerciseCategory");
}

public class ExerciseTypeItemConfiguration : IEntityTypeConfiguration<ExerciseTypeItem>
{
    public void Configure(EntityTypeBuilder<ExerciseTypeItem> builder) =>
        ExerciseReferenceMapping.Map(builder, "EXERCISE_TYPES", "EXERCISE_TYPE", "ExerciseType");
}

public class ExerciseLateralityItemConfiguration : IEntityTypeConfiguration<ExerciseLateralityItem>
{
    public void Configure(EntityTypeBuilder<ExerciseLateralityItem> builder) =>
        ExerciseReferenceMapping.Map(builder, "EXERCISE_LATERALITIES", "EXERCISE_LATERALITY", "ExerciseLaterality");
}

public class MovementPatternItemConfiguration : IEntityTypeConfiguration<MovementPatternItem>
{
    public void Configure(EntityTypeBuilder<MovementPatternItem> builder) =>
        ExerciseReferenceMapping.Map(builder, "MOVEMENT_PATTERNS", "MOVEMENT_PATTERN", "MovementPattern");
}

public class StartingPositionItemConfiguration : IEntityTypeConfiguration<StartingPositionItem>
{
    public void Configure(EntityTypeBuilder<StartingPositionItem> builder) =>
        ExerciseReferenceMapping.Map(builder, "STARTING_POSITIONS", "STARTING_POSITION", "StartingPosition");
}

public class GripTypeItemConfiguration : IEntityTypeConfiguration<GripTypeItem>
{
    public void Configure(EntityTypeBuilder<GripTypeItem> builder) =>
        ExerciseReferenceMapping.Map(builder, "GRIP_TYPES", "GRIP_TYPE", "GripType");
}
