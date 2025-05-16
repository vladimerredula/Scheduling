using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Scheduling.Models;
using Scheduling.Services;
using System.Security.Claims;

namespace Scheduling.Controllers
{
    [Authorize]
    public class ScheduleController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly LogService<ScheduleController> _log;

        public ScheduleController(ApplicationDbContext context, LogService<ScheduleController> logger)
        {
            _db = context;
            _log = logger;
        }

        public async Task<IActionResult> Index(int month = 0, int year = 0, int departmentId = 0)
        {
            var user = await ThisUser();

            if (user?.Personnel_ID == 999)
                return RedirectToAction(nameof(ScheduleView));

            var today = DateTime.Now;
            month = (month == 0) ? today.Month : month;
            year = (year == 0) ? today.Year : year;
            departmentId = (departmentId == 0) ? user?.Department_ID ?? 0 : departmentId;

            var shifts = await _db.Shifts
                .Where(s => s.Department_ID == departmentId)
                .ToListAsync();

            var holidays = await _db.Holidays.ToListAsync();

            var departments = await _db.Departments.ToListAsync();
            var leaveTypes = await _db.Leave_types.ToListAsync();

            // Get relevant employee orders for the selected year and month (latest per user)
            var employeeOrders = await _db.Employee_orders
                .Include(o => o.User)
                .Where(o =>
                    o.User.Department_ID == departmentId &&
                    (o.Year < year || (o.Year == year && o.Month <= month)))
                .GroupBy(o => o.Personnel_ID)
                .Select(g => g
                    .OrderByDescending(o => o.Year)
                    .ThenByDescending(o => o.Month)
                    .First())
                .ToListAsync();

            // Map Personnel_ID to Order_index and Sector_ID
            var orderLookup = employeeOrders
                .OrderBy(o => o.Order_index)
                .Select((o, index) => new
                {
                    PersonnelId = o.Personnel_ID,
                    SectorId = o.Sector_ID,
                    Index = index
                })
                .ToList();

            // Get all users in the department
            var baseUsers = await _db.Users
                .Include(u => u.Sector)
                .Where(u =>
                    u.Privilege_ID != 0 &&
                    u.Privilege_ID != 4 &&
                    u.Department_ID == departmentId &&
                    u.Status == 1 &&
                    (u.Date_hired == null || u.Date_hired.Value.Date <= new DateTime(year, month, DateTime.DaysInMonth(year, month)).Date) &&
                    (u.Last_day == null || u.Last_day.Value.Date >= new DateTime(year, month, 1).Date))
                .ToListAsync();

            // Override Sector_IDs based on latest Employee_order
            foreach (var baseUser in baseUsers)
            {
                var match = orderLookup.FirstOrDefault(o => o.PersonnelId == baseUser.Personnel_ID);
                if (match != null && match.SectorId.HasValue)
                {
                    baseUser.Sector_ID = match.SectorId.Value;
                    baseUser.Sector = await _db.Sectors.FindAsync(match.SectorId.Value);
                }
            }

            // Split and sort
            var usersInOrder = baseUsers
                .Where(u => orderLookup.Any(o => o.PersonnelId == u.Personnel_ID))
                .OrderBy(u => orderLookup.First(o => o.PersonnelId == u.Personnel_ID).Index);

            var usersNotInOrder = baseUsers
                .Where(u => orderLookup.All(o => o.PersonnelId != u.Personnel_ID))
                .OrderBy(u => u?.Sector?.Order)
                .ThenByDescending(u => u.Privilege_ID)
                .ThenBy(u => u.First_name)
                .ThenBy(u => u.Last_name);

            // Final user list
            var users = usersInOrder.Concat(usersNotInOrder).ToList();

            // Schedules for selected month/year
            var schedules = await _db.Schedules
                .Include(s => s.User)
                .Include(s => s.Shift)
                .Where(s =>
                    s.Date.Month == month &&
                    s.Date.Year == year &&
                    s.User.Department_ID == departmentId)
                .ToListAsync();

            // Leaves within the selected month/year
            var leaves = await _db.Leaves
                .Include(l => l.Leave_type)
                .Include(l => l.Approver1)
                .Where(l =>
                    ((l.Date_start.Year == year && l.Date_start.Month == month) ||
                    (l.Date_end.Year == year && l.Date_end.Month == month)) &&
                    l.Status != "Cancelled" && l.Status != "Denied")
                .ToListAsync();

            ViewBag.Departments = new SelectList(departments, "Department_ID", "Department_name", departmentId);
            ViewBag.LeaveTypes = leaveTypes;

            await _log.LogInfoAsync("Visited schedules");

            return View((users, shifts, schedules, leaves, holidays, month, year));
        }

