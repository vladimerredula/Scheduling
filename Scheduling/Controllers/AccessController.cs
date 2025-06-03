using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Scheduling.ViewModels;
using Scheduling.Services;

namespace Scheduling.Controllers
{
    public class AccessController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly LogService<AccessController> _log;

        public AccessController(ApplicationDbContext dbContext, LogService<AccessController> logger)
        {
            _db = dbContext;
            _log = logger;
        }

        public IActionResult Index()
        {
            ClaimsPrincipal claimUser = HttpContext.User;

            if (claimUser.Identity.IsAuthenticated)
            {
                if (claimUser.IsInRole("member") || claimUser.IsInRole("shiftLeader"))
                    return RedirectToAction("Calendar", "Schedule");
                else
                return RedirectToAction("Index", "Schedule");
            }

            return View("Login");
        }

        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _db.Users
                    .SingleOrDefaultAsync(m => m.Username == model.Username);

                if (user == null)
                {
                    ModelState.AddModelError("Username", "Username not found.");
                    return View(model);
                }

                var userdetails = await _db.Users
                    .SingleOrDefaultAsync(m => m.Username == model.Username && m.Password == model.Password);

                if (userdetails == null)
                {
                    ModelState.AddModelError("Password", "Invalid password. Please try again.");
                    return View(model);
                }

                var role = "";
                switch (userdetails.Privilege_ID)
                {
                    case 0:
                        role = "admin";
                        break;
                    case 1:
                        role = "member";
                        break;
                    case 2:
                        role = "shiftLeader";
                        break;
                    case 3:
                        role = "manager";
                        break;
                    case 4:
                        role = "topManager";
                        break;
                    default:
                        role = userdetails.Privilege_ID.ToString();
                        break;
                }

                List<Claim> claims = new List<Claim>()
                {
                    // Here we store user login information of the user that we can retrieve later
                    new Claim(ClaimTypes.Role, role),
                    new Claim(ClaimTypes.Name, userdetails.Username),
                    new Claim(ClaimTypes.GivenName, userdetails.First_name),
                    new Claim(ClaimTypes.Surname, userdetails.Last_name),
                    new Claim("Personnelid", userdetails.Personnel_ID.ToString())
                };

                ClaimsIdentity claimsIdentity = new ClaimsIdentity(claims,
                    CookieAuthenticationDefaults.AuthenticationScheme);

                AuthenticationProperties properties = new AuthenticationProperties()
                {
                    AllowRefresh = true,
                    IsPersistent = model.KeepLoggedIn
                };

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity), properties);

                // Force session creation by setting a value
                HttpContext.Session.SetString("SessionInitialized", "true");

                await _log.LogInfoAsync("Logged in", usernameOverride: userdetails.Username);

                return RedirectToAction("Index", "Access");
            }

            return View(model);
        }

        public async Task<IActionResult> Logout()
        {
            var sessionId = HttpContext.Session.Id;
            var session = await _db.Sessions.FindAsync(sessionId);
            if (session != null)
            {
                _db.Sessions.Remove(session);
                await _db.SaveChangesAsync();
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            HttpContext.Session.Clear();

            await _log.LogInfoAsync("Logged out");

            return RedirectToAction("Index", "Access");
        }
    }
}