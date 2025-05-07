using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scheduling.Services;

namespace Scheduling.Controllers
{
    public class DownloadController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ExcelService _excel;

        public DownloadController(ApplicationDbContext dbContext)
        {
            _db = dbContext;
            _excel = new ExcelService();
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult DownloadSchedule(int month, int year, int departmentId)
        {

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
                            .Include(sc => sc.Shift)
                            .Where( sc => sc.Date.Month == month && sc.Date.Year == year)
                            .Select(sc => new
                            {
                                Personnel_ID = sc.Personnel_ID,
                                Shift = sc.Shift.Shift_name,
                                Comment = sc.Comment,
                                Date = sc.Date
                            });

            var shifts = _db.Shifts.Where(s => s.Department_ID == departmentId);

            var leaves = _db.Leaves
                            .Include(l => l.Leave_type)
                            .Where(l => 
                            (l.Date_start.Year == year && l.Date_start.Month == month) || (l.Date_end.Year == year && l.Date_end.Month == month) &&
                            l.User.Department_ID == departmentId);

            var holidays = _db.Holidays.Where(h => h.Date.Year == year && h.Date.Month == month);

            var departmentName = _db.Departments.Find(departmentId).Department_name;

            var excelFile = _excel.Schedule(users.ToList<dynamic>(), schedules.ToList<dynamic>(), shifts.ToList(), leaves.ToList(), holidays.ToList(), departmentName, month, year);

            return File(excelFile, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Schedule.xlsx");
        }
    }
}
