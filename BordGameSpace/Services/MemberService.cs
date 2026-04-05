using Microsoft.EntityFrameworkCore;
using BordGameSpace.Data;
using BordGameSpace.Models;

namespace BordGameSpace.Services;

public class MemberService
{
    private readonly AppDbContext _db;
    private readonly ILogger<MemberService> _logger;

    public MemberService(AppDbContext db, ILogger<MemberService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// 檢查電話或Email是否已被使用
    /// </summary>
    public async Task<bool> IsPhoneOrEmailExistsAsync(string phone, string email, int? excludeMemberId = null)
    {
        var query = _db.Members.AsQueryable();

        if (excludeMemberId.HasValue)
            query = query.Where(m => m.Id != excludeMemberId.Value);

        return await query.AnyAsync(m => m.Phone == phone || m.Email == email);
    }

    /// <summary>
    /// 註冊新會員
    /// </summary>
    public async Task<(bool Success, string Message, Member? Member)> RegisterAsync(
        string name, string phone, string email, string password, DateTime? birthday)
    {
        // 檢查是否已有帳號
        if (await IsPhoneOrEmailExistsAsync(phone, email))
            return (false, "電話或Email已被註冊", null);

        // 取得預設等級（小鳳梨）
        var defaultLevel = await _db.Levels.FirstOrDefaultAsync(l => l.IsDefault);
        if (defaultLevel == null)
            return (false, "系統錯誤：找不到預設等級", null);

        var member = new Member
        {
            Name = name,
            Phone = phone,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, 12),
            Birthday = birthday,
            LevelId = defaultLevel.Id,
            Status = true,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        _db.Members.Add(member);
        await _db.SaveChangesAsync();

        _logger.LogInformation("新會員註冊: {Name}, {Phone}", name, phone);

        return (true, "註冊成功", member);
    }

    /// <summary>
    /// 驗證會員登入
    /// </summary>
    public async Task<(bool Success, string Message, Member? Member)> LoginAsync(string account, string password)
    {
        var member = await _db.Members
            .Include(m => m.Level)
            .FirstOrDefaultAsync(m =>
                (m.Phone == account || m.Email == account) && m.Status);

        if (member == null)
            return (false, "帳號不存在或已被停用", null);

        if (!BCrypt.Net.BCrypt.Verify(password, member.PasswordHash))
            return (false, "密碼錯誤", null);

        _logger.LogInformation("會員登入: {Name}", member.Name);

        return (true, "登入成功", member);
    }

    /// <summary>
    /// 取得會員資料
    /// </summary>
    public async Task<Member?> GetMemberByIdAsync(int id)
    {
        return await _db.Members
            .Include(m => m.Level)
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    /// <summary>
    /// 檢查會員是否可以升級
    /// </summary>
    public async Task CheckAndUpgradeLevelAsync(Member member)
    {
        // 取得所有等級，按SortOrder排序（數字小的在前）
        var levels = await _db.Levels
            .Where(l => l.IsDefault == false) // 排除預設等級
            .OrderByDescending(l => l.SortOrder)
            .ToListAsync();

        foreach (var level in levels)
        {
            // 同時滿足時數和金額門檻才升級
            if (member.TotalPlayHours >= level.UpgradeThresholdHours &&
                member.TotalSpending >= level.UpgradeThresholdAmount)
            {
                if (member.LevelId != level.Id)
                {
                    member.LevelId = level.Id;
                    member.UpdatedAt = DateTime.Now;
                    await _db.SaveChangesAsync();
                    _logger.LogInformation("會員 {Name} 升級為 {Level}", member.Name, level.Name);
                }
                break;
            }
        }
    }

    /// <summary>
    /// 更新會員資料
    /// </summary>
    public async Task<(bool Success, string Message)> UpdateProfileAsync(
        int memberId, string name, string phone, string email, DateTime? birthday)
    {
        var member = await _db.Members.FindAsync(memberId);
        if (member == null)
            return (false, "找不到會員資料");

        // 檢查電話/Email是否已被其他人使用
        if (await IsPhoneOrEmailExistsAsync(phone, email, memberId))
            return (false, "電話或Email已被其他會員使用");

        member.Name = name;
        member.Phone = phone;
        member.Email = email;
        member.Birthday = birthday;
        member.UpdatedAt = DateTime.Now;

        await _db.SaveChangesAsync();

        return (true, "更新成功");
    }

    /// <summary>
    /// 修改密碼
    /// </summary>
    public async Task<(bool Success, string Message)> ChangePasswordAsync(
        int memberId, string oldPassword, string newPassword)
    {
        var member = await _db.Members.FindAsync(memberId);
        if (member == null)
            return (false, "找不到會員資料");

        if (!BCrypt.Net.BCrypt.Verify(oldPassword, member.PasswordHash))
            return (false, "舊密碼錯誤");

        member.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, 12);
        member.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();

        return (true, "密碼修改成功");
    }
}
