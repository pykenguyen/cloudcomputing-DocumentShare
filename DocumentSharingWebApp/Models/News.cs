using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DocumentSharingWebApp.Models;

public partial class News
{
    [Key]
    [Column("NewsID")]
    public int NewsId { get; set; }

    [StringLength(255)]
    public string Title { get; set; } = null!;

    public string Content { get; set; } = null!;

    [StringLength(500)]
    public string? Summary { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? PublicationDate { get; set; }

    [Column("AuthorID")]
    public int? AuthorId { get; set; }

    [ForeignKey("AuthorId")]
    [InverseProperty("News")]
    public virtual User? Author { get; set; }
}
