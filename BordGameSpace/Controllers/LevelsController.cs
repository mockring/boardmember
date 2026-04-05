using Microsoft.AspNetCore.Mvc;
using BordGameSpace.Controllers;
using BordGameSpace.Services;
using BordGameSpace.Models;

namespace BordGameSpace.Controllers;

public class LevelsController : BaseController
{
    private readonly AdminService _adminService;

    public LevelsController(AdminService adminService)
    {
        _adminService = adminService;
    }

    /// <summary>
    /// 等級列表
    /// </summary>
    public async Task<IActionResult> Index()
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var levels = await _adminService.GetAllLevelsAsync();
        return View(levels);
    }

    /// <summary>
    /// 新增等級頁面
    /// </summary>
    public IActionResult Create()
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        return View();
    }

    /// <summary>
    /// 處理新增等級
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Level model)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        if (!ModelState.IsValid)
            return View(model);

        var (success, message) = await _adminService.CreateLevelAsync(model);

        if (!success)
        {
            ModelState.AddModelError("", message);
            return View(model);
        }

        TempData["SuccessMessage"] = message;
        return RedirectToAction("Index");
    }

    /// <summary>
    /// 編輯等級頁面
    /// </summary>
    public async Task<IActionResult> Edit(int id)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var level = await _adminService.GetLevelByIdAsync(id);
        if (level == null)
            return NotFound();

        return View(level);
    }

    /// <summary>
    /// 處理編輯等級
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Level model)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        if (!ModelState.IsValid)
            return View(model);

        var (success, message) = await _adminService.UpdateLevelAsync(model);

        if (!success)
        {
            ModelState.AddModelError("", message);
            return View(model);
        }

        TempData["SuccessMessage"] = message;
        return RedirectToAction("Index");
    }

    /// <summary>
    /// 刪除等級
    /// </summary>
    public async Task<IActionResult> Delete(int id)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var (success, message) = await _adminService.DeleteLevelAsync(id);

        TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
        return RedirectToAction("Index");
    }
}
