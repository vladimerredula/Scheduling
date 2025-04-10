using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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
            ViewBag.Leaves = _db.Leaves
                .Include(l => l.User)
                .Include(l => l.Leave_type)
                .Include(l => l.Approver)
                .Where(l => l.Personnel_ID == GetPersonnelID())
                .ToList();

            ViewBag.LeaveTypes = new SelectList(_db.Leave_types.Where(l => l.Leave_type_ID < 999).ToList(), "Leave_type_ID", "Leave_type_name");

            return View();
        }

        public async Task<IActionResult> Add(Leave request)
        {
            var existingLeaves = _db.Leaves
                .Where(l => l.Personnel_ID == request.Personnel_ID && l.Status != "Denied")
                .AsEnumerable()
                .Any(l => Overlaps(l.Date_start, l.Date_end, request.Date_start, request.Date_end));

            if (existingLeaves)
            {
                ModelState.AddModelError("Leave_ID", "Existing leave overlaps for this date.");
                return View("Index", request);
            }

            await Save(request);

            return RedirectToAction(nameof(Index));
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
            return RedirectToAction("Manage", "Schedule", new { month = request.Date_start.Month, year = request.Date_start.Year });
        }

        public async Task<IActionResult> Cancel(int Id)
        {
            var leave = await _db.Leaves.FindAsync(Id);
            if (leave == null) return NotFound();

            leave.Status = "Cancelled";
            _db.Leaves.Update(leave);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        public async Task<bool> Save(Leave request)
        {
            try
            {
                var personnelId = int.Parse(User.FindFirstValue("Personnelid"));

                request.Personnel_ID = personnelId;
                request.Status = "Pending";
                _db.Leaves.Add(request);
                await _db.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
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
                Name = leave.User.Full_name,
                Leave_ID = leave.Leave_ID,
                Leave_type_ID = leave?.Leave_type_ID,
                Leave_type_name = leave?.Leave_type?.Leave_type_name,
                Date_start = leave?.Date_start_string,
                Date_end = leave?.Date_end_string,
                Approver = leave?.Approver?.Full_name,
                Comment = leave?.Comment
            });
        }

        public async Task<IActionResult> Approve(int Id)
        {
            var leave = await _db.Leaves.FindAsync(Id);
            if (leave == null) return NotFound();

            leave.Status = "Approved";
            leave.Approved_by = int.Parse(User.FindFirstValue("Personnelid"));
            leave.Date_approved = DateTime.Now.Date;
            _db.Leaves.Update(leave);
            await _db.SaveChangesAsync();

            return RedirectToAction("Manage", "Schedule", new { month = leave.Date_start.Month, year = leave.Date_start.Year, departmentId = leave?.User?.Department_ID });
        }

        public async Task<IActionResult> Deny(int Id)
        {
            var leave = await _db.Leaves.FindAsync(Id);
            if (leave == null) return NotFound();

            leave.Status = "Denied";
            leave.Approved_by = int.Parse(User.FindFirstValue("Personnelid"));
            _db.Leaves.Update(leave);
            await _db.SaveChangesAsync();

            return RedirectToAction("Manage", "Schedule", new { month = leave.Date_start.Month, year = leave.Date_start.Year, departmentId = leave?.User?.Department_ID });
        }

        public async Task<IActionResult> AssignCompanyLeave(int userId, DateTime date)
        {
            var personnelId = int.Parse(User.FindFirstValue("Personnelid"));

            var leave = new Leave
            {
                Leave_type_ID = 999,
                Personnel_ID = userId,
                Date_start = date,
                Date_end = date,
                Date_approved = DateTime.Now.Date,
                Approved_by = personnelId,
                Comment = "Assigned by manager",
                Date_reflected = DateTime.Now.Date,
                Status = "Reflected"
            };

            _db.Leaves.Add(leave);
            await _db.SaveChangesAsync();

            // Redirect to Schedule page
            return Ok(new { success = true });
        }

        public int GetPersonnelID()
        {
            var personnelId = int.Parse(User.FindFirstValue("Personnelid"));

            return personnelId;
        }

        public async Task<User> ThisUser()
        {
            return _db.Users.Find(GetPersonnelID());
        }
    }
}
