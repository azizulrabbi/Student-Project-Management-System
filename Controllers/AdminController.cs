using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPMS.Data;
using SPMS.Helpers;
using SPMS.Models;

namespace SPMS.Controllers
{
    public class AdminController : Controller
    {
        private readonly AppDbContext _db;

        public AdminController(AppDbContext db) => _db = db;

        private IActionResult? Guard()
        {
            if (!SessionHelper.IsLoggedIn(HttpContext.Session) || !SessionHelper.IsAdmin(HttpContext.Session))
                return RedirectToAction("Login", "Account");
            return null;
        }

        // ─── Dashboard ────────────────────────────────────────────────────────

        public async Task<IActionResult> Index()
        {
            if (Guard() is { } r) return r;
            ViewBag.UserName         = SessionHelper.GetUserName(HttpContext.Session);
            ViewBag.PendingReview    = await _db.Projects.CountAsync(p => p.Status == ProjectStatus.PendingAdminReview);
            ViewBag.ApprovedProjects = await _db.Projects.CountAsync(p => p.Status == ProjectStatus.AdminApproved);
            ViewBag.ActiveGroups     = await _db.TutorialGroups.CountAsync(g => g.Status == TutorialGroupStatus.Approved);
            ViewBag.PendingGroups    = await _db.TutorialGroupRequests.CountAsync(r => r.Status == TutorGroupRequestStatus.Pending);
            ViewBag.TotalTutorials   = await _db.Tutorials.CountAsync();
            ViewBag.TotalStudents    = await _db.Users.CountAsync(u => u.Role == UserRole.Student);
            ViewBag.TotalTutors      = await _db.Users.CountAsync(u => u.Role == UserRole.Tutor);
            ViewBag.TotalCompanies   = await _db.Users.CountAsync(u => u.Role == UserRole.Company);
            return View();
        }

        // ─── User Management ─────────────────────────────────────────────────

        public async Task<IActionResult> ManageUsers(string? role, string? search)
        {
            if (Guard() is { } r) return r;
            var q = _db.Users.AsQueryable();
            if (!string.IsNullOrEmpty(role) && Enum.TryParse<UserRole>(role, out var re))
                q = q.Where(u => u.Role == re);
            if (!string.IsNullOrEmpty(search))
                q = q.Where(u => u.FullName.Contains(search) || u.Email.Contains(search));
            ViewBag.CurrentRole = role; ViewBag.CurrentSearch = search;
            return View(await q.OrderBy(u => u.Role).ThenBy(u => u.FullName).ToListAsync());
        }

