using Microsoft.AspNetCore.Identity;

namespace EDOC.API.Entities;

public sealed class EdocUser : IdentityUser
{
    public string DisplayName { get; set; } = "";
    public string PreferredLanguage { get; set; } = "en";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public PatientProfile? PatientProfile { get; set; }
    public DoctorProfile? DoctorProfile { get; set; }
}
public sealed class PatientProfile
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public EdocUser User { get; set; } = null!;
    public DateOnly? DateOfBirth { get; set; }
    public string? Phone { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class DoctorProfile
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public EdocUser User { get; set; } = null!;
    public string Specialty { get; set; } = "General Practice";
    public string Qualifications { get; set; } = "";
    public string? Languages { get; set; }
    public decimal ConsultationFee { get; set; }
    public string? HospitalName { get; set; }
    public int AppointmentDurationMinutes { get; set; } = 30;
    public bool IsActive { get; set; } = true;
}
public sealed class DoctorSchedule
{
    public int Id { get; set; }
    public int DoctorProfileId { get; set; }
    public DoctorProfile Doctor { get; set; } = null!;
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
}
public enum AppointmentStatus { Pending, Confirmed, Rejected, Completed, Cancelled }
public sealed class Appointment
{
    public long Id { get; set; }
    public Guid BookingReference { get; set; } = Guid.NewGuid();
    public string PatientUserId { get; set; } = "";
    public EdocUser Patient { get; set; } = null!;
    public int DoctorProfileId { get; set; }
    public DoctorProfile Doctor { get; set; } = null!;
    public DateTimeOffset StartsAtUtc { get; set; }
    public DateTimeOffset EndsAtUtc { get; set; }
    public string VisitReason { get; set; } = "";
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;
    public string? IdempotencyKey { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class Hospital { public int Id { get; set; } public string Name { get; set; } = ""; public string? Address { get; set; } public string? Phone { get; set; } public bool IsActive { get; set; } = true; }
public sealed class Department { public int Id { get; set; } public string Name { get; set; } = ""; public int? HospitalId { get; set; } public Hospital? Hospital { get; set; } }
public sealed class Specialty { public int Id { get; set; } public string Name { get; set; } = ""; }
public sealed class MedicalRecord { public long Id { get; set; } public string PatientUserId { get; set; } = ""; public string ProviderUserId { get; set; } = ""; public long? AppointmentId { get; set; } public string Summary { get; set; } = ""; public string? ClinicalNotes { get; set; } public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; }
public sealed class Allergy { public int Id { get; set; } public string PatientUserId { get; set; } = ""; public string Substance { get; set; } = ""; public string? Reaction { get; set; } public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; }
public sealed class HealthMeasurement { public long Id { get; set; } public string PatientUserId { get; set; } = ""; public string Type { get; set; } = ""; public decimal Value { get; set; } public string Unit { get; set; } = ""; public DateTimeOffset MeasuredAt { get; set; } public string? Notes { get; set; } }
public sealed class MedicationSchedule { public long Id { get; set; } public string PatientUserId { get; set; } = ""; public string MedicationName { get; set; } = ""; public string Instructions { get; set; } = ""; public DateOnly StartDate { get; set; } public DateOnly? EndDate { get; set; } public string ReminderTimes { get; set; } = "[]"; public bool IsActive { get; set; } = true; }
public sealed class Notification { public long Id { get; set; } public string RecipientUserId { get; set; } = ""; public string Title { get; set; } = ""; public string Message { get; set; } = ""; public bool IsRead { get; set; } public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; }
public sealed class EmergencyContact { public int Id { get; set; } public string? OwnerUserId { get; set; } public string Name { get; set; } = ""; public string Phone { get; set; } = ""; public string Relationship { get; set; } = ""; public bool IsOfficial { get; set; } }
public sealed class AuditLog { public long Id { get; set; } public string? ActorUserId { get; set; } public string Action { get; set; } = ""; public string EntityType { get; set; } = ""; public string EntityId { get; set; } = ""; public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; }
