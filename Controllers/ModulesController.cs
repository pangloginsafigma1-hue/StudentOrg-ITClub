using ITClubOrg.Models;
using ITClubOrg.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITClubOrg.Controllers;

[Authorize]
public sealed class ModulesController : Controller
{
    [HttpGet]
    public IActionResult Index(string section)
    {
        var item = NavigationCatalog.Find(section);
        if (item is null
            || item.Key is "dashboard" or "members" or "membership" or "activities" or "attendance" or "reports" or "settings")
        {
            return NotFound();
        }

        ViewData["Title"] = item.Title;
        ViewData["Description"] = item.Description;
        ViewData["ActiveSection"] = item.Key;

        return View(new ModulePageViewModel
        {
            Title = item.Title,
            Description = item.Description
        });
    }
}
