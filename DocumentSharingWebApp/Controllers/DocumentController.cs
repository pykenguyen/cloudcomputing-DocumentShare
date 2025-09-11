using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DocumentSharingWebApp.Models;
using DocumentSharingWebApp.ViewModels;
using ImageMagick;

namespace DocumentSharingWebApp.Controllers
{
    public class DocumentController : Controller
    {
        private readonly DocumentSharingDBContext _context;
        private readonly IWebHostEnvironment _env;

        private static readonly string[] AllowedExt = new[] { ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".txt" };
        private const long MaxSizeBytes = 200 * 1024 * 1024;
        private static readonly Regex Invalid = new("[^a-zA-Z0-9-_\\.]", RegexOptions.Compiled);

        public DocumentController(DocumentSharingDBContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        private string WebRoot => _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        private string UploadsRoot => Path.Combine(WebRoot, "uploads");
        private string ThumbsRoot => Path.Combine(WebRoot, "thumbs");

        // ========= LIST + SEARCH =========
        public IActionResult Index(string? search, int? categoryId)
        {
            var docs = _context.Documents
                .Include(d => d.Uploader)
                .Include(d => d.Category)
                .Where(d => d.Status == "Approved")
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                docs = docs.Where(d =>
                    d.Title.Contains(search) ||
                    (d.Description != null && d.Description.Contains(search)));
                ViewBag.Search = search;
            }

            if (categoryId.HasValue)
            {
                docs = docs.Where(d => d.CategoryId == categoryId.Value);
                ViewBag.CategoryId = categoryId.Value;
            }

            ViewBag.Categories = _context.Categories.ToList();
            var list = docs.OrderByDescending(d => d.UploadDate).ToList();
            return View(list);
        }

        // ========= DETAILS =========
        public IActionResult Details(int id)
        {
            var doc = _context.Documents
                .Include(d => d.Uploader)
                .Include(d => d.Category)
                .Include(d => d.Comments).ThenInclude(c => c.User)
                .Include(d => d.Likes)
                .FirstOrDefault(d => d.DocumentId == id && d.Status == "Approved");

            if (doc == null) return NotFound();

            ViewBag.LikeCount = doc.LikeCount ?? doc.Likes?.Count ?? 0;
            ViewBag.UserLiked = false;

            var price = doc.DownloadCost ?? 0m;
            var isAdminDoc = doc.Uploader != null && (doc.Uploader.Role ?? "").Equals("Admin", StringComparison.OrdinalIgnoreCase);
            var isPaid = isAdminDoc && price > 0m;

            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId.HasValue)
            {
                ViewBag.UserLiked = _context.Likes.Any(l => l.DocumentId == id && l.UserId == userId.Value);

                ViewBag.Balance = _context.Users
                    .Where(u => u.UserId == userId.Value)
                    .Select(u => (decimal?)(u.VirtualCurrency ?? 0m))
                    .FirstOrDefault() ?? 0m;

                ViewBag.HasPurchased = isPaid
                    ? _context.DocumentPurchases.Any(p => p.DocumentId == id && p.UserId == userId.Value)
                    : true; // miễn phí coi như có quyền tải
            }
            else
            {
                ViewBag.Balance = null;
                ViewBag.HasPurchased = !isPaid; // guest: chỉ tải nếu miễn phí
            }

            ViewBag.Price = price;
            ViewBag.IsPaid = isPaid;
            ViewBag.IsFree = !isPaid;

            return View(doc);
        }

