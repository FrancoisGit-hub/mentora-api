using Microsoft.JSInterop;

namespace Mentora.Admin.Api;

public enum RefreshOutcome
{
    Ok = 0,
    /// <summary>Le serveur a refusé le jeton (401) ou il n'y en a plus : session terminée.</summary>
    Rejected = 1,
    /// <summary>Réseau coupé ou erreur serveur : la session n'est pas perdue.</summary>
    Unavailable = 2
}

/// <summary>
/// Session de l'admin (décision B de 12.1) : jeton d'accès en mémoire seulement, jeton de
/// rafraîchissement dans localStorage ("mentora-admin-refresh"). Chaque rafraîchissement
/// s'exécute sous navigator.locks et relit le jeton stocké dans le verrou : deux onglets ou
/// deux appels parallèles n'envoient jamais deux fois le même jeton. Rien n'est journalisé.
/// </summary>
public sealed class AdminSession : IDisposable
{
    public const string ExpiredMessage = "Votre session a expiré, reconnectez-vous.";
    public const string ServerDownMessage = "Le serveur ne répond pas, réessayez.";

    private static readonly TimeSpan RefreshMargin = TimeSpan.FromSeconds(60);

    private readonly ApiTransport _api;
    private readonly IJSRuntime _js;
    private readonly DotNetObjectReference<AdminSession> _self;

    private string? _access;
    private DateTimeOffset _accessExpiresAt;
    private Task? _init;
    private Task<RefreshOutcome>? _refreshing;
    private bool _forceCurrentRefresh;

    // Repli quand localStorage est inutilisable : le jeton ne survit alors pas au rechargement.
    private bool _storageBroken;
    private string? _memoryRefresh;

    public AdminSession(ApiTransport api, IJSRuntime js)
    {
        _api = api;
        _js = js;
        _self = DotNetObjectReference.Create(this);
    }

    public event Action? Changed;

    public string? Email { get; private set; }

    public string? AccessToken => _access;

    public bool IsAuthenticated => _access is not null && Email is not null;

    /// <summary>Message à afficher sur la page de connexion (session expirée, serveur absent).</summary>
    public string? Notice { get; private set; }

    public Task InitializeAsync() => _init ??= InitializeCoreAsync();

    private async Task InitializeCoreAsync()
    {
        var stored = await GetStoredRefreshAsync();
        if (string.IsNullOrEmpty(stored))
            return;

        var outcome = await RefreshAsync(force: true);
        if (outcome == RefreshOutcome.Unavailable)
        {
            Notice = ServerDownMessage;
            return;
        }
        if (outcome != RefreshOutcome.Ok || _access is null)
            return;

        var me = await _api.SendAsync<AdminMe>(HttpMethod.Get, "api/v1/admin/auth/me", bearer: _access);
        if (me.IsSuccess && me.Data is not null)
        {
            Email = me.Data.Email;
            Changed?.Invoke();
        }
        else if (me.IsUnauthorized)
        {
            await EndSessionAsync(ExpiredMessage);
        }
        // Réseau coupé pendant me : l'adresse reçue au rafraîchissement suffit.
    }

    public Task<ApiResult<NoData>> RequestCodeAsync(string email) =>
        _api.SendAsync<NoData>(HttpMethod.Post, "api/v1/admin/auth/otp/request", new RequestCodeBody(email));

    public async Task<ApiResult<AdminAuthData>> VerifyCodeAsync(string email, string code)
    {
        var result = await _api.SendAsync<AdminAuthData>(
            HttpMethod.Post, "api/v1/admin/auth/otp/verify", new VerifyCodeBody(email, code));

        if (result.IsSuccess && result.Data is { } data)
        {
            await SetStoredRefreshAsync(data.RefreshToken);
            Apply(data);
            Notice = null;
            Changed?.Invoke();
        }
        return result;
    }

    public async Task LogoutAsync()
    {
        var stored = await GetStoredRefreshAsync();
        if (!string.IsNullOrEmpty(stored))
        {
            // Un échec de l'appel ne bloque pas la déconnexion locale.
            try
            {
                await _api.SendAsync<NoData>(HttpMethod.Post, "api/v1/admin/auth/logout", new LogoutBody(stored));
            }
            catch (Exception)
            {
            }
        }
        await EndSessionAsync(null);
    }

