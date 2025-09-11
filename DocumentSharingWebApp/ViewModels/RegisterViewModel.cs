using System.ComponentModel.DataAnnotations;

namespace DocumentSharingWebApp.ViewModels
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập.")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Tên đăng nhập từ 3–50 ký tự.")]
        [Display(Name = "Tên đăng nhập")]
        public string Username { get; set; } = "";

        [Required(ErrorMessage = "Vui lòng nhập email.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [StringLength(100)]
        [Display(Name = "Email")]
        public string Email { get; set; } = "";

        [StringLength(100)]
        [Display(Name = "Họ tên")]
        public string? FullName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
        [MinLength(6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự.")]
        [Display(Name = "Mật khẩu")]
        public string Password { get; set; } = "";

        [Compare(nameof(Password), ErrorMessage = "Xác nhận mật khẩu không khớp.")]
        [Display(Name = "Xác nhận mật khẩu")]
        public string ConfirmPassword { get; set; } = "";
    }
}
