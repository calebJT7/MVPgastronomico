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
public class CategoriesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public CategoriesController(AppDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _db.Categories.AsNoTracking().OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync());

    [HttpPost]
    [Authorize(Roles = $"{UserRoleNames.Owner},{UserRoleNames.Admin},{UserRoleNames.Manager},{UserRoleNames.Tester}")]
    public async Task<IActionResult> Create(Category category)
    {
        category.Id = 0;
        category.Name = category.Name.Trim();
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("create_category", "Category", category.Id.ToString());

        var business = await _db.Businesses.FindAsync(category.BusinessId);
        if (business is { OnboardingCompleted: false, OnboardingStep: OnboardingStep.Categories })
        {
            business.OnboardingStep = OnboardingStep.Products;
            await _db.SaveChangesAsync();
        }

        return Ok(category);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = $"{UserRoleNames.Owner},{UserRoleNames.Admin},{UserRoleNames.Manager},{UserRoleNames.Tester}")]
    public async Task<IActionResult> Update(int id, Category incoming)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id);
        if (category == null) return NotFound();
        category.Name = incoming.Name.Trim();
        category.SortOrder = incoming.SortOrder;
        category.TracksStock = incoming.TracksStock;
        category.IsActive = incoming.IsActive;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("update_category", "Category", category.Id.ToString());
        return Ok(category);
    }
}
