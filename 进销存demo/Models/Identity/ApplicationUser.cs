using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace 进销存demo.Models.Identity
{
    public class ApplicationUser : IdentityUser
    {
        [StringLength(32), Display(Name = "显示名")]
        public string? DisplayName { get; set; }

        [Display(Name = "创建时间")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "是否启用")]
        public bool IsEnabled { get; set; } = true;
    }
}
