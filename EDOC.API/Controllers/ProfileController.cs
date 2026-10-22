using System.Security.Claims;
using EDOC.API.Data;
using EDOC.API.DTOs;
using EDOC.API.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EDOC.API.Controllers;

[ApiController, Authorize(Roles = "Patient"), Route("api/profile")]
public sealed class ProfileController(EdocDbContext db) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? "";
    [HttpGet("me")]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await db.Users.AsNoTracking().Where(u => u.Id == UserId).Select(u => new { u.Id, u.Email, u.DisplayName, u.PreferredLanguage, Phone = u.PatientProfile!.Phone, u.CreatedAt }).FirstOrDefaultAsync(ct));

    [HttpGet("measurements")]
    public async Task<IActionResult> Measurements(CancellationToken ct) => Ok(await db.HealthMeasurements.AsNoTracking().Where(m => m.PatientUserId == UserId).OrderByDescending(m => m.MeasuredAt).Take(200).ToListAsync(ct));
    [HttpPost("measurements")]
    public async Task<IActionResult> AddMeasurement(MeasurementRequest request, CancellationToken ct)
    {
        var allowed = new[] { "Weight", "Height", "BloodPressureSystolic", "BloodPressureDiastolic", "HeartRate", "Temperature", "BloodGlucose" };
        if (!allowed.Contains(request.Type)) return BadRequest(new { error = "Unsupported measurement type." });
        if (request.MeasuredAt > DateTimeOffset.UtcNow) return BadRequest(new { error = "Measurement time cannot be in the future." });
        var item = new HealthMeasurement { PatientUserId = UserId, Type = request.Type, Value = request.Value, Unit = request.Unit, MeasuredAt = request.MeasuredAt, Notes = request.Notes };
        db.HealthMeasurements.Add(item); await db.SaveChangesAsync(ct); return Created($"/api/profile/measurements/{item.Id}", item);
    }
    [HttpGet("records")]
    public async Task<IActionResult> Records(CancellationToken ct) => Ok(await db.MedicalRecords.AsNoTracking().Where(r => r.PatientUserId == UserId).OrderByDescending(r => r.CreatedAt).ToListAsync(ct));
}
