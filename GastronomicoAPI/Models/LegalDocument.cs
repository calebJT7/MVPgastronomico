namespace RotiseriaAPI.Models;

public class LegalDocument
{
    public int Id { get; set; }
    public LegalDocumentType Type { get; set; }
    public string Version { get; set; } = "0.1-draft";
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime EffectiveAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsCurrent { get; set; } = true;
}
