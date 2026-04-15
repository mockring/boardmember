using Microsoft.AspNetCore.Mvc;
using BordGameSpace.Controllers;
using BordGameSpace.Services;
using BordGameSpace.Models;
using BordGameSpace.DTOs;
using BordGameSpace.Data;
using Microsoft.EntityFrameworkCore;

namespace BordGameSpace.Controllers;

public class AdminController : BaseController
{
    private readonly AdminService _adminService;
    private readonly ILogger<AdminController> _logger;
    private readonly AppDbContext _db;

    public AdminController(AdminService adminService, ILogger<AdminController> logger, AppDbContext db)
    {
        _adminService = adminService;
        _logger = logger;
        _db = db;
    }

    /// <summary>
    /// 後台儀表板
    /// </summary>
    public async Task<IActionResult> Index()
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        // 今日統計（使用台灣時區）
        var taiwanNow = TaiwanNow;
        var todayStart = taiwanNow.Date;
        var todayOrders = await _db.Orders
            .Where(o => o.CreatedAt >= todayStart && o.PaymentStatus == "Paid")
            .ToListAsync();

        ViewBag.TodayOrderCount = todayOrders.Count;
        ViewBag.TodayRevenue = todayOrders.Sum(o => o.FinalAmount);

        // 進行中的遊玩
        var activePlays = await _db.PlayRecords
            .Where(p => p.Status == "Playing" || p.Status == "Completed")
            .ToListAsync();
        ViewBag.ActivePlayCount = activePlays.Count;
        ViewBag.ActivePlays = activePlays;
        ViewBag.TaiwanNow = taiwanNow;

        // 進行中的遊戲租借
        var activeRentals = await _db.GameRentals
            .Include(g => g.Product)
            .Include(g => g.Member)
            .Where(g => g.Status == "Pending" || g.Status == "Approved" || g.Status == "Borrowed")
            .OrderBy(g => g.Status == "Pending" ? 0 : g.Status == "Approved" ? 1 : 2)
            .ThenByDescending(g => g.CreatedAt)
            .ToListAsync();
        ViewBag.ActiveRentals = activeRentals;
        ViewBag.ActiveRentalCount = activeRentals.Count;

        // 已預訂的空間預約
        var today = taiwanNow.Date;
        var upcomingReservations = await _db.SpaceReservations.Where(r => r.ReservationDate >= today && (r.Status == "Approved" || r.Status == "Pending")).ToListAsync();
        ViewBag.UpcomingReservations = upcomingReservations;
        ViewBag.UpcomingReservationCount = upcomingReservations.Count;
        ViewBag.ActiveReservations = upcomingReservations;
        ViewBag.ActiveReservationCount = upcomingReservations.Count;

        // 報名中的活動
        var openEvents = await _db.Events
            .Include(e => e.Registrations)
            .Where(e => e.Status == "RegistrationOpen" && e.EventDate >= today)
            .OrderBy(e => e.EventDate)
            .ToListAsync();
        ViewBag.OpenEvents = openEvents;
        ViewBag.OpenEventCount = openEvents.Count;

        return View();
    }

    /// <summary>
    /// 管理者登入頁面
    /// </summary>
    public IActionResult Login(string? returnUrl = null)
    {
        if (IsAdminLoggedIn)
            return RedirectToAction("Index", "Admin");

        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    /// <summary>
    /// 處理管理者登入
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginDto model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
            return View(model);

        var (success, message, admin) = await _adminService.LoginAsync(model.Account, model.Password);

        if (!success)
        {
            ModelState.AddModelError("", message);
            return View(model);
        }

        HttpContext.Session.SetInt32(ADMIN_SESSION_KEY, admin!.Id);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Index", "Admin");
    }

    /// <summary>
    /// 管理者登出
    /// </summary>
    public IActionResult Logout()
    {
        HttpContext.Session.Remove(ADMIN_SESSION_KEY);
        return RedirectToAction("Login", "Admin");
    }

    /// <summary>
    /// 修改密碼頁面
    /// </summary>
    public IActionResult ChangePassword()
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");
        return View();
    }

    /// <summary>
    /// 處理修改密碼
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto model)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        if (!ModelState.IsValid)
            return View(model);

        if (model.NewPassword != model.ConfirmPassword)
        {
            ModelState.AddModelError("", "新密碼與確認密碼不符");
            return View(model);
        }

        if (model.NewPassword.Length < 6)
        {
            ModelState.AddModelError("", "新密碼至少需要 6 個字元");
            return View(model);
        }

        var adminId = HttpContext.Session.GetInt32(ADMIN_SESSION_KEY);
        var admin = await _db.Admins.FindAsync(adminId);
        if (admin == null)
        {
            return RedirectToAction("Login", "Admin");
        }

        if (!BCrypt.Net.BCrypt.Verify(model.CurrentPassword, admin.PasswordHash))
        {
            ModelState.AddModelError("", "目前密碼輸入錯誤");
            return View(model);
        }

        admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword, 12);
        await _db.SaveChangesAsync();

        ViewData["Success"] = true;
        return View(model);
    }
}
