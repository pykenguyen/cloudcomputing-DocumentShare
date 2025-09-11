using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DocumentSharingWebApp.Models;

public partial class Transaction
{
    [Key]
    [Column("TransactionID")]
    public int TransactionId { get; set; }

    [Column("UserID")]
    public int? UserId { get; set; }

    [Column("DocumentID")]
    public int? DocumentId { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal Amount { get; set; }

    [StringLength(50)]
    public string TransactionType { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime? TransactionDate { get; set; }

    [StringLength(255)]
    public string? Description { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("Transactions")]
    public virtual User? User { get; set; }
}
