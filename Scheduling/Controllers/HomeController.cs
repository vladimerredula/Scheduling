using Microsoft.AspNetCore.Mvc;
using Scheduling.Models;
using System.Diagnostics;
using System.Security.Claims;

namespace Scheduling.Controllers
{
    public class HomeController : Controller
    {

        public HomeController()
        {
        }

        public IActionResult Index()
        {
            if (int.Parse(User.FindFirstValue("Personnelid")) == 999)
            {
                return RedirectToAction("ScheduleView", "Schedule");
            }

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
