using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scheduling.Models;

namespace Scheduling.Controllers
{
    public class SessionController : Controller
    {
        private readonly ApplicationDbContext _db;

        public SessionController(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetActiveUsers()
        {
            var threshold = DateTime.UtcNow.AddMinutes(-30);

            var activeUsers = await _db.Sessions
                .Include(s => s.User)
                .Where(s => s.Last_activity >= threshold && s.Personnel_ID != 0)
                .OrderByDescending(s => s.Last_activity)
                .Select(s => new
                {
                    Session_ID = s.Session_ID,
                    Ip_address = s.Ip_address,
                    Full_name = s.User.Full_name,
                    Last_activity = s.Last_activity.ToString("yyyy-MM-dd HH:mm"),
                    User_agent = s.User_agent
                })
                .ToListAsync();

            return Json(activeUsers);
        }

        [HttpGet("/test-session")]
        public IActionResult TestSession()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("SessionTest")))
            {
                HttpContext.Session.SetString("SessionTest", "initialized");
                return Content($"New Session ID: {HttpContext.Session.Id}");
            }
            else
            {
                return Content($"Same Session ID: {HttpContext.Session.Id}");
            }
        }
    }
}
