using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPMS.Data;
using SPMS.Helpers;
using SPMS.Models;

namespace SPMS.Controllers
{
    public class CompanyController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public CompanyController(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        private IActionResult? Guard()
        {
            if (!SessionHelper.IsLoggedIn(HttpContext.Session) || !SessionHelper.IsCompany(HttpContext.Session))
                return RedirectToAction("Login", "Account");
            return null;
        }

        private int CurrentUserId => SessionHelper.GetUserId(HttpContext.Session)!.Value;

        // Checklist template lives in Helpers/ChecklistTemplate.cs (shared with StudentController).
        private static int ChecklistTotalItems => ChecklistTemplate.TotalItems;

        public async Task<IActionResult> Dashboard()
        {
            if (Guard() is { } r) return r;
            var uid = CurrentUserId;
            ViewBag.UserName        = SessionHelper.GetUserName(HttpContext.Session);
            ViewBag.TotalSubmitted  = await _db.Projects.CountAsync(p => p.CompanyId == uid);
            ViewBag.Pending         = await _db.Projects.CountAsync(p => p.CompanyId == uid && p.Status == ProjectStatus.PendingAdminReview);
            ViewBag.Approved        = await _db.Projects.CountAsync(p => p.CompanyId == uid && p.Status == ProjectStatus.AdminApproved);
            ViewBag.Active          = await _db.Projects.CountAsync(p => p.CompanyId == uid && p.Status == ProjectStatus.TutorApproved);
            ViewBag.UnreadMessages  = await _db.Messages.CountAsync(m => m.ReceiverId == uid && !m.IsRead);
            ViewBag.UnreadNotifications = await _db.Notifications.CountAsync(n => n.UserId == uid && !n.IsRead);

            var projects = await _db.Projects
                .Where(p => p.CompanyId == uid)
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .ToListAsync();

            var projectIds = projects.Select(p => p.Id).ToList();

            var assignedGroups = await _db.TutorialGroups
                .Where(g => g.ApprovedProjectId != null && projectIds.Contains(g.ApprovedProjectId!.Value))
                .Include(g => g.Tutorial).ThenInclude(t => t!.Tutor)
                .ToListAsync();
            ViewBag.AssignedGroups = assignedGroups.ToDictionary(g => g.ApprovedProjectId!.Value);

            ViewBag.Active = assignedGroups.Count;

            var checklistCounts = await _db.ProjectChecklists
                .Where(c => projectIds.Contains(c.ProjectId))
                .GroupBy(c => c.ProjectId)
                .Select(g => new { ProjectId = g.Key, Count = g.Count() })
                .ToListAsync();
            ViewBag.ChecklistCounts = checklistCounts.ToDictionary(x => x.ProjectId, x => x.Count);
            ViewBag.ChecklistTotal  = ChecklistTotalItems;

            return View(projects);
        }

        public async Task<IActionResult> MyProjects()
        {
            if (Guard() is { } r) return r;
            var uid = CurrentUserId;
            var projects = await _db.Projects
                .Where(p => p.CompanyId == uid)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            var projectIds = projects.Select(p => p.Id).ToList();
            var assignedGroups = await _db.TutorialGroups
                .Where(g => g.ApprovedProjectId != null && projectIds.Contains(g.ApprovedProjectId!.Value))
                .Include(g => g.Tutorial).ThenInclude(t => t!.Tutor)
                .ToListAsync();
            ViewBag.AssignedGroups = assignedGroups.ToDictionary(g => g.ApprovedProjectId!.Value);

            return View(projects);
        }

[HttpGet]
        public async Task<IActionResult> GetChecklist(int projectId)
        {
            if (Guard() is { } r) return r;
            var rows = await _db.ProjectChecklists
                .Where(c => c.ProjectId == projectId)
                .ToListAsync();
            var byKey = rows.ToDictionary(c => c.ItemKey);
            var sections = ChecklistTemplate.Sections.Select(s => new {
                section = s.Title,
                items   = s.Items.Select(i => new {
                    key              = i.Key,
                    label            = i.Label,
                    isChecked        = byKey.ContainsKey(i.Key),
                    filePath         = byKey.ContainsKey(i.Key) ? byKey[i.Key].FilePath : null,
                    originalFileName = byKey.ContainsKey(i.Key) ? byKey[i.Key].OriginalFileName : null,
                    uploadedAt       = byKey.ContainsKey(i.Key) ? byKey[i.Key].UploadedAt : null
                }).ToList()
            }).ToList();
            return Json(new { sections, total = ChecklistTotalItems, checkedCount = rows.Count });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleChecklistItem(int projectId, string itemKey)
        {
            if (Guard() is { } r) return r;
            var existing = await _db.ProjectChecklists
                .FirstOrDefaultAsync(c => c.ProjectId == projectId && c.ItemKey == itemKey);
            bool nowChecked;
            if (existing != null)
            {
                // Removing the checked row also deletes any uploaded student file (kept consistent).
                DeleteUploadedFile(existing.FilePath);
                _db.ProjectChecklists.Remove(existing);
                nowChecked = false;
            }
            else
            {
                _db.ProjectChecklists.Add(new ProjectChecklist { ProjectId = projectId, ItemKey = itemKey });
                nowChecked = true;
            }
            await _db.SaveChangesAsync();
            var total = await _db.ProjectChecklists.CountAsync(c => c.ProjectId == projectId);
            return Json(new { isChecked = nowChecked, checkedCount = total });
        }

        public IActionResult SubmitProject()
        {
            if (Guard() is { } r) return r;
            return View(new SubmitProjectVM());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitProject(SubmitProjectVM model)
        {
            if (Guard() is { } r) return r;

            if (!ModelState.IsValid) return View(model);

            string? filePath = null;
            if (model.File != null && model.File.Length > 0)
            {
                var dir = Path.Combine(_env.WebRootPath, "uploads", "projects");
                Directory.CreateDirectory(dir);
                var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(model.File.FileName)}";
                filePath = $"uploads/projects/{fileName}";
                using var stream = new FileStream(Path.Combine(_env.WebRootPath, filePath), FileMode.Create);
                await model.File.CopyToAsync(stream);
            }

            var project = new Project
            {
                Title = model.Title,
                Description = model.Description,
                FilePath = filePath,
                CompanyId = CurrentUserId,
                Status = ProjectStatus.PendingAdminReview
            };
            _db.Projects.Add(project);
            await _db.SaveChangesAsync();

            // Notify all admins
            var admins = await _db.Users.Where(u => u.Role == UserRole.Admin).ToListAsync();
            foreach (var a in admins)
                NotificationHelper.Send(_db, a.Id, "New Project Submitted",
                    $"Company '{SessionHelper.GetUserName(HttpContext.Session)}' submitted project '{model.Title}' for review.",
                    "/Admin/ManageProjects");

            TempData["Success"] = "Project submitted successfully! Awaiting admin review.";
            return RedirectToAction("MyProjects");
        }

        public async Task<IActionResult> ProjectDetails(int id)
        {
            if (Guard() is { } r) return r;
            var project = await _db.Projects
                .Include(p => p.Tutor)
                .FirstOrDefaultAsync(p => p.Id == id && p.CompanyId == CurrentUserId);
            if (project == null) return NotFound();

            var assignedGroup = await _db.TutorialGroups
                .Where(g => g.ApprovedProjectId == id)
                .Include(g => g.Members)
                    .ThenInclude(m => m.Student)
                .Include(g => g.Tutorial)
                    .ThenInclude(t => t!.Tutor)
                .FirstOrDefaultAsync();
            ViewBag.AssignedGroup = assignedGroup;
            ViewBag.AssignedTutor = assignedGroup?.Tutorial?.Tutor;

            return View(project);
        }

        [HttpGet]
        public async Task<IActionResult> EditProject(int id)
        {
            if (Guard() is { } r) return r;
            var project = await _db.Projects
                .FirstOrDefaultAsync(p => p.Id == id && p.CompanyId == CurrentUserId);
            if (project == null) return NotFound();
            if (project.Status != ProjectStatus.PendingAdminReview)
            {
                TempData["Error"] = "This project can no longer be edited — it has already been reviewed by admin.";
                return RedirectToAction("ProjectDetails", new { id });
            }
            ViewBag.ExistingFilePath = project.FilePath;
            ViewBag.ProjectId = project.Id;
            return View(new SubmitProjectVM
            {
                Title = project.Title,
                Description = project.Description,
                Category = project.Category
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProject(int id, SubmitProjectVM model, bool removeExistingFile = false)
        {
            if (Guard() is { } r) return r;
            var project = await _db.Projects
                .FirstOrDefaultAsync(p => p.Id == id && p.CompanyId == CurrentUserId);
            if (project == null) return NotFound();
            if (project.Status != ProjectStatus.PendingAdminReview)
            {
                TempData["Error"] = "This project can no longer be edited — it has already been reviewed by admin.";
                return RedirectToAction("ProjectDetails", new { id });
            }
            if (!ModelState.IsValid)
            {
                ViewBag.ExistingFilePath = project.FilePath;
                ViewBag.ProjectId = project.Id;
                return View(model);
            }

            project.Title = model.Title;
            project.Description = model.Description;
            project.Category = model.Category;
            project.UpdatedAt = DateTime.Now;

            if (model.File != null && model.File.Length > 0)
            {
                DeleteUploadedFile(project.FilePath);
                var dir = Path.Combine(_env.WebRootPath, "uploads", "projects");
                Directory.CreateDirectory(dir);
                var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(model.File.FileName)}";
                var newRelative = $"uploads/projects/{fileName}";
                using var stream = new FileStream(Path.Combine(_env.WebRootPath, newRelative), FileMode.Create);
                await model.File.CopyToAsync(stream);
                project.FilePath = newRelative;
            }
            else if (removeExistingFile)
            {
                DeleteUploadedFile(project.FilePath);
                project.FilePath = null;
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = "Project updated.";
            return RedirectToAction("ProjectDetails", new { id });
        }

        private void DeleteUploadedFile(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return;
            var full = Path.Combine(_env.WebRootPath, relativePath);
            if (System.IO.File.Exists(full))
            {
                try { System.IO.File.Delete(full); } catch { /* leave orphaned file rather than crash */ }
            }
        }
    }
}
