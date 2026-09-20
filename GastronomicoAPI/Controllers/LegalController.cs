using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RotiseriaAPI.Data;

namespace RotiseriaAPI.Controllers;

[Route("api/legal")]
[ApiController]
public class LegalController : ControllerBase
{
    private readonly AppDbContext _db;
    public LegalController(AppDbContext db) => _db = db;

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Current()
    {
        var docs = await _db.LegalDocuments.AsNoTracking().Where(d => d.IsCurrent).ToListAsync();
        return Ok(docs);
    }

    [HttpGet("{type}")]
    [AllowAnonymous]
    public async Task<IActionResult> ByType(string type)
    {
        if (!Enum.TryParse<Models.LegalDocumentType>(type, true, out var parsed))
            return BadRequest();
        var doc = await _db.LegalDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.IsCurrent && d.Type == parsed);
        return doc == null ? NotFound() : Ok(doc);
    }
}
