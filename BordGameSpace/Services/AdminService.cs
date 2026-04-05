using Microsoft.EntityFrameworkCore;
using BordGameSpace.Data;
using BordGameSpace.Models;

namespace BordGameSpace.Services;

public class AdminService
{
    private readonly AppDbContext _db;
    private readonly ILogger<AdminService> _logger;

    public AdminService(AppDbContext db, ILogger<AdminService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// 驗證管理者登入
    /// </summary>
    public async Task<(bool Success, string Message, Admin? Admin)> LoginAsync(string username, string password)
    {
        var admin = await _db.Admins.FirstOrDefaultAsync(a => a.Username == username && a.IsActive);

        if (admin == null)
            return (false, "帳號不存在或已被停用", null);

        if (!BCrypt.Net.BCrypt.Verify(password, admin.PasswordHash))
            return (false, "密碼錯誤", null);

        _logger.LogInformation("管理者登入: {Username}", username);
        return (true, "登入成功", admin);
    }

    /// <summary>
    /// 取得所有等級
    /// </summary>
    public async Task<List<Level>> GetAllLevelsAsync()
    {
        return await _db.Levels.OrderBy(l => l.SortOrder).ToListAsync();
    }

    /// <summary>
    /// 取得等級ById
    /// </summary>
    public async Task<Level?> GetLevelByIdAsync(int id)
    {
        return await _db.Levels.FindAsync(id);
    }

    /// <summary>
    /// 新增等級
    /// </summary>
    public async Task<(bool Success, string Message)> CreateLevelAsync(Level level)
    {
        if (string.IsNullOrWhiteSpace(level.Name))
            return (false, "等級名稱為必填");

        // 檢查名稱是否重複
        if (await _db.Levels.AnyAsync(l => l.Name == level.Name))
            return (false, "等級名稱已存在");

        // 取得最大 SortOrder
        var maxOrder = await _db.Levels.MaxAsync(l => (int?)l.SortOrder) ?? 0;
        level.SortOrder = maxOrder + 1;

        _db.Levels.Add(level);
        await _db.SaveChangesAsync();

        _logger.LogInformation("新增等級: {Name}", level.Name);
        return (true, "新增成功");
    }

    /// <summary>
    /// 更新等級
    /// </summary>
    public async Task<(bool Success, string Message)> UpdateLevelAsync(Level level)
    {
        var existing = await _db.Levels.FindAsync(level.Id);
        if (existing == null)
            return (false, "找不到該等級");

        // 檢查名稱是否重複（排除自己）
        if (await _db.Levels.AnyAsync(l => l.Name == level.Name && l.Id != level.Id))
            return (false, "等級名稱已存在");

        existing.Name = level.Name;
        existing.UpgradeThresholdHours = level.UpgradeThresholdHours;
        existing.UpgradeThresholdAmount = level.UpgradeThresholdAmount;
        existing.GameDiscount = level.GameDiscount;
        existing.WeekdayHourlyRate = level.WeekdayHourlyRate;
        existing.HolidayHourlyRate = level.HolidayHourlyRate;
        existing.SortOrder = level.SortOrder;

        await _db.SaveChangesAsync();

        _logger.LogInformation("更新等級: {Name}", level.Name);
        return (true, "更新成功");
    }

    /// <summary>
    /// 刪除等級
    /// </summary>
    public async Task<(bool Success, string Message)> DeleteLevelAsync(int id)
    {
        var level = await _db.Levels.FindAsync(id);
        if (level == null)
            return (false, "找不到該等級");

        if (!level.IsDeletable)
            return (false, "此為系統預設等級，無法刪除");

        // 檢查是否有會員使用此等級
        if (await _db.Members.AnyAsync(m => m.LevelId == id))
            return (false, "有此會員使用此等級，無法刪除");

        _db.Levels.Remove(level);
        await _db.SaveChangesAsync();

        _logger.LogInformation("刪除等級: {Name}", level.Name);
        return (true, "刪除成功");
    }

    /// <summary>
    /// 取得所有商品
    /// </summary>
    public async Task<List<Product>> GetAllProductsAsync(string? category = null, bool? isActive = null)
    {
        var query = _db.Products.AsQueryable();

        if (!string.IsNullOrEmpty(category))
            query = query.Where(p => p.Category == category);

        if (isActive.HasValue)
            query = query.Where(p => p.IsActive == isActive.Value);

        return await query.OrderBy(p => p.Category).ThenBy(p => p.Name).ToListAsync();
    }

    /// <summary>
    /// 取得商品ById
    /// </summary>
    public async Task<Product?> GetProductByIdAsync(int id)
    {
        return await _db.Products.FindAsync(id);
    }

    /// <summary>
    /// 新增商品
    /// </summary>
    public async Task<(bool Success, string Message, Product? Product)> CreateProductAsync(Product product)
    {
        if (string.IsNullOrWhiteSpace(product.Name))
            return (false, "商品名稱為必填", null);

        if (product.Price < 0)
            return (false, "售價不可為負數", null);

        product.CreatedAt = DateTime.Now;
        product.UpdatedAt = DateTime.Now;

        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        _logger.LogInformation("新增商品: {Name}", product.Name);
        return (true, "新增成功", product);
    }

    /// <summary>
    /// 更新商品
    /// </summary>
    public async Task<(bool Success, string Message)> UpdateProductAsync(Product product)
    {
        var existing = await _db.Products.FindAsync(product.Id);
        if (existing == null)
            return (false, "找不到該商品");

        if (product.Price < 0)
            return (false, "售價不可為負數");

        existing.Category = product.Category;
        existing.Name = product.Name;
        existing.Description = product.Description;
        existing.Price = product.Price;
        existing.Cost = product.Cost;
        existing.Stock = product.Stock;
        existing.LowStockAlert = product.LowStockAlert;
        existing.ImageUrl = product.ImageUrl;
        existing.IsActive = product.IsActive;
        existing.UpdatedAt = DateTime.Now;

        await _db.SaveChangesAsync();

        _logger.LogInformation("更新商品: {Name}", product.Name);
        return (true, "更新成功");
    }

    /// <summary>
    /// 刪除商品
    /// </summary>
    public async Task<(bool Success, string Message)> DeleteProductAsync(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null)
            return (false, "找不到該商品");

        // 檢查是否有訂單使用
        if (await _db.OrderItems.AnyAsync(oi => oi.ItemType == "Product" && oi.ItemId == id))
            return (false, "有訂單使用此商品，無法刪除");

        _db.Products.Remove(product);
        await _db.SaveChangesAsync();

        _logger.LogInformation("刪除商品: {Name}", product.Name);
        return (true, "刪除成功");
    }

    /// <summary>
    /// 取得商品類別列表
    /// </summary>
    public async Task<List<string>> GetCategoriesAsync()
    {
        return await _db.Products
            .Select(p => p.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();
    }

    /// <summary>
    /// 庫存增減
    /// </summary>
    public async Task<(bool Success, string Message)> UpdateStockAsync(int productId, int quantity)
    {
        var product = await _db.Products.FindAsync(productId);
        if (product == null)
            return (false, "找不到該商品");

        product.Stock += quantity;
        if (product.Stock < 0)
            product.Stock = 0;

        product.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();

        return (true, "庫存更新成功");
    }

    /// <summary>
    /// 取得低庫存商品
    /// </summary>
    public async Task<List<Product>> GetLowStockProductsAsync()
    {
        return await _db.Products
            .Where(p => p.IsActive && p.Stock <= p.LowStockAlert)
            .OrderBy(p => p.Stock)
            .ToListAsync();
    }
}
