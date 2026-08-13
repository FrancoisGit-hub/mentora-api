using FluentValidation;
using Mentora.Core.DTOs.Agenda;

namespace Mentora.Core.Validators.Agenda;

public class MemberAgendaRequestValidator : AbstractValidator<MemberAgendaRequest>
{
    public MemberAgendaRequestValidator()
    {
        RuleFor(x => x.From).NotNull();
        RuleFor(x => x.To).NotNull();

        When(x => x.From is not null && x.To is not null, () =>
        {
            RuleFor(x => x.To)
                .Must((request, to) => to!.Value >= request.From!.Value)
                .WithMessage("to must be on or after from.")
                .WithName(nameof(MemberAgendaRequest.To));

            RuleFor(x => x.To)
                .Must((request, to) => to!.Value.DayNumber - request.From!.Value.DayNumber <= 186)
                .WithMessage("Date range cannot exceed 186 days.")
                .WithName(nameof(MemberAgendaRequest.To));
        });
    }
}
