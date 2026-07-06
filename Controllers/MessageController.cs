using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPMS.Data;
using SPMS.Helpers;
using SPMS.Models;

namespace SPMS.Controllers
{
    public class MessageController : Controller
    {
        private readonly AppDbContext _db;

        public MessageController(AppDbContext db) => _db = db;

        private IActionResult? Guard()
        {
            if (!SessionHelper.IsLoggedIn(HttpContext.Session))
                return RedirectToAction("Login", "Account");
            // Admin doesn't use messaging (email-based)
            if (SessionHelper.IsAdmin(HttpContext.Session))
                return RedirectToAction("Index", "Admin");
            return null;
        }

        private int CurrentUserId => SessionHelper.GetUserId(HttpContext.Session)!.Value;

        // Inbox: show all conversation partners
        public async Task<IActionResult> Index()
        {
            if (Guard() is { } r) return r;

            var userId = CurrentUserId;
            var role = SessionHelper.GetUserRole(HttpContext.Session);

            // Get all unique users I've had a conversation with
            var sentTo = await _db.Messages
                .Where(m => m.SenderId == userId)
                .Select(m => m.ReceiverId)
                .Distinct()
                .ToListAsync();

            var receivedFrom = await _db.Messages
                .Where(m => m.ReceiverId == userId)
                .Select(m => m.SenderId)
                .Distinct()
                .ToListAsync();

            var conversationUserIds = sentTo.Union(receivedFrom).Distinct().ToList();

            var conversationUsers = await _db.Users
                .Where(u => conversationUserIds.Contains(u.Id))
                .ToListAsync();

            // Unread count per sender
            var unreadCounts = await _db.Messages
                .Where(m => m.ReceiverId == userId && !m.IsRead)
                .GroupBy(m => m.SenderId)
                .Select(g => new { SenderId = g.Key, Count = g.Count() })
                .ToListAsync();

            ViewBag.UnreadCounts = unreadCounts.ToDictionary(x => x.SenderId, x => x.Count);
            ViewBag.UserName = SessionHelper.GetUserName(HttpContext.Session);
            ViewBag.CurrentUserId = userId;

            // Available users to message based on role
            ViewBag.AvailableUsers = await GetAvailableUsers(userId, role!);

            return View(conversationUsers);
        }

        // Conversation with a specific user
        public async Task<IActionResult> Conversation(int id)
        {
            if (Guard() is { } r) return r;

            var currentUserId = CurrentUserId;
            var otherUser = await _db.Users.FindAsync(id);
            if (otherUser == null) return NotFound();

            // Load messages between the two users
            var messages = await _db.Messages
                .Where(m => (m.SenderId == currentUserId && m.ReceiverId == id) ||
                            (m.SenderId == id && m.ReceiverId == currentUserId))
                .Include(m => m.Sender)
                .OrderBy(m => m.SentAt)
                .ToListAsync();

            // Mark received messages as read
            var unread = messages.Where(m => m.ReceiverId == currentUserId && !m.IsRead).ToList();
            foreach (var msg in unread)
                msg.IsRead = true;
            if (unread.Any())
                await _db.SaveChangesAsync();

            ViewBag.OtherUser = otherUser;
            ViewBag.CurrentUserId = currentUserId;
            ViewBag.UserName = SessionHelper.GetUserName(HttpContext.Session);

            return View(messages);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Send(SendMessageVM model)
        {
            if (Guard() is { } r) return r;

            if (!ModelState.IsValid)
                return RedirectToAction("Conversation", new { id = model.ReceiverId });

            var currentUserId = CurrentUserId;

            // Verify the recipient exists and is allowed
            var receiver = await _db.Users.FindAsync(model.ReceiverId);
            if (receiver == null) return NotFound();

            _db.Messages.Add(new Message
            {
                SenderId = currentUserId,
                ReceiverId = model.ReceiverId,
                Content = model.Content
            });

            await _db.SaveChangesAsync();

            // Notify receiver
            var senderName = SessionHelper.GetUserName(HttpContext.Session);
            NotificationHelper.Send(_db, model.ReceiverId,
                $"New message from {senderName}",
                model.Content.Length > 80 ? model.Content[..80] + "..." : model.Content,
                $"/Message/Conversation?id={currentUserId}");

            return RedirectToAction("Conversation", new { id = model.ReceiverId });
        }

        // Compose: pick a user to message
        public async Task<IActionResult> Compose()
        {
            if (Guard() is { } r) return r;

            var userId = CurrentUserId;
            var role = SessionHelper.GetUserRole(HttpContext.Session);

            ViewBag.UserName = SessionHelper.GetUserName(HttpContext.Session);
            ViewBag.AvailableUsers = await GetAvailableUsers(userId, role!);

            return View();
        }

        private async Task<List<User>> GetAvailableUsers(int currentUserId, string role)
        {
            if (role == "Company")
            {
                // Company can only message: tutor(s) and group leader(s) of their own projects
                var tutorIds = await _db.Projects
                    .Where(p => p.CompanyId == currentUserId && p.TutorId != null)
                    .Select(p => p.TutorId!.Value)
                    .Distinct()
                    .ToListAsync();

                // Find group leaders whose tutorial group has an approved project belonging to this company
                var companyProjectIds = await _db.Projects
                    .Where(p => p.CompanyId == currentUserId)
                    .Select(p => p.Id)
                    .ToListAsync();

                var leaderIds = await _db.TutorialGroupMembers
                    .Where(m => m.IsLeader && m.TutorialGroup!.ApprovedProjectId != null
                             && companyProjectIds.Contains(m.TutorialGroup.ApprovedProjectId!.Value))
                    .Select(m => m.StudentId)
                    .Distinct()
                    .ToListAsync();

                var allowedIds = tutorIds.Union(leaderIds).ToList();

                return await _db.Users
                    .Where(u => allowedIds.Contains(u.Id) && u.IsActive)
                    .OrderBy(u => u.Role)
                    .ThenBy(u => u.FullName)
                    .ToListAsync();
            }

            // Student  → can message: Tutors, Company users
            // Tutor    → can message: Students, Company users
            IQueryable<User> query = role switch
            {
                "Student" => _db.Users.Where(u => u.Role == UserRole.Tutor || u.Role == UserRole.Company),
                "Tutor"   => _db.Users.Where(u => u.Role == UserRole.Student || u.Role == UserRole.Company),
                _         => _db.Users.Where(u => false)
            };

            return await query
                .Where(u => u.IsActive && u.Id != currentUserId)
                .OrderBy(u => u.Role)
                .ThenBy(u => u.FullName)
                .ToListAsync();
        }
    }
}
