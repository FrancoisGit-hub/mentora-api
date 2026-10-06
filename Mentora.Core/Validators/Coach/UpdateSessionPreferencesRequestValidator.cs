using FluentValidation;
using Mentora.Core.DTOs.Coach;
using Mentora.Core.Text;

namespace Mentora.Core.Validators.Coach;

public class UpdateSessionPreferencesRequestValidator : AbstractValidator<UpdateSessionPreferencesRequest>
{
    public const int MaxLocations   = 10;
    public const int MaxLabelLength = 100;

    public UpdateSessionPreferencesRequestValidator()
    {
        RuleFor(x => x.OffersVisio).NotNull();
        RuleFor(x => x.OffersInPersonSolo).NotNull();
        RuleFor(x => x.InPersonLocations).NotNull();

        When(x => x.InPersonLocations is not null, () =>
        {
            RuleFor(x => x.InPersonLocations!)
                .Must(l => l.Count <= MaxLocations)
                .WithMessage($"At most {MaxLocations} locations are allowed.");

            RuleForEach(x => x.InPersonLocations!)
                .Must(l => l is not null && l.Trim().Length is >= 1 and <= MaxLabelLength)
                .WithMessage($"Each location must be 1 to {MaxLabelLength} characters once trimmed.");

            RuleFor(x => x.InPersonLocations!)
                .Custom((list, ctx) =>
                {
                    var seen = new HashSet<string>();
                    for (var i = 0; i < list.Count; i++)
                    {
                        if (list[i] is null) continue;
                        var key = AccentFolding.Fold(list[i].Trim());
                        if (key.Length > 0 && !seen.Add(key))
                            ctx.AddFailure($"InPersonLocations[{i}]", "Duplicate location (case and accents ignored).");
                    }
                });

            RuleFor(x => x.InPersonLocations!)
                .Must(l => l.Count == 0)
                .When(x => x.OffersInPersonSolo == false)
                .WithMessage("Locations must be empty when in-person solo is not offered.");
        });
    }
}
