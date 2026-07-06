using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPMS.Data;
using SPMS.Helpers;
using SPMS.Models;

namespace SPMS.Controllers
{
    public class StudentController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        private const int GroupMin = 3;
        private const int GroupMax = 5;

        public StudentController(AppDbContext db, IWebHostEnvironment env)
        { _db = db; _env = env; }

        private IActionResult? Guard()
        {
            if (!SessionHelper.IsLoggedIn(HttpContext.Session) || !SessionHelper.IsStudent(HttpContext.Session))
                return RedirectToAction("Login", "Account");
            return null;
        }

        private int CurrentUserId => SessionHelper.GetUserId(HttpContext.Session)!.Value;

        // ─── Dashboard ────────────────────────────────────────────────────────

        public async Task<IActionResult> Dashboard()
        {
            if (Guard() is { } r) return r;
            var uid = CurrentUserId;
            var myTutorialGroup = await GetMyTutorialGroup(uid);

            ViewBag.UserName            = SessionHelper.GetUserName(HttpContext.Session);
            ViewBag.MemberCount         = myTutorialGroup?.Members?.Count ?? 0;
            ViewBag.ProgressCount       = myTutorialGroup != null
                ? await _db.TutorialWeeklyProgressEntries.CountAsync(wp => wp.TutorialGroupId == myTutorialGroup.Id)
                : 0;
            ViewBag.HasApprovedProject  = myTutorialGroup?.ApprovedProjectId != null;
            ViewBag.ApprovedProjectTitle = myTutorialGroup?.ApprovedProject?.Title;
            ViewBag.GroupStatus         = myTutorialGroup?.Status.ToString() ?? "Not in a group";
            ViewBag.UnreadMessages      = await _db.Messages.CountAsync(m => m.ReceiverId == uid && !m.IsRead);
            ViewBag.UnreadNotifications = await _db.Notifications.CountAsync(n => n.UserId == uid && !n.IsRead);

            return View();
        }

        // ─── Browse & Select Projects ─────────────────────────────────────────

