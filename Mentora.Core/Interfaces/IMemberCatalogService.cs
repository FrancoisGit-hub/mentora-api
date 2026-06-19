using Mentora.Core.DTOs.Catalog;
using Mentora.Core.Enums;

namespace Mentora.Core.Interfaces;

public interface IMemberCatalogService
{
    Task<MemberCatalogResponseDto> GetCatalogAsync(
        Guid memberId,
        Guid coachId,
        OfferType? offerType,
        Guid? offerProgramId,
        CancellationToken ct);
}
