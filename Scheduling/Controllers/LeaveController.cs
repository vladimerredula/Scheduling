using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Scheduling.Models;
using System.Security.Claims;

namespace Scheduling.Controllers
{
    [Authorize]
    public class LeaveController : Controller
    {
        private readonly ApplicationDbContext _db;

        public LeaveController(ApplicationDbContext context)
        {
            _db = context;
        }

        public IActionResult Index(int departmentId = 1)
        {
            return RedirectToAction(nameof(Leaves));
        }

        public async Task<IActionResult> DepartmentLeaves(int? departmentId = 1)
        {
            if (User.IsInRole("manager"))
            {
                var user = await ThisUser();
                departmentId = user.Department_ID;
            }

            await PopulateLeaveViewBagsAsync(departmentId);

            return View();
        }

        public async Task<IActionResult> Leaves()
        {
            await PopulateLeaveViewBagsAsync();

            return View();
        }

        public async Task<IActionResult> Add(Leave leave)
        {
            if (HasOverlappingLeave(leave))
            {
                ModelState.AddModelError(string.Empty, "Existing leave overlaps for these dates.");
                await PopulateLeaveViewBagsAsync();
                return View(nameof(Leaves), leave);
            }

            try
            {
                var personnelId = int.Parse(User.FindFirstValue("Personnelid"));
                leave.Personnel_ID = personnelId;
                leave.Status = "Pending";
                _db.Leaves.Add(leave);
                await _db.SaveChangesAsync();

                TempData["toastMessage"] = "Successfully added leave request!-success";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to save Leave: {ex}");
                TempData["toastMessage"] = "Unable to request leave.-danger";
            }

            return RedirectToAction(nameof(Leaves));
        }

        public async Task<IActionResult> Edit(int id, [Bind("Leave_ID,Personnel_ID,Leave_type_ID,Status,Date_start,Date_end")] Leave leave)
        {
            if (id != leave.Leave_ID)
                return RedirectToAction(nameof(Leaves));

            if (!ModelState.IsValid)
            {
                await PopulateLeaveViewBagsAsync();
                return View(nameof(Leaves), leave);
            }

            if (HasOverlappingLeave(leave))
            {
                ModelState.AddModelError(string.Empty, "Existing leave overlaps for these dates.");
                await PopulateLeaveViewBagsAsync();
                return View(nameof(Leaves), leave);
            }

            try
            {
                _db.Leaves.Update(leave);
                await _db.SaveChangesAsync();

                TempData["toastMessage"] = "Successfully updated leave request!-success";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to update Leave: {id} - {ex}");
                TempData["toastMessage"] = "Unable to update leave.-danger";
            }

            return RedirectToAction(nameof(Leaves));
        }

        public async Task<IActionResult> Apply(Leave request)
        {
            try
            {
                var personnelId = int.Parse(User.FindFirstValue("Personnelid"));

                request.Personnel_ID = personnelId;
                request.Status = "Pending";

                if (User.IsInRole("manager") || User.IsInRole("topManager"))
                {
                    request.Approver_1 = personnelId;
                    request.Date_approved_1 = DateTime.Today;
                }

                _db.Leaves.Add(request);
                await _db.SaveChangesAsync();

                TempData["toastMessage"] = "Successfully added leave request!-success";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to add Leave request: {ex}");
                TempData["toastMessage"] = "Unable to add leave.-danger";
            }

            // Try to redirect back to the previous page (Calendar only)
            string referrerUrl = Request.Headers["Referer"].ToString();
            if (Uri.TryCreate(referrerUrl, UriKind.Absolute, out var referrerUri))
            {
                var segments = referrerUri.Segments
                    .Select(s => s.Trim('/'))
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToArray();

                if (segments.Length >= 2 && segments[1].Equals("Calendar", StringComparison.OrdinalIgnoreCase))
                {
                    string controller = segments[0];
                    return RedirectToAction("Calendar", controller);
                }
            }

            // Default redirect to schedule if no valid referrer
            return RedirectToAction("Index", "Schedule", new
            {
                month = request.Date_start.Month,
                year = request.Date_start.Year
            });
        }

