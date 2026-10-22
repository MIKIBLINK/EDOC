namespace EDOC.Core.Abstractions;

public interface IAppointmentPolicy
{
    bool IsValidSlot(DateTimeOffset startUtc, DateTimeOffset nowUtc, TimeOnly workStartUtc, TimeOnly workEndUtc, int durationMinutes);
    bool Overlaps(DateTimeOffset requestedStart, DateTimeOffset requestedEnd, DateTimeOffset existingStart, DateTimeOffset existingEnd);
}
