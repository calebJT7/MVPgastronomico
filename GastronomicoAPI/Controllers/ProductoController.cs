using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RotiseriaAPI.Data;
using RotiseriaAPI.DTOs;
using RotiseriaAPI.Middleware;
using RotiseriaAPI.Models;
using RotiseriaAPI.Services;

namespace RotiseriaAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ProductoController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAuditService _audit;
    private readonly SubscriptionAccessService _access;

    public ProductoController(AppDbContext context, IAuditService audit, SubscriptionAccessService access)
    {
        _context = context;
        _audit = audit;
        _access = access;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Product>>> GetProducts([FromQuery] int page = 1, [FromQuery] int pageSize = 100, [FromQuery] string? q = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var query = _context.Products.AsNoTracking().Include(p => p.Category).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(p => p.Name.ToLower().Contains(q.ToLower()));

        var total = await query.CountAsync();
        var items = await query.OrderBy(p => p.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new PagedResult<Product> { Items = items, Page = page, PageSize = pageSize, Total = total });
    }

    [HttpGet("activos")]
    public async Task<ActionResult<IEnumerable<Product>>> GetActiveProducts()
    {
        return await _context.Products.AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.IsActive && p.IsAvailable)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    [HttpPost]
    [Authorize(Roles = $"{UserRoleNames.Owner},{UserRoleNames.Admin},{UserRoleNames.Manager}")]
    public async Task<ActionResult<Product>> PostProduct(ProductWriteRequest request)
    {
        var count = await _context.Products.CountAsync();
        await _access.EnsureLimitAsync("products", count);
        await EnsureCategory(request.CategoryId);

        var product = new Product
        {
            Name = request.Name.Trim(),
            Price = request.Price,
            CategoryId = request.CategoryId,
            Stock = request.Stock,
            IsActive = request.IsActive,
            IsAvailable = request.IsAvailable,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        await _audit.LogAsync("create_product", "Product", product.Id.ToString(), new { product.Name });

        var business = await _context.Businesses.FindAsync(product.BusinessId);
        if (business is { OnboardingCompleted: false, OnboardingStep: OnboardingStep.Products })
        {
            business.OnboardingStep = OnboardingStep.Tables;
            await _context.SaveChangesAsync();
        }

        return CreatedAtAction(nameof(GetProducts), new { id = product.Id }, product);
    }

    [HttpGet("search/{term}")]
    public async Task<ActionResult<IEnumerable<Product>>> SearchProducts(string term)
    {
        var t = term.ToLower();
        return await _context.Products.AsNoTracking()
            .Where(p => p.IsActive && p.IsAvailable && p.Name.ToLower().Contains(t))
            .Take(10)
            .ToListAsync();
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = $"{UserRoleNames.Owner},{UserRoleNames.Admin},{UserRoleNames.Manager}")]
    public async Task<IActionResult> PutProduct(int id, ProductWriteRequest request)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (product == null) return NotFound();
        await EnsureCategory(request.CategoryId);

        product.Name = request.Name.Trim();
        product.Price = request.Price;
        product.CategoryId = request.CategoryId;
        product.Stock = request.Stock;
        product.IsActive = request.IsActive;
        product.IsAvailable = request.IsAvailable;
        product.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await _audit.LogAsync("update_product", "Product", product.Id.ToString());
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = $"{UserRoleNames.Owner},{UserRoleNames.Admin},{UserRoleNames.Manager}")]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (product == null) return NotFound();
        product.IsActive = false;
        product.IsAvailable = false;
        product.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await _audit.LogAsync("deactivate_product", "Product", product.Id.ToString());
        return NoContent();
    }

    private async Task EnsureCategory(int? categoryId)
    {
        if (!categoryId.HasValue) return;
        var exists = await _context.Categories.AnyAsync(c => c.Id == categoryId.Value);
        if (!exists) throw new InvalidOperationException("La categoría no pertenece a este negocio.");
    }
}
