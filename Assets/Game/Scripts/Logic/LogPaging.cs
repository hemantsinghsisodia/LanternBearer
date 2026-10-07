using System.Collections.Generic;
using System.Text;

namespace LanternKeeper
{
// The text of one open-book spread.
public struct LogSpread
{
    public string left;
    public string right;
}

// Pure paging for the Keeper's Log. Found entries then torn-page lines are packed onto pages of at most
// charsPerPage characters (a long entry is split at word boundaries and flows to the next page), and pages are
// paired into spreads. An island with no entries still gets one (empty) spread.
public static class LogPaging
{
    public const string TornPage = "— a torn page, not yet found —";
    const string Gap = "\n\n";

    public static List<LogSpread> Paginate(IList<string> entries, int foundCount, int charsPerPage)
    {
        int capacity = charsPerPage < 8 ? 8 : charsPerPage;
        List<string> pages = new List<string>();
        StringBuilder page = new StringBuilder();
        int count = entries == null ? 0 : entries.Count;
        int found = foundCount < 0 ? 0 : (foundCount > count ? count : foundCount);
        for (int i = 0; i < count; i++)
        {
            string text = i < found && entries[i] != null ? entries[i].Trim() : TornPage;
            Append(text, capacity, page, pages);
        }
        if (page.Length > 0)
        {
            pages.Add(page.ToString());
        }

        List<LogSpread> spreads = new List<LogSpread>();
        if (pages.Count == 0)
        {
            spreads.Add(new LogSpread { left = "", right = "" });
            return spreads;
        }
        for (int i = 0; i < pages.Count; i += 2)
        {
            spreads.Add(new LogSpread { left = pages[i], right = i + 1 < pages.Count ? pages[i + 1] : "" });
        }
        return spreads;
    }

    static void Append(string text, int capacity, StringBuilder page, List<string> pages)
    {
        // Whole entry fits on the current page.
        int needed = (page.Length > 0 ? Gap.Length : 0) + text.Length;
        if (page.Length + needed <= capacity)
        {
            if (page.Length > 0)
            {
                page.Append(Gap);
            }
            page.Append(text);
            return;
        }
        // Fits on a fresh page: start one.
        if (text.Length <= capacity)
        {
            pages.Add(page.ToString());
            page.Length = 0;
            page.Append(text);
            return;
        }
        // Too long for any page: fill what is left of this page, then whole pages.
        string rest = text;
        if (page.Length > 0)
        {
            int room = capacity - page.Length - Gap.Length;
            if (room >= 12)
            {
                int cut = CutAt(rest, room);
                page.Append(Gap).Append(rest.Substring(0, cut).TrimEnd());
                rest = rest.Substring(cut).TrimStart();
            }
            pages.Add(page.ToString());
            page.Length = 0;
        }
        while (rest.Length > capacity)
        {
            int cut = CutAt(rest, capacity);
            pages.Add(rest.Substring(0, cut).TrimEnd());
            rest = rest.Substring(cut).TrimStart();
        }
        page.Append(rest);
    }

    // Largest cut <= limit that lands after a space; a single word longer than the limit is cut hard.
    static int CutAt(string text, int limit)
    {
        if (text.Length <= limit)
        {
            return text.Length;
        }
        int space = text.LastIndexOf(' ', limit);
        return space > 0 ? space + 1 : limit;
    }
}
}
