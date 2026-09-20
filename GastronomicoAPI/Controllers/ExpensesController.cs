using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RotiseriaAPI.Data;
using RotiseriaAPI.Middleware;

namespace RotiseriaAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ExpensesController : ControllerBase
{
    private readonly AppDbContext _context;
    public ExpensesController(AppDbContext context) => _context = context;

    [HttpGet]
    [Authorize(Roles = $"{UserRoleNames.Owner},{UserRoleNames.Admin},{UserRoleNames.Manager},{UserRoleNames.Tester}")]
    public async Task<IActionResult> GetExpenses() => Ok(await _context.Expenses.AsNoTracking().OrderByDescending(e => e.Date).ToListAsync());

    [HttpPost]
    [Authorize(Roles = $"{UserRoleNames.Owner},{UserRoleNames.Admin},{UserRoleNames.Manager},{UserRoleNames.Tester}")]
    public async Task<IActionResult> PostExpense(Models.Expense expense)
    {
        expense.Id = 0;
        _context.Expenses.Add(expense);
        await _context.SaveChangesAsync();
        return Ok(expense);
    }
}
