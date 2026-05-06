using FluentValidation;
using Mentora.Core.DTOs.Cart;

namespace Mentora.Core.Validators.Cart;

public class UpdateCartItemQuantityRequestValidator : AbstractValidator<UpdateCartItemQuantityRequest>
{
    public UpdateCartItemQuantityRequestValidator()
    {
        RuleFor(x => x.Quantity)
            .InclusiveBetween(1, 99);
    }
}
