using System.ComponentModel.DataAnnotations;
using Genealogy.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Genealogy.Pages.Admin;

public sealed class MembersModel(ClubData data, ClubSignIn signIn) : PageModel
{
    [BindProperty]
    public MemberInput Input { get; set; } = new();

    [TempData]
    public string? Status { get; set; }

    public IReadOnlyList<ClubAccount> Members { get; private set; } = [];
    public ClubAccount? Member { get; private set; }

    public async Task<IActionResult> OnGetAsync(long? id)
    {
        if (id is null)
            return await ListAsync();

        Member = await FindMemberAsync(id.Value);
        return Member is null ? NotFound() : Page();
    }

    public async Task<IActionResult> OnPostAsync(long? id)
    {
        if (id is long memberId)
        {
            Member = await FindMemberAsync(memberId);
            if (Member is null)
                return NotFound();

            ModelState.Remove("Input.Email");
            if (!ModelState.IsValid)
                return Page();

            await data.SetMemberPasswordAsync(memberId, signIn.Hash(Input.Password));
            Status = "Password reset. The member can sign in with their new password.";
            return RedirectToPage(new { id = memberId });
        }

        if (!ModelState.IsValid)
            return await ListAsync();

        var email = ClubSignIn.NormalizeEmail(Input.Email);
        if (!await data.CreateMemberAsync(email, signIn.Hash(Input.Password)))
        {
            ModelState.AddModelError("Input.Email", "An account already uses this email address.");
            return await ListAsync();
        }

        Status = "Member account created.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSetStatusAsync(long id, bool active)
    {
        if (await FindMemberAsync(id) is null)
            return NotFound();

        await data.SetMemberStatusAsync(id, active);
        Status = active ? "Member access restored." : "Member access removed and active sessions ended.";
        return RedirectToPage(new { id });
    }

    private async Task<ClubAccount?> FindMemberAsync(long id)
    {
        var account = await data.FindAccountAsync(id);
        return account is { Role: AccountRoles.Member } ? account : null;
    }

    private async Task<PageResult> ListAsync()
    {
        Members = await data.ListMembersAsync();
        return Page();
    }

    public sealed class MemberInput
    {
        [Required, EmailAddress, StringLength(254)]
        public string Email { get; set; } = "";

        [Required, DataType(DataType.Password)]
        [StringLength(ClubSignIn.MaximumPasswordLength, MinimumLength = ClubSignIn.MinimumPasswordLength)]
        public string Password { get; set; } = "";
    }
}
