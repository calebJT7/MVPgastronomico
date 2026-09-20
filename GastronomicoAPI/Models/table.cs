namespace RotiseriaAPI.Models;

public class Table : ITenantEntity
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public int Number { get; set; }
    public string Status { get; set; } = "Libre";
    public int? CurrentOrderId { get; set; }
    public string? Label { get; set; }
}
