using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Scheduling.Controllers
{
    [Authorize(Roles = "admin")]
    public class SessionController : Controller
    {
        private readonly ApplicationDbContext _db;

        public SessionController(ApplicationDbContext db)
        {
            _db = db;
        }

        public IActionResult Index() => View();

        [HttpGet]
        public async Task<IActionResult> GetActiveUsers()
        {
            var activeUsers = await _db.Sessions
                .Include(s => s.User)
                .Where(s => s.App_name == "SCH" && s.Signed_out_at == null)
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
    }
}
