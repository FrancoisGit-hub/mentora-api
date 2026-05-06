using FluentValidation;
using Mentora.Core.DTOs.Catalog;

namespace Mentora.Core.Validators.Catalog;

public class ProductPackRequestValidator : AbstractValidator<ProductPackRequest>
{
    public ProductPackRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(120);

        RuleFor(x => x.PriceEuros)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.DiscountPercent)
            .InclusiveBetween(0, 100);

        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("A pack must contain at least one item.");

        RuleForEach(x => x.Items)
            .SetValidator(new ProductPackItemRequestValidator());

        RuleFor(x => x.Items)
            .Must(items => items == null || items.Select(i => i.ProductId).Distinct().Count() == items.Count)
            .WithMessage("A pack cannot contain duplicate products.")
            .When(x => x.Items is { Count: > 0 });
    }
}
