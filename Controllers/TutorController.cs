using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPMS.Data;
using SPMS.Helpers;
using SPMS.Models;

namespace SPMS.Controllers
{
    public class TutorController : Controller
    {
        private readonly AppDbContext _db;

        public TutorController(AppDbContext db) => _db = db;

        private IActionResult? Guard()
        {
            if (!SessionHelper.IsLoggedIn(HttpContext.Session) || !SessionHelper.IsTutor(HttpContext.Session))
                return RedirectToAction("Login", "Account");
            return null;
        }

        private int CurrentUserId => SessionHelper.GetUserId(HttpContext.Session)!.Value;

        public async Task<IActionResult> Dashboard()
        {
            if (Guard() is { } r) return r;
            var uid = CurrentUserId;
            ViewBag.UserName         = SessionHelper.GetUserName(HttpContext.Session);
            ViewBag.PendingRequests  = await _db.TutorialGroupRequests.CountAsync(r => r.Status == TutorGroupRequestStatus.Pending && r.TutorialGroup!.Tutorial!.TutorId == uid)
                                     + await _db.TutorialGroupProjectRequests.CountAsync(r => r.Status == TutorGroupRequestStatus.Pending && r.TutorialGroup!.Tutorial!.TutorId == uid);
            ViewBag.ActiveGroups     = await _db.TutorialGroups.CountAsync(g => g.Status == TutorialGroupStatus.Approved && g.Tutorial!.TutorId == uid);
            ViewBag.AwaitingFeedback = await _db.TutorialWeeklyProgressEntries.CountAsync(wp => wp.TutorId == uid && wp.TutorFeedback == null);
            ViewBag.UnreadMessages   = await _db.Messages.CountAsync(m => m.ReceiverId == uid && !m.IsRead);
            ViewBag.UnreadNotifications = await _db.Notifications.CountAsync(n => n.UserId == uid && !n.IsRead);
            return View();
        }

        public async Task<IActionResult> PendingRequests()
        {
            if (Guard() is { } r) return r;
            var uid = CurrentUserId;

            var groupRequests = await _db.TutorialGroupRequests
                .Where(r => r.Status == TutorGroupRequestStatus.Pending && r.TutorialGroup!.Tutorial!.TutorId == uid)
                .Include(r => r.TutorialGroup).ThenInclude(g => g!.Members).ThenInclude(m => m.Student)
                .Include(r => r.TutorialGroup).ThenInclude(g => g!.Tutorial)
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync();

            var projectRequests = await _db.TutorialGroupProjectRequests
                .Where(r => r.Status == TutorGroupRequestStatus.Pending && r.TutorialGroup!.Tutorial!.TutorId == uid)
                .Include(r => r.TutorialGroup).ThenInclude(g => g!.Members).ThenInclude(m => m.Student)
                .Include(r => r.TutorialGroup).ThenInclude(g => g!.Tutorial)
                .Include(r => r.Project).ThenInclude(p => p!.Company)
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync();

            ViewBag.GroupRequests   = groupRequests;
            ViewBag.ProjectRequests = projectRequests;
            return View();
        }

public async Task<IActionResult> ActiveGroups()
        {
            if (Guard() is { } r) return r;
            var uid = CurrentUserId;
            var groups = await _db.TutorialGroups
                .Where(g => g.Status == TutorialGroupStatus.Approved && g.Tutorial!.TutorId == uid)
                .Include(g => g.Members).ThenInclude(m => m.Student)
                .Include(g => g.ApprovedProject).ThenInclude(p => p!.Company)
                .Include(g => g.Tutorial)
                .ToListAsync();
            return View(groups);
        }

        // ─── Tutorials ────────────────────────────────────────────────────────

        public async Task<IActionResult> MyTutorials()
        {
            if (Guard() is { } r) return r;
            var uid = CurrentUserId;
            var tutorials = await _db.Tutorials
                .Where(t => t.TutorId == uid)
                .Include(t => t.Enrollments)
                .Include(t => t.Groups).ThenInclude(g => g.Members)
                .Include(t => t.Announcements)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
            return View(tutorials);
        }

