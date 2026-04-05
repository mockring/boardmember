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

        // 低庫存商品
        var lowStockProducts = await _adminService.GetLowStockProductsAsync();
        ViewBag.LowStockProducts = lowStockProducts;
        ViewBag.LowStockCount = lowStockProducts.Count;

        // 今日統計
        var today = DateTime.Today;
        var todayOrders = await _db.Orders
            .Where(o => o.CreatedAt >= today && o.PaymentStatus == "Paid")
            .ToListAsync();

        ViewBag.TodayOrderCount = todayOrders.Count;
        ViewBag.TodayRevenue = todayOrders.Sum(o => o.FinalAmount);

        // 進行中的遊玩
        var activePlays = await _db.PlayRecords
            .Where(p => p.Status == "Playing" || p.Status == "Completed")
            .ToListAsync();
        ViewBag.ActivePlayCount = activePlays.Count;
        ViewBag.ActivePlays = activePlays;

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
}
