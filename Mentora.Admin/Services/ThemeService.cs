using Microsoft.JSInterop;

namespace Mentora.Admin.Services;

/// <summary>Trois modes (Auto, Clair, Sombre). Le stockage et la classe sur html sont faits par admin.js.</summary>
public sealed class ThemeService(IJSRuntime js)
{
    private static readonly string[] Modes = ["auto", "light", "dark"];

    private bool _loaded;

    public string Mode { get; private set; } = "auto";

    public event Action? Changed;

    public string Label => $"Thème : {LabelOf(Mode)}";

    public string AriaLabel => $"{Label}, passer au mode {LabelOf(NextMode())}";

    public async Task EnsureLoadedAsync()
    {
        if (_loaded)
            return;
        _loaded = true;
        try
        {
            var mode = await js.InvokeAsync<string>("mentoraAdmin.getThemeMode");
            if (Array.IndexOf(Modes, mode) >= 0)
                Mode = mode;
            await js.InvokeVoidAsync("mentoraAdmin.syncTheme");
        }
        catch (JSException)
        {
        }
        Changed?.Invoke();
    }

    public async Task CycleAsync()
    {
        Mode = NextMode();
        try
        {
            await js.InvokeVoidAsync("mentoraAdmin.setThemeMode", Mode);
        }
        catch (JSException)
        {
        }
        Changed?.Invoke();
    }

    private string NextMode() => Modes[(Array.IndexOf(Modes, Mode) + 1) % Modes.Length];

    private static string LabelOf(string mode) => mode switch
    {
        "light" => "Clair",
        "dark" => "Sombre",
        _ => "Auto"
    };
}
