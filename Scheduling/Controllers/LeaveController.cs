using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scheduling.Models;
using System.Security.Claims;

namespace Scheduling.Controllers
{
    [Authorize]
    public class LeaveController : Controller
    {
        private readonly ApplicationDbContext _db;

        public LeaveController(ApplicationDbContext context)
        {
            _db = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> Apply(Leave request)
        {
            var existingLeaves = _db.Leaves
                .Where(l => l.Personnel_ID == request.Personnel_ID && l.Status != "Denied")
                .AsEnumerable()
                .Any(l => Overlaps(l.Date_start, l.Date_end, request.Date_start, request.Date_end));

            if (existingLeaves)
            {
                return BadRequest("Existing leave overlaps for this date.");
            }

            var personnelId = int.Parse(User.FindFirstValue("Personnelid"));

            request.Personnel_ID = personnelId;
            request.Status = "Pending";
            _db.Leaves.Add(request);
            await _db.SaveChangesAsync();

            // Redirect to Schedule page
            return RedirectToAction("Index", "Schedule", new { month = request.Date_start.Month, year = request.Date_start.Year });
        }

        // Check if the dates overlap
        public bool Overlaps(DateTime start1, DateTime end1, DateTime start2, DateTime end2)
        {
            return start1 <= end2 && start2 <= end1;
        }

        [HttpPost]
        public IActionResult GetLeave(int Id)
        {
            var leave = _db.Leaves
                .Include(l => l.Leave_type)
                .Include(l => l.User)
                .Include(l => l.Approver)
                .FirstOrDefault(l => l.Leave_ID == Id);

            if (leave == null) return NotFound();

            return Ok(new
            {
                Name = leave.User.First_name + " " + leave.User.Last_name,
                Leave_ID = leave.Leave_ID,
                Leave_type_name = leave?.Leave_type?.Leave_type_name,
                Date_start = leave?.Date_start.ToString("yyyy-MM-dd"),
                Date_end = leave?.Date_end.ToString("yyyy-MM-dd"),
                Approver = leave?.Approver?.First_name + " " + leave?.Approver?.Last_name,
                Comment = leave?.Comment
            });
        }

        public async Task<IActionResult> Approve(int Id)
        {
            var leave = await _db.Leaves.FindAsync(Id);
            if (leave == null) return NotFound();

            leave.Status = "Approved";
            leave.Approved_by = int.Parse(User.FindFirstValue("Personnelid"));
            _db.Leaves.Update(leave);
            await _db.SaveChangesAsync();

            return RedirectToAction("Index", "Schedule", new { month = leave.Date_start.Month, year = leave.Date_start.Year, departmentId = leave?.User?.Department_ID });
        }

        public async Task<IActionResult> Deny(int Id)
        {
            var leave = await _db.Leaves.FindAsync(Id);
            if (leave == null) return NotFound();

            leave.Status = "Denied";
            leave.Approved_by = int.Parse(User.FindFirstValue("Personnelid"));
            _db.Leaves.Update(leave);
            await _db.SaveChangesAsync();

            return RedirectToAction("Index", "Schedule", new { month = leave.Date_start.Month, year = leave.Date_start.Year, departmentId = leave?.User?.Department_ID });
        }
    }
}
