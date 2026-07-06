using Microsoft.AspNetCore.Mvc;
using SPMS.Helpers;

namespace SPMS.Controllers
{
    public class ContactController : Controller
    {
        public IActionResult Index()
        {
            if (!SessionHelper.IsLoggedIn(HttpContext.Session))
                return RedirectToAction("Login", "Account");

            ViewBag.UserName = SessionHelper.GetUserName(HttpContext.Session);
            ViewBag.UserRole = SessionHelper.GetUserRole(HttpContext.Session);
            return View();
        }
    }
}
