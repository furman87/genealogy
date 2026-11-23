using System.ComponentModel.DataAnnotations;
using Genealogy.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Genealogy.Pages.Admin;

public sealed class MaterialModel(ClubData data, ResourceFiles files) : PageModel
{
    [BindProperty]
    public ResourceInput Input { get; set; } = new();

    [TempData]
    public string? Status { get; set; }

    public long? ResourceId { get; private set; }
    public string? CurrentFile { get; private set; }
    public IReadOnlyList<Resource> Resources { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(long? id)
    {
        ResourceId = id;
        if (id is null)
            return await ShowAsync();

        var resource = await data.FindResourceAsync(id.Value, includePrivate: true);
        if (resource is null)
            return NotFound();

        CurrentFile = resource.OriginalName;
        Input = new ResourceInput
        {
            Title = resource.Title,
            Kind = resource.Kind,
            Description = resource.Description,
            MembersOnly = resource.MembersOnly,
            PublicLink = resource.ExternalUrl,
            Revision = resource.Revision
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(long? id)
    {
        ResourceId = id;
        var current = id is long existingId ? await data.FindResourceAsync(existingId, includePrivate: true) : null;
        if (id is not null && current is null)
            return NotFound();

        CurrentFile = current?.OriginalName;
        string? link = null;
        string? originalName = null;
        string? contentType = null;

        if (Input.Kind is not ("Slides" or "Video"))
            ModelState.AddModelError("Input.Kind", "Choose slides or video.");

        if (!string.IsNullOrWhiteSpace(Input.PublicLink))
        {
            if (!Uri.TryCreate(Input.PublicLink.Trim(), UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps || string.IsNullOrEmpty(uri.Host) || uri.UserInfo.Length != 0)
            {
                ModelState.AddModelError("Input.PublicLink", "Enter a valid HTTPS link.");
            }
            else
            {
                link = uri.AbsoluteUri;
            }

            if (Input.MembersOnly)
                ModelState.AddModelError("Input.PublicLink", "Upload a file to keep it members-only.");

            if (Input.Upload is not null)
                ModelState.AddModelError("Input.Upload", "Choose either an upload or a public link.");
        }
        else
        {
            originalName = Input.Upload is null ? CurrentFile : SafeFileName(Input.Upload.FileName);
            contentType = originalName is null ? null : ResourceFiles.ContentType(Input.Kind, originalName);
            if (contentType is null || Input.Upload is { Length: 0 })
                ModelState.AddModelError("Input.Upload", "Choose a PDF or PPTX for slides, or an MP4 or WebM for video.");

            if (Input.Upload is { Length: > ClubStorage.MaxUploadBytes })
                ModelState.AddModelError("Input.Upload", "Choose a file no larger than 100 MB.");
        }

        if (!ModelState.IsValid)
            return await ShowAsync();

        var storageKey = link is null ? current?.StorageKey : null;
        string? newKey = null;
        var saved = false;
        try
        {
            if (Input.Upload is not null)
                storageKey = newKey = await files.StoreAsync(Input.Upload, HttpContext.RequestAborted);

            var resource = new Resource(id ?? 0, Input.Title.Trim(), Input.Kind, Input.Description?.Trim(),
                Input.MembersOnly, link, storageKey, originalName, contentType, Input.Revision);
            saved = await data.SaveResourceAsync(resource);
        }
        finally
        {
            if (!saved)
                files.Remove(newKey);
        }

        if (!saved)
        {
            ModelState.AddModelError(string.Empty, "This resource changed while you were editing. Reload the page before saving again.");
            return await ShowAsync();
        }

        if (current?.StorageKey != storageKey)
            files.Remove(current?.StorageKey);

        Status = id is null ? "Resource posted." : "Resource updated.";
        return RedirectToPage(new { id = (long?)null });
    }

    public async Task<IActionResult> OnPostDeleteAsync(long id)
    {
        files.Remove(await data.DeleteResourceAsync(id));
        Status = "Resource removed.";
        return RedirectToPage(new { id = (long?)null });
    }

    private async Task<PageResult> ShowAsync()
    {
        if (ResourceId is null)
            Resources = await data.ListResourcesAsync(includePrivate: true);

        return Page();
    }

    private static string SafeFileName(string fileName)
    {
        var name = fileName.Replace((char)92, '/').Split('/')[^1];
        name = string.Concat(name.Where(character => !char.IsControl(character)));
        return name.Length <= 180 ? name : name[..160] + Path.GetExtension(name);
    }

    public sealed class ResourceInput
    {
        [Required, StringLength(160)]
        public string Title { get; set; } = "";

        [Required]
        public string Kind { get; set; } = "Slides";

        [StringLength(2500)]
        public string? Description { get; set; }

        public bool MembersOnly { get; set; } = true;

        [StringLength(1500)]
        public string? PublicLink { get; set; }

        public IFormFile? Upload { get; set; }
        public long Revision { get; set; }
    }
}
