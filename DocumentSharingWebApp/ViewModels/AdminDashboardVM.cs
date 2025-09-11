using System;
using System.Collections.Generic;

namespace DocumentSharingWebApp.ViewModels
{
    public class AdminDashboardVM
    {
        public int TotalUsers { get; set; }
        public int NewUsersToday { get; set; }
        public int TotalDocuments { get; set; }
        public int PendingDocuments { get; set; }
        public int OpenReports { get; set; }
        public int TotalDownloads { get; set; }
        public int PurchasesThisMonth { get; set; }
        public decimal CoinsSpentThisMonth { get; set; }
        public List<RecentDoc> PendingLatest { get; set; } = new();
        public List<RecentReport> ReportsLatest { get; set; } = new();
        public List<CategoryStat> TopCategories { get; set; } = new();
        public List<string> Last7DaysLabels { get; set; } = new();
        public List<int> Last7DaysUploads { get; set; } = new();
    }

    public class RecentDoc
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Uploader { get; set; }
        public string? Category { get; set; }
        public DateTime? UploadedAt { get; set; }
    }

    public class RecentReport
    {
        public int Id { get; set; }
        public string? DocTitle { get; set; }
        public string? Reporter { get; set; }
        public string? Reason { get; set; }
        public DateTime? At { get; set; }
        public string? Status { get; set; }
    }

    public class CategoryStat
    {
        public string Name { get; set; } = "Khác";
        public int Count { get; set; }
    }
}
