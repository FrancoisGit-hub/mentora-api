using FluentValidation;
using Mentora.Core.DTOs.Exercise;
using Mentora.Core.Enums;

namespace Mentora.Core.Validators.Exercise;

public class ExerciseRequestValidator : AbstractValidator<ExerciseRequest>
{
    public ExerciseRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(120);

        RuleFor(x => x.Description)
            .MaximumLength(2000)
            .When(x => x.Description is not null);

        RuleFor(x => x.Instructions)
            .MaximumLength(4000)
            .When(x => x.Instructions is not null);

        RuleFor(x => x.VideoUrl)
            .MaximumLength(500)
            .When(x => x.VideoUrl is not null);

        RuleFor(x => x.ImageUrl)
            .MaximumLength(500)
            .When(x => x.ImageUrl is not null);

        RuleFor(x => x.MuscleGroup)
            .NotEmpty()
            .Must(v => EnumMappings.MuscleGroupMapping.WireValues.Contains(v))
            .WithMessage($"MuscleGroup must be one of: {string.Join(", ", EnumMappings.MuscleGroupMapping.WireValues)}");

        RuleFor(x => x.Equipment)
            .NotEmpty()
            .Must(v => EnumMappings.EquipmentMapping.WireValues.Contains(v))
            .WithMessage($"Equipment must be one of: {string.Join(", ", EnumMappings.EquipmentMapping.WireValues)}");
    }
}
