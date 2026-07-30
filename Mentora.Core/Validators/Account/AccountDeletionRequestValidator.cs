using FluentValidation;
using Mentora.Core.DTOs.Account;

namespace Mentora.Core.Validators.Account;

public class AccountDeletionRequestValidator : AbstractValidator<AccountDeletionRequestDto>
{
    public AccountDeletionRequestValidator()
    {
        RuleFor(x => x.Reason)
            .MaximumLength(1000)
            .When(x => x.Reason is not null);
    }
}
