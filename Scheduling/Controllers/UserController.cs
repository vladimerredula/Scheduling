using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scheduling;
using Scheduling.Models;
using System.Security.Claims;

namespace QouToPOWebApp.Controllers
{
    [Authorize]
    public class UserController : Controller
    {
        private readonly ApplicationDbContext _db;

        public UserController(ApplicationDbContext dbContext)
        {
            _db = dbContext;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult ChangePassword(string currentpass, string newpass, string confirmpass)
        {
            var personnelId = GetPersonnelID();

            var user = _db.Users.FirstOrDefault(u => u.Personnel_ID == personnelId && u.Password == currentpass);

            if (user != null)
            {
                user.Password = newpass;
                user.Last_password_changed = DateTime.Now;
                _db.SaveChanges();

                TempData["toastMessage"] = "Password successfully changed.-success";
            }
            else
            {
                TempData["passerror"] = "Incorrect password";
            }

            // Get the referrer URL
            string referrerUrl = Request.Headers["Referer"].ToString();

            if (!string.IsNullOrEmpty(referrerUrl))
            {
                // Parse the referrer URL to extract controller information if needed
                // This step assumes the referrer URL is in the standard routing format
                Uri referrerUri = new Uri(referrerUrl);
                var segments = referrerUri.Segments;
                string previousController = segments.Length > 1 ? segments[1].Trim('/') : string.Empty;
                string previousAction = segments.Length > 2 ? segments[2].Trim('/') : string.Empty;

                if (previousAction != string.Empty)
                    return RedirectToAction(previousAction, previousController);
                else
                    return RedirectToAction("Index", previousController);
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public IActionResult ResetPassword(int personnelid, string newpass, string confirmpass)
        {
            var user = _db.Users.FirstOrDefault(u => u.Personnel_ID == personnelid);

            if (user != null)
            {
                user.Password = newpass;
                user.Last_password_changed = DateTime.Now;
                _db.SaveChanges();

                TempData["toastMessage"] = "Password successfully changed.-success";
            }
            else
            {
                TempData["toastMessage"] = "User not found-danger";
            }

            return RedirectToAction(nameof(Index));
        }

        public int GetPersonnelID()
        {
            var personnelId = int.Parse(User.FindFirstValue("Personnelid"));

            return personnelId;
        }

        public async Task<string> GetUsername()
        {
            var user = await ThisUser();
            return user.Username;
        }

        public async Task<User> ThisUser()
        {
            return _db.Users.Find(GetPersonnelID());
        }

        [HttpGet]
        public IActionResult PersonnelID()
        {
            return Ok(GetPersonnelID());
        }
    }
}
