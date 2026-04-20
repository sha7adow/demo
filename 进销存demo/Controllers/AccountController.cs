using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using 进销存demo.Models;
using 进销存demo.Models.Identity;

namespace 进销存demo.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signIn;
        private readonly UserManager<ApplicationUser> _users;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            SignInManager<ApplicationUser> signIn,
            UserManager<ApplicationUser> users,
            ILogger<AccountController> logger)
        {
            _signIn = signIn;
            _users = users;
            _logger = logger;
        }

        [AllowAnonymous, HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (_signIn.IsSignedIn(User))
                return LocalRedirect(IsLocal(returnUrl) ? returnUrl! : "/");
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _users.FindByNameAsync(model.UserName);
            if (user == null || !user.IsEnabled)
            {
                ModelState.AddModelError(string.Empty, "用户名或密码错误");
                return View(model);
            }

            var result = await _signIn.PasswordSignInAsync(
                user.UserName!, model.Password, model.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                _logger.LogInformation("用户 {User} 登录成功", user.UserName);
                if (IsLocal(model.ReturnUrl))
                    return LocalRedirect(model.ReturnUrl!);
                return RedirectToAction("Index", "Home");
            }

            if (result.IsLockedOut)
                ModelState.AddModelError(string.Empty, "账户已被锁定，请稍后再试");
            else
                ModelState.AddModelError(string.Empty, "用户名或密码错误");
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            _logger.LogInformation("用户 {User} 注销", User.Identity?.Name);
            await _signIn.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        [AllowAnonymous]
        public IActionResult AccessDenied() => View();

        [HttpGet]
        public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _users.GetUserAsync(User);
            if (user == null) return Challenge();

            var r = await _users.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (!r.Succeeded)
            {
                foreach (var err in r.Errors)
                    ModelState.AddModelError(string.Empty, TranslateIdentityError(err.Code, err.Description));
                return View(model);
            }

            await _signIn.RefreshSignInAsync(user);
            TempData["Msg"] = "密码修改成功";
            return RedirectToAction("Index", "Home");
        }

        private bool IsLocal(string? returnUrl)
            => !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl);

        private static string TranslateIdentityError(string code, string fallback) => code switch
        {
            "PasswordMismatch" => "当前密码不正确",
            "PasswordTooShort" => "密码长度过短",
            "PasswordRequiresDigit" => "密码需包含数字",
            "PasswordRequiresLower" => "密码需包含小写字母",
            "PasswordRequiresUpper" => "密码需包含大写字母",
            "PasswordRequiresNonAlphanumeric" => "密码需包含特殊字符",
            _ => fallback
        };
    }
}
