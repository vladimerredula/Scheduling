using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Scheduling;
using Scheduling.Services;
using NReco.Logging.File;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add DbContext with MySQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(builder.Configuration.GetConnectionString("DefaultConnection"),
    ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("DefaultConnection"))));

// Add services for dependency injection
builder.Services.AddTransient<ExcelService>();

builder.Services.AddScoped(typeof(LogService<>));

// Add authentication services
builder.Services.AddAuthentication(
    CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Access/Login"; // Redirect here if not authenticated
        options.LogoutPath = "/Access/Logout"; // Redirect here to logout
        options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
        options.AccessDeniedPath = "/AccessDenied";
        options.SlidingExpiration = true;
    });

// Add session services
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".Scheduling.Session";
    options.Cookie.HttpOnly = true; // Ensure session cookie is not accessible to client-side scripts
    options.Cookie.IsEssential = true; // Indicate that the session cookie is essential for the application
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Send session cookie only over HTTPS
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Session timeout set to 30 minutes
});

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "DataProtection-Keys")))
    .SetApplicationName("SchedulingApp");

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TemplateService>();

builder.Logging.AddFile(builder.Configuration.GetSection("Logging:File"), fileLoggerOpts =>
{
    string currentLogFileName = null;
    DateTime currentDate = DateTime.MinValue;

    fileLoggerOpts.FormatLogFileName = fName =>
    {
        DateTime now = DateTime.UtcNow;

        if (currentDate.Date != now.Date || currentLogFileName == null)
        {
            currentDate = now;
            string logPath = $"{now:yyyy}/{now:MM}/{now:dd}/{now:yyyy}-{now:MM}-{now:dd}.log";
            currentLogFileName = Path.Combine(fName, logPath);
            Directory.CreateDirectory(Path.GetDirectoryName(currentLogFileName));
        }

        return currentLogFileName;
    };

    fileLoggerOpts.FormatLogEntry = msg =>
    {
        var sb = new System.Text.StringBuilder();

        sb.Append(DateTime.Now.ToString("o")); // ISO timestamp
        sb.Append($" Level:{msg.LogLevel}, ");
        sb.Append($"{msg.LogName}, ");

        if (msg.Exception != null)
        {
            var stackTrace = new System.Diagnostics.StackTrace(msg.Exception, true);
            var frame = stackTrace.GetFrame(0);
            if (frame != null)
            {
                sb.Append($"Line:{frame.GetFileLineNumber()}, ");
            }

            sb.Append($"Exception:{msg.Exception.Message}");
        }
        else
        {
            sb.Append(msg.Message);
        }

        return sb.ToString();
    };

    fileLoggerOpts.FileSizeLimitBytes = 1 * 1024 * 1024; // 1 MB
    fileLoggerOpts.MaxRollingFiles = 3;
});

builder.Logging.AddFile(builder.Configuration.GetSection("Logging:File"), fileLoggerOpts =>
{
    string errorLogFileName = null;
    DateTime currentDate = DateTime.MinValue;

    fileLoggerOpts.FormatLogFileName = fName =>
    {
        DateTime now = DateTime.UtcNow;

        if (currentDate.Date != now.Date || errorLogFileName == null)
        {
            currentDate = now;
            string logPath = $"errors/{now:yyyy}/{now:MM}/{now:dd}/{now:yyyy}-{now:MM}-{now:dd}-errors.log";
            errorLogFileName = Path.Combine(fName, logPath);
            Directory.CreateDirectory(Path.GetDirectoryName(errorLogFileName));
        }

        return errorLogFileName;
    };

    fileLoggerOpts.FilterLogEntry = msg =>
        msg.LogLevel == LogLevel.Error || msg.LogLevel == LogLevel.Critical;

    fileLoggerOpts.FileSizeLimitBytes = 1 * 1024 * 1024; // 1 MB
    fileLoggerOpts.MaxRollingFiles = 3;
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();

app.UseAuthorization();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    //pattern: "{controller=Home}/{action=Index}/{id?}");
    pattern: "{controller=Access}/{action=Index}/{id?}");

app.Run();
