using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using RotiseriaWeb;
using RotiseriaWeb.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped<ILocalStorageService, LocalStorageService>();
builder.Services.AddScoped<JwtAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthenticationStateProvider>());
builder.Services.AddAuthorizationCore();

builder.Services.AddTransient<AuthHeaderHandler>();

var apiBaseUrl = builder.Configuration["ApiBaseUrl"];
Uri baseUri;

if (!string.IsNullOrWhiteSpace(apiBaseUrl))
{
    baseUri = new Uri(apiBaseUrl.EndsWith("/") ? apiBaseUrl : apiBaseUrl + "/");
}
else if (builder.HostEnvironment.BaseAddress.Contains(":5000"))
{
    baseUri = new Uri("http://localhost:5285/");
}
else if (builder.HostEnvironment.BaseAddress.Contains(":7206"))
{
    baseUri = new Uri("https://localhost:7148/");
}
else
{
    baseUri = new Uri(builder.HostEnvironment.BaseAddress);
}

builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<AuthHeaderHandler>();
    handler.InnerHandler = new HttpClientHandler();
    return new HttpClient(handler) { BaseAddress = baseUri };
});

builder.Services.AddScoped<SaaSStateService>();
builder.Services.AddMudServices();

var host = builder.Build();

try
{
    var stateService = host.Services.GetRequiredService<SaaSStateService>();
    await stateService.InitializeAsync();
}
catch { }

await host.RunAsync();