using EDOC.API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EDOC.API.Controllers;

[ApiController, Route("api/facilities")]
public sealed class FacilitiesController(EdocDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? q, CancellationToken ct)
    {
        var query = db.Hospitals.AsNoTracking().Where(h => h.IsActive);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(h => h.Name.Contains(q) || (h.Address != null && h.Address.Contains(q)));
        return Ok(await query.OrderBy(h => h.Name).Take(100).ToListAsync(ct));
    }
}
