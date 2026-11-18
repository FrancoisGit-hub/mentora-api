using FluentValidation;
using Mentora.Core.Messaging;

namespace Mentora.Core.Validators.Conversation;

/// <summary>
/// Lot 13.4: what the voice upload validator checks before any file is written. The declared length
/// is only a first gate: the service also caps the bytes it actually copies.
/// </summary>
/// <param name="FileLength">Length of the uploaded file in bytes, null when no file was sent.</param>
public sealed record SendAudioMessageRequest(long? FileLength);

/// <summary>
/// Lot 13.4: validator of a voice upload. Errors are reported under the key "File" (the form field).
/// </summary>
public sealed class SendAudioMessageRequestValidator : AbstractValidator<SendAudioMessageRequest>
{
    /// <summary>Message when the file field is absent or empty.</summary>
    public const string FileRequiredMessage = "File is required.";

    /// <summary>Message when the file is above <see cref="VoiceMessageRules.MaxSizeBytes"/>.</summary>
    public const string FileTooLargeMessage = "Audio file is too large (maximum 5 MB).";

    /// <summary>Property name used for every error of this validator.</summary>
    public const string FilePropertyName = "File";

    public SendAudioMessageRequestValidator()
    {
        RuleFor(x => x.FileLength)
            .Cascade(CascadeMode.Stop)
            .Must(length => length is > 0).WithMessage(FileRequiredMessage)
            .Must(length => length <= VoiceMessageRules.MaxSizeBytes).WithMessage(FileTooLargeMessage)
            .OverridePropertyName(FilePropertyName);
    }
}
