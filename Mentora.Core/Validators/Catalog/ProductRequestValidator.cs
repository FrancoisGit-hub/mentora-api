using FluentValidation;
using Mentora.Core.DTOs.Catalog;
using Mentora.Core.Enums;

namespace Mentora.Core.Validators.Catalog;

public class ProductRequestValidator : AbstractValidator<ProductRequest>
{
    public ProductRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(120);

        RuleFor(x => x.OfferType)
            .NotEmpty()
            .Must(v => EnumMappings.OfferTypeMapping.WireValues.Contains(v))
            .WithMessage($"OfferType must be one of: {string.Join(", ", EnumMappings.OfferTypeMapping.WireValues)}")
            .Must(v => v != "VISIO_GROUPE")
            .WithMessage("VISIO_GROUPE is not available in V1.");

        RuleFor(x => x.MaxParticipants)
            .GreaterThan(0)
            .When(x => x.MaxParticipants.HasValue);

        RuleFor(x => x.OfferNature)
            .MaximumLength(80)
            .When(x => x.OfferNature is not null);

        RuleFor(x => x.DurationMinutes)
            .GreaterThan(0);

        RuleFor(x => x.PriceEuros)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.Sport)
            .NotEmpty()
            .Must(v => EnumMappings.SportMapping.WireValues.Contains(v))
            .WithMessage($"Sport must be one of: {string.Join(", ", EnumMappings.SportMapping.WireValues)}");

        RuleFor(x => x.Location)
            .MaximumLength(200)
            .When(x => x.Location is not null);

        RuleFor(x => x.OfferProgramId)
            .NotEmpty();
    }
}
