using Mentora.Core.DTOs;
using Mentora.Core.DTOs.Credits;

namespace Mentora.Core.Interfaces;

public interface ICreditService
{
    Task<CreditBalanceResponse> GetBalanceAsync(Guid userId, Guid coachId);
    Task<PurchaseCreditsResponse> PurchaseCreditsAsync(Guid userId, Guid coachId, int quantity);
    Task<PagedResult<CreditTransactionResponse>> GetTransactionsAsync(Guid userId, Guid coachId, int page, int size);
}
