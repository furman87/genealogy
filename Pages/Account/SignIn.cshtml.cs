using System.ComponentModel.DataAnnotations;
using Genealogy.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Genealogy.Pages.Account;

[EnableRateLimiting("sign-in")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SignInModel(ClubData data, ClubSignIn signIn) : PageModel
{
    [BindProperty]
    public Credentials Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public IActionResult OnGet() => User.Identity?.IsAuthenticated == true ? Destination() : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var account = await data.FindAccountByEmailAsync(ClubSignIn.NormalizeEmail(Input.Email));
        if (!signIn.Verify(account, Input.Password) || account is null)
        {
            ModelState.AddModelError(string.Empty, "The email or password is incorrect.");
            return Page();
        }

        await ClubSignIn.IssueCookieAsync(HttpContext, account);
        return Destination(account.Role == AccountRoles.Admin);
    }

    public async Task<IActionResult> OnPostSignOutAsync()
    {
        await HttpContext.SignOutAsync();
        return RedirectToPage("/Index");
    }

    private IActionResult Destination(bool? admin = null) => Url.IsLocalUrl(ReturnUrl)
        ? LocalRedirect(ReturnUrl!)
        : RedirectToPage((admin ?? User.IsInRole(AccountRoles.Admin)) ? "/Admin/Index" : "/Library");

    public sealed class Credentials
    {
        [Required, EmailAddress, StringLength(254)]
        public string Email { get; set; } = "";

        [Required, StringLength(ClubSignIn.MaximumPasswordLength), DataType(DataType.Password)]
        public string Password { get; set; } = "";
    }
}
