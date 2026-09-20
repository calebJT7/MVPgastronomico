namespace RotiseriaWeb.Models;

public class PlanFeatureDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsPremium { get; set; }
}

public class Plan
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Currency { get; set; } = "ARS";
    public decimal MonthlyPrice { get; set; }
    public bool PriceConfigured { get; set; }
    public int TrialDays { get; set; }
    public int MaxUsers { get; set; }
    public int MaxProducts { get; set; }
    public int MaxTables { get; set; }
    public int MaxCustomers { get; set; }
    public int MaxBranches { get; set; }
    public bool ReportsEnabled { get; set; }
    public bool PremiumFeaturesEnabled { get; set; }
    public List<PlanFeatureDto> Features { get; set; } = new();
}

public class Subscription
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public int PlanId { get; set; }
    public Plan? Plan { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? TrialEndsAtUtc { get; set; }
    public DateTime? CurrentPeriodStartUtc { get; set; }
    public DateTime? CurrentPeriodEndUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
}

public class SubscriptionDetailsDto
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? TrialEndsAtUtc { get; set; }
    public DateTime? CurrentPeriodStartUtc { get; set; }
    public DateTime? CurrentPeriodEndUtc { get; set; }
    public Plan? Plan { get; set; }
    public List<string> Features { get; set; } = new();
    public UsageMetricsDto? Usage { get; set; }
}

public class UsageMetricsDto
{
    public int Products { get; set; }
    public int Tables { get; set; }
    public int Users { get; set; }
    public int Customers { get; set; }
}

public class CreateCheckoutRequest
{
    public int PlanId { get; set; }
    public string? Provider { get; set; }
    public string ReturnUrl { get; set; } = string.Empty;
}

public class CheckoutResponse
{
    public bool Success { get; set; }
    public string CheckoutUrl { get; set; } = string.Empty;
    public string? ExternalReference { get; set; }
    public string? ErrorMessage { get; set; }
}
