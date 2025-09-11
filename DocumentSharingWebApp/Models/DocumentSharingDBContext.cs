using System;
using Microsoft.EntityFrameworkCore;

namespace DocumentSharingWebApp.Models
{
    public partial class DocumentSharingDBContext : DbContext
    {
        public DocumentSharingDBContext() { }
        public DocumentSharingDBContext(DbContextOptions<DocumentSharingDBContext> options) : base(options) { }

        public virtual DbSet<Category> Categories { get; set; } = null!;
        public virtual DbSet<Comment> Comments { get; set; } = null!;
        public virtual DbSet<Document> Documents { get; set; } = null!;
        public virtual DbSet<Like> Likes { get; set; } = null!;
        public virtual DbSet<News> News { get; set; } = null!;
        public virtual DbSet<Report> Reports { get; set; } = null!;
        public virtual DbSet<Transaction> Transactions { get; set; } = null!;
        public virtual DbSet<User> Users { get; set; } = null!;
        public virtual DbSet<DocumentPurchase> DocumentPurchases { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasKey(e => e.CategoryId).HasName("PK__Categori__19093A2B9D1680DE");
            });

            modelBuilder.Entity<Comment>(entity =>
            {
                entity.HasKey(e => e.CommentId).HasName("PK__Comments__C3B4DFAA101F4C8D");
                entity.Property(e => e.CommentDate).HasDefaultValueSql("(getdate())");
                entity.HasOne(d => d.Document).WithMany(p => p.Comments)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK__Comments__Docume__4AB81AF0");
                entity.HasOne(d => d.User).WithMany(p => p.Comments)
                    .HasConstraintName("FK__Comments__UserID__49C3F6B7");
            });

            modelBuilder.Entity<Document>(entity =>
            {
                entity.HasKey(e => e.DocumentId).HasName("PK__Document__1ABEEF6F1ABC1F8E");

                entity.Property(e => e.DownloadCost).HasDefaultValue(0.00m); // ⬅️ default 0
                entity.Property(e => e.DownloadCount).HasDefaultValue(0);
                entity.Property(e => e.LikeCount).HasDefaultValue(0);
                entity.Property(e => e.Status).HasDefaultValue("Pending");
                entity.Property(e => e.UploadDate).HasDefaultValueSql("(getdate())");

                entity.HasOne(d => d.Category).WithMany(p => p.Documents).HasConstraintName("FK__Documents__Categ__4222D4EF");
                entity.HasOne(d => d.Uploader).WithMany(p => p.Documents).HasConstraintName("FK__Documents__Uploa__412EB0B6");
            });

            modelBuilder.Entity<Like>(entity =>
            {
                entity.HasKey(e => e.LikeId).HasName("PK__Likes__A2922CF48840C1A0");
                entity.HasOne(d => d.Document).WithMany(p => p.Likes)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK__Likes__DocumentI__4F7CD00D");
                entity.HasOne(d => d.User).WithMany(p => p.Likes)
                    .HasConstraintName("FK__Likes__UserID__4E88ABD4");
            });

            modelBuilder.Entity<News>(entity =>
            {
                entity.HasKey(e => e.NewsId).HasName("PK__News__954EBDD3E7E007A4");
                entity.Property(e => e.PublicationDate).HasDefaultValueSql("(getdate())");
                entity.HasOne(d => d.Author).WithMany(p => p.News).HasConstraintName("FK__News__AuthorID__571DF1D5");
            });

            modelBuilder.Entity<Report>(entity =>
            {
                entity.HasKey(e => e.ReportId).HasName("PK__Reports__D5BD48E5");

                entity.Property(e => e.ReportDate).HasDefaultValueSql("(getdate())");
                entity.Property(e => e.Status).HasDefaultValue("New");

                entity.HasOne(d => d.Document)
                      .WithMany(p => p.Reports)
                      .OnDelete(DeleteBehavior.Cascade)   // ⬅️ BẬT CASCADE
                      .HasConstraintName("FK_Reports_Documents_DocumentID");

                entity.HasOne(d => d.Reporter)
                      .WithMany(p => p.Reports)
                      .HasConstraintName("FK_Reports_Users_ReporterID");
            });

            modelBuilder.Entity<Transaction>(entity =>
            {
                entity.HasKey(e => e.TransactionId).HasName("PK__Transact__55433A4B0E25ED04");
                entity.Property(e => e.TransactionDate).HasDefaultValueSql("(getdate())");
                entity.HasOne(d => d.User).WithMany(p => p.Transactions).HasConstraintName("FK__Transacti__UserI__52593CB8");
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.UserId).HasName("PK__Users__1788CCAC8AF3A057");
                entity.Property(e => e.RegistrationDate).HasDefaultValueSql("(getdate())");
                entity.Property(e => e.Role).HasDefaultValue("User");
                entity.Property(e => e.VirtualCurrency).HasDefaultValue(100.00m);
            });

            modelBuilder.Entity<DocumentPurchase>(entity =>
            {
                entity.ToTable("DocumentPurchases");
                entity.HasKey(e => e.PurchaseId);
                entity.HasIndex(e => new { e.UserId, e.DocumentId }).IsUnique();
                entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");
                entity.Property(e => e.PurchasedAt).HasDefaultValueSql("(getdate())");
                entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Document).WithMany().HasForeignKey(e => e.DocumentId).OnDelete(DeleteBehavior.Cascade);
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
