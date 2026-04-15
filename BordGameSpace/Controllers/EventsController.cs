using Microsoft.AspNetCore.Mvc;
using BordGameSpace.Controllers;
using BordGameSpace.Models;
using BordGameSpace.Data;
using Microsoft.EntityFrameworkCore;

namespace BordGameSpace.Controllers;

public class EventsController : BaseController
{
    private readonly AppDbContext _db;

    public EventsController(AppDbContext db)
    {
        _db = db;
    }

    // ==================== 後台 ====================

    /// <summary>
    /// 後台活動列表
    /// </summary>
    public async Task<IActionResult> Index(string? status)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var query = _db.Events.AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(e => e.Status == status);

        var events = await query.OrderByDescending(e => e.EventDate).ToListAsync();
        ViewBag.Status = status;
        return View(events);
    }

    /// <summary>
    /// 新增活動頁面
    /// </summary>
    public IActionResult Create()
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        return View();
    }

    /// <summary>
    /// 處理新增活動
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Event model)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        if (!ModelState.IsValid)
            return View(model);

        model.CreatedAt = DateTime.Now;
        model.UpdatedAt = DateTime.Now;
        if (string.IsNullOrEmpty(model.Status))
            model.Status = "RegistrationOpen";

        _db.Events.Add(model);
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "活動已建立";
        return RedirectToAction("Index");
    }

    /// <summary>
    /// 編輯活動頁面
    /// </summary>
    public async Task<IActionResult> Edit(int id)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var evt = await _db.Events.FindAsync(id);
        if (evt == null)
            return NotFound();

        return View(evt);
    }

    /// <summary>
    /// 處理編輯活動
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Event model)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        if (!ModelState.IsValid)
            return View(model);

        var evt = await _db.Events.FindAsync(model.Id);
        if (evt == null)
            return NotFound();

        evt.Title = model.Title;
        evt.Content = model.Content;
        evt.EventDate = model.EventDate;
        evt.RegistrationDeadline = model.RegistrationDeadline;
        evt.MaxParticipants = model.MaxParticipants;
        evt.Status = model.Status;
        evt.ImageUrl = model.ImageUrl;
        evt.UpdatedAt = DateTime.Now;

        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "活動已更新";
        return RedirectToAction("Index");
    }

    /// <summary>
    /// 刪除活動
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var evt = await _db.Events.FindAsync(id);
        if (evt != null)
        {
            _db.Events.Remove(evt);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "活動已刪除";
        }

        return RedirectToAction("Index");
    }

    /// <summary>
    /// 活動詳情 + 報名人列表
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var evt = await _db.Events
            .Include(e => e.Registrations)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (evt == null)
            return NotFound();

        return View(evt);
    }

    // ==================== 前台 ====================

    /// <summary>
    /// 前台活動列表（月檢視）
    /// </summary>
    public async Task<IActionResult> FrontendIndex(int? year, int? month)
    {
        var now = TaiwanNow;
        var y = year ?? now.Year;
        var m = month ?? now.Month;

        var startOfMonth = new DateTime(y, m, 1);
        var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

        var events = await _db.Events
            .Include(e => e.Registrations)
            .Where(e => e.EventDate >= startOfMonth && e.EventDate <= endOfMonth)
            .OrderBy(e => e.EventDate)
            .ToListAsync();

        ViewBag.Year = y;
        ViewBag.Month = m;
        ViewBag.Events = events;

        // 計算當月天數與第一天的星期
        ViewBag.DaysInMonth = DateTime.DaysInMonth(y, m);
        ViewBag.FirstDayOfWeek = (int)startOfMonth.DayOfWeek;

        return View();
    }

    /// <summary>
    /// 前台活動詳情 + 報名表單
    /// </summary>
    public async Task<IActionResult> FrontendDetails(int id)
    {
        var evt = await _db.Events
            .Include(e => e.Registrations)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (evt == null)
            return NotFound();

        ViewBag.IsLoggedIn = CurrentMemberId.HasValue;
        return View(evt);
    }

    /// <summary>
    /// 處理報名
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(int eventId, string name, string phone)
    {
        var evt = await _db.Events.FindAsync(eventId);
        if (evt == null)
            return NotFound();

        // 檢查報名截止日
        if (TaiwanNow > evt.RegistrationDeadline)
        {
            TempData["ErrorMessage"] = "報名已截止";
            return RedirectToAction("FrontendDetails", new { id = eventId });
        }

        // 防呆：檢查重複報名（同一電話 + 同一活動）
        var alreadyRegistered = await _db.EventRegistrations
            .AnyAsync(r => r.EventId == eventId && r.Phone == phone);
        if (alreadyRegistered)
        {
            TempData["ErrorMessage"] = "您已報名此活動，請勿重複報名";
            return RedirectToAction("FrontendDetails", new { id = eventId });
        }

        // 檢查人數上限
        if (evt.MaxParticipants.HasValue)
        {
            var count = await _db.EventRegistrations.CountAsync(r => r.EventId == eventId);
            if (count >= evt.MaxParticipants.Value)
            {
                TempData["ErrorMessage"] = "報名人數已額滿";
                return RedirectToAction("FrontendDetails", new { id = eventId });
            }
        }

        var registration = new EventRegistration
        {
            EventId = eventId,
            Name = name,
            Phone = phone,
            CreatedAt = DateTime.Now
        };

        _db.EventRegistrations.Add(registration);
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "報名成功";
        return RedirectToAction("FrontendDetails", new { id = eventId });
    }

    /// <summary>
    /// 報名頁面（會員專用）
    /// </summary>
    public async Task<IActionResult> RegisterPage(int id)
    {
        if (!CurrentMemberId.HasValue)
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("RegisterPage", "Events", new { id }) });

        var evt = await _db.Events
            .Include(e => e.Registrations)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (evt == null)
            return NotFound();

        var member = await _db.Members.FindAsync(CurrentMemberId.Value);
        ViewBag.MemberName = member?.Name ?? "";
        ViewBag.MemberPhone = member?.Phone ?? "";
        ViewBag.IsLoggedIn = true;
        return View(evt);
    }

    /// <summary>
    /// 處理報名（從報名頁）
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterPagePost(int eventId, string name, string phone)
    {
        if (!CurrentMemberId.HasValue)
            return RedirectToAction("Login", "Account");

        var evt = await _db.Events.FindAsync(eventId);
        if (evt == null)
            return NotFound();

        if (TaiwanNow > evt.RegistrationDeadline)
        {
            TempData["ErrorMessage"] = "報名已截止";
            return RedirectToAction("FrontendDetails", new { id = eventId });
        }

        // 防呆：檢查重複報名（同一電話 + 同一活動）
        var alreadyRegistered = await _db.EventRegistrations
            .AnyAsync(r => r.EventId == eventId && r.Phone == phone);
        if (alreadyRegistered)
        {
            TempData["ErrorMessage"] = "您已報名此活動，請勿重複報名";
            return RedirectToAction("FrontendDetails", new { id = eventId });
        }

        if (evt.MaxParticipants.HasValue)
        {
            var count = await _db.EventRegistrations.CountAsync(r => r.EventId == eventId);
            if (count >= evt.MaxParticipants.Value)
            {
                TempData["ErrorMessage"] = "報名人數已額滿";
                return RedirectToAction("FrontendDetails", new { id = eventId });
            }
        }

        var registration = new EventRegistration
        {
            EventId = eventId,
            Name = name,
            Phone = phone,
            CreatedAt = DateTime.Now
        };

        _db.EventRegistrations.Add(registration);
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "報名成功";
        return RedirectToAction("FrontendDetails", new { id = eventId });
    }

    /// <summary>
    /// 我的活動報名（會員專用）
    /// </summary>
    public async Task<IActionResult> MyRegistrations()
    {
        if (!CurrentMemberId.HasValue)
            return RedirectToAction("Login", "Account");

        var member = await _db.Members.FindAsync(CurrentMemberId.Value);
        var myPhone = member?.Phone ?? "";

        var myRegs = await _db.EventRegistrations
            .Include(r => r.Event)
            .Where(r => r.Phone == myPhone)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return View(myRegs);
    }
}