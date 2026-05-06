using FluentValidation;
using Mentora.Core.DTOs.Session;

namespace Mentora.Core.Validators.Session;

public class ReserveSessionRequestValidator : AbstractValidator<ReserveSessionRequest>
{
    public ReserveSessionRequestValidator()
    {
        RuleFor(x => x.VoucherId).NotEqual(Guid.Empty);
        RuleFor(x => x.SlotId).NotEqual(Guid.Empty);
    }
}
