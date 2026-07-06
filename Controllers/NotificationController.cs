using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPMS.Data;
using SPMS.Helpers;
using SPMS.Models;

namespace SPMS.Controllers
{
    public class NotificationController : Controller
    {
        private readonly AppDbContext _db;

        public NotificationController(AppDbContext db) => _db = db;

        private IActionResult? Guard()
        {
            if (!SessionHelper.IsLoggedIn(HttpContext.Session))
                return RedirectToAction("Login", "Account");
            return null;
        }

        private int CurrentUserId => SessionHelper.GetUserId(HttpContext.Session)!.Value;

        public async Task<IActionResult> Index()
        {
            if (Guard() is { } r) return r;

            var userId = CurrentUserId;
            ViewBag.UserName = SessionHelper.GetUserName(HttpContext.Session);
            ViewBag.UserRole = SessionHelper.GetUserRole(HttpContext.Session);

            var notifications = await _db.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            return View(notifications);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRead(int id)
        {
            if (Guard() is { } r) return r;

            var notification = await _db.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == CurrentUserId);

            if (notification != null)
            {
                notification.IsRead = true;
                await _db.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead()
        {
            if (Guard() is { } r) return r;

            var userId = CurrentUserId;
            var unread = await _db.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            foreach (var n in unread)
                n.IsRead = true;

            await _db.SaveChangesAsync();

            TempData["Success"] = "All notifications marked as read.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (Guard() is { } r) return r;

            var notification = await _db.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == CurrentUserId);

            if (notification != null)
            {
                _db.Notifications.Remove(notification);
                await _db.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }
    }
}
