using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Scheduling.Models;
using Scheduling.Helpers;
using Scheduling.Services;

namespace Scheduling.Controllers
{
    [Authorize]
    public class ScheduleController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly LogService<ScheduleController> _log; 
        private readonly ScheduleHelper _helper;
        private readonly UserService _user;
        private readonly ScheduleTokenService _token;

        public ScheduleController(
            ApplicationDbContext context, 
            LogService<ScheduleController> logger, 
            ScheduleHelper helper, 
            UserService user,
            ScheduleTokenService token)
        {
            _db = context;
            _log = logger;
            _helper = helper;
            _user = user;
            _token = token;
        }

        public async Task<IActionResult> Index(int month = 0, int year = 0, int departmentId = 0)
        {
            if (_user.GetPersonnelId() == 999)
                return RedirectToAction(nameof(ScheduleView));

            if (month == 0)
                month = DateTime.Now.Month;
            if (year == 0)
                year = DateTime.Now.Year;
            if (departmentId == 0)
                departmentId = await _user.GetDepartmentId();

            ViewBag.LeaveTypes = await GetLeaveTypes();
            ViewBag.Departments = new SelectList(await GetDepartments(), "Department_ID", "Department_name", departmentId);

            return View((month, year));
        }

        public async Task<List<Schedule_order>> GetScheduleOrderIndex(int month, int year, int departmentId)
        {
            var scheduleOrder = await _db.Schedule_orders
                .Include(o => o.User)
                .Where(o =>
                    o.Department_ID == departmentId &&
                    (o.Year < year || (o.Year == year && o.Month <= month)))
                .GroupBy(o => o.Personnel_ID)
                .Select(g => g
                    .OrderByDescending(o => o.Year)
                    .ThenByDescending(o => o.Month)
                    .First())
                .ToListAsync();

            var orderLookup = scheduleOrder
                .OrderBy(o => o.Order_index)
                .Select((o, index) => new Schedule_order
                {
                    Personnel_ID = o.Personnel_ID,
                    Sector_ID = o.Sector_ID,
                    Department_ID = o.Department_ID,
                    Order_index = index
                })
                .ToList();

            return orderLookup;
        }

        public async Task<List<Schedule_shiftleader>> GetScheduleShiftleaders(int month, int year, int departmentId)
        {
            var scheduleShiftleaders = await _db.Schedule_shiftleaders
                .Include(o => o.User)
                .Where(o =>
                    o.Department_ID == departmentId &&
                    (o.Year < year || (o.Year == year && o.Month <= month)))
                .GroupBy(o => o.Personnel_ID)
                .Select(g => g
                    .OrderByDescending(o => o.Year)
                    .ThenByDescending(o => o.Month)
                    .First())
                .ToListAsync();

            return scheduleShiftleaders;
        }

        public async Task<List<Leave>> GetLeavesByMonthAndYear(int month, int year, int? userId = null)
        {
            var leaves = await _db.Leaves
                .Include(l => l.Approver1)
                .Where(l =>
                    ((l.Date_start.Year == year && l.Date_start.Month == month) ||
                    (l.Date_end.Year == year && l.Date_end.Month == month)) &&
                    (l.Status == "Approved" || l.Status == "Pending"))
                .ToListAsync();

            if (userId.HasValue)
            {
                leaves = leaves.Where(l => l.Personnel_ID == userId.Value).ToList();
            }

            return leaves;
        }

        public async Task<IActionResult> Calendar(int month = 0, int year = 0)
        {
            var personnelId = _user.GetPersonnelId();
            var departmentId = await _user.GetDepartmentId();

            if (personnelId == 999)
                return RedirectToAction(nameof(ScheduleView));
            if (month == 0) 
                month = DateTime.Now.Month;
            if (year == 0) 
                year = DateTime.Now.Year;

            ViewBag.LeaveTypes = await GetLeaveTypes();

            return View((month, year));
        }

