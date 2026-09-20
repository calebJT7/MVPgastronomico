namespace RotiseriaAPI.Models;

public class LegalAcceptance
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int BusinessId { get; set; }
    public int LegalDocumentId { get; set; }
    public LegalDocument? LegalDocument { get; set; }
    public string DocumentVersion { get; set; } = string.Empty;
    public LegalDocumentType DocumentType { get; set; }
    public DateTime AcceptedAtUtc { get; set; } = DateTime.UtcNow;
}
