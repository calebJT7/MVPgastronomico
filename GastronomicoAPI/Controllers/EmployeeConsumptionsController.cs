using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RotiseriaAPI.Data;
using RotiseriaAPI.Middleware;

namespace RotiseriaAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class EmployeeConsumptionsController : ControllerBase
{
    private readonly AppDbContext _context;
    public EmployeeConsumptionsController(AppDbContext context) => _context = context;

    [HttpGet]
    [Authorize(Roles = $"{UserRoleNames.Owner},{UserRoleNames.Admin},{UserRoleNames.Manager},{UserRoleNames.Tester}")]
    public async Task<IActionResult> GetConsumptions() =>
        Ok(await _context.EmployeeConsumptions.AsNoTracking().OrderByDescending(c => c.Date).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> PostConsumption(Models.EmployeeConsumption consumption)
    {
        consumption.Id = 0;
        _context.EmployeeConsumptions.Add(consumption);
        await _context.SaveChangesAsync();
        return Ok(consumption);
    }
}
