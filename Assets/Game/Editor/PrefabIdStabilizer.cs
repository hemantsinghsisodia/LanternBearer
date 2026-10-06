using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace LanternKeeper
{
// Makes a rebuilt prefab's text identical to the previous build when nothing changed.
// SaveAsPrefabAsset gives every new object a random fileID, so rebuilding rewrote the whole file. This gives each object a
// stable key (hierarchy path, component type and ordinal) and renumbers the fresh file: an object keeps the fileID it had in
// the previous file, a brand-new one gets a hash of its key. Scenes that override this prefab keep pointing at valid IDs.
// Pure text in, text out, so it is unit-testable.
public static class PrefabIdStabilizer
{
    class Doc
    {
        public int classId;
        public long id;
        public bool stripped;
        public List<string> lines = new List<string>();
        public string key;
    }

    static readonly Regex Header = new Regex(@"^--- !u!(\d+) &(-?\d+)( stripped)?\s*$");
    static readonly Regex LocalRef = new Regex(@"\{fileID: (-?\d+)\}");
    static readonly Regex Guid = new Regex(@"guid: ([0-9a-f]{32})");

    public static string Normalize(string fresh, string previous)
    {
        string preface;
        List<Doc> docs = Parse(fresh, out preface);
        Dictionary<long, Doc> byId = Index(docs);
        AssignKeys(docs, byId);

        Dictionary<string, long> oldIds = new Dictionary<string, long>();
        Dictionary<string, int> oldOrder = new Dictionary<string, int>();
        if (!string.IsNullOrEmpty(previous))
        {
            string ignored;
            List<Doc> oldDocs = Parse(previous, out ignored);
            AssignKeys(oldDocs, Index(oldDocs));
            for (int i = 0; i < oldDocs.Count; i++)
            {
                if (oldDocs[i].key != null && !oldIds.ContainsKey(oldDocs[i].key))
                {
                    oldIds[oldDocs[i].key] = oldDocs[i].id;
                    oldOrder[oldDocs[i].key] = i;
                }
            }
        }

        Dictionary<long, long> map = new Dictionary<long, long>();
        HashSet<long> used = new HashSet<long>();
        for (int i = 0; i < docs.Count; i++)
        {
            long reused;
            if (docs[i].key != null && oldIds.TryGetValue(docs[i].key, out reused) && used.Add(reused))
            {
                map[docs[i].id] = reused;
            }
        }
        for (int i = 0; i < docs.Count; i++)
        {
            if (map.ContainsKey(docs[i].id))
            {
                continue;
            }
            long candidate = HashKey(docs[i].key ?? ("doc#" + i));
            while (!used.Add(candidate))
            {
                candidate++;
            }
            map[docs[i].id] = candidate;
        }

        // Same objects as before: write them in the previous order too.
        bool allKnown = oldOrder.Count > 0;
        for (int i = 0; i < docs.Count && allKnown; i++)
        {
            allKnown = docs[i].key != null && oldOrder.ContainsKey(docs[i].key);
        }
        List<Doc> ordered = new List<Doc>(docs);
        if (allKnown)
        {
            ordered.Sort((a, b) => oldOrder[a.key].CompareTo(oldOrder[b.key]));
        }

        StringBuilder text = new StringBuilder(preface);
        for (int d = 0; d < ordered.Count; d++)
        {
            Doc doc = ordered[d];
            text.Append("--- !u!").Append(doc.classId).Append(" &").Append(map[doc.id]).Append(doc.stripped ? " stripped" : "").Append('\n');
            for (int i = 0; i < doc.lines.Count; i++)
            {
                text.Append(LocalRef.Replace(doc.lines[i], m =>
                {
                    long value = long.Parse(m.Groups[1].Value);
                    long mapped;
                    return value != 0 && map.TryGetValue(value, out mapped) ? "{fileID: " + mapped + "}" : m.Value;
                })).Append('\n');
            }
        }
        return text.ToString();
    }

    static List<Doc> Parse(string text, out string preface)
    {
        List<Doc> docs = new List<Doc>();
        StringBuilder head = new StringBuilder();
        Doc current = null;
        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        int count = lines.Length;
        if (count > 0 && lines[count - 1].Length == 0)
        {
            count--;
        }
        for (int i = 0; i < count; i++)
        {
            Match match = Header.Match(lines[i]);
            if (match.Success)
            {
                current = new Doc
                {
                    classId = int.Parse(match.Groups[1].Value),
                    id = long.Parse(match.Groups[2].Value),
                    stripped = match.Groups[3].Success
                };
                docs.Add(current);
            }
            else if (current == null)
            {
                head.Append(lines[i]).Append('\n');
            }
            else
            {
                current.lines.Add(lines[i]);
            }
        }
        preface = head.ToString();
        return docs;
    }

    static Dictionary<long, Doc> Index(List<Doc> docs)
    {
        Dictionary<long, Doc> byId = new Dictionary<long, Doc>();
        for (int i = 0; i < docs.Count; i++)
        {
            byId[docs[i].id] = docs[i];
        }
        return byId;
    }

    static bool IsTransform(Doc doc)
    {
        return doc.classId == 4 || doc.classId == 224;
    }

    static string Field(Doc doc, string name)
    {
        string prefix = "  " + name + ":";
        for (int i = 0; i < doc.lines.Count; i++)
        {
            if (doc.lines[i].StartsWith(prefix, StringComparison.Ordinal))
            {
                return doc.lines[i].Substring(prefix.Length).Trim();
            }
        }
        return null;
    }

    static long RefOf(string value)
    {
        if (value == null)
        {
            return 0;
        }
        Match match = LocalRef.Match(value);
        return match.Success ? long.Parse(match.Groups[1].Value) : 0;
    }

    // Lines "  - component: {fileID: N}" (a GameObject) or "  - {fileID: N}" (a Transform's children), in order.
    static List<long> RefList(Doc doc, string name, string itemPrefix)
    {
        List<long> list = new List<long>();
        int start = doc.lines.IndexOf("  " + name + ":");
        if (start < 0)
        {
            return list;
        }
        for (int i = start + 1; i < doc.lines.Count && doc.lines[i].StartsWith("  - ", StringComparison.Ordinal); i++)
        {
            string line = doc.lines[i].Substring(4);
            if (itemPrefix.Length > 0)
            {
                if (!line.StartsWith(itemPrefix, StringComparison.Ordinal))
                {
                    continue;
                }
                line = line.Substring(itemPrefix.Length);
            }
            list.Add(RefOf(line));
        }
        return list;
    }

    static void AssignKeys(List<Doc> docs, Dictionary<long, Doc> byId)
    {
        Dictionary<long, string> goKeys = new Dictionary<long, string>();
        for (int i = 0; i < docs.Count; i++)
        {
            if (docs[i].classId == 1)
            {
                GoKey(docs[i], byId, goKeys, 0);
            }
        }
        Dictionary<string, int> ordinals = new Dictionary<string, int>();
        for (int i = 0; i < docs.Count; i++)
        {
            Doc doc = docs[i];
            string baseKey;
            if (doc.classId == 1)
            {
                doc.key = goKeys.ContainsKey(doc.id) ? "go:" + goKeys[doc.id] : null;
                continue;
            }
            long owner = RefOf(Field(doc, "m_GameObject"));
            if (owner != 0 && goKeys.ContainsKey(owner))
            {
                baseKey = goKeys[owner] + ":" + doc.classId;
                if (doc.classId == 114)
                {
                    Match guid = Guid.Match(Field(doc, "m_Script") ?? "");
                    baseKey += "@" + (guid.Success ? guid.Groups[1].Value : "");
                }
            }
            else
            {
                baseKey = "doc:" + doc.classId;
            }
            int n;
            ordinals.TryGetValue(baseKey, out n);
            ordinals[baseKey] = n + 1;
            doc.key = baseKey + "#" + n;
        }
    }

    static string GoKey(Doc go, Dictionary<long, Doc> byId, Dictionary<long, string> cache, int depth)
    {
        string cached;
        if (cache.TryGetValue(go.id, out cached))
        {
            return cached;
        }
        string name = (Field(go, "m_Name") ?? "").Trim('\'', '"');
        string key = name;
        if (depth < 200)
        {
            Doc transform = null;
            foreach (long component in RefList(go, "m_Component", "component: "))
            {
                Doc doc;
                if (byId.TryGetValue(component, out doc) && IsTransform(doc))
                {
                    transform = doc;
                    break;
                }
            }
            long father = transform != null ? RefOf(Field(transform, "m_Father")) : 0;
            Doc fatherDoc;
            if (father != 0 && byId.TryGetValue(father, out fatherDoc))
            {
                Doc parentGo;
                long parentId = RefOf(Field(fatherDoc, "m_GameObject"));
                if (byId.TryGetValue(parentId, out parentGo))
                {
                    int index = RefList(fatherDoc, "m_Children", "").IndexOf(transform.id);
                    key = GoKey(parentGo, byId, cache, depth + 1) + "/" + name + "#" + index;
                }
            }
        }
        cache[go.id] = key;
        return key;
    }

    // FNV-1a over the key, folded into a positive 63-bit number.
    static long HashKey(string key)
    {
        ulong hash = 14695981039346656037UL;
        for (int i = 0; i < key.Length; i++)
        {
            hash ^= key[i];
            hash *= 1099511628211UL;
        }
        long value = (long)(hash & 0x7FFFFFFFFFFFFFFFUL);
        return value == 0 ? 1 : value;
    }
}
}
