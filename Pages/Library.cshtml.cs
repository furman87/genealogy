using Genealogy.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Genealogy.Pages;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class LibraryModel(ClubData data) : PageModel
{
    public string? Kind { get; private set; }
    public IReadOnlyList<Resource> Resources { get; private set; } = [];

    public async Task OnGetAsync(string? kind)
    {
        Kind = kind is "Slides" or "Video" ? kind : null;
        Resources = await data.ListResourcesAsync(User.Identity?.IsAuthenticated == true, Kind);
    }
}
