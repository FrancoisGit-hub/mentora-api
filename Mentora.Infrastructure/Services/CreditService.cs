using Mentora.Core.DTOs;
using Mentora.Core.DTOs.Credits;
using Mentora.Core.Entities;
using Mentora.Core.Interfaces;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mentora.Infrastructure.Services;

public class CreditService(MentoraDbContext db) : ICreditService
{
    public async Task<CreditBalanceResponse> GetBalanceAsync(Guid userId, Guid coachId)
    {
        var balance = await db.CreditBalances
            .FirstOrDefaultAsync(b => b.UserId == userId && b.CoachId == coachId);

        var parameters = await db.CoachParameters
            .FirstOrDefaultAsync(p => p.CoachId == coachId)
            ?? throw new InvalidOperationException("Coach parameters not found.");

        var amount = balance?.CreditBalanceAmount ?? 0;

        return new CreditBalanceResponse(
            Balance: amount,
            CreditValueEuros: parameters.CoachCreditValueEuros,
            EquivalentEuros: amount * parameters.CoachCreditValueEuros
        );
    }

    public async Task<PurchaseCreditsResponse> PurchaseCreditsAsync(Guid userId, Guid coachId, int quantity)
    {
        if (quantity <= 0)
            throw new InvalidOperationException("Quantity must be greater than zero.");

        var parameters = await db.CoachParameters
            .FirstOrDefaultAsync(p => p.CoachId == coachId)
            ?? throw new InvalidOperationException("Coach parameters not found.");

        var totalEuros = quantity * parameters.CoachCreditValueEuros;

        var balance = await db.CreditBalances
            .FirstOrDefaultAsync(b => b.UserId == userId && b.CoachId == coachId);

        if (balance == null)
        {
            balance = new CreditBalance
            {
                CreditBalanceAmount = 0,
                CreditBalanceUpdatedDate = DateTime.UtcNow,
                UserId = userId,
                CoachId = coachId
            };
            db.CreditBalances.Add(balance);
        }

        db.CreditTransactions.Add(new CreditTransaction
        {
            CreditTransactionType = "PURCHASE",
            CreditTransactionAmount = quantity,
            CreditTransactionEuros = totalEuros,
            CreditTransactionDate = DateTime.UtcNow,
            UserId = userId,
            CoachId = coachId
        });

        balance.CreditBalanceAmount += quantity;
        balance.CreditBalanceUpdatedDate = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return new PurchaseCreditsResponse(
            QuantityPurchased: quantity,
            TotalPaidEuros: totalEuros,
            NewBalance: balance.CreditBalanceAmount
        );
    }

    public async Task<PagedResult<CreditTransactionResponse>> GetTransactionsAsync(Guid userId, Guid coachId, int page, int size)
    {
        var query = db.CreditTransactions
            .Where(t => t.UserId == userId && t.CoachId == coachId);

        var totalItems = await query.CountAsync();

        var items = await query
            .OrderByDescending(t => t.CreditTransactionDate)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(t => new CreditTransactionResponse(
                t.CreditTransactionId,
                t.CreditTransactionType,
                t.CreditTransactionAmount,
                t.CreditTransactionEuros,
                t.CreditTransactionDate,
                t.SessionId
            ))
            .ToListAsync();

        return new PagedResult<CreditTransactionResponse>(items, totalItems, page, size);
    }
}
