using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DocumentSharingWebApp.Models
{
    [Index(nameof(UserId), nameof(DocumentId), IsUnique = true)]
    public class DocumentPurchase
    {
        [Key]
        public int PurchaseId { get; set; }

        [Required, Column("UserID")]
        public int UserId { get; set; }

        [Required, Column("DocumentID")]
        public int DocumentId { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Price { get; set; }

        public DateTime PurchasedAt { get; set; } = DateTime.Now;

        [ForeignKey(nameof(UserId))] public virtual User User { get; set; } = null!;
        [ForeignKey(nameof(DocumentId))] public virtual Document Document { get; set; } = null!;
    }
}