        // ========= DOWNLOAD =========
        public IActionResult Download(int id)
        {
            var doc = _context.Documents
                .Include(d => d.Uploader)
                .FirstOrDefault(d => d.DocumentId == id);
            if (doc == null) return NotFound();

            var price = doc.DownloadCost ?? 0m;
            var isAdminDoc = doc.Uploader != null && (doc.Uploader.Role ?? "").Equals("Admin", StringComparison.OrdinalIgnoreCase);
            var isPaid = isAdminDoc && price > 0m;

            var userId = HttpContext.Session.GetInt32("UserId");

            if (!userId.HasValue && isPaid)
            {
                TempData["DownloadError"] = "Bạn cần đăng nhập để tải tài liệu tính phí.";
                return RedirectToAction("Details", new { id });
            }

            if (isPaid && userId.HasValue)
            {
                var already = _context.DocumentPurchases.Any(p => p.UserId == userId.Value && p.DocumentId == id);
                if (!already)
                {
                    var user = _context.Users.Find(userId.Value);
                    var balance = user?.VirtualCurrency ?? 0m;
                    if (balance < price)
                    {
                        TempData["DownloadError"] = $"Bạn cần {price:0} xu, số dư hiện tại {balance:0} xu.";
                        return RedirectToAction("Details", new { id });
                    }

                    user!.VirtualCurrency = balance - price;

                    _context.DocumentPurchases.Add(new DocumentPurchase
                    {
                        UserId = userId.Value,
                        DocumentId = id,
                        Price = price,
                        PurchasedAt = DateTime.Now
                    });

                    _context.Transactions.Add(new Transaction
                    {
                        UserId = userId.Value,
                        DocumentId = id,
                        Amount = -price,
                        TransactionType = "Purchase",
                        TransactionDate = DateTime.Now,
                        Description = $"Mua tài liệu #{id}"
                    });

                    _context.SaveChanges();
                }
            }

            var sessionKey = $"dl_last_{id}";
            var now = DateTime.UtcNow.Ticks;
            var lastTicksStr = HttpContext.Session.GetString(sessionKey);
            var shouldCount = true;

            if (long.TryParse(lastTicksStr, out var lastTicks))
            {
                var delta = new TimeSpan(now - lastTicks).TotalSeconds;
                if (delta < 2) shouldCount = false; // 2 giây: chặn request lặp/Range thứ 2
            }

            if (shouldCount)
            {
                doc.DownloadCount = (doc.DownloadCount ?? 0) + 1;
                _context.SaveChanges();
                HttpContext.Session.SetString(sessionKey, now.ToString());
            }
            // ===================================

            // Trả file
            var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            var filePath = Path.Combine(uploadsRoot, doc.FilePath);
            if (!System.IO.File.Exists(filePath)) return NotFound();

            // Bật range để trình duyệt tải ổn định nhưng không đếm lặp nhờ session guard
            return PhysicalFile(filePath, "application/octet-stream", fileDownloadName: doc.FileName, enableRangeProcessing: true);
        }

        // ========= LIKE / UNLIKE =========
        [HttpPost]
        public IActionResult ToggleLike(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue) return RedirectToAction("Login", "Account");

            var like = _context.Likes.FirstOrDefault(l => l.DocumentId == id && l.UserId == userId.Value);
            if (like == null)
            {
                _context.Likes.Add(new Like { DocumentId = id, UserId = userId.Value });
                var doc = _context.Documents.Find(id);
                if (doc != null) doc.LikeCount = (doc.LikeCount ?? 0) + 1;
            }
            else
            {
                _context.Likes.Remove(like);
                var doc = _context.Documents.Find(id);
                if (doc != null) doc.LikeCount = Math.Max(0, (doc.LikeCount ?? 1) - 1);
            }

