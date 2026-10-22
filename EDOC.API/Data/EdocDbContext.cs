using EDOC.API.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EDOC.API.Data;

public sealed class EdocDbContext(DbContextOptions<EdocDbContext> options) : IdentityDbContext<EdocUser>(options)
{
    public DbSet<PatientProfile> PatientProfiles => Set<PatientProfile>();
    public DbSet<DoctorProfile> DoctorProfiles => Set<DoctorProfile>();
    public DbSet<DoctorSchedule> DoctorSchedules => Set<DoctorSchedule>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Hospital> Hospitals => Set<Hospital>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Specialty> Specialties => Set<Specialty>();
    public DbSet<MedicalRecord> MedicalRecords => Set<MedicalRecord>();
    public DbSet<Allergy> Allergies => Set<Allergy>();
    public DbSet<HealthMeasurement> HealthMeasurements => Set<HealthMeasurement>();
    public DbSet<MedicationSchedule> MedicationSchedules => Set<MedicationSchedule>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<EmergencyContact> EmergencyContacts => Set<EmergencyContact>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<PatientProfile>().HasIndex(x => x.UserId).IsUnique();
        b.Entity<PatientProfile>().HasOne(x => x.User).WithOne(x => x.PatientProfile).HasForeignKey<PatientProfile>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<DoctorProfile>().HasIndex(x => x.UserId).IsUnique();
        b.Entity<DoctorProfile>().Property(x => x.ConsultationFee).HasPrecision(10,2);
        b.Entity<HealthMeasurement>().Property(x => x.Value).HasPrecision(14,4);
        b.Entity<DoctorProfile>().HasOne(x => x.User).WithOne(x => x.DoctorProfile).HasForeignKey<DoctorProfile>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Appointment>().HasIndex(x => x.BookingReference).IsUnique();
        b.Entity<Appointment>().HasIndex(x => new { x.DoctorProfileId, x.StartsAtUtc, x.Status });
        b.Entity<Appointment>().HasIndex(x => new { x.PatientUserId, x.StartsAtUtc });
        b.Entity<Appointment>().HasIndex(x => new { x.PatientUserId, x.IdempotencyKey }).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
        b.Entity<Appointment>().Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Entity<Appointment>().HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientUserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Appointment>().HasOne(x => x.Doctor).WithMany().HasForeignKey(x => x.DoctorProfileId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<DoctorSchedule>().HasIndex(x => new { x.DoctorProfileId, x.DayOfWeek, x.EffectiveFrom });
        b.Entity<MedicalRecord>().HasIndex(x => new { x.PatientUserId, x.CreatedAt });
        b.Entity<HealthMeasurement>().HasIndex(x => new { x.PatientUserId, x.MeasuredAt });
        b.Entity<Notification>().HasIndex(x => new { x.RecipientUserId, x.IsRead, x.CreatedAt });
        b.Entity<Department>().HasIndex(x => new { x.HospitalId, x.Name }).IsUnique();
        b.Entity<Specialty>().HasIndex(x => x.Name).IsUnique();
        b.Entity<Hospital>().HasIndex(x => x.Name).IsUnique();
    }
}
