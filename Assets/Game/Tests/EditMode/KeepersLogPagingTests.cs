using System.Collections.Generic;
using NUnit.Framework;

namespace LanternKeeper.Tests
{
public class KeepersLogPagingTests
{
    static string Words(int chars)
    {
        System.Text.StringBuilder b = new System.Text.StringBuilder();
        while (b.Length < chars)
        {
            b.Append("lantern ");
        }
        return b.ToString().Substring(0, chars).TrimEnd();
    }

    static int Count(string text, string part)
    {
        int n = 0;
        int i = 0;
        while ((i = text.IndexOf(part, i, System.StringComparison.Ordinal)) >= 0)
        {
            n++;
            i += part.Length;
        }
        return n;
    }

    [Test]
    public void UnfoundShowTornPage()
    {
        List<LogSpread> spreads = LogPaging.Paginate(new[] { "one", "two", "three", "four" }, 2, 400);
        Assert.AreEqual(1, spreads.Count);
        string all = spreads[0].left + "|" + spreads[0].right;
        StringAssert.Contains("one", all);
        StringAssert.Contains("two", all);
        StringAssert.DoesNotContain("three", all);
        StringAssert.DoesNotContain("four", all);
        Assert.AreEqual(2, Count(all, LogPaging.TornPage));
    }

    [Test]
    public void OverflowFlowsToNextSpread()
    {
        List<LogSpread> spreads = LogPaging.Paginate(new[] { Words(270) }, 1, 100);
        Assert.AreEqual(2, spreads.Count, "about 3 pages = 2 spreads (word-boundary cuts leave slack)");
        Assert.IsNotEmpty(spreads[0].left);
        Assert.IsNotEmpty(spreads[0].right);
        Assert.IsNotEmpty(spreads[1].left);
        Assert.IsEmpty(spreads[1].right);
    }

    [Test]
    public void NoPageExceedsCapacityAndNoTextIsLost()
    {
        string[] entries = { Words(250), Words(90), Words(40), "short" };
        List<LogSpread> spreads = LogPaging.Paginate(entries, 4, 120);
        int letters = 0;
        foreach (LogSpread s in spreads)
        {
            Assert.LessOrEqual(s.left.Length, 120);
            Assert.LessOrEqual(s.right.Length, 120);
            letters += s.left.Replace("\n", "").Replace(" ", "").Length + s.right.Replace("\n", "").Replace(" ", "").Length;
        }
        int expected = 0;
        foreach (string e in entries)
        {
            expected += e.Replace(" ", "").Length;
        }
        Assert.AreEqual(expected, letters);
    }

    [Test]
    public void SmallerCapacityMakesMorePages()
    {
        string[] entries = { Words(200), Words(200), Words(200) };
        Assert.Greater(LogPaging.Paginate(entries, 3, 150).Count, LogPaging.Paginate(entries, 3, 300).Count);
    }

    [Test]
    public void EmptyIslandHasOneSpread()
    {
        List<LogSpread> spreads = LogPaging.Paginate(new string[0], 0, 400);
        Assert.AreEqual(1, spreads.Count);
        Assert.IsEmpty(spreads[0].left);
        Assert.IsEmpty(spreads[0].right);
    }

    [Test]
    public void FoundCountIsClamped()
    {
        List<LogSpread> spreads = LogPaging.Paginate(new[] { "a", "b" }, 9, 400);
        StringAssert.DoesNotContain(LogPaging.TornPage, spreads[0].left);
        spreads = LogPaging.Paginate(new[] { "a", "b" }, -3, 400);
        Assert.AreEqual(2, Count(spreads[0].left, LogPaging.TornPage));
    }
}
}