        public async Task<IActionResult> Cancel(int Id)
        {
            var leave = await _db.Leaves.FindAsync(Id);
            if (leave == null)
            {
                TempData["toastMessage"] = "Leave not found-danger";
                return RedirectToAction(nameof(Leaves));
            }

            leave.Status = "Cancelled";
            _db.Leaves.Update(leave);
            await _db.SaveChangesAsync();

            TempData["toastMessage"] = "Successfully cancelled leave request!-success";

            return RedirectToAction(nameof(Leaves));
        }

        // Check if the dates overlap
        private bool HasOverlappingLeave(Leave leave)
        {
            return _db.Leaves.Any(l =>
                l.Personnel_ID == leave.Personnel_ID &&
                l.Leave_ID != leave.Leave_ID &&
                (l.Status == "Pending" || l.Status == "Approved") &&
                l.Date_start.Date <= leave.Date_end.Date &&
                leave.Date_start.Date <= l.Date_end.Date
            );
        }

        [HttpPost]
        public IActionResult GetLeave(int Id)
        {
            var leave = _db.Leaves
                .Include(l => l.Leave_type)
                .Include(l => l.User)
                .Include(l => l.Approver1)
                .Include(l => l.Approver2)
                .FirstOrDefault(l => l.Leave_ID == Id);

            if (leave == null) return NotFound();

            return Ok(new
            {
                Name = leave.User.Full_name,
                Personnel_ID = leave.Personnel_ID,
                Leave_ID = leave.Leave_ID,
                Leave_type_ID = leave?.Leave_type_ID,
                Leave_type_name = leave?.Leave_type?.Leave_type_name,
                Date_start = leave?.Date_start_string,
                Date_end = leave?.Date_end_string,
                Approver_1 = leave?.Approver1?.Full_name,
                Approver_2 = leave?.Approver2?.Full_name,
                Status = leave?.Status,
                Comment = leave?.Comment
            });
        }

        public async Task<IActionResult> Approve(int Id)
        {
            var leave = await _db.Leaves
                .Include(l => l.User)
                .FirstOrDefaultAsync(l => l.Leave_ID == Id);

            if (leave == null)
            {
                TempData["toastMessage"] = "Leave not found-danger";
                return RedirectToAction(nameof(DepartmentLeaves));
            }

            var approverId = int.Parse(User.FindFirstValue("Personnelid"));
            var today = DateTime.Today;

            try
            {
                if (leave.Approver_1 != null)
                {
                    // Final approval
                    leave.Status = "Approved";
                    leave.Approver_2 = approverId;
                    leave.Date_approved_2 = today;
                }
                else
                {
                    // First level approval
                    leave.Approver_1 = approverId;
                    leave.Date_approved_1 = today;
                }

                _db.Leaves.Update(leave);
                await _db.SaveChangesAsync();

                TempData["toastMessage"] = "Successfully approved leave request!-success";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to approve Leave request: {ex}");
                TempData["toastMessage"] = "Unable to approve leave.-danger";
            }

            return RedirectToAction(nameof(DepartmentLeaves), new 
            { 
                departmentId = leave?.User?.Department_ID 
            });
        }

        public async Task<IActionResult> Deny(int Id, string Comment)
        {
            var leave = await _db.Leaves
                .Include(l => l.User)
                .FirstOrDefaultAsync(l => l.Leave_ID == Id);

            if (leave == null)
                return NotFound();

            var approverId = int.Parse(User.FindFirstValue("Personnelid"));
            var today = DateTime.Today;

            try
            {
                leave.Status = "Denied";
                leave.Comment = Comment;

                if (leave.Approver_1 != null)
                {
                    leave.Approver_2 = approverId;
                    leave.Date_approved_2 = today;
                }
                else
                {
                    leave.Approver_1 = approverId;
                    leave.Date_approved_1 = today;
                }

                _db.Leaves.Update(leave);
                await _db.SaveChangesAsync();

                TempData["toastMessage"] = "Successfully denied leave request!-success";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to deny Leave request: {ex}");
                TempData["toastMessage"] = "Unable to deny leave.-danger";
            }

            return RedirectToAction("DepartmentLeaves", new
            {
                departmentId = leave.User?.Department_ID
            });
        }

