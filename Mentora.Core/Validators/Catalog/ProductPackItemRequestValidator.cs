using FluentValidation;
using Mentora.Core.DTOs.Catalog;

namespace Mentora.Core.Validators.Catalog;

public class ProductPackItemRequestValidator : AbstractValidator<ProductPackItemRequest>
{
    public ProductPackItemRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty();

        RuleFor(x => x.Quantity)
            .GreaterThanOrEqualTo(1);
    }
}
