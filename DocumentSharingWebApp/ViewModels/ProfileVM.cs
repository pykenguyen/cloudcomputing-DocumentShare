using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DocumentSharingWebApp.Models;

namespace DocumentSharingWebApp.ViewModels
{
    public class ProfileVM
    {
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public string Email { get; set; } = "";
        public string? FullName { get; set; }
        public string Role { get; set; } = "User";
        public DateTime? RegistrationDate { get; set; }
        public decimal Coins { get; set; }

        public List<DocumentPurchase> Purchases { get; set; } = new();
        public List<Document> MyDocuments { get; set; } = new();
    }

    public class UpdateProfileVM
    {
        [Required, EmailAddress] public string Email { get; set; } = "";
        [StringLength(100)] public string? FullName { get; set; }
    }

    public class ChangePasswordVM
    {
        [Required] public string CurrentPassword { get; set; } = "";
        [Required, MinLength(6)] public string NewPassword { get; set; } = "";
        [Compare(nameof(NewPassword), ErrorMessage = "Xác nhận mật khẩu không khớp.")]
        public string ConfirmNewPassword { get; set; } = "";
    }
}
