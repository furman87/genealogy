using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using Genealogy.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Genealogy.Pages.Admin;

public sealed class HomepageModel(ClubData data) : PageModel
{
    [BindProperty]
    public HomepageInput Input { get; set; } = new();

    [TempData]
    public string? Status { get; set; }

    public async Task OnGetAsync()
    {
        var content = await data.GetHomepageAsync();
        Input.Headline = content.HeroHeadline;
        Input.Introduction = content.HeroIntroduction;
        Input.ClubEmail = content.ClubEmail;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var headline = Input.Headline.Trim().ReplaceLineEndings("\n");
        var parts = headline.Split('*');
        if (parts.Length % 2 == 0 ||
            parts.Where((_, index) => index % 2 == 1).Any(string.IsNullOrWhiteSpace))
        {
            ModelState.AddModelError("Input.Headline", "Put one asterisk on each side of the words you want to highlight.");
            return Page();
        }

        var introduction = Input.Introduction?.Trim().ReplaceLineEndings("\n") ?? "";
        var clubEmail = Input.ClubEmail?.Trim();
        if (clubEmail is not null &&
            (!MailAddress.TryCreate(clubEmail, out var address) || address.Address != clubEmail))
        {
            ModelState.AddModelError("Input.ClubEmail", "Enter a single email address.");
            return Page();
        }

        await data.SaveHomepageAsync(headline, introduction, clubEmail);
        Status = "Homepage updated.";
        return RedirectToPage();
    }

    public sealed class HomepageInput
    {
        public const int HeadlineMaxLength = 180;
        public const int IntroductionMaxLength = 600;
        public const int ClubEmailMaxLength = 254;

        [Required, StringLength(HeadlineMaxLength)]
        public string Headline { get; set; } = "";

        [StringLength(IntroductionMaxLength)]
        public string? Introduction { get; set; }

        [EmailAddress, StringLength(ClubEmailMaxLength)]
        public string? ClubEmail { get; set; }
    }
}
