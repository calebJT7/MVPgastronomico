using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components;

namespace RotiseriaWeb.Services;

public class AuthHeaderHandler : DelegatingHandler
{
    private readonly ILocalStorageService _localStorage;
    private readonly NavigationManager _nav;

    public AuthHeaderHandler(ILocalStorageService localStorage, NavigationManager nav)
    {
        _localStorage = localStorage;
        _nav = nav;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _localStorage.GetItemAsync(JwtAuthenticationStateProvider.TokenKey);
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            await _localStorage.RemoveItemAsync(JwtAuthenticationStateProvider.TokenKey);
            await _localStorage.RemoveItemAsync(JwtAuthenticationStateProvider.BusinessKey);
            if (!_nav.Uri.Contains("/login") && !_nav.Uri.Contains("/register"))
            {
                _nav.NavigateTo("/login");
            }
        }
        else if (response.StatusCode == HttpStatusCode.PaymentRequired)
        {
            if (!_nav.Uri.Contains("/subscription"))
            {
                _nav.NavigateTo("/subscription?reason=expired");
            }
        }

        return response;
    }
}
