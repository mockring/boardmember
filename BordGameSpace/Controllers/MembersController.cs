using Microsoft.AspNetCore.Mvc;
using BordGameSpace.Controllers;
using BordGameSpace.Services;
using BordGameSpace.Models;

namespace BordGameSpace.Controllers;

public class MembersController : BaseController
{
    private readonly AdminService _adminService;

    public MembersController(AdminService adminService)
    {
        _adminService = adminService;
    }

    /// <summary>
    /// 會員列表
    /// </summary>
    public async Task<IActionResult> Index(string? search = null, int? levelId = null, bool? status = null)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var members = await _adminService.GetAllMembersAsync(search, levelId, status);
        var levels = await _adminService.GetAllLevelsAsync();

        ViewBag.Search = search;
        ViewBag.LevelId = levelId;
        ViewBag.Status = status;
        ViewBag.Levels = levels;

        return View(members);
    }

    /// <summary>
    /// 會員詳情
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var member = await _adminService.GetMemberByIdAsync(id);
        if (member == null)
            return NotFound();

        var levels = await _adminService.GetAllLevelsAsync();
        ViewBag.Levels = levels;

        return View(member);
    }

    /// <summary>
    /// 新增會員頁面
    /// </summary>
    public async Task<IActionResult> Create()
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var levels = await _adminService.GetAllLevelsAsync();
        ViewBag.Levels = levels;

        return View();
    }

    /// <summary>
    /// 處理新增會員
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Member model)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        if (!ModelState.IsValid)
        {
            var levels = await _adminService.GetAllLevelsAsync();
            ViewBag.Levels = levels;
            return View(model);
        }

        var (success, message) = await _adminService.CreateMemberAsync(model);

        if (!success)
        {
            ModelState.AddModelError("", message);
            var levels = await _adminService.GetAllLevelsAsync();
            ViewBag.Levels = levels;
            return View(model);
        }

        TempData["SuccessMessage"] = message;
        return RedirectToAction("Index");
    }

    /// <summary>
    /// 編輯會員頁面
    /// </summary>
    public async Task<IActionResult> Edit(int id)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var member = await _adminService.GetMemberByIdAsync(id);
        if (member == null)
            return NotFound();

        var levels = await _adminService.GetAllLevelsAsync();
        ViewBag.Levels = levels;

        return View(member);
    }

    /// <summary>
    /// 處理編輯會員
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Member model, bool changePassword = false, string? newPassword = null)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        if (!ModelState.IsValid)
        {
            var levels = await _adminService.GetAllLevelsAsync();
            ViewBag.Levels = levels;
            return View(model);
        }

        if (changePassword && !string.IsNullOrWhiteSpace(newPassword))
        {
            model.PasswordHash = newPassword;
        }

        var (success, message) = await _adminService.UpdateMemberAsync(model, changePassword);

        if (!success)
        {
            ModelState.AddModelError("", message);
            var levels = await _adminService.GetAllLevelsAsync();
            ViewBag.Levels = levels;
            return View(model);
        }

        TempData["SuccessMessage"] = message;
        return RedirectToAction("Index");
    }

    /// <summary>
    /// 手動調整會員等級
    /// </summary>
    public async Task<IActionResult> ChangeLevel(int id, int levelId)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var (success, message) = await _adminService.ChangeMemberLevelAsync(id, levelId);

        TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
        return RedirectToAction("Details", new { id });
    }

    /// <summary>
    /// 重設會員密碼
    /// </summary>
    public async Task<IActionResult> ResetPassword(int id)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var (success, message) = await _adminService.ResetMemberPasswordAsync(id);

        TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
        return RedirectToAction("Details", new { id });
    }

    /// <summary>
    /// 停用/啟用會員
    /// </summary>
    public async Task<IActionResult> ToggleStatus(int id)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var (success, message) = await _adminService.ToggleMemberStatusAsync(id);

        TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
        return RedirectToAction("Index");
    }
}
