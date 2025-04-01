using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Scheduling.ViewModels;

namespace Scheduling.Controllers
{
    public class AccessController : Controller
    {
        private readonly ApplicationDbContext _db;

        public AccessController(ApplicationDbContext dbContext)
        {
            _db = dbContext;
        }

        public IActionResult Index()
        {
            ClaimsPrincipal claimUser = HttpContext.User;

            if (claimUser.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
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

                List<Claim> claims = new List<Claim>()
                {
                    // Here we store user login information of the user that we can retrieve later
                    new Claim(ClaimTypes.Role, userdetails.Privilege_ID.ToString()),
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

                return RedirectToAction("Index", "Access");
            }

            return View(model);
        }

        public async Task<IActionResult> Logout()
        {
            ClaimsPrincipal claimUser = HttpContext.User;
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            HttpContext.Session.Clear();

            return RedirectToAction("Index", "Access");
        }
    }
}