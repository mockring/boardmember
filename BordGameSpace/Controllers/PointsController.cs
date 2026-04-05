using Microsoft.AspNetCore.Mvc;
using BordGameSpace.Controllers;
using BordGameSpace.Services;
using BordGameSpace.Models;
using BordGameSpace.Data;
using Microsoft.EntityFrameworkCore;

namespace BordGameSpace.Controllers;

public class PointsController : BaseController
{
    private readonly PointsService _pointsService;
    private readonly AppDbContext _db;

    public PointsController(PointsService pointsService, AppDbContext db)
    {
        _pointsService = pointsService;
        _db = db;
    }

    /// <summary>
    /// 積分設定頁面
    /// </summary>
    public async Task<IActionResult> Index()
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var leaderboard = await _pointsService.GetLeaderboardAsync(20);
        var levels = await _db.Levels.OrderBy(l => l.SortOrder).ToListAsync();

        ViewBag.Leaderboard = leaderboard;
        ViewBag.Levels = levels;

        return View();
    }

    /// <summary>
    /// 手動發放積分給會員
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPoints(int memberId, int points, string description)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        if (string.IsNullOrWhiteSpace(description))
            description = "管理員發放";

        var (success, message) = await _pointsService.AddPointsAsync(memberId, points, description);

        TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
        return RedirectToAction("Index");
    }

    /// <summary>
    /// 大量發放積分給所有會員
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPointsToAll(int points, string description)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        if (string.IsNullOrWhiteSpace(description))
            description = "管理員發放";

        var (success, message, _) = await _pointsService.AddPointsToAllMembersAsync(points, description);

        TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
        return RedirectToAction("Index");
    }

    /// <summary>
    /// 依等級發放積分
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPointsByLevel(int levelId, int points, string description)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        if (string.IsNullOrWhiteSpace(description))
            description = "管理員發放";

        var (success, message, _) = await _pointsService.AddPointsByLevelAsync(points, levelId, description);

        TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
        return RedirectToAction("Index");
    }

    /// <summary>
    /// 扣回積分
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeductPoints(int memberId, int points, string description)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        if (string.IsNullOrWhiteSpace(description))
            description = "管理員扣回";

        var (success, message) = await _pointsService.DeductPointsAsync(memberId, points, description);

        TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
        return RedirectToAction("Index");
    }

    /// <summary>
    /// 查詢會員（AJAX）
    /// </summary>
    public async Task<IActionResult> SearchMembers(string? phone, string? name)
    {
        if (!IsAdminLoggedIn)
            return Unauthorized();

        var query = _db.Members.Include(m => m.Level).AsQueryable();

        if (!string.IsNullOrWhiteSpace(phone))
            query = query.Where(m => m.Phone.Contains(phone));

        if (!string.IsNullOrWhiteSpace(name))
            query = query.Where(m => m.Name.Contains(name));

        var members = await query.Take(10).ToListAsync();

        return Json(members.Select(m => new
        {
            m.Id,
            m.Name,
            m.Phone,
            LevelName = m.Level != null ? m.Level.Name : "未知"
        }));
    }
}
