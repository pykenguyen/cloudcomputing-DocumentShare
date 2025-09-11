using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DocumentSharingWebApp.Models;
using DocumentSharingWebApp.ViewModels; // <-- dùng ViewModels tách riêng

namespace DocumentSharingWebApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly DocumentSharingDBContext _db;
        public AccountController(DocumentSharingDBContext db) => _db = db;

        // ============== LOGIN ==============
        [HttpGet, AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
            => View(new LoginViewModel { ReturnUrl = returnUrl });

        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var identifier = vm.Identifier?.Trim() ?? "";
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Username == identifier || u.Email == identifier);

            if (user == null || !VerifySha256(vm.Password, user.PasswordHash))
            {
                ModelState.AddModelError(string.Empty, "Tài khoản hoặc mật khẩu không đúng.");
                return View(vm);
            }

            // Session (nếu bạn còn dùng ở nơi khác)
            HttpContext.Session.SetInt32("UserId", user.UserId);
            HttpContext.Session.SetString("Username", user.Username);
            HttpContext.Session.SetString("Role", user.Role ?? "User");

            // Cookie auth
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role ?? "User")
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = vm.RememberMe,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(vm.RememberMe ? 14 : 1)
                });

            if (!string.IsNullOrWhiteSpace(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
                return Redirect(vm.ReturnUrl);

            return RedirectToAction("Index", "Home");
        }

        // ============== REGISTER ==============
        [HttpGet, AllowAnonymous]
        public IActionResult Register() => View(new RegisterViewModel());

        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            vm.Username = vm.Username.Trim();
            vm.Email = vm.Email.Trim();

            if (await _db.Users.AnyAsync(u => u.Username == vm.Username))
            {
                ModelState.AddModelError(nameof(RegisterViewModel.Username), "Tên đăng nhập đã tồn tại.");
                return View(vm);
            }
            if (await _db.Users.AnyAsync(u => u.Email == vm.Email))
            {
                ModelState.AddModelError(nameof(RegisterViewModel.Email), "Email đã được sử dụng.");
                return View(vm);
            }

            var user = new User
            {
                Username = vm.Username,
                Email = vm.Email,
                FullName = vm.FullName,
                PasswordHash = HashSha256(vm.Password), // đổi nếu dự án dùng hash khác
                RegistrationDate = DateTime.Now,
                Role = "User"
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            // Auto login
            HttpContext.Session.SetInt32("UserId", user.UserId);
            HttpContext.Session.SetString("Username", user.Username);
            HttpContext.Session.SetString("Role", user.Role ?? "User");

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role ?? "User")
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
                });

            return RedirectToAction("Index", "Home");
        }

        // ============== LOGOUT ==============
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Clear();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        // ============== DENIED ==============
        [HttpGet, AllowAnonymous]
        public IActionResult Denied() => View();

        // ============== Hash helpers ==============
        private static string HashSha256(string input)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }
        private static bool VerifySha256(string input, string storedHash)
            => HashSha256(input) == (storedHash ?? string.Empty);
    }
}
