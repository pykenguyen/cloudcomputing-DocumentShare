using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace DocumentSharingWebApp.ViewModels
{
    public class AdminAddDocumentVM
    {
        [StringLength(255)]
        public string? Title { get; set; }

        public string? Description { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn danh mục.")]
        public int? CategoryId { get; set; }

        [Range(0, 1000000, ErrorMessage = "Giá phải >= 0.")]
        public decimal? DownloadCost { get; set; } = 0m;

        // File upload
        [Required(ErrorMessage = "Vui lòng chọn tệp.")]
        public IFormFile? UploadFile { get; set; }
    }
}
