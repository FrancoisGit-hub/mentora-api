using FluentValidation;
using Mentora.Core.DTOs.Account;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mentora.Infrastructure.Services;

public class AccountService(
    MentoraDbContext db,
    IValidator<AccountDeletionRequestDto> validator) : IAccountService
{
    public async Task RequestDeletionAsync(Guid userId, AccountDeletionRequestDto request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);

        var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct)
            ?? throw new NotFoundException($"User {userId} not found.");

        // Keep the original requested date if one is already pending — only the reason refreshes.
        user.UserDeletionRequestedDate ??= DateTime.UtcNow;
        user.UserDeletionReason = request.Reason?.Trim();

        await db.SaveChangesAsync(ct);
    }

    public async Task CancelDeletionRequestAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct)
            ?? throw new NotFoundException($"User {userId} not found.");

        user.UserDeletionRequestedDate = null;
        user.UserDeletionReason        = null;

        await db.SaveChangesAsync(ct);
    }
}
