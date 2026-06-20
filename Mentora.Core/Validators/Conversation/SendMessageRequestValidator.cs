using FluentValidation;
using Mentora.Core.DTOs.Conversation;

namespace Mentora.Core.Validators.Conversation;

public sealed class SendMessageRequestValidator : AbstractValidator<SendMessageRequestDto>
{
    public SendMessageRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotNull().WithMessage("Content is required.")
            .Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("Content cannot be empty or whitespace only.")
            .Must(c => c.Trim().Length <= 6500).WithMessage("Content cannot exceed 6500 characters.");
    }
}
