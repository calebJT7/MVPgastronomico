namespace RotiseriaAPI.Models;

public enum BusinessType
{
    General = 0,
    Rotiseria = 1,
    Cafeteria = 2,
    Bar = 3,
    Restaurante = 4,
    LocalComida = 5
}

public class Business
{
    public int Id { get; set; }
    public string TradeName { get; set; } = string.Empty;
    public string? LegalName { get; set; }
    public string? TaxId { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? LogoUrl { get; set; }
    public string? OpeningHoursJson { get; set; }
    public string? PreferencesJson { get; set; }

    // Tipo de negocio y módulos habilitados
    public BusinessType BusinessType { get; set; } = BusinessType.General;
    public bool TablesEnabled { get; set; } = true;
    public bool DeliveryEnabled { get; set; } = true;
    public bool TakeawayEnabled { get; set; } = true;
    public bool KitchenEnabled { get; set; } = true;
    public bool ReservationsEnabled { get; set; } = true;
    public bool CashControlEnabled { get; set; } = true;
    public bool InventoryEnabled { get; set; } = true;

    public string TimeZoneId { get; set; } = "America/Argentina/Buenos_Aires";
    public OnboardingStep OnboardingStep { get; set; } = OnboardingStep.BusinessProfile;
    public bool OnboardingCompleted { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public ICollection<User> Users { get; set; } = new List<User>();
    public Subscription? Subscription { get; set; }
}