        [HttpGet]
        public async Task<IActionResult> GetCalendar(int month = 0, int year = 0)
        {
            if (month == 0) 
                month = DateTime.Now.Month;
            if (year == 0) 
                year = DateTime.Now.Year;

            var personnelId = _user.GetPersonnelId();
            var departmentId = await _user.GetDepartmentId();

            var schedules = await _db.Schedules
                .Include(s => s.Shift)
                .Where(s => s.Personnel_ID == personnelId &&
                            s.Date.Month == month &&
                            s.Date.Year == year)
                .ToListAsync();

            var leaves = await GetLeavesByMonthAndYear(month, year, personnelId);

            ViewBag.LeaveTypes = await GetLeaveTypes();
            ViewBag.Shifts = await GetShifts(departmentId);
            ViewBag.Holidays = await GetHoldays(month);

            await _log.LogInfoAsync($"Loaded calendar for {_helper.DisplayMonthYear(month, year)}");

            return PartialView("_CalendarTable", (schedules, leaves, month, year));
        }

        [HttpPost]
        public async Task<IActionResult> AssignShift(int userId, int shiftId, DateTime date)
        {
            var existingSchedule = await _db.Schedules
                .Include(s => s.Shift)
                .FirstOrDefaultAsync(s => s.Personnel_ID == userId && s.Date == date);

            var empName = await _user.GetFullname(userId) ?? $"User {userId}";
            var shift = (await GetShift(shiftId))?.Shift_name ?? string.Empty;
            var shiftName = shift != string.Empty 
                ? $"'{shift}' shift" 
                : $"Shift_ID: {shiftId}";

            string dateStr = _helper.ToDateStr(date);

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
                        var prevShift = existingSchedule.Shift_ID.HasValue 
                            ? await GetShift(existingSchedule.Shift_ID.Value) 
                            : null;
                        var prevShiftName = prevShift != null ? prevShift.Shift_name : string.Empty;

                        existingSchedule.Shift_ID = shiftId;
                        existingSchedule.Comment = null;

                        await _log.LogInfoAsync($"Updated the shift of {empName} on {dateStr} from '{prevShiftName}' to {shiftName}");
                        _db.Schedules.Update(existingSchedule);
                        break;
                }
            }
            else
            {
                await _db.Schedules.AddAsync(new Schedule {
                    Personnel_ID = userId,
                    Shift_ID = shiftId,
                    Date = date
                });

                await _log.LogInfoAsync($"Assigned {shiftName} to {empName} on {dateStr}");
            }

            await _db.SaveChangesAsync();
            await _token.UpdateTokenAsync(await _user.GetDepartmentId(userId), date.Year, date.Month);

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> AssignTime(int userId, string time, DateTime date, bool isTimeIn)
        {
            var empName = await _user.GetFullname(userId) ?? $"User {userId}";
            var dateStr = _helper.ToDateStr(date);
            var parsedTime = TimeSpan.TryParse(time, out var t) ? t : (TimeSpan?)null;
            var timeStr = parsedTime?.ToString(@"hh\:mm") ?? "[blank]";
            var timeType = isTimeIn ? "in" : "out";

            var schedule = await _db.Schedules
                .FirstOrDefaultAsync(s => s.Personnel_ID == userId && s.Date == date);

            if (schedule == null)
            {
                if (parsedTime == null)
                    return Json(new { success = true }); // Nothing to assign and no existing record

                schedule = new Schedule
                {
                    Personnel_ID = userId,
                    Date = date
                };

                if (isTimeIn)
                    schedule.Time_in = parsedTime;
                else
                    schedule.Time_out = parsedTime;

                await _db.Schedules.AddAsync(schedule);
                await _log.LogInfoAsync($"Assigned {timeStr} time {timeType} to {empName} on {dateStr}");
            }
            else
            {
                if (isTimeIn)
                    schedule.Time_in = parsedTime;
                else
                    schedule.Time_out = parsedTime;

                if (schedule.Time_in == null && schedule.Time_out == null)
                {
                    _db.Schedules.Remove(schedule);
                    await _log.LogInfoAsync($"Removed schedule for {empName} on {dateStr} as both time in and out are blank");
                }
                else
                {
                    _db.Schedules.Update(schedule);
                    await _log.LogInfoAsync($"Updated the time {timeType} of {empName} on {dateStr} to {timeStr}");
                }
            }

            await _token.UpdateTokenAsync(await _user.GetDepartmentId(userId), date.Year, date.Month);
            await _db.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetScheduleByMonth(int month, int year, int departmentId)
        {
            if (month == 0)
                month = DateTime.Now.Month;

            if (year == 0)
                year = DateTime.Now.Year;

            if (departmentId == 0)
                departmentId = await _user.GetDepartmentId();

            var department = _db.Departments?.Find(departmentId)?.Department_name;
            var scheduleOrder = await GetScheduleOrderIndex(month, year, departmentId);
            var scheduleShiftleaders = await GetScheduleShiftleaders(month, year, departmentId);
            var baseUsers = await _helper.GetBaseUsers(month, year, departmentId);
            var users = _helper.OverrideSectorAndPrivilege(baseUsers, scheduleOrder, scheduleShiftleaders);
            var leaves = await GetLeavesByMonthAndYear(month, year);
            var schedules = await _db.Schedules
                .Include(s => s.Shift)
                .Where(s =>
                    s.Date.Month == month &&
                    s.Date.Year == year &&
                    users.Select(u => u.Personnel_ID).Contains(s.Personnel_ID))
                .ToListAsync();


            ViewBag.ShiftLeaders = _helper.CalculateShiftLeaders(schedules, scheduleShiftleaders, leaves);
            ViewBag.LeaveTypes = await GetLeaveTypes();
            ViewBag.Shifts = await GetShifts(departmentId);
            ViewBag.Sectors = await GetSectors(departmentId);
            ViewBag.Holidays = await GetHoldays(month);


            await _log.LogInfoAsync($"Loaded schedule for {department} {_helper.DisplayMonthYear(month, year)}");

            return PartialView("_ScheduleTable", (users, schedules, leaves, month, year));
        }

        [HttpPost]
        public async Task<IActionResult> GetShiftLeaders(int month, int year, int departmentId)
        {
            var scheduleShiftleaders = await GetScheduleShiftleaders(month, year, departmentId);
            var baseUsers = await _helper.GetBaseUsers(month, year, departmentId);

            // Override Sector_IDs and Privilege_IDs based on latest Employee_order
            foreach (var baseUser in baseUsers)
            {
                var match = scheduleShiftleaders.FirstOrDefault(o => o.Personnel_ID == baseUser.Personnel_ID);
                if (match != null && match.Is_shiftleader.HasValue)
                    baseUser.Privilege_ID = match.Is_shiftleader.Value ? 2 : 1;
            }

            // Final user list
            var shiftleaders = baseUsers.Where(o => o.Privilege_ID == 2).Select(o => new
            {
                Personnel_ID = o.Personnel_ID,
                Full_name = o.Full_name
            }).ToList();

            return Json(shiftleaders);
        }

        public async Task<List<Shift>> GetShifts(int? departmentId = null)
        {
            var shifts = await _db.Shifts.ToListAsync();

            if (departmentId.HasValue)
                shifts = shifts
                    .Where(s => s.Department_ID == departmentId)
                    .ToList();

            return shifts;
        }

        public async Task<Shift?> GetShift(int shiftId)
        {
            var shifts = await GetShifts();

            return shifts.FirstOrDefault(s => s.Shift_ID == shiftId);
        }

        public async Task<List<Sector>> GetSectors(int? departmentId = null)
        {
            var sectors = await _db.Sectors.ToListAsync();

            if (departmentId.HasValue)
                sectors = sectors
                    .Where(s => s.Department_ID == departmentId)
                    .ToList();

            return sectors;
        }

        public async Task<List<Department>> GetDepartments()
        {
            return await _db.Departments.ToListAsync();
        }

        public async Task<List<Leave_type>> GetLeaveTypes()
        {
            return await _db.Leave_types.ToListAsync();
        }

        public async Task<List<Holiday>> GetHoldays(int month)
        {
            return await _db.Holidays
                .Where(h => h.Date.Month == month)
                .ToListAsync();
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
            var departmentId = 0;

            var index = 1;
            foreach (var item in order.Split(","))
            {
                var userId = int.Parse(item.Split("-")[0]);
                var sectorId = int.Parse(item.Split("-")[1]);
                var employee = await _user.GetUser(userId);

                var scheduleOrder = await _db.Schedule_orders
                    .FirstOrDefaultAsync(e => e.Personnel_ID == userId && e.Year == year && e.Month == month);

                if (scheduleOrder == null)
                {
                    scheduleOrder = new Schedule_order
                    {
                        Personnel_ID = userId,
                        Year = year,
                        Month = month,
                        Order_index = index,
                        Sector_ID = employee?.Sector_ID,
                        Department_ID = employee?.Department_ID
                    };
                    _db.Schedule_orders.Add(scheduleOrder);
                }
                else
                {
                    scheduleOrder.Order_index = index;
                    scheduleOrder.Sector_ID = sectorId;

                    if (scheduleOrder.Department_ID == null)
                        scheduleOrder.Department_ID = employee?.Department_ID;

                    _db.Schedule_orders.Update(scheduleOrder);
                }

                if (departmentId == 0)
                    departmentId = employee?.Department_ID ?? 0;

                index++;
            }

            await _db.SaveChangesAsync();
            await _token.UpdateTokenAsync(departmentId, year, month);
            await _log.LogInfoAsync($"Updated Employee order for {_helper.DisplayMonthYear(month, year)}, Order:{order}");

            return Ok(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> AssignShiftLeader(int month, int year, int userId)
        {
            await UpdateShiftLeaderStatus(month, year, userId, true);
            await _log.LogInfoAsync($"Assigned {await _user.GetFullname(userId)} as shift leader for {_helper.DisplayMonthYear(month, year)}");
            await _token.UpdateTokenAsync(await _user.GetDepartmentId(userId), year, month);

            return Ok(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> RemoveShiftLeader(int month, int year, int userId)
        {
            await UpdateShiftLeaderStatus(month, year, userId, false);
            await _token.UpdateTokenAsync(await _user.GetDepartmentId(userId), year, month);
            await _log.LogInfoAsync($"Removed {await _user.GetFullname(userId)} from the {_helper.DisplayMonthYear(month, year)} shift leader list");

            return Ok(new { success = true });
        }

        private async Task UpdateShiftLeaderStatus(int month, int year, int userId, bool isLeader)
        {
            var shiftleader = await _db.Schedule_shiftleaders
                .FirstOrDefaultAsync(e => e.Personnel_ID == userId && e.Year == year && e.Month == month);

            if (shiftleader == null)
            {
                await _db.Schedule_shiftleaders.AddAsync(new Schedule_shiftleader
                {
                    Personnel_ID = userId,
                    Year = year,
                    Month = month,
                    Department_ID = await _user.GetDepartmentId(userId),
                    Is_shiftleader = isLeader
                });
            }
            else
            {
                shiftleader.Is_shiftleader = isLeader;
                _db.Schedule_shiftleaders.Update(shiftleader);
            }

            await _db.SaveChangesAsync();
            await _token.UpdateTokenAsync(await _user.GetDepartmentId(userId), year, month);
        }

        [HttpPost]
        public async Task<IActionResult> SetShiftLeader(int userId, DateTime date)
        {
            var existingSchedule = await _db.Schedules
                .Include(s => s.Shift)
                .FirstOrDefaultAsync(s => s.Personnel_ID == userId && s.Date == date);

            var empName = await _user.GetFullname(userId) ?? $"User {userId}";
            string dateStr = _helper.ToDateStr(date);

            await RemoveCurrentShiftLeader(userId, date);

            if (existingSchedule != null)
            {
                existingSchedule.Is_shiftleader = true;

                await _log.LogInfoAsync($"Assigned {empName} as shift '{existingSchedule.Shift?.Shift_name}' shiftleader for {dateStr}");
                _db.Schedules.Update(existingSchedule);
            }
            else
            {
                await _db.Schedules.AddAsync(new Schedule
                {
                    Personnel_ID = userId,
                    Date = date,
                    Is_shiftleader = true
                });
                await _log.LogInfoAsync($"Assigned {empName} as shiftleader for {dateStr}");
            }

            await _db.SaveChangesAsync();
            await _token.UpdateTokenAsync(await _user.GetDepartmentId(userId), date.Year, date.Month);

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> UnsetShiftLeader(int userId, DateTime date)
        {
            var existingSchedule = await _db.Schedules
                .Include(s => s.Shift)
                .FirstOrDefaultAsync(s => s.Personnel_ID == userId && s.Date == date);

            var empName = await _user.GetFullname(userId) ?? $"User {userId}";
            string dateStr = _helper.ToDateStr(date);

            await RemoveCurrentShiftLeader(userId, date);

            if (existingSchedule != null)
            {
                existingSchedule.Is_shiftleader = false;

                await _log.LogInfoAsync($"Removed {empName} as shift '{existingSchedule.Shift?.Shift_name}' shiftleader for {dateStr}");
                _db.Schedules.Update(existingSchedule);
            }
            else
            {
                await _db.Schedules.AddAsync(new Schedule
                {
                    Personnel_ID = userId,
                    Date = date,
                    Is_shiftleader = false
                });
                await _log.LogInfoAsync($"Removed {empName} as shiftleader for {dateStr}");
            }

            await _db.SaveChangesAsync();
            await _token.UpdateTokenAsync(await _user.GetDepartmentId(userId), date.Year, date.Month);

            return Json(new { success = true });
        }

        public async Task<bool> RemoveCurrentShiftLeader(int userId, DateTime date)
        {
            var userSchedule = await _db.Schedules
                .FirstOrDefaultAsync(s => s.Personnel_ID == userId && s.Date == date);
            if (userSchedule == null) 
                return false;

            var userOrder = await _db.Schedule_orders
                .FirstOrDefaultAsync(o => o.Year == date.Year && o.Month == date.Month && o.Personnel_ID == userId);
            if (userOrder == null) 
                return false;

            int departmentId = userOrder.Department_ID ?? 0;

            var departmentUserIds = (await GetScheduleOrderIndex(date.Month, date.Year, departmentId))
                .Select(o => o.Personnel_ID)
                .ToList();

            var currentLeaders = await _db.Schedules
                .Where(s =>
                    s.Date == date &&
                    s.Shift_ID == userSchedule.Shift_ID &&
                    departmentUserIds.Contains(s.Personnel_ID) &&
                    s.Is_shiftleader == true)
                .ToListAsync();

            foreach (var sched in currentLeaders)
                sched.Is_shiftleader = false;

            _db.Schedules.UpdateRange(currentLeaders);
            await _db.SaveChangesAsync();

            return true;
        }

        public async Task<IActionResult> ScheduleView(int month = 0, int year = 0, int departmentId = 0)
        {
            if (month == 0)
                month = DateTime.Now.Month;
            if (year == 0)
                year = DateTime.Now.Year;
            if (departmentId == 0)
                departmentId = await _user.GetDepartmentId();

            ViewBag.Departments = new SelectList(await GetDepartments(), "Department_ID", "Department_name", departmentId);

            return View((month, year));
        }

        public async Task<IActionResult> LoadScheduleView(int month = 0, int year = 0, int departmentId = 0)
        {
            if (month == 0)
                month = DateTime.Now.Month;
            if (year == 0)
                year = DateTime.Now.Year;
            if (departmentId == 0)
                departmentId = await _user.GetDepartmentId();

            var scheduleOrder = await GetScheduleOrderIndex(month, year, departmentId);
            var scheduleShiftleaders = await GetScheduleShiftleaders(month, year, departmentId);
            var baseUsers = await _helper.GetBaseUsers(month, year, departmentId);
            var users = _helper.OverrideSectorAndPrivilege(baseUsers, scheduleOrder, scheduleShiftleaders);

            var schedules = await _db.Schedules
                .Include(s => s.Shift)
                .Where(s =>
                    s.Date.Month == month &&
                    s.Date.Year == year &&
                    users.Select(u => u.Personnel_ID).Contains(s.Personnel_ID))
                .ToListAsync();

            var leaves = await _db.Leaves
                .Include(l => l.Approver1)
                .Where(l =>
                    ((l.Date_start.Year == year && l.Date_start.Month == month) ||
                    (l.Date_end.Year == year && l.Date_end.Month == month)) &&
                    l.Status == "Approved")
                .ToListAsync();

            ViewBag.ShiftLeaders = _helper.CalculateShiftLeaders(schedules, scheduleShiftleaders, leaves);
            ViewBag.LeaveTypes = await GetLeaveTypes();
            ViewBag.Shifts = await GetShifts(departmentId);
            ViewBag.Sectors = await GetSectors(departmentId);
            ViewBag.Holidays = await GetHoldays(month);

            return PartialView("_ScheduleView", (users, schedules, leaves, month, year));
        }
    }
}
