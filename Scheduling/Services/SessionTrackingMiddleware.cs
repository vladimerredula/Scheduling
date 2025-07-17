using Microsoft.EntityFrameworkCore;
using Scheduling.Models;

namespace Scheduling.Services
{
    public class SessionTrackingMiddleware
    {
        private readonly RequestDelegate _next;

        public SessionTrackingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context, ApplicationDbContext db)
        {
            if (context.User.Identity.IsAuthenticated)
            {
                var userId = context.User.FindFirst("Personnelid")?.Value;
                var sessionId = context.Session.Id;

                var ip = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                      ?? context.Connection.RemoteIpAddress?.ToString();

                if (ip == "::1") ip = "127.0.0.1";
                var userAgent = context.Request.Headers["User-Agent"].ToString();

                if (int.TryParse(userId, out var personnelId))
                {
                    var existing = await db.Sessions.FindAsync(sessionId);
                    if (existing != null)
                    {
                        existing.Last_activity = DateTime.Now;
                        existing.Ip_address = ip;
                        existing.User_agent = userAgent;
                    }
                    else
                    {
                        db.Sessions.Add(new Session
                        {
                            Session_ID = sessionId,
                            Personnel_ID = personnelId,
                            Ip_address = ip,
                            User_agent = userAgent,
                            Last_activity = DateTime.Now,
                            App_name = "SCH"
                        });
                    }

                    await db.SaveChangesAsync();
                }
            }

            await _next(context);
        }
    }
}
