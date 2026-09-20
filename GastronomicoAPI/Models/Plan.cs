namespace RotiseriaAPI.Models;

public class Plan
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty; // "basic", "premium"
    public string Name { get; set; } = string.Empty; // "Básico", "Premium"
    public string? Description { get; set; }
    public string Currency { get; set; } = "ARS";
    public decimal MonthlyPrice { get; set; }
    public int TrialDays { get; set; } = 14;
    public int MaxUsers { get; set; } = 3;
    public int MaxProducts { get; set; } = 100;
    public int MaxTables { get; set; } = 15;
    public int MaxCustomers { get; set; } = 250;
    public int MaxBranches { get; set; } = 1;
    public bool ReportsEnabled { get; set; } = true;
    public bool PremiumFeaturesEnabled { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public string? ExternalPriceId { get; set; }

    public ICollection<PlanFeature> PlanFeatures { get; set; } = new List<PlanFeature>();
}
