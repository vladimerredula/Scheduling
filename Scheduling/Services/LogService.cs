using System.Reflection;
using System.Security.Claims;
using System.Text;

namespace Scheduling.Services
{
    public class LogService<T>
    {
        private readonly ILogger<T> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public LogService(ILogger<T> logger, IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        private string GetUsername()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var userName = user?.FindFirst(ClaimTypes.Name)?.Value;
            return userName ?? "Unknown";
        }

        public void LogInfo(string message, object? obj = null, string? usernameOverride = null)
        {
            var username = usernameOverride ?? GetUsername();

            var log = new StringBuilder();

            log.Append($"User:{username}, Message:{message}");

            if (obj != null)
                log.Append($", Data:{ObjectToLogString(obj)}");

            _logger.LogInformation(log.ToString());
        }

        public void LogWarning(string message)
        {
            var username = GetUsername();
            _logger.LogWarning($"User:{username}, Message:{message}");
        }

        public void LogError(string message, Exception? ex = null)
        {
            var username = GetUsername();
            if (ex != null)
                _logger.LogError(ex, $"User:{username}, Message:{message}");
            else
                _logger.LogError($"User:{username}, Message:{message}");
        }

        private static string ObjectToLogString(object obj)
        {
            var props = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var formatted = props
                .Where(p =>
                    p.GetMethod != null &&
                    !p.GetMethod.IsVirtual &&
                    p.GetValue(obj) != null)
                .Select(p => $"{p.Name}:{p.GetValue(obj)}");

            return "[" + string.Join(",", formatted) + "]";
        }
    }
}
