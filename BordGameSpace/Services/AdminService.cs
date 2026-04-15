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

        // 如果設為預設，先取消其他預設
        if (level.IsDefault)
        {
            var currentDefaults = await _db.Levels.Where(l => l.IsDefault).ToListAsync();
            foreach (var l in currentDefaults)
            {
                l.IsDefault = false;
                l.IsDeletable = true; // 取消預設後恢復可刪
            }
            level.IsDeletable = false; // 預設等級不可刪
        }

        // 若未指定 SortOrder，取最大+1
        if (level.SortOrder <= 0)
        {
            var maxOrder = await _db.Levels.MaxAsync(l => (int?)l.SortOrder) ?? 0;
            level.SortOrder = maxOrder + 1;
        }

        _db.Levels.Add(level);
        await _db.SaveChangesAsync();

        _logger.LogInformation("新增等級: {Name}, 預設={IsDefault}", level.Name, level.IsDefault);
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

        // 若設為預設，先取消其他預設
        if (level.IsDefault && !existing.IsDefault)
        {
            var currentDefaults = await _db.Levels.Where(l => l.IsDefault && l.Id != existing.Id).ToListAsync();
            foreach (var l in currentDefaults)
            {
                l.IsDefault = false;
                l.IsDeletable = true;
            }
            existing.IsDefault = true;
            existing.IsDeletable = false;
        }
        else if (!level.IsDefault && existing.IsDefault)
        {
            // 取消預設時，恢復可刪
            existing.IsDefault = false;
            existing.IsDeletable = true;
        }

        // 其他人可修改 IsDeletable
        if (!existing.IsDefault) // 預設等級不可改可刪性
        {
            existing.IsDeletable = level.IsDeletable;
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("更新等級: {Name}", level.Name);
        return (true, "更新成功");
    }

    /// <summary>
    /// 將指定等級設為預設等級
    /// </summary>
    public async Task<(bool Success, string Message)> SetDefaultLevelAsync(int levelId)
    {
        var level = await _db.Levels.FindAsync(levelId);
        if (level == null)
            return (false, "找不到該等級");

        // 取消所有預設
        var currentDefaults = await _db.Levels.Where(l => l.IsDefault).ToListAsync();
        foreach (var l in currentDefaults)
        {
            l.IsDefault = false;
        }

        level.IsDefault = true;
        level.IsDeletable = false; // 預設等級不可刪
        await _db.SaveChangesAsync();

        _logger.LogInformation("設定預設等級: {Name}", level.Name);
        return (true, $"「{level.Name}」已設為預設等級");
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
    public async Task<List<Product>> GetAllProductsAsync(string? category = null, bool? isActive = null, string? search = null)
    {
        var query = _db.Products.AsQueryable();

        if (!string.IsNullOrEmpty(category))
            query = query.Where(p => p.Category == category);

        if (isActive.HasValue)
            query = query.Where(p => p.IsActive == isActive.Value);

        if (!string.IsNullOrEmpty(search))
            query = query.Where(p => p.Name.Contains(search));

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
    /// 取得商品ByName
    /// </summary>
    public async Task<Product?> GetProductByNameAsync(string name)
    {
        return await _db.Products.FirstOrDefaultAsync(p => p.Name == name);
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
        existing.Stock = product.Stock;
        existing.LowStockAlert = product.LowStockAlert;
        existing.ImageUrl = product.ImageUrl;
        existing.IsActive = product.IsActive;
        existing.IsService = product.IsService;
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

        product.Stock = (product.Stock ?? 0) + quantity;
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
            .Where(p => p.IsActive && !p.IsService && p.Stock <= p.LowStockAlert)
            .OrderBy(p => p.Stock)
            .ToListAsync();
    }

    // ==================== 會員管理 ====================

    /// <summary>
    /// 取得會員列表（支援搜尋、等級篩選、狀態篩選）
    /// </summary>
    public async Task<List<Member>> GetAllMembersAsync(string? search = null, int? levelId = null, bool? status = null)
    {
        var query = _db.Members.Include(m => m.Level).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            query = query.Where(m =>
                m.Name.Contains(search) ||
                m.Phone.Contains(search) ||
                m.Email.Contains(search));
        }

        if (levelId.HasValue)
            query = query.Where(m => m.LevelId == levelId.Value);

        if (status.HasValue)
            query = query.Where(m => m.Status == status.Value);

        return await query.OrderByDescending(m => m.CreatedAt).ToListAsync();
    }

    /// <summary>
    /// 取得會員 ById（含等級）
    /// </summary>
    public async Task<Member?> GetMemberByIdAsync(int id)
    {
        return await _db.Members.Include(m => m.Level).FirstOrDefaultAsync(m => m.Id == id);
    }

    /// <summary>
    /// 取得會員 By電話
    /// </summary>
    public async Task<Member?> GetMemberByPhoneAsync(string phone)
    {
        return await _db.Members.FirstOrDefaultAsync(m => m.Phone == phone);
    }

    /// <summary>
    /// 新增會員（後台用）
    /// </summary>
    public async Task<(bool Success, string Message)> CreateMemberAsync(Member model)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
            return (false, "姓名為必填");
        if (string.IsNullOrWhiteSpace(model.Phone))
            return (false, "電話為必填");
        if (string.IsNullOrWhiteSpace(model.Email))
            return (false, "Email為必填");

        // 檢查電話/Email 是否已被使用
        if (await _db.Members.AnyAsync(m => m.Phone == model.Phone))
            return (false, "電話已被註冊");
        if (await _db.Members.AnyAsync(m => m.Email == model.Email))
            return (false, "Email已被註冊");

        // 取得預設等級
        var defaultLevel = await _db.Levels.FirstOrDefaultAsync(l => l.IsDefault);
        if (defaultLevel == null)
            return (false, "系統錯誤：找不到預設等級");

        var member = new Member
        {
            Name = model.Name.Trim(),
            Phone = model.Phone.Trim(),
            Email = model.Email.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Phone.Substring(model.Phone.Length - 6)), // 預設密碼為電話後6碼
            Birthday = model.Birthday,
            LevelId = defaultLevel.Id,
            Status = true,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        _db.Members.Add(member);
        await _db.SaveChangesAsync();

        _logger.LogInformation("後台新增會員: {Name}, {Phone}", member.Name, member.Phone);
        return (true, "會員新增成功（預設密碼為電話後6碼）");
    }

    /// <summary>
    /// 更新會員（後台用）
    /// </summary>
    public async Task<(bool Success, string Message)> UpdateMemberAsync(Member model, bool changePassword = false)
    {
        var member = await _db.Members.FindAsync(model.Id);
        if (member == null)
            return (false, "找不到該會員");

        // 檢查電話/Email 是否已被其他人使用
        if (await _db.Members.AnyAsync(m => m.Id != model.Id && m.Phone == model.Phone))
            return (false, "電話已被其他會員使用");
        if (await _db.Members.AnyAsync(m => m.Id != model.Id && m.Email == model.Email))
            return (false, "Email已被其他會員使用");

        member.Name = model.Name.Trim();
        member.Phone = model.Phone.Trim();
        member.Email = model.Email.Trim();
        member.Birthday = model.Birthday;
        member.LevelId = model.LevelId;
        member.Status = model.Status;
        member.UpdatedAt = DateTime.Now;

        if (changePassword && !string.IsNullOrWhiteSpace(model.PasswordHash))
        {
            member.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.PasswordHash, 12);
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("更新會員: {Name}, Level={LevelId}, Status={Status}",
            member.Name, member.LevelId, member.Status);
        return (true, "更新成功");
    }

    /// <summary>
    /// 手動調整會員等級
    /// </summary>
    public async Task<(bool Success, string Message)> ChangeMemberLevelAsync(int memberId, int newLevelId)
    {
        var member = await _db.Members.FindAsync(memberId);
        if (member == null)
            return (false, "找不到該會員");

        var level = await _db.Levels.FindAsync(newLevelId);
        if (level == null)
            return (false, "找不到該等級");

        var oldLevelId = member.LevelId;
        member.LevelId = newLevelId;
        member.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();

        _logger.LogInformation("手動調整會員 {Name} 等級: {OldLevelId} -> {NewLevelId}",
            member.Name, oldLevelId, newLevelId);
        return (true, $"會員等級已調整為「{level.Name}」");
    }

    /// <summary>
    /// 重設會員密碼
    /// </summary>
    public async Task<(bool Success, string Message)> ResetMemberPasswordAsync(int memberId)
    {
        var member = await _db.Members.FindAsync(memberId);
        if (member == null)
            return (false, "找不到該會員");

        var newPassword = member.Phone.Substring(member.Phone.Length - 6);
        member.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, 12);
        member.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();

        _logger.LogInformation("重設會員密碼: {Name}", member.Name);
        return (true, $"密碼已重設為電話後6碼（{newPassword}）");
    }

    /// <summary>
    /// 切換會員狀態（停用/啟用）
    /// </summary>
    public async Task<(bool Success, string Message)> ToggleMemberStatusAsync(int memberId)
    {
        var member = await _db.Members.FindAsync(memberId);
        if (member == null)
            return (false, "找不到該會員");

        member.Status = !member.Status;
        member.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();

        var action = member.Status ? "啟用" : "停用";
        _logger.LogInformation("會員 {Name} 已被{Action}", member.Name, action);
        return (true, $"會員「{member.Name}」已改為「{(member.Status ? "啟用" : "停用")}」");
    }

    // ========== 訂單管理 ==========

    /// <summary>
    /// 取得所有訂單（可依條件篩選）
    /// </summary>
    public async Task<List<Order>> GetAllOrdersAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? memberSearch = null,
        string? orderType = null,
        string? paymentStatus = null)
    {
        var query = _db.Orders
            .Include(o => o.Member)
            .Include(o => o.OrderItems)
            .AsQueryable();

        if (startDate.HasValue)
            query = query.Where(o => o.CreatedAt.Date >= startDate.Value.Date);

        if (endDate.HasValue)
            query = query.Where(o => o.CreatedAt.Date <= endDate.Value.Date);

        if (!string.IsNullOrWhiteSpace(memberSearch))
            query = query.Where(o =>
                (o.MemberName != null && o.MemberName.Contains(memberSearch)) ||
                (o.MemberPhone != null && o.MemberPhone.Contains(memberSearch)) ||
                (o.Member != null && o.Member.Name != null && o.Member.Name.Contains(memberSearch)));

        if (!string.IsNullOrWhiteSpace(orderType) && orderType != "All")
            query = query.Where(o => o.OrderType == orderType);

        if (!string.IsNullOrWhiteSpace(paymentStatus) && paymentStatus != "All")
            query = query.Where(o => o.PaymentStatus == paymentStatus);

        return await query.OrderByDescending(o => o.CreatedAt).ToListAsync();
    }

    /// <summary>
    /// 依 ID 取得訂單（含明細）
    /// </summary>
    public async Task<Order?> GetOrderByIdAsync(int id)
    {
        return await _db.Orders
            .Include(o => o.Member)
                .ThenInclude(m => m!.Level)
            .Include(o => o.Coupon)
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    /// <summary>
    /// 依 ID 取得訂單（僅主檔）
    /// </summary>
    public async Task<Order?> GetOrderHeaderByIdAsync(int id)
    {
        return await _db.Orders
            .Include(o => o.Member)
            .FirstOrDefaultAsync(o => o.Id == id);
    }
}
