using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DocumentSharingWebApp.Models;

public partial class Report
{
    [Key]
    [Column("ReportID")]
    public int ReportId { get; set; }

    [Column("ReporterID")]
    public int? ReporterId { get; set; }

    [Column("DocumentID")]
    public int? DocumentId { get; set; }

    [StringLength(1000)]
    public string Reason { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime? ReportDate { get; set; }

    [StringLength(50)]
    public string Status { get; set; } = null!;

    [ForeignKey("DocumentId")]
    [InverseProperty("Reports")]
    public virtual Document? Document { get; set; }

    [ForeignKey("ReporterId")]
    [InverseProperty("Reports")]
    public virtual User? Reporter { get; set; }
}
