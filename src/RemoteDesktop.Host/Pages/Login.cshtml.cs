using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using RemoteDesktop.Host.Options;

namespace RemoteDesktop.Host.Pages;

[AllowAnonymous]
public sealed class LoginModel : PageModel
{
    private readonly ControlServerOptions _options;

    public LoginModel(IOptions<ControlServerOptions> options)
    {
        _options = options.Value;
    }

    [BindProperty]
    public LoginInputModel Input { get; set; } = new();

    public string? ErrorMessage { get; private set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Index");
        }

        Input.UserName = _options.AdminUserName;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (!IsAuthorized(Input.UserName, Input.Password))
        {
            ErrorMessage = "帳號或密碼不正確。";
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, _options.AdminUserName)
        };

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12),
                AllowRefresh = true
            });

        return RedirectToPage("/Index");
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Login");
    }

    private bool IsAuthorized(string? userName, string? password)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        return FixedTimeEquals(userName.Trim(), _options.AdminUserName)
            && FixedTimeEquals(password, _options.AdminPassword);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftHash = SHA256.HashData(Encoding.UTF8.GetBytes(left));
        var rightHash = SHA256.HashData(Encoding.UTF8.GetBytes(right));
        return CryptographicOperations.FixedTimeEquals(leftHash, rightHash);
    }

    public sealed class LoginInputModel
    {
        [Required(ErrorMessage = "請輸入帳號。")]
        [Display(Name = "帳號")]
        public string UserName { get; set; } = string.Empty;

        [Required(ErrorMessage = "請輸入密碼。")]
        [DataType(DataType.Password)]
        [Display(Name = "密碼")]
        public string Password { get; set; } = string.Empty;
    }
}
