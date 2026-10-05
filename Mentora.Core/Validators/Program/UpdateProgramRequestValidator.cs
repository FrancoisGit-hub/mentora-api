using FluentValidation;
using Mentora.Core.DTOs.Program;
using Mentora.Core.DTOs.ProgramTemplate.Body;
using Mentora.Core.Enums;

namespace Mentora.Core.Validators.Program;

public class UpdateProgramRequestValidator : AbstractValidator<UpdateProgramRequest>
{
    public UpdateProgramRequestValidator(IValidator<ProgramTemplateBody> bodyValidator)
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(120);

        RuleFor(x => x.Goal)
            .NotEmpty()
            .Must(v => EnumMappings.ProgramGoalMapping.WireValues.Contains(v))
            .WithMessage($"Goal must be one of: {string.Join(", ", EnumMappings.ProgramGoalMapping.WireValues)}");

        RuleFor(x => x.DurationWeeks)
            .GreaterThan(0);

        RuleFor(x => x.Body)
            .NotNull();

        // DurationWeeks isn't a property of Body, so it's bridged via RootContextData — same
        // channel already used for CoachId (see ProgramTemplateBodyValidator).
        RuleFor(x => x).Custom((request, context) =>
        {
            context.RootContextData["DurationWeeks"] = request.DurationWeeks;
        });

        // The PUT tree carries node ids the template tree does not have: validate an id-free copy
        // with the shared tree validator, under the same "Body" path, so the 422 keys are the same
        // as before. Ids are validated separately, in ProgramService, against the existing tree.
        // A null Body maps to null, which SetValidator skips (NotNull above reports it).
        RuleFor(x => x.Body == null ? null : x.Body.ToTemplateBody())
            .SetValidator(bodyValidator!)
            .OverridePropertyName(nameof(UpdateProgramRequest.Body));
    }
}
