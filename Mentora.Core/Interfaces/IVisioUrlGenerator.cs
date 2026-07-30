namespace Mentora.Core.Interfaces;

public interface IVisioUrlGenerator
{
    /// <summary>Returns a random Jitsi room URL (used by session scheduling).</summary>
    string Generate();
}
