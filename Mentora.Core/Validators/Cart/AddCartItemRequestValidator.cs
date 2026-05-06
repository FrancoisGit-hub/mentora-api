using FluentValidation;
using Mentora.Core.DTOs.Cart;

namespace Mentora.Core.Validators.Cart;

public class AddCartItemRequestValidator : AbstractValidator<AddCartItemRequest>
{
    public AddCartItemRequestValidator()
    {
        RuleFor(x => x.Quantity)
            .InclusiveBetween(1, 99);

        RuleFor(x => x)
            .Must(req => req.ProductId.HasValue ^ req.PackId.HasValue)
            .WithMessage("Exactly one of ProductId or PackId must be provided.");
    }
}
