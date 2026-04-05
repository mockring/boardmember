using Microsoft.AspNetCore.Mvc;
using BordGameSpace.Controllers;

namespace BordGameSpace.Controllers;

public class OrdersController : BaseController
{
    public IActionResult Index()
    {
        if (!IsAdminLoggedIn)
            return RedirectToAction("Login", "Admin");

        return View();
    }
}
