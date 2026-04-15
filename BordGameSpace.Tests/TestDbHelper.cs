using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using BordGameSpace.Data;
using BordGameSpace.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace BordGameSpace.Tests;

public class TestDbHelper
{
    public static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        SeedTestData(db);
        return db;
    }

    private static void SeedTestData(AppDbContext db)
    {
        // Note: Admin is already seeded by AppDbContext.OnModelCreating via EnsureCreated()
        // Only add additional test data here

        // Add default level
        db.Levels.Add(new Level
        {
            Name = "非會員",
            UpgradeThresholdHours = 0,
            UpgradeThresholdAmount = 0,
            GameDiscount = 1.00m,
            WeekdayHourlyRate = 60,
            HolidayHourlyRate = 70,
            SortOrder = 0,
            IsDefault = true,
            IsDeletable = false,
            CreatedAt = new DateTime(2024, 1, 1)
        });

        db.Levels.Add(new Level
        {
            Name = "會員",
            UpgradeThresholdHours = 1000,
            UpgradeThresholdAmount = 100000,
            GameDiscount = 0.90m,
            WeekdayHourlyRate = 50,
            HolidayHourlyRate = 60,
            SortOrder = 1,
            IsDefault = false,
            IsDeletable = true,
            CreatedAt = new DateTime(2024, 1, 1)
        });

        db.SaveChanges();
    }
}
