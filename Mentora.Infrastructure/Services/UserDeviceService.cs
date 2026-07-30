using FluentValidation;
using Mentora.Core.DTOs.Device;
using Mentora.Core.Entities;
using Mentora.Core.Interfaces;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Mentora.Infrastructure.Services;

public class UserDeviceService(
    MentoraDbContext db,
    IValidator<RegisterDeviceRequest> registerValidator,
    IValidator<DeleteDeviceRequest> deleteValidator) : IUserDeviceService
{
    public async Task RegisterAsync(Guid userId, RegisterDeviceRequest request, CancellationToken ct)
    {
        await registerValidator.ValidateAndThrowAsync(request, ct);

        var now = DateTime.UtcNow;
        var existing = await db.UserDevices
            .FirstOrDefaultAsync(d => d.UserDeviceToken == request.Token, ct);

        if (existing is null)
        {
            db.UserDevices.Add(new UserDevice
            {
                UserDeviceToken        = request.Token,
                UserDevicePlatform     = request.Platform,
                UserDeviceCreatedDate  = now,
                UserDeviceLastSeenDate = now,
                UserId                 = userId
            });

            try
            {
                await db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == "23505")
            {
                // Race: another concurrent call registered this token first. Fall through to the
                // update/reassign path against the row the winner just created.
                db.ChangeTracker.Clear();
                existing = await db.UserDevices.FirstAsync(d => d.UserDeviceToken == request.Token, ct);
            }
        }

        if (existing.UserId == userId)
        {
            // Same owner, same device — just record that it's still alive.
            existing.UserDeviceLastSeenDate = now;
        }
        else
        {
            // Different owner: this physical device changed hands. Reassign rather than
            // duplicate, so the previous owner stops receiving push notifications for it.
            existing.UserId                 = userId;
            existing.UserDevicePlatform     = request.Platform;
            existing.UserDeviceCreatedDate  = now;
            existing.UserDeviceLastSeenDate = now;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid userId, string token, CancellationToken ct)
    {
        await deleteValidator.ValidateAndThrowAsync(new DeleteDeviceRequest(token), ct);

        await db.UserDevices
            .Where(d => d.UserDeviceToken == token && d.UserId == userId)
            .ExecuteDeleteAsync(ct);
    }
}
