using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using BordGameSpace.DTOs;
using BordGameSpace.Services;

namespace BordGameSpace.Controllers;

public class AccountController : Controller
{
    private readonly MemberService _memberService;
    private readonly ILogger<AccountController> _logger;

    private const string MEMBER_SESSION_KEY = "MemberId";

    public AccountController(MemberService memberService, ILogger<AccountController> logger)
    {
        _memberService = memberService;
        _logger = logger;
    }

    /// <summary>
    /// 登入頁面
    /// </summary>
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    /// <summary>
    /// 處理登入
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginDto model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
            return View(model);

        var (success, message, member) = await _memberService.LoginAsync(model.Account, model.Password);

        if (!success)
        {
            ModelState.AddModelError("", message);
            return View(model);
        }

        // 儲存 Session
        HttpContext.Session.SetInt32(MEMBER_SESSION_KEY, member!.Id);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Index", "Member");
    }

    /// <summary>
    /// 註冊頁面
    /// </summary>
    public IActionResult Register()
    {
        return View();
    }

    /// <summary>
    /// 處理註冊
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterDto model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var (success, message, member) = await _memberService.RegisterAsync(
            model.Name, model.Phone, model.Email, model.Password, model.Birthday);

        if (!success)
        {
            ModelState.AddModelError("", message);
            return View(model);
        }

        // 註冊後自動登入
        HttpContext.Session.SetInt32(MEMBER_SESSION_KEY, member!.Id);

        TempData["SuccessMessage"] = "註冊成功！歡迎加入菠德桌上遊戲空間";
        return RedirectToAction("Index", "Home");
    }

    /// <summary>
    /// 登出
    /// </summary>
    public IActionResult Logout()
    {
        HttpContext.Session.Remove(MEMBER_SESSION_KEY);
        return RedirectToAction("Index", "Home");
    }

    /// <summary>
    /// 檢查是否已登入
    /// </summary>
    public int? GetCurrentMemberId()
    {
        return HttpContext.Session.GetInt32(MEMBER_SESSION_KEY);
    }

    /// <summary>
    /// 檢查是否已登入（Boolean）
    /// </summary>
    public bool IsLoggedIn()
    {
        return HttpContext.Session.GetInt32(MEMBER_SESSION_KEY).HasValue;
    }
}
