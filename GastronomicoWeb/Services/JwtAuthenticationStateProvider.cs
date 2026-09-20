using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace RotiseriaWeb.Services;

public class JwtAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly ILocalStorageService _localStorage;
    private readonly AuthenticationState _anonymous;

    public const string TokenKey = "auth_token";
    public const string BusinessKey = "business_info";

    public JwtAuthenticationStateProvider(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
        _anonymous = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var token = await _localStorage.GetItemAsync(TokenKey);
            if (string.IsNullOrWhiteSpace(token))
                return _anonymous;

            var claims = ParseClaimsFromJwt(token);
            var expiry = claims.FirstOrDefault(c => c.Type == "exp")?.Value;
            if (expiry != null && long.TryParse(expiry, out var expSeconds))
            {
                var expDate = DateTimeOffset.FromUnixTimeSeconds(expSeconds).UtcDateTime;
                if (expDate <= DateTime.UtcNow)
                {
                    await _localStorage.RemoveItemAsync(TokenKey);
                    await _localStorage.RemoveItemAsync(BusinessKey);
                    return _anonymous;
                }
            }

            var identity = new ClaimsIdentity(claims, "jwt");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch
        {
            return _anonymous;
        }
    }

    public async Task MarkUserAsAuthenticatedAsync(string token, string? businessJson = null)
    {
        await _localStorage.SetItemAsync(TokenKey, token);
        if (!string.IsNullOrWhiteSpace(businessJson))
            await _localStorage.SetItemAsync(BusinessKey, businessJson);

        var claims = ParseClaimsFromJwt(token);
        var identity = new ClaimsIdentity(claims, "jwt");
        var user = new ClaimsPrincipal(identity);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
    }

    public async Task MarkUserAsLoggedOutAsync()
    {
        await _localStorage.RemoveItemAsync(TokenKey);
        await _localStorage.RemoveItemAsync(BusinessKey);
        NotifyAuthenticationStateChanged(Task.FromResult(_anonymous));
    }

    private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var claims = new List<Claim>();
        var parts = jwt.Split('.');
        if (parts.Length < 2) return claims;

        var payload = parts[1];
        var jsonBytes = ParseBase64WithoutPadding(payload);
        var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);

        if (keyValuePairs != null)
        {
            foreach (var kvp in keyValuePairs)
            {
                if (kvp.Key == "role" || kvp.Key == ClaimTypes.Role)
                {
                    claims.Add(new Claim(ClaimTypes.Role, kvp.Value.ToString() ?? ""));
                }
                else if (kvp.Key == "sub" || kvp.Key == ClaimTypes.NameIdentifier)
                {
                    claims.Add(new Claim(ClaimTypes.NameIdentifier, kvp.Value.ToString() ?? ""));
                }
                else if (kvp.Key == "unique_name" || kvp.Key == ClaimTypes.Name)
                {
                    claims.Add(new Claim(ClaimTypes.Name, kvp.Value.ToString() ?? ""));
                }
                else if (kvp.Key == "email" || kvp.Key == ClaimTypes.Email)
                {
                    claims.Add(new Claim(ClaimTypes.Email, kvp.Value.ToString() ?? ""));
                }
                else
                {
                    claims.Add(new Claim(kvp.Key, kvp.Value.ToString() ?? ""));
                }
            }
        }

        return claims;
    }

    private static byte[] ParseBase64WithoutPadding(string base64)
    {
        base64 = base64.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }
}
