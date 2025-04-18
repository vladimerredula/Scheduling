﻿using Microsoft.AspNetCore.Authorization;
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

            if (user?.Personnel_ID == 999)
            {
                return RedirectToAction(nameof(ScheduleView));
            }

            if (month == 0)
                month = DateTime.Now.Month;

            if (year == 0)
                year = DateTime.Now.Year;

            if (departmentId == 0)
            {
                departmentId = user.Department_ID.Value;
            }

            var shifts = _db.Shifts.Where(s => s.Department_ID == departmentId).ToList();
            var holidays = _db.Holidays.ToList();

            var userOrder = _db.Employee_orders
                .Include(o => o.User)
                .Where(o =>
                    o.User.Department_ID == departmentId &&
                    (o.Year < year || (o.Year == year && o.Month <= month))) // Filter for orders earlier or equal to the selected month and year
                .OrderBy(o => o.Year)  // Order by Year first to get the latest orders
                .ThenBy(o => o.Month)  // Then by Month within the same year
                .ThenBy(o => o.Order_index) // Optional: If you want to order by Order_index
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
                .OrderBy(u => u?.Sector?.Order)
                .ThenByDescending(u => u.Privilege_ID)
                .ThenBy(u => u.First_name)
                .ThenBy(u => u.Last_name)
                .ToList();

            var users = usersInOrder.Concat(usersNotInOrder).ToList();

            var schedules = _db.Schedules
                    .Include(s => s.User)
                    .Include(s => s.Shift)
                .Where(s =>
                    s.Date.Month == month &&
                    s.Date.Year == year &&
                    s.User.Department_ID == departmentId)
                    .ToList();

            var leaves = _db.Leaves
                .Include(l => l.Leave_type)
                .Include(l => l.Approver1)
                .Where(l => 
                    ((l.Date_start.Year == year && l.Date_start.Month == month) || 
                    (l.Date_end.Year == year && l.Date_end.Month == month)) &&
                    l.Status != "Cancelled" && l.Status != "Denied")
                .ToList();

            ViewBag.Departments = new SelectList(_db.Departments.ToList(), "Department_ID", "Department_name", departmentId);
            ViewBag.LeaveTypes = _db.Leave_types.ToList();

            if (User.IsInRole("member") || User.IsInRole("shiftLeader"))
                return View((users, shifts, schedules, leaves, holidays, month, year));
            else
                return View("Manage", (users, shifts, schedules, leaves, holidays, month, year));
        }

        public async Task<IActionResult> Calendar(int month = 0, int year = 0)
        {
            var user = await ThisUser();

            if (user?.Personnel_ID == 999)
            {
                return RedirectToAction(nameof(ScheduleView));
            }

            if (month == 0)
                month = DateTime.Now.Month;

            if (year == 0)
                year = DateTime.Now.Year;

            var holidays = _db.Holidays.ToList();

            var schedules = _db.Schedules
                .Include(s => s.User)
                .Include(s => s.Shift)
                .Where(s =>
                    s.Date.Month == month &&
                    s.Date.Year == year &&
                    s.Personnel_ID == user.Personnel_ID)
                .ToList();

            var shifts = _db.Shifts.Where(s => s.Department_ID == user.Department_ID).ToList();

            var leaves = _db.Leaves
                .Include(l => l.Leave_type)
                .Include(l => l.Approver1)
                .Where(l =>
                    ((l.Date_start.Year == year && l.Date_start.Month == month) ||
                    (l.Date_end.Year == year && l.Date_end.Month == month)) &&
                    l.Status != "Cancelled" && l.Status != "Denied" &&
                    l.Personnel_ID == user.Personnel_ID)
                .ToList();

            ViewBag.LeaveTypes = _db.Leave_types.ToList();

            return View((schedules, shifts, leaves, holidays, month, year));
        }

        [HttpGet]
        public async Task<IActionResult> GetCalendar(int month = 0, int year = 0)
        {
            if (month == 0)
                month = DateTime.Now.Month;

            if (year == 0)
                year = DateTime.Now.Year;

            var holidays = _db.Holidays.ToList();
            var user = await ThisUser();

            var schedules = _db.Schedules
                .Include(s => s.User)
                .Include(s => s.Shift)
                .Where(s =>
                    s.Date.Month == month &&
                    s.Date.Year == year &&
                    s.Personnel_ID == user.Personnel_ID)
                .ToList();

            var shifts = _db.Shifts.Where(s => s.Department_ID == user.Department_ID).ToList();

            var leaves = _db.Leaves
                .Include(l => l.Leave_type)
                .Include(l => l.Approver1)
                .Where(l =>
                    ((l.Date_start.Year == year && l.Date_start.Month == month) ||
                    (l.Date_end.Year == year && l.Date_end.Month == month)) &&
                    l.Status != "Cancelled" && l.Status != "Denied" &&
                    l.Personnel_ID == user.Personnel_ID)
                .ToList();

            ViewBag.LeaveTypes = _db.Leave_types.ToList();

            return PartialView("_CalendarTable", (schedules, shifts, leaves, holidays, month, year));
        }

        [HttpPost]
        [Authorize(Roles = "admin,manager,topManager")]
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
                    if (shiftId == 999) // Cancelled shift by manager
                    {
                        existingSchedule.Comment = "cancelled";
                    }
                    else if (shiftId == 998)
                    {
                        existingSchedule.Comment = null;
                    }
                    else
                    {
                        // Update existing schedule with new shift
                        existingSchedule.Shift_ID = shiftId;
                        existingSchedule.Comment = null;
                    }

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

                await _db.Schedules.AddAsync(newSchedule);
            }

            await _db.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        [Authorize(Roles = "admin,manager,topManager")]
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
        public async Task<IActionResult> GetScheduleByMonth(int month, int year, int departmentId)
        {
            var user = await ThisUser();
            var shifts = _db.Shifts.Where(s => s.Department_ID == departmentId).ToList();
            var holidays = _db.Holidays.ToList();

            var userOrder = _db.Employee_orders
                .Include(o => o.User)
                .Where(o =>
                    o.User.Department_ID == departmentId &&
                    (o.Year < year || (o.Year == year && o.Month <= month))) // Filter for orders earlier or equal to the selected month and year
                .OrderBy(o => o.Year)  // Order by Year first to get the latest orders
                .ThenBy(o => o.Month)  // Then by Month within the same year
                .ThenBy(o => o.Order_index) // Optional: If you want to order by Order_index
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
                .OrderBy(u => u?.Sector?.Order)
                .ThenByDescending(u => u.Privilege_ID)
                .ThenBy(u => u.First_name)
                .ThenBy(u => u.Last_name)
                .ToList();

            var users = usersInOrder.Concat(usersNotInOrder).ToList();

            var schedules = _db.Schedules
                .Include(s => s.User)
                .Include(s => s.Shift)
                .Where(s =>
                    s.Date.Month == month &&
                    s.Date.Year == year &&
                    s.User.Department_ID == departmentId)
                .ToList();

            var leaves = _db.Leaves
                .Include(l => l.Leave_type)
                .Include(l => l.Approver1)
                .Where(l =>
                    ((l.Date_start.Year == year && l.Date_start.Month == month) ||
                    (l.Date_end.Year == year && l.Date_end.Month == month)) &&
                    l.Status != "Cancelled" && l.Status != "Denied")
                .ToList();

            ViewBag.LeaveTypes = _db.Leave_types.ToList();

            return PartialView("_ScheduleTable", (users, shifts, schedules, leaves, holidays, month, year));
        }

            var userOrder = _db.Employee_orders
                .Include(o => o.User)
                .Where(o =>
                    o.User.Department_ID == departmentId &&
                    (o.Year < year || (o.Year == year && o.Month <= month))) // Filter for orders earlier or equal to the selected month and year
                .OrderBy(o => o.Year)  // Order by Year first to get the latest orders
                .ThenBy(o => o.Month)  // Then by Month within the same year
                .ThenBy(o => o.Order_index) // Optional: If you want to order by Order_index
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
                .OrderBy(u => u?.Sector?.Order)
                .ThenByDescending(u => u.Privilege_ID)
                .ThenBy(u => u.First_name)
                .ThenBy(u => u.Last_name)
                .ToList();

            var users = usersInOrder.Concat(usersNotInOrder).ToList();

            var schedules = _db.Schedules
                .Include(s => s.User)
                .Include(s => s.Shift)
                .Where(s =>
                    s.Date.Month == month &&
                    s.Date.Year == year &&
                    s.User.Department_ID == departmentId)
                .ToList();

            var leaves = _db.Leaves
                .Include(l => l.Leave_type)
                .Include(l => l.Approver1)
                .Where(l =>
                    ((l.Date_start.Year == year && l.Date_start.Month == month) ||
                    (l.Date_end.Year == year && l.Date_end.Month == month)) &&
                    l.Status != "Cancelled" && l.Status != "Denied")
                .ToList();

            ViewBag.LeaveTypes = _db.Leave_types.ToList();

            return PartialView("_PrintScheduleTable", (users, shifts, schedules, leaves, holidays, month, year));
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
                var userId = int.Parse(item.Split("-")[0]);
                var sectorId = int.Parse(item.Split("-")[1]);


                var employeeOrder = await _db.Employee_orders
                    .FirstOrDefaultAsync(e => e.Personnel_ID == userId && e.Year == year && e.Month == month);

                if (employeeOrder == null)
                {
                    employeeOrder = new Employee_order
                    {
                        Personnel_ID = userId,
                        Year = year,
                        Month = month,
                        Order_index = index,
                        Sector_ID = sectorId
                    };
                    _db.Employee_orders.Add(employeeOrder);
                }
                else
                {
                    employeeOrder.Order_index = index;
                    employeeOrder.Sector_ID = sectorId;
                    _db.Employee_orders.Update(employeeOrder);
                }

                index++;
            }

            await _db.SaveChangesAsync();

            return Ok(new { success = true });
        }

        public async Task<IActionResult> ScheduleView(int month = 0, int year = 0, int departmentId = 0)
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

            var shifts = _db.Shifts.Where(s => s.Department_ID == departmentId).ToList();
            var holidays = _db.Holidays.ToList();

            var userOrder = _db.Employee_orders
                .Include(o => o.User)
                .Where(o =>
                    o.User.Department_ID == departmentId &&
                    (o.Year < year || (o.Year == year && o.Month <= month))) // Filter for orders earlier or equal to the selected month and year
                .OrderBy(o => o.Year)  // Order by Year first to get the latest orders
                .ThenBy(o => o.Month)  // Then by Month within the same year
                .ThenBy(o => o.Order_index) // Optional: If you want to order by Order_index
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
                .OrderBy(u => u?.Sector?.Order)
                .ThenByDescending(u => u.Privilege_ID)
                .ThenBy(u => u.First_name)
                .ThenBy(u => u.Last_name)
                .ToList();

            var users = usersInOrder.Concat(usersNotInOrder).ToList();

            var schedules = _db.Schedules
                .Include(s => s.User)
                .Include(s => s.Shift)
                .Where(s => 
                    s.Date.Month == month && 
                    s.Date.Year == year && 
                    s.User.Department_ID == departmentId)
                .ToList();

            var leaves = _db.Leaves
                .Include(l => l.Leave_type)
                .Include(l => l.Approver1)
                .Where(l =>
                    ((l.Date_start.Year == year && l.Date_start.Month == month) ||
                    (l.Date_end.Year == year && l.Date_end.Month == month)) &&
                    l.Status == "Approved")
                .ToList();

            ViewBag.Departments = new SelectList(_db.Departments.ToList(), "Department_ID", "Department_name", departmentId);
            ViewBag.LeaveTypes = _db.Leave_types.ToList();

            return View((users, shifts, schedules, leaves, holidays, month, year));
        }

        public async Task<IActionResult> LoadScheduleView(int month = 0, int year = 0, int departmentId = 0)
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

            var shifts = _db.Shifts.Where(s => s.Department_ID == departmentId).ToList();
            var holidays = _db.Holidays.ToList();

            var userOrder = _db.Employee_orders
                .Include(o => o.User)
                .Where(o =>
                    o.User.Department_ID == departmentId &&
                    (o.Year < year || (o.Year == year && o.Month <= month))) // Filter for orders earlier or equal to the selected month and year
                .OrderBy(o => o.Year)  // Order by Year first to get the latest orders
                .ThenBy(o => o.Month)  // Then by Month within the same year
                .ThenBy(o => o.Order_index) // Optional: If you want to order by Order_index
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
                .OrderBy(u => u?.Sector?.Order)
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
                .Include(l => l.Leave_type)
                .Include(l => l.Approver1)
                .Where(l =>
                    ((l.Date_start.Year == year && l.Date_start.Month == month) ||
                    (l.Date_end.Year == year && l.Date_end.Month == month)) &&
                    l.Status == "Approved")
                .ToList();

            ViewBag.LeaveTypes = _db.Leave_types.ToList();

            return PartialView("_ScheduleView", (users, shifts, schedules, leaves, holidays, month, year));
        }
    }
}
