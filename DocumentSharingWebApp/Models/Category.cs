using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DocumentSharingWebApp.Models;

public partial class Category
{
    [Key]
    [Column("CategoryID")]
    public int CategoryId { get; set; }

    [StringLength(100)]
    public string CategoryName { get; set; } = null!;

    [StringLength(500)]
    public string? Description { get; set; }

    [InverseProperty("Category")]
    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
}
