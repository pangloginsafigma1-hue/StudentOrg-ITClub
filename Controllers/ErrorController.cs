using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITClubOrg.Controllers;

[AllowAnonymous]
public sealed class ErrorController : Controller
{
    [Route("Error")]
    public IActionResult Index()
    {
        return View();
    }
}
