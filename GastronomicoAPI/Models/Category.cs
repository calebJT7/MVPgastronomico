namespace RotiseriaAPI.Models;

public class Category : ITenantEntity
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool TracksStock { get; set; }
    public bool IsActive { get; set; } = true;
}