        public async Task<IActionResult> Calendar(int month = 0, int year = 0)
        {
            var user = await ThisUser();

            if (user?.Personnel_ID == 999)
            {
                return RedirectToAction(nameof(ScheduleView));
            }

            var today = DateTime.Now;

            if (month == 0) month = today.Month;
            if (year == 0) year = today.Year;

            var holidays = await _db.Holidays.ToListAsync();

            var schedules = await _db.Schedules
                .Include(s => s.User)
                .Include(s => s.Shift)
                .Where(s => s.Personnel_ID == user.Personnel_ID &&
                            s.Date.Month == month &&
                            s.Date.Year == year)
                .ToListAsync();

            var shifts = await _db.Shifts
                .Where(s => s.Department_ID == user.Department_ID)
                .ToListAsync();

            var leaves = await _db.Leaves
                .Include(l => l.Leave_type)
                .Include(l => l.Approver1)
                .Where(l => l.Personnel_ID == user.Personnel_ID &&
                           (l.Date_start.Year == year && l.Date_start.Month == month ||
                            l.Date_end.Year == year && l.Date_end.Month == month) &&
                            l.Status != "Cancelled" &&
                            l.Status != "Denied")
                .ToListAsync();

            ViewBag.LeaveTypes = await _db.Leave_types.ToListAsync();
            await _log.LogInfoAsync("Visited calendar");

            return View((schedules, shifts, leaves, holidays, month, year));
        }

        [HttpGet]
        public async Task<IActionResult> GetCalendar(int month = 0, int year = 0)
        {
            var today = DateTime.Now;

            if (month == 0) month = today.Month;
            if (year == 0) year = today.Year;

            var user = await ThisUser();

            var holidays = await _db.Holidays.ToListAsync();

            var schedules = await _db.Schedules
                .Include(s => s.User)
                .Include(s => s.Shift)
                .Where(s => s.Personnel_ID == user.Personnel_ID &&
                            s.Date.Month == month &&
                            s.Date.Year == year)
                .ToListAsync();

            var shifts = await _db.Shifts
                .Where(s => s.Department_ID == user.Department_ID)
                .ToListAsync();

            var leaves = await _db.Leaves
                .Include(l => l.Leave_type)
                .Include(l => l.Approver1)
                .Where(l => l.Personnel_ID == user.Personnel_ID &&
                           (l.Date_start.Year == year && l.Date_start.Month == month ||
                            l.Date_end.Year == year && l.Date_end.Month == month) &&
                            l.Status != "Cancelled" &&
                            l.Status != "Denied")
                .ToListAsync();

            ViewBag.LeaveTypes = await _db.Leave_types.ToListAsync();
            await _log.LogInfoAsync($"Loaded calendar for {new DateTime(year, month, 1).ToString("MMMM yyyy")}");

            return PartialView("_CalendarTable", (schedules, shifts, leaves, holidays, month, year));
        }

