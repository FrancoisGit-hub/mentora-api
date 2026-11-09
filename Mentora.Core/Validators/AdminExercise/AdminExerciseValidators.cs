using FluentValidation;
using Mentora.Core.DTOs.AdminExercise;
using Mentora.Core.Enums;

namespace Mentora.Core.Validators.AdminExercise;

/// <summary>
/// Lot 12.2b — shape rules of the admin exercise body. Existence and "active" rules need the
/// database and live in AdminExerciseService. Lengths are checked on the trimmed value.
/// </summary>
public class AdminExerciseRequestValidator : AbstractValidator<AdminExerciseRequest>
{
    public AdminExerciseRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotNull()
            .Must(v => v is null || v.Trim().Length >= 1)
            .WithMessage("'Name' must not be empty.")
            .Must(v => v is null || v.Trim().Length <= 120)
            .WithMessage("'Name' must be 120 characters or fewer.");

        RuleFor(x => x.NameEn).Must(v => TrimmedLength(v) <= 120).WithMessage("'NameEn' must be 120 characters or fewer.");
        RuleFor(x => x.Description).Must(v => TrimmedLength(v) <= 2000).WithMessage("'Description' must be 2000 characters or fewer.");
        RuleFor(x => x.Instructions).Must(v => TrimmedLength(v) <= 8000).WithMessage("'Instructions' must be 8000 characters or fewer.");
        RuleFor(x => x.Breathing).Must(v => TrimmedLength(v) <= 2000).WithMessage("'Breathing' must be 2000 characters or fewer.");
        RuleFor(x => x.Precautions).Must(v => TrimmedLength(v) <= 2000).WithMessage("'Precautions' must be 2000 characters or fewer.");
        RuleFor(x => x.CoachTip).Must(v => TrimmedLength(v) <= 2000).WithMessage("'CoachTip' must be 2000 characters or fewer.");
        RuleFor(x => x.SearchTags).Must(v => TrimmedLength(v) <= 500).WithMessage("'SearchTags' must be 500 characters or fewer.");

        RuleFor(x => x.CategoryId)
            .NotNull().WithMessage("'CategoryId' is required.")
            .Must(v => v is null || v != Guid.Empty).WithMessage("'CategoryId' must not be empty.");

        RuleFor(x => x.PrimaryEquipmentId)
            .NotNull().WithMessage("'PrimaryEquipmentId' is required.")
            .Must(v => v is null || v != Guid.Empty).WithMessage("'PrimaryEquipmentId' must not be empty.");

        RuleFor(x => x.TypeId).Must(NotEmptyGuid).WithMessage("'TypeId' must not be empty.");
        RuleFor(x => x.LateralityId).Must(NotEmptyGuid).WithMessage("'LateralityId' must not be empty.");
        RuleFor(x => x.MovementPatternId).Must(NotEmptyGuid).WithMessage("'MovementPatternId' must not be empty.");
        RuleFor(x => x.StartingPositionId).Must(NotEmptyGuid).WithMessage("'StartingPositionId' must not be empty.");
        RuleFor(x => x.GripTypeId).Must(NotEmptyGuid).WithMessage("'GripTypeId' must not be empty.");
        RuleFor(x => x.PrimaryMuscleGroupId).Must(NotEmptyGuid).WithMessage("'PrimaryMuscleGroupId' must not be empty.");

        RuleFor(x => x.SecondaryEquipmentId)
            .Must(NotEmptyGuid).WithMessage("'SecondaryEquipmentId' must not be empty.");
        RuleFor(x => x)
            .Must(x => x.SecondaryEquipmentId is null || x.SecondaryEquipmentId != x.PrimaryEquipmentId)
            .WithMessage("'SecondaryEquipmentId' must differ from the primary equipment.")
            .OverridePropertyName(nameof(AdminExerciseRequest.SecondaryEquipmentId));

        RuleFor(x => x.SecondaryMuscleGroupIds)
            .Must(v => v is null || v.Count <= 6).WithMessage("'SecondaryMuscleGroupIds' must hold 6 items or fewer.")
            .Must(v => v is null || v.Distinct().Count() == v.Count).WithMessage("'SecondaryMuscleGroupIds' must not contain duplicates.")
            .Must(v => v is null || v.All(g => g != Guid.Empty)).WithMessage("'SecondaryMuscleGroupIds' must not contain an empty id.");
        RuleFor(x => x)
            .Must(x => x.PrimaryMuscleGroupId is null || x.SecondaryMuscleGroupIds is null
                       || !x.SecondaryMuscleGroupIds.Contains(x.PrimaryMuscleGroupId.Value))
            .WithMessage("'SecondaryMuscleGroupIds' must not contain the primary muscle group.")
            .OverridePropertyName(nameof(AdminExerciseRequest.SecondaryMuscleGroupIds));

        RuleFor(x => x.ObjectiveIds)
            .Must(v => v is null || v.Count <= 20).WithMessage("'ObjectiveIds' must hold 20 items or fewer.")
            .Must(v => v is null || v.Distinct().Count() == v.Count).WithMessage("'ObjectiveIds' must not contain duplicates.")
            .Must(v => v is null || v.All(g => g != Guid.Empty)).WithMessage("'ObjectiveIds' must not contain an empty id.");

        RuleFor(x => x.Level)
            .Must(v => string.IsNullOrWhiteSpace(v)
                       || EnumMappings.ExerciseLevelMapping.WireValues.Contains(v.Trim().ToUpperInvariant()))
            .WithMessage($"'Level' must be one of: {string.Join(", ", EnumMappings.ExerciseLevelMapping.WireValues)}.");

        RuleFor(x => x.CommonMistakes)
            .Must(v => v is null || v.Count <= 20).WithMessage("'CommonMistakes' must hold 20 items or fewer.")
            .Must(v => v is null || v.All(m => m is not null && m.Trim().Length is >= 1 and <= 300))
            .WithMessage("Each item of 'CommonMistakes' must be 1 to 300 characters.");
    }

    private static int TrimmedLength(string? value) => value is null ? 0 : value.Trim().Length;

    private static bool NotEmptyGuid(Guid? value) => value is null || value != Guid.Empty;
}

/// <summary>Lot 12.2b — body of the activation route.</summary>
public class AdminExerciseActivationRequestValidator : AbstractValidator<AdminExerciseActivationRequest>
{
    public AdminExerciseActivationRequestValidator()
    {
        RuleFor(x => x.IsActive).NotNull().WithMessage("'IsActive' is required.");
    }
}
