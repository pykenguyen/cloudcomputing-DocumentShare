using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DocumentSharingWebApp.Models;
using DocumentSharingWebApp.ViewModels;
using System.Security.Cryptography;
using System.Text;

namespace DocumentSharingWebApp.Controllers
{
    [Authorize]
    public class UserController : Controller
    {
        private readonly DocumentSharingDBContext _db;
        public UserController(DocumentSharingDBContext db) => _db = db;

        [HttpGet]
        public IActionResult Profile()
        {
            var uid = HttpContext.Session.GetInt32("UserId");
            if (!uid.HasValue) return RedirectToAction("Login", "Account");

            var user = _db.Users.Find(uid.Value);
            if (user == null) return RedirectToAction("Login", "Account");

            var vm = new ProfileVM
            {
                UserId = user.UserId,
                Username = user.Username,
                Email = user.Email,
                FullName = user.FullName,
                Role = user.Role,
                RegistrationDate = user.RegistrationDate,
                Coins = user.VirtualCurrency ?? 0m,
                Purchases = _db.DocumentPurchases
                                .Where(p => p.UserId == uid.Value)
                                .OrderByDescending(p => p.PurchasedAt)
                                .Include(p => p.Document)
                                .ToList(),
                MyDocuments = _db.Documents
                                .Where(d => d.UploaderId == uid.Value)
                                .OrderByDescending(d => d.UploadDate)
                                .ToList()
            };
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult UpdateProfile(UpdateProfileVM vm)
        {
            var uid = HttpContext.Session.GetInt32("UserId");
            if (!uid.HasValue) return RedirectToAction("Login", "Account");

            var user = _db.Users.Find(uid.Value);
            if (user == null) return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid)
            {
                TempData["ProfileMsg"] = "Vui lòng kiểm tra lại thông tin.";
                return RedirectToAction(nameof(Profile));
            }

            if (!string.Equals(user.Email, vm.Email, StringComparison.OrdinalIgnoreCase))
            {
                var exists = _db.Users.Any(u => u.Email == vm.Email && u.UserId != user.UserId);
                if (exists) { TempData["ProfileMsg"] = "Email đã được sử dụng."; return RedirectToAction(nameof(Profile)); }
                user.Email = vm.Email;
            }

            user.FullName = vm.FullName;
            _db.SaveChanges();

            TempData["ProfileMsg"] = "Đã lưu thay đổi hồ sơ.";
            return RedirectToAction(nameof(Profile));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult ChangePassword(ChangePasswordVM vm)
        {
            var uid = HttpContext.Session.GetInt32("UserId");
            if (!uid.HasValue) return RedirectToAction("Login", "Account");
            if (!ModelState.IsValid) { TempData["PwdMsg"] = "Dữ liệu không hợp lệ."; return RedirectToAction(nameof(Profile)); }

            var user = _db.Users.Find(uid.Value);
            if (user == null) return RedirectToAction("Login", "Account");

            // TODO: thay bằng thuật toán hash bạn đang dùng
            if (!VerifySha256(vm.CurrentPassword, user.PasswordHash))
            {
                TempData["PwdMsg"] = "Mật khẩu hiện tại không đúng.";
                return RedirectToAction(nameof(Profile));
            }

            user.PasswordHash = HashSha256(vm.NewPassword);
            _db.SaveChanges();

            TempData["PwdMsg"] = "Đã đổi mật khẩu.";
            return RedirectToAction(nameof(Profile));
        }

        // Helpers hash (đổi theo thuật toán của dự án nếu khác)
        private static string HashSha256(string input)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }
        private static bool VerifySha256(string input, string storedHash) => HashSha256(input) == storedHash;
    }
}
