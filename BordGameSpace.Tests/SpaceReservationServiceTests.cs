using Xunit;
using BordGameSpace.Services;
using BordGameSpace.Models;
using BordGameSpace.Data;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace BordGameSpace.Tests;

public class SpaceReservationServiceTests
{
    private SpaceReservationService CreateService(AppDbContext db)
    {
        return new SpaceReservationService(db);
    }

    [Theory]
    [InlineData(10, 12, 2)]     // 2 hours
    [InlineData(10, 11, 1)]    // 1 hour (minimum)
    [InlineData(9, 14, 5)]    // 5 hours
    public void CalculateHours_ReturnsCorrectHours(int startHour, int endHour, int expectedHours)
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var start = new TimeSpan(startHour, 0, 0);
        var end = new TimeSpan(endHour, 0, 0);

        // Act
        var hours = service.CalculateHours(start, end);

        // Assert
        Assert.Equal(expectedHours, hours);
    }

    [Theory]
    [InlineData(1, 1, 0, 1)]   // Same time -> minimum 1 hour
    [InlineData(1, 1, 30, 1)] // 30 min -> minimum 1 hour
    public void CalculateHours_WithLessThanOneHour_ReturnsMinimumOneHour(
        int startHour, int endHour, int endMinute, int expected)
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var start = new TimeSpan(startHour, 0, 0);
        var end = new TimeSpan(endHour, endMinute, 0);

        // Act
        var hours = service.CalculateHours(start, end);

        // Assert
        Assert.Equal(expected, hours);
    }

    [Theory]
    [InlineData(2024, 6, 8, true)]    // Saturday - is holiday
    [InlineData(2024, 6, 9, true)]    // Sunday - is holiday
    [InlineData(2024, 6, 10, false)]  // Monday - not holiday
    [InlineData(2024, 6, 11, false)]  // Tuesday - not holiday
    [InlineData(2024, 6, 12, false)]  // Wednesday - not holiday
    [InlineData(2024, 6, 13, false)]  // Thursday - not holiday
    [InlineData(2024, 6, 14, false)]  // Friday - not holiday
    public void IsHoliday_ReturnsCorrectResult(int year, int month, int day, bool expected)
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var date = new DateTime(year, month, day);

        // Act
        var result = service.IsHoliday(date);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task GetHourlyRateAsync_WithMember_ReturnsMemberRate()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var member = new Member
        {
            Name = "王小明",
            Phone = "0912345678",
            Email = "test@example.com",
            PasswordHash = "hash",
            LevelId = 2 // 會員 level: weekday 50, holiday 60
        };
        db.Members.Add(member);
        await db.SaveChangesAsync();

        // Act
        var weekdayRate = await service.GetHourlyRateAsync(member.Id, new DateTime(2024, 6, 10)); // Monday
        var holidayRate = await service.GetHourlyRateAsync(member.Id, new DateTime(2024, 6, 8));  // Saturday

        // Assert
        Assert.Equal(50, weekdayRate);  // Level 2 weekday rate
        Assert.Equal(60, holidayRate);  // Level 2 holiday rate
    }

    [Fact]
    public async Task GetHourlyRateAsync_WithNonMember_ReturnsDefaultRate()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var member = new Member
        {
            Name = "王小明",
            Phone = "0912345678",
            Email = "test@example.com",
            PasswordHash = "hash",
            LevelId = 1 // 非會員
        };
        db.Members.Add(member);
        await db.SaveChangesAsync();

        // Act
        var rate = await service.GetHourlyRateAsync(member.Id, new DateTime(2024, 6, 10));

        // Assert
        Assert.Equal(60, rate); // Default non-member rate
    }

    [Fact]
    public async Task CreateAsync_WithValidData_CreatesReservation()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var member = new Member
        {
            Name = "王小明",
            Phone = "0912345678",
            Email = "test@example.com",
            PasswordHash = "hash",
            LevelId = 2
        };
        db.Members.Add(member);
        await db.SaveChangesAsync();

        var reservationDate = new DateTime(2024, 6, 10); // Monday
        var startTime = new TimeSpan(10, 0, 0);
        var endTime = new TimeSpan(14, 0, 0);

        // Act
        var (success, message, reservation) = await service.CreateAsync(
            member.Id, "王小明", "0912345678",
            reservationDate, startTime, endTime, "測試備註");

        // Assert
        Assert.True(success);
        Assert.NotNull(reservation);
        Assert.Equal("Pending", reservation.Status);
        Assert.Equal(4, reservation.Hours);
        Assert.Equal(50, reservation.HourlyRate); // Member weekday rate
        Assert.Equal(200, reservation.TotalAmount); // 4 * 50
    }

    [Fact]
    public async Task CreateAsync_WithInvalidTimeRange_ReturnsFailure()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var member = new Member
        {
            Name = "王小明",
            Phone = "0912345678",
            Email = "test@example.com",
            PasswordHash = "hash",
            LevelId = 2
        };
        db.Members.Add(member);
        await db.SaveChangesAsync();

        var startTime = new TimeSpan(14, 0, 0);
        var endTime = new TimeSpan(10, 0, 0); // End before start

        // Act
        var (success, message, _) = await service.CreateAsync(
            member.Id, "王小明", "0912345678",
            DateTime.Now, startTime, endTime, null);

        // Assert
        Assert.False(success);
        Assert.Contains("開始時間必須早於結束時間", message);
    }

    [Fact]
    public async Task ReviewAsync_WithApproval_ChangesStatusToApproved()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var reservation = new SpaceReservation
        {
            MemberId = 1,
            Name = "王小明",
            Phone = "0912345678",
            ReservationDate = DateTime.Now,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(12, 0, 0),
            Hours = 2,
            HourlyRate = 60,
            TotalAmount = 120,
            Status = "Pending"
        };
        db.SpaceReservations.Add(reservation);
        await db.SaveChangesAsync();

        // Act
        var (success, message) = await service.ReviewAsync(reservation.Id, true);

        // Assert
        Assert.True(success);
        var updated = await db.SpaceReservations.FindAsync(reservation.Id);
        Assert.Equal("Approved", updated!.Status);
    }

    [Fact]
    public async Task ReviewAsync_WithRejection_ChangesStatusToRejected()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var reservation = new SpaceReservation
        {
            MemberId = 1,
            Name = "王小明",
            Phone = "0912345678",
            ReservationDate = DateTime.Now,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(12, 0, 0),
            Hours = 2,
            HourlyRate = 60,
            TotalAmount = 120,
            Status = "Pending"
        };
        db.SpaceReservations.Add(reservation);
        await db.SaveChangesAsync();

        // Act
        var (success, message) = await service.ReviewAsync(reservation.Id, false);

        // Assert
        Assert.True(success);
        var updated = await db.SpaceReservations.FindAsync(reservation.Id);
        Assert.Equal("Rejected", updated!.Status);
    }

    [Fact]
    public async Task CancelAsync_WithCorrectMember_CancelsReservation()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var member = new Member
        {
            Name = "王小明",
            Phone = "0912345678",
            Email = "test@example.com",
            PasswordHash = "hash",
            LevelId = 2
        };
        db.Members.Add(member);

        var reservation = new SpaceReservation
        {
            MemberId = member.Id,
            Name = "王小明",
            Phone = "0912345678",
            ReservationDate = DateTime.Now,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(12, 0, 0),
            Status = "Pending"
        };
        db.SpaceReservations.Add(reservation);
        await db.SaveChangesAsync();

        // Act
        var (success, message) = await service.CancelAsync(reservation.Id, member.Id);

        // Assert
        Assert.True(success);
        var updated = await db.SpaceReservations.FindAsync(reservation.Id);
        Assert.Equal("Cancelled", updated!.Status);
    }

    [Fact]
    public async Task CancelAsync_WithWrongMember_ReturnsFailure()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var member1 = new Member
        {
            Name = "王小明",
            Phone = "0912345678",
            Email = "test1@example.com",
            PasswordHash = "hash",
            LevelId = 2
        };
        var member2 = new Member
        {
            Name = "李小明",
            Phone = "0987654321",
            Email = "test2@example.com",
            PasswordHash = "hash",
            LevelId = 2
        };
        db.Members.AddRange(member1, member2);

        var reservation = new SpaceReservation
        {
            MemberId = member1.Id,
            Name = "王小明",
            Phone = "0912345678",
            ReservationDate = DateTime.Now,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(12, 0, 0),
            Status = "Pending"
        };
        db.SpaceReservations.Add(reservation);
        await db.SaveChangesAsync();

        // Act - try to cancel as different member
        var (success, message) = await service.CancelAsync(reservation.Id, member2.Id);

        // Assert
        Assert.False(success);
        Assert.Contains("無權限", message);
    }

    [Fact]
    public async Task CancelAsync_WithAlreadyCancelled_ReturnsFailure()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var member = new Member
        {
            Name = "王小明",
            Phone = "0912345678",
            Email = "test@example.com",
            PasswordHash = "hash",
            LevelId = 2
        };
        db.Members.Add(member);

        var reservation = new SpaceReservation
        {
            MemberId = member.Id,
            Name = "王小明",
            Phone = "0912345678",
            ReservationDate = DateTime.Now,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(12, 0, 0),
            Status = "Cancelled"
        };
        db.SpaceReservations.Add(reservation);
        await db.SaveChangesAsync();

        // Act
        var (success, message) = await service.CancelAsync(reservation.Id, member.Id);

        // Assert
        Assert.False(success);
        Assert.Contains("已取消", message);
    }

    [Fact]
    public async Task HasTimeConflictAsync_WithOverlappingTime_ReturnsTrue()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var member = new Member
        {
            Name = "王小明",
            Phone = "0912345678",
            Email = "test@example.com",
            PasswordHash = "hash",
            LevelId = 2
        };
        db.Members.Add(member);

        var existingReservation = new SpaceReservation
        {
            MemberId = member.Id,
            Name = "王小明",
            Phone = "0912345678",
            ReservationDate = new DateTime(2024, 6, 10),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(12, 0, 0),
            Status = "Pending"
        };
        db.SpaceReservations.Add(existingReservation);
        await db.SaveChangesAsync();

        // Act - overlapping: 11:00-13:00 overlaps with 10:00-12:00
        var hasConflict = await service.HasTimeConflictAsync(
            new DateTime(2024, 6, 10),
            new TimeSpan(11, 0, 0),
            new TimeSpan(13, 0, 0));

        // Assert
        Assert.True(hasConflict);
    }

    [Fact]
    public async Task HasTimeConflictAsync_WithNoOverlap_ReturnsFalse()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var member = new Member
        {
            Name = "王小明",
            Phone = "0912345678",
            Email = "test@example.com",
            PasswordHash = "hash",
            LevelId = 2
        };
        db.Members.Add(member);

        var existingReservation = new SpaceReservation
        {
            MemberId = member.Id,
            Name = "王小明",
            Phone = "0912345678",
            ReservationDate = new DateTime(2024, 6, 10),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(12, 0, 0),
            Status = "Pending"
        };
        db.SpaceReservations.Add(existingReservation);
        await db.SaveChangesAsync();

        // Act - no overlap: 13:00-15:00 does not overlap with 10:00-12:00
        var hasConflict = await service.HasTimeConflictAsync(
            new DateTime(2024, 6, 10),
            new TimeSpan(13, 0, 0),
            new TimeSpan(15, 0, 0));

        // Assert
        Assert.False(hasConflict);
    }

    [Fact]
    public async Task HasTimeConflictAsync_WithCancelledReservation_ReturnsFalse()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var member = new Member
        {
            Name = "王小明",
            Phone = "0912345678",
            Email = "test@example.com",
            PasswordHash = "hash",
            LevelId = 2
        };
        db.Members.Add(member);

        var existingReservation = new SpaceReservation
        {
            MemberId = member.Id,
            Name = "王小明",
            Phone = "0912345678",
            ReservationDate = new DateTime(2024, 6, 10),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(12, 0, 0),
            Status = "Cancelled"
        };
        db.SpaceReservations.Add(existingReservation);
        await db.SaveChangesAsync();

        // Act - cancelled reservations should not conflict
        var hasConflict = await service.HasTimeConflictAsync(
            new DateTime(2024, 6, 10),
            new TimeSpan(10, 0, 0),
            new TimeSpan(12, 0, 0));

        // Assert
        Assert.False(hasConflict);
    }

    [Fact]
    public async Task CreateAsync_WithTimeConflict_ReturnsFailure()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var member = new Member
        {
            Name = "王小明",
            Phone = "0912345678",
            Email = "test@example.com",
            PasswordHash = "hash",
            LevelId = 2
        };
        db.Members.Add(member);

        var existingReservation = new SpaceReservation
        {
            MemberId = member.Id,
            Name = "王小明",
            Phone = "0912345678",
            ReservationDate = new DateTime(2024, 6, 10),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(12, 0, 0),
            Status = "Pending"
        };
        db.SpaceReservations.Add(existingReservation);
        await db.SaveChangesAsync();

        // Act - try to create overlapping reservation
        var (success, message, _) = await service.CreateAsync(
            member.Id, "王小明", "0912345678",
            new DateTime(2024, 6, 10),
            new TimeSpan(11, 0, 0),
            new TimeSpan(13, 0, 0),
            null);

        // Assert
        Assert.False(success);
        Assert.Contains("時段已有其他預約", message);
    }
}
