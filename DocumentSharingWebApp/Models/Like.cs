using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DocumentSharingWebApp.Models;

[Index("UserId", "DocumentId", Name = "UQ_User_Like", IsUnique = true)]
public partial class Like
{
    [Key]
    [Column("LikeID")]
    public int LikeId { get; set; }

    [Column("UserID")]
    public int? UserId { get; set; }

    [Column("DocumentID")]
    public int? DocumentId { get; set; }

    [ForeignKey("DocumentId")]
    [InverseProperty("Likes")]
    public virtual Document? Document { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("Likes")]
    public virtual User? User { get; set; }
}
