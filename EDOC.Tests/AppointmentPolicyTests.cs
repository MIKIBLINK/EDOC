using EDOC.Core.Models;

namespace EDOC.Tests;

public sealed class AppointmentPolicyTests
{
    private readonly AppointmentPolicy _policy = new();
    [Fact]
    public void RejectsPastAndNonUtcAppointments()
    {
        var now = DateTimeOffset.Parse("2030-01-01T08:00:00Z");
        Assert.False(_policy.IsValidSlot(now.AddMinutes(-1), now, new TimeOnly(8, 0), new TimeOnly(17, 0), 30));
        Assert.False(_policy.IsValidSlot(now.AddHours(1).ToOffset(TimeSpan.FromHours(7)), now, new TimeOnly(8, 0), new TimeOnly(17, 0), 30));
    }
    [Fact]
    public void RequiresEntireAppointmentInsideWorkingHours()
    {
        var now = DateTimeOffset.Parse("2030-01-01T08:00:00Z");
        Assert.True(_policy.IsValidSlot(DateTimeOffset.Parse("2030-01-02T16:30:00Z"), now, new TimeOnly(8, 0), new TimeOnly(17, 0), 30));
        Assert.False(_policy.IsValidSlot(DateTimeOffset.Parse("2030-01-02T16:45:00Z"), now, new TimeOnly(8, 0), new TimeOnly(17, 0), 30));
    }
    [Fact]
    public void DetectsTimeOverlapAndPermitsAdjacentAppointments()
    {
        var a = DateTimeOffset.Parse("2030-01-01T10:00:00Z");
        Assert.True(_policy.Overlaps(a, a.AddMinutes(30), a.AddMinutes(15), a.AddMinutes(45)));
        Assert.False(_policy.Overlaps(a, a.AddMinutes(30), a.AddMinutes(30), a.AddMinutes(60)));
    }

    [Fact]
    public void RejectsUnreasonableAppointmentDurations()
    {
        var now = DateTimeOffset.Parse("2030-01-01T08:00:00Z");
        var start = DateTimeOffset.Parse("2030-01-02T09:00:00Z");
        Assert.False(_policy.IsValidSlot(start, now, new TimeOnly(8, 0), new TimeOnly(17, 0), 0));
        Assert.False(_policy.IsValidSlot(start, now, new TimeOnly(8, 0), new TimeOnly(17, 0), 300));
    }
}
