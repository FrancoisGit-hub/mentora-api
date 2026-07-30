using FluentValidation;
using Mentora.Core.DTOs.Device;

namespace Mentora.Core.Validators.Device;

public class RegisterDeviceRequestValidator : AbstractValidator<RegisterDeviceRequest>
{
    public RegisterDeviceRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty()
            .MaximumLength(512);
    }
}
