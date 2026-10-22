using EDOC.Core.Abstractions;

namespace EDOC.Core.Models;

public sealed class AppointmentPolicy : IAppointmentPolicy
{
    public bool IsValidSlot(DateTimeOffset startUtc, DateTimeOffset nowUtc, TimeOnly workStartUtc, TimeOnly workEndUtc, int durationMinutes)
    {
        if (startUtc.Offset != TimeSpan.Zero || startUtc <= nowUtc || durationMinutes is < 5 or > 240) return false;
        var start = TimeOnly.FromDateTime(startUtc.UtcDateTime);
        return start >= workStartUtc && start.AddMinutes(durationMinutes) <= workEndUtc;
    }
    public bool Overlaps(DateTimeOffset requestedStart, DateTimeOffset requestedEnd, DateTimeOffset existingStart, DateTimeOffset existingEnd) => requestedStart < existingEnd && requestedEnd > existingStart;
}
