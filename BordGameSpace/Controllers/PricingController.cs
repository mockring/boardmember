using Microsoft.AspNetCore.Mvc;

namespace BordGameSpace.Controllers;

public class PricingController : BaseController
{
    public IActionResult Pricing()
    {
        return View();
    }
}