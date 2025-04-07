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
            if (month == 0)
                month = DateTime.Now.Month;

            if (year == 0)
                year = DateTime.Now.Year;

            if (departmentId == 0)
            {
                var user = await ThisUser();
                departmentId = user.Department_ID.Value;
            }

            var shifts = _db.Shifts.ToList();
            var holidays = _db.Holidays.ToList();
            var sectors = _db.Sectors.ToList();

            var userOrder = _db.Employee_orders
                .Where(o => o.Year == year && o.Month == month)
                .OrderBy(o => o.Order_index)
                .Select(o => o.Personnel_ID)
                .ToList();

            var baseQuery = _db.Users
                .Include(u => u.Sector)
                .Where(u =>
                    u.Privilege_ID != 0 &&
                    u.Privilege_ID != 4 &&
                    u.Department_ID == departmentId &&
                    u.Status == 1)
                .ToList(); // Execute once, filter and sort in memory

            var usersInOrder = baseQuery
                .Where(u => userOrder.Contains(u.Personnel_ID))
                .OrderBy(u => userOrder.IndexOf(u.Personnel_ID))
                .ToList();

            var usersNotInOrder = baseQuery
                .Where(u => !userOrder.Contains(u.Personnel_ID))
                .OrderBy(u => u.Sector.Order)
                .ThenByDescending(u => u.Privilege_ID)
                .ThenBy(u => u.First_name)
                .ThenBy(u => u.Last_name)
                .ToList();

            var users = usersInOrder.Concat(usersNotInOrder).ToList();

            var schedules = _db.Schedules
                    .Include(s => s.User)
                    .Include(s => s.Shift)
                    .Where(s => s.Date.Month == month && s.Date.Year == year)
                    .ToList();

            var leaves = _db.Leaves
                .Where(l => 
                    (l.Date_start.Year == year && l.Date_start.Month == month) || 
                    (l.Date_end.Year == year && l.Date_end.Month == month) &&
                    l.Status != "Cancelled" && l.Status != "Denied")
                .ToList();

            ViewBag.Departments = new SelectList(_db.Departments.ToList(), "Department_ID", "Department_name", departmentId);
            ViewBag.LeaveTypes = _db.Leave_types.ToList();

            if (User.IsInRole("member"))
                return View((users, shifts, schedules, leaves, holidays, sectors, month, year));
            else
                return View("Manage", (users, shifts, schedules, leaves, holidays, sectors, month, year));
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
            var shifts = _db.Shifts.ToList();
            var holidays = _db.Holidays.ToList();
            var sectors = _db.Sectors.ToList();

            var userOrder = _db.Employee_orders
                .Where(o => o.Year == year && o.Month == month)
                .OrderBy(o => o.Order_index)
                .Select(o => o.Personnel_ID)
                .ToList();

            var baseQuery = _db.Users
                .Include(u => u.Sector)
                .Where(u =>
                    u.Privilege_ID != 0 &&
                    u.Privilege_ID != 4 &&
                    u.Department_ID == departmentId &&
                    u.Status == 1)
                .ToList(); // Execute once, filter and sort in memory

            var usersInOrder = baseQuery
                .Where(u => userOrder.Contains(u.Personnel_ID))
                .OrderBy(u => userOrder.IndexOf(u.Personnel_ID))
                .ToList();

            var usersNotInOrder = baseQuery
                .Where(u => !userOrder.Contains(u.Personnel_ID))
                .OrderBy(u => u.Sector.Order)
                .ThenByDescending(u => u.Privilege_ID)
                .ThenBy(u => u.First_name)
                .ThenBy(u => u.Last_name)
                .ToList();

            var users = usersInOrder.Concat(usersNotInOrder).ToList();

            var schedules = _db.Schedules
                .Include(s => s.User)
                .Include(s => s.Shift)
                .Where(s => s.Date.Month == month && s.Date.Year == year)
                .ToList();

            var leaves = _db.Leaves
                .Where(l =>
                    (l.Date_start.Year == year && l.Date_start.Month == month) ||
                    (l.Date_end.Year == year && l.Date_end.Month == month) &&
                    l.Status != "Cancelled" && l.Status != "Denied")
                .ToList();

            return PartialView("_ScheduleTable", (users, shifts, schedules, leaves, holidays, sectors, month, year));
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

        [HttpPost]
        public async Task<IActionResult> UpdateEmployeeOrder(string order, int month, int year)
        {
            var index = 1;
            foreach (var item in order.Split(","))
            {
                var userId = int.Parse(item.Split("-")[1]);
                var sample = _db.Employee_orders.FirstOrDefault(e => e.Personnel_ID == 1);

                var employeeOrder = await _db.Employee_orders
                    .FirstOrDefaultAsync(e => e.Personnel_ID == userId && e.Year == year && e.Month == month);

                if (employeeOrder == null)
                {
                    employeeOrder = new Employee_order
                    {
                        Personnel_ID = userId,
                        Year = year,
                        Month = month,
                        Order_index = index
                    };
                    _db.Employee_orders.Add(employeeOrder);
                }
                else
                {
                    employeeOrder.Order_index = index;
                    _db.Employee_orders.Update(employeeOrder);
                }

                index++;
            }

            await _db.SaveChangesAsync();

            return Ok(new { success = true });
        }
    }
}
