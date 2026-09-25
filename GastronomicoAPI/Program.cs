using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RotiseriaAPI.Data;
using RotiseriaAPI.Middleware;
using RotiseriaAPI.Security;
using RotiseriaAPI.Services;

var builder = WebApplication.CreateBuilder(args);

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.SetIsOriginAllowed(_ => true)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
        else if (origins.Length > 0)
        {
            policy.WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
        else
        {
            policy.WithOrigins("http://localhost:5000", "https://localhost:7206", "http://127.0.0.1:5000", "https://127.0.0.1:7206")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    });
});

builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<TenantSaveChangesInterceptor>();
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    var cs = builder.Configuration.GetConnectionString("DefaultConnection")
             ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection no configurada.");
    var provider = builder.Configuration["Database:Provider"] ?? "Sqlite";
    if (provider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
        options.UseNpgsql(cs);
    else
        options.UseSqlite(cs);
    options.AddInterceptors(sp.GetRequiredService<TenantSaveChangesInterceptor>());
});

var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
{
    jwtKey = "DEV_ONLY_CHANGE_ME_GASTRONOMICO_LOCAL_KEY_32";
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "GastronomicoAPI",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "GastronomicoWeb",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ManageCatalog", p => p.RequireRole(UserRoleNames.Owner, UserRoleNames.Admin, UserRoleNames.Manager, UserRoleNames.Tester));
    options.AddPolicy("ManageUsers", p => p.RequireRole(UserRoleNames.Owner, UserRoleNames.Admin, UserRoleNames.Tester));
});

builder.Services.AddScoped<PrintService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<SubscriptionAccessService>();
builder.Services.AddScoped<ITenantDateTimeService, TenantDateTimeService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddSingleton<RotiseriaAPI.Services.Payment.PaymentGatewayFactory>();
builder.Services.AddSignalR();

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseHttpsRedirection();
app.UseCors("Default");
app.UseWebSockets();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseMiddleware<SubscriptionGuardMiddleware>();

app.MapControllers();
app.MapHub<RotiseriaAPI.Hubs.OrderHub>("/hubs/orders");
app.MapHealthChecks("/health");

using (var scope = app.Services.CreateScope())
{
    var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
    tenant.EnableBypass();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await context.Database.MigrateAsync();
    await DataSeeder.SeedAsync(context, app.Configuration);
    await LegacyTenantBackfill.RunAsync(context);
}

app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;

public static class LegacyTenantBackfill
{
    public static async Task RunAsync(AppDbContext db)
    {
        if (await db.Businesses.AnyAsync())
            return;

        // Base nueva: no hay negocios ni filas huérfanas.
        if (!await db.Products.IgnoreQueryFilters().AnyAsync()
            && !await db.Orders.IgnoreQueryFilters().AnyAsync()
            && !await db.Tables.IgnoreQueryFilters().AnyAsync()
            && !await db.Customers.IgnoreQueryFilters().AnyAsync())
            return;

        var business = new RotiseriaAPI.Models.Business
        {
            TradeName = "Negocio migrado",
            OnboardingCompleted = true,
            OnboardingStep = RotiseriaAPI.Models.OnboardingStep.Completed
        };
        db.Businesses.Add(business);
        await db.SaveChangesAsync();

        var plan = await db.Plans.FirstAsync();
        db.Subscriptions.Add(new RotiseriaAPI.Models.Subscription
        {
            BusinessId = business.Id,
            PlanId = plan.Id,
            Status = RotiseriaAPI.Models.SubscriptionStatus.Trial,
            TrialEndsAtUtc = DateTime.UtcNow.AddDays(plan.TrialDays)
        });

        await db.Database.ExecuteSqlRawAsync("UPDATE Products SET BusinessId = {0} WHERE BusinessId = 0 OR BusinessId IS NULL", business.Id);
        await db.Database.ExecuteSqlRawAsync("UPDATE Orders SET BusinessId = {0} WHERE BusinessId = 0 OR BusinessId IS NULL", business.Id);
        await db.Database.ExecuteSqlRawAsync("UPDATE OrderItems SET BusinessId = {0} WHERE BusinessId = 0 OR BusinessId IS NULL", business.Id);
        await db.Database.ExecuteSqlRawAsync("UPDATE Tables SET BusinessId = {0} WHERE BusinessId = 0 OR BusinessId IS NULL", business.Id);
        await db.Database.ExecuteSqlRawAsync("UPDATE Customers SET BusinessId = {0} WHERE BusinessId = 0 OR BusinessId IS NULL", business.Id);
        await db.Database.ExecuteSqlRawAsync("UPDATE Debts SET BusinessId = {0} WHERE BusinessId = 0 OR BusinessId IS NULL", business.Id);
        await db.Database.ExecuteSqlRawAsync("UPDATE Expenses SET BusinessId = {0} WHERE BusinessId = 0 OR BusinessId IS NULL", business.Id);
        await db.Database.ExecuteSqlRawAsync("UPDATE EmployeeConsumptions SET BusinessId = {0} WHERE BusinessId = 0 OR BusinessId IS NULL", business.Id);
        await db.SaveChangesAsync();
    }
}
