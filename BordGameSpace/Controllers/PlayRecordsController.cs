using Microsoft.AspNetCore.Mvc;
using BordGameSpace.Controllers;
using BordGameSpace.Services;
using BordGameSpace.Models;
using BordGameSpace.Data;
using Microsoft.EntityFrameworkCore;

namespace BordGameSpace.Controllers;

public class PlayRecordsController : BaseController
{
    private readonly AppDbContext _db;
    private readonly ILogger<PlayRecordsController> _logger;

    public PlayRecordsController(AppDbContext db, ILogger<PlayRecordsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// 遊玩紀錄首頁
    /// </summary>
    public async Task<IActionResult> Index()
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var activePlays = await _db.PlayRecords
            .Include(p => p.Member)
                .ThenInclude(m => m!.Level)
            .Where(p => p.Status == "Playing" || p.Status == "Completed")
            .OrderByDescending(p => p.Status == "Completed")
            .ThenBy(p => p.StartTime)
            .ToListAsync();

        ViewBag.ActivePlays = activePlays;
        ViewBag.HasUnpaid = activePlays.Any(p => p.Status == "Completed");
        ViewBag.TaiwanNow = TaiwanNow;
        ViewBag.DefaultStartTime = TaiwanNow.ToString("yyyy-MM-ddTHH:mm");

        // 會員等級費率（用於計費說明）
        var levels = await _db.Levels
            .OrderBy(l => l.SortOrder)
            .ToListAsync();
        ViewBag.Levels = levels;

        // 計算各記錄的已玩時數和費用
        foreach (var play in activePlays)
        {
            var elapsed = (play.EndTime ?? TaiwanNow) - play.StartTime;
            var hours = (decimal)Math.Ceiling(elapsed.TotalHours); // 無條件進位
            play.TotalHours = hours;
            play.Amount = hours * play.HourlyRate;
        }

        return View();
    }

    /// <summary>
    /// 搜尋會員（用於新增遊玩）
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SearchMember(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return Json(new { success = false, message = "請輸入電話號碼" });

        var member = await _db.Members
            .Include(m => m.Level)
            .FirstOrDefaultAsync(m => m.Phone == phone && m.Status);

        if (member == null)
            return Json(new { success = true, isMember = false });

        var weekdayRate = member.Level?.WeekdayHourlyRate ?? 60;
        var holidayRate = member.Level?.HolidayHourlyRate ?? 70;

        return Json(new
        {
            success = true,
            isMember = true,
            member = new
            {
                member.Id,
                member.Name,
                member.Phone,
                LevelName = member.Level?.Name,
                weekdayHourlyRate = weekdayRate,
                holidayHourlyRate = holidayRate
            }
        });
    }

    /// <summary>
    /// 開始遊玩
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> StartPlay([FromBody] StartPlayRequest request)
    {
        if (!IsAdminLoggedIn)
            return Json(new { success = false, message = "未登入" });

        // 將 client 送來的 UTC 時間轉為台灣時區
        var startTime = TimeZoneInfo.ConvertTimeFromUtc(request.StartTime.ToUniversalTime(), TaiwanZone);

        Member? member = null;
        decimal hourlyRate = GetNonMemberRate(startTime);

        if (request.MemberId.HasValue)
        {
            member = await _db.Members
                .Include(m => m.Level)
                .FirstOrDefaultAsync(m => m.Id == request.MemberId.Value);

            if (member?.Level != null)
            {
                hourlyRate = IsHoliday(startTime)
                    ? member.Level.HolidayHourlyRate
                    : member.Level.WeekdayHourlyRate;
            }
        }

        var play = new PlayRecord
        {
            MemberId = member?.Id,
            MemberName = member?.Name ?? request.MemberName ?? "非會員",
            MemberPhone = member?.Phone ?? request.Phone,
            StartTime = startTime,
            HourlyRate = hourlyRate,
            Status = "Playing",
            CreatedAt = TaiwanNow
        };

        _db.PlayRecords.Add(play);
        await _db.SaveChangesAsync();

        _logger.LogInformation("開始遊玩: PlayId={PlayId}, Member={Member}, StartTime={StartTime}",
            play.Id, play.MemberName, play.StartTime);

        return Json(new
        {
            success = true,
            message = "已開始遊玩",
            play = new
            {
                play.Id,
                play.MemberName,
                play.MemberPhone,
                play.StartTime,
                play.HourlyRate,
                play.Status
            }
        });
    }

    /// <summary>
    /// 結束遊玩
    /// </summary>
    public async Task<IActionResult> EndPlay(int id)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var play = await _db.PlayRecords
            .Include(p => p.Member)
                .ThenInclude(m => m!.Level)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (play == null)
            return RedirectToAction("Index");

        // 計算時數與金額
        var endTime = TaiwanNow;
        play.EndTime = endTime;

        var elapsed = endTime - play.StartTime;
        var hours = (decimal)Math.Ceiling(elapsed.TotalHours); // 無條件進位
        play.TotalHours = hours;

        // 更新時單價（可能開始時是平日，結束時是假日）
        if (play.Member?.Level != null)
        {
            play.HourlyRate = IsHoliday(play.StartTime)
                ? play.Member.Level.HolidayHourlyRate
                : play.Member.Level.WeekdayHourlyRate;
        }
        else
        {
            play.HourlyRate = GetNonMemberRate(play.StartTime);
        }

        // 套用每日上限
        var dailyCap = play.Member != null
            ? (IsHoliday(play.StartTime) ? 220m : 180m)
            : (IsHoliday(play.StartTime) ? 280m : 240m);
        play.Amount = Math.Min(hours * play.HourlyRate, dailyCap);
        play.Status = "Completed";

        await _db.SaveChangesAsync();

        _logger.LogInformation("結束遊玩: PlayId={PlayId}, Hours={Hours}, Amount={Amount}, Cap={DailyCap}",
            play.Id, play.TotalHours, play.Amount, dailyCap);

        return RedirectToAction("Index");
    }

    /// <summary>
    /// 刪除遊玩記錄
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        if (!IsAdminLoggedIn)
            return Json(new { success = false, message = "未登入" });

        var play = await _db.PlayRecords.FindAsync(id);
        if (play == null)
            return Json(new { success = false, message = "找不到紀錄" });

        if (play.Status == "CheckedOut")
            return Json(new { success = false, message = "已結帳的記錄無法刪除" });

        _db.PlayRecords.Remove(play);
        await _db.SaveChangesAsync();

        return Json(new { success = true, message = "已刪除" });
    }

    /// <summary>
    /// 取得非會員的時單價（平日65/假日75）
    /// </summary>
    private decimal GetNonMemberRate(DateTime date)
    {
        return IsHoliday(date) ? 70 : 60;
    }

    /// <summary>
    /// 判斷是否為假日
    /// </summary>
    private bool IsHoliday(DateTime date)
    {
        // 0 = Sunday, 6 = Saturday
        return date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
    }
}

public class StartPlayRequest
{
    public int? MemberId { get; set; }
    public string? MemberName { get; set; }
    public string? Phone { get; set; }
    public DateTime StartTime { get; set; }
}
