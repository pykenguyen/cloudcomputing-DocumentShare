using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DocumentSharingWebApp.Models;

[Index("Username", Name = "UQ__Users__536C85E4C4C9442B", IsUnique = true)]
[Index("Email", Name = "UQ__Users__A9D10534E3EFCD21", IsUnique = true)]
public partial class User
{
    [Key]
    [Column("UserID")]
    public int UserId { get; set; }

    [StringLength(50)]
    public string Username { get; set; } = null!;

    [StringLength(255)]
    public string PasswordHash { get; set; } = null!;

    [StringLength(100)]
    public string Email { get; set; } = null!;

    [StringLength(100)]
    public string? FullName { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? RegistrationDate { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? VirtualCurrency { get; set; }

    [StringLength(20)]
    public string Role { get; set; } = null!;

    [InverseProperty("User")]
    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();

    [InverseProperty("Uploader")]
    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();

    [InverseProperty("User")]
    public virtual ICollection<Like> Likes { get; set; } = new List<Like>();

    [InverseProperty("Author")]
    public virtual ICollection<News> News { get; set; } = new List<News>();

    [InverseProperty("Reporter")]
    public virtual ICollection<Report> Reports { get; set; } = new List<Report>();

    [InverseProperty("User")]
    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
