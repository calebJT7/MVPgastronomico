using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RotiseriaAPI.Data;
using RotiseriaAPI.Middleware;
using RotiseriaAPI.Models;
using RotiseriaAPI.Services;

namespace RotiseriaAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
[RequireFeature(FeatureCodes.Recipes)]
public class RecipesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public RecipesController(AppDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    [HttpGet("product/{productId:int}")]
    public async Task<IActionResult> GetProductRecipe(int productId)
    {
        var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId);
        if (product == null) return NotFound("Producto no encontrado.");

        var items = await _db.RecipeItems
            .AsNoTracking()
            .Include(r => r.Supply)
            .Where(r => r.ProductId == productId)
            .Select(r => new
            {
                r.Id,
                r.SupplyId,
                SupplyName = r.Supply != null ? r.Supply.Name : string.Empty,
                Unit = r.Supply != null ? r.Supply.Unit : string.Empty,
                UnitCost = r.Supply != null ? r.Supply.CostPerUnit : 0,
                r.Quantity,
                SubtotalCost = r.Quantity * (r.Supply != null ? r.Supply.CostPerUnit : 0)
            })
            .ToListAsync();

        var totalCost = items.Sum(i => i.SubtotalCost);
        var margin = product.Price > 0 ? ((product.Price - totalCost) / product.Price) * 100 : 0;

        return Ok(new
        {
            productId = product.Id,
            productName = product.Name,
            salePrice = product.Price,
            totalCost,
            estimatedMarginPercentage = Math.Round(margin, 2),
            items
        });
    }

    [HttpPost("product/{productId:int}")]
    public async Task<IActionResult> SaveProductRecipe(int productId, [FromBody] SaveRecipeRequest request)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId);
        if (product == null) return NotFound("Producto no encontrado.");

        var existingItems = await _db.RecipeItems.Where(r => r.ProductId == productId).ToListAsync();
        _db.RecipeItems.RemoveRange(existingItems);

        if (request.Items != null && request.Items.Count > 0)
        {
            foreach (var item in request.Items)
            {
                if (item.Quantity <= 0) continue;
                var supplyExists = await _db.Supplies.AnyAsync(s => s.Id == item.SupplyId && s.IsActive);
                if (!supplyExists) return BadRequest($"Insumo ID {item.SupplyId} inválido.");

                _db.RecipeItems.Add(new RecipeItem
                {
                    ProductId = productId,
                    SupplyId = item.SupplyId,
                    Quantity = item.Quantity
                });
            }
        }

        await _db.SaveChangesAsync();
        await _audit.LogAsync("save_recipe", "Recipe", productId.ToString(), new { itemCount = request.Items?.Count ?? 0 });

        return await GetProductRecipe(productId);
    }
}

public class SaveRecipeRequest
{
    public List<RecipeItemDto> Items { get; set; } = new();
}

public class RecipeItemDto
{
    public int SupplyId { get; set; }
    public decimal Quantity { get; set; }
}
