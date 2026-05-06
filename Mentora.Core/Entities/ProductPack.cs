using Mentora.Core.Enums;

namespace Mentora.Core.Entities;

public class ProductPack
{
    public Guid ProductPackId { get; set; }
    public string ProductPackName { get; set; } = null!;
    public string? ProductPackDescription { get; set; }
    public decimal ProductPackPriceEuros { get; set; }
    public decimal ProductPackDiscountPercent { get; set; }
    public ProductStatus ProductPackStatus { get; set; } = ProductStatus.Draft;
    public DateTime ProductPackCreatedDate { get; set; }
    public DateTime ProductPackUpdatedDate { get; set; }

    public Guid CoachId { get; set; }
    public Coach Coach { get; set; } = null!;

    public ICollection<ProductPackItem> Items { get; set; } = [];
}
