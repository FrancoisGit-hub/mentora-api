namespace Mentora.Core.Entities;

public class ProductPackItem
{
    public Guid ProductPackItemId { get; set; }
    public int ProductPackItemQuantity { get; set; }

    public Guid ProductPackId { get; set; }
    public ProductPack ProductPack { get; set; } = null!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
}
