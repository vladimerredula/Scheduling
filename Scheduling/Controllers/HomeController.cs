using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scheduling.Models;
using Scheduling.Services;
using System.Diagnostics;
using System.Security.Claims;

namespace Scheduling.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly TemplateService _template;

        public HomeController(ApplicationDbContext db, TemplateService template)
        {
            _db = db;
            _template = template;
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

        [HttpPost]
        public async Task<ActionResult> GetRequestCount()
        {
            if (User.IsInRole("member") || User.IsInRole("shiftLeader"))
                return Json(0);

            var requests = await _db.Leaves
                .Include(l => l.User)
                .Where(l => l.Status == "Pending").ToListAsync();

            if (_template.HasPermission("Department Leaves", "DeptSelect") || User.IsInRole("topManager"))
            {
                requests = requests.Where(l => l.Approver_1 != null && l.Approver_2 == null).ToList();
            }
            else if (User.IsInRole("manager"))
            {
                var user = await ThisUser();

                if (user.Personnel_ID == 35) // TEMPORARY FIX FOR KONSTANTIN
                {
                    requests = requests.Where(l => l.Approver_1 == null && (l?.User?.Department_ID == 2 || l?.User?.Department_ID == 3)).ToList();
                } else
                {
                    requests = requests.Where(l => l.Approver_1 == null && l?.User?.Department_ID == user.Department_ID).ToList();
                }
            }

            return Json(requests.Count());
        }

        [HttpPost]
        public async Task<ActionResult> GetNotifications()
        {
            int personnelId = GetPersonnelID();

            var requests = await _db.Leaves
                .Where(l => l.Notify > 0 && l.Personnel_ID == personnelId)
                .OrderBy(l => l.Notify)
                .ThenByDescending(l => l.Date_approved_2)
                .ThenByDescending(l => l.Date_approved_1)
                .Take(10)
                .ToArrayAsync();

            return Json(requests);
        }

        [HttpPost]
        public async Task<ActionResult> UpdateNotifications()
        {
            int personnelId = GetPersonnelID();

            var requests = await _db.Leaves
                .Where(l => l.Notify == 1 && l.Personnel_ID == personnelId)
                .ToArrayAsync();

            if (requests.Count() == 0)
            {
                return Ok();
            }

            foreach (var notification in requests)
            {
                _db.Update(notification);

                notification.Notify = 2;
            }

            await _db.SaveChangesAsync();

            return Ok();
        }

        [HttpPost]
        public IActionResult ReadNotification(int id)
        {
            var notif = _db.Leaves.Find(id);

            if (notif != null)
            {
                notif.Notify = 3;
                _db.SaveChanges();
            }

            return Ok();
        }

        public int GetPersonnelID()
        {
            var value = User.FindFirstValue("Personnelid");

            return int.TryParse(value, out int personnelId) ? personnelId : 0;
        }

        public async Task<string> GetUsername()
        {
            var user = await ThisUser();
            return user.Username;
        }

        public async Task<string> GetUserFullname(int? id = null)
        {
            User user;

            if (id.HasValue)
                user = await GetUser(id.Value);
            else
                user = await ThisUser();

            return user?.Full_name ?? string.Empty;
        }

        public async Task<User> GetUser(int id)
        {
            return await _db.Users.FindAsync(id);
        }

        public async Task<User> ThisUser()
        {
            var id = GetPersonnelID();
            return await GetUser(id);
        }
    }
}
