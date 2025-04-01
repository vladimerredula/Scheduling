using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Scheduling.Models;

namespace Scheduling.Controllers
{
    public class ErrorController : Controller
    {
        [Route("Error/{statusCode}")]
        public IActionResult Error(int statusCode)
        {
            switch (statusCode)
            {
                case 404:
                    return View("404");
                // Add more cases here for other status codes if needed
                default:
                    return View("Error", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
            }
        }

        [Route("Error")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult GenericError()
        {
            return View("Error", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [Route("AccessDenied")]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
