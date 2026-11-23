using Genealogy.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Genealogy.Pages;

public sealed class MeetingsModel(ClubData data, ClubClock clock) : PageModel
{
    public IReadOnlyList<Meeting> Meetings { get; private set; } = [];

    public async Task OnGetAsync() => Meetings = await data.ListMeetingsAsync(clock.Now);
}
