using System.ComponentModel.DataAnnotations;
using RotiseriaAPI.Models;

namespace RotiseriaAPI.DTOs;

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
}

public class RegisterRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(8), MaxLength(100)]
    public string Password { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string BusinessName { get; set; } = string.Empty;

    public BusinessType BusinessType { get; set; } = BusinessType.General;

    [Required]
    public bool AcceptLegal { get; set; }
}

public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class ForgotPasswordRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordRequest
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string NewPassword { get; set; } = string.Empty;
}

public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string NewPassword { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int BusinessId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public bool OnboardingCompleted { get; set; }
    public string SubscriptionStatus { get; set; } = string.Empty;
    public string PlanCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public List<string> EnabledFeatures { get; set; } = new();
}

public class BusinessUpdateRequest
{
    [Required, MaxLength(200)]
    public string TradeName { get; set; } = string.Empty;
    public string? LegalName { get; set; }
    public string? TaxId { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? LogoUrl { get; set; }
    public string? OpeningHoursJson { get; set; }
    public string? PreferencesJson { get; set; }

    public BusinessType BusinessType { get; set; } = BusinessType.General;
    public bool TablesEnabled { get; set; } = true;
    public bool DeliveryEnabled { get; set; } = true;
    public bool TakeawayEnabled { get; set; } = true;
    public bool KitchenEnabled { get; set; } = true;
    public bool ReservationsEnabled { get; set; } = true;
    public bool CashControlEnabled { get; set; } = true;
    public bool InventoryEnabled { get; set; } = true;

    public string? TimeZoneId { get; set; }
}

public class CreateUserRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string FullName { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = "Employee";
}

public class UpdateUserRequest
{
    [Required]
    public string FullName { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = "Employee";

    public bool IsActive { get; set; } = true;
}

public class ProductWriteRequest
{
    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Range(0, 100_000_000)]
    public decimal Price { get; set; }

    public int? CategoryId { get; set; }
    public int Stock { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsAvailable { get; set; } = true;
}

public class CreateOrderRequest
{
    public int? CustomerId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string DeliveryAddress { get; set; } = string.Empty;
    public string OrderType { get; set; } = "Local";
    public int? TableId { get; set; }
    public string PaymentMethod { get; set; } = "Efectivo";
    public string? Comments { get; set; }
    public decimal DeliveryCost { get; set; }
    public decimal TipAmount { get; set; }
    public List<CreateOrderItemRequest> Items { get; set; } = new();
}

public class CreateOrderItemRequest
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}

public class CreateCheckoutRequest
{
    [Required]
    public int PlanId { get; set; }

    [Required]
    public string Provider { get; set; } = "Dev";

    public string ReturnUrl { get; set; } = "/subscription";
}

public class BillingWebhookRequest
{
    public string Provider { get; set; } = string.Empty;
    public string ProviderEventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? ProviderSubscriptionId { get; set; }
    public int? BusinessId { get; set; }
    public string? NewStatus { get; set; }
    public DateTime? PeriodEndUtc { get; set; }
}
