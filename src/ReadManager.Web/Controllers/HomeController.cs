using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Web.Models;
using ReadManager.Web.Services;

namespace ReadManager.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index(string? q, int? genre) => View(DemoHomeData.GetHome(q?.Trim(), genre));
    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
    });
}