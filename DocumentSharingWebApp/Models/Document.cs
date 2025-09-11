using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentSharingWebApp.Models
{
    public partial class Document
    {
        [Key]
        [Column("DocumentID")]
        public int DocumentId { get; set; }

        [StringLength(255)]
        public string Title { get; set; } = null!;

        public string? Description { get; set; }

        [StringLength(255)]
        public string FileName { get; set; } = null!;

        [StringLength(500)]
        public string FilePath { get; set; } = null!;

        [Column("FileSizeKB")]
        public int? FileSizeKb { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime? UploadDate { get; set; }

        [Column("UploaderID")]
        public int? UploaderId { get; set; }

        [Column("CategoryID")]
        public int? CategoryId { get; set; }

        [StringLength(50)]
        public string Status { get; set; } = null!;

        public int? DownloadCount { get; set; }
        public int? LikeCount { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? DownloadCost { get; set; }

        // ===== Chỉ giữ 1 lần ở đây =====
        public bool IsGuestUpload { get; set; }
        [StringLength(100)]
        public string? GuestName { get; set; }
        [StringLength(255)]
        public string? GuestEmail { get; set; }
        // ===== Hết =====

        [ForeignKey(nameof(CategoryId))]
        [InverseProperty(nameof(Category.Documents))]
        public virtual Category? Category { get; set; }

        [InverseProperty(nameof(Comment.Document))]
        public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();

        [InverseProperty(nameof(Like.Document))]
        public virtual ICollection<Like> Likes { get; set; } = new List<Like>();

        [InverseProperty(nameof(Report.Document))]
        public virtual ICollection<Report> Reports { get; set; } = new List<Report>();

        [ForeignKey(nameof(UploaderId))]
        [InverseProperty(nameof(User.Documents))]
        public virtual User? Uploader { get; set; }
    }
}
