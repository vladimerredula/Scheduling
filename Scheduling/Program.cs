using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using NReco.Logging.File;
using Scheduling;
using Scheduling.Helpers;
using Scheduling.Models.Misc;
using Scheduling.Services;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Configure Services
builder.Services.AddControllersWithViews();

// Database (MySQL)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("DefaultConnection"))
    ));

// Dependency Injection
builder.Services.AddTransient<ExcelService>();
builder.Services.AddScoped(typeof(LogService<>));
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ScheduleTokenService>();
builder.Services.AddScoped<ScheduleHelper>();
builder.Services.AddScoped<TemplateService>();
builder.Services.AddHostedService<ScheduleMonitorService>();
builder.Services.AddSingleton<EncryptionHelper>();
builder.Services.AddHttpContextAccessor();

// Load settings
builder.Services.Configure<SchBackupSettings>(builder.Configuration.GetSection("SchBackupSettings"));
builder.Services.Configure<EncryptionSettings>(builder.Configuration.GetSection("Encryption"));

// Authentication + Cookie Config
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "sch.Auth";
        options.LoginPath = "/Access/Login";
        options.LogoutPath = "/Access/Logout";
        options.AccessDeniedPath = "/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    });

// Session
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "sch.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.None;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});

// Data Protection
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "DataProtection-Keys")))
    .SetApplicationName("SchedulingWebApp");

// Forwarded Headers (for reverse proxy)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Add(IPAddress.Parse("192.168.161.115")); // Nginx IP
});

// File Logging (General + Error)
builder.Logging.AddFile(builder.Configuration.GetSection("Logging:File"), options =>
{
    string currentLogFileName = string.Empty;
    DateTime currentDate = DateTime.MinValue;

    options.FormatLogFileName = folder =>
    {
        var now = DateTime.UtcNow;
        if (currentDate.Date != now.Date || string.IsNullOrEmpty(currentLogFileName))
        {
            currentDate = now;
            string logPath = $"{now:yyyy}/{now:MM}/{now:dd}/{now:yyyy-MM-dd}.log";
            currentLogFileName = Path.Combine(folder, logPath);
            var directoryPath = Path.GetDirectoryName(currentLogFileName);
            if (!string.IsNullOrEmpty(directoryPath))
                Directory.CreateDirectory(directoryPath);
        }
        return currentLogFileName;
    };

    options.FormatLogEntry = msg =>
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(DateTime.Now.ToString("o"));
        sb.Append($" Level:{msg.LogLevel}, {msg.LogName}, ");
        if (msg.Exception != null)
        {
            var trace = new System.Diagnostics.StackTrace(msg.Exception, true);
            var frame = trace.GetFrame(0);
            if (frame != null)
                sb.Append($"Line:{frame.GetFileLineNumber()}, ");
            sb.Append($"Exception:{msg.Exception.Message}");
        }
        else
        {
            sb.Append(msg.Message);
        }
        return sb.ToString();
    };

    options.FileSizeLimitBytes = 1 * 1024 * 1024;
    options.MaxRollingFiles = 3;
});

builder.Logging.AddFile(builder.Configuration.GetSection("Logging:File"), options =>
{
    string errorLogFileName = string.Empty;
    DateTime currentDate = DateTime.MinValue;

    options.FormatLogFileName = folder =>
    {
        var now = DateTime.UtcNow;
        if (currentDate.Date != now.Date || string.IsNullOrEmpty(errorLogFileName))
        {
            currentDate = now;
            string logPath = $"errors/{now:yyyy}/{now:MM}/{now:dd}/{now:yyyy-MM-dd}-errors.log";
            errorLogFileName = Path.Combine(folder, logPath);
            var directoryPath = Path.GetDirectoryName(errorLogFileName);
            if (!string.IsNullOrEmpty(directoryPath))
                Directory.CreateDirectory(directoryPath);
        }
        return errorLogFileName;
    };

    options.FilterLogEntry = msg =>
        msg.LogLevel == LogLevel.Error || msg.LogLevel == LogLevel.Critical;

    options.FileSizeLimitBytes = 1 * 1024 * 1024;
    options.MaxRollingFiles = 3;
});

// Listen on port 80
builder.WebHost.UseUrls("http://0.0.0.0:80");

var app = builder.Build();

// Middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseForwardedHeaders();
// app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();
app.UseMiddleware<SessionTrackingMiddleware>();

// Routes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Access}/{action=Index}/{id?}");

app.Run();
