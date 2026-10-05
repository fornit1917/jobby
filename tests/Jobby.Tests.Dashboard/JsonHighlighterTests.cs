using Jobby.Dashboard.Client.Shared;

namespace Jobby.Tests.Dashboard;

public class JsonHighlighterTests
{
    [Fact]
    public void Wraps_KeysStringsNumbersBoolsAndNullsInSpans()
    {
        var html = JsonHighlighter.Highlight("""{"name":"jobby","count":42,"on":true,"x":null}""");

        Assert.Contains("jobby-json-key", html);
        Assert.Contains("jobby-json-str", html);
        Assert.Contains("jobby-json-num", html);
        Assert.Contains("jobby-json-bool", html);
        Assert.Contains("jobby-json-null", html);
    }

    [Fact]
    public void Escapes_HtmlSpecialCharacters()
    {
        var html = JsonHighlighter.Highlight("""{"html":"<b>&</b>"}""");
        Assert.DoesNotContain("<b>", html);
        Assert.Contains("&lt;b&gt;", html);
    }

    [Fact]
    public void Non_JsonIsReturnedHtmlEscapedWithoutThrowing()
    {
        var html = JsonHighlighter.Highlight("not json <x>");
        Assert.Contains("not json", html);
        Assert.Contains("&lt;x&gt;", html);
    }

    [Fact]
    public void Pretty_PrintsNestedJsonWithNewlinesAndIndentation()
    {
        var html = JsonHighlighter.Highlight("""{"a":{"b":1}}""");
        Assert.Contains("\n", html);
        Assert.Contains("\n  ", html);
        Assert.Contains("\n    ", html);
    }
}