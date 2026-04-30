using Mentora.Core.DTOs.Lot2;
using Mentora.Core.Entities;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mentora.Infrastructure.Services;

public class OfferProgramService(MentoraDbContext db) : IOfferProgramService
{
    public async Task<List<OfferProgramDto>> GetAllAsync(Guid coachId)
    {
        return await db.OfferPrograms
            .Where(p => p.CoachId == coachId && p.OfferProgramIsActive)
            .Select(p => ToDto(p))
            .ToListAsync();
    }

    public async Task<OfferProgramDto> CreateAsync(Guid coachId, CreateOfferProgramRequest request)
    {
        var name = (request.Name ?? string.Empty).Trim();

        if (string.IsNullOrEmpty(name))
            throw new InvalidOperationException("Program name cannot be empty.");

        if (name.Length > 80)
            throw new InvalidOperationException("Program name cannot exceed 80 characters.");

        var duplicate = await db.OfferPrograms.AnyAsync(p =>
            p.CoachId == coachId &&
            p.OfferProgramName == name &&
            p.OfferProgramIsActive);

        if (duplicate)
            throw new ConflictException($"An active program named '{name}' already exists for this coach.");

        var program = new OfferProgram
        {
            OfferProgramName        = name,
            OfferProgramIsActive    = true,
            OfferProgramCreatedDate = DateTime.UtcNow,
            CoachId                 = coachId
        };

        db.OfferPrograms.Add(program);
        await db.SaveChangesAsync();

        return ToDto(program);
    }

    public async Task<OfferProgramDto> UpdateAsync(Guid coachId, Guid programId, UpdateOfferProgramRequest request)
    {
        var name = (request.Name ?? string.Empty).Trim();

        if (string.IsNullOrEmpty(name))
            throw new InvalidOperationException("Program name cannot be empty.");

        if (name.Length > 80)
            throw new InvalidOperationException("Program name cannot exceed 80 characters.");

        var program = await db.OfferPrograms
            .FirstOrDefaultAsync(p => p.OfferProgramId == programId && p.CoachId == coachId)
            ?? throw new NotFoundException("Program not found.");

        // Guard: if the new name differs and an active program with that name already exists
        if (program.OfferProgramName != name || program.OfferProgramIsActive != request.IsActive)
        {
            var wouldConflict = request.IsActive && await db.OfferPrograms.AnyAsync(p =>
                p.CoachId == coachId &&
                p.OfferProgramId != programId &&
                p.OfferProgramName == name &&
                p.OfferProgramIsActive);

            if (wouldConflict)
                throw new ConflictException($"An active program named '{name}' already exists for this coach.");
        }

        program.OfferProgramName     = name;
        program.OfferProgramIsActive = request.IsActive;

        await db.SaveChangesAsync();

        return ToDto(program);
    }

    public async Task DeleteAsync(Guid coachId, Guid programId)
    {
        var program = await db.OfferPrograms
            .FirstOrDefaultAsync(p => p.OfferProgramId == programId && p.CoachId == coachId)
            ?? throw new NotFoundException("Program not found.");

        program.OfferProgramIsActive = false;

        await db.SaveChangesAsync();
    }

    private static OfferProgramDto ToDto(OfferProgram p) =>
        new(p.OfferProgramId, p.OfferProgramName, p.OfferProgramIsActive, p.OfferProgramCreatedDate);
}
