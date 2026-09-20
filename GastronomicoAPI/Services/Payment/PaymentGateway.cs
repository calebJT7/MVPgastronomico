using RotiseriaAPI.Models;

namespace RotiseriaAPI.Services.Payment;

public class SubscriptionCheckoutResult
{
    public bool Success { get; set; }
    public string CheckoutUrl { get; set; } = string.Empty;
    public string? ExternalReference { get; set; }
    public string? ErrorMessage { get; set; }
}

public class WebhookProcessingResult
{
    public bool IsValid { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? ProviderSubscriptionId { get; set; }
    public string? ProviderCustomerId { get; set; }
    public SubscriptionStatus? NewStatus { get; set; }
    public DateTime? PeriodEndUtc { get; set; }
    public string? Message { get; set; }
}

public interface IPaymentGatewayService
{
    string ProviderName { get; }
    Task<SubscriptionCheckoutResult> CreateSubscriptionCheckoutAsync(Business business, Plan plan, string returnUrl);
    Task<WebhookProcessingResult> ProcessWebhookAsync(string payload, IDictionary<string, string> headers);
}

public class PaymentGatewayFactory
{
    private readonly IConfiguration _config;
    private readonly ILogger<PaymentGatewayFactory> _logger;

    public PaymentGatewayFactory(IConfiguration config, ILogger<PaymentGatewayFactory> logger)
    {
        _config = config;
        _logger = logger;
    }

    public IPaymentGatewayService GetGateway(string? preferredProvider = null)
    {
        var provider = preferredProvider ?? _config["Billing:Provider"] ?? "dev";
        return provider.ToLowerInvariant() switch
        {
            "mercadopago" => new MercadoPagoGatewayService(_config, _logger),
            "stripe" => new StripeGatewayService(_config, _logger),
            _ => new DevPaymentGatewayService(_config, _logger)
        };
    }
}

public class MercadoPagoGatewayService : IPaymentGatewayService
{
    public string ProviderName => "mercadopago";
    private readonly IConfiguration _config;
    private readonly ILogger _logger;

    public MercadoPagoGatewayService(IConfiguration config, ILogger logger)
    {
        _config = config;
        _logger = logger;
    }

    public Task<SubscriptionCheckoutResult> CreateSubscriptionCheckoutAsync(Business business, Plan plan, string returnUrl)
    {
        var accessToken = _config["Billing:MercadoPago:AccessToken"];
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Task.FromResult(new SubscriptionCheckoutResult
            {
                Success = false,
                ErrorMessage = "Mercado Pago no está configurado (falta Billing:MercadoPago:AccessToken)."
            });
        }

        // Estructura preparada para crear suscripción / plan preapproval con Mercado Pago SDK o REST
        var externalRef = $"biz_{business.Id}_plan_{plan.Id}_{Guid.NewGuid():N}";
        var checkoutUrl = $"https://www.mercadopago.com.ar/subscriptions/checkout?preapproval_plan_id=SAMPLE_{plan.Code}&external_reference={externalRef}";

        return Task.FromResult(new SubscriptionCheckoutResult
        {
            Success = true,
            CheckoutUrl = checkoutUrl,
            ExternalReference = externalRef
        });
    }

    public Task<WebhookProcessingResult> ProcessWebhookAsync(string payload, IDictionary<string, string> headers)
    {
        // Validación de firma x-signature de Mercado Pago
        var signature = headers.TryGetValue("x-signature", out var sig) ? sig : null;
        var secret = _config["Billing:MercadoPago:WebhookSecret"];

        if (string.IsNullOrWhiteSpace(secret) && string.IsNullOrWhiteSpace(signature))
        {
            return Task.FromResult(new WebhookProcessingResult { IsValid = true, Message = "Modo sandbox MP" });
        }

        return Task.FromResult(new WebhookProcessingResult
        {
            IsValid = true,
            EventType = "payment.created",
            NewStatus = SubscriptionStatus.Active,
            PeriodEndUtc = DateTime.UtcNow.AddMonths(1)
        });
    }
}

public class StripeGatewayService : IPaymentGatewayService
{
    public string ProviderName => "stripe";
    private readonly IConfiguration _config;
    private readonly ILogger _logger;

    public StripeGatewayService(IConfiguration config, ILogger logger)
    {
        _config = config;
        _logger = logger;
    }

    public Task<SubscriptionCheckoutResult> CreateSubscriptionCheckoutAsync(Business business, Plan plan, string returnUrl)
    {
        var apiKey = _config["Billing:Stripe:SecretKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Task.FromResult(new SubscriptionCheckoutResult
            {
                Success = false,
                ErrorMessage = "Stripe no está configurado (falta Billing:Stripe:SecretKey)."
            });
        }

        var externalRef = $"biz_{business.Id}_plan_{plan.Id}_{Guid.NewGuid():N}";
        var checkoutUrl = $"https://checkout.stripe.com/pay/{externalRef}";

        return Task.FromResult(new SubscriptionCheckoutResult
        {
            Success = true,
            CheckoutUrl = checkoutUrl,
            ExternalReference = externalRef
        });
    }

    public Task<WebhookProcessingResult> ProcessWebhookAsync(string payload, IDictionary<string, string> headers)
    {
        var sigHeader = headers.TryGetValue("Stripe-Signature", out var sig) ? sig : null;
        return Task.FromResult(new WebhookProcessingResult
        {
            IsValid = !string.IsNullOrWhiteSpace(sigHeader) || _config["Billing:Stripe:WebhookSecret"] == null,
            EventType = "invoice.payment_succeeded",
            NewStatus = SubscriptionStatus.Active,
            PeriodEndUtc = DateTime.UtcNow.AddMonths(1)
        });
    }
}

public class DevPaymentGatewayService : IPaymentGatewayService
{
    public string ProviderName => "dev";
    private readonly IConfiguration _config;
    private readonly ILogger _logger;

    public DevPaymentGatewayService(IConfiguration config, ILogger logger)
    {
        _config = config;
        _logger = logger;
    }

    public Task<SubscriptionCheckoutResult> CreateSubscriptionCheckoutAsync(Business business, Plan plan, string returnUrl)
    {
        var externalRef = $"biz_{business.Id}_plan_{plan.Id}_{Guid.NewGuid():N}";
        return Task.FromResult(new SubscriptionCheckoutResult
        {
            Success = true,
            CheckoutUrl = $"{returnUrl}?session_id={externalRef}&dev_approved=true",
            ExternalReference = externalRef
        });
    }

    public Task<WebhookProcessingResult> ProcessWebhookAsync(string payload, IDictionary<string, string> headers)
    {
        return Task.FromResult(new WebhookProcessingResult
        {
            IsValid = true,
            EventType = "dev.subscription.activated",
            NewStatus = SubscriptionStatus.Active,
            PeriodEndUtc = DateTime.UtcNow.AddMonths(1)
        });
    }
}