        public async Task<IActionResult> BrowseProjects(string? search)
        {
            if (Guard() is { } r) return r;
            var uid = CurrentUserId;

            var myTutorialGroup = await _db.TutorialGroupMembers
                .Include(m => m.TutorialGroup)
                .Where(m => m.StudentId == uid)
                .Select(m => m.TutorialGroup)
                .FirstOrDefaultAsync();

            var isEnrolled = await _db.TutorialEnrollments.AnyAsync(e => e.StudentId == uid);

            if (myTutorialGroup == null)
            {
                ViewBag.Locked = true;
                ViewBag.LockReason = isEnrolled ? "no_group" : "no_tutorial";
                return View(new List<Project>());
            }

            if (myTutorialGroup.Status != TutorialGroupStatus.Approved)
            {
                ViewBag.Locked = true;
                ViewBag.LockReason = "not_approved";
                return View(new List<Project>());
            }

            if (myTutorialGroup.ApprovedProjectId != null)
            {
                ViewBag.Locked = true;
                ViewBag.LockReason = "project_approved";
                return View(new List<Project>());
            }

            var mySelectionIds = await _db.StudentProjectSelections
                .Where(s => s.StudentId == uid)
                .Select(s => s.ProjectId)
                .ToListAsync();

            var q = _db.Projects
                .Where(p => p.Status == ProjectStatus.AdminApproved)
                .Include(p => p.Company)
                .Include(p => p.StudentSelections)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                q = q.Where(p => p.Title.Contains(search) || p.Description.Contains(search));

            var projects = await q.OrderByDescending(p => p.CreatedAt).ToListAsync();

            ViewBag.MySelectionIds   = mySelectionIds;
            ViewBag.MySelectionCount = mySelectionIds.Count;
            ViewBag.Search           = search;
            ViewBag.Locked           = false;

            return View(projects);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectProject(int projectId)
        {
            if (Guard() is { } r) return r;
            var uid = CurrentUserId;

            // Gate: must be in a tutor-approved tutorial group before selecting projects
            var approved = await _db.TutorialGroupMembers
                .AnyAsync(m => m.StudentId == uid && m.TutorialGroup!.Status == TutorialGroupStatus.Approved);
            if (!approved)
            { TempData["Error"] = "Your group must be approved by a tutor before you can select projects."; return RedirectToAction("BrowseProjects"); }

            // Max 1 selection
            var count = await _db.StudentProjectSelections.CountAsync(s => s.StudentId == uid);
            if (count >= 1) { TempData["Error"] = "You can only select 1 project. Remove your current selection first if you want to choose a different one."; return RedirectToAction("BrowseProjects"); }

            // Already selected?
            if (await _db.StudentProjectSelections.AnyAsync(s => s.StudentId == uid && s.ProjectId == projectId))
            { TempData["Error"] = "You have already selected this project."; return RedirectToAction("BrowseProjects"); }

            // Project must be AdminApproved
            var project = await _db.Projects.FindAsync(projectId);
            if (project == null || project.Status != ProjectStatus.AdminApproved)
            { TempData["Error"] = "This project is not available for selection."; return RedirectToAction("BrowseProjects"); }

            _db.StudentProjectSelections.Add(new StudentProjectSelection { StudentId = uid, ProjectId = projectId });
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Project '{project.Title}' added to your selections.";
            return RedirectToAction("BrowseProjects");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeselectProject(int projectId)
        {
            if (Guard() is { } r) return r;
            var uid = CurrentUserId;
            var sel = await _db.StudentProjectSelections
                .FirstOrDefaultAsync(s => s.StudentId == uid && s.ProjectId == projectId);
            if (sel != null) { _db.StudentProjectSelections.Remove(sel); await _db.SaveChangesAsync(); }
            TempData["Success"] = "Project removed from your selections.";
            return RedirectToAction("BrowseProjects");
        }

        public IActionResult MySelections()
        {
            if (Guard() is { } r) return r;
            return RedirectToAction("MyTutorial");
        }

        // ─── Legacy redirects ─────────────────────────────────────────────────

        public IActionResult Groups()
        {
            if (Guard() is { } r) return r;
            return RedirectToAction("MyTutorial");
        }


                private async Task<TutorialGroup?> GetMyTutorialGroup(int studentId)
        {
            var membership = await _db.TutorialGroupMembers
                .Include(m => m.TutorialGroup).ThenInclude(g => g!.Members)
                .FirstOrDefaultAsync(m => m.StudentId == studentId);
            return membership?.TutorialGroup;
        }

        private async Task<int?> GetMyTutorialId(int studentId)
        {
            var enrollment = await _db.TutorialEnrollments
                .FirstOrDefaultAsync(e => e.StudentId == studentId);
            return enrollment?.TutorialId;
        }

        // ─── Tutorial ─────────────────────────────────────────────────────────

        public async Task<IActionResult> MyTutorial()
        {
            if (Guard() is { } r) return r;
            var uid = CurrentUserId;

            // Find enrollment
            var enrollmentRow = await _db.TutorialEnrollments
                .FirstOrDefaultAsync(e => e.StudentId == uid);

            if (enrollmentRow == null) return View((Tutorial?)null);

            // Load tutorial separately — pass it directly as the view model
            var tutorial = await _db.Tutorials
                .Include(t => t.Tutor)
                .Include(t => t.Announcements)
                .FirstOrDefaultAsync(t => t.Id == enrollmentRow.TutorialId);

            if (tutorial == null) return View((Tutorial?)null);

            var tutorialId = enrollmentRow.TutorialId;

            // Load membership without chained Includes (Pomelo EF Core issue)
            var myMembership = await _db.TutorialGroupMembers
                .FirstOrDefaultAsync(m => m.StudentId == uid && m.TutorialGroup!.TutorialId == tutorialId);

            // Load the group and all its children separately
            TutorialGroup? myGroup = null;
            if (myMembership != null)
            {
                myGroup = await _db.TutorialGroups
                    .Include(g => g.Members).ThenInclude(m => m.Student)
                    .Include(g => g.GroupRequests)
                    .Include(g => g.ProjectRequests).ThenInclude(r => r.Project)
                    .Include(g => g.ApprovedProject).ThenInclude(p => p!.Company)
                    .FirstOrDefaultAsync(g => g.Id == myMembership.TutorialGroupId);
                if (myGroup != null) myMembership.TutorialGroup = myGroup;
            }

            // Peers enrolled in same tutorial (excluding self)
            var peers = await _db.TutorialEnrollments
                .Where(e => e.TutorialId == tutorialId && e.StudentId != uid)
                .Include(e => e.Student)
                .ToListAsync();

            // Joinable groups in same tutorial (only if not in a group)
            List<TutorialGroup> joinableGroups = new();
            if (myGroup == null)
            {
                joinableGroups = await _db.TutorialGroups
                    .Where(g => g.TutorialId == tutorialId &&
                                (g.Status == TutorialGroupStatus.Forming || g.Status == TutorialGroupStatus.Ready))
                    .Include(g => g.Members).ThenInclude(m => m.Student)
                    .ToListAsync();
                joinableGroups = joinableGroups.Where(g => g.Members.Count < GroupMax).ToList();
            }

            // Latest group request + project request
            var latestGroupReq = myGroup?.GroupRequests
                .OrderByDescending(r => r.RequestedAt).FirstOrDefault();
            var latestProjReq = myGroup?.ProjectRequests
                .OrderByDescending(r => r.RequestedAt).FirstOrDefault();

            // Available projects (if group is Approved and leader needs to pick one)
            List<Project> availableProjects = new();
            if (myGroup?.Status == TutorialGroupStatus.Approved &&
                (latestProjReq == null || latestProjReq.Status == TutorGroupRequestStatus.Rejected) &&
                myMembership?.IsLeader == true)
            {
                availableProjects = await _db.Projects
                    .Where(p => p.Status == ProjectStatus.AdminApproved)
                    .Include(p => p.Company)
                    .OrderBy(p => p.Title)
                    .ToListAsync();
            }

            ViewBag.MyGroup           = myGroup;
            ViewBag.MyMembership      = myMembership;
            ViewBag.TutorialPeers     = peers;
            ViewBag.JoinableGroups    = joinableGroups;
            ViewBag.LatestGroupReq    = latestGroupReq;
            ViewBag.LatestProjReq     = latestProjReq;
            ViewBag.AvailableProjects = availableProjects;
            ViewBag.GroupMin          = GroupMin;
            ViewBag.GroupMax          = GroupMax;

            return View(tutorial);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTutorialGroup(int tutorialId, string name)
        {
            if (Guard() is { } r) return r;
            var uid = CurrentUserId;

            // Must be enrolled
            var enrolled = await _db.TutorialEnrollments.AnyAsync(e => e.TutorialId == tutorialId && e.StudentId == uid);
            if (!enrolled) { TempData["Error"] = "You are not enrolled in this tutorial."; return RedirectToAction("MyTutorial"); }

            // Must not already be in a group for this tutorial
            var alreadyInGroup = await _db.TutorialGroupMembers
                .AnyAsync(m => m.StudentId == uid && m.TutorialGroup!.TutorialId == tutorialId);
            if (alreadyInGroup) { TempData["Error"] = "You are already in a group for this tutorial."; return RedirectToAction("MyTutorial"); }

            var group = new TutorialGroup
            {
                TutorialId = tutorialId, Name = name,
                Status = TutorialGroupStatus.Forming,
                CreatedByStudentId = uid
            };
            _db.TutorialGroups.Add(group);
            await _db.SaveChangesAsync();

            _db.TutorialGroupMembers.Add(new TutorialGroupMember
            {
                TutorialGroupId = group.Id, StudentId = uid, IsLeader = true
            });
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Group '{name}' created. You are the leader!";
            return RedirectToAction("MyTutorial");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> JoinTutorialGroup(int tutorialGroupId)
        {
            if (Guard() is { } r) return r;
            var uid = CurrentUserId;

            var group = await _db.TutorialGroups
                .Include(g => g.Members)
                .FirstOrDefaultAsync(g => g.Id == tutorialGroupId);
            if (group == null) return NotFound();

            // Must be enrolled in same tutorial
            var enrolled = await _db.TutorialEnrollments.AnyAsync(e => e.TutorialId == group.TutorialId && e.StudentId == uid);
            if (!enrolled) { TempData["Error"] = "You are not enrolled in this tutorial."; return RedirectToAction("MyTutorial"); }

            // Must not already be in a group
            var alreadyInGroup = await _db.TutorialGroupMembers
                .AnyAsync(m => m.StudentId == uid && m.TutorialGroup!.TutorialId == group.TutorialId);
            if (alreadyInGroup) { TempData["Error"] = "You are already in a group for this tutorial."; return RedirectToAction("MyTutorial"); }

            if (group.Status != TutorialGroupStatus.Forming && group.Status != TutorialGroupStatus.Ready)
            { TempData["Error"] = "This group is not accepting new members."; return RedirectToAction("MyTutorial"); }

            if (group.Members.Count >= GroupMax)
            { TempData["Error"] = $"Group is full (max {GroupMax})."; return RedirectToAction("MyTutorial"); }

            _db.TutorialGroupMembers.Add(new TutorialGroupMember
            {
                TutorialGroupId = group.Id, StudentId = uid, IsLeader = false
            });

            var newCount = group.Members.Count + 1;
            group.Status = newCount >= GroupMin ? TutorialGroupStatus.Ready : TutorialGroupStatus.Forming;
            await _db.SaveChangesAsync();

            TempData["Success"] = $"You joined group '{group.Name}'.";
            return RedirectToAction("MyTutorial");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> LeaveTutorialGroup(int tutorialGroupId)
        {
            if (Guard() is { } r) return r;
            var uid = CurrentUserId;

            var group = await _db.TutorialGroups
                .Include(g => g.Members)
                .FirstOrDefaultAsync(g => g.Id == tutorialGroupId);
            if (group == null) return NotFound();

            if (group.Status == TutorialGroupStatus.AwaitingApproval || group.Status == TutorialGroupStatus.Approved)
            { TempData["Error"] = "Cannot leave a group once approval has been requested or granted."; return RedirectToAction("MyTutorial"); }

            var membership = group.Members.FirstOrDefault(m => m.StudentId == uid);
            if (membership == null) return RedirectToAction("MyTutorial");

            bool wasLeader = membership.IsLeader;
            _db.TutorialGroupMembers.Remove(membership);

            var remaining = group.Members.Count - 1;
            if (remaining == 0)
            {
                _db.TutorialGroups.Remove(group);
            }
            else
            {
                if (wasLeader)
                {
                    var next = group.Members.First(m => m.StudentId != uid);
                    next.IsLeader = true;
                }
                group.Status = remaining >= GroupMin ? TutorialGroupStatus.Ready : TutorialGroupStatus.Forming;
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = "You have left the group.";
            return RedirectToAction("MyTutorial");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestTutorialGroupApproval(int tutorialGroupId, string? message)
        {
            if (Guard() is { } r) return r;
            var uid = CurrentUserId;

            var group = await _db.TutorialGroups
                .Include(g => g.Members)
                .Include(g => g.Tutorial)
                .Include(g => g.GroupRequests)
                .FirstOrDefaultAsync(g => g.Id == tutorialGroupId);

            if (group == null || !group.Members.Any(m => m.StudentId == uid && m.IsLeader))
            { TempData["Error"] = "Only the group leader can request approval."; return RedirectToAction("MyTutorial"); }

            if (group.Members.Count < GroupMin)
            { TempData["Error"] = $"Need at least {GroupMin} members to request approval."; return RedirectToAction("MyTutorial"); }

            if (group.Status == TutorialGroupStatus.AwaitingApproval)
            { TempData["Error"] = "An approval request is already pending."; return RedirectToAction("MyTutorial"); }

            _db.TutorialGroupRequests.Add(new TutorialGroupRequest
            {
                TutorialGroupId = group.Id, Message = message
            });
            group.Status = TutorialGroupStatus.AwaitingApproval;
            await _db.SaveChangesAsync();

            NotificationHelper.Send(_db, group.Tutorial!.TutorId,
                $"Group Approval Request: {group.Name}",
                $"Group '{group.Name}' has {group.Members.Count} members and is requesting your approval.",
                $"/Tutor/TutorialDetails/{group.TutorialId}");

            TempData["Success"] = "Approval request sent to your tutor!";
            return RedirectToAction("MyTutorial");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestTutorialProjectApproval(int tutorialGroupId, int projectId, string? message)
        {
            if (Guard() is { } r) return r;
            var uid = CurrentUserId;

            var group = await _db.TutorialGroups
                .Include(g => g.Members)
                .Include(g => g.Tutorial)
                .Include(g => g.ProjectRequests)
                .FirstOrDefaultAsync(g => g.Id == tutorialGroupId);

            if (group == null || !group.Members.Any(m => m.StudentId == uid && m.IsLeader))
            { TempData["Error"] = "Only the group leader can request project approval."; return RedirectToAction("MyTutorial"); }

            if (group.Status != TutorialGroupStatus.Approved)
            { TempData["Error"] = "Your group must be approved first."; return RedirectToAction("MyTutorial"); }

            if (group.ApprovedProjectId != null)
            { TempData["Error"] = "Your group already has an approved project."; return RedirectToAction("MyTutorial"); }

            var pendingExists = group.ProjectRequests.Any(r => r.Status == TutorGroupRequestStatus.Pending);
            if (pendingExists)
            { TempData["Error"] = "A project request is already pending."; return RedirectToAction("MyTutorial"); }

            var project = await _db.Projects.FindAsync(projectId);
            if (project == null || project.Status != ProjectStatus.AdminApproved)
            { TempData["Error"] = "Invalid project selected."; return RedirectToAction("MyTutorial"); }

            _db.TutorialGroupProjectRequests.Add(new TutorialGroupProjectRequest
            {
                TutorialGroupId = group.Id, ProjectId = projectId, Message = message
            });
            await _db.SaveChangesAsync();

            NotificationHelper.Send(_db, group.Tutorial!.TutorId,
                $"Project Request: {project.Title}",
                $"Group '{group.Name}' has selected project '{project.Title}' and is requesting your approval.",
                $"/Tutor/TutorialDetails/{group.TutorialId}");

            TempData["Success"] = "Project request sent to your tutor!";
            return RedirectToAction("MyTutorial");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ReRequestTutorialGroupApproval(int tutorialGroupId, string? message)
        {
            if (Guard() is { } r) return r;
            var uid = CurrentUserId;

            var group = await _db.TutorialGroups
                .Include(g => g.Members)
                .Include(g => g.Tutorial)
                .FirstOrDefaultAsync(g => g.Id == tutorialGroupId);

            if (group == null || !group.Members.Any(m => m.StudentId == uid && m.IsLeader))
            { TempData["Error"] = "Only the group leader can re-request approval."; return RedirectToAction("MyTutorial"); }

            if (group.Status != TutorialGroupStatus.Rejected)
            { TempData["Error"] = "Only rejected groups can re-request approval."; return RedirectToAction("MyTutorial"); }

            _db.TutorialGroupRequests.Add(new TutorialGroupRequest
            {
                TutorialGroupId = group.Id, Message = message
            });
            group.Status = TutorialGroupStatus.AwaitingApproval;
            await _db.SaveChangesAsync();

            NotificationHelper.Send(_db, group.Tutorial!.TutorId,
                $"Group Re-Approval Request: {group.Name}",
                $"Group '{group.Name}' has re-submitted their approval request.",
                $"/Tutor/TutorialDetails/{group.TutorialId}");

            TempData["Success"] = "Re-approval request sent!";
            return RedirectToAction("MyTutorial");
        }

        // ─── Tutorial Weekly Progress ─────────────────────────────────────────

        public async Task<IActionResult> TutorialWeeklyProgress(int id)
        {
            if (Guard() is { } r) return r;
            var uid = CurrentUserId;

            var group = await _db.TutorialGroups
                .Include(g => g.Members)
                .Include(g => g.ApprovedProject).ThenInclude(p => p!.Company)
                .Include(g => g.Tutorial)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (group == null || !group.Members.Any(m => m.StudentId == uid)) return NotFound();
            if (group.ApprovedProject == null)
            { TempData["Error"] = "Your group does not have an approved project yet."; return RedirectToAction("MyTutorial"); }

            var entries = await _db.TutorialWeeklyProgressEntries
                .Where(wp => wp.TutorialGroupId == id)
                .Include(wp => wp.SubmittedByStudent)
                .OrderByDescending(wp => wp.WeekNumber)
                .ToListAsync();

            var isLeader  = group.Members.Any(m => m.StudentId == uid && m.IsLeader);
            var canSubmit = isLeader && (!entries.Any() || entries.First().TutorFeedback != null);
            var nextWeek  = entries.Any() ? entries.Max(e => e.WeekNumber) + 1 : 1;

            ViewBag.Group     = group;
            ViewBag.NextWeek  = nextWeek;
            ViewBag.CanSubmit = canSubmit;
            ViewBag.IsLeader  = isLeader;

            return View(entries);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [RequestSizeLimit(50 * 1024 * 1024)]
        public async Task<IActionResult> SubmitTutorialWeeklyProgress(SubmitTutorialWeeklyProgressVM model)
        {
            if (Guard() is { } r) return r;
            var uid = CurrentUserId;

            var group = await _db.TutorialGroups
                .Include(g => g.Members)
                .Include(g => g.ApprovedProject)
                .Include(g => g.Tutorial)
                .FirstOrDefaultAsync(g => g.Id == model.TutorialGroupId);

            if (group == null || group.ApprovedProject == null || !group.Members.Any(m => m.StudentId == uid))
            { TempData["Error"] = "Not allowed."; return RedirectToAction("MyTutorial"); }

            if (!group.Members.Any(m => m.StudentId == uid && m.IsLeader))
            { TempData["Error"] = "Only the group leader can submit weekly progress."; return RedirectToAction("TutorialWeeklyProgress", new { id = model.TutorialGroupId }); }

            var latest = await _db.TutorialWeeklyProgressEntries
                .Where(wp => wp.TutorialGroupId == model.TutorialGroupId)
                .OrderByDescending(wp => wp.WeekNumber)
                .FirstOrDefaultAsync();

            if (latest != null && latest.TutorFeedback == null)
            { TempData["Error"] = "Wait for tutor feedback on the previous week before submitting a new update."; return RedirectToAction("TutorialWeeklyProgress", new { id = model.TutorialGroupId }); }

            var weekNum = (latest?.WeekNumber ?? 0) + 1;

            string? filePath = null;
            string? originalFileName = null;
            if (model.File != null && model.File.Length > 0)
            {
                var dir = Path.Combine(_env.WebRootPath, "uploads", "weekly");
                Directory.CreateDirectory(dir);
                var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(model.File.FileName)}";
                filePath = $"uploads/weekly/{fileName}";
                using var stream = new FileStream(Path.Combine(_env.WebRootPath, filePath), FileMode.Create);
                await model.File.CopyToAsync(stream);
                originalFileName = Path.GetFileName(model.File.FileName);
            }

            _db.TutorialWeeklyProgressEntries.Add(new TutorialWeeklyProgress
            {
                TutorialGroupId      = model.TutorialGroupId,
                ProjectId            = group.ApprovedProject.Id,
                WeekNumber           = weekNum,
                StudentUpdate        = model.StudentUpdate,
                FilePath             = filePath,
                OriginalFileName     = originalFileName,
                SubmittedByStudentId = uid,
                TutorId              = group.Tutorial!.TutorId
            });
            await _db.SaveChangesAsync();

            NotificationHelper.Send(_db, group.Tutorial.TutorId,
                $"Week {weekNum} Progress — {group.Name}",
                $"Group '{group.Name}' submitted their week {weekNum} progress update.",
                $"/Tutor/TutorialDetails/{group.TutorialId}");

            TempData["Success"] = $"Week {weekNum} progress submitted!";
            return RedirectToAction("TutorialWeeklyProgress", new { id = model.TutorialGroupId });
        }
    }
}
