using System.ComponentModel.DataAnnotations;
using Genealogy.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Genealogy.Pages.Admin;

public sealed class MissionModel(ClubData data) : PageModel
{
    [BindProperty, Required, StringLength(2000)]
    public string Mission { get; set; } = "";

    [TempData]
    public string? Status { get; set; }

    public async Task OnGetAsync() => Mission = (await data.GetHomepageAsync()).Mission;

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        await data.SaveMissionAsync(Mission.Trim());
        Status = "Mission statement updated.";
        return RedirectToPage();
    }
}
