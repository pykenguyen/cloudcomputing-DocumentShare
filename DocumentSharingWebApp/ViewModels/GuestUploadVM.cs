using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace DocumentSharingWebApp.ViewModels
{
    public class GuestUploadVM
    {
        [Display(Name = "Họ tên (tuỳ chọn)")]
        public string? UploaderName { get; set; }

        [EmailAddress]
        [Display(Name = "Email (tuỳ chọn)")]
        public string? UploaderEmail { get; set; }

        [Required]
        public int CategoryId { get; set; }

        public string? Notes { get; set; }

        [Required]
        public IFormFile File { get; set; } = null!;
    }
}
