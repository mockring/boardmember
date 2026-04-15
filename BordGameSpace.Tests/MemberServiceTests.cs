using Xunit;
using BordGameSpace.Services;
using BordGameSpace.Models;
using BordGameSpace.Data;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace BordGameSpace.Tests;

public class MemberServiceTests
{
    private MemberService CreateService(AppDbContext db)
    {
        var logger = new Mock<ILogger<MemberService>>();
        return new MemberService(db, logger.Object);
    }

    [Fact]
    public async Task RegisterAsync_WithValidData_ReturnsSuccess()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        // Act
        var (success, message, member) = await service.RegisterAsync(
            "王小明", "0912345678", "test@example.com", "password123", null);

        // Assert
        Assert.True(success);
        Assert.NotNull(member);
        Assert.Equal("王小明", member.Name);
        Assert.Equal("0912345678", member.Phone);
        Assert.Equal("test@example.com", member.Email);
        Assert.Equal(1, member.LevelId); // Default is "非會員" (Id=1 per seed data)
    }

    [Fact]
    public async Task RegisterAsync_WithDuplicatePhone_ReturnsFailure()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        await service.RegisterAsync("張三", "0912345678", "test1@example.com", "password", null);

        // Act
        var (success, message, member) = await service.RegisterAsync(
            "李四", "0912345678", "test2@example.com", "password", null);

        // Assert
        Assert.False(success);
        Assert.Contains("電話或Email已被註冊", message);
    }

    [Fact]
    public async Task RegisterAsync_WithDuplicateEmail_ReturnsFailure()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        await service.RegisterAsync("張三", "0912345678", "test@example.com", "password", null);

        // Act
        var (success, message, member) = await service.RegisterAsync(
            "李四", "0987654321", "test@example.com", "password", null);

        // Assert
        Assert.False(success);
        Assert.Contains("電話或Email已被註冊", message);
    }

    [Fact]
    public async Task LoginAsync_WithCorrectPassword_ReturnsSuccess()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        await service.RegisterAsync("王小明", "0912345678", "test@example.com", "password123", null);

        // Act
        var (success, message, member) = await service.LoginAsync("0912345678", "password123");

        // Assert
        Assert.True(success);
        Assert.NotNull(member);
        Assert.Equal("王小明", member.Name);
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ReturnsFailure()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        await service.RegisterAsync("王小明", "0912345678", "test@example.com", "password123", null);

        // Act
        var (success, message, member) = await service.LoginAsync("0912345678", "wrongpassword");

        // Assert
        Assert.False(success);
        Assert.Contains("密碼錯誤", message);
    }

    [Fact]
    public async Task LoginAsync_WithInactiveMember_ReturnsFailure()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var (success, _, member) = await service.RegisterAsync(
            "王小明", "0912345678", "test@example.com", "password123", null);
        Assert.True(success);

        // Deactivate member
        member!.Status = false;
        await db.SaveChangesAsync();

        // Act
        var (loginSuccess, message, _) = await service.LoginAsync("0912345678", "password123");

        // Assert
        Assert.False(loginSuccess);
        Assert.Contains("帳號不存在或已被停用", message);
    }

    [Fact]
    public async Task LoginAsync_WithEmailAccount_ReturnsSuccess()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        await service.RegisterAsync("王小明", "0912345678", "test@example.com", "password123", null);

        // Act
        var (success, message, member) = await service.LoginAsync("test@example.com", "password123");

        // Assert
        Assert.True(success);
        Assert.NotNull(member);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithCorrectOldPassword_ReturnsSuccess()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var (_, _, member) = await service.RegisterAsync(
            "王小明", "0912345678", "test@example.com", "oldpassword", null);

        // Act
        var (success, message) = await service.ChangePasswordAsync(
            member!.Id, "oldpassword", "newpassword123");

        // Assert
        Assert.True(success);

        // Verify new password works
        var (loginSuccess, _, _) = await service.LoginAsync("0912345678", "newpassword123");
        Assert.True(loginSuccess);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithWrongOldPassword_ReturnsFailure()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var (_, _, member) = await service.RegisterAsync(
            "王小明", "0912345678", "test@example.com", "oldpassword", null);

        // Act
        var (success, message) = await service.ChangePasswordAsync(
            member!.Id, "wrongpassword", "newpassword123");

        // Assert
        Assert.False(success);
        Assert.Contains("舊密碼錯誤", message);
    }

    [Fact]
    public async Task UpdateProfileAsync_WithValidData_ReturnsSuccess()
    {
        // Arrange
        using var db = TestDbHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var (_, _, member) = await service.RegisterAsync(
            "王小明", "0912345678", "test@example.com", "password", null);

        // Act
        var (success, message) = await service.UpdateProfileAsync(
            member!.Id, "王小明更新", "0922222222", "newemail@example.com", null);

        // Assert
        Assert.True(success);

        var updated = await db.Members.FindAsync(member.Id);
        Assert.Equal("王小明更新", updated!.Name);
        Assert.Equal("0922222222", updated.Phone);
        Assert.Equal("newemail@example.com", updated.Email);
    }
}
