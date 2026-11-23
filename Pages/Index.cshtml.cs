using Genealogy.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Genealogy.Pages;

public sealed class IndexModel(ClubData data, ClubClock clock) : PageModel
{
    public string Mission { get; private set; } = "";
    public IReadOnlyList<Meeting> Meetings { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Mission = await data.GetMissionAsync();
        Meetings = await data.ListMeetingsAsync(clock.Now, limit: 3);
    }
}