        public IActionResult CreateUser()
        {
            if (Guard() is { } r) return r;
            return View(new CreateUserVM { DOB = new DateTime(2000, 1, 1) });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(CreateUserVM model)
        {
            if (Guard() is { } r) return r;
            if (await _db.Users.AnyAsync(u => u.Email == model.Email))
                ModelState.AddModelError("Email", "Email already exists.");
            if (!ModelState.IsValid) return View(model);

            var pw = PasswordHelper.GenerateFromDOB(model.DOB);
            _db.Users.Add(new User
            {
                FullName = model.FullName, Email = model.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(pw),
                Role = model.Role, DOB = model.DOB, Phone = model.Phone, IsActive = true,
                StudentNumber = model.StudentNumber, Program = model.Program, Semester = model.Semester,
                Department = model.Department, Expertise = model.Expertise,
                CompanyName = model.CompanyName, Industry = model.Industry, ABN = model.ABN
            });
            await _db.SaveChangesAsync();
            TempData["Success"] = $"User created. Password: {pw}";
            return RedirectToAction("ManageUsers");
        }

        public async Task<IActionResult> EditUser(int id)
        {
            if (Guard() is { } r) return r;
            var u = await _db.Users.FindAsync(id);
            if (u == null) return NotFound();
            return View(new EditUserVM
            {
                Id = u.Id, FullName = u.FullName, Email = u.Email, Role = u.Role,
                DOB = u.DOB, Phone = u.Phone, IsActive = u.IsActive,
                StudentNumber = u.StudentNumber, Program = u.Program, Semester = u.Semester,
                Department = u.Department, Expertise = u.Expertise,
                CompanyName = u.CompanyName, Industry = u.Industry, ABN = u.ABN
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(EditUserVM model)
        {
            if (Guard() is { } r) return r;
            if (await _db.Users.AnyAsync(u => u.Email == model.Email && u.Id != model.Id))
                ModelState.AddModelError("Email", "Email already exists.");
            if (!ModelState.IsValid) return View(model);

            var u = await _db.Users.FindAsync(model.Id);
            if (u == null) return NotFound();
            u.FullName = model.FullName; u.Email = model.Email; u.Role = model.Role;
            u.DOB = model.DOB; u.Phone = model.Phone; u.IsActive = model.IsActive;
            u.StudentNumber = model.StudentNumber; u.Program = model.Program; u.Semester = model.Semester;
            u.Department = model.Department; u.Expertise = model.Expertise;
            u.CompanyName = model.CompanyName; u.Industry = model.Industry; u.ABN = model.ABN;
            await _db.SaveChangesAsync();
            TempData["Success"] = "User updated.";
            return RedirectToAction("ManageUsers");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(int id)
        {
            if (Guard() is { } r) return r;
            var u = await _db.Users.FindAsync(id);
            if (u == null) return NotFound();
            var pw = PasswordHelper.GenerateFromDOB(u.DOB);
            u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(pw);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Password reset to: {pw}";
            return RedirectToAction("ManageUsers");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUserStatus(int id)
        {
            if (Guard() is { } r) return r;
            var u = await _db.Users.FindAsync(id);
            if (u == null) return NotFound();
            u.IsActive = !u.IsActive;
            await _db.SaveChangesAsync();
            TempData["Success"] = $"User {(u.IsActive ? "activated" : "deactivated")}.";
            return RedirectToAction("ManageUsers");
        }

        // ─── Project Management ───────────────────────────────────────────────

        public async Task<IActionResult> ManageProjects(string? status)
        {
            if (Guard() is { } r) return r;
            var q = _db.Projects
                .Include(p => p.Company)
                .AsQueryable();
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<ProjectStatus>(status, out var s))
                q = q.Where(p => p.Status == s);
            ViewBag.CurrentStatus = status;
            ViewBag.PendingCount  = await _db.Projects.CountAsync(p => p.Status == ProjectStatus.PendingAdminReview);
            return View(await q.OrderByDescending(p => p.CreatedAt).ToListAsync());
        }

        public async Task<IActionResult> ReviewProject(int id)
        {
            if (Guard() is { } r) return r;
            var p = await _db.Projects
                .Include(p => p.Company)
                .Include(p => p.StudentSelections)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (p == null) return NotFound();
            return View(p);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveProject(int projectId, string? adminNotes, string? category)
        {
            if (Guard() is { } r) return r;
            var p = await _db.Projects.Include(p => p.Company).FirstOrDefaultAsync(p => p.Id == projectId);
            if (p == null) return NotFound();
            p.Status = ProjectStatus.AdminApproved;
            p.AdminNotes = adminNotes;
            if (category != null && Enum.TryParse<ProjectCategory>(category, out var cat))
                p.Category = cat;
            p.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            NotificationHelper.Send(_db, p.CompanyId, "Project Approved",
                $"Your project '{p.Title}' has been approved and is now visible to students.", "/Company/MyProjects");
            var tutorIds = await _db.Users
                .Where(u => u.Role == UserRole.Tutor && u.IsActive)
                .Select(u => u.Id)
                .ToListAsync();
            foreach (var tid in tutorIds)
                NotificationHelper.Send(_db, tid, "New Project Available",
                    $"Project '{p.Title}' has been approved and is now available for student groups.", "/Tutor/Dashboard");
            TempData["Success"] = "Project approved and published to students.";
            return RedirectToAction("ManageProjects");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectProject(int projectId, string? adminNotes)
        {
            if (Guard() is { } r) return r;
            var p = await _db.Projects.Include(p => p.Company).FirstOrDefaultAsync(p => p.Id == projectId);
            if (p == null) return NotFound();
            p.Status = ProjectStatus.AdminRejected;
            p.AdminNotes = adminNotes;
            p.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            NotificationHelper.Send(_db, p.CompanyId, "Project Rejected",
                $"Your project '{p.Title}' was not approved. Notes: {adminNotes ?? "N/A"}", "/Company/MyProjects");
            TempData["Error"] = "Project rejected.";
            return RedirectToAction("ManageProjects");
        }

        public async Task<IActionResult> EditProject(int id)
        {
            if (Guard() is { } r) return r;
            var p = await _db.Projects
                .Include(p => p.StudentSelections)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (p == null) return NotFound();
            if (p.StudentSelections.Any())
            {
                TempData["Error"] = "Cannot edit a project that students have already selected.";
                return RedirectToAction("ManageProjects");
            }
            return View(new EditProjectVM
            {
                Id = p.Id, Title = p.Title, Description = p.Description,
                Category = p.Category, AdminNotes = p.AdminNotes
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProject(EditProjectVM model)
        {
            if (Guard() is { } r) return r;
            if (!ModelState.IsValid) return View(model);
            var p = await _db.Projects
                .Include(p => p.StudentSelections)
                .FirstOrDefaultAsync(p => p.Id == model.Id);
            if (p == null) return NotFound();
            if (p.StudentSelections.Any())
            {
                TempData["Error"] = "Cannot edit a project that students have already selected.";
                return RedirectToAction("ManageProjects");
            }
            p.Title = model.Title; p.Description = model.Description;
            p.Category = model.Category; p.AdminNotes = model.AdminNotes;
            p.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Project updated.";
            return RedirectToAction("ManageProjects");
        }

        // ─── Groups overview ──────────────────────────────────────────────────

        public async Task<IActionResult> ManageGroups()
        {
            if (Guard() is { } r) return r;
            var groups = await _db.TutorialGroups
                .Include(g => g.Members).ThenInclude(m => m.Student)
                .Include(g => g.ApprovedProject)
                .Include(g => g.Tutorial).ThenInclude(t => t!.Tutor)
                .OrderByDescending(g => g.CreatedAt)
                .ToListAsync();
            return View(groups);
        }

        // ─── Tutorials ────────────────────────────────────────────────────────

        public async Task<IActionResult> Tutorials()
        {
            if (Guard() is { } r) return r;
            var tutorials = await _db.Tutorials
                .Include(t => t.Tutor)
                .Include(t => t.Enrollments)
                .Include(t => t.Groups).ThenInclude(g => g.Members)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
            return View(tutorials);
        }

        public async Task<IActionResult> CreateTutorial()
        {
            if (Guard() is { } r) return r;
            ViewBag.Tutors = await _db.Users.Where(u => u.Role == UserRole.Tutor && u.IsActive).ToListAsync();
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTutorial(string name, int tutorId)
        {
            if (Guard() is { } r) return r;
            _db.Tutorials.Add(new Tutorial { Name = name, TutorId = tutorId });
            await _db.SaveChangesAsync();
            TempData["Success"] = "Tutorial created.";
            return RedirectToAction("Tutorials");
        }

        public async Task<IActionResult> TutorialDetails(int id)
        {
            if (Guard() is { } r) return r;
            var tutorial = await _db.Tutorials
                .Include(t => t.Tutor)
                .Include(t => t.Enrollments).ThenInclude(e => e.Student)
                .Include(t => t.Groups).ThenInclude(g => g.Members).ThenInclude(m => m.Student)
                .Include(t => t.Groups).ThenInclude(g => g.ApprovedProject)
                .Include(t => t.Announcements)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (tutorial == null) return NotFound();

            var enrolledAnywhereIds = await _db.TutorialEnrollments.Select(e => e.StudentId).Distinct().ToListAsync();
            ViewBag.AvailableStudents = await _db.Users
                .Where(u => u.Role == UserRole.Student && u.IsActive && !enrolledAnywhereIds.Contains(u.Id))
                .OrderBy(u => u.FullName).ToListAsync();

            return View(tutorial);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EnrollStudent(int tutorialId, int studentId)
        {
            if (Guard() is { } r) return r;
            var existing = await _db.TutorialEnrollments
                .Include(e => e.Tutorial)
                .FirstOrDefaultAsync(e => e.StudentId == studentId);
            if (existing != null)
            {
                if (existing.TutorialId == tutorialId)
                    TempData["Error"] = "Student is already enrolled in this tutorial.";
                else
                    TempData["Error"] = $"Student is already enrolled in another tutorial ({existing.Tutorial?.Name}). A student can only belong to one tutorial.";
                return RedirectToAction("TutorialDetails", new { id = tutorialId });
            }
            _db.TutorialEnrollments.Add(new TutorialEnrollment { TutorialId = tutorialId, StudentId = studentId });
            await _db.SaveChangesAsync();
            TempData["Success"] = "Student enrolled.";
            return RedirectToAction("TutorialDetails", new { id = tutorialId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UnenrollStudent(int tutorialId, int enrollmentId)
        {
            if (Guard() is { } r) return r;
            var entry = await _db.TutorialEnrollments.FindAsync(enrollmentId);
            if (entry != null) _db.TutorialEnrollments.Remove(entry);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Student removed from tutorial.";
            return RedirectToAction("TutorialDetails", new { id = tutorialId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTutorial(int id)
        {
            if (Guard() is { } r) return r;
            var t = await _db.Tutorials.FindAsync(id);
            if (t != null) _db.Tutorials.Remove(t);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Tutorial deleted.";
            return RedirectToAction("Tutorials");
        }
    }
}
