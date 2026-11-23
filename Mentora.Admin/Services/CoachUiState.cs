namespace Mentora.Admin.Services;

/// <summary>Message à afficher sur la liste des coachs après une création.</summary>
public sealed class CoachUiState
{
    public string? Flash { get; set; }

    public string? TakeFlash()
    {
        var value = Flash;
        Flash = null;
        return value;
    }
}
