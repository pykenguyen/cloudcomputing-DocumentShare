using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace DocumentSharingWebApp.ViewModels
{
    public class MemberUploadVM
    {
        [Required]
        [StringLength(255)]
        public string Title { get; set; } = null!;

        public string? Description { get; set; }

        [Required]
        public int CategoryId { get; set; }

        [Required]
        public IFormFile File { get; set; } = null!;
    }
}