        public async Task<IActionResult> TutorialDetails(int id)
        {
            if (Guard() is { } r) return r;
            var uid = CurrentUserId;
            var tutorial = await _db.Tutorials
                .Include(t => t.Tutor)
                .Include(t => t.Enrollments).ThenInclude(e => e.Student)
                .Include(t => t.Groups).ThenInclude(g => g.Members).ThenInclude(m => m.Student)
                .Include(t => t.Groups).ThenInclude(g => g.GroupRequests)
                .Include(t => t.Groups).ThenInclude(g => g.ProjectRequests).ThenInclude(r => r.Project)
                .Include(t => t.Groups).ThenInclude(g => g.ApprovedProject)
                .Include(t => t.Announcements)
                .FirstOrDefaultAsync(t => t.Id == id && t.TutorId == uid);
            if (tutorial == null) return NotFound();

            var groupIds = tutorial.Groups.Select(g => g.Id).ToList();
            var weeklyEntries = await _db.TutorialWeeklyProgressEntries
                .Where(wp => groupIds.Contains(wp.TutorialGroupId))
                .Include(wp => wp.TutorialGroup)
                .Include(wp => wp.Project)
                .Include(wp => wp.SubmittedByStudent)
                .OrderBy(wp => wp.TutorialGroupId).ThenByDescending(wp => wp.WeekNumber)
                .ToListAsync();

            ViewBag.WeeklyEntries = weeklyEntries;
            return View(tutorial);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveTutorialGroup(int requestId, string? tutorFeedback)
        {
            if (Guard() is { } r) return r;
            var uid = CurrentUserId;
            var req = await _db.TutorialGroupRequests
                .Include(r => r.TutorialGroup).ThenInclude(g => g!.Tutorial)
                .Include(r => r.TutorialGroup).ThenInclude(g => g!.Members)
                .FirstOrDefaultAsync(r => r.Id == requestId && r.TutorialGroup!.Tutorial!.TutorId == uid);
            if (req == null) return NotFound();

            req.Status = TutorGroupRequestStatus.Approved;
            req.TutorFeedback = tutorFeedback;
            req.RespondedAt = DateTime.Now;
            req.TutorialGroup!.Status = TutorialGroupStatus.Approved;
            await _db.SaveChangesAsync();

            foreach (var m in req.TutorialGroup.Members)
                NotificationHelper.Send(_db, m.StudentId, "Tutorial Group Approved!",
                    $"Your group '{req.TutorialGroup.Name}' has been approved. Your leader can now select a project.",
                    "/Student/MyTutorial");

            TempData["Success"] = $"Group '{req.TutorialGroup.Name}' approved.";
            return RedirectToAction("TutorialDetails", new { id = req.TutorialGroup.TutorialId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectTutorialGroup(int requestId, string? tutorFeedback)
        {
            if (Guard() is { } r) return r;
            var uid = CurrentUserId;
            var req = await _db.TutorialGroupRequests
                .Include(r => r.TutorialGroup).ThenInclude(g => g!.Tutorial)
                .Include(r => r.TutorialGroup).ThenInclude(g => g!.Members)
                .FirstOrDefaultAsync(r => r.Id == requestId && r.TutorialGroup!.Tutorial!.TutorId == uid);
            if (req == null) return NotFound();

            req.Status = TutorGroupRequestStatus.Rejected;
            req.TutorFeedback = tutorFeedback;
            req.RespondedAt = DateTime.Now;
            req.TutorialGroup!.Status = TutorialGroupStatus.Rejected;
            await _db.SaveChangesAsync();

            foreach (var m in req.TutorialGroup.Members)
                NotificationHelper.Send(_db, m.StudentId, "Tutorial Group Rejected",
                    $"Your group '{req.TutorialGroup.Name}' was rejected. Feedback: {tutorFeedback ?? "N/A"}",
                    "/Student/MyTutorial");

            TempData["Error"] = $"Group '{req.TutorialGroup.Name}' rejected.";
            return RedirectToAction("TutorialDetails", new { id = req.TutorialGroup.TutorialId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveTutorialProjectRequest(int requestId, string? tutorFeedback)
        {
            if (Guard() is { } r) return r;
            var uid = CurrentUserId;
            var req = await _db.TutorialGroupProjectRequests
                .Include(r => r.TutorialGroup).ThenInclude(g => g!.Tutorial)
                .Include(r => r.TutorialGroup).ThenInclude(g => g!.Members)
                .Include(r => r.Project)
                .FirstOrDefaultAsync(r => r.Id == requestId && r.TutorialGroup!.Tutorial!.TutorId == uid);
            if (req == null) return NotFound();

            req.Status = TutorGroupRequestStatus.Approved;
            req.TutorFeedback = tutorFeedback;
            req.RespondedAt = DateTime.Now;
            req.TutorialGroup!.ApprovedProjectId = req.ProjectId;
            await _db.SaveChangesAsync();

            foreach (var m in req.TutorialGroup.Members)
                NotificationHelper.Send(_db, m.StudentId, "Project Approved!",
                    $"Your project '{req.Project!.Title}' has been approved for group '{req.TutorialGroup.Name}'.",
                    "/Student/MyTutorial");

            // Notify the company that their project has been assigned to a group
            NotificationHelper.Send(_db, req.Project!.CompanyId, "Your Project Has Been Assigned",
                $"Your project '{req.Project.Title}' has been assigned to group '{req.TutorialGroup.Name}' and work will begin soon.",
                "/Company/MyProjects");

            TempData["Success"] = $"Project '{req.Project!.Title}' approved.";
            return RedirectToAction("TutorialDetails", new { id = req.TutorialGroup.TutorialId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectTutorialProjectRequest(int requestId, string? tutorFeedback)
        {
            if (Guard() is { } r) return r;
            var uid = CurrentUserId;
            var req = await _db.TutorialGroupProjectRequests
                .Include(r => r.TutorialGroup).ThenInclude(g => g!.Tutorial)
                .Include(r => r.TutorialGroup).ThenInclude(g => g!.Members)
                .Include(r => r.Project)
                .FirstOrDefaultAsync(r => r.Id == requestId && r.TutorialGroup!.Tutorial!.TutorId == uid);
            if (req == null) return NotFound();

            req.Status = TutorGroupRequestStatus.Rejected;
            req.TutorFeedback = tutorFeedback;
            req.RespondedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            foreach (var m in req.TutorialGroup!.Members)
                NotificationHelper.Send(_db, m.StudentId, "Project Request Rejected",
                    $"Project '{req.Project!.Title}' was rejected. Feedback: {tutorFeedback ?? "N/A"}. Your leader can select a different project.",
                    "/Student/MyTutorial");

            TempData["Error"] = $"Project request rejected.";
            return RedirectToAction("TutorialDetails", new { id = req.TutorialGroup.TutorialId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> PostAnnouncement(int tutorialId, string title, string body)
        {
            if (Guard() is { } r) return r;
            var uid = CurrentUserId;
            var tutorial = await _db.Tutorials
                .Include(t => t.Enrollments)
                .FirstOrDefaultAsync(t => t.Id == tutorialId && t.TutorId == uid);
            if (tutorial == null) return NotFound();

            _db.TutorialAnnouncements.Add(new TutorialAnnouncement
            {
                TutorialId = tutorialId, Title = title, Body = body
            });

            foreach (var e in tutorial.Enrollments)
                NotificationHelper.Send(_db, e.StudentId,
                    $"New announcement: {title}",
                    body.Length > 80 ? body[..80] + "..." : body,
                    "/Student/MyTutorial");

            await _db.SaveChangesAsync();
            TempData["Success"] = "Announcement posted.";
            return RedirectToAction("TutorialDetails", new { id = tutorialId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> GiveTutorialWeeklyFeedback(TutorialWeeklyFeedbackVM model)
        {
            if (Guard() is { } r) return r;
            var uid = CurrentUserId;

            var entry = await _db.TutorialWeeklyProgressEntries
                .Include(wp => wp.TutorialGroup).ThenInclude(g => g!.Members)
                .FirstOrDefaultAsync(wp => wp.Id == model.EntryId && wp.TutorId == uid);
            if (entry == null) return NotFound();

            entry.TutorFeedback    = model.TutorFeedback;
            entry.TutorRespondedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            foreach (var m in entry.TutorialGroup!.Members)
                NotificationHelper.Send(_db, m.StudentId,
                    $"Tutor Feedback — Week {entry.WeekNumber}",
                    $"Your tutor has given feedback on your week {entry.WeekNumber} progress.",
                    $"/Student/TutorialWeeklyProgress/{entry.TutorialGroupId}");

            TempData["Success"] = $"Feedback submitted for Week {entry.WeekNumber}.";
            return RedirectToAction("TutorialDetails", new { id = entry.TutorialGroup.TutorialId });
        }
    }
}
