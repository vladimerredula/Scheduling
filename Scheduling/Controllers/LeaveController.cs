using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Scheduling.Models;
using Scheduling.Services;

namespace Scheduling.Controllers
{
    [Authorize]
    public class LeaveController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly LogService<LeaveController> _log;
        private readonly TemplateService _template;
        private readonly UserService _user;
        private readonly ScheduleTokenService _token;

        public LeaveController(ApplicationDbContext context, LogService<LeaveController> logger, TemplateService template, UserService user, ScheduleTokenService token)
        {
            _db = context;
            _log = logger;
            _template = template;
            _user = user;
            _token = token;
        }

        public async Task<IActionResult> Index()
        {
            await PopulateLeaveViewBagsAsync();
            _log.LogInfo("Visited leaves");

            return View();
        }

        [Authorize(Roles = "admin,manager,topManager")]
        public async Task<IActionResult> DepartmentLeaves(int? departmentId = 0)
        {
            if (!_template.HasPermission("DeptSelect"))
                departmentId = await _user.GetDepartmentId();

            await PopulateLeaveViewBagsAsync(departmentId);
            _log.LogInfo("Visited department leaves");

            return View();
        }

        public async Task<IActionResult> Add(Leave leave)
        {
            if (HasOverlappingLeave(leave))
            {
                ModelState.AddModelError(string.Empty, "Existing leave overlaps for the dates selected.");

                await PopulateLeaveViewBagsAsync();
                _log.LogWarning("Existing leave overlaps for the dates selected");
                return View(nameof(Index), leave);
            }

            try
            {
                var personnelId = _user.GetPersonnelId();
                leave.Personnel_ID = personnelId;
                leave.Status = "Pending";

                if (_template.HasPermission("FirstApprover"))
                {
                    leave.Approver_1 = personnelId;
                    leave.Date_approved_1 = DateTime.Now;
                } else if (_template.HasPermission("FinalApprover"))
                {
                    leave.Approver_1 = personnelId;
                    leave.Date_approved_1 = DateTime.Now;
                    leave.Approver_2 = personnelId;
                    leave.Date_approved_2 = DateTime.Now;
                    leave.Status = "Approved";
                }

                _db.Leaves.Add(leave);

                await _db.SaveChangesAsync();
                _log.LogInfo($"Added leave request", leave);

                TempData["toastMessage"] = "Successfully added leave request!-success";
            }
            catch (Exception ex)
            {
                _log.LogError("Unable to add leave request", ex);
                TempData["toastMessage"] = "Unable to request leave.-danger";
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> AddLeave(Leave leave)
        {
            if (HasOverlappingLeave(leave))
            {
                ModelState.AddModelError(string.Empty, "Existing leave overlaps for the dates selected.");

                await PopulateLeaveViewBagsAsync();
                _log.LogWarning("Existing leave overlaps for the dates selected");
                return View(nameof(DepartmentLeaves), leave);
            }

            try
            {
                leave.Status = "Approved";
                leave.Notify = 1;

                if (_template.HasPermission("FinalApprover"))
                {
                    var personnelId = _user.GetPersonnelId();
                    leave.Approver_2 = personnelId;
                    leave.Date_approved_2 = DateTime.Now;
                }

                _db.Leaves.Add(leave);

                await _db.SaveChangesAsync();
                _log.LogInfo($"Added leave request", leave);

                TempData["toastMessage"] = $"Successfully added leave for {await _user.GetFullname(leave.Personnel_ID)}!-success";
            }
            catch (Exception ex)
            {
                _log.LogError("Unable to add leave request", ex);
                TempData["toastMessage"] = "Unable to request leave.-danger";
            }

            return RedirectToAction(nameof(DepartmentLeaves));
        }

        public async Task<IActionResult> Edit(int id, [Bind("Leave_ID,Personnel_ID,Leave_type_ID,Message,Status,Date_start,Date_end,Day_type")] Leave leave)
        {
            if (id != leave.Leave_ID)
            {
                TempData["toastMessage"] = "Leave IDs didn't match.-danger";
                _log.LogWarning($"Leave IDs didn't match: {id} and {leave.Leave_ID}");
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                await PopulateLeaveViewBagsAsync();
                return View(nameof(Index), leave);
            }

            if (HasOverlappingLeave(leave))
            {
                ModelState.AddModelError(string.Empty, "Existing leave overlaps for the dates selected.");
                _log.LogWarning("Existing leave overlaps for the dates selected");
                await PopulateLeaveViewBagsAsync();
                return View(nameof(Index), leave);
            }

            try
            {
                _db.Leaves.Update(leave);
                await _db.SaveChangesAsync();
                _log.LogInfo($"Updated leave request", leave);

                TempData["toastMessage"] = "Successfully updated leave request!-success";
            }
            catch (Exception ex)
            {
                _log.LogError($"Unable to update leave with ID: {id}", ex);
                TempData["toastMessage"] = "Unable to update leave.-danger";
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Update(int? departmentId, Leave leave)
        {
            if (leave == null)
            {
                TempData["toastMessage"] = "Leave not found-danger";
                _log.LogWarning("Leave object is null");
                return RedirectToAction(nameof(DepartmentLeaves), new
                {
                    departmentId = departmentId
                });
            }

            if (HasOverlappingLeave(leave))
            {
                TempData["toastMessage"] = "Existing leave overlaps for the dates selected.";
                _log.LogWarning("Existing leave overlaps for the dates selected");

                return RedirectToAction(nameof(DepartmentLeaves), new
                {
                    departmentId = departmentId
                });
            }

            try
            {
                _db.Leaves.Update(leave);

                leave.Notify = 1;
                await _db.SaveChangesAsync();
                _log.LogInfo($"Updated leave request", leave);

                var deptId = await _user.GetDepartmentId(leave.Personnel_ID);
                await _token.UpdateTokenAsync(deptId, leave.Date_start.Year, leave.Date_start.Month);

                // If the leave spans multiple months, update the token for both months
                if (leave.Date_start.Month != leave.Date_end.Month)
                    await _token.UpdateTokenAsync(deptId, leave.Date_end.Year, leave.Date_end.Month);

                TempData["toastMessage"] = "Successfully updated leave request!-success";
            }
            catch (Exception ex)
            {
                _log.LogError($"Unable to update leave with ID: {leave.Leave_ID}", ex);
                TempData["toastMessage"] = "Unable to update leave.-danger";
            }

            return RedirectToAction(nameof(DepartmentLeaves), new
            {
                departmentId = departmentId
            });
        }

        public async Task<IActionResult> Apply(Leave request)
        {
            if (!HasOverlappingLeave(request))
            {
                try
                {
                    var personnelId = _user.GetPersonnelId();
                    request.Personnel_ID = personnelId;
                    request.Status = "Pending";

                    if (_template.HasPermission("FirstApprover"))
                    {
                        request.Approver_1 = personnelId;
                        request.Date_approved_1 = DateTime.Now;
                    }
                    else if (_template.HasPermission("FinalApprover"))
                    {
                        request.Approver_1 = personnelId;
                        request.Date_approved_1 = DateTime.Now;
                        request.Approver_2 = personnelId;
                        request.Date_approved_2 = DateTime.Now;
                        request.Status = "Approved";
                    }

                    _db.Leaves.Add(request);
                    await _db.SaveChangesAsync();
                    _log.LogInfo($"Added leave request", request);

                    TempData["toastMessage"] = "Successfully added leave request!-success";
                }
                catch (Exception ex)
                {
                    _log.LogError($"Unable to add leave request", ex);
                    TempData["toastMessage"] = "Unable to add leave.-danger";
                }
            } 
            else
            {
                TempData["toastMessage"] = "Unable to apply leave. Existing leave overlaps with the dates selected.-danger";
                _log.LogWarning("Existing leave overlaps for the dates selected");
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
                _log.LogWarning($"Leave ID: {Id} was not found");
                return RedirectToAction(nameof(Index));
            }

            leave.Status = "Cancelled";
            _db.Leaves.Update(leave);
            await _db.SaveChangesAsync();
            _log.LogInfo($"Cancelled leave request", leave);

            TempData["toastMessage"] = "Successfully cancelled leave request!-success";

            return RedirectToAction(nameof(Index));
        }

        // withdraw leave request for department leaves
        public async Task<IActionResult> Withdraw(int id, int deptId, string Comment)
        {
            var leave = await _db.Leaves.FindAsync(id);
            if (leave == null)
            {
                TempData["toastMessage"] = "Leave not found-danger";
                _log.LogWarning($"Leave ID: {id} was not found");
                return RedirectToAction(nameof(DepartmentLeaves), new
                {
                    departmentId = deptId
                });
            }

            leave.Status = "Withdrawn";
            leave.Comment = Comment;
            leave.Notify = 1;

            _db.Leaves.Update(leave);
            await _db.SaveChangesAsync();
            _log.LogInfo($"Cancelled leave request", leave);
            await _token.UpdateTokenAsync(deptId, leave.Date_start.Year, leave.Date_start.Month);

            var departmentId = await _user.GetDepartmentId(leave.Personnel_ID);
            await _token.UpdateTokenAsync(departmentId, leave.Date_start.Year, leave.Date_start.Month);

            // If the leave spans multiple months, update the token for both months
            if (leave.Date_start.Month != leave.Date_end.Month)
                await _token.UpdateTokenAsync(departmentId, leave.Date_end.Year, leave.Date_end.Month);

            TempData["toastMessage"] = "Successfully cancelled leave!-success";

            return RedirectToAction(nameof(DepartmentLeaves), new
            {
                departmentId = deptId
            });
        }

        // withdraw leave request for personal leaves
        public async Task<IActionResult> WithdrawLeave(int id)
        {
            var leave = await _db.Leaves.FindAsync(id);
            if (leave == null)
            {
                TempData["toastMessage"] = "Leave not found-danger";
                _log.LogWarning($"Leave ID: {id} was not found");
                return RedirectToAction(nameof(Index));
            }

            leave.Status = "Withdrawn";
            leave.Comment = "Withdrawn by employee";

            _db.Leaves.Update(leave);
            await _db.SaveChangesAsync();

            var deptId = await _user.GetDepartmentId(leave.Personnel_ID);
            await _token.UpdateTokenAsync(deptId, leave.Date_start.Year, leave.Date_start.Month);

            if (leave.Date_start.Month != leave.Date_end.Month)
                await _token.UpdateTokenAsync(deptId, leave.Date_end.Year, leave.Date_end.Month);

            _log.LogInfo($"Withdrawn leave", leave);

            TempData["toastMessage"] = "Successfully withdrawn leave!-success";

            return RedirectToAction(nameof(Index));
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

            var dayType = string.Empty;

            switch (leave?.Day_type)
            {
                case "HalfDay1":
                    dayType = "First half of day";
                    break;
                case "HalfDay2":
                    dayType = "Second half of day";
                    break;
                default:
                    dayType = "Full day";
                    break;
            }

            return Ok(new
            {
                Name = leave.User.Full_name,
                Personnel_ID = leave.Personnel_ID,
                Leave_ID = leave.Leave_ID,
                Leave_type_ID = leave?.Leave_type_ID,
                Leave_type_name = leave?.Leave_type?.Leave_type_name,
                Date_start = leave?.Date_start_string,
                Date_end = leave?.Date_end_string,
                Day_type = leave?.Day_type,
                Day_type_name = dayType,
                Message = leave?.Message,
                Approver_1 = leave?.Approver_1,
                Approver_1_name = leave?.Approver1?.Full_name,
                Date_approved_1 = leave?.Date_approved_1,
                Approver_2 = leave?.Approver_2,
                Approver_2_name = leave?.Approver2?.Full_name,
                Date_approved_2 = leave?.Date_approved_2,
                Status = leave?.Status,
                Comment = leave?.Comment
            });
        }

        public async Task<IActionResult> Approve(int Id, int? departmentId = null)
        {
            var leave = await _db.Leaves
                .Include(l => l.User)
                .FirstOrDefaultAsync(l => l.Leave_ID == Id);

            if (leave == null)
            {
                TempData["toastMessage"] = "Leave not found-danger";
                _log.LogWarning($"Leave ID: {Id} was not found");
                return RedirectToAction(nameof(DepartmentLeaves));
            }

            var approverId = _user.GetPersonnelId();
            var today = DateTime.Now;

            try
            {
                if (leave.Approver_1 != null)
                {
                    // Final approval
                    leave.Status = "Approved";
                    leave.Approver_2 = approverId;
                    leave.Date_approved_2 = today;
                    leave.Notify = 1; // Notify the user that the leave was approved
                }
                else
                {
                    // First level approval
                    leave.Approver_1 = approverId;
                    leave.Date_approved_1 = today;
                }

                _db.Leaves.Update(leave);
                await _db.SaveChangesAsync();

                if (leave.Approver_2 != null)
                {
                    var deptId = await _user.GetDepartmentId(leave.Personnel_ID);
                    await _token.UpdateTokenAsync(deptId, leave.Date_start.Year, leave.Date_start.Month);
                    if (leave.Date_start.Month != leave.Date_end.Month)
                        await _token.UpdateTokenAsync(deptId, leave.Date_end.Year, leave.Date_end.Month);
                }

                _log.LogInfo($"Approved leave request", leave);

                TempData["toastMessage"] = "Successfully approved leave request!-success";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to approve Leave request: {ex}");
                TempData["toastMessage"] = "Unable to approve leave.-danger";
            }

            return RedirectToAction(nameof(DepartmentLeaves), new
            {
                departmentId = departmentId
            });
        }

        public async Task<IActionResult> Deny(int Id, string Comment, int? departmentID = null)
        {
            var leave = await _db.Leaves
                .Include(l => l.User)
                .FirstOrDefaultAsync(l => l.Leave_ID == Id);

            if (leave == null)
            {
                TempData["toastMessage"] = "Leave not found-danger";
                _log.LogWarning($"Leave ID: {Id} was not found");
                return RedirectToAction("DepartmentLeaves");
            }

            var approverId = _user.GetPersonnelId();
            var today = DateTime.Now;

            try
            {
                leave.Status = "Denied";
                leave.Comment = Comment;

                if (leave.Approver_1 != null)
                {
                    leave.Approver_2 = approverId;
                    leave.Date_approved_2 = today;
                    leave.Notify = 1; // Notify the user that the leave was denied
                }
                else
                {
                    leave.Approver_1 = approverId;
                    leave.Date_approved_1 = today;
                    leave.Notify = 1; // Notify the user that the leave was denied
                }

                _db.Leaves.Update(leave);
                await _db.SaveChangesAsync();
                _log.LogInfo($"Denied leave request", leave);

                TempData["toastMessage"] = "Successfully denied leave request!-success";
            }
            catch (Exception ex)
            {
                _log.LogError($"Unable to deny leave request", ex);
                TempData["toastMessage"] = "Unable to deny leave.-danger";
            }

            return RedirectToAction("DepartmentLeaves", new
            {
                departmentId = departmentID != null ? departmentID : leave?.User?.Department_ID
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
                _log.LogWarning($"Leave ID: {Id} was not found");
                return RedirectToAction("Index", "Schedule");
            }

            var approverId = _user.GetPersonnelId();
            var deptId = await _user.GetDepartmentId(leave.Personnel_ID);
            var today = DateTime.Now;

            try
            {
                if (leave.Approver_1 != null)
                {
                    // Final approval
                    leave.Status = "Approved";
                    leave.Approver_2 = approverId;
                    leave.Date_approved_2 = today;
                    leave.Notify = 1; // Notify the user that the leave was approved
                }
                else
                {
                    // First level approval
                    leave.Approver_1 = approverId;
                    leave.Date_approved_1 = today;
                }

                _db.Leaves.Update(leave);
                await _db.SaveChangesAsync();

                if (leave.Approver_2 != null)
                {
                    await _token.UpdateTokenAsync(deptId, leave.Date_start.Year, leave.Date_start.Month);
                    if (leave.Date_start.Month != leave.Date_end.Month)
                        await _token.UpdateTokenAsync(deptId, leave.Date_end.Year, leave.Date_end.Month);
                }

                _log.LogInfo($"Approved leave request", leave);

                TempData["toastMessage"] = "Successfully approved leave request!-success";
            }
            catch (Exception ex)
            {
                _log.LogError($"Unable to approve leave request", ex);
                TempData["toastMessage"] = "Unable to approve leave.-danger";
            }

            return RedirectToAction("Index", "Schedule", new { 
                month = leave.Date_start.Month, 
                year = leave.Date_start.Year, 
                departmentId = deptId
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
                _log.LogWarning($"Leave ID: {Id} was not found");
                return RedirectToAction("Index", "Schedule");
            }

            var approverId = _user.GetPersonnelId();
            var today = DateTime.Now;

            try
            {
                leave.Status = "Denied";
                leave.Comment = Comment;

                if (leave.Approver_1 != null)
                {
                    leave.Approver_2 = approverId;
                    leave.Date_approved_2 = today;
                    leave.Notify = 1; // Notify the user that the leave was denied
                }
                else
                {
                    leave.Approver_1 = approverId;
                    leave.Date_approved_1 = today;
                    leave.Notify = 1; // Notify the user that the leave was denied
                }

                _db.Leaves.Update(leave);
                await _db.SaveChangesAsync();
                _log.LogInfo($"Denied leave request", leave);

                TempData["toastMessage"] = "Successfully denied leave request!-success";
            }
            catch (Exception ex)
            {
                _log.LogError($"Unable to deny leave request", ex);
                TempData["toastMessage"] = "Unable to deny leave.-danger";
            }

            return RedirectToAction("Index", "Schedule", new { 
                month = leave.Date_start.Month, 
                year = leave.Date_start.Year, 
                departmentId = leave?.User?.Department_ID 
            });
        }

        private async Task PopulateLeaveViewBagsAsync(int? departmentId = null)
        {
            if (departmentId == 0)
            {
                ViewBag.Leaves = await _db.Leaves
                    .Include(l => l.User)
                    .Include(l => l.Leave_type)
                    .Include(l => l.Approver1)
                    .Include(l => l.Approver2)
                    .ToListAsync();

                ViewBag.Departments = new SelectList(
                    _db.Departments.ToList(), 
                    "Department_ID", 
                    "Department_name", 
                    departmentId
                );

                ViewBag.Users = new SelectList(
                    _db.Users
                        .Where(u => u.Privilege_ID != 0 && u.Privilege_ID != 4 && u.Status == 1)
                        .AsEnumerable()
                        .OrderBy(u => u.Full_name)
                        .Select(u => new { u.Personnel_ID, u.Full_name })
                        .ToList(),
                    "Personnel_ID",
                    "Full_name"
                );
            }
            else if (departmentId != null && departmentId != 0)
            {
                var userId = _user.GetPersonnelId();

                if (userId == 35) // TEMPORARY FIX FOR KONSTANTIN
                {
                    ViewBag.Leaves = await _db.Leaves
                        .Include(l => l.User)
                        .Include(l => l.Leave_type)
                        .Include(l => l.Approver1)
                        .Include(l => l.Approver2)
                        .Where(l => l.User.Department_ID == 2 || l.User.Department_ID == 3)
                        .ToListAsync();
                } else
                {
                    ViewBag.Leaves = await _db.Leaves
                        .Include(l => l.User)
                        .Include(l => l.Leave_type)
                        .Include(l => l.Approver1)
                        .Include(l => l.Approver2)
                        .Where(l => l.User.Department_ID == departmentId)
                        .ToListAsync();
                }

                ViewBag.Departments = new SelectList(
                    _db.Departments.ToList(),
                    "Department_ID",
                    "Department_name",
                    departmentId
                );
            }
            else
            {
                var personnelId = _user.GetPersonnelId();

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
