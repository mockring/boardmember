using Microsoft.AspNetCore.Mvc;
using BordGameSpace.Controllers;
using BordGameSpace.Services;
using BordGameSpace.Models;

namespace BordGameSpace.Controllers;

public class ProductsController : BaseController
{
    private readonly AdminService _adminService;

    public ProductsController(AdminService adminService)
    {
        _adminService = adminService;
    }

    /// <summary>
    /// 商品列表
    /// </summary>
    public async Task<IActionResult> Index(string? category = null, bool? isActive = null)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var products = await _adminService.GetAllProductsAsync(category, isActive);
        var categories = await _adminService.GetCategoriesAsync();

        ViewBag.Categories = categories;
        ViewBag.SelectedCategory = category;
        ViewBag.SelectedIsActive = isActive;

        return View(products);
    }

    /// <summary>
    /// 新增商品頁面
    /// </summary>
    public async Task<IActionResult> Create()
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var categories = await _adminService.GetCategoriesAsync();
        ViewBag.Categories = categories;
        return View();
    }

    /// <summary>
    /// 處理新增商品
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Product model)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        if (!ModelState.IsValid)
        {
            var cats = await _adminService.GetCategoriesAsync();
            ViewBag.Categories = cats;
            return View(model);
        }

        var (success, message, _) = await _adminService.CreateProductAsync(model);

        if (!success)
        {
            ModelState.AddModelError("", message);
            var categories = await _adminService.GetCategoriesAsync();
            ViewBag.Categories = categories;
            return View(model);
        }

        TempData["SuccessMessage"] = message;
        return RedirectToAction("Index");
    }

    /// <summary>
    /// 編輯商品頁面
    /// </summary>
    public async Task<IActionResult> Edit(int id)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var product = await _adminService.GetProductByIdAsync(id);
        if (product == null)
            return NotFound();

        var categories = await _adminService.GetCategoriesAsync();
        ViewBag.Categories = categories;

        return View(product);
    }

    /// <summary>
    /// 處理編輯商品
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Product model)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        if (!ModelState.IsValid)
        {
            var cats = await _adminService.GetCategoriesAsync();
            ViewBag.Categories = cats;
            return View(model);
        }

        var (success, message) = await _adminService.UpdateProductAsync(model);

        if (!success)
        {
            ModelState.AddModelError("", message);
            var categories = await _adminService.GetCategoriesAsync();
            ViewBag.Categories = categories;
            return View(model);
        }

        TempData["SuccessMessage"] = message;
        return RedirectToAction("Index");
    }

    /// <summary>
    /// 刪除商品
    /// </summary>
    public async Task<IActionResult> Delete(int id)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var (success, message) = await _adminService.DeleteProductAsync(id);

        TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
        return RedirectToAction("Index");
    }

    /// <summary>
    /// 快速調整庫存
    /// </summary>
    public async Task<IActionResult> AdjustStock(int id, int quantity)
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        var (success, message) = await _adminService.UpdateStockAsync(id, quantity);

        TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
        return RedirectToAction("Index");
    }

}
