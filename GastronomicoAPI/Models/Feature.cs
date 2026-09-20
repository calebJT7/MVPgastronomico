namespace RotiseriaAPI.Models;

public static class FeatureCodes
{
    // Básico
    public const string Products = "PRODUCTS";
    public const string Orders = "ORDERS";
    public const string Tables = "TABLES";
    public const string Customers = "CUSTOMERS";
    public const string Cash = "CASH";
    public const string BasicStock = "BASIC_STOCK";
    public const string BasicReports = "BASIC_REPORTS";
    public const string BusinessSettings = "BUSINESS_SETTINGS";

    // Premium
    public const string AdvancedInventory = "ADVANCED_INVENTORY";
    public const string Recipes = "RECIPES";
    public const string Kds = "KDS";
    public const string Reservations = "RESERVATIONS";
    public const string SplitBills = "SPLIT_BILLS";
    public const string ModifiersCombos = "MODIFIERS_COMBOS";
    public const string EmployeeConsumption = "EMPLOYEE_CONSUMPTION";
    public const string AdvancedReports = "ADVANCED_REPORTS";
    public const string AdvancedPermissions = "ADVANCED_PERMISSIONS";
    public const string Audit = "AUDIT";
}

public class Feature
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsPremium { get; set; }
    public int SortOrder { get; set; }

    public ICollection<PlanFeature> PlanFeatures { get; set; } = new List<PlanFeature>();
}

public class PlanFeature
{
    public int PlanId { get; set; }
    public Plan? Plan { get; set; }
    public int FeatureId { get; set; }
    public Feature? Feature { get; set; }
}