        public async Task<IActionResult> Approve1(int Id)
        {
            var leave = await _db.Leaves
                .Include(l => l.User)
                .FirstOrDefaultAsync(l => l.Leave_ID == Id);

            if (leave == null)
            {
                TempData["toastMessage"] = "Leave not found-danger";
                return RedirectToAction("Index", "Schedule");
            }

            var approverId = int.Parse(User.FindFirstValue("Personnelid"));
            var today = DateTime.Today;

            try
            {
                if (leave.Approver_1 != null)
                {
                    // Final approval
                    leave.Status = "Approved";
                    leave.Approver_2 = approverId;
                    leave.Date_approved_2 = today;
                }
                else
                {
                    // First level approval
                    leave.Approver_1 = approverId;
                    leave.Date_approved_1 = today;
                }

                _db.Leaves.Update(leave);
                await _db.SaveChangesAsync();

                TempData["toastMessage"] = "Successfully approved leave request!-success";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to approve Leave request: {ex}");
                TempData["toastMessage"] = "Unable to approve leave.-danger";
            }

            return RedirectToAction("Index", "Schedule", new { 
                month = leave.Date_start.Month, 
                year = leave.Date_start.Year, 
                departmentId = leave?.User?.Department_ID 
            });
        }

        public async Task<IActionResult> Deny1(int Id, string Comment)
        {
            var leave = await _db.Leaves
                .Include(l => l.User)
                .FirstOrDefaultAsync(l => l.Leave_ID == Id);

            if (leave == null)
            {
                TempData["toastMessage"] = "Leave not found-danger";
                return RedirectToAction("Index", "Schedule");
            }

            var approverId = int.Parse(User.FindFirstValue("Personnelid"));
            var today = DateTime.Today;

            try
            {
                leave.Status = "Denied";
                leave.Comment = Comment;

                if (leave.Approver_1 != null)
                {
                    leave.Approver_2 = approverId;
                    leave.Date_approved_2 = today;
                }
                else
                {
                    leave.Approver_1 = approverId;
                    leave.Date_approved_1 = today;
                }

                _db.Leaves.Update(leave);
                await _db.SaveChangesAsync();

                TempData["toastMessage"] = "Successfully denied leave request!-success";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to deny Leave request: {ex}");
                TempData["toastMessage"] = "Unable to deny leave.-danger";
            }

            return RedirectToAction("Index", "Schedule", new { 
                month = leave.Date_start.Month, 
                year = leave.Date_start.Year, 
                departmentId = leave?.User?.Department_ID 
            });
        }

        public int GetPersonnelID()
        {
            var personnelId = int.Parse(User.FindFirstValue("Personnelid"));

            return personnelId;
        }

        public async Task<User> ThisUser()
        {
            return _db.Users.Find(GetPersonnelID());
        }

        private async Task PopulateLeaveViewBagsAsync(int? departmentId = 0)
        {
            if (departmentId != 0)
            {
                var personnelId = GetPersonnelID();

                ViewBag.Leaves = await _db.Leaves
                    .Include(l => l.User)
                    .Include(l => l.Leave_type)
                    .Include(l => l.Approver1)
                    .Include(l => l.Approver2)
                    .Where(l => l.User.Department_ID == departmentId)
                    .ToListAsync();

                ViewBag.Departments = new SelectList(
                    _db.Departments.ToList(), 
                    "Department_ID", 
                    "Department_name", 
                    departmentId
                );
            } else
            {
                var personnelId = GetPersonnelID();

                ViewBag.Leaves = await _db.Leaves
                    .Include(l => l.User)
                    .Include(l => l.Leave_type)
                    .Include(l => l.Approver1)
                    .Include(l => l.Approver2)
                    .Where(l => l.Personnel_ID == personnelId)
                    .ToListAsync();
            }

            ViewBag.LeaveTypes = new SelectList(
                _db.Leave_types.ToList(),
                "Leave_type_ID",
                "Leave_type_name"
            );
        }
    }
}
