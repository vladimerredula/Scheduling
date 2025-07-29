using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Scheduling.ViewModels;
using Scheduling.Services;
using Scheduling.Helpers;
using Scheduling.Models;
using System.IdentityModel.Tokens.Jwt;

namespace Scheduling.Controllers
{
    public class AccessController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly LogService<AccessController> _log;
        private readonly EncryptionHelper _aesHelper;

        public AccessController(ApplicationDbContext dbContext, LogService<AccessController> logger, EncryptionHelper encryption)
        {
            _db = dbContext;
            _log = logger;
            _aesHelper = encryption;
        }

        public IActionResult Index()
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                return User.IsInRole("member") || User.IsInRole("shiftLeader")
                    ? RedirectToAction("Calendar", "Schedule")
                    : RedirectToAction("Index", "Schedule");
            }

            return View("Login");
        }

        public IActionResult Login() => View();

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) 
                return View(model);

            var user = new User();
            var claims = new List<Claim>();

            var response = await TryExternalLogin(model);

            if (response.IsSuccessStatusCode)
            {
                var jwtClaims = await ReadJwtClaims(response);

                if (jwtClaims == null)
                {
                    ModelState.AddModelError("", "Login failed. Token not received.");
                    return View(model);
                }

                var personnelIdClaim = jwtClaims.FirstOrDefault(c => c.Type == "Personnelid")?.Value;
                if (!int.TryParse(personnelIdClaim, out var personnelId))
                {
                    ModelState.AddModelError("", "Personnel ID from token not found.");
                    return View(model);
                }

                user = await _db.Users.SingleOrDefaultAsync(u => u.Personnel_ID == personnelId);
                if (user == null)
                {
                    ModelState.AddModelError("", "User not found.");
                    return View(model);
                }

                claims.Add(new Claim("ChangePassword", "false"));
                claims.AddRange(jwtClaims);
            }
            else
            {
                // Retrieve user in DB if not found in AD

                user = await _db.Users.SingleOrDefaultAsync(u => u.Username == model.Username);

                if (user == null)
                {
                    ModelState.AddModelError("Username", "Username not found.");
                    return View(model);
                }

                if (user.Password != model.Password)
                {
                    ModelState.AddModelError("Password", "Invalid password.");
                    return View(model);
                }

                claims.Add(new Claim("ChangePassword", "true"));
                claims.AddRange(GetLocalClaims(user));
            }

            claims.Add(new Claim(ClaimTypes.Role, GetRoleName(user.Privilege_ID)));

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProps = new AuthenticationProperties
            {
                AllowRefresh = true,
                IsPersistent = model.KeepLoggedIn
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), authProps);

            // Ensure session is initialized
            HttpContext.Session.SetString("SessionInitialized", HttpContext.Session.Id);
            _log.LogInfo("Logged in", usernameOverride: model.Username);

            return RedirectToAction("Index", "Access");
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
            _log.LogInfo("Logged out");

            return RedirectToAction("Index", "Access");
        }

        public class AuthResponse
        {
            public string Token { get; set; }
        }

        // ========== Helper Methods ==========

        private async Task<HttpResponseMessage> TryExternalLogin(LoginViewModel model)
        {
            var client = new HttpClient();
            var payload = new
            {
                username = _aesHelper.Encrypt(model.Username),
                password = _aesHelper.Encrypt(model.Password)
            };

            return await client.PostAsJsonAsync("http://auth.faradaygroup.local/api/Auth/login", payload);
        }

        private async Task<List<Claim>?> ReadJwtClaims(HttpResponseMessage response)
        {
            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
            if (string.IsNullOrWhiteSpace(auth?.Token)) 
                return null;

            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(auth.Token);

            return token.Claims.ToList();
        }

        private List<Claim> GetLocalClaims(User user) => new()
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.GivenName, user.Full_name),
            new Claim("Personnelid", user.Personnel_ID.ToString())
        };

        private string GetRoleName(int privilegeId) => privilegeId switch
        {
            0 => "admin",
            1 => "member",
            2 => "shiftLeader",
            3 => "manager",
            4 => "topManager",
            _ => privilegeId.ToString()
        };
    }
}