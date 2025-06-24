using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scheduling.Models;
using System.Net.Http.Headers;

namespace Scheduling.Services
{
    public class ScheduleMonitorService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ScheduleMonitorService> _logger;
        private readonly ExcelService _excel;
        private readonly SchBackupSettings _settings;

        public ScheduleMonitorService(
            IServiceProvider serviceProvider, 
            ILogger<ScheduleMonitorService> logger, 
            ExcelService excel, 
            IOptions<SchBackupSettings> options)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _excel = excel;
            _settings = options.Value;
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
                            // Export Excel
                            var file = await ExportSchedule(token.Month, token.Year, token.Department_ID, db);

                            var firstDayOfMonth = new DateTime(token.Year, token.Month, 1);
                            var department = await db.Departments.FindAsync(token.Department_ID);

                            var fileName = $"{firstDayOfMonth.ToString("yyyy.MM")} {department?.Department_name}";
                            var folderPath = $"{firstDayOfMonth.ToString("yyyy/MM. MMMM")}";

                            await SaveToLocal(file, fileName, folderPath);
                            await UploadToNas(file, fileName, folderPath);

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

        public async Task<byte[]> ExportSchedule(int month, int year, int departmentId, ApplicationDbContext db)
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

            return _excel.Schedule(users.ToList<dynamic>(), schedules, shifts, leaves, holidays, department.Department_name, month, year);
        }

        public async Task<string> SaveToLocal(byte[] file, string baseFileName, string baseFilePath)
        {
            try
            {
                string folderPath = Path.Combine(_settings.LocalPath, baseFilePath);
                Directory.CreateDirectory(folderPath); // Ensure the directory exists

                string extension = ".xlsx";
                string fileName = baseFileName + extension;
                string fullPath = Path.Combine(folderPath, fileName);

                int count = 1;
                while (File.Exists(fullPath))
                {
                    fileName = $"{baseFileName}_{count}{extension}";
                    fullPath = Path.Combine(folderPath, fileName);
                    count++;
                }

                await File.WriteAllBytesAsync(fullPath, file);
                _logger.LogInformation($"File saved locally to {fullPath}");

                return fileName;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error saving file: {ex.Message}");
                return string.Empty;
            }
        }

        public async Task UploadToNas(byte[] fileData, string fileName, string folderPath)
        {
            using var httpClient = new HttpClient();
            using var multipart = new MultipartFormDataContent();

            var fileContent = new ByteArrayContent(fileData);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");

            var relativePath = Path.Combine(_settings.RemotePath ?? "", folderPath ?? "").Replace("\\", "/");

            multipart.Add(fileContent, "file", fileName + ".xlsx");
            multipart.Add(new StringContent(relativePath), "relativePath");
            multipart.Add(new StringContent("false"), "overwrite");

            var response = await httpClient.PostAsync(_settings.RequestUrl, multipart);

            var result = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("NAS API error: {StatusCode} - {Body}", response.StatusCode, result);
                throw new HttpRequestException($"NAS upload failed: {response.StatusCode}");
            }

            _logger.LogInformation("Uploaded file to NAS: " + result);
        }
    }
}
