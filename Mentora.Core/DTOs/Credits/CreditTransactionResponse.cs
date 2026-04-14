namespace Mentora.Core.DTOs.Credits;

public record CreditTransactionResponse(Guid TransactionId, string Type, int Amount, decimal? Euros, DateTime Date, Guid? SessionId);
