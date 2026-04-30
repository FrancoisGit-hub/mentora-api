using Mentora.Core.Enums;

namespace Mentora.Core.Entities;

public class Product
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = null!;
    public string? ProductDescription { get; set; }
    public OfferType ProductOfferType { get; set; }
    public string ProductOfferNature { get; set; } = "CLASSIQUE";
    public int ProductDurationMinutes { get; set; }
    public decimal ProductPriceEuros { get; set; }
    public Sport ProductSport { get; set; } = Sport.Training;
    public string? ProductLocation { get; set; }
    public List<string>? ProductTags { get; set; }
    public ProductStatus ProductStatus { get; set; } = ProductStatus.Draft;
    public DateTime ProductCreatedDate { get; set; }
    public DateTime ProductUpdatedDate { get; set; }

    public Guid OfferProgramId { get; set; }
    public OfferProgram OfferProgram { get; set; } = null!;

    public Guid CoachId { get; set; }
    public Coach Coach { get; set; } = null!;

    public ICollection<ProductPackItem> PackItems { get; set; } = [];
}
