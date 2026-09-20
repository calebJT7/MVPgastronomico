using System.Net.Http.Json;
using System.Text.Json;
using RotiseriaWeb.Models;

namespace RotiseriaWeb.Services;

public class SaaSStateService
{
    private readonly HttpClient _http;
    private readonly ILocalStorageService _storage;

    public Business? CurrentBusiness { get; private set; }
    public string UserFullName { get; private set; } = string.Empty;
    public string UserRole { get; private set; } = string.Empty;
    public string SubscriptionStatus { get; private set; } = string.Empty;
    public string PlanCode { get; private set; } = "basic";
    public string PlanName { get; private set; } = "Plan Básico";
    public HashSet<string> EnabledFeatures { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool HasFeature(string code) => EnabledFeatures.Contains(code);
    public bool IsPremium => HasFeature(FeatureCodes.Kds) || string.Equals(PlanCode, "premium", StringComparison.OrdinalIgnoreCase);

    public event Action? OnChange;

    public SaaSStateService(HttpClient http, ILocalStorageService storage)
    {
        _http = http;
        _storage = storage;
    }

    public async Task InitializeAsync()
    {
        try
        {
            var cachedJson = await _storage.GetItemAsync(JwtAuthenticationStateProvider.BusinessKey);
            if (!string.IsNullOrWhiteSpace(cachedJson))
            {
                var authResp = JsonSerializer.Deserialize<AuthResponse>(cachedJson);
                if (authResp != null)
                {
                    ApplyAuthResponse(authResp);
                }
            }

            var token = await _storage.GetItemAsync(JwtAuthenticationStateProvider.TokenKey);
            if (!string.IsNullOrWhiteSpace(token))
            {
                var me = await _http.GetFromJsonAsync<AuthResponse>("api/Auth/me");
                if (me != null)
                {
                    ApplyAuthResponse(me);
                    await _storage.SetItemAsync(JwtAuthenticationStateProvider.BusinessKey, JsonSerializer.Serialize(me));
                }
            }
        }
        catch
        {
            // fallback
        }
        NotifyStateChanged();
    }

    public async Task RefreshSubscriptionStateAsync()
    {
        try
        {
            var me = await _http.GetFromJsonAsync<AuthResponse>("api/Auth/me");
            if (me != null)
            {
                ApplyAuthResponse(me);
                await _storage.SetItemAsync(JwtAuthenticationStateProvider.BusinessKey, JsonSerializer.Serialize(me));
                NotifyStateChanged();
            }
        }
        catch
        {
            // fallback
        }
    }

    private void ApplyAuthResponse(AuthResponse auth)
    {
        CurrentBusiness = new Business
        {
            Id = auth.BusinessId,
            TradeName = auth.BusinessName,
            OnboardingCompleted = auth.OnboardingCompleted
        };
        UserFullName = auth.FullName;
        UserRole = auth.Role;
        SubscriptionStatus = auth.SubscriptionStatus;
        PlanCode = string.IsNullOrWhiteSpace(auth.PlanCode) ? "basic" : auth.PlanCode;
        PlanName = string.IsNullOrWhiteSpace(auth.PlanName) ? "Plan Básico" : auth.PlanName;
        EnabledFeatures = new HashSet<string>(auth.EnabledFeatures ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
    }

    public void SetAuthInfo(AuthResponse auth)
    {
        ApplyAuthResponse(auth);
        NotifyStateChanged();
    }

    public void UpdateBusinessInfo(Business business)
    {
        CurrentBusiness = business;
        NotifyStateChanged();
    }

    public void Clear()
    {
        CurrentBusiness = null;
        UserFullName = string.Empty;
        UserRole = string.Empty;
        SubscriptionStatus = string.Empty;
        PlanCode = "basic";
        PlanName = "Plan Básico";
        EnabledFeatures.Clear();
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
