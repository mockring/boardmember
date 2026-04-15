using Xunit;
using BordGameSpace.Services;
using BordGameSpace.Models;
using BordGameSpace.Data;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace BordGameSpace.Tests;

public class AdminServiceTests
{
    private AdminService CreateService(AppDbContext db)
    {
        var logger = new Mock<ILogger<AdminService>>();
        return new AdminService(db, logger.Object);
    }

    [Fact]
    public async Task LoginAsync_WithCorrectCredentials_ReturnsSuccess()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        // Act
        var (success, message, admin) = await service.LoginAsync("admin", "admin123");

        // Assert
        Assert.True(success);
        Assert.NotNull(admin);
        Assert.Equal("admin", admin.Username);
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ReturnsFailure()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        // Act
        var (success, message, admin) = await service.LoginAsync("admin", "wrongpassword");

        // Assert
        Assert.False(success);
        Assert.Contains("密碼錯誤", message);
    }

    [Fact]
    public async Task LoginAsync_WithInactiveAdmin_ReturnsFailure()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();

        // There should be exactly one admin from AppDbContext seed
        var admin = await db.Admins.FirstAsync();
        admin.IsActive = false;
        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Act
        var (success, message, _) = await service.LoginAsync("admin", "admin123");

        // Assert
        Assert.False(success);
        Assert.Contains("帳號不存在或已被停用", message);
    }

    [Fact]
    public async Task CreateLevelAsync_WithValidData_ReturnsSuccess()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var level = new Level
        {
            Name = "金牌會員",
            UpgradeThresholdHours = 5000,
            UpgradeThresholdAmount = 500000,
            GameDiscount = 0.80m,
            WeekdayHourlyRate = 40,
            HolidayHourlyRate = 50,
            SortOrder = 2
        };

        // Act
        var (success, message) = await service.CreateLevelAsync(level);

        // Assert
        Assert.True(success);

        var created = await db.Levels.FirstOrDefaultAsync(l => l.Name == "金牌會員");
        Assert.NotNull(created);
        Assert.Equal(5000, created.UpgradeThresholdHours);
    }

    [Fact]
    public async Task CreateLevelAsync_WithDuplicateName_ReturnsFailure()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var level = new Level { Name = "會員", SortOrder = 2 };

        // Act
        var (success, message) = await service.CreateLevelAsync(level);

        // Assert
        Assert.False(success);
        Assert.Contains("等級名稱已存在", message);
    }

    [Fact]
    public async Task CreateLevelAsync_SetAsDefault_UnsetsOtherDefaults()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        // "非會員" is already default
        var newLevel = new Level
        {
            Name = "新預設等級",
            IsDefault = true,
            SortOrder = 10
        };

        // Act
        await service.CreateLevelAsync(newLevel);

        // Assert
        var oldDefault = await db.Levels.FindAsync(1);
        Assert.False(oldDefault!.IsDefault);
    }

    [Fact]
    public async Task DeleteLevelAsync_WithDeletableLevel_ReturnsSuccess()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        // Add a deletable level
        var level = new Level { Name = "測試等級", IsDeletable = true, SortOrder = 99 };
        db.Levels.Add(level);
        await db.SaveChangesAsync();

        // Act
        var (success, message) = await service.DeleteLevelAsync(level.Id);

        // Assert
        Assert.True(success);
        Assert.Null(await db.Levels.FindAsync(level.Id));
    }

    [Fact]
    public async Task DeleteLevelAsync_WithNonDeletableLevel_ReturnsFailure()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        // Level "非會員" is not deletable - get its ID
        var nonMemberLevel = await db.Levels.FirstAsync(l => l.Name == "非會員");

        // Act
        var (success, message) = await service.DeleteLevelAsync(nonMemberLevel.Id);

        // Assert
        Assert.False(success);
        Assert.Contains("系統預設等級", message);
    }

    [Fact]
    public async Task DeleteLevelAsync_WithMembersUsingIt_ReturnsFailure()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var memberLevel = await db.Levels.FirstAsync(l => l.Name == "會員");

        // Add a member with member level
        db.Members.Add(new Member
        {
            Name = "測試會員",
            Phone = "0912345678",
            Email = "test@example.com",
            PasswordHash = "hash",
            LevelId = memberLevel.Id
        });
        await db.SaveChangesAsync();

        // Act
        var (success, message) = await service.DeleteLevelAsync(memberLevel.Id);

        // Assert
        Assert.False(success);
        Assert.Contains("有此會員使用此等級", message);
    }

    [Fact]
    public async Task CreateMemberAsync_WithValidData_ReturnsSuccess()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var member = new Member
        {
            Name = "新會員",
            Phone = "0912345678",
            Email = "new@example.com"
        };

        // Act
        var (success, message) = await service.CreateMemberAsync(member);

        // Assert
        Assert.True(success);
        Assert.Contains("預設密碼為電話後6碼", message);

        var created = await db.Members.FirstOrDefaultAsync(m => m.Phone == "0912345678");
        Assert.NotNull(created);
        Assert.Equal(1, created.LevelId); // Default non-member level
    }

    [Fact]
    public async Task ToggleMemberStatusAsync_ChangesStatus()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        db.Members.Add(new Member
        {
            Name = "測試會員",
            Phone = "0912345678",
            Email = "test@example.com",
            PasswordHash = "hash",
            Status = true,
            LevelId = 1 // 非會員
        });
        await db.SaveChangesAsync();

        var member = await db.Members.FirstAsync();

        // Act
        var (success, message) = await service.ToggleMemberStatusAsync(member.Id);

        // Assert
        Assert.True(success);

        var updated = await db.Members.FindAsync(member.Id);
        Assert.False(updated!.Status);
    }

    [Fact]
    public async Task ResetMemberPasswordAsync_ResetsToPhoneLast6Digits()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        db.Members.Add(new Member
        {
            Name = "測試會員",
            Phone = "0912345678",
            Email = "test@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("oldpassword"),
            LevelId = 1 // 非會員
        });
        await db.SaveChangesAsync();

        var member = await db.Members.FirstAsync();

        // Act
        var (success, message) = await service.ResetMemberPasswordAsync(member.Id);

        // Assert
        Assert.True(success);
        Assert.Contains("345678", message); // Last 6 digits of phone
    }
}
