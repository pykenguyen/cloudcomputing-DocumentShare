using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DocumentSharingWebApp.Models;

public partial class Comment
{
    [Key]
    [Column("CommentID")]
    public int CommentId { get; set; }

    [StringLength(1000)]
    public string Content { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime? CommentDate { get; set; }

    [Column("UserID")]
    public int? UserId { get; set; }

    [Column("DocumentID")]
    public int? DocumentId { get; set; }

    [ForeignKey("DocumentId")]
    [InverseProperty("Comments")]
    public virtual Document? Document { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("Comments")]
    public virtual User? User { get; set; }
}
