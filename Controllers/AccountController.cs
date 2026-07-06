using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPMS.Data;
using SPMS.Helpers;
using SPMS.Models;

namespace SPMS.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _db;

        public AccountController(AppDbContext db) => _db = db;

        public IActionResult Login()
        {
            if (SessionHelper.IsLoggedIn(HttpContext.Session))
                return RedirectToDashboard();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginVM model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
            {
                ModelState.AddModelError("", "Invalid email or password.");
                return View(model);
            }

            if (!user.IsActive)
            {
                ModelState.AddModelError("", "Your account has been deactivated. Contact admin.");
                return View(model);
            }

            SessionHelper.SetUser(HttpContext.Session, user);
            return RedirectToDashboard();
        }

        public IActionResult Logout()
        {
            SessionHelper.Clear(HttpContext.Session);
            return RedirectToAction("Login");
        }

        private IActionResult RedirectToDashboard()
        {
            var role = SessionHelper.GetUserRole(HttpContext.Session);
            return role switch
            {
                "Admin" => RedirectToAction("Index", "Admin"),
                "Student" => RedirectToAction("Dashboard", "Student"),
                "Tutor" => RedirectToAction("Dashboard", "Tutor"),
                "Company" => RedirectToAction("Dashboard", "Company"),
                _ => RedirectToAction("Login")
            };
        }

        public IActionResult Error() => View();
    }
}
