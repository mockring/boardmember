using Microsoft.AspNetCore.Mvc;
using BordGameSpace.Controllers;
using BordGameSpace.Services;
using BordGameSpace.Data;
using Microsoft.EntityFrameworkCore;

namespace BordGameSpace.Controllers;

[Route("Reports")]
[Route("admin/Reports", Order = 1)]
public class ReportsController : BaseController
{
    private readonly ReportService _service;
    private readonly AppDbContext _db;

    public ReportsController(ReportService service, AppDbContext db)
    {
        _service = service;
        _db = db;
    }

    /// <summary>
    /// 報表首頁（當月）
    /// </summary>
    public async Task<IActionResult> Index(int? year, int? month)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var now = DateTime.Now;
        var y = year ?? now.Year;
        var m = month ?? now.Month;

        ViewBag.Year = y;
        ViewBag.Month = m;

        var revenue = await _service.GetMonthlyRevenueAsync(y, m);
        var topProducts = await _service.GetTopProductsAsync(y, m, 10);
        var topMembers = await _service.GetTopMembersAsync(y, m, 10);
        var levelDist = await _service.GetLevelDistributionAsync();

        ViewBag.Revenue = revenue;
        ViewBag.TopProducts = topProducts;
        ViewBag.TopMembers = topMembers;
        ViewBag.LevelDistribution = levelDist;

        return View();
    }
}
