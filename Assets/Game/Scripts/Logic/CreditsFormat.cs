using System;
using System.Collections.Generic;

namespace LanternKeeper
{
public struct CreditEntry { public string heading; public string title; public string author; public string licence; }

// Reads the text of CREDITS.txt into entries. Paths and URLs are dropped.
public static class CreditsFormat
{
    public static List<CreditEntry> Parse(string raw)
    {
        var result = new List<CreditEntry>();
        if (string.IsNullOrEmpty(raw)) return result;
        string[] lines = raw.Replace("\r", "").Split('\n');
        string heading = "";
        string titleLine = null;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            string trimmed = line.Trim();
            if (IsRule(trimmed))
            {
                // A rule, a heading line and a closing rule.
                if (i + 2 < lines.Length && !IsRule(lines[i + 1].Trim()) && lines[i + 1].Trim().Length > 0 && IsRule(lines[i + 2].Trim()))
                {
                    heading = lines[i + 1].Trim();
                    i += 2;
                }
                titleLine = null;
                continue;
            }
            if (trimmed.Length == 0) { titleLine = null; continue; }
            if (trimmed.StartsWith("Licence:", StringComparison.Ordinal))
            {
                string licence = trimmed.Substring("Licence:".Length).Trim();
                if (titleLine != null && licence.Length > 0)
                {
                    SplitTitle(titleLine, out string title, out string author);
                    result.Add(new CreditEntry { heading = heading, title = title, author = author, licence = licence });
                }
                titleLine = null;
                continue;
            }
            bool indented = line.Length > 0 && char.IsWhiteSpace(line[0]);
            if (indented && !trimmed.Contains("http") && !trimmed.Contains("Assets/")) titleLine = trimmed;
        }
        return result;
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
        int paren = author.IndexOf(" (", StringComparison.Ordinal);
        if (paren >= 0) author = author.Substring(0, paren).Trim();
    }
}
}
