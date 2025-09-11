using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DocumentSharingWebApp.Models;

namespace DocumentSharingWebApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly DocumentSharingDBContext _context;

        public HomeController(ILogger<HomeController> logger, DocumentSharingDBContext context)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult Index()
        {
            // L?y danh m?c
            var categories = _context.Categories.ToList();
            ViewBag.Categories = categories;

            // L?y 8 tài li?u m?i nh?t ?ã ???c duy?t
            var latestDocs = _context.Documents
                .Include(d => d.Uploader)
                .Include(d => d.Category)
                .Where(d => d.Status == "Approved")
                .OrderByDescending(d => d.UploadDate)
                .Take(8)
                .ToList();

            return View(latestDocs);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
