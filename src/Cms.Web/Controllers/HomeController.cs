namespace Cms.Web.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

public sealed class HomeController : Controller
{
    [Authorize]
    public IActionResult Index()
    {
        return Content($"Hosgeldin, {User.Identity?.Name}");
    }
}
