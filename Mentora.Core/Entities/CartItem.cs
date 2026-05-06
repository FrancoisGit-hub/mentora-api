namespace Mentora.Core.Entities;

public class CartItem
{
    public Guid CartItemId { get; set; }

    public Guid CartId { get; set; }
    public Cart Cart { get; set; } = null!;

    // Exactly one of ProductId / PackId is set (enforced by DB check constraint)
    public Guid? ProductId { get; set; }
    public Product? Product { get; set; }

    public Guid? PackId { get; set; }
    public ProductPack? Pack { get; set; }

    public int Quantity { get; set; }
    public DateTime AddedDate { get; set; }
}
