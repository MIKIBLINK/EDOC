using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using EDOC.API.Data;
using EDOC.API.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EDOC.API.Controllers;

[ApiController, Authorize(Roles = "Administrator"), Route("api/admin")]
public sealed class AdminController(EdocDbContext db, UserManager<EdocUser> users) : ControllerBase
{
    private string Actor => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? "";
    public sealed record AssignRoleRequest([Required, EmailAddress] string Email, [Required] string Role);
    public sealed record DoctorRequest([Required, EmailAddress] string Email, [Required, StringLength(80)] string Specialty, [Required, StringLength(250)] string Qualifications, [Range(0, 100000)] decimal Fee, [StringLength(160)] string? Hospital, [Range(5, 240)] int AppointmentDurationMinutes = 30);
    public sealed record ScheduleRequest(int DoctorProfileId, DayOfWeek DayOfWeek, TimeOnly StartTimeUtc, TimeOnly EndTimeUtc, DateOnly EffectiveFrom, DateOnly? EffectiveTo);
    public sealed record HospitalRequest([Required, StringLength(160)] string Name, [StringLength(400)] string? Address, [Phone, StringLength(50)] string? Phone);

    [HttpPost("roles")]
    public async Task<IActionResult> AssignRole(AssignRoleRequest request, CancellationToken ct)
    {
        if (request.Role is not ("Patient" or "Doctor" or "Administrator")) return BadRequest(new { error = "Unknown role." });
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null) return NotFound(new { error = "User not found." });
        if (!await users.IsInRoleAsync(user, request.Role)) await users.AddToRoleAsync(user, request.Role);
        if (request.Role == "Patient" && !await db.PatientProfiles.AnyAsync(p => p.UserId == user.Id, ct)) db.PatientProfiles.Add(new PatientProfile { UserId = user.Id });
        db.AuditLogs.Add(new AuditLog { ActorUserId = Actor, Action = "RoleAssigned:" + request.Role, EntityType = "User", EntityId = user.Id });
        await db.SaveChangesAsync(ct); return NoContent();
    }

    [HttpPost("doctors")]
    public async Task<IActionResult> CreateDoctor(DoctorRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null) return NotFound(new { error = "Create the account first, then assign it the Doctor role." });
        if (!await users.IsInRoleAsync(user, "Doctor")) return BadRequest(new { error = "Assign the Doctor role first." });
        if (await db.DoctorProfiles.AnyAsync(d => d.UserId == user.Id, ct)) return Conflict(new { error = "A doctor profile already exists for this account." });
        var doctor = new DoctorProfile { UserId = user.Id, Specialty = request.Specialty.Trim(), Qualifications = request.Qualifications.Trim(), ConsultationFee = request.Fee, HospitalName = request.Hospital, AppointmentDurationMinutes = request.AppointmentDurationMinutes };
        db.DoctorProfiles.Add(doctor); db.AuditLogs.Add(new AuditLog { ActorUserId = Actor, Action = "DoctorCreated", EntityType = "DoctorProfile", EntityId = user.Id });
        await db.SaveChangesAsync(ct); return Created($"/api/doctors/{doctor.Id}", new { doctor.Id, user.DisplayName, doctor.Specialty });
    }

    [HttpPost("schedules")]
    public async Task<IActionResult> AddSchedule(ScheduleRequest request, CancellationToken ct)
    {
        if (request.StartTimeUtc >= request.EndTimeUtc || request.EffectiveTo < request.EffectiveFrom) return BadRequest(new { error = "Schedule dates and times are invalid." });
        if (!await db.DoctorProfiles.AnyAsync(d => d.Id == request.DoctorProfileId && d.IsActive, ct)) return NotFound(new { error = "Doctor not found." });
        var schedule = new DoctorSchedule { DoctorProfileId = request.DoctorProfileId, DayOfWeek = request.DayOfWeek, StartTime = request.StartTimeUtc, EndTime = request.EndTimeUtc, EffectiveFrom = request.EffectiveFrom, EffectiveTo = request.EffectiveTo };
        db.DoctorSchedules.Add(schedule); await db.SaveChangesAsync(ct);
        db.AuditLogs.Add(new AuditLog { ActorUserId = Actor, Action = "DoctorScheduleCreated", EntityType = "DoctorSchedule", EntityId = schedule.Id.ToString() });
        await db.SaveChangesAsync(ct); return Created($"/api/admin/schedules/{schedule.Id}", schedule);
    }

    [HttpPost("facilities")]
    public async Task<IActionResult> AddHospital(HospitalRequest request, CancellationToken ct)
    {
        if (await db.Hospitals.AnyAsync(h => h.Name == request.Name, ct)) return Conflict(new { error = "A facility with this name already exists." });
        var hospital = new Hospital { Name = request.Name.Trim(), Address = request.Address?.Trim(), Phone = request.Phone?.Trim() };
        db.Hospitals.Add(hospital); db.AuditLogs.Add(new AuditLog { ActorUserId = Actor, Action = "FacilityCreated", EntityType = "Hospital", EntityId = hospital.Name });
        await db.SaveChangesAsync(ct); return Created("/api/facilities", hospital);
    }

    [HttpGet("reports/summary")]
    public async Task<IActionResult> Summary(CancellationToken ct) => Ok(new
    {
        patients = await db.UserRoles.Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name }).CountAsync(x => x.Name == "Patient", ct),
        activeDoctors = await db.DoctorProfiles.CountAsync(d => d.IsActive, ct),
        facilities = await db.Hospitals.CountAsync(h => h.IsActive, ct),
        appointmentsToday = await db.Appointments.CountAsync(a => a.StartsAtUtc >= DateTimeOffset.UtcNow.Date && a.StartsAtUtc < DateTimeOffset.UtcNow.Date.AddDays(1), ct),
        pendingAppointments = await db.Appointments.CountAsync(a => a.Status == AppointmentStatus.Pending, ct),
        completedAppointments = await db.Appointments.CountAsync(a => a.Status == AppointmentStatus.Completed, ct),
        cancelledAppointments = await db.Appointments.CountAsync(a => a.Status == AppointmentStatus.Cancelled, ct)
    });
}
