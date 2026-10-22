using EDOC.API.Data;
using EDOC.API.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EDOC.API.Controllers;

[ApiController, Route("api/doctors")]
public sealed class DoctorsController(EdocDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<object>> Search([FromQuery] string? q, [FromQuery] string? specialty, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize, 1, 50);
        var query = db.DoctorProfiles.AsNoTracking().Where(d => d.IsActive);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(d => d.User.DisplayName.Contains(q) || d.Specialty.Contains(q) || (d.HospitalName != null && d.HospitalName.Contains(q)));
        if (!string.IsNullOrWhiteSpace(specialty)) query = query.Where(d => d.Specialty == specialty);
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(d => d.User.DisplayName).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(d => new DoctorSummary(d.Id, d.User.DisplayName, d.Specialty, d.Qualifications, d.ConsultationFee, d.HospitalName, d.AppointmentDurationMinutes)).ToListAsync(ct);
        return Ok(new { items, page, pageSize, total });
    }

    [HttpGet("{doctorId:int}/slots")]
    public async Task<ActionResult<IReadOnlyList<SlotDto>>> Slots(int doctorId, [FromQuery] DateOnly date, CancellationToken ct)
    {
        var doctor = await db.DoctorProfiles.AsNoTracking().FirstOrDefaultAsync(d => d.Id == doctorId && d.IsActive, ct);
        if (doctor is null) return NotFound();
        var schedules = await db.DoctorSchedules.AsNoTracking().Where(s => s.DoctorProfileId == doctorId && s.DayOfWeek == date.DayOfWeek && s.IsActive && s.EffectiveFrom <= date && (s.EffectiveTo == null || s.EffectiveTo >= date)).ToListAsync(ct);
        var dayStart = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);
        var booked = await db.Appointments.AsNoTracking().Where(a => a.DoctorProfileId == doctorId && a.Status != Entities.AppointmentStatus.Cancelled && a.Status != Entities.AppointmentStatus.Rejected && a.StartsAtUtc < dayEnd && a.EndsAtUtc > dayStart).Select(a => new { a.StartsAtUtc, a.EndsAtUtc }).ToListAsync(ct);
        var slots = new List<SlotDto>(); var now = DateTimeOffset.UtcNow;
        foreach (var schedule in schedules)
        {
            // Schedule times are UTC for this initial release; facility time zones are a remaining configuration item.
            var cursor = new DateTimeOffset(date.ToDateTime(schedule.StartTime), TimeSpan.Zero);
            var end = new DateTimeOffset(date.ToDateTime(schedule.EndTime), TimeSpan.Zero);
            while (cursor + TimeSpan.FromMinutes(doctor.AppointmentDurationMinutes) <= end)
            {
                var slotEnd = cursor.AddMinutes(doctor.AppointmentDurationMinutes);
                if (cursor > now && !booked.Any(b => b.StartsAtUtc < slotEnd && b.EndsAtUtc > cursor)) slots.Add(new(cursor, slotEnd));
                cursor = slotEnd;
            }
        }
        return Ok(slots);
    }
}
