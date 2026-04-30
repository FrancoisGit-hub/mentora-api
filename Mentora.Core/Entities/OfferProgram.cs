namespace Mentora.Core.Entities;

public class OfferProgram
{
    public Guid OfferProgramId { get; set; }
    public string OfferProgramName { get; set; } = null!;
    public bool OfferProgramIsActive { get; set; }
    public DateTime OfferProgramCreatedDate { get; set; }

    public Guid CoachId { get; set; }
    public Coach Coach { get; set; } = null!;

    public ICollection<Product> Products { get; set; } = [];
}
