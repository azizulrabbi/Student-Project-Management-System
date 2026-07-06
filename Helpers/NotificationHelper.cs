using SPMS.Data;
using SPMS.Models;

namespace SPMS.Helpers
{
    public static class NotificationHelper
    {
        public static void Send(AppDbContext db, int userId, string title, string body, string? link = null)
        {
            db.Notifications.Add(new Notification
            {
                UserId = userId,
                Title = title,
                Body = body,
                Link = link
            });
            db.SaveChanges();
        }
    }
}