            _context.SaveChanges();
            return RedirectToAction("Details", new { id });
        }

        // ========= COMMENT =========
        [HttpPost]
        public IActionResult PostComment(int documentId, string content)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue) return RedirectToAction("Login", "Account");
            if (string.IsNullOrWhiteSpace(content))
            {
                TempData["CommentError"] = "Nội dung bình luận không được để trống.";
                return RedirectToAction("Details", new { id = documentId });
            }

            _context.Comments.Add(new Comment
            {
                Content = content,
                CommentDate = DateTime.Now,
                UserId = userId.Value,
                DocumentId = documentId
            });
            _context.SaveChanges();
            return RedirectToAction("Details", new { id = documentId });
        }

        // ========= REPORT =========
        [HttpPost]
        public IActionResult Report(int documentId, string reason)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue) return RedirectToAction("Login", "Account");
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["ReportError"] = "Vui lòng nhập lý do báo lỗi.";
                return RedirectToAction("Details", new { id = documentId });
            }

            _context.Reports.Add(new Report
            {
                DocumentId = documentId,
                ReporterId = userId.Value,
                Reason = reason,
                ReportDate = DateTime.Now,
                Status = "Pending"
            });
            _context.SaveChanges();

            TempData["ReportSuccess"] = "Báo lỗi đã được gửi đến quản trị.";
            return RedirectToAction("Details", new { id = documentId });
        }

        // ========= MY DOCS =========
        public IActionResult MyDocuments()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue) return RedirectToAction("Login", "Account");

            var docs = _context.Documents
                .Where(d => d.UploaderId == userId.Value)
                .OrderByDescending(d => d.UploadDate)
                .ToList();

            return View(docs);
        }

        // ========= UPLOAD (GET) =========
        [HttpGet, AllowAnonymous]
        public IActionResult Upload()
        {
            ViewBag.Categories = _context.Categories.OrderBy(c => c.CategoryName).ToList();
            ViewBag.IsLoggedIn = HttpContext.Session.GetInt32("UserId").HasValue;
            return View();
        }

        // ========= NEW: Convert helper (LibreOffice) =========
        private async Task<string?> TryConvertToPdf(string absSourcePath, CancellationToken ct = default)
        {
            var ext = Path.GetExtension(absSourcePath).ToLowerInvariant();
            if (ext == ".pdf") return absSourcePath;

            var soffice = "soffice"; // đã trong PATH
            var outDir = Path.GetDirectoryName(absSourcePath)!;

            var psi = new ProcessStartInfo
            {
                FileName = soffice,
                Arguments = $"--headless --norestore --convert-to pdf --outdir \"{outDir}\" \"{absSourcePath}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return null;

            var exited = await Task.Run(() => proc.WaitForExit(90_000), ct);
            if (!exited)
            {
                try { proc.Kill(true); } catch { /* ignore */ }
                return null;
            }

            var pdf = Path.Combine(outDir, Path.GetFileNameWithoutExtension(absSourcePath) + ".pdf");
            return System.IO.File.Exists(pdf) ? pdf : null;
        }

        private async Task<(string RelPath, long SizeKb, string DisplayName)> SaveFileAsPdfIfPossible(
            IFormFile file, string subFolder, CancellationToken ct = default)
        {
            var targetDir = Path.Combine(UploadsRoot, subFolder);
            Directory.CreateDirectory(targetDir);

            var safeName = Invalid.Replace(Path.GetFileName(file.FileName), "_");
            var uniqueName = $"{Path.GetFileNameWithoutExtension(safeName)}_{Guid.NewGuid():N}{Path.GetExtension(safeName)}";
            var absPath = Path.Combine(targetDir, uniqueName);

            await using (var fs = new FileStream(absPath, FileMode.CreateNew))
                await file.CopyToAsync(fs, ct);

            var pdfAbs = await TryConvertToPdf(absPath, ct);
            string finalAbs = absPath;
            if (pdfAbs != null)
            {
                finalAbs = pdfAbs;
                if (!finalAbs.Equals(absPath, StringComparison.OrdinalIgnoreCase))
                {
                    try { System.IO.File.Delete(absPath); } catch { /* ignore */ }
                }
            }

            var rel = Path.GetRelativePath(UploadsRoot, finalAbs).Replace('\\', '/');
            var sizeKb = new FileInfo(finalAbs).Length / 1024;
            var display = Path.GetFileName(finalAbs);
            return (rel, sizeKb, display);
        }

        // ========= UPLOAD: GUEST =========
        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxSizeBytes)]
        public async Task<IActionResult> UploadGuest(GuestUploadVM vm)
        {
            void FillBags()
            {
                ViewBag.Categories = _context.Categories.OrderBy(c => c.CategoryName).ToList();
                ViewBag.IsLoggedIn = HttpContext.Session.GetInt32("UserId").HasValue;
            }

            if (!ModelState.IsValid)
            {
                FillBags();
                TempData["GuestErr"] = "Vui lòng kiểm tra lại thông tin.";
                return View("Upload");
            }
            if (!_context.Categories.Any(c => c.CategoryId == vm.CategoryId))
            {
                FillBags();
                TempData["GuestErr"] = "Danh mục không hợp lệ.";
                return View("Upload");
            }
            if (!ValidateFile(vm.File, out var err))
            {
                FillBags();
                TempData["GuestErr"] = err;
                return View("Upload");
            }

            // ⬇⬇ LƯU + CONVERT SANG PDF (nếu có thể)
            var (relPath, sizeKb, displayName) = await SaveFileAsPdfIfPossible(vm.File, "pending");

            var doc = new Document
            {
                Title = Path.GetFileNameWithoutExtension(displayName),
                Description = vm.Notes,
                CategoryId = vm.CategoryId,
                FileName = displayName,
                FilePath = relPath,
                FileSizeKb = (int)sizeKb,
                UploadDate = DateTime.Now,
                UploaderId = null,
                IsGuestUpload = true,
                GuestName = vm.UploaderName,
                GuestEmail = vm.UploaderEmail,
                Status = "Pending",
                DownloadCost = 0m
            };

            _context.Documents.Add(doc);
            await _context.SaveChangesAsync();

            TempData["GuestOk"] = "Đã nhận tài liệu. Quản trị sẽ duyệt sớm!";
            return RedirectToAction(nameof(Upload));
        }

        // ========= UPLOAD: MEMBER =========
        [HttpPost, ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxSizeBytes)]
        public async Task<IActionResult> UploadMember(MemberUploadVM vm)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue) return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid)
            {
                ViewBag.Categories = _context.Categories.OrderBy(c => c.CategoryName).ToList();
                ViewBag.IsLoggedIn = true;
                TempData["MemberErr"] = "Vui lòng kiểm tra lại thông tin.";
                return View("Upload");
            }
            if (!_context.Categories.Any(c => c.CategoryId == vm.CategoryId))
            {
                ViewBag.Categories = _context.Categories.OrderBy(c => c.CategoryName).ToList();
                ViewBag.IsLoggedIn = true;
                TempData["MemberErr"] = "Danh mục không hợp lệ.";
                return View("Upload");
            }
            if (!ValidateFile(vm.File, out var err))
            {
                ViewBag.Categories = _context.Categories.OrderBy(c => c.CategoryName).ToList();
                ViewBag.IsLoggedIn = true;
                TempData["MemberErr"] = err;
                return View("Upload");
            }

            var sub = Path.Combine("users", userId.Value.ToString());

            // ⬇⬇ LƯU + CONVERT SANG PDF (nếu có thể)
            var (relPath, sizeKb, displayName) = await SaveFileAsPdfIfPossible(vm.File, sub);

            var doc = new Document
            {
                Title = vm.Title.Trim(),
                Description = vm.Description,
                CategoryId = vm.CategoryId,
                FileName = displayName,
                FilePath = relPath,
                FileSizeKb = (int)sizeKb,
                UploadDate = DateTime.Now,
                UploaderId = userId.Value,
                IsGuestUpload = false,
                Status = "Pending",
                DownloadCost = 0m
            };

            _context.Documents.Add(doc);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(MyDocuments));
        }

        // ========= Validate file (giữ) =========
        private static bool ValidateFile(IFormFile f, out string? error)
        {
            error = null;
            if (f == null || f.Length == 0) { error = "Chưa chọn file."; return false; }
            if (f.Length > MaxSizeBytes) { error = "File quá lớn (tối đa 200MB)."; return false; }
            var ext = Path.GetExtension(f.FileName).ToLowerInvariant();
            if (!AllowedExt.Contains(ext)) { error = "Định dạng không được phép."; return false; }
            return true;
        }

        // ========= THUMB (PDF trang đầu) =========
        [HttpGet, AllowAnonymous]
        public IActionResult Thumb(int id, int w = 360)
        {
            var doc = _context.Documents.Find(id);
            if (doc == null) return NotFound();

            Directory.CreateDirectory(ThumbsRoot);
            var thumbPath = Path.Combine(ThumbsRoot, $"{id}.jpg");

            if (!System.IO.File.Exists(thumbPath))
            {
                var src = Path.Combine(UploadsRoot, doc.FilePath ?? "");
                if (System.IO.File.Exists(src))
                {
                    var ext = Path.GetExtension(src).ToLowerInvariant();
                    if (ext == ".pdf")
                    {
                        try
                        {
                            var settings = new MagickReadSettings { Density = new Density(144) };
                            using var images = new MagickImageCollection();
                            images.Read(src, settings);
                            using var page0 = images[0];

                            page0.BackgroundColor = MagickColors.White;
                            page0.Alpha(AlphaOption.Remove);
                            page0.Resize(new MagickGeometry((uint)w, 0)); // width=w, height auto
                            page0.Format = MagickFormat.Jpeg;
                            page0.Quality = 82;
                            page0.Write(thumbPath);
                        }
                        catch
                        {
                            // ignore -> fallback placeholder
                        }
                    }
                }
            }

            var placeholder = Path.Combine(WebRoot, "images", "doc-placeholder.jpg");
            var path = System.IO.File.Exists(thumbPath) ? thumbPath : placeholder;
            if (!System.IO.File.Exists(path)) return NotFound();

            return PhysicalFile(path, "image/jpeg");
        }

        // ========= WALLET (giữ nguyên) =========
        [Authorize]
        [HttpGet]
        public IActionResult Wallet()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue) return RedirectToAction("Login", "Account");

            var user = _context.Users.Find(userId.Value);
            if (user == null) return RedirectToAction("Login", "Account");

            var txs = _context.Transactions
                .Where(t => t.UserId == userId.Value)
                .OrderByDescending(t => t.TransactionDate)
                .Take(100)
                .ToList();

            ViewBag.Balance = user.VirtualCurrency ?? 0m;
            return View(txs);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Recharge(decimal amount, string? note)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue) return RedirectToAction("Login", "Account");

            if (amount <= 0)
            {
                TempData["WalletError"] = "Số xu nạp phải lớn hơn 0.";
                return RedirectToAction(nameof(Wallet));
            }

            var user = _context.Users.Find(userId.Value);
            if (user == null)
            {
                TempData["WalletError"] = "Không tìm thấy tài khoản.";
                return RedirectToAction(nameof(Wallet));
            }

            user.VirtualCurrency = (user.VirtualCurrency ?? 0m) + amount;

            _context.Transactions.Add(new Transaction
            {
                UserId = user.UserId,
                Amount = amount,
                TransactionType = "Recharge",
                Description = string.IsNullOrWhiteSpace(note) ? "Nạp xu" : note,
                TransactionDate = DateTime.Now
            });

            _context.SaveChanges();

            TempData["WalletOk"] = $"Đã nạp {amount:0} xu.";
            return RedirectToAction(nameof(Wallet));
        }
    }
}
