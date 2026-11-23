using Genealogy.Data;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Genealogy.Pages;

public sealed class IndexModel(ClubData data, ClubClock clock) : PageModel
{
    public string Mission { get; private set; } = "";
    public IHtmlContent HeroHeadline { get; private set; } = HtmlString.Empty;
    public string HeroIntroduction { get; private set; } = "";
    public IReadOnlyList<Meeting> Meetings { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var content = await data.GetHomepageAsync();
        Mission = content.Mission;
        HeroHeadline = FormatHeadline(content.HeroHeadline);
        HeroIntroduction = content.HeroIntroduction;
        Meetings = await data.ListMeetingsAsync(clock.Now, limit: 3);
    }

    private static IHtmlContent FormatHeadline(string headline)
    {
        var content = new HtmlContentBuilder();
        var parts = headline.Split('*');
        for (var index = 0; index < parts.Length; index++)
        {
            if (index % 2 == 0)
            {
                content.Append(parts[index]);
            }
            else
            {
                var highlight = new TagBuilder("em");
                highlight.InnerHtml.Append(parts[index]);
                content.AppendHtml(highlight);
            }
        }

        return content;
    }
}
