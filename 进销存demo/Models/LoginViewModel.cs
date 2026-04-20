using System.ComponentModel.DataAnnotations;

namespace 进销存demo.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "请输入用户名")]
        [Display(Name = "用户名")]
        public string UserName { get; set; } = string.Empty;

        [Required(ErrorMessage = "请输入密码")]
        [DataType(DataType.Password)]
        [Display(Name = "密码")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "记住我")]
        public bool RememberMe { get; set; }

        public string? ReturnUrl { get; set; }
    }

    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "请输入当前密码"), DataType(DataType.Password)]
        [Display(Name = "当前密码")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "请输入新密码"), DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "密码长度至少 6 位")]
        [Display(Name = "新密码")]
        public string NewPassword { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "两次密码不一致")]
        [Display(Name = "确认新密码")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
