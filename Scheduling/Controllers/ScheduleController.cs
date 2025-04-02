using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Scheduling.Models;
using System.Security.Claims;

namespace Scheduling.Controllers
{
    [Authorize]
    public class ScheduleController : Controller
    {
        private readonly ApplicationDbContext _db;

        public ScheduleController(ApplicationDbContext context)
        {
            _db = context;
        }

        public async Task<IActionResult> Index(int month = 0, int year = 0, int departmentId = 0)
        {
            var user = await ThisUser();

            if (month == 0)
                month = DateTime.Now.Month;

            if (year == 0)
                year = DateTime.Now.Year;

            if (departmentId == 0)
            {
                departmentId = user.Department_ID.Value;
            }

            var users = _db.Users.Where(u => (u.Privilege_ID == 1 || u.Privilege_ID == 2) && u.Department_ID == departmentId).ToList();
            var shifts = _db.Shifts.ToList();
            var schedules = _db.Schedules.Include(s => s.User).Include(s => s.Shift).Include(s => s.Leave).ToList();
            var holidays = _db.Holidays.ToList();

            ViewBag.Departments = new SelectList(_db.Departments.ToList(), "Department_ID", "Department_name", departmentId);
            ViewBag.Leaves = _db.Leaves.ToList();

            return View((users, shifts, schedules, holidays, month, year, departmentId));
        }

        [Authorize(Roles = "admin,manager")]
        public async Task<IActionResult> Manage(int month = 0, int year = 0, int departmentId = 0)
        {
            var user = await ThisUser();

            if (month == 0)
                month = DateTime.Now.Month;

            if (year == 0)
                year = DateTime.Now.Year;

            if (departmentId == 0)
            {
                departmentId = user.Department_ID.Value;
            }

            var users = _db.Users
                .Where(u => (u.Privilege_ID == 1 || u.Privilege_ID == 2) && u.Department_ID == departmentId)
                .OrderBy(u => u.Sector_ID)
                .ThenBy(u => u.Privilege_ID)
                .ThenBy(u => u.First_name)
                .ThenBy(u => u.Last_name)
                .ToList();
            var shifts = _db.Shifts.ToList();
            var schedules = _db.Schedules.Include(s => s.User).Include(s => s.Shift).Include(s => s.Leave).ToList();
            var holidays = _db.Holidays.ToList();

            ViewBag.Departments = new SelectList(_db.Departments.ToList(), "Department_ID", "Department_name", departmentId);
            ViewBag.Leaves = _db.Leaves.ToList();

            return View((users, shifts, schedules, holidays, month, year, departmentId));
        }

        [HttpPost]
        [Authorize(Roles = "admin,manager")]
        public async Task<IActionResult> AssignShift(int userId, int shiftId, DateTime date)
        {
            var existingSchedule = await _db.Schedules
                .FirstOrDefaultAsync(s => s.Personnel_ID == userId && s.Date == date);

            if (existingSchedule != null)
            {
                if (shiftId == 0)
                {
                    _db.Schedules.Remove(existingSchedule);
                } else
                {
                    // Update existing schedule with new shift
                    existingSchedule.Leave_ID = null;
                    existingSchedule.Shift_ID = shiftId;
                    _db.Schedules.Update(existingSchedule);
                }
            }
            else
            {
                // Create a new schedule
                var newSchedule = new Schedule
                {
                    Personnel_ID = userId,
                    Shift_ID = shiftId,
                    Date = date
                };
                _db.Schedules.Add(newSchedule);
            }

            await _db.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        [Authorize(Roles = "admin,manager")]
        public async Task<IActionResult> AssignLeave(int userId, int leaveId, DateTime date)
        {
            var existingLeave = await _db.Schedules
                .FirstOrDefaultAsync(s => s.Personnel_ID == userId && s.Date == date);

            if (existingLeave != null)
            {
                // Update existing schedule with new leave
                existingLeave.Shift_ID = null;
                existingLeave.Leave_ID = leaveId;
                _db.Schedules.Update(existingLeave);
            }
            else
            {
                // Create a new schedule
                var newSchedule = new Schedule
                {
                    Personnel_ID = userId,
                    Leave_ID = leaveId,
                    Date = date
                };
                _db.Schedules.Add(newSchedule);
            }

            await _db.SaveChangesAsync();

            return Json(new { success = true });
        }

        [Authorize(Roles = "admin,manager")]
        public async Task<IActionResult> AssignLeaves(int userId, int leaveId, DateTime dateStart, DateTime dateEnd, string comment = "")
        {
            for (DateTime date = dateStart; date <= dateEnd; date = date.AddDays(1))
            {
                var existingLeave = await _db.Schedules
                    .FirstOrDefaultAsync(s => s.Personnel_ID == userId && s.Date == date);

                if (existingLeave != null)
                {
                    // Update existing schedule with new leave
                    existingLeave.Shift_ID = null;
                    existingLeave.Leave_ID = leaveId;
                    existingLeave.Comment = comment;
                    _db.Schedules.Update(existingLeave);

                    await _db.SaveChangesAsync();
                }
                else
                {
                    // Create a new schedule
                    var newSchedule = new Schedule
                    {
                        Personnel_ID = userId,
                        Leave_ID = leaveId,
                        Date = date,
                        Comment = comment
                    };

                    _db.Schedules.Add(newSchedule);
                    await _db.SaveChangesAsync();
                }
            }

            var user = await ThisUser();

            return RedirectToAction("Manage", (dateStart.Month, dateStart.Year, user.Department_ID));
        }

        [HttpPost]
        [Authorize(Roles = "admin,manager")]
        public async Task<IActionResult> RemoveShift(int scheduleId)
        {
            var schedule = await _db.Schedules.FindAsync(scheduleId);
            if (schedule != null)
            {
                _db.Schedules.Remove(schedule);
                await _db.SaveChangesAsync();
            }
            return RedirectToAction("Manage");
        }

        [HttpGet]
        [Authorize]
        public IActionResult GetScheduleByMonth(int month, int year, int departmentId)
        {
            var users = _db.Users.Where(u => (u.Privilege_ID == 1 || u.Privilege_ID == 2) && u.Department_ID == departmentId).ToList();
            var shifts = _db.Shifts.ToList();
            var schedules = _db.Schedules
                .Include(s => s.Shift)
                .Include(s => s.Leave)
                .Where(s => s.Date.Year == year && s.Date.Month == month)
                .ToList();

            ViewBag.Leaves = _db.Leaves.ToList();
            var holidays = _db.Holidays.ToList();

            return PartialView("_ScheduleTable", (users, shifts, schedules, holidays, month, year, departmentId));
        }

        public int GetPersonnelID()
        {
            var personnelId = int.Parse(User.FindFirstValue("Personnelid"));

            return personnelId;
        }

        public async Task<string> GetUsername()
        {
            var user = await ThisUser();
            return user.Username;
        }

        public async Task<User> ThisUser()
        {
            return _db.Users.Find(GetPersonnelID());
        }

        [HttpPost]
        public async Task<IActionResult> GetShiftCount(int shiftId, DateTime date)
        {
            var count = await _db.Schedules
                .CountAsync(s => s.Shift_ID == shiftId && s.Date == date);

            return Json(new { count });
        }
    }
}
