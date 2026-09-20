namespace RotiseriaAPI.Models;

public class Subscription
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public Business? Business { get; set; }
    public int PlanId { get; set; }
    public Plan? Plan { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Trial;
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? TrialEndsAtUtc { get; set; }
    public DateTime? CurrentPeriodStartUtc { get; set; }
    public DateTime? CurrentPeriodEndUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public DateTime? SuspendedAtUtc { get; set; }
    public string? Provider { get; set; }
    public string? ProviderCustomerId { get; set; }
    public string? ProviderSubscriptionId { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
