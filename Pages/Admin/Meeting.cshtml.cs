using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Genealogy.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Genealogy.Pages.Admin;

public sealed class MeetingModel(ClubData data, ClubClock clock) : PageModel
{
    [BindProperty]
    public MeetingInput Input { get; set; } = new();

    [TempData]
    public string? Status { get; set; }

    public long? MeetingId { get; private set; }
    public string TimeZoneName => clock.Zone.DisplayName;
    public IReadOnlyList<Meeting> Meetings { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(long? id)
    {
        MeetingId = id;
        if (id is null)
            return await ListAsync();

        var meeting = await data.FindMeetingAsync(id.Value);
        if (meeting is null)
            return NotFound();

        Input = new MeetingInput
        {
            Title = meeting.Title,
            StartsAt = meeting.StartsAt,
            Location = meeting.Location,
            Description = meeting.Description
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(long? id)
    {
        MeetingId = id;
        if (!DateTime.TryParseExact(Input.StartsAt, ClubClock.LocalFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var startsAt) ||
            clock.Zone.IsInvalidTime(startsAt) || clock.Zone.IsAmbiguousTime(startsAt))
        {
            ModelState.AddModelError("Input.StartsAt", "Enter a valid local date and time outside the daylight saving transition hour.");
        }

        if (!ModelState.IsValid)
            return id is null ? await ListAsync() : Page();

        var meeting = new Meeting(id ?? 0, Input.Title.Trim(),
            startsAt.ToString(ClubClock.LocalFormat, CultureInfo.InvariantCulture),
            Input.Location?.Trim(), Input.Description?.Trim());

        if (!await data.SaveMeetingAsync(meeting))
            return NotFound();

        Status = id is null ? "Meeting posted." : "Meeting updated.";
        return RedirectToPage(new { id = (long?)null });
    }

    public async Task<IActionResult> OnPostDeleteAsync(long id)
    {
        await data.DeleteMeetingAsync(id);
        Status = "Meeting removed.";
        return RedirectToPage(new { id = (long?)null });
    }

    private async Task<PageResult> ListAsync()
    {
        Meetings = await data.ListMeetingsAsync();
        return Page();
    }

    public sealed class MeetingInput
    {
        [Required, StringLength(160)]
        public string Title { get; set; } = "";

        [Required]
        public string StartsAt { get; set; } = "";

        [StringLength(160)]
        public string? Location { get; set; }

        [StringLength(2500)]
        public string? Description { get; set; }
    }
}
