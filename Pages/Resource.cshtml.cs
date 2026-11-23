using Genealogy.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Genealogy.Pages;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ResourceModel(ClubData data, ResourceFiles files) : PageModel
{
    public Resource Item { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(long id)
    {
        var resource = await data.FindResourceAsync(id, User.Identity?.IsAuthenticated == true);
        if (resource is null)
            return NotFound();

        Item = resource;
        return Page();
    }

    public async Task<IActionResult> OnGetFileAsync(long id, bool download = false)
    {
        var resource = await data.FindResourceAsync(id, User.Identity?.IsAuthenticated == true);
        if (resource is not { StorageKey: not null, ContentType: not null } || files.Open(resource.StorageKey) is not { } stream)
            return NotFound();

        return new FileStreamResult(stream, resource.ContentType)
        {
            FileDownloadName = download || resource.Kind == "Slides" ? resource.OriginalName : null,
            EnableRangeProcessing = true
        };
    }
}
