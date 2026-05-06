using Mentora.Core.Interfaces;

namespace Mentora.Infrastructure.Services.Visio;

public class JitsiVisioUrlGenerator : IVisioUrlGenerator
{
    public string Generate()
    {
        var roomId = "mentora-" + Guid.NewGuid().ToString("N");
        return $"https://meet.jit.si/{roomId}";
    }
}
