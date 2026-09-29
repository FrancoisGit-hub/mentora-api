using FluentValidation;
using Mentora.Core.DTOs.Voucher;

namespace Mentora.Core.Validators.Voucher;

public class VoucherGrantRequestValidator : AbstractValidator<VoucherGrantRequest>
{
    public VoucherGrantRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty();

        RuleFor(x => x.Quantity)
            .InclusiveBetween(1, 50);

        RuleFor(x => x.Note)
            .MaximumLength(500)
            .When(x => x.Note is not null);
    }
}
