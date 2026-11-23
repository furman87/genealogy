using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Genealogy.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Genealogy.Pages.Account;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PasswordModel(ClubData data, ClubSignIn signIn) : PageModel
{
    [BindProperty]
    public PasswordInput Input { get; set; } = new();

    [TempData]
    public string? Status { get; set; }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var id = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var account = await data.FindAccountAsync(id);
        if (account is null)
            return Challenge();

        if (!signIn.Verify(account, Input.Current))
        {
            ModelState.AddModelError("Input.Current", "Enter your current password.");
            return Page();
        }

        var hash = signIn.Hash(Input.New);
        if (!await data.ChangePasswordAsync(account, hash))
            return Challenge();

        await ClubSignIn.IssueCookieAsync(HttpContext, account with { SessionVersion = account.SessionVersion + 1 });
        Status = "Password updated.";
        return RedirectToPage();
    }

    public sealed class PasswordInput
    {
        [Required, DataType(DataType.Password), StringLength(ClubSignIn.MaximumPasswordLength)]
        public string Current { get; set; } = "";

        [Required, DataType(DataType.Password)]
        [StringLength(ClubSignIn.MaximumPasswordLength, MinimumLength = ClubSignIn.MinimumPasswordLength)]
        public string New { get; set; } = "";

        [Required, DataType(DataType.Password), Compare(nameof(New), ErrorMessage = "The new passwords must match.")]
        public string Confirm { get; set; } = "";
    }
}
