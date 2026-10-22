using System.Security.Claims;
using EDOC.API.Data;
using EDOC.API.DTOs;
using EDOC.API.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EDOC.API.Controllers;

[ApiController, Authorize, Route("api/appointments")]
public sealed class AppointmentsController(EdocDbContext db, ILogger<AppointmentsController> logger) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? "";

    [HttpPost, Authorize(Roles = "Patient")]
    public async Task<ActionResult<AppointmentDto>> Book(BookAppointmentRequest request, CancellationToken ct)
    {
        var patientId = UserId;
        if (request.StartsAtUtc.Offset != TimeSpan.Zero || request.StartsAtUtc <= DateTimeOffset.UtcNow) return BadRequest(new { error = "Choose a future UTC appointment time." });
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var prior = await db.Appointments.Include(a => a.Doctor).ThenInclude(d => d.User).FirstOrDefaultAsync(a => a.PatientUserId == patientId && a.IdempotencyKey == request.IdempotencyKey, ct);
            if (prior is not null)
            {
                if (prior.DoctorProfileId != request.DoctorProfileId || prior.StartsAtUtc != request.StartsAtUtc || prior.VisitReason != request.VisitReason.Trim())
                    return Conflict(new { error = "This idempotency key was already used for a different booking." });
                return Ok(ToDto(prior, prior.Doctor.User.DisplayName));
            }
        }
        var doctor = await db.DoctorProfiles.Include(d => d.User).FirstOrDefaultAsync(d => d.Id == request.DoctorProfileId && d.IsActive, ct);
        if (doctor is null) return NotFound(new { error = "Doctor not found or inactive." });
        if (doctor.AppointmentDurationMinutes is < 5 or > 240) return Conflict(new { error = "The doctor's appointment duration is not configured correctly." });
        var end = request.StartsAtUtc.AddMinutes(doctor.AppointmentDurationMinutes);
        var date = DateOnly.FromDateTime(request.StartsAtUtc.UtcDateTime);
        var time = TimeOnly.FromDateTime(request.StartsAtUtc.UtcDateTime);
        var schedules = await db.DoctorSchedules.Where(s => s.DoctorProfileId == doctor.Id && s.DayOfWeek == date.DayOfWeek && s.IsActive && s.EffectiveFrom <= date && (s.EffectiveTo == null || s.EffectiveTo >= date) && s.StartTime <= time && s.EndTime >= time.AddMinutes(doctor.AppointmentDurationMinutes)).ToListAsync(ct);
        var durationTicks = TimeSpan.FromMinutes(doctor.AppointmentDurationMinutes).Ticks;
        var isPublishedSlot = schedules.Any(s => (time.ToTimeSpan() - s.StartTime.ToTimeSpan()).Ticks % durationTicks == 0);
        if (!isPublishedSlot) return Conflict(new { error = "The requested time is outside the doctor's published appointment slots." });
        var clash = await db.Appointments.AnyAsync(a => a.DoctorProfileId == doctor.Id && a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.Rejected && a.StartsAtUtc < end && a.EndsAtUtc > request.StartsAtUtc, ct);
        if (clash) return Conflict(new { error = "This appointment slot is no longer available." });
        var patientClash = await db.Appointments.AnyAsync(a => a.PatientUserId == patientId && a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.Rejected && a.StartsAtUtc < end && a.EndsAtUtc > request.StartsAtUtc, ct);
        if (patientClash) return Conflict(new { error = "You already have an overlapping appointment." });
        var appointment = new Appointment { PatientUserId = patientId, DoctorProfileId = doctor.Id, StartsAtUtc = request.StartsAtUtc, EndsAtUtc = end, VisitReason = request.VisitReason.Trim(), IdempotencyKey = request.IdempotencyKey };
        db.Appointments.Add(appointment);
        db.AuditLogs.Add(new AuditLog { ActorUserId = patientId, Action = "AppointmentBooked", EntityType = "Appointment", EntityId = appointment.BookingReference.ToString() });
        db.Notifications.Add(new Notification { RecipientUserId = doctor.UserId, Title = "Appointment request", Message = $"New request for {appointment.StartsAtUtc:yyyy-MM-dd HH:mm} UTC." });
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
        catch (DbUpdateException ex) { logger.LogInformation(ex, "Concurrent appointment booking conflict"); return Conflict(new { error = "The selected slot was booked by another request. Please choose another slot." }); }
        return CreatedAtAction(nameof(History), new { id = appointment.Id }, ToDto(appointment, doctor.User.DisplayName));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AppointmentDto>>> History(CancellationToken ct)
    {
        IQueryable<Appointment> query = db.Appointments.AsNoTracking().Include(a => a.Doctor).ThenInclude(d => d.User);
        if (User.IsInRole("Patient")) query = query.Where(a => a.PatientUserId == UserId);
        else if (User.IsInRole("Doctor")) query = query.Where(a => a.Doctor.UserId == UserId);
        else if (!User.IsInRole("Administrator")) return Forbid();
        var items = await query.OrderByDescending(a => a.StartsAtUtc).Take(100).ToListAsync(ct);
        return Ok(items.Select(a => ToDto(a, a.Doctor.User.DisplayName)).ToArray());
    }

    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, CancellationToken ct)
    {
        var appointment = await db.Appointments.Include(a => a.Doctor).FirstOrDefaultAsync(a => a.Id == id, ct);
        if (appointment is null) return NotFound();
        if (User.IsInRole("Patient") && appointment.PatientUserId != UserId) return Forbid();
        if (User.IsInRole("Doctor") && appointment.Doctor.UserId != UserId) return Forbid();
        if (!User.IsInRole("Patient") && !User.IsInRole("Doctor") && !User.IsInRole("Administrator")) return Forbid();
        if (appointment.Status is not (AppointmentStatus.Pending or AppointmentStatus.Confirmed)) return Conflict(new { error = "This appointment cannot be cancelled in its current status." });
        if (appointment.StartsAtUtc <= DateTimeOffset.UtcNow.AddHours(24)) return Conflict(new { error = "Cancellation is available until 24 hours before the appointment." });
        appointment.Status = AppointmentStatus.Cancelled; appointment.UpdatedAt = DateTimeOffset.UtcNow;
        db.AuditLogs.Add(new AuditLog { ActorUserId = UserId, Action = "AppointmentCancelled", EntityType = "Appointment", EntityId = id.ToString() });
        await db.SaveChangesAsync(ct); return NoContent();
    }

    [HttpPatch("{id:long}/status"), Authorize(Roles = "Doctor,Administrator")]
    public async Task<IActionResult> SetStatus(long id, StatusRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<AppointmentStatus>(request.Status, true, out var status) || status == AppointmentStatus.Cancelled) return BadRequest(new { error = "Unsupported appointment status." });
        var appt = await db.Appointments.Include(a => a.Doctor).FirstOrDefaultAsync(a => a.Id == id, ct);
        if (appt is null) return NotFound();
        if (User.IsInRole("Doctor") && appt.Doctor.UserId != UserId) return Forbid();
        var allowed = appt.Status switch { AppointmentStatus.Pending => status is AppointmentStatus.Confirmed or AppointmentStatus.Rejected, AppointmentStatus.Confirmed => status == AppointmentStatus.Completed, _ => false };
        if (!allowed) return Conflict(new { error = "Invalid appointment status transition." });
        appt.Status = status; appt.UpdatedAt = DateTimeOffset.UtcNow;
        db.AuditLogs.Add(new AuditLog { ActorUserId = UserId, Action = "AppointmentStatusChanged:" + status, EntityType = "Appointment", EntityId = id.ToString() });
        db.Notifications.Add(new Notification { RecipientUserId = appt.PatientUserId, Title = "Appointment updated", Message = $"Your appointment status is now {status}." });
        await db.SaveChangesAsync(ct); return NoContent();
    }
    private static AppointmentDto ToDto(Appointment a, string? doctorName = null) => new(a.Id, a.BookingReference, a.DoctorProfileId, doctorName ?? "", a.StartsAtUtc, a.EndsAtUtc, a.VisitReason, a.Status.ToString());
}
