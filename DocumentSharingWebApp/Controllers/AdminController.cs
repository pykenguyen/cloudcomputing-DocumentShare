using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using DocumentSharingWebApp.Models;
using DocumentSharingWebApp.ViewModels;

namespace DocumentSharingWebApp.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly DocumentSharingDBContext _context;
        private readonly IWebHostEnvironment _env;

        private static readonly string[] AllowedExt = { ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".txt" };
        private const long MaxSizeBytes = 200L * 1024 * 1024; // 200MB
        private static readonly Regex Invalid = new("[^a-zA-Z0-9-_\\.]", RegexOptions.Compiled);

        public AdminController(DocumentSharingDBContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        private int? CurrentUserId()
        {
            var sid = HttpContext.Session.GetInt32("UserId");
            if (sid.HasValue) return sid.Value;
            var claim = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : (int?)null;
        }

        private string WebRoot => _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        private string UploadsRoot => Path.Combine(WebRoot, "uploads");
        private string ThumbsRoot => Path.Combine(WebRoot, "thumbs");

        private static bool ValidateFile(IFormFile? f, out string? error)
        {
            error = null;
            if (f == null || f.Length == 0) { error = "Vui lòng chọn một tệp."; return false; }
            if (f.Length > MaxSizeBytes) { error = "File quá lớn (tối đa 200MB)."; return false; }
            var ext = Path.GetExtension(f.FileName).ToLowerInvariant();
            if (!AllowedExt.Contains(ext)) { error = "Định dạng không được phép."; return false; }
            return true;
        }

        private void DeleteThumbFile(int docId)
        {
            try
            {
                Directory.CreateDirectory(ThumbsRoot);
                var thumbPath = Path.Combine(ThumbsRoot, $"{docId}.jpg");
                if (System.IO.File.Exists(thumbPath)) System.IO.File.Delete(thumbPath);
            }
            catch { /* ignore */ }
        }

        // ===== Dashboard =====
        public IActionResult Dashboard()
        {
            var today = DateTime.Today;
            var now = DateTime.Now;

            // KPIs
            var totalUsers = _context.Users.Count();
            var newUsersToday = _context.Users.Count(u => u.RegistrationDate != null &&
                                                          u.RegistrationDate.Value.Date == today);
            var totalDocs = _context.Documents.Count();
            var pendingDocs = _context.Documents.Count(d => d.Status == "Pending");
            var openReports = _context.Reports.Count(r => r.Status != "Resolved");
            var totalDownloads = _context.Documents.Sum(d => d.DownloadCount ?? 0);

            // Xu đã chi trong tháng (Transactions.Amount âm khi mua)
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var purchasesThisMonth = _context.DocumentPurchases
        .Count(p => p.PurchasedAt >= monthStart);
            var coinsSpent = _context.Transactions
                .Where(t => t.TransactionType == "Purchase" && t.TransactionDate >= monthStart)
                .Select(t => (decimal?)(-t.Amount)) // Amount âm -> lấy trị dương
                .Sum() ?? 0m;

            // Tài liệu chờ duyệt gần đây
            var pendingLatest = _context.Documents
                .Include(d => d.Uploader)
                .Include(d => d.Category)
                .Where(d => d.Status == "Pending")
                .OrderByDescending(d => d.UploadDate)
                .Take(8)
                .Select(d => new RecentDoc
                {
                    Id = d.DocumentId,
                    Title = d.Title,
                    Uploader = d.IsGuestUpload ? (d.GuestName ?? "Khách")
                             : (d.Uploader != null ? d.Uploader.Username : "—"),
                    Category = d.Category != null ? d.Category.CategoryName : null,
                    UploadedAt = d.UploadDate
                })
                .ToList();

            // Báo lỗi chưa resolve mới nhất
            var reportsLatest = _context.Reports
                .Include(r => r.Document)
                .Include(r => r.Reporter)
                .Where(r => r.Status != "Resolved")
                .OrderByDescending(r => r.ReportDate)
                .Take(8)
                .Select(r => new RecentReport
                {
                    Id = r.ReportId,
                    DocTitle = r.Document != null ? r.Document.Title : "(đã xoá?)",
                    Reporter = r.Reporter != null ? r.Reporter.Username : "—",
                    Reason = r.Reason ?? "",
                    At = r.ReportDate,
                    Status = r.Status ?? ""
                })
                .ToList();

            // Top danh mục (Docs Approved)
            var topCats = _context.Documents
                .Include(d => d.Category)
                .Where(d => d.Status == "Approved" && d.CategoryId != null)
                .AsEnumerable() // tránh group by object trên SQL
                .GroupBy(d => d.Category?.CategoryName ?? "Khác")
                .Select(g => new CategoryStat { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(6)
                .ToList();

            // Upload 7 ngày gần đây
            var labels = new List<string>();
            var values = new List<int>();
            for (int i = 6; i >= 0; i--)
            {
                var day = today.AddDays(-i);
                labels.Add(day.ToString("dd/MM"));
                var cnt = _context.Documents.Count(d => d.UploadDate != null &&
                                                        d.UploadDate.Value.Date == day);
                values.Add(cnt);
            }

            var vm = new AdminDashboardVM
            {
                TotalUsers = totalUsers,
                NewUsersToday = newUsersToday,
                TotalDocuments = totalDocs,
                PendingDocuments = pendingDocs,
                OpenReports = openReports,
                TotalDownloads = totalDownloads,
                PurchasesThisMonth = purchasesThisMonth,
                CoinsSpentThisMonth = coinsSpent,
                PendingLatest = pendingLatest,
                ReportsLatest = reportsLatest,
                TopCategories = topCats,
                Last7DaysLabels = labels,
                Last7DaysUploads = values
            };

            return View(vm);
        }

        // ===== Users =====
        public IActionResult ManageUsers()
        {
            var users = _context.Users.OrderBy(u => u.UserId).ToList();
            return View(users);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteUser(int id)
        {
            var me = CurrentUserId();
            if (me == id)
            {
                TempData["UserErr"] = "Bạn không thể xoá chính tài khoản đang đăng nhập.";
                return RedirectToAction(nameof(ManageUsers));
            }

            var user = _context.Users.FirstOrDefault(u => u.UserId == id);
            if (user == null)
            {
                TempData["UserErr"] = "Không tìm thấy người dùng.";
                return RedirectToAction(nameof(ManageUsers));
            }

            using var tx = _context.Database.BeginTransaction();
            try
            {
                var likes = _context.Likes.Where(x => x.UserId == id);
                var comments = _context.Comments.Where(x => x.UserId == id);
                var reports = _context.Reports.Where(x => x.ReporterId == id);
                var purchases = _context.DocumentPurchases.Where(x => x.UserId == id);
                var transactions = _context.Transactions.Where(x => x.UserId == id);

                _context.Likes.RemoveRange(likes);
                _context.Comments.RemoveRange(comments);
                _context.Reports.RemoveRange(reports);
                _context.DocumentPurchases.RemoveRange(purchases);
                _context.Transactions.RemoveRange(transactions);
                _context.SaveChanges();

                var docs = _context.Documents.Where(d => d.UploaderId == id).ToList();
                foreach (var d in docs)
                {
                    d.UploaderId = null;
                    d.IsGuestUpload = true;
                    if (string.IsNullOrWhiteSpace(d.GuestName))
                        d.GuestName = $"{user.Username} (đã xoá)";
                }
                _context.SaveChanges();

                _context.Users.Remove(user);
                _context.SaveChanges();

                tx.Commit();
                TempData["UserOk"] = "Đã xoá người dùng và dọn dữ liệu liên quan.";
            }
            catch
            {
                tx.Rollback();
                TempData["UserErr"] = "Có lỗi khi xoá người dùng.";
            }

            return RedirectToAction(nameof(ManageUsers));
        }

        // ===== Documents =====
        public IActionResult ManageDocuments(string? search, int? categoryId)
        {
            ViewBag.Categories = _context.Categories.ToList();
            ViewBag.CategoryId = categoryId;
            ViewBag.Search = search;

            var q = _context.Documents
                .Include(d => d.Uploader)
                .Include(d => d.Category)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                q = q.Where(d =>
                    d.Title.Contains(search) ||
                    (d.Description != null && d.Description.Contains(search)) ||
                    (d.Uploader != null && d.Uploader.Username.Contains(search)));
            }

            if (categoryId.HasValue)
                q = q.Where(d => d.CategoryId == categoryId.Value);

            return View(q.OrderByDescending(d => d.UploadDate).ToList());
        }

        public IActionResult ViewDocument(int id)
        {
            var doc = _context.Documents
                .Include(d => d.Uploader)
                .Include(d => d.Category)
                .FirstOrDefault(d => d.DocumentId == id);

            if (doc == null) return NotFound();
            return View(doc);
        }

        public IActionResult ApproveDocument(int id)
        {
            var doc = _context.Documents.Find(id);
            if (doc == null) return NotFound();
            doc.Status = "Approved";
            _context.SaveChanges();
            return RedirectToAction(nameof(ManageDocuments));
        }

        public IActionResult RejectDocument(int id)
        {
            var doc = _context.Documents.Find(id);
            if (doc == null) return NotFound();
            doc.Status = "Rejected";
            _context.SaveChanges();
            return RedirectToAction(nameof(ManageDocuments));
        }

        public IActionResult DeleteDocument(int id)
        {
            var doc = _context.Documents
                .Include(d => d.Comments)
                .Include(d => d.Likes)
                .Include(d => d.Reports)
                .FirstOrDefault(d => d.DocumentId == id);

            if (doc == null) return NotFound();

            try
            {
                var full = Path.Combine(UploadsRoot, doc.FilePath ?? "");
                if (System.IO.File.Exists(full)) System.IO.File.Delete(full);
            }
            catch { }

            DeleteThumbFile(id);

            var purchases = _context.DocumentPurchases.Where(p => p.DocumentId == id).ToList();
            _context.DocumentPurchases.RemoveRange(purchases);
            _context.Comments.RemoveRange(doc.Comments);
            _context.Likes.RemoveRange(doc.Likes);
            _context.Reports.RemoveRange(doc.Reports);

            _context.Documents.Remove(doc);
            _context.SaveChanges();

            return RedirectToAction(nameof(ManageDocuments));
        }

        // ===== NEW: Edit file + sửa tiêu đề, mô tả =====

        [HttpGet]
        public IActionResult EditFile(int id)
        {
            var doc = _context.Documents.FirstOrDefault(d => d.DocumentId == id);
            if (doc == null) return NotFound();
            return View(doc); // Views/Admin/EditFile.cshtml (model = Document)
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxSizeBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxSizeBytes)]
        public async Task<IActionResult> EditFile(int id, string? title, string? description, IFormFile? newFile)
        {
            var doc = _context.Documents.FirstOrDefault(d => d.DocumentId == id);
            if (doc == null) return NotFound();

            // Cập nhật tiêu đề/mô tả trước (dù có thay file hay không)
            if (!string.IsNullOrWhiteSpace(title))
                doc.Title = title.Trim();
            else if (string.IsNullOrWhiteSpace(doc.Title))
                doc.Title = "(Không tiêu đề)";

            doc.Description = description; // cho phép null/empty

            // Nếu có chọn file mới => validate và thay thế
            if (newFile != null && newFile.Length > 0)
            {
                if (!ValidateFile(newFile, out var err))
                {
                    ModelState.AddModelError("newFile", err ?? "File không hợp lệ.");
                    return View(doc);
                }

                // Lưu đè: cùng thư mục với file cũ nếu có, hoặc uploads/admin/{adminId}
                string targetDir;
                if (!string.IsNullOrEmpty(doc.FilePath))
                {
                    var oldAbs = Path.Combine(UploadsRoot, doc.FilePath.Replace('/', Path.DirectorySeparatorChar));
                    var oldDir = Path.GetDirectoryName(oldAbs);
                    targetDir = string.IsNullOrEmpty(oldDir)
                        ? Path.Combine(UploadsRoot, "admin", (CurrentUserId() ?? 0).ToString())
                        : oldDir!;
                }
                else
                {
                    targetDir = Path.Combine(UploadsRoot, "admin", (CurrentUserId() ?? 0).ToString());
                }
                Directory.CreateDirectory(targetDir);

                var safeName = Invalid.Replace(Path.GetFileName(newFile.FileName), "_");
                var uniqueName = $"{Path.GetFileNameWithoutExtension(safeName)}_{Guid.NewGuid():N}{Path.GetExtension(safeName)}";
                var newAbs = Path.Combine(targetDir, uniqueName);
                await using (var fs = new FileStream(newAbs, FileMode.CreateNew))
                    await newFile.CopyToAsync(fs);

                // Xoá file cũ
                try
                {
                    if (!string.IsNullOrEmpty(doc.FilePath))
                    {
                        var oldAbs = Path.Combine(UploadsRoot, doc.FilePath.Replace('/', Path.DirectorySeparatorChar));
                        if (System.IO.File.Exists(oldAbs)) System.IO.File.Delete(oldAbs);
                    }
                }
                catch { /* ignore */ }

                var rel = Path.GetRelativePath(UploadsRoot, newAbs).Replace('\\', '/');
                var sizeKb = new FileInfo(newAbs).Length / 1024;

                doc.FileName = safeName;
                doc.FilePath = rel;
                doc.FileSizeKb = (int)sizeKb;

                // đổi tiêu đề rỗng theo tên file mới nếu user không nhập
                if (string.IsNullOrWhiteSpace(title))
                    doc.Title = Path.GetFileNameWithoutExtension(safeName);

                // invalidate thumb
                DeleteThumbFile(doc.DocumentId);
            }

            // cập nhật mốc thời gian sửa
            doc.UploadDate = DateTime.Now;
            _context.SaveChanges();

            TempData["Ok"] = "Đã cập nhật thông tin tài liệu.";
            return RedirectToAction(nameof(ViewDocument), new { id = doc.DocumentId });
        }

        // ===== Add (admin upload) =====
        [HttpGet]
        public IActionResult AddDocument()
        {
            ViewBag.Categories = _context.Categories.ToList();
            return View(new AdminAddDocumentVM { DownloadCost = 0m });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxSizeBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxSizeBytes)]
        public async Task<IActionResult> AddDocument(AdminAddDocumentVM vm)
        {
            ViewBag.Categories = _context.Categories.ToList();

            var adminId = CurrentUserId();
            if (!adminId.HasValue) return RedirectToAction("Login", "Account");

            if (vm.CategoryId == null || !_context.Categories.Any(c => c.CategoryId == vm.CategoryId))
                ModelState.AddModelError(nameof(vm.CategoryId), "Vui lòng chọn danh mục.");

            if (!ValidateFile(vm.UploadFile, out var fileErr))
                ModelState.AddModelError(nameof(vm.UploadFile), fileErr ?? "File không hợp lệ.");

            if (!ModelState.IsValid) return View(vm);

            var targetDir = Path.Combine(UploadsRoot, "admin", adminId.Value.ToString());
            Directory.CreateDirectory(targetDir);
            var safe = Invalid.Replace(Path.GetFileName(vm.UploadFile!.FileName), "_");
            var unique = $"{Path.GetFileNameWithoutExtension(safe)}_{Guid.NewGuid():N}{Path.GetExtension(safe)}";
            var abs = Path.Combine(targetDir, unique);
            await using (var fs = new FileStream(abs, FileMode.CreateNew))
                await vm.UploadFile.CopyToAsync(fs);

            var rel = Path.GetRelativePath(UploadsRoot, abs).Replace('\\', '/');
            var sizeKb = new FileInfo(abs).Length / 1024;
            var price = vm.DownloadCost.HasValue ? Math.Max(0m, vm.DownloadCost.Value) : 0m;

            var doc = new Document
            {
                Title = string.IsNullOrWhiteSpace(vm.Title) ? Path.GetFileNameWithoutExtension(safe) : vm.Title!.Trim(),
                Description = vm.Description,
                CategoryId = vm.CategoryId,
                FileName = safe,
                FilePath = rel,
                FileSizeKb = (int)sizeKb,
                UploadDate = DateTime.Now,
                UploaderId = adminId.Value,
                IsGuestUpload = false,
                Status = "Approved",
                DownloadCost = price
            };

            _context.Documents.Add(doc);
            await _context.SaveChangesAsync();

            DeleteThumbFile(doc.DocumentId);
            TempData["AddSuccess"] = "Đã thêm tài liệu mới.";
            return RedirectToAction(nameof(ManageDocuments));
        }

        // ===== Categories =====
        public IActionResult ManageCategories()
        {
            var cats = _context.Categories.OrderBy(c => c.CategoryName).ToList();
            return View(cats);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateCategory(string categoryName)
        {
            var name = (categoryName ?? "").Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["CatErr"] = "Tên danh mục không được để trống.";
                return RedirectToAction(nameof(ManageCategories));
            }

            var exists = _context.Categories.Any(c => c.CategoryName.ToLower() == name.ToLower());
            if (exists)
            {
                TempData["CatErr"] = "Danh mục đã tồn tại.";
                return RedirectToAction(nameof(ManageCategories));
            }

            _context.Categories.Add(new Category { CategoryName = name });
            _context.SaveChanges();

            TempData["CatOk"] = $"Đã tạo danh mục: {name}.";
            return RedirectToAction(nameof(ManageCategories));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteCategory(int id)
        {
            var cat = _context.Categories.Find(id);
            if (cat == null) return NotFound();

            var inUse = _context.Documents.Any(d => d.CategoryId == id);
            if (inUse)
            {
                TempData["CatErr"] = "Danh mục đang được sử dụng, không thể xoá.";
                return RedirectToAction(nameof(ManageCategories));
            }

            _context.Categories.Remove(cat);
            _context.SaveChanges();

            TempData["CatOk"] = $"Đã xoá danh mục: {cat.CategoryName}.";
            return RedirectToAction(nameof(ManageCategories));
        }

        // ===== Comments =====
        public IActionResult ManageComments(string? search)
        {
            var q = _context.Comments
                .Include(c => c.User)
                .Include(c => c.Document)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                q = q.Where(c =>
                    (c.Content != null && c.Content.Contains(search)) ||
                    (c.User != null && c.User.Username.Contains(search)) ||
                    (c.Document != null && c.Document.Title.Contains(search)));
            }

            return View(q.OrderByDescending(c => c.CommentDate).ToList());
        }

        public IActionResult DeleteComment(int id)
        {
            var c = _context.Comments.Find(id);
            if (c == null) return NotFound();
            _context.Comments.Remove(c);
            _context.SaveChanges();
            return RedirectToAction(nameof(ManageComments));
        }

        // ===== Reports =====
        public IActionResult ManageReports(string? search)
        {
            var q = _context.Reports
                .Include(r => r.Reporter)
                .Include(r => r.Document)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                q = q.Where(r =>
                    (r.Reason != null && r.Reason.Contains(search)) ||
                    (r.Reporter != null && r.Reporter.Username.Contains(search)) ||
                    (r.Document != null && r.Document.Title.Contains(search)));
            }

            return View(q.OrderByDescending(r => r.ReportDate).ToList());
        }

        public IActionResult ResolveReport(int id)
        {
            var r = _context.Reports.Find(id);
            if (r == null) return NotFound();
            r.Status = "Resolved";
            _context.SaveChanges();
            return RedirectToAction(nameof(ManageReports));
        }

        public IActionResult DeleteReport(int id)
        {
            var r = _context.Reports.Find(id);
            if (r == null) return NotFound();
            _context.Reports.Remove(r);
            _context.SaveChanges();
            return RedirectToAction(nameof(ManageReports));
        }

        [HttpPost]
        public IActionResult ClearThumb(int id)
        {
            DeleteThumbFile(id);
            TempData["Ok"] = "Đã xoá thumbnail cache. Ảnh sẽ tự render lại ở lần xem kế tiếp.";
            return RedirectToAction(nameof(ManageDocuments));
        }
    }
}