        [HttpPost]
        //[Authorize(Roles = "admin,manager,topManager")]
        public async Task<IActionResult> AssignShift(int userId, int shiftId, DateTime date)
        {
            if (User.IsInRole("user") || (User.IsInRole("shiftLeader") && GetPersonnelID() != 94)) // TEMPORARY FIX FOR JONNEL TO ASSIGN SHIFTS
            {
                return Unauthorized();
            }

            var existingSchedule = await _db.Schedules
                .FirstOrDefaultAsync(s => s.Personnel_ID == userId && s.Date == date);

            var empName = await GetUserFullname(userId) ?? $"User {userId}";
            var shift = await _db.Shifts.FirstOrDefaultAsync(s => s.Shift_ID == shiftId);
            var shiftName = $"'{shift?.Shift_name}' shift" ?? $"Shift_ID: {shiftId}";

            string dateStr = date.ToString("yyyy-MM-dd");

            if (existingSchedule != null)
            {
                switch (shiftId)
                {
                    case 0:
                        _db.Schedules.Remove(existingSchedule);
                        await _log.LogInfoAsync($"Removed the shift of {empName} on {dateStr}");
                        break;

                    case 999:
                        existingSchedule.Comment = "cancelled";
                        await _log.LogInfoAsync($"Cancelled the shift of {empName} on {dateStr}");
                        _db.Schedules.Update(existingSchedule);
                        break;

                    case 998:
                        existingSchedule.Comment = null;
                        await _log.LogInfoAsync($"Uncancelled the shift of {empName} on {dateStr}");
                        _db.Schedules.Update(existingSchedule);
                        break;

                    default:
                        existingSchedule.Shift_ID = shiftId;
                        existingSchedule.Comment = null;

                        await _log.LogInfoAsync($"Updated the shift of {empName} on {dateStr} to {shiftName}");
                        _db.Schedules.Update(existingSchedule);
                        break;
                }
            }
            else
            {
                var newSchedule = new Schedule
                {
                    Personnel_ID = userId,
                    Shift_ID = shiftId,
                    Date = date
                };

                await _db.Schedules.AddAsync(newSchedule);
                await _log.LogInfoAsync($"Assigned {shiftName} to {empName} on {dateStr}");
            }

            await _db.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetScheduleByMonth(int month, int year, int departmentId)
        {
            var user = await ThisUser();

            var today = DateTime.Now;
            month = (month == 0) ? today.Month : month;
            year = (year == 0) ? today.Year : year;
            departmentId = (departmentId == 0) ? user?.Department_ID ?? 0 : departmentId;

            var shifts = await _db.Shifts
                .Where(s => s.Department_ID == departmentId)
                .ToListAsync();

            var holidays = await _db.Holidays.ToListAsync();
            var leaveTypes = await _db.Leave_types.ToListAsync();

            // Get relevant employee orders for the selected year and month (latest per user)
            var employeeOrders = await _db.Employee_orders
                .Include(o => o.User)
                .Where(o =>
                    o.User.Department_ID == departmentId &&
                    (o.Year < year || (o.Year == year && o.Month <= month)))
                .GroupBy(o => o.Personnel_ID)
                .Select(g => g
                    .OrderByDescending(o => o.Year)
                    .ThenByDescending(o => o.Month)
                    .First())
                .ToListAsync();

            // Map Personnel_ID to Order_index and Sector_ID
            var orderLookup = employeeOrders
                .OrderBy(o => o.Order_index)
                .Select((o, index) => new
                {
                    PersonnelId = o.Personnel_ID,
                    SectorId = o.Sector_ID,
                    Index = index
                })
                .ToList();

            // Get all users in the department
            var baseUsers = await _db.Users
                .Include(u => u.Sector)
                .Where(u =>
                    u.Privilege_ID != 0 &&
                    u.Privilege_ID != 4 &&
                    u.Department_ID == departmentId &&
                    u.Status == 1 &&
                    (u.Date_hired == null || u.Date_hired.Value.Date <= new DateTime(year, month, DateTime.DaysInMonth(year, month)).Date) &&
                    (u.Last_day == null || u.Last_day.Value.Date >= new DateTime(year, month, 1).Date))
                .ToListAsync();

            // Override Sector_IDs based on latest Employee_order
            foreach (var baseUser in baseUsers)
            {
                var match = orderLookup.FirstOrDefault(o => o.PersonnelId == baseUser.Personnel_ID);
                if (match != null && match.SectorId.HasValue)
                {
                    baseUser.Sector_ID = match.SectorId.Value;
                    baseUser.Sector = await _db.Sectors.FindAsync(match.SectorId.Value);
                }
            }

            // Split and sort
            var usersInOrder = baseUsers
                .Where(u => orderLookup.Any(o => o.PersonnelId == u.Personnel_ID))
                .OrderBy(u => orderLookup.First(o => o.PersonnelId == u.Personnel_ID).Index);

            var usersNotInOrder = baseUsers
                .Where(u => orderLookup.All(o => o.PersonnelId != u.Personnel_ID))
                .OrderBy(u => u?.Sector?.Order)
                .ThenByDescending(u => u.Privilege_ID)
                .ThenBy(u => u.First_name)
                .ThenBy(u => u.Last_name);

            // Final user list
            var users = usersInOrder.Concat(usersNotInOrder).ToList();

            // Schedules for selected month/year
            var schedules = await _db.Schedules
                .Include(s => s.User)
                .Include(s => s.Shift)
                .Where(s =>
                    s.Date.Month == month &&
                    s.Date.Year == year &&
                    s.User.Department_ID == departmentId)
                .ToListAsync();

            // Leaves within the selected month/year
            var leaves = await _db.Leaves
                .Include(l => l.Leave_type)
                .Include(l => l.Approver1)
                .Where(l =>
                    ((l.Date_start.Year == year && l.Date_start.Month == month) ||
                    (l.Date_end.Year == year && l.Date_end.Month == month)) &&
                    l.Status != "Cancelled" && l.Status != "Denied")
                .ToListAsync();

            ViewBag.LeaveTypes = leaveTypes;

            var model = (users, shifts, schedules, leaves, holidays, month, year);

            var department = _db.Departments?.Find(departmentId)?.Department_name;

            await _log.LogInfoAsync($"Loaded schedule for {department} {new DateTime(year, month, 1).ToString("MMMM yyyy")}");

            return PartialView("_ScheduleTable", model);
        }


        [HttpGet]
        [Authorize]
        public IActionResult PrintSchedule(int month, int year, int departmentId)
        {
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

            return PartialView("_PrintScheduleTable", (users, shifts, schedules, leaves, holidays, month, year));
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

        public async Task<User> ThisUser()
        {
            var id = GetPersonnelID();
            return await GetUser(id);
        }

        public async Task<User> GetUser(int id)
        {
            return await _db.Users.FindAsync(id);
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

            var displayDate = new DateTime(year, month, 1).ToString("MMMM yyyy");
            await _log.LogInfoAsync($"Updated Employee order for {displayDate}, Order:{order}");

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

            // Get relevant employee orders for the selected year and month (latest per user)
            var employeeOrders = await _db.Employee_orders
                .Include(o => o.User)
                .Where(o =>
                    o.User.Department_ID == departmentId &&
                    (o.Year < year || (o.Year == year && o.Month <= month)))
                .GroupBy(o => o.Personnel_ID)
                .Select(g => g
                    .OrderByDescending(o => o.Year)
                    .ThenByDescending(o => o.Month)
                    .First())
                .ToListAsync();

            // Map Personnel_ID to Order_index and Sector_ID
            var orderLookup = employeeOrders
                .OrderBy(o => o.Order_index)
                .Select((o, index) => new
                {
                    PersonnelId = o.Personnel_ID,
                    SectorId = o.Sector_ID,
                    Index = index
                })
                .ToList();

            // Get all users in the department
            var baseUsers = await _db.Users
                .Include(u => u.Sector)
                .Where(u =>
                    u.Privilege_ID != 0 &&
                    u.Privilege_ID != 4 &&
                    u.Department_ID == departmentId &&
                    u.Status == 1 &&
                    (u.Date_hired == null || u.Date_hired.Value.Date <= new DateTime(year, month, DateTime.DaysInMonth(year, month)).Date) &&
                    (u.Last_day == null || u.Last_day.Value.Date >= new DateTime(year, month, 1).Date))
                .ToListAsync();

            // Override Sector_IDs based on latest Employee_order
            foreach (var baseUser in baseUsers)
            {
                var match = orderLookup.FirstOrDefault(o => o.PersonnelId == baseUser.Personnel_ID);
                if (match != null && match.SectorId.HasValue)
                {
                    baseUser.Sector_ID = match.SectorId.Value;
                    baseUser.Sector = await _db.Sectors.FindAsync(match.SectorId.Value);
                }
            }

            // Split and sort
            var usersInOrder = baseUsers
                .Where(u => orderLookup.Any(o => o.PersonnelId == u.Personnel_ID))
                .OrderBy(u => orderLookup.First(o => o.PersonnelId == u.Personnel_ID).Index);

            var usersNotInOrder = baseUsers
                .Where(u => orderLookup.All(o => o.PersonnelId != u.Personnel_ID))
                .OrderBy(u => u?.Sector?.Order)
                .ThenByDescending(u => u.Privilege_ID)
                .ThenBy(u => u.First_name)
                .ThenBy(u => u.Last_name);

            // Final user list
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

            // Get relevant employee orders for the selected year and month (latest per user)
            var employeeOrders = await _db.Employee_orders
                .Include(o => o.User)
                .Where(o =>
                    o.User.Department_ID == departmentId &&
                    (o.Year < year || (o.Year == year && o.Month <= month)))
                .GroupBy(o => o.Personnel_ID)
                .Select(g => g
                    .OrderByDescending(o => o.Year)
                    .ThenByDescending(o => o.Month)
                    .First())
                .ToListAsync();

            // Map Personnel_ID to Order_index and Sector_ID
            var orderLookup = employeeOrders
                .OrderBy(o => o.Order_index)
                .Select((o, index) => new
                {
                    PersonnelId = o.Personnel_ID,
                    SectorId = o.Sector_ID,
                    Index = index
                })
                .ToList();

            // Get all users in the department
            var baseUsers = await _db.Users
                .Include(u => u.Sector)
                .Where(u =>
                    u.Privilege_ID != 0 &&
                    u.Privilege_ID != 4 &&
                    u.Department_ID == departmentId &&
                    u.Status == 1 &&
                    (u.Date_hired == null || u.Date_hired.Value.Date <= new DateTime(year, month, DateTime.DaysInMonth(year, month)).Date) &&
                    (u.Last_day == null || u.Last_day.Value.Date >= new DateTime(year, month, 1).Date))
                .ToListAsync();

            // Override Sector_IDs based on latest Employee_order
            foreach (var baseUser in baseUsers)
            {
                var match = orderLookup.FirstOrDefault(o => o.PersonnelId == baseUser.Personnel_ID);
                if (match != null && match.SectorId.HasValue)
                {
                    baseUser.Sector_ID = match.SectorId.Value;
                    baseUser.Sector = await _db.Sectors.FindAsync(match.SectorId.Value);
                }
            }

            // Split and sort
            var usersInOrder = baseUsers
                .Where(u => orderLookup.Any(o => o.PersonnelId == u.Personnel_ID))
                .OrderBy(u => orderLookup.First(o => o.PersonnelId == u.Personnel_ID).Index);

            var usersNotInOrder = baseUsers
                .Where(u => orderLookup.All(o => o.PersonnelId != u.Personnel_ID))
                .OrderBy(u => u?.Sector?.Order)
                .ThenByDescending(u => u.Privilege_ID)
                .ThenBy(u => u.First_name)
                .ThenBy(u => u.Last_name);

            // Final user list
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
