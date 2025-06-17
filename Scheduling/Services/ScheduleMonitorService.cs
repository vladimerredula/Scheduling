using Microsoft.EntityFrameworkCore;

namespace Scheduling.Services
{
    public class ScheduleMonitorService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ScheduleMonitorService> _logger;
        private readonly ExcelService _excel;

        public ScheduleMonitorService(IServiceProvider serviceProvider, ILogger<ScheduleMonitorService> logger, ExcelService excel)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _excel = excel;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var exportService = scope.ServiceProvider.GetRequiredService<ExcelService>();

                    var now = DateTime.Now;

                    var expiredTokens = await db.Edit_tokens
                        .Where(t => t.Expiry <= now)
                        .ToListAsync(stoppingToken);

                    foreach (var token in expiredTokens)
                    {
                        try
                        {
                            // Export Excel logic
                            await ExportSchedule(token.Month, token.Year, token.Department_ID, db);

                            _logger.LogInformation($"Exported updated version of the {token.Month}/{token.Year} {token.Department_ID} schedule");

                            // Remove the token after successful export
                            db.Edit_tokens.Remove(token);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Failed to export schedule for Department {token.Department_ID} {token.Month}/{token.Year}");
                        }
                    }

                    await db.SaveChangesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Background worker encountered an error.");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Check every minute
            }
        }

        public async Task ExportSchedule(int month, int year, int departmentId, ApplicationDbContext db)
        {
            var firstDayOfMonth = new DateTime(year, month, 1);
            var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

            // Get relevant employee orders for the selected year and month (latest per user)
            var employeeOrders = await db.Schedule_orders
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
            var baseUsers = await db.Users
                .Include(u => u.Sector)
                .Where(u =>
                    u.Privilege_ID != 0 &&
                    u.Privilege_ID != 4 &&
                    u.Department_ID == departmentId &&
                    u.Status == 1 &&
                    (u.Date_hired == null || u.Date_hired.Value.Date <= lastDayOfMonth.Date) &&
                    (u.Last_day == null || u.Last_day.Value.Date >= firstDayOfMonth.Date))
                .ToListAsync();

            // Override Sector_IDs based on latest Employee_order
            foreach (var baseUser in baseUsers)
            {
                var match = orderLookup.FirstOrDefault(o => o.PersonnelId == baseUser.Personnel_ID);
                if (match != null && match.SectorId.HasValue)
                {
                    baseUser.Sector_ID = match.SectorId.Value;
                    baseUser.Sector = await db.Sectors.FindAsync(match.SectorId.Value);
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

            var schedules = await db.Schedules
                            .Include(sc => sc.Shift)
                            .Where(sc => sc.Date.Month == month && sc.Date.Year == year)
                            .Select(sc => new
                            {
                                Personnel_ID = sc.Personnel_ID,
                                Shift = sc.Shift.Shift_name,
                                Time_in = sc.Time_in,
                                Time_out = sc.Time_out,
                                Comment = sc.Comment,
                                Date = sc.Date
                            })
                            .ToListAsync<dynamic>();

            var shifts = await db.Shifts.Where(s => s.Department_ID == departmentId).ToListAsync();

            var leaves = await db.Leaves
                            .Include(l => l.Leave_type)
                            .Where(l =>
                                l.Status == "Approved" &&
                                l.User.Department_ID == departmentId &&
                                l.Date_start.Date <= lastDayOfMonth.Date &&
                                l.Date_end.Date >= firstDayOfMonth.Date)
                            .ToListAsync();

            var holidays = await db.Holidays.Where(h => h.Date.Year == year && h.Date.Month == month).ToListAsync();

            var department = await db.Departments.FindAsync(departmentId);

            var excelFile = _excel.Schedule(users.ToList<dynamic>(), schedules, shifts, leaves, holidays, department.Department_name, month, year);

            string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "exports", $"{firstDayOfMonth.ToString("yyyy/MM. MMMM")}");
            Directory.CreateDirectory(folderPath); // Ensure the directory exists

            string baseFileName = $"{firstDayOfMonth.ToString("yyyy.MM")} {department.Department_name}";
            string extension = ".xlsx";
            string fileName = baseFileName + extension;
            string fullPath = Path.Combine(folderPath, fileName);

            int count = 1;
            while (System.IO.File.Exists(fullPath))
            {
                fileName = $"{baseFileName}_{count}{extension}";
                fullPath = Path.Combine(folderPath, fileName);
                count++;
            }

            System.IO.File.WriteAllBytes(fullPath, excelFile);
        }
    }
}
