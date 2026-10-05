using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard.Client;
using Jobby.Dashboard.Client.Localization;
using Microsoft.JSInterop;

namespace Jobby.Tests.Dashboard.Globalization;

public class LanguageStateReviewFixesTests
{
    private static LanguageState New() => new(new DashboardClientConfig());

    [Fact]
    public void SeedBootLanguage_AppliesValidCodeAsOverride()
    {
        var l = New();
        l.SeedBootLanguage("ru");
        Assert.True(l.HasUserOverride);
        Assert.Equal(DashboardLanguage.Russian, l.Language);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("de")]
    public void SeedBootLanguage_IgnoresUnknownOrNull(string? code)
    {
        var l = New();
        l.SeedBootLanguage(code);
        Assert.False(l.HasUserOverride);
        Assert.Equal(DashboardLanguage.English, l.Language);
    }

    [Fact]
    public async Task SetAsync_RejectsUndefinedEnum()
    {
        var l = New();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => l.SetAsync(null!, (DashboardLanguage)999));
        Assert.False(l.HasUserOverride);
        Assert.Equal(DashboardLanguage.English, l.Language);
    }

    [Fact]
    public async Task InitializeAsync_RunsOnceAndRaisesChangedOnlyOnChange()
    {
        var l = New();
        var raised = 0;
        l.Changed += () => raised++;
        var js = new StubJs("{\"v\":1,\"lang\":\"ru\"}");

        await l.InitializeAsync(js);
        Assert.Equal(DashboardLanguage.Russian, l.Language);
        Assert.Equal(1, raised);
        Assert.Equal(1, js.GetItemCalls);

        await l.InitializeAsync(js);
        Assert.Equal(1, raised);
        Assert.Equal(1, js.GetItemCalls);
    }

    [Fact]
    public async Task InitializeAsync_NoOverrideNoChangeDoesNotRaise()
    {
        var l = New();
        var raised = 0;
        l.Changed += () => raised++;
        await l.InitializeAsync(new StubJs(null));
        Assert.Equal(0, raised);
        Assert.Equal(DashboardLanguage.English, l.Language);
    }

    private sealed class StubJs : IJSRuntime
    {
        private readonly string? _stored;
        public int GetItemCalls { get; private set; }
        public StubJs(string? stored) => _stored = stored;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            if (identifier == "localStorage.getItem")
            {
                GetItemCalls++;
                return new ValueTask<TValue>((TValue)(object?)_stored!);
            }

            return new ValueTask<TValue>(default(TValue)!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken,
            object?[]? args)
            => InvokeAsync<TValue>(identifier, args);
    }
}
