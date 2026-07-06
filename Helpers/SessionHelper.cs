using SPMS.Models;

namespace SPMS.Helpers
{
    public static class SessionHelper
    {
        public static void SetUser(ISession session, User user)
        {
            session.SetInt32("UserId", user.Id);
            session.SetString("UserName", user.FullName);
            session.SetString("UserEmail", user.Email);
            session.SetString("UserRole", user.Role.ToString());
        }

        public static int? GetUserId(ISession session) => session.GetInt32("UserId");
        public static string? GetUserName(ISession session) => session.GetString("UserName");
        public static string? GetUserEmail(ISession session) => session.GetString("UserEmail");
        public static string? GetUserRole(ISession session) => session.GetString("UserRole");

        public static bool IsLoggedIn(ISession session) => session.GetInt32("UserId").HasValue;

        public static bool IsAdmin(ISession session) => session.GetString("UserRole") == "Admin";
        public static bool IsStudent(ISession session) => session.GetString("UserRole") == "Student";
        public static bool IsTutor(ISession session) => session.GetString("UserRole") == "Tutor";
        public static bool IsCompany(ISession session) => session.GetString("UserRole") == "Company";

        public static void Clear(ISession session) => session.Clear();
    }
}
