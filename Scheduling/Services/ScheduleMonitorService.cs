using Microsoft.EntityFrameworkCore;

namespace Scheduling.Services
{
    public class ScheduleMonitorService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ScheduleMonitorService> _logger;

        public ScheduleMonitorService(IServiceProvider serviceProvider, ILogger<ScheduleMonitorService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
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
                            //await exportService.ExportMonthlyScheduleAsync(token.DepartmentId, token.Year, token.Month);
                            //await _log.LogInfoAsync($"Exported updated version of the {token.Month}/{token.Year} {token.Department_ID} schedule");
                            var excel = new ExcelService();

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
    }
}
