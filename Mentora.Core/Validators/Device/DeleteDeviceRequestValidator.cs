using FluentValidation;
using Mentora.Core.DTOs.Device;

namespace Mentora.Core.Validators.Device;

public class DeleteDeviceRequestValidator : AbstractValidator<DeleteDeviceRequest>
{
    public DeleteDeviceRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty()
            .MaximumLength(512);
    }
}
