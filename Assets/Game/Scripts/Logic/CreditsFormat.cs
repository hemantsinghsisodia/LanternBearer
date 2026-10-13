using System;
using System.Collections.Generic;

namespace LanternKeeper
{
public struct CreditEntry { public string heading; public string title; public string author; public string licence; }

// Reads the text of CREDITS.txt into entries for the credits roll. Paths and URLs are dropped.
// A section is a heading line between two `---` rules. Its licence comes from the parenthesis in the heading:
// "(CC0...)" is a CC0 section, "(SIL Open Font License ...)" is a font section. Any other section (made for
// this project, Unity, editor only) is skipped.
public static class CreditsFormat
{
    class Section { public string heading; public List<string> lines = new List<string>(); }

    public static List<CreditEntry> Parse(string raw)
    {
        var result = new List<CreditEntry>();
        if (string.IsNullOrEmpty(raw)) return result;
        foreach (Section s in Split(raw.Replace("\r", "").Split('\n')))
        {
            string heading = Before(s.heading, '(').Trim();
            string inner = Inside(s.heading);
            if (inner.StartsWith("CC0", StringComparison.Ordinal)) ParseCc0(s, heading, "CC0", result);
            else if (inner.IndexOf("Open Font License", StringComparison.Ordinal) >= 0)
                ParseFonts(s, "Fonts", inner.Replace("SIL Open Font License", "SIL OFL"), result);
        }
        return result;
    }

    static List<Section> Split(string[] lines)
    {
        var sections = new List<Section>();
        var cur = new Section { heading = "" };
        sections.Add(cur);
        for (int i = 0; i < lines.Length; i++)
        {
            string t = lines[i].Trim();
            if (IsRule(t))
            {
                if (i + 2 < lines.Length && lines[i + 1].Trim().Length > 0 && !IsRule(lines[i + 1].Trim()) && IsRule(lines[i + 2].Trim()))
                {
                    cur = new Section { heading = lines[i + 1].Trim() };
                    sections.Add(cur);
                    i += 2;
                }
                continue;
            }
            cur.lines.Add(lines[i]);
        }
        return sections;
    }

    static void ParseCc0(Section s, string heading, string sectionLicence, List<CreditEntry> result)
    {
        string titleLine = null;       // last indented "Title by Author" line of a block
        string subHeading = null;      // current subsection of a list, e.g. "Models"
        var subAuthors = new List<string>();
        for (int li = 0; li < s.lines.Count; li++)
        {
            string line = s.lines[li];
            string t = line.Trim();
            bool indented = line.Length > 0 && char.IsWhiteSpace(line[0]);
            if (t.Length == 0) { titleLine = null; continue; }
            if (t.StartsWith("Licence:", StringComparison.Ordinal))
            {
                string licence = Before(t.Substring("Licence:".Length), '(').Trim();
                if (titleLine != null && licence.Length > 0)
                {
                    SplitTitle(titleLine, out string title, out string author);
                    result.Add(new CreditEntry { heading = heading, title = title, author = author, licence = licence });
                }
                titleLine = null;
                continue;
            }
            if (indented)
            {
                if (!t.Contains("http") && !t.Contains("Assets/")) titleLine = t;
                continue;
            }
            if (t.EndsWith(":", StringComparison.Ordinal))
            {
                FlushSub(heading, subHeading, subAuthors, sectionLicence, result);
                subHeading = Before(t.TrimEnd(':'), '(').Trim();
                subAuthors = new List<string>();
                continue;
            }
            if (t.StartsWith("- ", StringComparison.Ordinal))
            {
                if (subHeading != null) AddAuthors(subAuthors, BulletAuthors(t.Substring(2)));
                continue;
            }
            if (subHeading != null || t.Contains("Assets/")) continue;
            int dash = t.IndexOf(" — ", StringComparison.Ordinal);
            string head = dash >= 0 ? t.Substring(0, dash) : t;
            if (t.StartsWith("\"", StringComparison.Ordinal))
            {
                int close = head.IndexOf('"', 1);
                if (close < 0) continue;
                string rest = head.Substring(close + 1).Trim();
                if (!rest.StartsWith("by ", StringComparison.Ordinal)) continue;
                string author = Before(rest.Substring(3), ',').Trim();
                result.Add(new CreditEntry { heading = heading, title = head.Substring(1, close - 1), author = author, licence = sectionLicence });
            }
            else if (head.Contains(" by ") && !head.Contains("http") && ParagraphHasSource(s.lines, li))
            {
                SplitTitle(head, out string title, out string author);
                result.Add(new CreditEntry { heading = heading, title = title, author = author, licence = sectionLicence });
            }
        }
        FlushSub(heading, subHeading, subAuthors, sectionLicence, result);
    }

    static void FlushSub(string heading, string sub, List<string> authors, string licence, List<CreditEntry> result)
    {
        if (sub == null || authors.Count == 0) return;
        string shown = LeadingCapitalWords(heading);
        result.Add(new CreditEntry { heading = shown, title = sub, author = string.Join(", ", authors), licence = licence });
    }

