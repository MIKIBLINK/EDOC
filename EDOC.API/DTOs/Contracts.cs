using System.ComponentModel.DataAnnotations;

namespace EDOC.API.DTOs;

public sealed record RegisterRequest([Required, EmailAddress] string Email, [Required, MinLength(10)] string Password, [Required, MaxLength(120)] string DisplayName);
public sealed record LoginRequest([Required, EmailAddress] string Email, [Required] string Password);
public sealed record AuthResponse(string AccessToken, string UserId, string DisplayName, string[] Roles);
public sealed record DoctorSummary(int Id, string Name, string Specialty, string Qualifications, decimal Fee, string? Hospital, int AppointmentDurationMinutes);
public sealed record SlotDto(DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc);
public sealed record BookAppointmentRequest([Range(1, int.MaxValue)] int DoctorProfileId, DateTimeOffset StartsAtUtc, [Required, StringLength(1000)] string VisitReason, [StringLength(100)] string? IdempotencyKey);
public sealed record AppointmentDto(long Id, Guid BookingReference, int DoctorProfileId, string DoctorName, DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc, string VisitReason, string Status);
public sealed record StatusRequest([Required] string Status);
public sealed record MeasurementRequest([Required, StringLength(40)] string Type, [Range(0.01, 100000)] decimal Value, [Required, StringLength(30)] string Unit, DateTimeOffset MeasuredAt, [StringLength(500)] string? Notes);
