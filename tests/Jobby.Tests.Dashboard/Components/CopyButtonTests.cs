using Bunit;
using Jobby.Dashboard.Client.Shared;

namespace Jobby.Tests.Dashboard.Components;

public class CopyButtonTests : MudTestContext
{
    [Fact]
    public void Clicking_WritesTextToClipboard()
    {
        var cut = Render<CopyButton>(p => p.Add(c => c.Text, "hello world"));

        cut.Find("button").Click();

        var invocation = JSInterop.VerifyInvoke("navigator.clipboard.writeText");
        Assert.Equal("hello world", invocation.Arguments[0]);
    }

    [Fact]
    public void Button_IsDisabledWhenTextEmpty()
    {
        var cut = Render<CopyButton>(p => p.Add(c => c.Text, ""));

        Assert.True(cut.Find("button").HasAttribute("disabled"));
    }
}