    // A real credit line has a Licence: line or a URL in its own paragraph (this line to the next blank line).
    // Prose such as "Animations used by the keeper ..." has neither.
    static bool ParagraphHasSource(List<string> lines, int index)
    {
        for (int i = index; i < lines.Count && lines[i].Trim().Length > 0; i++)
            if (lines[i].Contains("http") || lines[i].TrimStart().StartsWith("Licence:", StringComparison.Ordinal)) return true;
        return false;
    }

    // "Poly Haven models, textures, and HDRIs" -> "Poly Haven": the leading run of capitalised words.
    static string LeadingCapitalWords(string heading)
    {
        var kept = new List<string>();
        foreach (string w in heading.Split(' '))
        {
            if (w.Length == 0 || !char.IsUpper(w[0])) break;
            kept.Add(w);
        }
        return kept.Count > 0 ? string.Join(" ", kept) : heading;
    }

    // "name, name — A, B" or "Title (id) by A and B".
    static List<string> BulletAuthors(string b)
    {
        string names;
        int dash = b.LastIndexOf(" — ", StringComparison.Ordinal);
        int by = b.LastIndexOf(" by ", StringComparison.Ordinal);
        if (dash >= 0) names = b.Substring(dash + 3);
        else if (by >= 0) names = b.Substring(by + 4);
        else return new List<string>();
        var list = new List<string>();
        foreach (string n in names.Replace(" and ", ", ").Split(','))
        {
            string name = n.Trim();
            if (name.Length > 0 && !name.Contains("http")) list.Add(name);
        }
        return list;
    }

    static void AddAuthors(List<string> into, List<string> more)
    {
        foreach (string a in more) if (!into.Contains(a)) into.Add(a);
    }

    static void ParseFonts(Section s, string heading, string licence, List<CreditEntry> result)
    {
        var para = new List<string>();
        for (int i = 0; i <= s.lines.Count; i++)
        {
            string t = i < s.lines.Count ? s.lines[i].Trim() : "";
            if (t.Length > 0) { para.Add(t); continue; }
            if (para.Count > 0) FontParagraph(para, heading, licence, result);
            para.Clear();
        }
    }

    static void FontParagraph(List<string> para, string heading, string licence, List<CreditEntry> result)
    {
        string first = para[0];
        if (first.Contains("http") || first.Contains("Assets/")) return;
        if (first.Contains(" by "))
        {
            SplitTitle(first, out string title, out string author);
            author = author.TrimEnd('.').Trim();
            if (author.StartsWith("the ", StringComparison.Ordinal)) author = char.ToUpperInvariant(author[4]) + author.Substring(5);
            result.Add(new CreditEntry { heading = heading, title = title, author = author, licence = licence });
            return;
        }
        var holders = new List<string>();
        foreach (string l in para)
        {
            const string prefix = "copyright (c) ";
            int at = l.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (at < 0) continue;
            string rest = l.Substring(at + prefix.Length).Trim();
            int sp = rest.IndexOf(' ');
            if (sp > 0 && char.IsDigit(rest[0])) rest = rest.Substring(sp + 1).Trim();
            if (rest.Length > 0 && !holders.Contains(rest)) holders.Add(rest);
        }
        if (holders.Count > 0)
            result.Add(new CreditEntry { heading = heading, title = first, author = string.Join(", ", holders), licence = licence });
    }

    static string Before(string s, char c)
    {
        int i = s.IndexOf(c);
        return i < 0 ? s : s.Substring(0, i);
    }

    static string Inside(string s)
    {
        int a = s.IndexOf('(');
        int b = s.LastIndexOf(')');
        return a >= 0 && b > a ? s.Substring(a + 1, b - a - 1).Trim() : "";
    }

    static bool IsRule(string s)
    {
        if (s.Length < 3) return false;
        for (int i = 0; i < s.Length; i++) if (s[i] != '-') return false;
        return true;
    }

    static void SplitTitle(string line, out string title, out string author)
    {
        int by = line.LastIndexOf(" by ", StringComparison.Ordinal);
        if (by < 0) { title = line; author = ""; return; }
        title = line.Substring(0, by).Trim();
        author = line.Substring(by + 4).Trim();
        // A note ending in a digit, such as "(CC0)", is dropped. Keep a named person in brackets ("(Rasmus Andersson)"); drop notes such as "(first 83 s, looped ...)".
        int paren = author.IndexOf(" (", StringComparison.Ordinal);
        if (paren >= 0)
        {
            string note = author.Substring(paren + 2).TrimEnd(')', '.');
            bool person = author.TrimEnd('.').EndsWith(")", StringComparison.Ordinal) && IsPersonNote(note);
            if (!person) author = author.Substring(0, paren).Trim();
        }
    }

    static bool IsPersonNote(string s)
    {
        return s.Length > 0 && char.IsUpper(s[0]) && s.IndexOf(',') < 0 && !char.IsDigit(s[s.Length - 1]);
    }
}
}