    /// <summary>Jeton d'accès valide, rafraîchi d'abord s'il expire dans moins de 60 secondes.</summary>
    public async Task<(string? Token, RefreshOutcome Outcome)> GetAccessTokenAsync()
    {
        if (_access is null)
            return (null, RefreshOutcome.Rejected);

        if (_accessExpiresAt - DateTimeOffset.UtcNow < RefreshMargin)
        {
            var outcome = await RefreshAsync(force: false);
            return outcome == RefreshOutcome.Ok ? (_access, outcome) : (null, outcome);
        }
        return (_access, RefreshOutcome.Ok);
    }

    /// <summary>Après un 401 avec <paramref name="usedToken"/> : un seul rafraîchissement, partagé.</summary>
    public async Task<RefreshOutcome> RefreshAfterUnauthorizedAsync(string usedToken)
    {
        // Un autre appel a déjà renouvelé le jeton : on réessaie simplement avec le nouveau.
        if (_access is not null && !string.Equals(_access, usedToken, StringComparison.Ordinal))
            return RefreshOutcome.Ok;
        return await RefreshAsync(force: true);
    }

    /// <summary>Termine la session localement et efface le jeton stocké.</summary>
    public async Task EndSessionAsync(string? notice)
    {
        await ClearStoredRefreshAsync();
        ClearLocal(notice);
    }

    private Task<RefreshOutcome> RefreshAsync(bool force)
    {
        if (_refreshing is { IsCompleted: false })
        {
            _forceCurrentRefresh |= force;
            return _refreshing;
        }
        _forceCurrentRefresh = force;
        return _refreshing = RunRefreshUnderLockAsync();
    }

    private async Task<RefreshOutcome> RunRefreshUnderLockAsync()
    {
        try
        {
            var code = await _js.InvokeAsync<int>(
                "mentoraAdmin.runExclusiveRefresh", _self, nameof(RefreshInsideLock));
            return (RefreshOutcome)code;
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            return RefreshOutcome.Unavailable;
        }
    }

    /// <summary>Appelé par JS une fois le verrou exclusif obtenu.</summary>
    [JSInvokable]
    public async Task<int> RefreshInsideLock()
    {
        // Pendant l'attente du verrou, le jeton a pu être renouvelé : rien à faire.
        if (!_forceCurrentRefresh && _access is not null
            && _accessExpiresAt - DateTimeOffset.UtcNow >= RefreshMargin)
            return (int)RefreshOutcome.Ok;

        // Le jeton stocké est lu dans le verrou, jamais avant.
        var stored = await GetStoredRefreshAsync();
        if (string.IsNullOrEmpty(stored))
        {
            // Déconnexion faite dans un autre onglet.
            ClearLocal(null);
            return (int)RefreshOutcome.Rejected;
        }

        var result = await _api.SendAsync<AdminAuthData>(
            HttpMethod.Post, "api/v1/admin/auth/token/refresh", new RefreshBody(stored));

        if (result.IsSuccess && result.Data is { } data)
        {
            await SetStoredRefreshAsync(data.RefreshToken);
            Apply(data);
            Changed?.Invoke();
            return (int)RefreshOutcome.Ok;
        }

        if (result.IsUnauthorized)
        {
            await ClearStoredRefreshAsync();
            ClearLocal(ExpiredMessage);
            return (int)RefreshOutcome.Rejected;
        }

        return (int)RefreshOutcome.Unavailable;
    }

    private void Apply(AdminAuthData data)
    {
        _access = data.AccessToken;
        _accessExpiresAt = DateTimeOffset.UtcNow.AddSeconds(data.ExpiresIn);
        Email = data.Admin.Email;
    }

    private void ClearLocal(string? notice)
    {
        _access = null;
        _accessExpiresAt = default;
        Email = null;
        Notice = notice;
        Changed?.Invoke();
    }

    private async Task<string?> GetStoredRefreshAsync()
    {
        if (_storageBroken)
            return _memoryRefresh;
        try
        {
            return await _js.InvokeAsync<string?>("mentoraAdmin.getRefreshToken");
        }
        catch (JSException)
        {
            return _memoryRefresh;
        }
    }

    private async Task SetStoredRefreshAsync(string value)
    {
        _memoryRefresh = value;
        try
        {
            var ok = await _js.InvokeAsync<bool>("mentoraAdmin.setRefreshToken", value);
            _storageBroken = !ok;
        }
        catch (JSException)
        {
            _storageBroken = true;
        }
    }

    private async Task ClearStoredRefreshAsync()
    {
        _memoryRefresh = null;
        try
        {
            await _js.InvokeVoidAsync("mentoraAdmin.clearRefreshToken");
        }
        catch (JSException)
        {
        }
    }

    public void Dispose() => _self.Dispose();
}
